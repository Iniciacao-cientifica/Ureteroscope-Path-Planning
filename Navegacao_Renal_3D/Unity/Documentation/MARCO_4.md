# Navegação Renal 3D — Marco 4

## Resultado

O protótipo agora possui uma partida completa no nível fácil. O rim
navegável `v003`, o caminho, a pedra e toda a colisão por `SphereCast` foram
preservados.

Estados da tentativa:

- `Ready`: mostra objetivo e controles, sem movimento ou cronômetro;
- `Playing`: libera a navegação realista e a captura;
- `Paused`: congela movimento, captura e tempo;
- `Won`: pedra presa à âncora da garra;
- `Lost`: quinto contato com a parede.

O menu agora separa `Modo Realista` de `Modo Exploração`. Não é possível
usar F1/F2 para escapar de uma tentativa. A Exploração não conta contatos,
tempo, captura ou resultado.

## Como jogar

Abra `Assets/Scenes/MainMenu.unity` e pressione Play.

No modo Realista:

- clique dentro da janela para prender o cursor; `Esc` libera;
- mouse orienta a ponta;
- `W/S` ou setas avançam e recuam;
- `Q/E` aplicam rotação axial;
- `T` alterna a rota, ligada inicialmente;
- `M` alterna o minimapa, ligado inicialmente;
- `P` pausa;
- `R` reinicia e retorna à tela de preparação;
- próximo da pedra, segure `Espaço` continuamente por `1 segundo`.

A garra virtual fecha conforme a barra progride. Soltar Espaço ou sair dos
`0,10 m` permitidos cancela a captura e reabre a garra. Ao concluir, a pedra
acompanha a âncora da garra e a tentativa termina em vitória.

Cada contato contínuo com a parede conta uma vez. O contato mostra flash
vermelho, atualiza `0/5` e toca um alerta curto. O quinto contato termina em
derrota.

## Arquitetura preparada para o Marco 6

`MouseKeyboardInputSource` implementa `IEndoscopeInputSource` e entrega um
`EndoscopeInputFrame` compartilhado pelo controlador e pelo gameplay. A futura
entrada serial do ESP32 poderá implementar a mesma interface sem substituir a
física aprovada.

Este marco não contém comunicação serial, MPU, encoder, servo ou garra
física.

## Validação

O comando `Navegacao Renal > Construir Marco 4` reconstrói as cenas, executa
os validadores anteriores e valida o gameplay novo.

- Unity `6000.5.0f1` compilado sem erros de C# ou scripts ausentes;
- `65` verificações anteriores reexecutadas;
- `38` verificações específicas do Marco 4;
- `103` verificações aprovadas no total;
- hashes dos FBX `v002`, `v003` e Meshy preservados.

O relatório fica em `Documentation/marco4_validation.json`. As seis imagens
de aceite ficam em `Documentation/Previews/marco4_*.png`.
