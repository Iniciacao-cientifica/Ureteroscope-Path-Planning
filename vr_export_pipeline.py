import argparse
import csv
import json
import math
import time
from datetime import datetime, timezone
from pathlib import Path

import numpy as np
from scipy.interpolate import splprep, splev
from scipy.ndimage import map_coordinates
from scipy.spatial import KDTree
from skimage import measure

import data
import path_planning
from curves import bspline_curve


CLINICAL_NOTICE = (
    "Academic/educational prototype for pre-operative planning and training. "
    "Not validated for clinical diagnosis, intra-operative navigation, or patient care."
)


def parse_xyz(value):
    parts = [part.strip() for part in value.split(",")]
    if len(parts) != 3:
        raise argparse.ArgumentTypeError("Use x,y,z, for example 253,355,20")
    try:
        return tuple(float(part) for part in parts)
    except ValueError as exc:
        raise argparse.ArgumentTypeError("Coordinates must be numeric") from exc


def point_object(point):
    return {"x": float(point[0]), "y": float(point[1]), "z": float(point[2])}


def point_list(points):
    return [point_object(point) for point in points]


def calculate_length(points):
    if len(points) < 2:
        return 0.0
    array = np.asarray(points, dtype=float)
    return float(np.sum(np.linalg.norm(np.diff(array, axis=0), axis=1)))


def calculate_curvature_torsion(points):
    array = np.asarray(points, dtype=float)
    if len(array) < 5:
        return {
            "curvature_mean": 0.0,
            "curvature_max": 0.0,
            "torsion_mean": 0.0,
            "torsion_max": 0.0,
        }

    try:
        spline_order = min(3, len(array) - 1)
        tck, u = splprep(array.T, s=0, k=spline_order)
        first = splev(u, tck, der=1)
        second = splev(u, tck, der=2)
        third = splev(u, tck, der=3) if spline_order >= 3 else None
    except Exception:
        return {
            "curvature_mean": 0.0,
            "curvature_max": 0.0,
            "torsion_mean": 0.0,
            "torsion_max": 0.0,
        }

    curvatures = []
    torsions = []
    for index in range(len(u)):
        d1 = np.array([axis[index] for axis in first], dtype=float)
        d2 = np.array([axis[index] for axis in second], dtype=float)
        cross = np.cross(d1, d2)
        denom = np.linalg.norm(d1) ** 3
        curvature = np.linalg.norm(cross) / denom if denom > 1e-12 else 0.0
        curvatures.append(curvature)

        if third is None:
            torsions.append(0.0)
        else:
            d3 = np.array([axis[index] for axis in third], dtype=float)
            torsion_denom = np.linalg.norm(cross) ** 2
            torsion = np.dot(cross, d3) / torsion_denom if torsion_denom > 1e-12 else 0.0
            torsions.append(torsion)

    curvature_array = np.nan_to_num(np.asarray(curvatures, dtype=float))
    torsion_array = np.nan_to_num(np.asarray(torsions, dtype=float))
    return {
        "curvature_mean": float(np.mean(curvature_array)),
        "curvature_max": float(np.max(curvature_array)),
        "torsion_mean": float(np.mean(np.abs(torsion_array))),
        "torsion_max": float(np.max(np.abs(torsion_array))),
    }


def check_extrapolation(points, volume, threshold=0.1):
    valid_points = np.argwhere(volume == 1)
    valid_tree = KDTree(valid_points)
    outside_indices = []
    distances = []

    for index, point in enumerate(points):
        x, y, z = point
        if (
            z < 0
            or z >= volume.shape[0]
            or y < 0
            or y >= volume.shape[1]
            or x < 0
            or x >= volume.shape[2]
        ):
            outside_indices.append(index)
            distances.append(math.inf)
            continue

        value = map_coordinates(volume, [[z], [y], [x]], order=1, mode="nearest")[0]
        if value < 0.5:
            distance, _ = valid_tree.query([z, y, x])
            if distance > threshold:
                outside_indices.append(index)
                distances.append(float(distance))

    finite_distances = [distance for distance in distances if math.isfinite(distance)]
    return {
        "outside_points": len(outside_indices),
        "outside_percent": 100.0 * len(outside_indices) / max(1, len(points)),
        "outside_indices": outside_indices,
        "outside_max_distance": max(finite_distances) if finite_distances else 0.0,
        "outside_mean_distance": float(np.mean(finite_distances)) if finite_distances else 0.0,
    }


def count_risk_points(points, volume, threshold=0.5):
    obstacle_points = np.argwhere(volume == 0)
    if len(obstacle_points) == 0:
        return 0
    tree = KDTree(obstacle_points)
    count = 0
    for point in points:
        x, y, z = point
        distance, _ = tree.query([z, y, x])
        if distance < threshold:
            count += 1
    return count


def select_route_points(points, ratio):
    if ratio >= 1.0:
        return list(points)
    return path_planning.reduzir_pontos_porcentagem(points, porcentagem=ratio)


def load_best_bspline_params(csv_path):
    defaults = {"order": 3, "smooth_factor": 1.0}
    if not csv_path or not csv_path.exists():
        return defaults

    best_row = None
    best_fitness = float("inf")
    with csv_path.open("r", newline="", encoding="utf-8") as handle:
        reader = csv.DictReader(handle, delimiter=";")
        for row in reader:
            if row.get("technique") != "B-Spline":
                continue
            try:
                fitness = float(row["fitness"].replace(",", "."))
            except (KeyError, ValueError):
                continue
            if fitness < best_fitness:
                best_fitness = fitness
                best_row = row

    if not best_row:
        return defaults

    return {
        "order": int(best_row["order"]) if best_row.get("order") else defaults["order"],
        "smooth_factor": (
            float(best_row["smooth_factor"].replace(",", "."))
            if best_row.get("smooth_factor")
            else defaults["smooth_factor"]
        ),
    }


def choose_safe_bspline(path_points, volume, target_xyz, initial_params, extrapolation_threshold, risk_threshold):
    candidates = [initial_params]
    for order in [1, 2, 3]:
        for smooth_factor in [0.0, 0.5, 1.0, 2.0, 5.0, 10.0, 20.0, 50.0]:
            candidate = {"order": order, "smooth_factor": smooth_factor}
            if candidate not in candidates:
                candidates.append(candidate)

    best = None
    target = np.asarray(target_xyz, dtype=float)
    for params in candidates:
        try:
            smoothed = bspline_curve(
                path_points,
                order=min(params["order"], len(path_points) - 1),
                smooth_factor=params["smooth_factor"],
            )
        except Exception:
            continue

        extrapolation = check_extrapolation(smoothed, volume, threshold=extrapolation_threshold)
        risk_points = count_risk_points(smoothed, volume, threshold=risk_threshold)
        curvature = calculate_curvature_torsion(smoothed)
        final_error = float(np.linalg.norm(np.asarray(smoothed[-1], dtype=float) - target))
        score = (
            extrapolation["outside_points"],
            risk_points,
            final_error,
            curvature["curvature_max"],
            calculate_length(smoothed),
        )
        current = {
            "params": params,
            "points": smoothed,
            "score": score,
            "extrapolation": extrapolation,
            "risk_points": risk_points,
        }
        if best is None or score < best["score"]:
            best = current
            if score[0] == 0 and score[1] == 0:
                break

    if best is None:
        raise RuntimeError("Could not generate a valid B-Spline route.")

    return best


def export_obj(volume, output_path):
    verts, faces, normals, _ = measure.marching_cubes(volume.astype(np.float32), level=0.5)
    # skimage returns coordinates in array order (z, y, x). Unity-friendly export uses x, y, z.
    xyz_verts = np.column_stack([verts[:, 2], verts[:, 1], verts[:, 0]])
    xyz_normals = np.column_stack([normals[:, 2], normals[:, 1], normals[:, 0]])

    with output_path.open("w", encoding="utf-8", newline="\n") as handle:
        handle.write("# Urinary tract mesh generated from binary mask volume\n")
        for vert in xyz_verts:
            handle.write(f"v {vert[0]:.6f} {vert[1]:.6f} {vert[2]:.6f}\n")
        for normal in xyz_normals:
            handle.write(f"vn {normal[0]:.6f} {normal[1]:.6f} {normal[2]:.6f}\n")
        for face in faces:
            a, b, c = face + 1
            handle.write(f"f {a}//{a} {b}//{b} {c}//{c}\n")


def write_route_csv(path_original, path_smoothed, output_path):
    with output_path.open("w", newline="", encoding="utf-8") as handle:
        writer = csv.writer(handle)
        writer.writerow(["route", "index", "x", "y", "z"])
        for route_name, points in [("a_star", path_original), ("bspline", path_smoothed)]:
            for index, point in enumerate(points):
                writer.writerow([route_name, index, point[0], point[1], point[2]])


def write_json(path, payload):
    with path.open("w", encoding="utf-8", newline="\n") as handle:
        json.dump(payload, handle, ensure_ascii=False, indent=2)
        handle.write("\n")


def build_export(args):
    start_time = time.time()
    case_dir = args.output_dir / args.case_name
    case_dir.mkdir(parents=True, exist_ok=True)

    volume = data.carregar_imagens_binarias(str(args.mask_dir))
    start_xyz = tuple(args.start)
    target_xyz = tuple(args.target)
    start_idx = tuple(int(round(value)) for value in start_xyz[::-1])
    target_idx = tuple(int(round(value)) for value in target_xyz[::-1])

    planned = path_planning.path_plan(volume, start_idx, target_idx)
    if not planned:
        raise RuntimeError("No valid path found between start and target in the mask volume.")

    path_xyz = [tuple(float(value) for value in point[::-1]) for point in planned]
    reduced_path = select_route_points(path_xyz, args.path_point_ratio)

    bspline_params = load_best_bspline_params(args.best_results_csv)
    selected_bspline = choose_safe_bspline(
        reduced_path,
        volume,
        target_xyz,
        bspline_params,
        args.extrapolation_threshold,
        args.risk_threshold,
    )
    smoothed = selected_bspline["points"]
    bspline_params = selected_bspline["params"]

    route_csv = case_dir / "route.csv"
    route_json = case_dir / "vr_route_unity.json"
    manifest_json = case_dir / "case_manifest.json"
    mesh_obj = case_dir / "urinary_tract.obj"

    metrics = {
        "path_points": len(path_xyz),
        "exported_path_points": len(reduced_path),
        "smoothed_points": len(smoothed),
        "path_length_voxels": calculate_length(path_xyz),
        "smoothed_length_voxels": calculate_length(smoothed),
        "risk_points": selected_bspline["risk_points"],
        "final_error_voxels": float(np.linalg.norm(np.asarray(smoothed[-1]) - np.asarray(target_xyz))),
        "processing_seconds": float(time.time() - start_time),
    }
    metrics.update(calculate_curvature_torsion(smoothed))
    metrics.update(selected_bspline["extrapolation"])

    unity_route = {
        "case_name": args.case_name,
        "clinical_notice": CLINICAL_NOTICE,
        "volume_shape_zyx": list(volume.shape),
        "start": point_object(start_xyz),
        "target": point_object(target_xyz),
        "path_original": point_list(reduced_path),
        "path_smoothed": point_list(smoothed),
        "metrics": metrics,
    }

    manifest = {
        "schema_version": 1,
        "generated_at": datetime.now(timezone.utc).isoformat(),
        "case_name": args.case_name,
        "clinical_notice": CLINICAL_NOTICE,
        "input": {
            "mask_dir": str(args.mask_dir),
            "start_xyz": list(start_xyz),
            "target_xyz": list(target_xyz),
        },
        "coordinate_system": {
            "volume": "Binary mask volume is indexed as z,y,x.",
            "exported_points": "Route and OBJ vertices are exported as x,y,z voxel coordinates.",
            "unity": "Unity loader applies voxelToMeterScale and optional axis transform.",
        },
        "files": {
            "route_csv": route_csv.name,
            "route_unity_json": route_json.name,
            "urinary_tract_obj": mesh_obj.name,
        },
        "bspline": bspline_params,
        "bspline_selection": {
            "score_order": [
                "outside_points",
                "risk_points",
                "final_error_voxels",
                "curvature_max",
                "smoothed_length_voxels",
            ],
            "selected_score": list(selected_bspline["score"]),
        },
        "metrics": metrics,
    }

    export_obj(volume, mesh_obj)
    write_route_csv(reduced_path, smoothed, route_csv)
    write_json(route_json, unity_route)
    write_json(manifest_json, manifest)

    return case_dir, manifest


def build_parser():
    parser = argparse.ArgumentParser(
        description="Export a ureteroscopy path-planning case for a Unity/OpenXR VR prototype."
    )
    parser.add_argument("--mask-dir", type=Path, default=Path("map"), help="Directory with binary PNG masks.")
    parser.add_argument("--case-name", default="murillo_sample_case", help="Name for the export folder.")
    parser.add_argument("--output-dir", type=Path, default=Path("exports"), help="Directory for VR exports.")
    parser.add_argument("--start", type=parse_xyz, default=(253.0, 355.0, 20.0), help="Start point as x,y,z.")
    parser.add_argument("--target", type=parse_xyz, default=(220.0, 174.0, 10.0), help="Target stone point as x,y,z.")
    parser.add_argument(
        "--path-point-ratio",
        type=float,
        default=0.5,
        help="Fraction of A* points kept before B-Spline smoothing. Thesis default: 0.5.",
    )
    parser.add_argument(
        "--best-results-csv",
        type=Path,
        default=Path("resultados_completos.csv"),
        help="CSV used to select B-Spline parameters.",
    )
    parser.add_argument("--risk-threshold", type=float, default=0.5, help="Wall proximity threshold in voxels.")
    parser.add_argument(
        "--extrapolation-threshold",
        type=float,
        default=0.1,
        help="Allowed distance outside the valid mask before a point is counted as extrapolated.",
    )
    return parser


def main():
    args = build_parser().parse_args()
    if not 0 < args.path_point_ratio <= 1:
        raise ValueError("--path-point-ratio must be in the interval (0, 1].")

    case_dir, manifest = build_export(args)
    print(f"VR export written to: {case_dir}")
    print(f"Mesh: {manifest['files']['urinary_tract_obj']}")
    print(f"Route: {manifest['files']['route_unity_json']}")
    print(f"Smoothed length: {manifest['metrics']['smoothed_length_voxels']:.2f} voxels")
    print(f"Outside points: {manifest['metrics']['outside_points']}")
    print(CLINICAL_NOTICE)


if __name__ == "__main__":
    main()
