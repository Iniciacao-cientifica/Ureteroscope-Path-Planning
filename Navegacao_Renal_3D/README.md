# Navegação Renal 3D — Marco 1

Snapshot autocontido da primeira etapa do desenvolvimento do zero.

## Maya

Abra `Maya/Source/Kidney_Master.ma`. O arquivo está em centímetros e na escala
física real. Ele contém exterior renal, sistema coletor, collider interno,
rota, pedra, materiais, UVs básicos e âncoras.

O FBX autoritativo está em `Maya/Exports/Kidney_Game_v001.fbx`. Os relatórios
de topologia, escala e reimportação ficam em `Maya/Documentation`.

## Unity

`Unity/Assets/Art/Kidney/Models/Kidney_Game_v001.fbx` é uma cópia byte a byte
do FBX do Maya. O projeto Unity e o gameplay ainda não fazem parte deste marco.

## Pipeline

Os scripts reproduzíveis estão em `Tools`. O gerador cria a geometria, executa
o Maya 2027 em modo standalone, exporta o FBX, calcula o manifesto e sincroniza
a cópia destinada ao Unity.

Validação da versão registrada:

- escala física aprovada;
- malhas fechadas e manifold;
- rota dentro do sistema coletor;
- folga mínima da rota de 3,818 mm;
- reimportação Maya/FBX sem alteração de dimensões ou triângulos;
- SHA-256 idêntico nas duas cópias do FBX.

Este é um modelo anatômico orientado ao jogo, não uma reconstrução clínica de
paciente. A lateralidade permanece “lado a confirmar”.
