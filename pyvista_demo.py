import argparse
import csv
import json
import time
from pathlib import Path

import numpy as np

import data
import path_planning
from curves import bspline_curve, laplacian_curve, savgol_curve
from view import Viewer3D
from vr_export_pipeline import (
    CLINICAL_NOTICE,
    calculate_curvature_torsion,
    calculate_length,
    check_extrapolation,
    choose_safe_bspline,
    count_risk_points,
    export_obj,
    load_best_bspline_params,
    parse_xyz,
    point_list,
    select_route_points,
)


def build_parser():
    parser = argparse.ArgumentParser(description="Run the original PyVista-based 3D ureteroscopy demo.")
    parser.add_argument("--mask-dir", type=Path, default=Path("map"), help="Directory with binary PNG masks.")
    parser.add_argument("--case-name", default="murillo_pyvista_demo", help="Name used for exported files.")
    parser.add_argument("--output-dir", type=Path, default=Path("outputs/pyvista_demo"), help="Output directory.")
    parser.add_argument("--start", type=parse_xyz, default=(253.0, 355.0, 20.0), help="Start point as x,y,z.")
    parser.add_argument("--target", type=parse_xyz, default=(220.0, 174.0, 10.0), help="Target stone as x,y,z.")
    parser.add_argument("--curve", choices=["bspline", "savgol", "laplacian"], default="bspline")
    parser.add_argument("--path-point-ratio", type=float, default=0.5, help="Fraction of A* points kept before smoothing.")
    parser.add_argument("--best-results-csv", type=Path, default=Path("resultados_completos.csv"))
    parser.add_argument("--no-view", action="store_true", help="Compute and export files without opening PyVista.")
    parser.add_argument("--screenshot-only", action="store_true", help="Render one screenshot without opening a window.")
    return parser


def build_curve(curve_name, path_points, volume, target_xyz, args):
    if curve_name == "bspline":
        params = load_best_bspline_params(args.best_results_csv)
        selected = choose_safe_bspline(path_points, volume, target_xyz, params, 0.1, 0.5)
        return selected["points"], {"method": "bspline", **selected["params"]}

    if curve_name == "savgol":
        return savgol_curve(path_points, window_ratio=0.15, iterations=3, order=5), {
            "method": "savgol",
            "window_ratio": 0.15,
            "iterations": 3,
            "order": 5,
        }

    return laplacian_curve(path_points, iterations=10, lambda_factor=0.5), {
        "method": "laplacian",
        "iterations": 10,
        "lambda_factor": 0.5,
    }


def write_csv(path, original, curve):
    with path.open("w", newline="", encoding="utf-8") as handle:
        writer = csv.writer(handle)
        writer.writerow(["route", "index", "x", "y", "z"])
        for name, points in [("a_star", original), ("smoothed", curve)]:
            for index, point in enumerate(points):
                writer.writerow([name, index, point[0], point[1], point[2]])


def write_json(path, payload):
    with path.open("w", encoding="utf-8", newline="\n") as handle:
        json.dump(payload, handle, indent=2, ensure_ascii=False)
        handle.write("\n")


def main():
    args = build_parser().parse_args()
    start_time = time.time()
    args.output_dir.mkdir(parents=True, exist_ok=True)

    print("Iniciando demo PyVista...", flush=True)
    print(f"Carregando mascaras de: {args.mask_dir}", flush=True)
    volume = data.carregar_imagens_binarias(str(args.mask_dir))
    start_xyz = tuple(args.start)
    target_xyz = tuple(args.target)
    start_idx = tuple(int(round(value)) for value in start_xyz[::-1])
    target_idx = tuple(int(round(value)) for value in target_xyz[::-1])

    print("Calculando caminho A*...", flush=True)
    raw_path = path_planning.path_plan(volume, start_idx, target_idx)
    if not raw_path:
        raise RuntimeError("No valid path was found between start and target.")

    path_xyz = [tuple(float(value) for value in point[::-1]) for point in raw_path]
    reduced_path = select_route_points(path_xyz, args.path_point_ratio)
    print(f"Suavizando rota com {args.curve}...", flush=True)
    curve, curve_params = build_curve(args.curve, reduced_path, volume, target_xyz, args)

    print("Calculando metricas e extrapolacao...", flush=True)
    extrapolation = check_extrapolation(curve, volume, threshold=0.1)
    curve_metrics = {
        "case_name": args.case_name,
        "clinical_notice": CLINICAL_NOTICE,
        "volume_shape_zyx": list(volume.shape),
        "start": {"x": start_xyz[0], "y": start_xyz[1], "z": start_xyz[2]},
        "target": {"x": target_xyz[0], "y": target_xyz[1], "z": target_xyz[2]},
        "curve": curve_params,
        "metrics": {
            "path_points": len(path_xyz),
            "exported_path_points": len(reduced_path),
            "smoothed_points": len(curve),
            "path_length_voxels": calculate_length(path_xyz),
            "smoothed_length_voxels": calculate_length(curve),
            "risk_points": count_risk_points(curve, volume, threshold=0.5),
            "processing_seconds": time.time() - start_time,
            **calculate_curvature_torsion(curve),
            **extrapolation,
        },
        "path_original": point_list(reduced_path),
        "path_smoothed": point_list(curve),
    }

    print("Exportando arquivos do caso...", flush=True)
    write_json(args.output_dir / f"{args.case_name}_summary.json", curve_metrics)
    write_csv(args.output_dir / f"{args.case_name}_route.csv", reduced_path, curve)
    export_obj(volume, args.output_dir / f"{args.case_name}_surface.obj")

    print("PyVista demo case computed.", flush=True)
    print(f"Volume shape: {volume.shape}", flush=True)
    print(f"Original A* points: {len(path_xyz)}", flush=True)
    print(f"Exported points: {len(reduced_path)}", flush=True)
    print(f"Smoothed points: {len(curve)}", flush=True)
    print(f"Outside points: {curve_metrics['metrics']['outside_points']}", flush=True)
    print(f"Risk points: {curve_metrics['metrics']['risk_points']}", flush=True)
    print(f"Outputs: {args.output_dir}", flush=True)

    if args.no_view:
        return

    print("Preparando janela 3D do PyVista...", flush=True)
    screenshot_path = args.output_dir / f"{args.case_name}_screenshot.png"
    viewer = Viewer3D(
        volume,
        reduced_path,
        target_xyz,
        start_xyz,
        curve,
        screenshot_path=str(screenshot_path),
        off_screen=args.screenshot_only,
    )

    if args.screenshot_only:
        viewer.save_screenshot()
        viewer.plotter.close()
        return

    print("Abrindo janela. Se ela nao aparecer, verifique se ficou atras do VS Code.", flush=True)
    viewer.show()


if __name__ == "__main__":
    main()
