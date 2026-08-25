"""Create the editable Maya master and authoritative FBX from validated OBJs."""

from __future__ import annotations

import argparse
import math
import sys
from pathlib import Path

import maya.standalone

maya.standalone.initialize(name="python")

import maya.cmds as cmds  # noqa: E402
import maya.mel as mel  # noqa: E402


OBJECT_FILES = {
    "KidneyExterior": "KidneyExterior.obj",
    "CollectingSystemVisual": "CollectingSystemVisual.obj",
    "CollectingSystemCollision_Inward": "CollectingSystemCollision_Inward.obj",
    "RouteGuide": "RouteGuide.obj",
    "Stone": "Stone.obj",
}


def load_plugin(name, candidates):
    try:
        cmds.loadPlugin(name, quiet=True)
        return
    except RuntimeError:
        pass
    for candidate in candidates:
        if candidate.exists():
            cmds.loadPlugin(str(candidate), quiet=True)
            return
    raise RuntimeError(f"Could not load Maya plugin: {name}")


def create_standard_material(name, color, opacity=1.0, emission=None):
    shader = cmds.shadingNode("standardSurface", asShader=True, name=name)
    cmds.setAttr(f"{shader}.baseColor", *color, type="double3")
    cmds.setAttr(f"{shader}.base", 0.82)
    cmds.setAttr(f"{shader}.specularIOR", 1.38)
    cmds.setAttr(f"{shader}.specularRoughness", 0.42)
    cmds.setAttr(f"{shader}.coat", 0.12)
    cmds.setAttr(f"{shader}.coatRoughness", 0.28)
    if opacity < 1.0:
        cmds.setAttr(f"{shader}.opacity", opacity, opacity, opacity, type="double3")
        cmds.setAttr(f"{shader}.transmission", 0.12)
    if emission:
        cmds.setAttr(f"{shader}.emissionColor", *emission, type="double3")
        cmds.setAttr(f"{shader}.emission", 0.35)
    group = cmds.sets(renderable=True, noSurfaceShader=True, empty=True, name=f"{name}SG")
    cmds.connectAttr(f"{shader}.outColor", f"{group}.surfaceShader", force=True)
    return group


def import_single_obj(path, desired_name):
    before = set(cmds.ls(type="transform", long=True))
    cmds.file(
        str(path).replace("\\", "/"),
        i=True,
        type="OBJ",
        ignoreVersion=True,
        ra=True,
        mergeNamespacesOnClash=False,
        options="mo=1",
    )
    after = set(cmds.ls(type="transform", long=True))
    created = [node for node in after - before if cmds.listRelatives(node, shapes=True, type="mesh")]
    if len(created) != 1:
        raise RuntimeError(f"Expected one mesh transform from {path}; got {created}")
    return cmds.rename(created[0], desired_name)


def aim_euler(start, target):
    dx, dy, dz = [b - a for a, b in zip(start, target)]
    horizontal = math.sqrt(dx * dx + dz * dz)
    pitch = -math.degrees(math.atan2(dy, max(horizontal, 1e-9)))
    yaw = math.degrees(math.atan2(dx, dz))
    return pitch, yaw, 0.0


def create_locator(name, position_cm, target_cm=None, scale=0.55):
    locator = cmds.spaceLocator(name=name)[0]
    cmds.setAttr(f"{locator}.translate", *position_cm, type="double3")
    if target_cm:
        cmds.setAttr(f"{locator}.rotate", *aim_euler(position_cm, target_cm), type="double3")
    shape = cmds.listRelatives(locator, shapes=True)[0]
    cmds.setAttr(f"{shape}.localScale", scale, scale, scale, type="double3")
    return locator


def add_string_attribute(node, name, value):
    cmds.addAttr(node, longName=name, dataType="string")
    cmds.setAttr(f"{node}.{name}", value, type="string")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--obj-dir", type=Path, required=True)
    parser.add_argument("--source-ma", type=Path, required=True)
    parser.add_argument("--fbx", type=Path, required=True)
    parser.add_argument("--version", required=True)
    args = parser.parse_args()

    maya_root = Path(sys.executable).resolve().parent.parent
    load_plugin(
        "objExport",
        [maya_root / "bin" / "plug-ins" / "objExport.mll", maya_root / "plug-ins" / "objExport.mll"],
    )
    load_plugin(
        "fbxmaya",
        [maya_root / "plug-ins" / "fbx" / "plug-ins" / "fbxmaya.mll"],
    )

    cmds.file(new=True, force=True)
    cmds.currentUnit(linear="cm", angle="deg", time="film")
    root = cmds.group(empty=True, name="Kidney_Game_v001_ROOT")
    visual_group = cmds.group(empty=True, name="VISUAL", parent=root)
    gameplay_group = cmds.group(empty=True, name="GAMEPLAY", parent=root)
    anchor_group = cmds.group(empty=True, name="ANCHORS", parent=root)

    objects = {}
    for name, filename in OBJECT_FILES.items():
        objects[name] = import_single_obj(args.obj_dir / filename, name)

    cmds.parent(objects["KidneyExterior"], visual_group)
    cmds.parent(objects["CollectingSystemVisual"], visual_group)
    cmds.parent(objects["CollectingSystemCollision_Inward"], gameplay_group)
    cmds.parent(objects["RouteGuide"], gameplay_group)
    cmds.parent(objects["Stone"], gameplay_group)

    materials = {
        "KidneyExterior": create_standard_material("MAT_KidneyExterior", (0.48, 0.055, 0.095), 0.34),
        "CollectingSystemVisual": create_standard_material("MAT_CollectingSystem", (0.83, 0.22, 0.31)),
        "CollectingSystemCollision_Inward": create_standard_material("MAT_CollisionDebug", (0.06, 0.64, 0.86), 0.18),
        "RouteGuide": create_standard_material("MAT_RouteCyan", (0.0, 0.78, 1.0), emission=(0.0, 0.55, 0.8)),
        "Stone": create_standard_material("MAT_StoneGold", (0.92, 0.56, 0.08)),
    }
    for name, shading_group in materials.items():
        cmds.sets(objects[name], edit=True, forceElement=shading_group)

    # Basic editable UVs for Maya inspection and later material work. These do
    # not affect the authoritative object transforms or physical dimensions.
    for name in ("KidneyExterior", "CollectingSystemVisual", "Stone"):
        try:
            cmds.polyAutoProjection(
                objects[name],
                layoutMethod=1,
                insertBeforeDeformers=1,
                planes=6,
                percentageSpace=0.2,
                worldSpace=1,
            )
        except RuntimeError:
            pass

    start = create_locator("StartAnchor", (-4.3, -8.6, -0.4), (-4.2, -7.2, -0.33), 0.6)
    target = create_locator("TargetAnchor", (3.0, 1.5, 0.7), (2.1, 1.25, 0.5), 0.5)
    minimap = create_locator("MinimapAnchor", (15.5, -14.0, 11.0), (0.0, -0.4, 0.0), 0.7)
    cmds.parent(start, target, minimap, anchor_group)

    add_string_attribute(root, "assetVersion", args.version)
    add_string_attribute(root, "authoringUnits", "centimetres")
    add_string_attribute(root, "laterality", "lado a confirmar")
    cmds.addAttr(root, longName="gameplayVisualScale", attributeType="double", defaultValue=5.0)
    cmds.addAttr(root, longName="tipRadiusMM", attributeType="double", defaultValue=2.0)
    cmds.addAttr(root, longName="stoneDiameterMM", attributeType="double", defaultValue=6.0)

    collision_layer = cmds.createDisplayLayer(
        objects["CollectingSystemCollision_Inward"], name="COLLISION_DEBUG", number=1
    )
    cmds.setAttr(f"{collision_layer}.visibility", False)
    route_layer = cmds.createDisplayLayer(objects["RouteGuide"], name="ROUTE_GUIDE", number=2)
    cmds.setAttr(f"{route_layer}.displayType", 0)

    cmds.select(root, hierarchy=True)
    cmds.delete(constructionHistory=True)
    cmds.file(rename=str(args.source_ma).replace("\\", "/"))
    cmds.file(save=True, type="mayaAscii", force=True)

    mel.eval('FBXResetExport;')
    mel.eval('FBXExportFileVersion -v "FBX202000";')
    mel.eval('FBXExportUpAxis y;')
    mel.eval('FBXExportInAscii -v false;')
    mel.eval('FBXExportCameras -v false;')
    mel.eval('FBXExportLights -v false;')
    mel.eval('FBXExportEmbeddedTextures -v false;')
    mel.eval('FBXExportInputConnections -v true;')
    cmds.select(root, hierarchy=True, replace=True)
    cmds.file(
        str(args.fbx).replace("\\", "/"),
        force=True,
        options="v=0;",
        type="FBX export",
        preserveReferences=True,
        exportSelected=True,
    )
    print(f"MAYA_MASTER={args.source_ma}")
    print(f"FBX_EXPORT={args.fbx}")


if __name__ == "__main__":
    main()
