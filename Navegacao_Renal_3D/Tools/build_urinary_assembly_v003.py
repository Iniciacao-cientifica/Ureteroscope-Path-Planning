"""Monta no Maya a mesma combinação visual usada pelo Unity no Marco 3.2."""

from __future__ import annotations

import argparse
import json
from pathlib import Path

import maya.standalone

maya.standalone.initialize(name="python")
from maya import cmds  # noqa: E402


def mesh_world_points(root):
    points = []
    shapes = cmds.listRelatives(root, allDescendents=True, type="mesh", fullPath=True) or []
    for shape in shapes:
        for index in range(cmds.polyEvaluate(shape, vertex=True)):
            points.append(cmds.pointPosition(f"{shape}.vtx[{index}]", world=True))
    return points


def left_ureter_top_center(root):
    points = [point for point in mesh_world_points(root) if point[0] < 0.0]
    top_y = max(point[1] for point in points)
    band = [point for point in points if point[1] >= top_y - 0.3]
    return [sum(point[axis] for point in band) / len(band) for axis in range(3)]


def bounds_center(node):
    bounds = cmds.exactWorldBoundingBox(node)
    return [(bounds[index] + bounds[index + 3]) * 0.5 for index in range(3)]


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--kidney", type=Path, required=True)
    parser.add_argument("--meshy", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--report", type=Path, required=True)
    args = parser.parse_args()

    cmds.file(new=True, force=True)
    cmds.currentUnit(linear="cm", angle="deg", time="film")
    cmds.file(str(args.kidney), i=True, namespace="kidney", mergeNamespacesOnClash=False, options="v=0;")
    cmds.file(str(args.meshy), i=True, namespace="meshy", mergeNamespacesOnClash=False, options="v=0;")

    assembly = cmds.group(empty=True, name="Urinary_System_Assembly_v003")
    kidney_root = "kidney:Kidney_Game_v003_ROOT"
    meshy_export = "meshy:MeshyUrinaryVisual_Export"
    lower_system = "meshy:Meshy_UretersAndBladder"
    marker = "kidney:JunctionInterfaceMarker"

    ureter_before = left_ureter_top_center(lower_system)
    interface_center = bounds_center(marker)
    translation = [interface_center[index] - ureter_before[index] for index in range(3)]
    cmds.move(*translation, meshy_export, relative=True, worldSpace=True)
    ureter_after = left_ureter_top_center(lower_system)

    kidney_group = cmds.group(empty=True, name="ActiveKidney_v003")
    meshy_group = cmds.group(empty=True, name="MeshyVisual_v002")
    cmds.parent(kidney_root, kidney_group)
    cmds.parent(meshy_export, meshy_group)
    cmds.parent(kidney_group, meshy_group, assembly)

    for node in (marker, "kidney:CollectingSystemCollision_Inward", "kidney:RouteGuide"):
        if cmds.objExists(node):
            cmds.setAttr(f"{node}.visibility", False)
    if cmds.objExists("meshy:REFERENCE_ONLY"):
        cmds.setAttr("meshy:REFERENCE_ONLY.visibility", False)

    cmds.addAttr(assembly, longName="assetVersion", dataType="string")
    cmds.setAttr(f"{assembly}.assetVersion", "v003", type="string")
    cmds.addAttr(assembly, longName="unityVisualScale", attributeType="double", defaultValue=5.0)
    cmds.addAttr(assembly, longName="interfaceDistanceMM", attributeType="double", defaultValue=0.0)
    distance = sum((ureter_after[index] - interface_center[index]) ** 2 for index in range(3)) ** 0.5 * 10.0
    cmds.setAttr(f"{assembly}.interfaceDistanceMM", distance)

    args.output.parent.mkdir(parents=True, exist_ok=True)
    cmds.file(rename=str(args.output))
    cmds.file(save=True, type="mayaAscii", force=True)

    report = {
        "assembly": str(args.output),
        "units": "centimetres",
        "kidney_version": "v003",
        "meshy_version": "v002",
        "interface_center_cm": interface_center,
        "meshy_translation_cm": translation,
        "interface_distance_mm": distance,
        "unity_visual_scale": 5.0,
    }
    args.report.parent.mkdir(parents=True, exist_ok=True)
    args.report.write_text(json.dumps(report, indent=2), encoding="utf-8")
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
