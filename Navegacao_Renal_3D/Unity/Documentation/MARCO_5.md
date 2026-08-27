# Navegação Renal 3D — Marco 5

## Resultado

O modo Exploração e o minimapa foram finalizados sem alterar a geometria
renal, a colisão `SphereCast` ou o gameplay do Marco 4.

A câmera livre atravessa todas as malhas e inicia enquadrando o sistema
urinário completo. Um clique prende o cursor; `Esc` o libera e interrompe o
movimento. `F` retorna suavemente à visão geral em `0,45 s`.

## Controles da Exploração

- clique e mouse: prender o cursor e olhar;
- `WASD`: mover;
- `Q/E`: descer e subir;
- `Shift`: acelerar em `3×`;
- `F`: retornar à visão geral;
- `1`: exterior transparente, opaco ou oculto;
- `2`: mostrar ou ocultar o sistema coletor;
- `3` ou `T`: mostrar ou ocultar a rota;
- `4`: mostrar ou ocultar a pedra;
- `H`: recolher ou abrir o painel;
- `M`: mostrar ou ocultar o minimapa.

O exterior inicia semitransparente, com opacidade aproximada de `32%` e
renderização das duas faces. O estado opaco usa variantes próprias dos
materiais, sem modificar os materiais aprovados dos modelos.

## Minimapa

O minimapa usa uma `RenderTexture` de `512×512` e uma representação isolada na
camada `MinimapOnly`. Ele mantém uma vista 3D fixa e inclinada do rim ativo,
independente dos controles visuais da câmera principal.

A seta segue a ponta no modo Realista e a câmera no modo Exploração. Fora do
enquadramento, ela permanece na borda, aponta para o rim e mostra a distância.
A rota ciano do minimapa acompanha o estado de `T`.

## Validação

O comando `Navegacao Renal > Construir Marco 5` aplica o marco sobre a base do
Marco 4. O comando `Navegacao Renal > Validar Marco 5` executa somente a
validação atual.

- Unity `6000.5.0f1` compilado sem erros de C# ou scripts ausentes;
- `103` verificações anteriores reexecutadas;
- `30` verificações específicas do Marco 5;
- `133` verificações aprovadas no total;
- equivalência de movimento confirmada em 30, 60 e 120 FPS;
- hashes dos FBX v002, v003 e Meshy preservados.

O relatório está em `Documentation/marco5_validation.json`. As cinco imagens
de aceite estão em `Documentation/Previews/marco5_*.png`.

## Limites mantidos

Somente o rim ativo possui sistema coletor detalhado. O segundo rim permanece
visual. Não há ESP32, MPU, encoder, garra física, build Windows final ou
integração Quest neste marco.
