# Navegação Renal 3D — modelo v002

## Arquivo correto para abrir

Abra diretamente no Maya:

`Source/Kidney_Master.ma`

Esse é o arquivo-mestre editável, em centímetros e na escala física real. O
aumento visual de 5× será aplicado somente no Unity.

O rim abre **sólido e opaco** para deixar claro que a superfície externa está
completa. Para enxergar o sistema coletor interno, selecione `KidneyExterior`
e ative `Shading > X-Ray Selected` no viewport, ou oculte temporariamente a
display layer `EXTERIOR_SOLID`.

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
layer para inspecioná-la. A rota, o exterior e o sistema coletor possuem suas
próprias layers.

## Exportação validada

O FBX autoritativo está em `Exports/Kidney_Game_v002.fbx`. A pasta
`Exports/OBJ_Fallback` contém somente as alternativas OBJ/MTL.

O sistema interno contém pelve afunilada, três grupos principais e nove
cálices menores com terminações côncavas. Trata-se do espaço navegável do jogo,
não de uma representação da microanatomia renal.

Os diretórios antigos `Maya_RealScale_CM` e `Maya_GameScale_x5_CM` pertencem ao
blockout 0.1 e não devem mais ser usados como modelo final.

Consulte `Documentation/validation_report.json` e
`Documentation/maya_roundtrip_report.json` para os resultados técnicos.

O mestre anterior foi preservado em `Archive/Kidney_Master_v001.ma`.

Este é um modelo anatômico orientado ao jogo, não uma reconstrução clínica nem
um dispositivo validado para treinamento médico. O lado anatômico permanece
“lado a confirmar”.
