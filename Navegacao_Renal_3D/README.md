# Navegação Renal 3D — Marco 3

Projeto desenvolvido do zero com modelo renal `v002` e protótipo navegável no Unity.

## Maya

Abra `Maya/Source/Kidney_Master.ma`. O arquivo está em centímetros e na escala
física real. Ele contém exterior renal, sistema coletor, collider interno,
rota, pedra, materiais, UVs básicos e âncoras.

O FBX autoritativo atual está em `Maya/Exports/Kidney_Game_v002.fbx`. Os relatórios
de topologia, escala e reimportação ficam em `Maya/Documentation`.

## Unity

O projeto Unity 6 URP está completo dentro de `Unity`. Abra a pasta pelo Unity
Hub e execute `Assets/Scenes/MainMenu.unity` ou `Assets/Scenes/KidneyGame.unity`.

O projeto inclui a fundação do Marco 2 e a navegação realista do Marco 3:

- modo Realista com mouse, movimento `W/S`, rota opcional e colisão interna por
  `SphereCast`, sem `CharacterController`;
- modo Exploração com câmera livre dentro e fora dos rins;
- nível fácil, contador de cinco contatos e captura da pedra com Espaço;
- dois rins, dois ureteres e bexiga na composição global;
- acabamento vermelho orgânico no rim direito, nos ureteres e na bexiga, sem
  alterar o visual já aprovado do rim ativo superior esquerdo;
- textura interna original com base color e normal map, minimapa provisório,
  materiais URP e iluminação da ponta;
- prefab `KidneyLevel` com escala física preservada e raiz visual `5x`.

O rim ativo usa `Unity/Assets/Art/Kidney/Models/Kidney_Game_v002.fbx`, cópia
byte a byte do FBX do Maya. O segundo rim, os ureteres longos e a bexiga formam
a composição visual externa; somente o rim ativo é navegável nesta etapa.

Instruções, controles e validação atual estão em
`Unity/Documentation/MARCO_3.md`. O Marco 2 permanece documentado em
`Unity/Documentation/MARCO_2.md`.

## Pipeline

Os scripts reproduzíveis estão em `Tools`. O gerador cria a geometria, executa
o Maya 2027 em modo standalone, exporta o FBX, calcula o manifesto e sincroniza
a cópia destinada ao Unity.

Validação da versão registrada:

- escala física aprovada;
- malhas fechadas e manifold;
- rota dentro do sistema coletor;
- folga mínima da rota de 3,711 mm;
- reimportação Maya/FBX sem alteração de dimensões ou triângulos;
- SHA-256 idêntico nas duas cópias do FBX.

Este é um modelo anatômico orientado ao jogo, não uma reconstrução clínica de
paciente. A lateralidade permanece “lado a confirmar”.

O histórico consolidado de requisitos, todas as perguntas e decisões está em
`Contexto/Identificar navegação em Python.md`.
