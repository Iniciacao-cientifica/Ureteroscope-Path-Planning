# Navegação Renal 3D — Marco 6

Projeto Unity 6 URP de navegação ureteroscópica com um rim navegável, sistema
urinário completo para visualização e modos Realista e Exploração.

## Unity

Abra pelo Unity Hub:

`Navegacao_Renal_3D/Unity`

Use Unity `6000.5.0f1` e abra `Assets/Scenes/MainMenu.unity`.

- Realista: nível fácil, cinco contatos, cronômetro, rota, minimapa, garra
  virtual e captura sustentada da pedra;
- Exploração: câmera livre dentro e fora das malhas, exterior em três estados
  e visibilidade independente de sistema coletor, rota e pedra;
- minimapa final inclinado com rim ativo, rota e indicador de posição;
- controlador interno por `SphereCast`, sem `CharacterController`;
- entrada Realista selecionável entre mouse/teclado e ESP32 DevKit V1 com
  MPU6050 e botão físico;
- detecção automática/lista COM, calibração por `C` e pausa segura na
  desconexão;
- firmware JSON v2 compilado para `esp32dev`;
- relatório aprovado com `173` verificações em
  `Unity/Documentation/marco6_validation.json`.

Instruções completas: `Unity/Documentation/MARCO_6.md`.

## Maya e geometria

A montagem editável completa permanece em:

`Maya/Candidates/Kidney_Game_v003/Source/Urinary_System_Assembly_v003.ma`

O FBX renal usado pelo Unity permanece em:

`Unity/Assets/Art/Kidney/Models/Kidney_Game_v003.fbx`

O Marco 5 não altera a geometria v003, sua cópia Maya nem o visual Meshy.

## Estado dos marcos

- Marcos 1–3.2: modelo, Unity, navegação, Meshy e junção v003 concluídos;
- Marco 4: gameplay desktop concluído;
- Marco 5: exploração livre e minimapa final concluídos;
- Marco 6: controle ESP32/MPU, firmware e build Windows concluídos;
- Marco 5.1: próxima revisão, reconstrução do rim ativo sobre a base Meshy.

O histórico de requisitos e decisões está em
`Contexto/Identificar navegação em Python.md`.
