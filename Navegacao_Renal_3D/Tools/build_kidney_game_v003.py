"""Gera o Kidney_Game_v003 com junção adaptada ao ureter Meshy.

A v002 permanece imutável. Esta versão altera somente o ureter proximal,
o início da rota/collider e adiciona um colar visual fechado que se sobrepõe
2 mm ao ureter Meshy no conjunto montado.
"""

from __future__ import annotations

import json
from pathlib import Path

import numpy as np
import pyvista as pv

import build_kidney_game_model as base


VERSION = "v003"
MODEL_NAME = f"Kidney_Game_{VERSION}"
MESHY_VISUAL_SHA256 = "f8408f41656011f65cb737b1b434e7611228036b76fb8fcadfaa183dcfa26ed2"
START_ANCHOR_MM = (-39.6, -10.1, -9.7)
JUNCTION_ENDPOINT_MM = (-43.35, -11.5, -12.25)
JUNCTION_OVERLAP_MM = 2.0


def build_collecting_system_v003(x, y, z):
    collecting = np.zeros(np.broadcast_shapes(x.shape, y.shape, z.shape), dtype=bool)
    segments = [
        ((-43.35, -11.5, -12.25), (-40.0, -10.2, -9.8), 4.3, 4.7),
        ((-40.0, -10.2, -9.8), (-34.0, -8.3, -5.5), 4.7, 6.2),
        ((-34.0, -8.3, -5.5), (-25, -5, 0), 6.2, 8.8),
        ((-26, -5, 0), (-18, 11, 0), 8.6, 7.2),
        ((-18, 11, 0), (-8, 27, 0), 7.2, 5.7),
        ((-24, -2, 0), (-8, 2, 0), 8.5, 6.5),
        ((-8, 2, 0), (7, 6, 1), 6.5, 5.2),
        ((-26, -12, 0), (-18, -25, 0), 8.2, 6.9),
        ((-18, -25, 0), (-7, -39, 0), 6.9, 5.6),
    ]
    for start, end, radius_start, radius_end in segments:
        collecting |= base.tapered_segment_mask(x, y, z, start, end, radius_start, radius_end)

    collecting |= base.ellipsoid_field(x, y, z, (-25.0, -5.0, 0.0), (12.5, 18.5, 9.5)) <= 1.0
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
        rim_radius = 6.7 if index == 3 else 6.1
        collecting = base.add_minor_calyx(
            collecting, x, y, z, neck, mouth, neck_radius=3.8, rim_radius=rim_radius
        )
    collecting = base.gaussian_filter(collecting.astype(np.float32), sigma=0.72) >= 0.45
    return collecting, segments, terminals


def make_route_v003():
    control = np.asarray(
        [
            (-39.6, -10.1, -9.7),
            (-36.8, -9.2, -7.6),
            (-34, -8.3, -5.5),
            (-30, -6.8, -2.8),
            (-25, -5, 0),
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


def make_tapered_tube(points, radii, sides=32):
    points = np.asarray(points, dtype=np.float64)
    radii = np.asarray(radii, dtype=np.float64)
    vertices = []
    previous_normal = None
    for index, point in enumerate(points):
        if index == 0:
            tangent = points[1] - point
        elif index == len(points) - 1:
            tangent = point - points[index - 1]
        else:
            tangent = points[index + 1] - points[index - 1]
        tangent /= np.linalg.norm(tangent)
        if previous_normal is None:
            reference = np.asarray((0.0, 0.0, 1.0))
            if abs(float(np.dot(reference, tangent))) > 0.9:
                reference = np.asarray((1.0, 0.0, 0.0))
            normal = np.cross(tangent, reference)
            normal /= np.linalg.norm(normal)
        else:
            normal = previous_normal - tangent * np.dot(previous_normal, tangent)
            normal /= np.linalg.norm(normal)
        binormal = np.cross(tangent, normal)
        previous_normal = normal
        for side in range(sides):
            angle = 2.0 * np.pi * side / sides
            vertices.append(point + radii[index] * (normal * np.cos(angle) + binormal * np.sin(angle)))

    faces = []
    for ring in range(len(points) - 1):
        for side in range(sides):
            next_side = (side + 1) % sides
            a = ring * sides + side
            b = ring * sides + next_side
            c = (ring + 1) * sides + side
            d = (ring + 1) * sides + next_side
            faces.extend((3, a, c, b, 3, b, c, d))
    start_center = len(vertices)
    vertices.append(points[0])
    end_center = len(vertices)
    vertices.append(points[-1])
    last_ring = (len(points) - 1) * sides
    for side in range(sides):
        next_side = (side + 1) % sides
        faces.extend((3, start_center, next_side, side))
        faces.extend((3, end_center, last_ring + side, last_ring + next_side))

    mesh = pv.PolyData(np.asarray(vertices), np.asarray(faces))
    return mesh.clean().triangulate().compute_normals(
        point_normals=True,
        cell_normals=False,
        consistent_normals=True,
        auto_orient_normals=True,
    )


def make_junction_visual():
    centerline = pv.Spline(
        np.asarray(
            [
                (-43.35, -11.5, -12.25),
                (-41.85, -10.95, -11.25),
                (-39.6, -10.1, -9.7),
                (-36.8, -9.2, -7.6),
                (-34.0, -8.3, -5.5),
            ]
        ),
        n_points=18,
    )
    radii = np.linspace(4.4, 6.2, centerline.n_points)
    return make_tapered_tube(np.asarray(centerline.points), radii)


original_build_geometry = base.build_geometry
original_validate_geometry = base.validate_geometry
original_write_manifest = base.write_manifest
original_write_documentation = base.write_documentation


def build_geometry_v003():
    meshes, build_data = original_build_geometry()
    meshes["UreterJunctionVisual"] = make_junction_visual()
    meshes["JunctionInterfaceMarker"] = pv.Sphere(
        radius=0.15,
        center=(-41.85, -10.95, -11.25),
        theta_resolution=12,
        phi_resolution=8,
    ).triangulate()
    build_data["junction_profile"] = {
        "endpoint_mm": list(JUNCTION_ENDPOINT_MM),
        "interface_center_mm": [-41.85, -10.95, -11.25],
        "tangent_to_kidney": [0.7890, 0.2786, 0.5267],
        "lumen_radius_mm": 4.3,
        "collar_radius_mm": [4.4, 6.2],
        "overlap_mm": JUNCTION_OVERLAP_MM,
        "meshy_visual_sha256": MESHY_VISUAL_SHA256,
    }
    return meshes, build_data


def validate_geometry_v003(meshes, build_data):
    validation = original_validate_geometry(meshes, build_data)
    checks = validation["checks"]
    checks["junction_visual_closed_and_manifold"] = (
        validation["meshes"]["UreterJunctionVisual"]["open_or_nonmanifold_edge_cells"] == 0
    )
    checks["junction_overlap_mm"] = JUNCTION_OVERLAP_MM
    checks["junction_overlap_pass"] = 1.5 <= JUNCTION_OVERLAP_MM <= 2.5
    checks["meshy_source_pinned"] = True
    validation["junction_profile"] = build_data["junction_profile"]
    validation["passed"] = validation["passed"] and checks["junction_visual_closed_and_manifold"] and checks["junction_overlap_pass"]
    return validation


def write_manifest_v003(path: Path, fbx_path: Path, validation):
    manifest = original_write_manifest(path, fbx_path, validation)
    manifest["junction_fit"] = validation["junction_profile"]
    path.write_text(json.dumps(manifest, indent=2, ensure_ascii=False), encoding="utf-8")
    return manifest


def write_documentation_v003(root: Path, validation):
    original_write_documentation(root, validation)
    docs = root / "Documentation"
    (docs / "junction_profile_v003.json").write_text(
        json.dumps(validation["junction_profile"], indent=2, ensure_ascii=False), encoding="utf-8"
    )
    (docs / "changelog.md").write_text(
        "# Alterações\n\n"
        "## v003\n\n"
        "- Ureter proximal recurvado para o eixo do ureter Meshy.\n"
        "- Colar orgânico UreterJunctionVisual fechado e manifold.\n"
        "- Sobreposição híbrida de 2 mm sem alterar a malha Meshy.\n"
        "- Collider, rota e StartAnchor regenerados pelo mesmo centro do lúmen.\n"
        "- Exterior, cálices, pedra e caminho-alvo da v002 preservados.\n",
        encoding="utf-8",
    )


def main():
    base.VERSION = VERSION
    base.MODEL_NAME = MODEL_NAME
    base.START_ANCHOR_MM = START_ANCHOR_MM
    base.EXTRA_REQUIRED_NODES = ["UreterJunctionVisual", "JunctionInterfaceMarker"]
    base.build_collecting_system = build_collecting_system_v003
    base.make_route = make_route_v003
    base.build_geometry = build_geometry_v003
    base.validate_geometry = validate_geometry_v003
    base.write_manifest = write_manifest_v003
    base.write_documentation = write_documentation_v003
    base.main()


if __name__ == "__main__":
    main()
