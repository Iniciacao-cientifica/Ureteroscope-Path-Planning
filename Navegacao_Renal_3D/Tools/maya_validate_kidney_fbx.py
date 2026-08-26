"""Round-trip validation for the kidney Maya master and FBX export."""

from __future__ import annotations

import argparse
import json
import shutil
import sys
import tempfile
from pathlib import Path

import maya.standalone

maya.standalone.initialize(name="python")

import maya.cmds as cmds  # noqa: E402


REQUIRED_MESHES = [
    "KidneyExterior",
    "CollectingSystemVisual",
    "CollectingSystemCollision_Inward",
    "RouteGuide",
    "Stone",
]
REQUIRED_ANCHORS = ["StartAnchor", "TargetAnchor", "MinimapAnchor"]


def load_fbx_plugin():
    maya_root = Path(sys.executable).resolve().parent.parent
    candidates = [
        maya_root / "plug-ins" / "fbx" / "plug-ins" / "fbxmaya.mll",
        maya_root / "plug-ins" / "fbxmaya.mll",
    ]
    for candidate in candidates:
        if candidate.exists():
            cmds.loadPlugin(str(candidate), quiet=True)
            return
    raise RuntimeError("fbxmaya plugin not found")


def find_exact_short_name(name, node_type=None):
    nodes = cmds.ls(type=node_type, long=True) if node_type else cmds.ls(long=True)
    matches = [node for node in nodes if node.rsplit("|", 1)[-1] == name]
    return matches[0] if matches else None


def inspect_scene(label):
    meshes = {}
    missing = []
    for name in REQUIRED_MESHES:
        transform = find_exact_short_name(name, "transform")
        if not transform:
            missing.append(name)
            continue
        shapes = cmds.listRelatives(transform, shapes=True, type="mesh", fullPath=True) or []
        if not shapes:
            missing.append(name)
            continue
        bbox = cmds.exactWorldBoundingBox(transform)
        meshes[name] = {
            "vertices": int(cmds.polyEvaluate(transform, vertex=True)),
            "triangles": int(cmds.polyEvaluate(transform, triangle=True)),
            "bounds_cm": [round(float(value), 4) for value in bbox],
        }
    anchors = {}
    for name in REQUIRED_ANCHORS:
        transform = find_exact_short_name(name, "transform")
        if not transform:
            missing.append(name)
        else:
            anchors[name] = [
                round(float(value), 4)
                for value in cmds.xform(transform, query=True, worldSpace=True, translation=True)
            ]
    return {
        "label": label,
        "missing_nodes": missing,
        "meshes": meshes,
        "anchors_cm": anchors,
        "linear_unit": cmds.currentUnit(query=True, linear=True),
    }


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--source-ma", type=Path, required=True)
    parser.add_argument("--fbx", type=Path, required=True)
    parser.add_argument("--report", type=Path)
    args = parser.parse_args()
    load_fbx_plugin()

    # Maya standalone on Windows may decode accented command-line paths using
    # the legacy code page. Stage both files under an ASCII-only temporary path
    # before opening them, while keeping the report at the requested location.
    with tempfile.TemporaryDirectory(prefix="kidney_validate_") as temp_name:
        temp_root = Path(temp_name)
        staged_ma = temp_root / "Kidney_Master.ma"
        staged_fbx = temp_root / "Kidney_Game.fbx"
        shutil.copy2(args.source_ma, staged_ma)
        shutil.copy2(args.fbx, staged_fbx)

        cmds.file(str(staged_ma).replace("\\", "/"), open=True, force=True, ignoreVersion=True)
        source = inspect_scene("maya_master")
        cmds.file(new=True, force=True)
        cmds.currentUnit(linear="cm")
        cmds.file(
            str(staged_fbx).replace("\\", "/"),
            i=True,
            type="FBX",
            ignoreVersion=True,
            mergeNamespacesOnClash=False,
            options="fbx",
        )
        fbx = inspect_scene("fbx_round_trip")

    counts_match = all(
        source["meshes"].get(name, {}).get("triangles")
        == fbx["meshes"].get(name, {}).get("triangles")
        for name in REQUIRED_MESHES
    )
    bounds_match = all(
        source["meshes"].get(name, {}).get("bounds_cm")
        == fbx["meshes"].get(name, {}).get("bounds_cm")
        for name in REQUIRED_MESHES
    )
    exterior_bounds = fbx["meshes"].get("KidneyExterior", {}).get("bounds_cm", [0] * 6)
    collecting_bounds = fbx["meshes"].get("CollectingSystemVisual", {}).get("bounds_cm", [0] * 6)
    stone_bounds = fbx["meshes"].get("Stone", {}).get("bounds_cm", [0] * 6)
    exterior_height_cm = exterior_bounds[4] - exterior_bounds[1]
    collecting_height_cm = collecting_bounds[4] - collecting_bounds[1]
    stone_dimensions_cm = [
        stone_bounds[3] - stone_bounds[0],
        stone_bounds[4] - stone_bounds[1],
        stone_bounds[5] - stone_bounds[2],
    ]
    physical_scale_pass = (
        12.0 <= exterior_height_cm <= 16.0
        and 14.0 <= collecting_height_cm <= 18.0
        and 0.5 <= max(stone_dimensions_cm) <= 0.8
    )
    passed = (
        not source["missing_nodes"]
        and not fbx["missing_nodes"]
        and counts_match
        and bounds_match
        and physical_scale_pass
    )
    report = {
        "passed": bool(passed),
        "triangle_counts_match": bool(counts_match),
        "world_bounds_match": bool(bounds_match),
        "physical_scale_pass": bool(physical_scale_pass),
        "measurements_cm": {
            "kidney_exterior_height": round(exterior_height_cm, 4),
            "collecting_system_with_ureter_height": round(collecting_height_cm, 4),
            "stone_dimensions": [round(value, 4) for value in stone_dimensions_cm],
        },
        "source": source,
        "fbx": fbx,
    }
    report_json = json.dumps(report, indent=2, ensure_ascii=False)
    if args.report:
        args.report.write_text(report_json, encoding="utf-8")
    print(report_json)
    raise SystemExit(0 if passed else 1)


if __name__ == "__main__":
    main()
