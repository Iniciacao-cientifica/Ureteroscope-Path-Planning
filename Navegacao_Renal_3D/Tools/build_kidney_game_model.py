"""Build and validate the v001 kidney game asset for Maya and Unity.

The source geometry is authored in millimetres with Y up. OBJ files are
written in centimetres for Maya. Maya is then used headlessly to create the
editable .ma master and the single authoritative FBX copied to Unity.

This is a game-oriented anatomical model, not patient-specific anatomy.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import shutil
import subprocess
import sys
import tempfile
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path

import numpy as np
import pyvista as pv
from scipy.ndimage import distance_transform_edt, gaussian_filter
from skimage.measure import marching_cubes


VERSION = "v001"
MODEL_NAME = f"Kidney_Game_{VERSION}"
TIP_RADIUS_MM = 2.0
STONE_DIAMETER_MM = 6.0
GAMEPLAY_SCALE = 5.0
GRID_SPACING_MM = 0.9


@dataclass(frozen=True)
class Material:
    name: str
    color: tuple[float, float, float]
    opacity: float = 1.0


MATERIALS = {
    "exterior": Material("MAT_KidneyExterior", (0.48, 0.055, 0.095), 0.34),
    "interior": Material("MAT_CollectingSystem", (0.83, 0.22, 0.31), 1.0),
    "collision": Material("MAT_CollisionDebug", (0.06, 0.64, 0.86), 0.18),
    "route": Material("MAT_RouteCyan", (0.00, 0.78, 1.00), 1.0),
    "stone": Material("MAT_StoneGold", (0.92, 0.56, 0.08), 1.0),
}


def ellipsoid_field(x, y, z, center, radii):
    center = np.asarray(center, dtype=np.float32)
    radii = np.asarray(radii, dtype=np.float32)
    return (
        ((x - center[0]) / radii[0]) ** 2
        + ((y - center[1]) / radii[1]) ** 2
        + ((z - center[2]) / radii[2]) ** 2
    )


def tapered_segment_mask(x, y, z, start, end, radius_start, radius_end):
    start = np.asarray(start, dtype=np.float32)
    end = np.asarray(end, dtype=np.float32)
    segment = end - start
    length_squared = float(np.dot(segment, segment))
    px, py, pz = x - start[0], y - start[1], z - start[2]
    t = np.clip(
        (px * segment[0] + py * segment[1] + pz * segment[2]) / length_squared,
        0.0,
        1.0,
    )
    dx = px - t * segment[0]
    dy = py - t * segment[1]
    dz = pz - t * segment[2]
    radius = radius_start + (radius_end - radius_start) * t
    return dx * dx + dy * dy + dz * dz <= radius * radius


def volume_to_mesh(volume, origin, spacing, smoothing=25, pass_band=0.09):
    vertices, faces, _, _ = marching_cubes(
        volume.astype(np.float32), level=0.5, spacing=(spacing,) * 3
    )
    vertices += np.asarray(origin, dtype=np.float32)
    vtk_faces = np.column_stack(
        (np.full(len(faces), 3, dtype=np.int64), faces)
    ).ravel()
    mesh = pv.PolyData(vertices, vtk_faces).clean().triangulate()
    if smoothing:
        mesh = mesh.smooth(
            n_iter=smoothing,
            relaxation_factor=0.045,
            feature_smoothing=False,
            boundary_smoothing=True,
        )
    return mesh.compute_normals(
        point_normals=True,
        cell_normals=False,
        consistent_normals=True,
        auto_orient_normals=True,
    )


def build_exterior(x, y, z):
    # Curved longitudinal axis and asymmetric poles create a natural renal
    # silhouette while the medial subtraction creates the hilum/bean concavity.
    curved_x = x - 0.055 * y - 0.0008 * y * y
    body = ellipsoid_field(curved_x, y, z, (5.0, 0.0, 1.0), (47.0, 66.0, 28.0)) <= 1.0
    lateral_bulge = ellipsoid_field(x, y, z, (20.0, -1.0, 1.5), (38.0, 57.0, 29.0)) <= 1.0
    upper_pole = ellipsoid_field(x, y, z, (3.0, 43.0, -1.0), (39.0, 30.0, 26.0)) <= 1.0
    lower_pole = ellipsoid_field(x, y, z, (9.0, -44.0, 2.0), (37.0, 31.0, 25.0)) <= 1.0
    exterior = body | lateral_bulge | upper_pole | lower_pole

    # The subtraction spans the anterior/posterior thickness so the medial
    # concavity remains visible in the game silhouette instead of becoming a
    # shallow dimple hidden by the anterior surface.
    hilum = ellipsoid_field(x, y, z, (-43.0, 1.0, -1.0), (27.0, 27.0, 42.0)) <= 1.0
    exterior &= ~hilum

    exterior = gaussian_filter(exterior.astype(np.float32), sigma=1.35) >= 0.49
    return exterior


def build_collecting_system(x, y, z):
    collecting = np.zeros(np.broadcast_shapes(x.shape, y.shape, z.shape), dtype=bool)

    # Ureter -> pelvis. Radii stay above the 8 mm minimum passage requirement.
    segments = [
        ((-43, -88, -4), (-42, -67, -3), 4.4, 4.6),
        ((-42, -67, -3), (-38, -45, -2), 4.6, 5.2),
        ((-38, -45, -2), (-31, -24, -1), 5.2, 6.1),
        ((-31, -24, -1), (-24, -8, 0), 6.1, 8.3),
        # Upper infundibulum and minor calyces.
        ((-23, -3, 0), (-14, 17, 0), 8.0, 6.6),
        ((-14, 17, 0), (-2, 34, 0), 6.6, 5.4),
        ((-2, 34, 0), (14, 49, 6), 5.4, 4.3),
        ((14, 49, 6), (27, 56, 10), 4.3, 4.1),
        ((5, 40, 1), (23, 46, -9), 4.9, 4.1),
        ((-5, 30, -1), (14, 35, 13), 4.8, 4.0),
        # Middle infundibulum; the positive-Z terminal is the fixed target.
        ((-21, 1, 0), (-4, 5, 1), 8.2, 6.4),
        ((-4, 5, 1), (13, 10, 3), 6.4, 5.0),
        ((13, 10, 3), (30, 15, 7), 5.0, 4.2),
        ((5, 7, 0), (24, 3, -12), 4.9, 4.1),
        ((1, 5, 1), (20, 18, 15), 4.8, 4.0),
        # Lower infundibulum and minor calyces.
        ((-23, -9, 0), (-13, -25, 0), 7.7, 6.3),
        ((-13, -25, 0), (0, -40, 0), 6.3, 5.2),
        ((0, -40, 0), (17, -54, 7), 5.2, 4.2),
        ((17, -54, 7), (27, -59, 10), 4.2, 4.0),
        ((6, -45, 0), (24, -49, -11), 4.8, 4.0),
        ((-3, -36, -1), (15, -42, 14), 4.7, 4.0),
    ]
    for start, end, radius_start, radius_end in segments:
        collecting |= tapered_segment_mask(
            x, y, z, start, end, radius_start, radius_end
        )

    # Broad renal pelvis and flattened cup-like terminal chambers.
    collecting |= ellipsoid_field(x, y, z, (-23.0, -4.0, 0.0), (14.5, 22.0, 11.5)) <= 1.0
    terminals = [
        ((27, 56, 10), (6.8, 5.5, 6.2)),
        ((23, 46, -9), (6.6, 5.6, 6.1)),
        ((14, 35, 13), (6.3, 5.5, 5.8)),
        ((30, 15, 7), (7.0, 5.8, 6.4)),
        ((24, 3, -12), (6.6, 5.7, 6.0)),
        ((20, 18, 15), (6.3, 5.5, 5.8)),
        ((27, -59, 10), (6.7, 5.4, 6.0)),
        ((24, -49, -11), (6.4, 5.6, 5.9)),
        ((15, -42, 14), (6.2, 5.4, 5.7)),
    ]
    for center, radii in terminals:
        collecting |= ellipsoid_field(x, y, z, center, radii) <= 1.0

    collecting = gaussian_filter(collecting.astype(np.float32), sigma=0.92) >= 0.43
    return collecting, segments, terminals


def make_route():
    control = np.asarray(
        [
            (-43, -86, -4),
            (-42, -72, -3.3),
            (-41, -58, -2.6),
            (-38, -45, -2),
            (-34, -33, -1.5),
            (-31, -24, -1),
            (-27, -15, -0.4),
            (-23, -7, 0),
            (-19, -1, 0.3),
            (-12, 2.5, 0.7),
            (-4, 5, 1),
            (5, 7.5, 2),
            (13, 10, 3),
            (21, 12.5, 5),
            (27.5, 14.5, 6.5),
        ],
        dtype=np.float32,
    )
    centerline = pv.Spline(control, n_points=360)
    tube = centerline.tube(radius=0.85, n_sides=12, capping=True).triangulate().clean()
    return control, np.asarray(centerline.points), tube


def make_stone(center=(30.0, 15.0, 7.0)):
    center = np.asarray(center, dtype=np.float64)
    stone = pv.Icosphere(radius=STONE_DIAMETER_MM / 2, center=center, nsub=4)
    local = stone.points - center
    direction = local / np.maximum(np.linalg.norm(local, axis=1)[:, None], 1e-8)
    distortion = (
        1.0
        + 0.12 * np.sin(direction[:, 0] * 8.1 + direction[:, 1] * 4.3)
        + 0.07 * np.cos(direction[:, 2] * 11.0 - direction[:, 0] * 3.2)
    )
    stone.points = center + local * distortion[:, None]
    return stone.triangulate().compute_normals(
        point_normals=True, cell_normals=False, consistent_normals=True
    )


def build_geometry():
    bounds = (-76.0, 66.0, -99.0, 76.0, -39.0, 39.0)
    spacing = GRID_SPACING_MM
    xs = np.arange(bounds[0], bounds[1] + spacing, spacing, dtype=np.float32)
    ys = np.arange(bounds[2], bounds[3] + spacing, spacing, dtype=np.float32)
    zs = np.arange(bounds[4], bounds[5] + spacing, spacing, dtype=np.float32)
    x, y, z = np.meshgrid(xs, ys, zs, indexing="ij", sparse=True)

    exterior_volume = build_exterior(x, y, z)
    collecting_volume, segments, terminals = build_collecting_system(x, y, z)
    origin = (float(xs[0]), float(ys[0]), float(zs[0]))

    exterior = volume_to_mesh(exterior_volume, origin, spacing, 42, 0.085)
    collecting = volume_to_mesh(collecting_volume, origin, spacing, 38, 0.075)
    collision = collecting.decimate_pro(0.76, preserve_topology=True).clean().triangulate()
    collision = collision.compute_normals(
        point_normals=True,
        cell_normals=False,
        consistent_normals=True,
        auto_orient_normals=True,
    )
    route_control, route_samples, route = make_route()
    stone = make_stone()

    meshes = {
        "KidneyExterior": exterior,
        "CollectingSystemVisual": collecting,
        "CollectingSystemCollision_Inward": collision,
        "RouteGuide": route,
        "Stone": stone,
    }
    build_data = {
        "origin": origin,
        "spacing": spacing,
        "collecting_volume": collecting_volume,
        "route_control": route_control,
        "route_samples": route_samples,
        "segments": segments,
        "terminals": terminals,
    }
    return meshes, build_data


def mesh_arrays(mesh, centimetres=True, inward=False):
    mesh = mesh.triangulate()
    # Always copy: PyVista may expose its live point buffer through np.asarray.
    # Scaling a view here used to shrink marching-cubes meshes every time an
    # additional OBJ was written, while tube meshes happened to remain intact.
    vertices = np.array(mesh.points, dtype=np.float64, copy=True)
    if centimetres:
        vertices *= 0.1
    faces = np.asarray(mesh.faces, dtype=np.int64).reshape(-1, 4)[:, 1:]
    if inward:
        faces = faces[:, [0, 2, 1]]
    raw = np.asarray(mesh.points, dtype=np.float64)
    face_normals = np.cross(
        raw[faces[:, 1]] - raw[faces[:, 0]],
        raw[faces[:, 2]] - raw[faces[:, 0]],
    )
    normals = np.zeros_like(raw)
    for corner in range(3):
        np.add.at(normals, faces[:, corner], face_normals)
    normals /= np.maximum(np.linalg.norm(normals, axis=1)[:, None], 1e-12)
    return vertices, faces, normals


def write_mtl(path):
    lines = ["# Navegacao Renal 3D - game model v001"]
    for material in MATERIALS.values():
        lines.extend(
            [
                f"newmtl {material.name}",
                f"Kd {material.color[0]:.6f} {material.color[1]:.6f} {material.color[2]:.6f}",
                "Ka 0.060000 0.060000 0.060000",
                "Ks 0.250000 0.250000 0.250000",
                "Ns 80.000000",
                f"d {material.opacity:.6f}",
                "illum 2",
                "",
            ]
        )
    path.write_text("\n".join(lines), encoding="utf-8", newline="\n")


def append_obj(lines, name, mesh, material, vertex_offset, normal_offset, inward=False):
    vertices, faces, normals = mesh_arrays(mesh, centimetres=True, inward=inward)
    lines.extend([f"o {name}", f"g {name}", f"usemtl {material.name}", "s 1"])
    lines.extend(f"v {x:.7f} {y:.7f} {z:.7f}" for x, y, z in vertices)
    lines.extend(f"vn {x:.7f} {y:.7f} {z:.7f}" for x, y, z in normals)
    for a, b, c in faces:
        ia, ib, ic = a + 1 + vertex_offset, b + 1 + vertex_offset, c + 1 + vertex_offset
        na, nb, nc = a + 1 + normal_offset, b + 1 + normal_offset, c + 1 + normal_offset
        lines.append(f"f {ia}//{na} {ib}//{nb} {ic}//{nc}")
    lines.append("")
    return vertex_offset + len(vertices), normal_offset + len(normals)


def write_obj(path, entries):
    lines = ["# Navegacao Renal 3D - centimetres, Y up", "mtllib Kidney_Game_v001.mtl", ""]
    vo = no = 0
    for name, mesh, material, inward in entries:
        vo, no = append_obj(lines, name, mesh, material, vo, no, inward)
    path.write_text("\n".join(lines), encoding="utf-8", newline="\n")


def export_obj_set(obj_dir, meshes):
    obj_dir.mkdir(parents=True, exist_ok=True)
    write_mtl(obj_dir / f"{MODEL_NAME}.mtl")
    entries = [
        ("KidneyExterior", meshes["KidneyExterior"], MATERIALS["exterior"], False),
        ("CollectingSystemVisual", meshes["CollectingSystemVisual"], MATERIALS["interior"], False),
        ("CollectingSystemCollision_Inward", meshes["CollectingSystemCollision_Inward"], MATERIALS["collision"], True),
        ("RouteGuide", meshes["RouteGuide"], MATERIALS["route"], False),
        ("Stone", meshes["Stone"], MATERIALS["stone"], False),
    ]
    write_obj(obj_dir / f"{MODEL_NAME}.obj", entries)
    for entry in entries:
        write_obj(obj_dir / f"{entry[0]}.obj", [entry])


def mesh_topology(mesh):
    edges = mesh.extract_feature_edges(
        boundary_edges=True,
        non_manifold_edges=True,
        feature_edges=False,
        manifold_edges=False,
    )
    return {
        "vertices": int(mesh.n_points),
        "triangles": int(mesh.n_cells),
        "open_or_nonmanifold_edge_cells": int(edges.n_cells),
        "bounds_mm": [round(float(v), 3) for v in mesh.bounds],
    }


def validate_geometry(meshes, build_data):
    volume = build_data["collecting_volume"]
    spacing = build_data["spacing"]
    origin = np.asarray(build_data["origin"])
    distance_inside = distance_transform_edt(volume) * spacing
    samples = build_data["route_samples"]
    indices = np.rint((samples - origin) / spacing).astype(int)
    indices[:, 0] = np.clip(indices[:, 0], 0, volume.shape[0] - 1)
    indices[:, 1] = np.clip(indices[:, 1], 0, volume.shape[1] - 1)
    indices[:, 2] = np.clip(indices[:, 2], 0, volume.shape[2] - 1)
    clearance = distance_inside[indices[:, 0], indices[:, 1], indices[:, 2]]

    topology = {name: mesh_topology(mesh) for name, mesh in meshes.items()}
    all_closed = all(
        stats["open_or_nonmanifold_edge_cells"] == 0 for stats in topology.values()
    )
    minimum_clearance = float(np.min(clearance))
    route_inside = bool(np.all(clearance > 0.0))
    stone_center = np.asarray((30.0, 15.0, 7.0))
    stone_idx = np.rint((stone_center - origin) / spacing).astype(int)
    stone_inside = bool(volume[tuple(stone_idx)])

    checks = {
        "all_meshes_closed_and_manifold": all_closed,
        "route_fully_inside_collecting_system": route_inside,
        "route_minimum_centerline_clearance_mm": round(minimum_clearance, 3),
        "required_clearance_mm": TIP_RADIUS_MM + 0.5,
        "route_clearance_pass": minimum_clearance >= TIP_RADIUS_MM + 0.5,
        "stone_center_inside_target_calyx": stone_inside,
        "source_is_real_scale": True,
    }
    passed = all(
        [
            checks["all_meshes_closed_and_manifold"],
            checks["route_fully_inside_collecting_system"],
            checks["route_clearance_pass"],
            checks["stone_center_inside_target_calyx"],
        ]
    )
    return {"passed": passed, "checks": checks, "meshes": topology}


def add_orientation_widget(plotter):
    plotter.add_axes(line_width=3, labels_off=False)


def render_previews(preview_dir, meshes):
    preview_dir.mkdir(parents=True, exist_ok=True)
    pv.global_theme.allow_empty_mesh = True

    views = []
    plotter = pv.Plotter(off_screen=True, window_size=(1600, 1200))
    plotter.set_background("#11141b")
    plotter.add_mesh(meshes["KidneyExterior"], color="#b52d45", smooth_shading=True, lighting=False)
    add_orientation_widget(plotter)
    plotter.camera_position = [(175, -18, 220), (5, -4, 0), (0, 1, 0)]
    plotter.camera.clipping_range = (0.5, 1000.0)
    path = preview_dir / "01_exterior.png"
    plotter.show(screenshot=str(path), auto_close=True)
    views.append(path)

    plotter = pv.Plotter(off_screen=True, window_size=(1400, 1200))
    plotter.set_background("#11141b")
    plotter.add_mesh(meshes["KidneyExterior"], color="#b52d45", smooth_shading=True, lighting=False)
    add_orientation_widget(plotter)
    plotter.camera_position = [(0, -4, 300), (5, -4, 0), (0, 1, 0)]
    plotter.camera.clipping_range = (0.5, 1000.0)
    path = preview_dir / "01b_exterior_front.png"
    plotter.show(screenshot=str(path), auto_close=True)
    views.append(path)

    plotter = pv.Plotter(off_screen=True, window_size=(1600, 1200))
    plotter.set_background("#10131a")
    plotter.add_mesh(meshes["KidneyExterior"], color="#c83950", opacity=0.24, smooth_shading=True, lighting=False)
    plotter.add_mesh(meshes["CollectingSystemVisual"], color="#ed5b70", opacity=0.94, smooth_shading=True, lighting=False)
    plotter.add_mesh(meshes["RouteGuide"], color="#00d9ff", smooth_shading=True, lighting=False)
    plotter.add_mesh(meshes["Stone"], color="#f0a324", smooth_shading=True, lighting=False)
    add_orientation_widget(plotter)
    plotter.camera_position = [(180, -22, 225), (2, -5, 0), (0, 1, 0)]
    plotter.camera.clipping_range = (0.5, 1000.0)
    path = preview_dir / "02_complete_transparent.png"
    plotter.show(screenshot=str(path), auto_close=True)
    views.append(path)

    plotter = pv.Plotter(off_screen=True, window_size=(1600, 1200))
    plotter.set_background("#0f1218")
    cutaway = meshes["CollectingSystemVisual"].clip(
        normal=(0, 0, 1), origin=(0, 0, 1), invert=True
    )
    plotter.add_mesh(cutaway, color="#ed5369", smooth_shading=True, lighting=False)
    plotter.add_mesh(meshes["RouteGuide"], color="#00d9ff", smooth_shading=True, lighting=False)
    plotter.add_mesh(meshes["Stone"], color="#f0a324", smooth_shading=True, lighting=False)
    add_orientation_widget(plotter)
    plotter.camera_position = [(150, -18, 205), (-2, -7, 0), (0, 1, 0)]
    plotter.camera.clipping_range = (0.5, 1000.0)
    path = preview_dir / "03_collecting_system_cutaway.png"
    plotter.show(screenshot=str(path), auto_close=True)
    views.append(path)

    plotter = pv.Plotter(off_screen=True, window_size=(1600, 1200))
    plotter.set_background("#10131a")
    plotter.add_mesh(meshes["CollectingSystemVisual"], color="#de4b61", opacity=0.28, smooth_shading=True, lighting=False)
    plotter.add_mesh(meshes["RouteGuide"], color="#00d9ff", smooth_shading=True, lighting=False)
    plotter.add_mesh(meshes["Stone"], color="#f4b13b", smooth_shading=True, lighting=False)
    add_orientation_widget(plotter)
    plotter.camera_position = [(135, -25, 205), (-7, -12, 0), (0, 1, 0)]
    plotter.camera.clipping_range = (0.5, 1000.0)
    path = preview_dir / "04_route_and_target.png"
    plotter.show(screenshot=str(path), auto_close=True)
    views.append(path)
    return views


def sha256(path):
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def write_documentation(root, validation):
    docs = root / "Documentation"
    docs.mkdir(parents=True, exist_ok=True)
    (docs / "model_spec.md").write_text(
        "# Kidney Game v001\n\n"
        "Modelo anatômico orientado ao jogo, não reconstruído de um paciente.\n\n"
        "## Conteúdo\n\n"
        "- Exterior renal fechado com hilo e ureter curto.\n"
        "- Pelve renal e grupos calicinais superior, médio e inferior.\n"
        "- Nove cálices terminais e ramificações secundárias.\n"
        "- Rota fixa até o cálice médio, cálculo irregular e âncoras de jogo.\n"
        "- Malha de colisão simplificada com faces orientadas para o lúmen.\n\n"
        "O lado anatômico permanece `lado a confirmar`.\n",
        encoding="utf-8",
    )
    (docs / "scale_and_measurements.md").write_text(
        f"# Escala e medidas\n\n"
        "- Fonte procedural: milímetros, Y para cima.\n"
        "- Maya: centímetros em escala física real.\n"
        "- FBX: exportado pelo Maya em centímetros.\n"
        "- Unity: converter centímetros para metros e aplicar escala visual 5× em `GameplayScaleRoot`.\n"
        f"- Ponta virtual: raio físico de {TIP_RADIUS_MM:.1f} mm.\n"
        f"- Pedra: diâmetro nominal de {STONE_DIAMETER_MM:.1f} mm.\n"
        f"- Menor folga medida no centro da rota: {validation['checks']['route_minimum_centerline_clearance_mm']:.3f} mm.\n",
        encoding="utf-8",
    )
    (docs / "changelog.md").write_text(
        "# Alterações\n\n"
        "## v001\n\n"
        "- Substituído o blockout de elipsoides e cápsulas.\n"
        "- Criado arquivo-mestre Maya editável e FBX autoritativo.\n"
        "- Adicionados nove terminais calicinais, rota, pedra e âncoras.\n"
        "- Separadas as malhas visual e de colisão interna.\n"
        "- Eliminada a cópia física ampliada; o aumento 5× passa a pertencer ao Unity.\n",
        encoding="utf-8",
    )
    (docs / "validation_report.json").write_text(
        json.dumps(validation, indent=2, ensure_ascii=False), encoding="utf-8"
    )


def run_maya_export(mayapy, maya_script, obj_dir, source_ma, fbx_path):
    command = [
        str(mayapy),
        str(maya_script),
        "--obj-dir",
        str(obj_dir),
        "--source-ma",
        str(source_ma),
        "--fbx",
        str(fbx_path),
        "--version",
        VERSION,
    ]
    result = subprocess.run(command, text=True, capture_output=True)
    if result.stdout:
        print(result.stdout)
    if result.returncode:
        if result.stderr:
            print(result.stderr, file=sys.stderr)
        raise RuntimeError(f"Maya export failed with exit code {result.returncode}")


def write_manifest(path, fbx_path, validation):
    manifest = {
        "project": "Navegacao Renal 3D",
        "model": MODEL_NAME,
        "version": VERSION,
        "status": "game-complete anatomical model; not clinically validated",
        "laterality": "lado a confirmar",
        "generated_utc": datetime.now(timezone.utc).isoformat(),
        "coordinate_system": "Y-up",
        "maya_units": "centimetres",
        "unity_units": "metres",
        "unity_import_scale_cm_to_m": 0.01,
        "gameplay_visual_scale": GAMEPLAY_SCALE,
        "tip_radius_mm": TIP_RADIUS_MM,
        "stone_nominal_diameter_mm": STONE_DIAMETER_MM,
        "start_anchor_mm": [-43.0, -86.0, -4.0],
        "target_anchor_mm": [30.0, 15.0, 7.0],
        "required_nodes": [
            "KidneyExterior",
            "CollectingSystemVisual",
            "CollectingSystemCollision_Inward",
            "RouteGuide",
            "Stone",
            "StartAnchor",
            "TargetAnchor",
            "MinimapAnchor",
        ],
        "fbx_file": fbx_path.name,
        "fbx_sha256": sha256(fbx_path),
        "validation": validation,
    }
    path.write_text(json.dumps(manifest, indent=2, ensure_ascii=False), encoding="utf-8")
    return manifest


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--maya-root", type=Path, required=True)
    parser.add_argument("--unity-models", type=Path, required=True)
    parser.add_argument(
        "--mayapy",
        type=Path,
        default=Path(r"C:\Program Files\Autodesk\Maya2027\bin\mayapy.exe"),
    )
    args = parser.parse_args()
    maya_root = args.maya_root.resolve()
    unity_models = args.unity_models.resolve()

    source_dir = maya_root / "Source"
    exports_dir = maya_root / "Exports"
    obj_dir = exports_dir / "OBJ_Fallback"
    preview_dir = maya_root / "Previews"
    archive_dir = maya_root / "Archive"
    for directory in (source_dir, exports_dir, obj_dir, preview_dir, archive_dir, unity_models):
        directory.mkdir(parents=True, exist_ok=True)

    print("Building v001 geometry...")
    meshes, build_data = build_geometry()
    validation = validate_geometry(meshes, build_data)
    if not validation["passed"]:
        raise RuntimeError(json.dumps(validation, indent=2, ensure_ascii=False))

    export_obj_set(obj_dir, meshes)
    render_previews(preview_dir, meshes)
    write_documentation(maya_root, validation)

    source_ma = source_dir / "Kidney_Master.ma"
    fbx_path = exports_dir / f"{MODEL_NAME}.fbx"
    maya_script = Path(__file__).with_name("maya_export_kidney_game_model.py")

    # Maya's standalone Python on Windows can misdecode non-ASCII command-line
    # paths. Build in an ASCII-only temporary directory, then copy the
    # self-contained .ma and FBX back to the requested accented OneDrive path.
    with tempfile.TemporaryDirectory(prefix="kidney_v001_") as temp_name:
        temp_root = Path(temp_name)
        temp_obj = temp_root / "obj"
        temp_obj.mkdir()
        for source_file in obj_dir.iterdir():
            if source_file.is_file():
                shutil.copy2(source_file, temp_obj / source_file.name)
        temp_script = temp_root / "maya_export.py"
        shutil.copy2(maya_script, temp_script)
        temp_ma = temp_root / "Kidney_Master.ma"
        temp_fbx = temp_root / f"{MODEL_NAME}.fbx"
        run_maya_export(args.mayapy, temp_script, temp_obj, temp_ma, temp_fbx)
        shutil.copy2(temp_ma, source_ma)
        shutil.copy2(temp_fbx, fbx_path)

    manifest_path = exports_dir / f"{MODEL_NAME}_manifest.json"
    manifest = write_manifest(manifest_path, fbx_path, validation)

    unity_fbx = unity_models / fbx_path.name
    unity_manifest = unity_models / manifest_path.name
    shutil.copy2(fbx_path, unity_fbx)
    shutil.copy2(manifest_path, unity_manifest)
    if sha256(fbx_path) != sha256(unity_fbx):
        raise RuntimeError("Unity FBX copy hash differs from Maya export")

    summary = {
        "maya_master": str(source_ma),
        "maya_fbx": str(fbx_path),
        "unity_fbx": str(unity_fbx),
        "fbx_sha256": manifest["fbx_sha256"],
        "validation_passed": validation["passed"],
        "previews": [str(path) for path in sorted(preview_dir.glob("*.png"))],
    }
    print(json.dumps(summary, indent=2, ensure_ascii=False))


if __name__ == "__main__":
    main()
