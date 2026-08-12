"""Copy validated case packages into Unity StreamingAssets and rebuild the catalog."""

from __future__ import annotations

import argparse
import json
import shutil
from pathlib import Path

from validate_vr_case import validate_case


def write_json(path, payload):
    with path.open("w", encoding="utf-8", newline="\n") as handle:
        json.dump(payload, handle, ensure_ascii=False, indent=2, allow_nan=False)
        handle.write("\n")


def sync_case(case_dir, unity_project):
    case_dir = case_dir.resolve()
    unity_project = unity_project.resolve()
    errors = validate_case(case_dir)
    if errors:
        raise ValueError("Cannot sync invalid case:\n- " + "\n- ".join(errors))

    with (case_dir / "manifest.json").open("r", encoding="utf-8") as handle:
        manifest = json.load(handle)
    case_id = manifest["case_id"]

    cases_root = unity_project / "Assets" / "StreamingAssets" / "Cases"
    cases_root.mkdir(parents=True, exist_ok=True)
    destination = (cases_root / case_id).resolve()
    if cases_root not in destination.parents:
        raise ValueError("Refusing to sync outside Unity StreamingAssets/Cases.")
    if destination.exists():
        shutil.rmtree(destination)
    shutil.copytree(case_dir, destination)

    catalog_entries = []
    for child in sorted(path for path in cases_root.iterdir() if path.is_dir()):
        manifest_path = child / "manifest.json"
        if not manifest_path.is_file():
            continue
        with manifest_path.open("r", encoding="utf-8") as handle:
            item = json.load(handle)
        catalog_entries.append(
            {
                "case_id": item["case_id"],
                "display_name": item.get("display_name", item["case_id"]),
                "manifest_file": f"{item['case_id']}/manifest.json",
            }
        )

    write_json(cases_root / "catalog.json", {"schema_version": 1, "cases": catalog_entries})
    return destination, len(catalog_entries)


def main():
    parser = argparse.ArgumentParser(description="Sync a VR case into a Unity project.")
    parser.add_argument("case_dir", type=Path)
    parser.add_argument("--unity-project", type=Path, default=Path("UnityVRPrototype"))
    args = parser.parse_args()
    destination, count = sync_case(args.case_dir, args.unity_project)
    print(f"Synced case to: {destination}")
    print(f"Catalog cases: {count}")


if __name__ == "__main__":
    main()
