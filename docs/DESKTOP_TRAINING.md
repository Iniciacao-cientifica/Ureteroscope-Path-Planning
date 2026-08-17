# Treinador desktop com vareta física

Protótipo acadêmico/educacional para navegação em uma máscara anatômica revisada. Não é um dispositivo médico e não deve ser usado em pacientes, procedimentos clínicos, diagnóstico, navegação intraoperatória ou controle de equipamentos.

## O que já funciona

- Visão endoscópica na ponta virtual e minimapa externo.
- Casos, pedras e rotas do pipeline v2 já existente.
- Tutorial com rota interna, intermediário com rota somente no minimapa e avançado sem rota.
- Controle por teclado/mouse ou ESP32-S3 via USB.
- Colisões, tempo, desvio RMS, eficiência, pontuação e resultado CSV anônimo.
- Pausa após 500 ms sem pacotes do controle.

## Executar o build pronto no Windows

1. Abra `UnityVRPrototype/Builds/Desktop/UreteroscopyTraining.exe`.
2. Não mova somente o arquivo `.exe`: mantenha-o ao lado da pasta `UreteroscopyTraining_Data`.
3. Na tela inicial escolha `Teclado`, dificuldade `Tutorial` e um código anônimo, por exemplo `TESTE-001`.
4. Inicie a sessão e confirme que aparecem a visão interna, o minimapa, a rota, a pedra e as métricas.

No modo teclado:

- `W/S`: avanço e recuo;
- mouse ou setas: inclinação e direção;
- `Q/E`: rotação axial;
- `Espaço`: botão de ação;
- `C`: calibração/recentralização.

Se o Windows SmartScreen bloquear o executável compilado localmente, selecione `Mais informações > Executar assim mesmo`. Não faça isso com executáveis recebidos de origem desconhecida.

## Abrir e testar no Unity

O projeto usa Unity `6000.5.0f1`. No Unity Hub escolha `Add project from disk` e selecione a pasta `UnityVRPrototype`, não a raiz do repositório.

Abra `Assets/Scenes/UreteroscopyDesktopTraining.unity` e pressione Play. A cena já está versionada. Use `Murillo VR > Setup Desktop Training Scene` apenas se ela estiver ausente ou precisar ser reconstruída.

Pare o Play Mode antes de editar a cena. Alterações feitas pelo Inspector enquanto o jogo está rodando normalmente são descartadas ao pressionar Stop.

### Ajustes manuais seguros

Selecione `Desktop Training Controller` na Hierarchy para ajustar:

- `Tip Radius Meters`: raio físico da ponta virtual;
- `Millimeters Per Encoder Tick`: escala de avanço do encoder;
- `Rotation Smoothing`: suavização da orientação;
- tolerâncias de distância, ângulo e permanência no alvo;
- modo de entrada e porta serial padrão.

Selecione `Training Case Loader` para ajustar:

- cores e transparência da anatomia;
- cores da pedra, rota, início e alvo;
- espessura da rota e tamanho dos marcadores;
- duração da animação que percorre a rota.

O campo de visão da câmera, o material interno e o HUD são configurados por `Assets/Scripts/UreteroscopyTrainingController.cs`. Esses itens exigem alteração de código, recompilação e um novo teste.

Salve a cena com `Ctrl+S`. Para gerar um novo executável use `Murillo VR > Build Desktop Training (Windows)`. O resultado fica em `UnityVRPrototype/Builds/Desktop/UreteroscopyTraining.exe`.

## Usar a vareta física

1. Grave o firmware em `hardware/firmware/ureteroscope_controller` no ESP32-S3.
2. Conecte o ESP32 ao computador por USB e mantenha a vareta imóvel durante os dois segundos de calibração do giroscópio.
3. No jogo escolha `Vareta USB` e deixe a porta como `AUTO`.
4. Se a conexão automática falhar, consulte a porta no Gerenciador de Dispositivos do Windows e informe `COM3`, `COM4` ou a porta encontrada.
5. Use a calibração guiada e desloque exatamente 100 mm para calcular `mm por tick`.
6. Inicie uma sessão curta: o MPU6050 controla a orientação e o encoder mede avanço e recuo.

O jogo pausa depois de 500 ms sem pacotes válidos. Nesse caso confira o cabo USB, a porta COM, o Serial Monitor fechado e a taxa do firmware.

## Problemas comuns

- **O executável não abre:** confirme que a pasta `UreteroscopyTraining_Data` continua ao lado do `.exe`.
- **Tela sem caso ou rota:** abra a Console do Unity, procure o primeiro erro vermelho e confirme que `StreamingAssets/Cases` foi incluído no build.
- **Alteração sumiu:** ela provavelmente foi feita durante o Play Mode; repita com o jogo parado e salve a cena.
- **A vareta não conecta:** feche o Serial Monitor do Arduino, use `AUTO` ou informe a porta COM manualmente.
- **O movimento avança na direção errada:** refaça a calibração de 100 mm e, se necessário, inverta os canais A/B do encoder.
- **A orientação deriva:** recoloque a vareta na posição neutra, mantenha-a imóvel e pressione o botão de calibração ou `C` no simulador.

## Aprendizado recomendado

Para operar e adaptar este protótipo, aprenda nesta ordem:

1. Hierarchy, Scene, Game, Inspector, GameObjects e componentes;
2. materiais, iluminação e câmeras;
3. Play Mode, Console e build para Windows;
4. Arduino IDE, porta serial e gravação no ESP32;
5. barramento I2C, MPU6050 e encoder incremental.

Materiais de referência:

- [Unity Essentials](https://learn.unity.com/pathway/unity-essentials?language=en), curso oficial para Editor, objetos, física, scripts e publicação;
- [abrir um projeto pelo Unity Hub](https://learn.unity.com/tutorial/open-the-unity-essentials-project), tutorial oficial;
- [Unity Creative Core](https://learn.unity.com/pathway/creative-core?language=en), para materiais, luzes, câmeras, VFX e áudio;
- [canal oficial do Unity no YouTube](https://www.youtube.com/@unity), pesquisando por `Unity 6 Editor`, `Lighting`, `Materials` e `Build`;
- [Arduino ESP32: primeiros passos](https://docs.espressif.com/projects/arduino-esp32/en/latest/getting_started.html), documentação oficial;
- [canal oficial da Espressif no YouTube](https://www.youtube.com/@EspressifSystems) e [introdução ao ESP32-S3](https://www.youtube.com/watch?v=AuO-pQbbZCE);
- [canal oficial do Arduino no YouTube](https://www.youtube.com/@Arduino).

## Regras e dados

A sessão termina quando a ponta permanece por 0,5 s a no máximo `máx(raio da pedra + 5 mm, 8 mm)`, aponta para o alvo com erro de até 15 graus e o usuário aciona o gatilho. Avanço contra a parede é bloqueado; recuo continua permitido.

A nota combina segurança (40), precisão (30), eficiência (20) e tempo (10). Sessões interrompidas são registradas como `DNF` e não recebem nota. O CSV fica em `%USERPROFILE%/AppData/LocalLow/<Company>/<Product>/Sessions/ureteroscopy_sessions.csv` e contém apenas código escolhido pelo pesquisador, caso, rota, dificuldade e métricas. Não digite nome, CPF, prontuário ou outro identificador pessoal.

## Limitação anatômica

O início é o ponto de entrada humano-revisado da máscara disponível. A cena não afirma reproduzir todo o percurso externo, uretra, bexiga, ureter e rim. Uma simulação contínua dessas estruturas depende de dados segmentados e validados que ainda não estão no repositório.
