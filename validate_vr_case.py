"""Validate a generated VR case without exposing source patient paths."""

from __future__ import annotations

import argparse
import json
import math
import re
from pathlib import Path


SAFE_CASE_ID = re.compile(r"^[A-Za-z0-9][A-Za-z0-9_-]{0,63}$")
FORBIDDEN_KEYS = {"patient_name", "patient_id", "birth_date", "date_of_birth", "cpf"}


def load_json(path):
    with path.open("r", encoding="utf-8") as handle:
        return json.load(handle)


def is_finite_point(point):
    return isinstance(point, dict) and all(
        key in point and isinstance(point[key], (int, float)) and math.isfinite(point[key])
        for key in ("x", "y", "z")
    )


def walk_forbidden_keys(value, location="root"):
    errors = []
    if isinstance(value, dict):
        for key, child in value.items():
            if key.lower() in FORBIDDEN_KEYS:
                errors.append(f"Forbidden identifying field at {location}.{key}.")
            errors.extend(walk_forbidden_keys(child, f"{location}.{key}"))
    elif isinstance(value, list):
        for index, child in enumerate(value):
            errors.extend(walk_forbidden_keys(child, f"{location}[{index}]"))
    return errors


def validate_case(case_dir):
    case_dir = Path(case_dir)
    errors = []
    manifest_path = case_dir / "manifest.json"
    routes_path = case_dir / "routes.json"
    if not manifest_path.is_file():
        return ["manifest.json is missing."]
    if not routes_path.is_file():
        return ["routes.json is missing."]

    try:
        manifest = load_json(manifest_path)
        routes_document = load_json(routes_path)
    except (OSError, json.JSONDecodeError) as exc:
        return [f"Invalid JSON: {exc}"]

    if manifest.get("schema_version") != 2 or routes_document.get("schema_version") != 2:
        errors.append("Expected schema_version 2 in manifest and routes.")
    case_id = manifest.get("case_id", "")
    if not SAFE_CASE_ID.fullmatch(case_id):
        errors.append("case_id is missing or is not anonymized-safe.")
    if routes_document.get("case_id") != case_id:
        errors.append("Manifest and routes case_id values differ.")

    files = manifest.get("files", {})
    for key in ("anatomy", "routes"):
        file_name = files.get(key, "")
        if not file_name or Path(file_name).name != file_name:
            errors.append(f"Invalid relative file name for {key}.")
        elif not (case_dir / file_name).is_file():
            errors.append(f"Declared {key} file does not exist: {file_name}.")
    stone_file = files.get("stones", "")
    if stone_file and not (case_dir / stone_file).is_file():
        errors.append(f"Declared stone file does not exist: {stone_file}.")

    routes = routes_document.get("routes")
    if not isinstance(routes, list) or not routes:
        errors.append("At least one route is required.")
        routes = []
    for route_index, route in enumerate(routes):
        prefix = f"routes[{route_index}]"
        for key in ("start", "target"):
            if not is_finite_point(route.get(key)):
                errors.append(f"{prefix}.{key} is not a finite point.")
        for key in ("path_original", "path_smoothed", "path_visual"):
            points = route.get(key)
            if not isinstance(points, list) or len(points) < 2:
                errors.append(f"{prefix}.{key} needs at least two points.")
            elif not all(is_finite_point(point) for point in points):
                errors.append(f"{prefix}.{key} contains a non-finite point.")
        metrics = route.get("metrics", {})
        if metrics.get("outside_points") != 0:
            errors.append(f"{prefix} leaves the navigable mask.")
        if metrics.get("smoothed_length_mm", 0) <= 0:
            errors.append(f"{prefix} has no positive physical length.")

    if manifest.get("route_count") != len(routes):
        errors.append("route_count does not match routes.json.")
    errors.extend(walk_forbidden_keys(manifest))
    errors.extend(walk_forbidden_keys(routes_document))
    return errors


def main():
    parser = argparse.ArgumentParser(description="Validate a VR case package.")
    parser.add_argument("case_dir", type=Path)
    args = parser.parse_args()
    errors = validate_case(args.case_dir)
    if errors:
        for error in errors:
            print(f"ERROR: {error}")
        raise SystemExit(1)
    print(f"VR case is valid: {args.case_dir}")


if __name__ == "__main__":
    main()
