# Navegação Renal 3D — modelo v001

## Arquivo correto para abrir

Abra diretamente no Maya:

`Source/Kidney_Master.ma`

Esse é o arquivo-mestre editável, em centímetros e na escala física real. O
aumento visual de 5× será aplicado somente no Unity.

## Conteúdo da cena

- `KidneyExterior`
- `CollectingSystemVisual`
- `CollectingSystemCollision_Inward`
- `RouteGuide`
- `Stone`
- `StartAnchor`
- `TargetAnchor`
- `MinimapAnchor`

A malha de colisão fica oculta na display layer `COLLISION_DEBUG`. Ative essa
layer para inspecioná-la. A rota possui sua própria layer.

## Exportação validada

O FBX autoritativo está em `Exports/Kidney_Game_v001.fbx`. A pasta
`Exports/OBJ_Fallback` contém somente as alternativas OBJ/MTL.

Os diretórios antigos `Maya_RealScale_CM` e `Maya_GameScale_x5_CM` pertencem ao
blockout 0.1 e não devem mais ser usados como modelo final.

Consulte `Documentation/validation_report.json` e
`Documentation/maya_roundtrip_report.json` para os resultados técnicos.

Este é um modelo anatômico orientado ao jogo, não uma reconstrução clínica de
um paciente. O lado anatômico permanece “lado a confirmar”.
