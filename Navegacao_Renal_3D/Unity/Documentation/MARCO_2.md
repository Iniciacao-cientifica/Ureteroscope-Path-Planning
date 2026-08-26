# Navegacao Renal 3D — Marco 2

## Resultado deste marco

Este projeto Unity 6 foi criado do zero em URP e usa o `Kidney_Game_v002.fbx` validado no Marco 1.

A cena principal contém:

- um rim ativo com sistema coletor interno navegavel, colisao interna, rota, pedra e ancoras;
- um segundo rim para compor o sistema urinario;
- dois ureteres e uma bexiga como base visual procedural;
- modo Realista, no qual a ponta fica limitada pelas paredes internas;
- modo Exploracao, com camera livre dentro e fora dos rins;
- interface provisoria em portugues, contador de toques e nivel facil;
- minimapa provisório;
- iluminacao URP e luz na ponta do endoscopio.

O segundo rim, os ureteres longos e a bexiga são a base visual do Marco 2. Eles ainda não são modelos anatômicos finais nem estão destinados à navegação interna. O rim ativo v002 permanece a geometria autoritativa para a jogabilidade.

## Como abrir

No Unity Hub, escolha **Add project from disk** e selecione:

`C:\Users\pedro\OneDrive\Área de Trabalho\Navegacao_Renal_3D_Unity`

Editor validado: **Unity 6000.5.0f1**.

Abra `Assets/Scenes/MainMenu.unity` para começar pelo menu ou `Assets/Scenes/KidneyGame.unity` para abrir diretamente a simulação.

## Controles atuais

### Modo Realista

- Mouse: orientar a ponta.
- `W` / `S`: avançar e recuar.
- `Q` / `E`: rolar a ponta.
- `Espaço`: tentar capturar a pedra quando estiver próximo.
- `R`: reiniciar tentativa.
- `P`: pausar.
- `T`: mostrar ou ocultar a rota ciano.
- `M`: mostrar ou ocultar o minimapa.
- `F2`: mudar para Exploração.

No nível fácil, cinco contatos com a parede encerram a tentativa. A colisão bloqueia a passagem pela parede sem tela preta e sem revelar a parte externa.

### Modo Exploração

- Segurar botão direito do mouse: olhar livremente.
- `WASD`: mover.
- `Q` / `E`: descer e subir.
- `Shift`: acelerar.
- `F1`: voltar ao modo Realista.

## Escala e modelo

- O FBX é importado em escala física: centímetros do Maya convertidos em metros no Unity.
- A altura importada do rim é `0,150614 m`.
- `GameplayScaleRoot` aplica `5x` apenas para a apresentação/jogabilidade.
- SHA-256 do FBX v002: `174fabbf6ec31b3052360be995b5bbc4fb7e074b91ef2a2bba838ca45cc0fa9c`.

## Validação

O relatório automático fica em `Documentation/marco2_validation.json`. As imagens de inspeção ficam em `Documentation/Previews`.

O gerador do marco pode ser executado novamente pelo menu do Unity:

`Navegacao Renal > Construir Marco 2`

Essa operação recria de forma determinística os materiais, o prefab, as malhas procedurais, as cenas e o relatório.
