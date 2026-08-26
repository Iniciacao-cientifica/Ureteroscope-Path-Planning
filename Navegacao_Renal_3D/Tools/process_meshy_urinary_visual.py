"""Prepara o sistema urinário Meshy de alta resolução para Maya e Unity.

Executar com o mayapy do Maya 2027. A geometria de alta resolução não possui
UVs, portanto o atlas do primeiro pacote Meshy é transferido por proximidade.
O rim esquerdo gerado por IA permanece apenas como referência no arquivo Maya
e não é incluído no FBX destinado ao Unity.
"""

from __future__ import annotations

import hashlib
import json
import os
from pathlib import Path

import maya.standalone


CANDIDATE_ROOT = Path(
    r"C:\Users\pedro\OneDrive\Área de Trabalho\Navegacao_Renal_3D_Maya_v01"
    r"\Candidates\Meshy_Urinary_System_v002"
)
IMPORT_DIR = CANDIDATE_ROOT / "Imports"
SOURCE_DIR = CANDIDATE_ROOT / "Source"
EXPORT_DIR = CANDIDATE_ROOT / "Exports"
DOCUMENTATION_DIR = CANDIDATE_ROOT / "Documentation"

HIGH_FBX = IMPORT_DIR / "Meshy_Urinary_System_High_v002.fbx"
DONOR_FBX = IMPORT_DIR / "Meshy_Urinary_TextureDonor_v001.fbx"
SCENE_PATH = SOURCE_DIR / "Meshy_Urinary_System_v002.ma"
EXPORT_FBX = EXPORT_DIR / "Meshy_Urinary_Visual_v002.fbx"
REPORT_PATH = DOCUMENTATION_DIR / "meshy_v002_report.json"

TEXTURE_PATHS = {
    "base_color_texture": IMPORT_DIR / "T_MeshyUrinary_BaseColor_v002.png",
    "normalmap_texture": IMPORT_DIR / "T_MeshyUrinary_Normal_v002.png",
    "metallic_texture": IMPORT_DIR / "T_MeshyUrinary_Metallic_v002.png",
    "roughness_texture": IMPORT_DIR / "T_MeshyUrinary_Roughness_v002.png",
}


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def bounds_size(bounds: list[float]) -> list[float]:
    return [bounds[index + 3] - bounds[index] for index in range(3)]


def bounds_center(bounds: list[float]) -> list[float]:
    return [(bounds[index] + bounds[index + 3]) * 0.5 for index in range(3)]


def mesh_shape(transform: str) -> str:
    from maya import cmds

    shapes = cmds.listRelatives(transform, shapes=True, fullPath=True, noIntermediate=True) or []
    if not shapes:
        raise RuntimeError(f"Transform sem mesh: {transform}")
    return shapes[0]


def import_single_mesh(path: Path, target_name: str) -> tuple[str, str]:
    from maya import cmds

    before = set(cmds.ls(type="mesh", long=True, noIntermediate=True) or [])
    cmds.file(
        str(path),
        i=True,
        type="FBX",
        ignoreVersion=True,
        ra=True,
        mergeNamespacesOnClash=False,
        options="fbx",
    )
    imported = list(set(cmds.ls(type="mesh", long=True, noIntermediate=True) or []) - before)
    if len(imported) != 1:
        raise RuntimeError(f"Esperava uma malha em {path.name}; encontrei {len(imported)}")
    transform = (cmds.listRelatives(imported[0], parent=True, fullPath=True) or [None])[0]
    transform = cmds.rename(transform, target_name)
    return transform, mesh_shape(transform)


def non_manifold_components(shape: str) -> list[str]:
    from maya import cmds

    try:
        return cmds.polyInfo(shape, nonManifoldVertices=True) or []
    except RuntimeError:
        return []


def remap_texture_nodes() -> list[dict[str, object]]:
    from maya import cmds

    result: list[dict[str, object]] = []
    for node_name, texture_path in TEXTURE_PATHS.items():
        candidates = cmds.ls(node_name, type="file") or []
        if not candidates:
            raise RuntimeError(f"Nó de textura não encontrado: {node_name}")
        node = candidates[0]
        cmds.setAttr(node + ".fileTextureName", str(texture_path).replace("\\", "/"), type="string")
        if cmds.attributeQuery("colorSpace", node=node, exists=True):
            color_space = "sRGB" if node_name == "base_color_texture" else "Raw"
            try:
                cmds.setAttr(node + ".colorSpace", color_space, type="string")
            except RuntimeError:
                pass
        result.append(
            {
                "node": node,
                "path": str(texture_path),
                "exists": texture_path.is_file(),
                "sha256": sha256(texture_path),
            }
        )
    return result


def classify_parts(parts: list[str]) -> dict[str, list[str]]:
    from maya import cmds

    data = []
    for part in parts:
        bounds = cmds.exactWorldBoundingBox(part)
        size = bounds_size(bounds)
        center = bounds_center(bounds)
        data.append({"part": part, "bounds": bounds, "size": size, "center": center})

    lower = [item for item in data if item["bounds"][1] <= 1.0 and item["size"][1] > 25.0]
    kidneys = [item for item in data if item["bounds"][1] > 25.0 and item["size"][0] > 7.0]
    if len(lower) != 1 or len(kidneys) != 2:
        raise RuntimeError(
            f"Classificação inesperada: lower={len(lower)}, kidneys={len(kidneys)}, total={len(parts)}"
        )

    # A conversao Maya (right-handed) -> Unity (left-handed) inverte X no FBX.
    # Portanto, o shell Maya em +X torna-se o rim esquerdo no Unity e fica
    # somente como referencia; o shell Maya em -X torna-se o rim direito
    # fechado que deve ser exportado para a cena do jogo.
    left = max(kidneys, key=lambda item: item["center"][0])
    right = min(kidneys, key=lambda item: item["center"][0])
    groups = {
        "lower": [lower[0]["part"]],
        "left": [left["part"]],
        "right": [right["part"]],
    }
    used = {lower[0]["part"], left["part"], right["part"]}
    details = [item for item in data if item["part"] not in used]
    for item in details:
        if item["bounds"][1] < 25.0 or item["center"][1] < 30.0:
            groups["lower"].append(item["part"])
        elif item["center"][0] < 0.0:
            groups["right"].append(item["part"])
        else:
            groups["left"].append(item["part"])
    return groups


def unite_and_name(parts: list[str], name: str) -> str:
    from maya import cmds

    if not parts:
        raise RuntimeError(f"Nenhuma parte para {name}")
    if len(parts) == 1:
        result = cmds.rename(parts[0], name)
    else:
        united = cmds.polyUnite(*parts, constructionHistory=False, mergeUVSets=1, name=name) or []
        transforms = [item for item in united if cmds.objExists(item) and cmds.nodeType(item) == "transform"]
        if not transforms:
            raise RuntimeError(f"Falha ao unir as partes de {name}")
        result = transforms[0]
    shape = mesh_shape(result)
    uv_sets = cmds.polyUVSet(shape, query=True, allUVSets=True) or []
    if "UVMap" in uv_sets:
        cmds.polyUVSet(shape, currentUVSet=True, uvSet="UVMap")
    return result


def describe_transform(transform: str) -> dict[str, object]:
    from maya import cmds

    shape = mesh_shape(transform)
    bounds = cmds.exactWorldBoundingBox(transform)
    return {
        "name": transform,
        "vertices": int(cmds.polyEvaluate(shape, vertex=True) or 0),
        "triangles": int(cmds.polyEvaluate(shape, triangle=True) or 0),
        "uvs": int(cmds.polyEvaluate(shape, uv=True) or 0),
        "non_manifold": non_manifold_components(shape),
        "bounds_cm": {
            "width": round(bounds[3] - bounds[0], 5),
            "height": round(bounds[4] - bounds[1], 5),
            "depth": round(bounds[5] - bounds[2], 5),
            "minimum": [round(value, 5) for value in bounds[:3]],
            "maximum": [round(value, 5) for value in bounds[3:]],
        },
    }


def main() -> None:
    for directory in (SOURCE_DIR, EXPORT_DIR, DOCUMENTATION_DIR):
        directory.mkdir(parents=True, exist_ok=True)
    required = [HIGH_FBX, DONOR_FBX, *TEXTURE_PATHS.values()]
    missing = [str(path) for path in required if not path.is_file()]
    if missing:
        raise FileNotFoundError("Arquivos ausentes: " + " | ".join(missing))

    maya.standalone.initialize(name="python")
    from maya import cmds, mel

    cmds.loadPlugin("fbxmaya", quiet=True)
    cmds.file(new=True, force=True)
    cmds.currentUnit(linear="cm")

    high_transform, high_shape = import_single_mesh(HIGH_FBX, "Meshy_High_Source")
    high_bounds = cmds.exactWorldBoundingBox(high_transform)
    high_height = high_bounds[4] - high_bounds[1]
    high_triangles = int(cmds.polyEvaluate(high_shape, triangle=True) or 0)
    if not 12000 <= high_triangles <= 18000:
        raise RuntimeError(f"Triângulos fora da faixa aprovada: {high_triangles}")
    if abs(high_height - 46.5) > 0.5:
        raise RuntimeError(f"Altura fora da tolerância: {high_height:.5f} cm")

    non_manifold_before = non_manifold_components(high_shape)
    cmds.select(high_transform, replace=True)
    mel.eval(
        'polyCleanupArgList 4 {"0","1","0","0","0","0","0","0",'
        '"0","1e-005","0","1e-005","0","1e-005","0","1","0","0"};'
    )
    cmds.delete(high_transform, constructionHistory=True)
    high_shape = mesh_shape(high_transform)
    non_manifold_after = non_manifold_components(high_shape)
    if non_manifold_after:
        raise RuntimeError("A limpeza não removeu todos os componentes non-manifold")

    donor_transform, donor_shape = import_single_mesh(DONOR_FBX, "Meshy_TextureDonor")
    texture_report = remap_texture_nodes()
    donor_shading_groups = cmds.listConnections(donor_shape, type="shadingEngine") or []
    donor_shading_group = next(
        (group for group in donor_shading_groups if group != "initialShadingGroup"),
        donor_shading_groups[0] if donor_shading_groups else None,
    )
    if donor_shading_group is None:
        raise RuntimeError("Material doador não encontrado")

    donor_bounds = cmds.exactWorldBoundingBox(donor_transform)
    high_size = bounds_size(high_bounds)
    donor_size = bounds_size(donor_bounds)
    alignment_scale = [high_size[index] / donor_size[index] for index in range(3)]
    high_center = bounds_center(high_bounds)
    donor_center = bounds_center(donor_bounds)
    donor_group = cmds.group(donor_transform, name="Meshy_TextureDonor_ALIGNMENT")
    donor_transform = (cmds.listRelatives(donor_group, children=True, type="transform", fullPath=True) or [None])[0]
    donor_shape = mesh_shape(donor_transform)
    cmds.xform(donor_group, pivots=donor_center, worldSpace=True)
    cmds.setAttr(donor_group + ".scale", *alignment_scale, type="double3")
    scaled_center = bounds_center(cmds.exactWorldBoundingBox(donor_group))
    alignment_translation = [high_center[index] - scaled_center[index] for index in range(3)]
    cmds.move(*alignment_translation, donor_group, relative=True, worldSpace=True)
    cmds.makeIdentity(donor_group, apply=True, translate=True, rotate=True, scale=True, normal=False)

    cmds.transferAttributes(
        donor_transform,
        high_transform,
        transferPositions=0,
        transferNormals=0,
        transferUVs=2,
        transferColors=0,
        sampleSpace=0,
        searchMethod=3,
        flipUVs=0,
        colorBorders=1,
    )
    cmds.delete(high_transform, constructionHistory=True)
    high_shape = mesh_shape(high_transform)
    high_uv_sets = cmds.polyUVSet(high_shape, query=True, allUVSets=True) or []
    if "UVMap" in high_uv_sets:
        cmds.polyUVSet(high_shape, currentUVSet=True, uvSet="UVMap")
    transferred_uvs = int(cmds.polyEvaluate(high_shape, uv=True) or 0)
    if transferred_uvs <= 0:
        raise RuntimeError("Transferência de UV não produziu coordenadas")

    separated = cmds.polySeparate(high_transform, constructionHistory=False) or []
    part_transforms = []
    for item in separated:
        if cmds.objExists(item) and cmds.nodeType(item) == "transform":
            if cmds.listRelatives(item, shapes=True, noIntermediate=True):
                part_transforms.append(item)
    part_transforms = sorted(set(part_transforms))
    for transform in part_transforms:
        shape = mesh_shape(transform)
        uv_sets = cmds.polyUVSet(shape, query=True, allUVSets=True) or []
        if "UVMap" in uv_sets:
            cmds.polyUVSet(shape, currentUVSet=True, uvSet="UVMap")
    roles = classify_parts(part_transforms)
    renamed = {
        "lower": unite_and_name(roles["lower"], "Meshy_UretersAndBladder"),
        "left": unite_and_name(roles["left"], "Meshy_LeftKidney_REFERENCE"),
        "right": unite_and_name(roles["right"], "Meshy_RightKidney"),
    }
    for transform in renamed.values():
        cmds.polySoftEdge(transform, angle=180, constructionHistory=False)
        cmds.sets(transform, edit=True, forceElement=donor_shading_group)
        cmds.delete(transform, constructionHistory=True)
        shape = mesh_shape(transform)
        uv_sets = cmds.polyUVSet(shape, query=True, allUVSets=True) or []
        if "UVMap" in uv_sets:
            cmds.polyUVSet(shape, currentUVSet=True, uvSet="UVMap")

    export_root = cmds.group(renamed["lower"], renamed["right"], name="MeshyUrinaryVisual_Export")
    reference_root = cmds.group(renamed["left"], donor_group, name="REFERENCE_ONLY")
    cmds.setAttr(reference_root + ".visibility", 0)
    scene_root = cmds.group(export_root, reference_root, name="MeshyUrinarySystem_v002")

    cmds.file(rename=str(SCENE_PATH))
    cmds.file(save=True, type="mayaAscii", force=True)

    cmds.select(export_root, hierarchy=True, replace=True)
    mel.eval("FBXResetExport;")
    mel.eval("FBXExportSmoothingGroups -v true;")
    mel.eval("FBXExportTangents -v true;")
    mel.eval("FBXExportSmoothMesh -v false;")
    mel.eval("FBXExportInstances -v false;")
    mel.eval("FBXExportInputConnections -v true;")
    mel.eval(f'FBXExport -f "{str(EXPORT_FBX).replace(os.sep, "/")}" -s;')

    exported_parts = [renamed["lower"], renamed["right"]]
    part_report = [describe_transform(transform) for transform in exported_parts]
    export_bounds = cmds.exactWorldBoundingBox(exported_parts)
    export_triangles = sum(int(item["triangles"]) for item in part_report)
    export_uvs = sum(int(item["uvs"]) for item in part_report)
    export_non_manifold = [component for item in part_report for component in item["non_manifold"]]

    report = {
        "milestone": "Marco 3.1",
        "maya_version": cmds.about(version=True),
        "linear_unit": cmds.currentUnit(query=True, linear=True),
        "source_high_fbx": str(HIGH_FBX),
        "source_high_sha256": sha256(HIGH_FBX),
        "texture_donor_fbx": str(DONOR_FBX),
        "texture_donor_sha256": sha256(DONOR_FBX),
        "maya_scene": str(SCENE_PATH),
        "unity_visual_fbx": str(EXPORT_FBX),
        "unity_visual_fbx_sha256": sha256(EXPORT_FBX),
        "source_height_cm": round(high_height, 5),
        "source_triangles": high_triangles,
        "source_uvs_after_transfer": transferred_uvs,
        "non_manifold_before": non_manifold_before,
        "non_manifold_after": non_manifold_after,
        "alignment_scale": alignment_scale,
        "alignment_translation": alignment_translation,
        "export_height_cm": round(export_bounds[4] - export_bounds[1], 5),
        "export_triangles": export_triangles,
        "export_uvs": export_uvs,
        "export_non_manifold": export_non_manifold,
        "excluded_from_export": [renamed["left"], donor_transform],
        "parts": part_report,
        "textures": texture_report,
        "passed": (
            12000 <= high_triangles <= 18000
            and abs(high_height - 46.5) <= 0.5
            and transferred_uvs > 0
            and export_uvs > 0
            and not non_manifold_after
            and not export_non_manifold
            and EXPORT_FBX.is_file()
        ),
    }
    REPORT_PATH.write_text(json.dumps(report, indent=2, ensure_ascii=False), encoding="utf-8")
    print(json.dumps(report, indent=2, ensure_ascii=False))
    if not report["passed"]:
        raise RuntimeError("A preparação Meshy v002 não passou na validação")
    maya.standalone.uninitialize()


if __name__ == "__main__":
    main()
