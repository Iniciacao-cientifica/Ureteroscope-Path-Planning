# Treinador desktop com vareta física

Protótipo acadêmico/educacional para navegação em uma máscara anatômica revisada. Não é um dispositivo médico e não deve ser usado em pacientes, procedimentos clínicos, diagnóstico, navegação intraoperatória ou controle de equipamentos.

## O que já funciona

- Visão endoscópica na ponta virtual e minimapa externo.
- Casos, pedras e rotas do pipeline v2 já existente.
- Tutorial com rota interna, intermediário com rota somente no minimapa e avançado sem rota.
- Modo `Treinamento`, confinado à anatomia e ao corredor seguro da rota, e modo `Exploração livre`, sem pontuação ou CSV.
- Controle por teclado/mouse ou ESP32-S3 via USB.
- Colisões nas duas direções, tempo, desvio RMS, eficiência, pontuação e resultado CSV anônimo.
- Seta 3D ciano indicando o próximo trecho da rota e laboratório científico estilizado na exploração.
- Pausa após 500 ms sem pacotes do controle.

## Executar o build pronto no Windows

1. Abra `UnityVRPrototype/Builds/Desktop/UreteroscopyTraining.exe`.
2. Não mova somente o arquivo `.exe`: mantenha-o ao lado da pasta `UreteroscopyTraining_Data`.
3. Na tela inicial escolha `Treinamento`, `Teclado`, dificuldade `Tutorial` e um código anônimo, por exemplo `TESTE-001`.
4. Inicie a sessão e confirme que aparecem a visão interna, o minimapa, a rota, a pedra e as métricas.

No modo teclado:

- `W/S`: avanço e recuo;
- mouse ou setas: inclinação e direção;
- botão esquerdo/direito do mouse: avanço e recuo (alternativa ao `W/S`);
- `Q/E`: rotação axial;
- `Espaço` ou botão central do mouse: botão de ação;
- `C`: calibração/recentralização.

A sensibilidade do mouse pode ser ajustada entre `0,5` e `4,0` na tela inicial e fica salva para as próximas execuções.

No modo `Exploração livre`, a ponta pode atravessar e sair da anatomia. Esse modo não calcula nota, colisões ou métricas e não grava CSV. O laboratório azul-petróleo é um ambiente visual acadêmico estilizado e não representa anatomia real.

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
- desvio máximo da rota, limite de colisões e duração do flash vermelho;
- sensibilidade do mouse e modo inicial da experiência;
- modo de entrada e porta serial padrão.

Selecione `Training Case Loader` para ajustar:

- cores e transparência da anatomia;
- cores da pedra, rota, início e alvo;
- espessura da rota e tamanho dos marcadores;
- duração da animação que percorre a rota.

O campo de visão da câmera, o material interno e o HUD são configurados por `Assets/Scripts/UreteroscopyTrainingController.cs`. Esses itens exigem alteração de código, recompilação e um novo teste.

Salve a cena com `Ctrl+S`. Para gerar um novo executável use `Murillo VR > Build Desktop Training (Windows)`. O resultado fica em `UnityVRPrototype/Builds/Desktop/UreteroscopyTraining.exe`.

## Usar a vareta física

1. Grave `hardware/firmware/mpu6050_text_test/mpu6050_text_test.ino` no ESP32.
2. Conecte o ESP32 ao computador por USB e mantenha o MPU6050 imóvel.
3. No jogo escolha `MPU6050 USB` e deixe a porta como `AUTO`.
4. Se a conexão automática falhar, consulte a porta no Gerenciador de Dispositivos do Windows e informe `COM3`, `COM4` ou a porta encontrada.
5. Inicie uma sessão, clique em `CALIBRAR AGORA` com o controle na posição neutra e incline para frente ou para trás para mover.
6. Use `INVERTER AVANÇO` se o sentido físico ficar trocado. O botão no GPIO 25 confirma a pedra quando o alvo estiver alinhado.

Este modo experimental aceita o texto de aceleração, giroscópio e botão enviado a cada 700 ms. O jogo pausa depois de dois segundos sem uma amostra completa. Nesse caso confira o cabo USB, a porta COM e se o Monitor Serial está fechado.

## Problemas comuns

- **O executável não abre:** confirme que a pasta `UreteroscopyTraining_Data` continua ao lado do `.exe`.
- **Tela sem caso ou rota:** abra a Console do Unity, procure o primeiro erro vermelho e confirme que `StreamingAssets/Cases` foi incluído no build.
- **Alteração sumiu:** ela provavelmente foi feita durante o Play Mode; repita com o jogo parado e salve a cena.
- **A vareta não conecta:** feche o Serial Monitor do Arduino, use `AUTO` ou informe a porta COM manualmente.
- **O movimento avança na direção errada:** volte ao menu e ative `INVERTER AVANÇO`.
- **A orientação deriva:** recoloque a vareta na posição neutra, mantenha-a imóvel e pressione o botão de calibração ou `C` no simulador.
- **A ponta não atravessa uma abertura no treinamento:** isso é esperado quando a abertura sai do corredor seguro de 15 mm; use `Exploração livre` para inspecionar o exterior.

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

A sessão termina quando a ponta permanece por 0,5 s a no máximo `máx(raio da pedra + 5 mm, 8 mm)`, aponta para o alvo com erro de até 15 graus e o usuário aciona o gatilho. Impactos contra faces internas ou externas são bloqueados tanto no avanço quanto no recuo. O corredor de segurança começa em 15 mm ao redor da rota e impede fugas por pequenas aberturas da malha. Manter o movimento pressionado durante um contato conta uma colisão; afastar e colidir novamente inicia outro episódio. A quinta colisão encerra a tentativa como `DNF`.

A nota combina segurança (40), precisão (30), eficiência (20) e tempo (10). O botão `DESISTIR` pede confirmação; sessões confirmadas são registradas como `DNF` e não recebem nota. O CSV fica em `%USERPROFILE%/AppData/LocalLow/<Company>/<Product>/Sessions/ureteroscopy_sessions.csv` e contém apenas código escolhido pelo pesquisador, caso, rota, dificuldade e métricas. Não digite nome, CPF, prontuário ou outro identificador pessoal.

## Limitação anatômica

O início é o ponto de entrada humano-revisado da máscara disponível. A cena não afirma reproduzir todo o percurso externo, uretra, bexiga, ureter e rim. Uma simulação contínua dessas estruturas depende de dados segmentados e validados que ainda não estão no repositório.
