# Navegação Renal 3D — Marco 3.2

## Resultado

O encaixe do rim ativo com o ureter esquerdo Meshy foi reconstruído na
geometria `Kidney_Game_v003`. O deslocamento provisório de `0,075 m` foi
removido: o rim voltou à posição equilibrada original e a conexão agora é
feita por uma transição curta, curva e navegável entre a pelve e o topo do
ureter.

O ureter completo Meshy continua apenas visual. A navegação começa no pequeno
trecho proximal criado na v003 e segue pelo mesmo sistema coletor, pela mesma
rota e até a mesma pedra.

## Arquivos para abrir

No Unity, abra o projeto
`C:\Users\pedro\OneDrive\Área de Trabalho\Navegacao_Renal_3D_Unity` e a cena
`Assets/Scenes/KidneyGame.unity`.

No Maya, a cena mais completa para inspeção é:

`C:\Users\pedro\OneDrive\Área de Trabalho\Navegacao_Renal_3D_Maya_v01\Candidates\Kidney_Game_v003\Source\Urinary_System_Assembly_v003.ma`

Para editar apenas o rim e sua navegação, use:

`C:\Users\pedro\OneDrive\Área de Trabalho\Navegacao_Renal_3D_Maya_v01\Candidates\Kidney_Game_v003\Source\Kidney_Master_v003.ma`

## Validação

- Unity `6000.5.0f1` compilado sem scripts ausentes;
- `65` verificações automáticas aprovadas;
- centro da gola e centro do topo do ureter separados por apenas `0,000041 m`
  no mundo Unity;
- `SphereCast`, contato único, rearme, reset, pausa, modos e equivalência em
  30/60/120 FPS preservados;
- câmera interna continua sem enxergar `KidneyExterior`;
- a gola é somente visual e não adiciona collider;
- a v002 foi preservada byte a byte.

SHA-256 da v003:

`f721e63ad9188f007520709f24cb7c60e85ce3a2588bec6b533e7460fddc9bcf`

SHA-256 preservado da v002:

`174fabbf6ec31b3052360be995b5bbc4fb7e074b91ef2a2bba838ca45cc0fa9c`

O Marco 4 não foi iniciado automaticamente.
