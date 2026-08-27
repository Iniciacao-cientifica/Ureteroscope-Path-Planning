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


VERSION = "v002"
MODEL_NAME = f"Kidney_Game_{VERSION}"
TIP_RADIUS_MM = 2.0
STONE_DIAMETER_MM = 6.0
GAMEPLAY_SCALE = 5.0
GRID_SPACING_MM = 0.9
START_ANCHOR_MM = (-43.0, -88.0, -3.0)
EXTRA_REQUIRED_NODES = []


@dataclass(frozen=True)
class Material:
    name: str
    color: tuple[float, float, float]
    opacity: float = 1.0


MATERIALS = {
    # The Maya master opens with a solid kidney.  Transparency is reserved for
    # the generated review preview so the asset no longer looks incomplete.
    "exterior": Material("MAT_KidneyExterior", (0.48, 0.055, 0.095), 1.0),
    "interior": Material("MAT_CollectingSystem", (0.93, 0.33, 0.42), 1.0),
    "collision": Material("MAT_CollisionDebug", (0.06, 0.64, 0.86), 0.18),
    "route": Material("MAT_RouteCyan", (0.00, 0.78, 1.00), 1.0),
    "stone": Material("MAT_StoneGold", (0.92, 0.56, 0.08), 1.0),
    "junction": Material("MAT_UreterJunction", (0.82, 0.12, 0.20), 1.0),
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


def oriented_ellipsoid_field(x, y, z, center, axis, axial_radius, radial_radius):
    """Ellipsoid aligned to an arbitrary branch axis.

    Minor calyces are not spherical bulbs.  Their terminal chamber is a short,
    flattened cup aligned with the infundibulum; this field supplies the lip
    and the papillary impression used by ``add_minor_calyx``.
    """
    center = np.asarray(center, dtype=np.float32)
    axis = np.asarray(axis, dtype=np.float32)
    axis /= np.linalg.norm(axis)
    px, py, pz = x - center[0], y - center[1], z - center[2]
    axial = px * axis[0] + py * axis[1] + pz * axis[2]
    radial_sq = px * px + py * py + pz * pz - axial * axial
    return (axial / axial_radius) ** 2 + radial_sq / (radial_radius * radial_radius)


def add_minor_calyx(volume, x, y, z, neck, mouth, neck_radius=3.7, rim_radius=6.2):
    """Add a flared minor calyx with a concave papillary cup.

    The result is a closed lumen volume.  A shallow axial ellipsoid is added at
    the mouth and a smaller ellipsoid entering from the papillary side is
    subtracted, replacing the ball-shaped endings present in v001.
    """
    neck = np.asarray(neck, dtype=np.float32)
    mouth = np.asarray(mouth, dtype=np.float32)
    axis = mouth - neck
    axis /= np.linalg.norm(axis)
    volume |= tapered_segment_mask(
        x, y, z, neck, mouth - axis * 1.2, neck_radius, rim_radius
    )
    lip_center = mouth - axis * 1.0
    lip = oriented_ellipsoid_field(
        x, y, z, lip_center, axis, axial_radius=3.6, radial_radius=rim_radius
    ) <= 1.0
    volume |= lip

    # The indentation comes from outside the terminal chamber but stops before
    # the neck, leaving a navigable, manifold and cup-shaped ending.
    papilla_center = mouth + axis * 1.4
    papilla = oriented_ellipsoid_field(
        x, y, z, papilla_center, axis, axial_radius=5.2, radial_radius=rim_radius * 0.67
    ) <= 1.0
    volume &= ~papilla
    return volume


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
        # Taubin smoothing removes marching-cubes stair stepping without the
        # shrinkage and contour bands produced by repeated Laplacian passes.
        mesh = mesh.smooth_taubin(n_iter=smoothing, pass_band=pass_band)
    return mesh.compute_normals(
        point_normals=True,
        cell_normals=False,
        consistent_normals=True,
        auto_orient_normals=True,
    )


def build_exterior(x, y, z):
    # A curved long axis, unequal poles and a fuller lateral border avoid the
    # symmetric oval silhouette of the earlier blockout.
    curved_x = x - 0.045 * y - 0.00055 * y * y
    body = ellipsoid_field(curved_x, y, z, (7.0, 0.0, 0.0), (46.0, 67.0, 27.0)) <= 1.0
    lateral_bulge = ellipsoid_field(x, y, z, (23.0, -2.0, 0.5), (35.0, 57.0, 28.0)) <= 1.0
    upper_pole = ellipsoid_field(x, y, z, (4.0, 43.0, -1.0), (37.0, 31.0, 25.5)) <= 1.0
    lower_pole = ellipsoid_field(x, y, z, (10.0, -43.0, 1.5), (35.0, 33.0, 24.5)) <= 1.0
    exterior = body | lateral_bulge | upper_pole | lower_pole

    # Deep but rounded medial notch: enough space for pelvis/UPJ while keeping
    # the external surface a single closed manifold.
    hilum = ellipsoid_field(x, y, z, (-43.0, 0.0, -0.5), (25.0, 28.0, 38.0)) <= 1.0
    exterior &= ~hilum

    exterior = gaussian_filter(exterior.astype(np.float32), sigma=1.45) >= 0.49
    return exterior


def build_collecting_system(x, y, z):
    collecting = np.zeros(np.broadcast_shapes(x.shape, y.shape, z.shape), dtype=bool)

    # Ureter -> renal pelvis.  The progressive flare forms the ureteropelvic
    # junction and a funnel-shaped pelvis instead of a large central sphere.
    segments = [
        ((-43, -91, -3), (-42, -69, -3), 4.3, 4.5),
        ((-42, -69, -3), (-38, -47, -2), 4.5, 5.0),
        ((-38, -47, -2), (-32, -27, -1), 5.0, 6.1),
        ((-32, -27, -1), (-25, -10, 0), 6.1, 8.8),
        # Upper major calyx / infundibulum.
        ((-26, -5, 0), (-18, 11, 0), 8.6, 7.2),
        ((-18, 11, 0), (-8, 27, 0), 7.2, 5.7),
        # Middle major calyx; its anterior branch contains the fixed stone.
        ((-24, -2, 0), (-8, 2, 0), 8.5, 6.5),
        ((-8, 2, 0), (7, 6, 1), 6.5, 5.2),
        # Lower major calyx / infundibulum.
        ((-26, -12, 0), (-18, -25, 0), 8.2, 6.9),
        ((-18, -25, 0), (-7, -39, 0), 6.9, 5.6),
    ]
    for start, end, radius_start, radius_end in segments:
        collecting |= tapered_segment_mask(
            x, y, z, start, end, radius_start, radius_end
        )

    # Flattened central pelvis blended into the funnel/major calyces.
    collecting |= ellipsoid_field(x, y, z, (-25.0, -5.0, 0.0), (12.5, 18.5, 9.5)) <= 1.0

    # Each pair is (neck, mouth).  The terminal chamber flares and receives a
    # concave papillary impression, so it reads as a calyx rather than a ball.
    terminals = [
        ((-8, 27, 0), (10, 45, 8)),
        ((-5, 31, -1), (12, 43, -11)),
        ((-10, 24, 1), (5, 34, 15)),
        ((7, 6, 1), (28, 11, 8)),
        ((4, 5, -1), (24, 1, -13)),
        ((1, 7, 2), (18, 20, 15)),
        ((-7, -39, 0), (11, -55, 8)),
        ((-4, -42, -1), (14, -52, -12)),
        ((-10, -35, 1), (6, -43, 15)),
    ]
    for index, (neck, mouth) in enumerate(terminals):
        # The navigated middle calyx is slightly wider for the 4 mm instrument
        # tip and 6 mm stone; all others retain believable minor-calyx scale.
        rim_radius = 6.7 if index == 3 else 6.1
        collecting = add_minor_calyx(
            collecting, x, y, z, neck, mouth, neck_radius=3.8, rim_radius=rim_radius
        )

    collecting = gaussian_filter(collecting.astype(np.float32), sigma=0.72) >= 0.45
    return collecting, segments, terminals


def make_route():
    control = np.asarray(
        [
            (-43, -88, -3),
            (-42, -72, -3),
            (-40, -58, -2.5),
            (-38, -47, -2),
            (-35, -36, -1.5),
            (-32, -27, -1),
            (-28, -17, -0.5),
            (-25, -9, 0),
            (-21, -4, 0),
            (-15, -1, 0.2),
            (-8, 2, 0.5),
            (0, 4, 0.8),
            (7, 6, 1),
            (14, 7.8, 3),
            (20.5, 9.0, 5.2),
        ],
        dtype=np.float32,
    )
    centerline = pv.Spline(control, n_points=360)
    tube = centerline.tube(radius=0.85, n_sides=12, capping=True).triangulate().clean()
    return control, np.asarray(centerline.points), tube


def make_stone(center=(20.5, 9.0, 5.2)):
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
    # Deeper papillary cups require a conservative reduction.  More aggressive
    # decimation can collapse the narrow calyceal rim into a non-manifold edge.
    collision = collecting.decimate_pro(0.45, preserve_topology=True).clean().triangulate()
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
    lines = [f"# Navegacao Renal 3D - game model {VERSION}"]
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
    lines = ["# Navegacao Renal 3D - centimetres, Y up", f"mtllib {MODEL_NAME}.mtl", ""]
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
    if "UreterJunctionVisual" in meshes:
        entries.append(("UreterJunctionVisual", meshes["UreterJunctionVisual"], MATERIALS["junction"], False))
    if "JunctionInterfaceMarker" in meshes:
        entries.append(("JunctionInterfaceMarker", meshes["JunctionInterfaceMarker"], MATERIALS["route"], False))
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
    stone_center = np.asarray((20.5, 9.0, 5.2))
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
    plotter.add_mesh(meshes["KidneyExterior"], color="#b52d45", smooth_shading=True, lighting=True,
                     ambient=0.24, diffuse=0.74, specular=0.22, specular_power=28)
    add_orientation_widget(plotter)
    plotter.camera_position = [(175, -18, 220), (5, -4, 0), (0, 1, 0)]
    plotter.camera.clipping_range = (0.5, 1000.0)
    path = preview_dir / "01_exterior.png"
    plotter.show(screenshot=str(path), auto_close=True)
    views.append(path)

    plotter = pv.Plotter(off_screen=True, window_size=(1400, 1200))
    plotter.set_background("#11141b")
    plotter.add_mesh(meshes["KidneyExterior"], color="#b52d45", smooth_shading=True, lighting=True,
                     ambient=0.24, diffuse=0.74, specular=0.22, specular_power=28)
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
    if "UreterJunctionVisual" in meshes:
        plotter.add_mesh(meshes["UreterJunctionVisual"], color="#d83d52", smooth_shading=True, lighting=True)
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
    # Remove only the anterior half of the renal exterior.  The collecting
    # system remains intact, making the anatomical relationship easy to judge.
    cutaway = meshes["KidneyExterior"].clip(
        normal=(0, 0, 1), origin=(0, 0, 0), invert=True
    )
    plotter.add_mesh(cutaway, color="#b52d45", opacity=0.72, smooth_shading=True, lighting=False)
    plotter.add_mesh(meshes["CollectingSystemVisual"], color="#ed5369", smooth_shading=True, lighting=False)
    plotter.add_mesh(meshes["RouteGuide"], color="#00d9ff", smooth_shading=True, lighting=False)
    plotter.add_mesh(meshes["Stone"], color="#f0a324", smooth_shading=True, lighting=False)
    add_orientation_widget(plotter)
    plotter.camera_position = [(150, -18, 205), (-2, -7, 0), (0, 1, 0)]
    plotter.camera.clipping_range = (0.5, 1000.0)
    path = preview_dir / "03_kidney_cutaway.png"
    plotter.show(screenshot=str(path), auto_close=True)
    views.append(path)

    plotter = pv.Plotter(off_screen=True, window_size=(1600, 1200))
    plotter.set_background("#10131a")
    plotter.add_mesh(meshes["CollectingSystemVisual"], color="#de4b61", opacity=0.88, smooth_shading=True, lighting=False)
    plotter.add_mesh(meshes["RouteGuide"], color="#00d9ff", smooth_shading=True, lighting=False)
    plotter.add_mesh(meshes["Stone"], color="#f4b13b", smooth_shading=True, lighting=False)
    add_orientation_widget(plotter)
    plotter.camera_position = [(135, -25, 205), (-7, -12, 0), (0, 1, 0)]
    plotter.camera.clipping_range = (0.5, 1000.0)
    path = preview_dir / "04_route_and_target.png"
    plotter.show(screenshot=str(path), auto_close=True)
    views.append(path)

    # Direct view towards the papillary ends, used to verify that the minor
    # calyces are concave cups rather than the spherical caps from v001.
    plotter = pv.Plotter(off_screen=True, window_size=(1600, 1200))
    plotter.set_background("#10131a")
    plotter.add_mesh(meshes["CollectingSystemVisual"], color="#ed5369", smooth_shading=True,
                     lighting=True, ambient=0.28, diffuse=0.72, specular=0.18, specular_power=24)
    add_orientation_widget(plotter)
    plotter.camera_position = [(230, -2, 5), (0, -5, 0), (0, 1, 0)]
    plotter.camera.clipping_range = (0.5, 1000.0)
    path = preview_dir / "05_calyx_cups.png"
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
        f"# Kidney Game {VERSION}\n\n"
        "Modelo anatômico orientado ao jogo, não reconstruído de um paciente.\n\n"
        "## Conteúdo\n\n"
        "- Exterior renal fechado com hilo e ureter curto.\n"
        "- Pelve renal afunilada e grupos calicinais superior, médio e inferior.\n"
        "- Nove cálices menores alargados, com impressão papilar côncava.\n"
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
        f"## {VERSION}\n\n"
        "- Remodelado o sistema coletor para remover terminações esféricas.\n"
        "- Pelve, infundíbulos e cálices passaram a ter transições afuniladas.\n"
        "- Adicionadas terminações calicinais em forma de taça com impressão papilar.\n"
        "- Exterior passa a abrir opaco no Maya para não aparentar estar incompleto.\n"
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
    required_nodes = [
        "KidneyExterior",
        "CollectingSystemVisual",
        "CollectingSystemCollision_Inward",
        "RouteGuide",
        "Stone",
        "StartAnchor",
        "TargetAnchor",
        "MinimapAnchor",
    ] + list(EXTRA_REQUIRED_NODES)
    manifest = {
        "project": "Navegacao Renal 3D",
        "model": MODEL_NAME,
        "version": VERSION,
        "status": "Marco 1 candidate; visual approval pending; not clinically validated",
        "laterality": "lado a confirmar",
        "generated_utc": datetime.now(timezone.utc).isoformat(),
        "coordinate_system": "Y-up",
        "maya_units": "centimetres",
        "unity_units": "metres",
        "unity_import_scale_cm_to_m": 0.01,
        "gameplay_visual_scale": GAMEPLAY_SCALE,
        "tip_radius_mm": TIP_RADIUS_MM,
        "stone_nominal_diameter_mm": STONE_DIAMETER_MM,
        "start_anchor_mm": list(START_ANCHOR_MM),
        "target_anchor_mm": [20.5, 9.0, 5.2],
        "required_nodes": required_nodes,
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

    print(f"Building {VERSION} geometry...")
    meshes, build_data = build_geometry()
    validation = validate_geometry(meshes, build_data)
    if not validation["passed"]:
        raise RuntimeError(json.dumps(validation, indent=2, ensure_ascii=False))

    export_obj_set(obj_dir, meshes)
    render_previews(preview_dir, meshes)
    write_documentation(maya_root, validation)

    source_ma = source_dir / ("Kidney_Master.ma" if VERSION == "v002" else f"Kidney_Master_{VERSION}.ma")
    fbx_path = exports_dir / f"{MODEL_NAME}.fbx"
    maya_script = Path(__file__).with_name("maya_export_kidney_game_model.py")

    # Maya's standalone Python on Windows can misdecode non-ASCII command-line
    # paths. Build in an ASCII-only temporary directory, then copy the
    # self-contained .ma and FBX back to the requested accented OneDrive path.
    with tempfile.TemporaryDirectory(prefix=f"kidney_{VERSION}_") as temp_name:
        temp_root = Path(temp_name)
        temp_obj = temp_root / "obj"
        temp_obj.mkdir()
        for source_file in obj_dir.iterdir():
            if source_file.is_file():
                shutil.copy2(source_file, temp_obj / source_file.name)
        temp_script = temp_root / "maya_export.py"
        shutil.copy2(maya_script, temp_script)
        temp_ma = temp_root / source_ma.name
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
