# Navegação Renal 3D — Marco 3.1

## Resultado

O novo sistema urinário Meshy substitui somente o visual externo do rim
direito, dos dois ureteres e da bexiga. O rim ativo esquerdo, o interior
`Kidney_Game_v002`, o `MeshCollider`, a rota, a pedra, as âncoras e o
controlador `SphereCast` continuam sendo a base navegável do Marco 3.

## Arquivos principais

- cena Unity: `Assets/Scenes/KidneyGame.unity`;
- modelo visual Unity: `Assets/Art/UrinarySystem/Models/Meshy_Urinary_Visual_v002.fbx`;
- material URP: `Assets/Materials/MAT_MeshyUrinary_URP.mat`;
- cena Maya editável: `Maya/Candidates/Meshy_Urinary_System_v002/Source/Meshy_Urinary_System_v002.ma`;
- exportação equivalente Maya: `Maya/Candidates/Meshy_Urinary_System_v002/Exports/Meshy_Urinary_Visual_v002.fbx`;
- relatório Unity: `Documentation/marco31_validation.json`.

Na pasta externa do Maya, abra:

`C:\Users\pedro\OneDrive\Área de Trabalho\Navegacao_Renal_3D_Maya_v01\Candidates\Meshy_Urinary_System_v002\Source\Meshy_Urinary_System_v002.ma`

No Unity, abra o projeto:

`C:\Users\pedro\OneDrive\Área de Trabalho\Navegacao_Renal_3D_Unity`

e depois a cena `Assets/Scenes/KidneyGame.unity`.

## Geometria e materiais

- fonte Meshy: `15.979` triângulos, altura física `46,5 cm`;
- visual destinado ao Unity: `11.026` triângulos e `7.931` UVs;
- malhas: `Meshy_RightKidney` e `Meshy_UretersAndBladder`;
- escala visual Unity: `5×`, produzindo aproximadamente `2,325 m` na cena;
- mapas preservados: Base Color, Normal, Metallic e Roughness;
- Mask Map: Metallic em R, AO neutro em G e `1 - Roughness` em A;
- camada exclusiva: `KidneyExterior`, invisível para a câmera interna;
- o visual Meshy não possui collider.

O pipeline considera a inversão do eixo X entre Maya e Unity para exportar o
rim anatômico correto. O rim esquerdo Meshy permanece oculto como referência e
é excluído do FBX final.

## Validação

O Unity `6000.5.0f1` concluiu `58` verificações sem erro. Foram confirmados:

- hash inalterado do `Kidney_Game_v002`;
- hash idêntico do visual Maya/Unity;
- altura, triângulos, UVs, nomes e quatro mapas PBR;
- contato visual dos dois ureteres com os respectivos rins;
- ausência das malhas procedurais antigas na cena;
- ausência de colliders no novo visual;
- todos os testes do controlador do Marco 3, inclusive 30/60/120 FPS;
- três capturas automáticas externa e internas.

SHA-256 do visual:

`f8408f41656011f65cb737b1b434e7611228036b76fb8fcadfaa183dcfa26ed2`

O Marco 4 não foi iniciado automaticamente.
