# Estado canônico — Navegação Renal 3D

Última base aprovada: Marco 6 na branch de integração `DoZero`.

## Entrega validada

- projeto ativo: `Navegacao_Renal_3D/Unity`;
- Unity: `6000.5.0f1`;
- cena inicial: `Assets/Scenes/MainMenu.unity`;
- validação: `194` verificações aprovadas (`133` herdadas + `61` do Marco 6);
- firmware: ESP32 DevKit V1, MPU6050, botão no GPIO 25 e JSON v2;
- validação física: simulação/replay aprovada; montagem e teste elétrico reais
  ainda pendentes.

## Marcos

- Marcos 1–3.2: modelo renal, visual Meshy, junção v003 e navegação;
- Marco 4: gameplay desktop, colisões, estados e captura;
- Marco 5: exploração livre, visibilidade e minimapa;
- Marco 6: ESP32/MPU, reconexão serial e build Windows;
- Marco 5.1: próxima revisão, com anatomia v004 aprovada no Maya antes de
  substituir qualquer geometria no Unity.

## Artefatos protegidos

- FBX renal v003: `f721e63ad9188f007520709f24cb7c60e85ce3a2588bec6b533e7460fddc9bcf`;
- FBX renal v002: `174fabbf6ec31b3052360be995b5bbc4fb7e074b91ef2a2bba838ca45cc0fa9c`;
- FBX visual Meshy: `f8408f41656011f65cb737b1b434e7611228036b76fb8fcadfaa183dcfa26ed2`.

As cópias Maya e Unity desses artefatos não podem divergir. Uma nova versão
anatômica deve usar outro nome e ser aprovada antes de substituir a versão
ativa.

## Restrições

- não reutilizar `UnityVRPrototype` como base do jogo ativo;
- não alterar o pipeline científico antigo sem solicitação explícita;
- não executar A* durante o jogo;
- não usar NavMesh ou controlador FPS no modo Realista;
- não adicionar múltiplos pacientes/casos à primeira versão;
- não declarar validação física sem teste no hardware real;
- Quest, encoder, servo e sensor físico permanecem fora do escopo atual.

Atualize este arquivo, o README ativo, `MARCO_6.md` e o relatório canônico na
mesma entrega sempre que um novo marco for aprovado.
