# Treinador desktop com vareta física

Protótipo acadêmico/educacional para navegação em uma máscara anatômica revisada. Não é um dispositivo médico e não deve ser usado em pacientes, procedimentos clínicos, diagnóstico, navegação intraoperatória ou controle de equipamentos.

## O que já funciona

- Visão endoscópica na ponta virtual e minimapa externo.
- Fase genérica HRA fixa, sem anatomia de paciente: bexiga, ureter direito, rim direito e pedra procedural.
- Lúmen simplificado criado em tempo de execução a partir de um percurso editável, com paredes visuais e físicas sincronizadas.
- Linha interna azul de 1 mm visível em Tutorial, Intermediário e Avançado, com cópia de 1,5 mm no minimapa.
- Modo `Treinamento`, confinado ao lúmen variável da fase, e modo `Exploração livre`, sem pontuação ou CSV.
- Controle por teclado/mouse ou ESP32-S3 via USB.
- Colisões nas duas direções, tempo, desvio RMS, eficiência, pontuação e resultado CSV anônimo.
- Sistema urinário externo HRA com dois rins, dois ureteres e bexiga no minimapa e na exploração.
- Pausa após 500 ms sem pacotes do controle.

## Executar o build pronto no Windows

1. Abra `UnityVRPrototype/Builds/Desktop/UreteroscopyTraining.exe`.
2. Não mova somente o arquivo `.exe`: mantenha-o ao lado da pasta `UreteroscopyTraining_Data`.
3. Na tela inicial escolha `Treinamento`, `Teclado`, dificuldade `Tutorial` e um código anônimo, por exemplo `TESTE-001`.
4. Inicie a sessão e confirme que a câmera começa dentro da bexiga, com minimapa, rota azul, pedra e métricas.

No modo teclado:

- `W/S`: avanço e recuo;
- mouse ou setas: inclinação e direção;
- botão esquerdo/direito do mouse: avanço e recuo (alternativa ao `W/S`);
- `Q/E`: rotação axial;
- `Espaço` ou botão central do mouse: botão de ação;
- `C`: calibração/recentralização.

A sensibilidade do mouse pode ser ajustada entre `0,5` e `4,0` na tela inicial e fica salva para as próximas execuções.

No modo `Exploração livre`, o lúmen interno fica oculto e a cena mostra por fora o mesmo conjunto HRA genérico usado no minimapa. A câmera começa livre, enquadrando o sistema inteiro. Use `W/S` para frente e trás, `A/D` para os lados, `Q/E` para subir e descer, botão direito com o mouse para olhar, `Shift` para acelerar, `F` para reenquadrar e `Tab` para alternar entre câmera e instrumento. A câmera não atravessa a anatomia. Esse modo não calcula nota, colisões ou métricas e não grava CSV.

A câmera principal usa uma única linha azul opaca de 1 mm, sem tubo, contorno ou troca por proximidade. O minimapa mantém uma cópia completa de 1,5 mm e apresenta o rim direito semitransparente. O alvo é uma pedra irregular marrom-ocre; na exploração, um anel âmbar discreto indica sua posição. A seta central aparece apenas no Tutorial; a linha permanece visível em todas as dificuldades.

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
- limite de colisões e duração do flash vermelho;
- sensibilidade do mouse e modo inicial da experiência;
- modo de entrada e porta serial padrão.

O curso genérico fica em `HraTrainingCourse`. Nele podem ser ajustados escala uniforme, espaçamento da rota, raios da bexiga/ureter/pelve, tamanho da pedra e pontos de controle manuais. Se não houver ao menos quatro pontos manuais, a rota inicial é derivada das âncoras HRA da bexiga, do ureter direito e da pelve renal. As larguras das linhas externa, interna, minimapa e halo ficam em `TrainingNavigationVisuals`. Velocidade livre, aceleração, colisão, distância, altura, deslocamento lateral e suavização da câmera ficam em `ExternalExplorationCameraController`; esses componentes são criados em tempo de execução pelo controlador desktop.

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
- **Tela sem fase ou rota:** abra a Console do Unity, procure o primeiro erro vermelho e confirme que os cinco GLBs existem em `Assets/Resources/HRAKidneys`.
- **Alteração sumiu:** ela provavelmente foi feita durante o Play Mode; repita com o jogo parado e salve a cena.
- **A vareta não conecta:** feche o Serial Monitor do Arduino, use `AUTO` ou informe a porta COM manualmente.
- **O movimento avança na direção errada:** refaça a calibração de 100 mm e, se necessário, inverta os canais A/B do encoder.
- **A orientação deriva:** recoloque a vareta na posição neutra, mantenha-a imóvel e pressione o botão de calibração ou `C` no simulador.
- **A ponta não atravessa a parede:** isso é esperado; o movimento é interrompido no limite do lúmen e o contato conta como colisão.

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

A sessão termina quando a ponta permanece por 0,5 s a no máximo `máx(raio da pedra + 5 mm, 8 mm)`, aponta para o alvo com erro de até 15 graus e o usuário aciona o gatilho. O usuário pode afastar-se da linha azul, mas não pode deixar o lúmen: cada deslocamento é subdividido e limitado no primeiro contato com a parede, tanto no avanço quanto no recuo. Manter o movimento pressionado durante um contato conta uma colisão; afastar e colidir novamente inicia outro episódio. A quinta colisão encerra a tentativa como `DNF`.

A nota combina segurança (40), precisão (30), eficiência (20) e tempo (10). O botão `DESISTIR` pede confirmação; sessões confirmadas são registradas como `DNF` e não recebem nota. O CSV fica em `%USERPROFILE%/AppData/LocalLow/<Company>/<Product>/Sessions/ureteroscopy_sessions.csv` e contém apenas código escolhido pelo pesquisador, caso, rota, dificuldade e métricas. Não digite nome, CPF, prontuário ou outro identificador pessoal.

## Limitação anatômica

O conjunto externo é uma referência masculina HRA e não uma reconstrução do paciente. O treino começa dentro da bexiga e segue ao rim direito, sem representar a uretra. O interior vermelho é um lúmen simplificado para treinamento, não uma reconstrução anatômica validada nem o interior de um paciente. O pipeline científico e o `VrCaseLoader` permanecem no repositório para futura integração, mas ficam desativados na fase desktop genérica.
