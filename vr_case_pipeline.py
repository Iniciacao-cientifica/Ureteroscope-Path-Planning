"""Build a versioned, physically-scaled VR case package.

The planner still operates in voxel coordinates because that is the coordinate
system used by the original research code.  This module converts every exported
mesh and route to Unity coordinates in metres and records the source spacing for
traceability.
"""

from __future__ import annotations

import argparse
import csv
import json
import math
import re
import time
from datetime import datetime, timezone
from pathlib import Path

import numpy as np
from scipy import ndimage
from scipy.spatial import KDTree
from skimage import measure

import data
import path_planning
import vr_export_pipeline as legacy


SCHEMA_VERSION = 2
CLINICAL_NOTICE = legacy.CLINICAL_NOTICE
SAFE_CASE_ID = re.compile(r"^[A-Za-z0-9][A-Za-z0-9_-]{0,63}$")


def parse_spacing_mm(value: str) -> tuple[float, float, float]:
    spacing = legacy.parse_xyz(value)
    if any(component <= 0 for component in spacing):
        raise argparse.ArgumentTypeError("Spacing values must be greater than zero.")
    return spacing


def load_volume(source: Path) -> tuple[np.ndarray, dict]:
    """Load a z,y,x binary volume and return non-identifying geometry metadata."""
    source = source.resolve()
    if source.is_dir():
        volume = data.carregar_imagens_binarias(str(source))
        return (volume > 0).astype(np.uint8), {"format": "png_stack"}

    suffixes = "".join(source.suffixes).lower()
    if source.suffix.lower() == ".npy":
        volume = np.load(source, allow_pickle=False)
        if volume.ndim != 3:
            raise ValueError(f"Expected a 3D NPY volume, got shape {volume.shape}.")
        return (volume > 0).astype(np.uint8), {"format": "npy"}

    if suffixes.endswith(".nii") or suffixes.endswith(".nii.gz"):
        try:
            import SimpleITK as sitk
        except ImportError as exc:
            raise RuntimeError(
                "NIfTI input requires SimpleITK. Install requirements-medical-io.txt."
            ) from exc

        image = sitk.ReadImage(str(source))
        volume = sitk.GetArrayFromImage(image)
        if volume.ndim != 3:
            raise ValueError(f"Expected a 3D NIfTI volume, got shape {volume.shape}.")
        return (volume > 0).astype(np.uint8), {
            "format": "nifti",
            "spacing_mm_xyz": list(image.GetSpacing()),
            "origin_mm_xyz": list(image.GetOrigin()),
            "direction": list(image.GetDirection()),
        }

    raise ValueError(f"Unsupported volume source: {source}. Use a PNG directory, NPY, NII, or NII.GZ.")


def voxel_xyz_to_unity_m(point, spacing_mm_xyz, direction=None):
    """Convert an xyz continuous index to relative patient space, then Unity metres."""
    index_mm = np.asarray(point, dtype=float) * np.asarray(spacing_mm_xyz, dtype=float)
    direction_matrix = np.eye(3) if direction is None else np.asarray(direction, dtype=float).reshape(3, 3)
    physical_relative = direction_matrix @ index_mm
    return (
        float(physical_relative[0] / 1000.0),
        float(physical_relative[2] / 1000.0),
        float(physical_relative[1] / 1000.0),
    )


def point_object(point):
    return {"x": float(point[0]), "y": float(point[1]), "z": float(point[2])}


def physical_length_mm(points_xyz, spacing_mm_xyz):
    if len(points_xyz) < 2:
        return 0.0
    points = np.asarray(points_xyz, dtype=float)
    spacing = np.asarray(spacing_mm_xyz, dtype=float)
    return float(np.linalg.norm(np.diff(points, axis=0) * spacing, axis=1).sum())


def validate_point_in_volume(point_xyz, volume, label):
    x, y, z = (int(round(value)) for value in point_xyz)
    if not (0 <= z < volume.shape[0] and 0 <= y < volume.shape[1] and 0 <= x < volume.shape[2]):
        raise ValueError(f"{label} {point_xyz} is outside volume shape {volume.shape} (z,y,x).")


def nearest_navigable_point(point_xyz, valid_tree, valid_zyx):
    x, y, z = point_xyz
    _, index = valid_tree.query([z, y, x])
    nearest_zyx = valid_zyx[int(index)]
    return tuple(float(value) for value in nearest_zyx[::-1])


def extract_stones(stone_volume, spacing_mm_xyz, minimum_voxels, maximum_stones):
    labels, count = ndimage.label(stone_volume > 0, structure=np.ones((3, 3, 3), dtype=np.uint8))
    stones = []
    voxel_volume_mm3 = float(np.prod(spacing_mm_xyz))

    for label_index in range(1, count + 1):
        coordinates_zyx = np.argwhere(labels == label_index)
        voxel_count = int(len(coordinates_zyx))
        if voxel_count < minimum_voxels:
            continue

        centroid_zyx = coordinates_zyx.mean(axis=0)
        centroid_xyz = tuple(float(value) for value in centroid_zyx[::-1])
        volume_mm3 = voxel_count * voxel_volume_mm3
        diameter_mm = (6.0 * volume_mm3 / math.pi) ** (1.0 / 3.0)
        stones.append(
            {
                "voxel_count": voxel_count,
                "volume_mm3": float(volume_mm3),
                "equivalent_diameter_mm": float(diameter_mm),
                "centroid_voxel_xyz": centroid_xyz,
            }
        )

    stones.sort(key=lambda stone: stone["voxel_count"], reverse=True)
    stones = stones[:maximum_stones]
    for index, stone in enumerate(stones, start=1):
        stone["stone_id"] = f"stone_{index:03d}"
    return stones


def export_obj_meters(
    volume,
    output_path,
    spacing_mm_xyz,
    smoothing_iterations,
    smoothing_relaxation,
    step_size,
    direction=None,
):
    if not np.any(volume):
        raise ValueError(f"Cannot export an empty volume to {output_path}.")

    verts, faces, _, _ = measure.marching_cubes(
        volume.astype(np.float32),
        level=0.5,
        step_size=max(1, int(step_size)),
        allow_degenerate=False,
    )
    if smoothing_iterations > 0 and smoothing_relaxation > 0:
        verts = legacy.smooth_mesh_vertices(verts, faces, smoothing_iterations, smoothing_relaxation)

    xyz_indices = np.column_stack([verts[:, 2], verts[:, 1], verts[:, 0]])
    scaled_xyz = xyz_indices * np.asarray(spacing_mm_xyz, dtype=float)
    direction_matrix = np.eye(3) if direction is None else np.asarray(direction, dtype=float).reshape(3, 3)
    physical_relative = scaled_xyz @ direction_matrix.T
    unity_verts = np.column_stack(
        [physical_relative[:, 0], physical_relative[:, 2], physical_relative[:, 1]]
    ) / 1000.0
    unity_normals = legacy.calculate_vertex_normals(unity_verts, faces)

    with output_path.open("w", encoding="utf-8", newline="\n") as handle:
        handle.write("# Geometry exported in Unity metres (x, y-up, z)\n")
        for vertex in unity_verts:
            handle.write(f"v {vertex[0]:.7f} {vertex[1]:.7f} {vertex[2]:.7f}\n")
        for normal in unity_normals:
            handle.write(f"vn {normal[0]:.7f} {normal[1]:.7f} {normal[2]:.7f}\n")
        for face in faces:
            a, b, c = face + 1
            handle.write(f"f {a}//{a} {b}//{b} {c}//{c}\n")


def build_route(volume, start_xyz, requested_target_xyz, route_id, stone_id, args):
    valid_zyx = np.argwhere(volume > 0)
    if len(valid_zyx) == 0:
        raise ValueError("The navigable anatomy mask is empty.")
    valid_tree = KDTree(valid_zyx)

    validate_point_in_volume(start_xyz, volume, "Start point")
    planning_start_xyz = nearest_navigable_point(start_xyz, valid_tree, valid_zyx)
    planning_target_xyz = nearest_navigable_point(requested_target_xyz, valid_tree, valid_zyx)
    start_idx = tuple(int(round(value)) for value in planning_start_xyz[::-1])
    target_idx = tuple(int(round(value)) for value in planning_target_xyz[::-1])

    started_at = time.time()
    planned = path_planning.path_plan(volume, start_idx, target_idx)
    if not planned:
        raise RuntimeError(f"No valid path found for {route_id}.")

    path_xyz = [tuple(float(value) for value in point[::-1]) for point in planned]
    reduced_path = legacy.select_route_points(path_xyz, args.path_point_ratio)
    params = legacy.load_best_bspline_params(args.best_results_csv)
    selected = legacy.choose_safe_bspline(
        reduced_path,
        volume,
        planning_target_xyz,
        params,
        args.extrapolation_threshold,
        args.risk_threshold,
    )
    smoothed = selected["points"]
    visual, visual_source = legacy.build_visual_route(
        smoothed,
        volume,
        args.visual_samples,
        args.extrapolation_threshold,
    )
    curvature = legacy.calculate_curvature_torsion(
        [tuple(np.asarray(point) * np.asarray(args.spacing_mm)) for point in smoothed]
    )

    metrics = {
        "path_points": len(path_xyz),
        "exported_path_points": len(reduced_path),
        "smoothed_points": len(smoothed),
        "visual_points": len(visual),
        "visual_route_source": visual_source,
        "path_length_mm": physical_length_mm(path_xyz, args.spacing_mm),
        "smoothed_length_mm": physical_length_mm(smoothed, args.spacing_mm),
        "risk_points": int(selected["risk_points"]),
        "final_error_mm": float(
            np.linalg.norm(
                (np.asarray(smoothed[-1]) - np.asarray(planning_target_xyz))
                * np.asarray(args.spacing_mm)
            )
        ),
        "processing_seconds": float(time.time() - started_at),
    }
    metrics.update(curvature)
    metrics.update(selected["extrapolation"])

    def unity_points(points):
        return [point_object(voxel_xyz_to_unity_m(point, args.spacing_mm, args.direction)) for point in points]

    return {
        "route_id": route_id,
        "stone_id": stone_id,
        "coordinate_space": "unity_meters_y_up",
        "start": point_object(voxel_xyz_to_unity_m(planning_start_xyz, args.spacing_mm, args.direction)),
        "target": point_object(voxel_xyz_to_unity_m(planning_target_xyz, args.spacing_mm, args.direction)),
        "requested_target": point_object(voxel_xyz_to_unity_m(requested_target_xyz, args.spacing_mm, args.direction)),
        "path_original": unity_points(reduced_path),
        "path_smoothed": unity_points(smoothed),
        "path_visual": unity_points(visual),
        "metrics": metrics,
        "planning": {
            "start_voxel_xyz": list(planning_start_xyz),
            "target_voxel_xyz": list(planning_target_xyz),
            "bspline": selected["params"],
        },
    }


def write_json(path, payload):
    with path.open("w", encoding="utf-8", newline="\n") as handle:
        json.dump(payload, handle, ensure_ascii=False, indent=2, allow_nan=False)
        handle.write("\n")


def write_route_csv(routes, output_path):
    with output_path.open("w", newline="", encoding="utf-8") as handle:
        writer = csv.writer(handle)
        writer.writerow(["route_id", "path", "index", "x_m", "y_m", "z_m"])
        for route in routes:
            for path_name in ("path_original", "path_smoothed"):
                for index, point in enumerate(route[path_name]):
                    writer.writerow([route["route_id"], path_name, index, point["x"], point["y"], point["z"]])


def build_case(args):
    if not SAFE_CASE_ID.fullmatch(args.case_id):
        raise ValueError("--case-id must contain only letters, numbers, underscores, and hyphens.")

    anatomy, anatomy_metadata = load_volume(args.mask)
    if args.spacing_mm is None:
        metadata_spacing = anatomy_metadata.get("spacing_mm_xyz")
        args.spacing_mm = tuple(metadata_spacing) if metadata_spacing else (2.0, 2.0, 2.0)
        spacing_assumed = metadata_spacing is None
    else:
        spacing_assumed = False
        metadata_spacing = anatomy_metadata.get("spacing_mm_xyz")
        if metadata_spacing is not None and not np.allclose(args.spacing_mm, metadata_spacing, atol=1e-5):
            raise ValueError(
                f"Provided spacing {args.spacing_mm} differs from image spacing {tuple(metadata_spacing)}."
            )

    args.direction = anatomy_metadata.get("direction", np.eye(3).reshape(-1).tolist())
    source_origin = anatomy_metadata.get("origin_mm_xyz", [0.0, 0.0, 0.0])

    if not np.any(anatomy):
        raise ValueError("The anatomy mask is empty.")

    stone_volume = None
    stones = []
    if args.stone_mask is not None:
        stone_volume, stone_metadata = load_volume(args.stone_mask)
        if stone_volume.shape != anatomy.shape:
            raise ValueError(
                f"Stone mask shape {stone_volume.shape} does not match anatomy shape {anatomy.shape}."
            )
        for geometry_key in ("spacing_mm_xyz", "origin_mm_xyz", "direction"):
            anatomy_value = anatomy_metadata.get(geometry_key)
            stone_value = stone_metadata.get(geometry_key)
            if anatomy_value is not None and stone_value is not None and not np.allclose(
                anatomy_value, stone_value, atol=1e-5
            ):
                raise ValueError(f"Stone and anatomy {geometry_key} values do not match.")
        stones = extract_stones(
            stone_volume,
            args.spacing_mm,
            args.minimum_stone_voxels,
            args.maximum_stones,
        )
        if not stones:
            raise ValueError("No stone component passed the minimum voxel threshold.")
    else:
        stones = [
            {
                "stone_id": "stone_001",
                "voxel_count": 0,
                "volume_mm3": 0.0,
                "equivalent_diameter_mm": 0.0,
                "centroid_voxel_xyz": tuple(args.target),
                "source": "manual_target",
            }
        ]

    case_dir = args.output_dir / args.case_id
    case_dir.mkdir(parents=True, exist_ok=True)
    anatomy_file = case_dir / "anatomy.obj"
    stones_file = case_dir / "stones.obj"
    routes_file = case_dir / "routes.json"
    manifest_file = case_dir / "manifest.json"
    route_csv = case_dir / "route.csv"

    export_obj_meters(
        anatomy,
        anatomy_file,
        args.spacing_mm,
        args.mesh_smoothing_iterations,
        args.mesh_smoothing_relaxation,
        args.mesh_step_size,
        args.direction,
    )
    if stone_volume is not None:
        export_obj_meters(
            stone_volume,
            stones_file,
            args.spacing_mm,
            0,
            0.0,
            1,
            args.direction,
        )

    routes = []
    exported_stones = []
    for index, stone in enumerate(stones, start=1):
        route = build_route(
            anatomy,
            tuple(args.start),
            stone["centroid_voxel_xyz"],
            f"route_{index:03d}",
            stone["stone_id"],
            args,
        )
        routes.append(route)
        exported_stones.append(
            {
                "stone_id": stone["stone_id"],
                "centroid": point_object(
                    voxel_xyz_to_unity_m(stone["centroid_voxel_xyz"], args.spacing_mm, args.direction)
                ),
                "voxel_count": stone["voxel_count"],
                "volume_mm3": stone["volume_mm3"],
                "equivalent_diameter_mm": stone["equivalent_diameter_mm"],
                "source": stone.get("source", "segmentation_mask"),
            }
        )

    routes_document = {
        "schema_version": SCHEMA_VERSION,
        "case_id": args.case_id,
        "coordinate_space": "unity_meters_y_up",
        "routes": routes,
    }
    write_json(routes_file, routes_document)
    write_route_csv(routes, route_csv)

    manifest = {
        "schema_version": SCHEMA_VERSION,
        "case_id": args.case_id,
        "display_name": args.display_name or args.case_id,
        "generated_at": datetime.now(timezone.utc).isoformat(),
        "clinical_notice": CLINICAL_NOTICE,
        "volume_shape_zyx": list(anatomy.shape),
        "spacing_mm_xyz": point_object(args.spacing_mm),
        "spacing_assumed": spacing_assumed,
        "coordinate_system": {
            "source_index_order": "z,y,x",
            "unity_axes": "x,y-up,z",
            "unity_units": "meters",
            "conversion": "relative_patient=direction@(index*spacing); unity=(patient_x,patient_z,patient_y)/1000",
        },
        "source_geometry": {
            "origin_mm_xyz": list(source_origin),
            "direction": list(args.direction),
            "orientation_applied": anatomy_metadata.get("direction") is not None,
        },
        "files": {
            "anatomy": anatomy_file.name,
            "stones": stones_file.name if stone_volume is not None else "",
            "routes": routes_file.name,
            "thumbnail": "",
        },
        "stones": exported_stones,
        "route_count": len(routes),
        "source_summary": {
            "anatomy_format": anatomy_metadata["format"],
            "stone_source": "mask" if stone_volume is not None else "manual_target",
        },
    }
    write_json(manifest_file, manifest)

    # Compatibility files keep the original prototype and existing notebooks usable.
    first_route = dict(routes[0])
    first_route.update(
        {
            "schema_version": SCHEMA_VERSION,
            "case_name": args.case_id,
            "clinical_notice": CLINICAL_NOTICE,
            "volume_shape_zyx": list(anatomy.shape),
        }
    )
    write_json(case_dir / "vr_route_unity.json", first_route)
    write_json(case_dir / "case_manifest.json", manifest)

    from validate_vr_case import validate_case

    errors = validate_case(case_dir)
    if errors:
        raise RuntimeError("Case validation failed:\n- " + "\n- ".join(errors))
    return case_dir, manifest


def build_parser():
    parser = argparse.ArgumentParser(description="Build a versioned VR ureteroscopy case package.")
    parser.add_argument("--mask", type=Path, default=Path("map"), help="Anatomy mask: PNG directory, NPY, or NIfTI.")
    parser.add_argument("--stone-mask", type=Path, help="Optional stone mask in the same geometry as the anatomy.")
    parser.add_argument("--case-id", default="murillo_sample_case")
    parser.add_argument("--display-name")
    parser.add_argument("--output-dir", type=Path, default=Path("cases"))
    parser.add_argument("--start", type=legacy.parse_xyz, default=(253.0, 355.0, 20.0))
    parser.add_argument("--target", type=legacy.parse_xyz, default=(220.0, 174.0, 10.0))
    parser.add_argument("--spacing-mm", type=parse_spacing_mm)
    parser.add_argument("--minimum-stone-voxels", type=int, default=3)
    parser.add_argument("--maximum-stones", type=int, default=8)
    parser.add_argument("--path-point-ratio", type=float, default=0.5)
    parser.add_argument("--best-results-csv", type=Path, default=Path("resultados_completos.csv"))
    parser.add_argument("--risk-threshold", type=float, default=0.5)
    parser.add_argument("--extrapolation-threshold", type=float, default=0.1)
    parser.add_argument("--visual-samples", type=int, default=360)
    parser.add_argument("--mesh-smoothing-iterations", type=int, default=6)
    parser.add_argument("--mesh-smoothing-relaxation", type=float, default=0.08)
    parser.add_argument("--mesh-step-size", type=int, choices=(1, 2, 3), default=1)
    return parser


def main():
    args = build_parser().parse_args()
    if not 0 < args.path_point_ratio <= 1:
        raise ValueError("--path-point-ratio must be in (0, 1].")
    if args.minimum_stone_voxels < 1 or args.maximum_stones < 1:
        raise ValueError("Stone thresholds must be positive.")
    case_dir, manifest = build_case(args)
    print(f"Validated VR case: {case_dir}")
    print(f"Routes: {manifest['route_count']}")
    spacing = manifest["spacing_mm_xyz"]
    print(f"Spacing mm (x,y,z): {(spacing['x'], spacing['y'], spacing['z'])}")
    if manifest["spacing_assumed"]:
        print("WARNING: 2 mm isotropic spacing was assumed. Provide --spacing-mm for real cases.")
    print(CLINICAL_NOTICE)


if __name__ == "__main__":
    main()
