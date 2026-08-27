# Navegação Renal 3D — Marco 4

Projeto Unity 6 URP de navegação ureteroscópica com um rim navegável,
sistema urinário completo para visualização e dois modos separados.

## Unity

Abra pelo Unity Hub:

`Navegacao_Renal_3D/Unity`

Use Unity `6000.5.0f1` e abra `Assets/Scenes/MainMenu.unity`.

- Realista: partida no nível fácil, cinco contatos, pausa, cronômetro,
  rota, minimapa, garra virtual e captura sustentada da pedra;
- Exploração: câmera livre dentro e fora do sistema, sem pontuação;
- controlador interno por `SphereCast`, sem `CharacterController`;
- entrada atual por mouse/teclado abstraída para a futura integração ESP32;
- relatório aprovado com `103` verificações em
  `Unity/Documentation/marco4_validation.json`.

Instruções completas: `Unity/Documentation/MARCO_4.md`.

## Maya e geometria

A montagem editável completa atual é:

`Maya/Candidates/Kidney_Game_v003/Source/Urinary_System_Assembly_v003.ma`

O FBX renal usado pelo Unity é:

`Unity/Assets/Art/Kidney/Models/Kidney_Game_v003.fbx`

O Marco 4 não altera a geometria `v003`, a cópia Maya ou o visual Meshy.

## Estado dos marcos

- Marcos 1–3.2: modelo, Unity, navegação, Meshy e junção v003 concluídos;
- Marco 4: gameplay desktop concluído;
- Marco 5: exploração e minimapa finais;
- Marco 6: polimento, build e integração futura com ESP32, MPU, encoder e
  garra física.

O histórico de requisitos e decisões fica em
`Contexto/Identificar navegação em Python.md`.
