using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NavegacaoRenal.Editor
{
    public static class Marco6ProjectSetup
    {
        private const string GameScenePath = "Assets/Scenes/KidneyGame.unity";
        private const string MenuScenePath = "Assets/Scenes/MainMenu.unity";
        private const string ModelPath = "Assets/Art/Kidney/Models/Kidney_Game_v003.fbx";
        private const string LegacyModelPath = "Assets/Art/Kidney/Models/Kidney_Game_v002.fbx";
        private const string MeshyModelPath = "Assets/Art/UrinarySystem/Models/Meshy_Urinary_Visual_v002.fbx";
        private const string ExpectedModelHash = "f721e63ad9188f007520709f24cb7c60e85ce3a2588bec6b533e7460fddc9bcf";
        private const string ExpectedLegacyHash = "174fabbf6ec31b3052360be995b5bbc4fb7e074b91ef2a2bba838ca45cc0fa9c";
        private const string ExpectedMeshyHash = "f8408f41656011f65cb737b1b434e7611228036b76fb8fcadfaa183dcfa26ed2";

        private static readonly Color PanelColor = new Color(0.075f, 0.018f, 0.028f, 0.96f);
        private static readonly Color AccentColor = new Color(0.95f, 0.19f, 0.27f, 1f);
        private static readonly Color SoftAccentColor = new Color(1f, 0.48f, 0.52f, 1f);

        [MenuItem("Navegacao Renal/Construir Marco 6")]
        public static void Configure()
        {
            Debug.Log("[Marco6] Construindo controle ESP32/MPU e entrega Windows.");
            Marco5ProjectSetup.Validate();
            ConfigureGameScene();
            ConfigureMenuScene();
            ConfigurePlayerSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Validate();
            CapturePreviews();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Marco6] Cenas, validacao simulada e previews concluidos.");
        }

        [MenuItem("Navegacao Renal/Validar Marco 6")]
        public static void Validate()
        {
            List<string> checks = new List<string>();
            List<string> errors = new List<string>();
            int legacyChecks = ReadLegacyCheckCount();
            Scene gameScene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            KidneyGameManager manager = FindSceneComponent<KidneyGameManager>(gameScene, "KidneyGameManager");
            MouseEndoscopeController controller = FindSceneComponent<MouseEndoscopeController>(gameScene, "ProbeTip");
            MouseKeyboardInputSource mouse = FindSceneComponent<MouseKeyboardInputSource>(gameScene, "KidneyGameManager");
            Esp32MpuInputSource mpu = FindSceneComponent<Esp32MpuInputSource>(gameScene, "KidneyGameManager");
            EndoscopeInputRouter router = FindSceneComponent<EndoscopeInputRouter>(gameScene, "KidneyGameManager");
            KidneyHardwareUI hardwareUi = FindSceneComponent<KidneyHardwareUI>(gameScene, "GameplayCanvas");

            Check(manager != null && controller != null && mouse != null && mpu != null && router != null,
                "cena possui controlador, fontes de mouse/MPU e roteador", checks, errors);
            Check(router != null && router.MouseKeyboard == mouse && router.Esp32Mpu == mpu,
                "roteador referencia as duas fontes de entrada", checks, errors);
            Check(manager != null && manager.InputRouter == router && manager.HardwareUI == hardwareUi,
                "gerenciador integra roteador e interface de hardware", checks, errors);
            Check(controller != null && controller.InputSourceBehaviour == router,
                "controlador de movimento consome o roteador sem trocar a fisica", checks, errors);
            Check(hardwareUi != null && hardwareUi.IsConfigured,
                "Canvas possui conexao, porta COM, calibracao e sensibilidade", checks, errors);
            Check(controller != null && Approximately(controller.ForwardSpeed, 0.10f) &&
                  Approximately(controller.TipRadius, 0.010f) && Approximately(controller.MaximumSubstepDistance, 0.005f) &&
                  Approximately(controller.CollisionSkin, 0.001f) && Approximately(controller.ContactRearmRadius, 0.015f),
                "SphereCast preserva velocidade, raio, subpasso, margem e rearme", checks, errors);
            Check(controller != null && Approximately(controller.MaximumSteeringSpeed, 70f) &&
                  Approximately(controller.SteeringSmoothTime, 0.12f),
                "orientacao preserva limite de 70 graus por segundo e suavizacao de 0,12 s", checks, errors);
            Check(mpu != null && Approximately(mpu.StalePacketSeconds, 0.25f) &&
                  Approximately(mpu.OrientationDeadZoneDegrees, 1.5f),
                "MPU usa timeout de 250 ms e zona morta pequena", checks, errors);
            Check(CountMissingScripts(gameScene) == 0, "cena de jogo sem scripts ausentes", checks, errors);

            const string validPacket = "{\"v\":2,\"seq\":123,\"ms\":4567,\"q\":[1.0,0.0,0.0,0.0],\"button\":true,\"imu_ok\":true,\"fw\":\"mpu6050-button-v2.0.0\"}";
            Check(Esp32MpuPacketParser.TryParse(validPacket, out Esp32MpuPacket parsed) &&
                  parsed.ProtocolVersion == 2 && parsed.Sequence == 123 && parsed.ButtonPressed && parsed.ImuOk &&
                  parsed.FirmwareVersion == "mpu6050-button-v2.0.0",
                "parser aceita pacote JSON v2 completo", checks, errors);
            Check(!Esp32MpuPacketParser.TryParse(validPacket.Replace("\"v\":2", "\"v\":1"), out _),
                "parser rejeita versao incompatível", checks, errors);
            Check(!Esp32MpuPacketParser.TryParse("texto de diagnostico", out _) &&
                  !Esp32MpuPacketParser.TryParse(validPacket.Replace("1.0,0.0,0.0,0.0", "0,0,0,0"), out _),
                "parser descarta texto e quaternion invalido", checks, errors);
            Check(Esp32MpuPacketParser.IsNewerSequence(11, 10) &&
                  !Esp32MpuPacketParser.IsNewerSequence(10, 10) &&
                  Esp32MpuPacketParser.IsNewerSequence(0, uint.MaxValue),
                "sequencia rejeita duplicatas e aceita retorno de uint", checks, errors);

            MpuOrientationMapper mapper = new MpuOrientationMapper();
            mapper.Calibrate(Quaternion.Euler(12f, -8f, 22f));
            Quaternion neutral = mapper.MapRelative(Quaternion.Euler(12f, -8f, 22f), 1f, 1.5f);
            Check(Quaternion.Angle(Quaternion.identity, neutral) < 0.001f,
                "calibracao transforma a pose inicial em orientacao neutra", checks, errors);
            mapper.Calibrate(Quaternion.identity);
            Quaternion verticalTilt = mapper.MapRelative(Quaternion.AngleAxis(20f, Vector3.right), 1f, 1.5f);
            Vector3 verticalDirection = verticalTilt * Vector3.forward;
            Check(verticalDirection.y > 0.2f && Mathf.Abs(verticalDirection.x) < 0.01f,
                "inclinacao frente/tras orienta a camera para cima/baixo", checks, errors);
            Quaternion lateralTilt = mapper.MapRelative(Quaternion.AngleAxis(20f, Vector3.up), 1f, 1.5f);
            Vector3 lateralDirection = lateralTilt * Vector3.forward;
            Check(lateralDirection.x > 0.2f && Mathf.Abs(lateralDirection.y) < 0.01f,
                "inclinacao lateral orienta a camera para esquerda/direita", checks, errors);
            Quaternion axialTurn = mapper.MapRelative(Quaternion.AngleAxis(35f, Vector3.forward), 1f, 1.5f);
            Check(Quaternion.Angle(Quaternion.identity, axialTurn) < 0.001f,
                "giro axial do MPU nao causa deriva de direcao", checks, errors);
            Check(Quaternion.Angle(Quaternion.identity,
                      mapper.MapRelative(Quaternion.Euler(0.4f, 0f, 0f), 1f, 1.5f)) < 0.001f,
                "zona morta elimina tremor inferior a 1,5 grau", checks, errors);
            float lowGainAngle = Quaternion.Angle(Quaternion.identity,
                mapper.MapRelative(Quaternion.AngleAxis(20f, Vector3.right), 0.5f, 0f));
            float highGainAngle = Quaternion.Angle(Quaternion.identity,
                mapper.MapRelative(Quaternion.AngleAxis(20f, Vector3.right), 2f, 0f));
            Check(lowGainAngle > 9f && lowGainAngle < 11f && highGainAngle > 39f && highGainAngle < 41f,
                "resposta do MPU escala a inclinacao entre 0,5x e 2x", checks, errors);

            ValidateWallSafety(controller, checks, errors);

            Esp32ButtonInterpreter button = new Esp32ButtonInterpreter(0.35);
            Esp32ButtonState forward = button.Update(true, 0.0, false);
            button.Update(false, 0.10, false);
            Esp32ButtonState reverse = button.Update(true, 0.20, false);
            Check(Approximately(forward.Advance, 1f) && forward.Direction == 1,
                "segurar botao inicia em avanco", checks, errors);
            Check(Approximately(reverse.Advance, -1f) && reverse.Direction == -1,
                "clique duplo em 350 ms alterna para recuo", checks, errors);
            button.Update(false, 0.30, false);
            Esp32ButtonState capture = button.Update(true, 1.0, true);
            Check(Approximately(capture.Advance, 0f) && capture.CaptureHeld && capture.Direction == -1,
                "perto da pedra o botao bloqueia movimento e comanda captura", checks, errors);
            button.Reset();
            Check(button.Direction == 1, "nova tentativa restaura direcao Avanco", checks, errors);

            float angle30 = SimulateOrientation(45f, 1f, 30);
            float angle60 = SimulateOrientation(45f, 1f, 60);
            float angle120 = SimulateOrientation(45f, 1f, 120);
            Check(Mathf.Abs(angle30 - angle60) < 0.6f && Mathf.Abs(angle60 - angle120) < 0.6f,
                "orientacao equivalente em 30, 60 e 120 FPS", checks, errors);
            Check(angle120 <= 45.01f && angle120 > 40f,
                "suavizacao converge sem ultrapassar o alvo", checks, errors);

            ReplayEsp32PacketTransport replay = new ReplayEsp32PacketTransport();
            replay.Start();
            replay.Push(parsed);
            Check(replay.Status == Esp32ConnectionStatus.Streaming && replay.TryGetLatest(out _, out double replayAge) && replayAge == 0,
                "transporte de replay fornece pacote deterministico a 50 Hz", checks, errors);
            replay.Disconnect();
            Check(replay.Status == Esp32ConnectionStatus.Error && replay.TryGetLatest(out _, out replayAge) && double.IsPositiveInfinity(replayAge),
                "replay simula desconexao e pacote obsoleto", checks, errors);
            replay.Stop();
            Check(replay.Status == Esp32ConnectionStatus.Stopped,
                "transporte encerra sem deixar leitura ativa", checks, errors);

            string transportSource = File.ReadAllText(ToAbsolute("Assets/Scripts/Runtime/IEsp32PacketTransport.cs"));
            Check(transportSource.Contains("IsBackground = true") && transportSource.Contains("ReadTotalTimeoutConstant = readTimeoutMs") &&
                  transportSource.Contains("thread.Join(1200)"),
                "serial executa em thread de fundo com timeout e encerramento seguro", checks, errors);
            Check(transportSource.Contains("QueryDosDevice") && transportSource.Contains("115200"),
                "conexao oferece busca automatica/lista COM em 115200 baud", checks, errors);
            string managerSource = File.ReadAllText(ToAbsolute("Assets/Scripts/Runtime/KidneyGameManager.cs"));
            Check(managerSource.Contains("pausedForHardwareReconnect") && managerSource.Contains("HandleHardwareConnection"),
                "desconexao pausa e reconexao retoma a tentativa", checks, errors);
            Check(managerSource.Contains("controller.HasClearPathTo(targetStone.position)"),
                "captura exige caminho livre entre a ponta e a pedra", checks, errors);
            string inputSource = File.ReadAllText(ToAbsolute("Assets/Scripts/Runtime/Esp32MpuInputSource.cs"));
            Check(inputSource.Contains("cKey.wasPressedThisFrame") && inputSource.Contains("CalibrateNow"),
                "tecla C recalibra o MPU sem reiniciar", checks, errors);

            Scene menuScene = EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);
            MainMenuPresenter menu = FindSceneComponent<MainMenuPresenter>(menuScene, "MainMenu");
            Check(menu != null && menu.RealisticButton != null && menu.RealisticMpuButton != null && menu.ExplorationButton != null,
                "menu separa Realista Mouse, Realista MPU e Exploracao", checks, errors);
            Check(CountMissingScripts(menuScene) == 0, "menu sem scripts ausentes", checks, errors);

            string firmwarePath = Path.GetFullPath(Path.Combine(Application.dataPath,
                "../../../hardware/firmware/ureteroscope_controller/ureteroscope_controller.ino"));
            string platformPath = Path.GetFullPath(Path.Combine(Application.dataPath,
                "../../../hardware/firmware/ureteroscope_controller/platformio.ini"));
            string firmware = File.ReadAllText(firmwarePath);
            string platform = File.ReadAllText(platformPath);
            Check(firmware.Contains("PIN_SDA = 21") && firmware.Contains("PIN_SCL = 22") && firmware.Contains("PIN_ACTION = 25"),
                "firmware usa SDA21, SCL22 e botao GPIO25", checks, errors);
            Check(firmware.Contains("SAMPLE_HZ = 100.0f") && firmware.Contains("SEND_PERIOD_MS = 20"),
                "firmware amostra a 100 Hz e envia a 50 Hz", checks, errors);
            Check(firmware.Contains("GYRO_CALIBRATION_MS = 2000") && firmware.Contains("Madgwick"),
                "firmware calibra por dois segundos e aplica Madgwick", checks, errors);
            Check(firmware.Contains("\\\"button\\\":%s") && firmware.Contains("mpu6050-button-v2.0.0") &&
                  !firmware.Contains("ENCODER"),
                "JSON v2 contem botao e firmware nao depende de encoder", checks, errors);
            Check(platform.Contains("[env:esp32dev]") && platform.Contains("board = esp32dev"),
                "PlatformIO aponta para ESP32 DevKit V1", checks, errors);

            string modelHash = Sha256(ToAbsolute(ModelPath));
            string legacyHash = Sha256(ToAbsolute(LegacyModelPath));
            string meshyHash = Sha256(ToAbsolute(MeshyModelPath));
            Check(modelHash == ExpectedModelHash, "FBX renal v003 permaneceu inalterado", checks, errors);
            Check(legacyHash == ExpectedLegacyHash, "FBX renal v002 permaneceu inalterado", checks, errors);
            Check(meshyHash == ExpectedMeshyHash, "FBX visual Meshy permaneceu inalterado", checks, errors);
            Check(legacyChecks == 133, $"133 verificacoes anteriores reexecutadas ({legacyChecks})", checks, errors);

            ValidationReport report = new ValidationReport
            {
                milestone = "Marco 6",
                unityVersion = Application.unityVersion,
                generatedUtc = DateTime.UtcNow.ToString("O"),
                passed = errors.Count == 0,
                validationMode = "simulacao/replay; hardware fisico ainda nao montado",
                hardwarePhysicallyTested = false,
                legacyChecks = legacyChecks,
                marco6Checks = checks.Count,
                totalChecks = legacyChecks + checks.Count,
                fbxV003Sha256 = modelHash,
                fbxV002Sha256 = legacyHash,
                meshyFbxSha256 = meshyHash,
                checks = checks.ToArray(),
                errors = errors.ToArray()
            };
            string reportPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../Documentation/marco6_validation.json"));
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
            File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
            Debug.Log($"[Marco6] Relatorio: {reportPath}\n{JsonUtility.ToJson(report, true)}");
            if (errors.Count > 0) throw new InvalidOperationException("Marco 6 falhou: " + string.Join(" | ", errors));
        }

        [MenuItem("Navegacao Renal/Gerar Build Windows Marco 6")]
        public static void BuildWindows()
        {
            Environment.SetEnvironmentVariable("UNITY_BURST_DISABLE_COMPILATION", "1");
            DisableBurstForStandalone();
            Validate();
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            string buildDirectory = Path.Combine(desktop, "Navegacao_Renal_3D_Unity_Build_Marco6");
            string executablePath = Path.Combine(buildDirectory, "Navegacao_Renal_3D.exe");
            string zipPath = buildDirectory + ".zip";
            if (Directory.Exists(buildDirectory)) Directory.Delete(buildDirectory, true);
            if (File.Exists(zipPath)) File.Delete(zipPath);
            Directory.CreateDirectory(buildDirectory);

            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.Standalone, ApiCompatibilityLevel.NET_Unity_4_8);
            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = new[] { MenuScenePath, GameScenePath },
                locationPathName = executablePath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };
            BuildReport build = BuildPipeline.BuildPlayer(options);
            if (build.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException($"Build Windows falhou: {build.summary.result}");

            File.WriteAllText(Path.Combine(buildDirectory, "LEIA-ME.txt"),
                "NAVEGACAO RENAL 3D - MARCO 6\r\n\r\n" +
                "Execute Navegacao_Renal_3D.exe.\r\n" +
                "Controle fisico: ESP32 DevKit V1 + MPU6050 + botao, USB 115200 baud.\r\n" +
                "Este build foi validado por simulacao/replay; o hardware fisico ainda nao foi ensaiado.\r\n" +
                "Consulte Documentation/MARCO_6.md no projeto para montagem e testes.\r\n");
            ZipFile.CreateFromDirectory(buildDirectory, zipPath, System.IO.Compression.CompressionLevel.Optimal, false);

            string manifestPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../Documentation/marco6_build.json"));
            File.WriteAllText(manifestPath, JsonUtility.ToJson(new BuildManifest
            {
                generatedUtc = DateTime.UtcNow.ToString("O"),
                unityVersion = Application.unityVersion,
                target = "Windows x86-64",
                executable = executablePath,
                zip = zipPath,
                totalSizeBytes = build.summary.totalSize,
                result = build.summary.result.ToString()
            }, true));
            AssetDatabase.Refresh();
            Debug.Log($"[Marco6] Build portatil: {buildDirectory}\n[Marco6] ZIP: {zipPath}");
        }

        private static void DisableBurstForStandalone()
        {
            Type settingsType = Type.GetType("Unity.Burst.Editor.BurstPlatformAotSettings, Unity.Burst.Editor");
            if (settingsType == null) return;
            MethodInfo getSettings = settingsType.GetMethod("GetOrCreateSettings",
                BindingFlags.Static | BindingFlags.NonPublic);
            object settings = getSettings?.Invoke(null, new object[] { (BuildTarget?)BuildTarget.StandaloneWindows64 });
            if (settings == null) return;
            FieldInfo enabled = settingsType.GetField("EnableBurstCompilation",
                BindingFlags.Instance | BindingFlags.NonPublic);
            enabled?.SetValue(settings, false);
            MethodInfo save = settingsType.GetMethod("Save", BindingFlags.Instance | BindingFlags.NonPublic);
            save?.Invoke(settings, new object[] { (BuildTarget?)BuildTarget.StandaloneWindows64 });
        }

        private static void ConfigureGameScene()
        {
            Scene scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            KidneyGameManager manager = FindSceneComponent<KidneyGameManager>(scene, "KidneyGameManager");
            MouseEndoscopeController controller = FindSceneComponent<MouseEndoscopeController>(scene, "ProbeTip");
            MouseKeyboardInputSource mouse = manager != null ? manager.GetComponent<MouseKeyboardInputSource>() : null;
            Canvas canvas = FindSceneComponent<Canvas>(scene, "GameplayCanvas");
            KidneyGameUI gameUi = canvas != null ? canvas.GetComponent<KidneyGameUI>() : null;
            if (manager == null || controller == null || mouse == null || canvas == null || gameUi == null)
                throw new InvalidOperationException("Base do Marco 5 incompleta para construir o Marco 6.");

            DestroySceneObject(scene, "HardwareConnectionPanel");
            DestroySceneObject(scene, "HardwareStatusPanel");
            DestroySceneObject(scene, "InputSensitivityPanel");
            KidneyHardwareUI oldHardwareUi = canvas.GetComponent<KidneyHardwareUI>();
            if (oldHardwareUi != null) UnityEngine.Object.DestroyImmediate(oldHardwareUi);

            Esp32MpuInputSource mpu = manager.GetComponent<Esp32MpuInputSource>();
            if (mpu == null) mpu = manager.gameObject.AddComponent<Esp32MpuInputSource>();
            EndoscopeInputRouter router = manager.GetComponent<EndoscopeInputRouter>();
            if (router == null) router = manager.gameObject.AddComponent<EndoscopeInputRouter>();
            mpu.Configure(manager);
            router.Configure(mouse, mpu);
            router.SelectMode(EndoscopeControlMode.MouseKeyboard);

            GameObject ready = FindSceneObject(scene, "ReadyPanel");
            Text readyControls = FindSceneObject(scene, "ReadyControls")?.GetComponent<Text>();
            Button start = FindSceneObject(scene, "StartButton")?.GetComponent<Button>();
            if (ready == null || readyControls == null || start == null)
                throw new InvalidOperationException("Painel Ready do Marco 4 nao encontrado.");
            ready.GetComponent<RectTransform>().sizeDelta = new Vector2(680f, 480f);
            start.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -178f);

            GameObject sensitivityPanel = CreatePanel("InputSensitivityPanel", ready.transform, new Vector2(500f, 62f),
                new Vector2(0f, -108f), new Vector2(0.5f, 0.5f), new Color(0.12f, 0.035f, 0.05f, 0.92f));
            Text sensitivityLabel = CreateText("SensitivityLabel", sensitivityPanel.transform, "Sensibilidade do mouse", 14,
                FontStyle.Bold, TextAnchor.MiddleLeft, new Vector2(190f, 28f), new Vector2(-138f, 0f), Color.white);
            Slider sensitivity = CreateSlider("InputSensitivitySlider", sensitivityPanel.transform,
                new Vector2(250f, 22f), new Vector2(105f, 0f));

            GameObject connection = CreatePanel("HardwareConnectionPanel", canvas.transform, new Vector2(470f, 350f),
                new Vector2(-28f, 0f), new Vector2(1f, 0.5f), PanelColor);
            CreateText("HardwareTitle", connection.transform, "CONEXÃO MPU / ESP32", 24, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(420f, 40f), new Vector2(0f, 135f), Color.white);
            CreateText("HardwareHelp", connection.transform,
                "Ligue a placa por USB, aguarde a calibração inicial\ne mantenha o sensor parado por dois segundos.",
                15, FontStyle.Normal, TextAnchor.MiddleCenter, new Vector2(420f, 58f), new Vector2(0f, 86f), SoftAccentColor);
            Text connectionText = CreateText("ConnectionStatus", connection.transform, "Estado: procurando\nPorta: automática", 15,
                FontStyle.Bold, TextAnchor.MiddleCenter, new Vector2(410f, 62f), new Vector2(0f, 24f), Color.white);
            Dropdown dropdown = CreateDropdown("PortDropdown", connection.transform, new Vector2(310f, 38f), new Vector2(0f, -38f));
            Button reconnect = CreateButton("ReconnectButton", connection.transform, "CONECTAR / RECONECTAR",
                new Vector2(310f, 44f), new Vector2(0f, -92f));
            Button calibrate = CreateButton("CalibrateButton", connection.transform, "CALIBRAR AGORA (C)",
                new Vector2(310f, 40f), new Vector2(0f, -143f));

            GameObject statusPanel = CreatePanel("HardwareStatusPanel", canvas.transform, new Vector2(390f, 86f),
                new Vector2(215f, -184f), new Vector2(0f, 1f), PanelColor);
            Text hardwareStatus = CreateText("HardwareStatus", statusPanel.transform, "MPU desconectado", 14,
                FontStyle.Bold, TextAnchor.MiddleLeft, new Vector2(350f, 30f), new Vector2(0f, 18f), Color.white);
            Text direction = CreateText("HardwareDirection", statusPanel.transform, "Direção: Avanço", 16,
                FontStyle.Bold, TextAnchor.MiddleLeft, new Vector2(350f, 30f), new Vector2(0f, -18f), SoftAccentColor);

            KidneyHardwareUI hardwareUi = canvas.gameObject.AddComponent<KidneyHardwareUI>();
            hardwareUi.Configure(manager, router, controller, connection, statusPanel, connectionText, hardwareStatus,
                direction, readyControls, sensitivityLabel, dropdown, reconnect, calibrate, start, sensitivity);
            manager.ConfigureMarco6(router, hardwareUi);
            controller.ConfigureInputSource(router);
            connection.SetActive(false);
            statusPanel.SetActive(false);

            EditorUtility.SetDirty(mpu);
            EditorUtility.SetDirty(router);
            EditorUtility.SetDirty(hardwareUi);
            EditorUtility.SetDirty(manager);
            EditorUtility.SetDirty(controller);
            EditorSceneManager.SaveScene(scene, GameScenePath);
        }

        private static void ConfigureMenuScene()
        {
            Scene scene = EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);
            MainMenuPresenter presenter = FindSceneComponent<MainMenuPresenter>(scene, "MainMenu");
            if (presenter == null)
            {
                GameObject root = new GameObject("MainMenu");
                presenter = root.AddComponent<MainMenuPresenter>();
            }
            DestroySceneObject(scene, "MainMenuCanvas");
            DestroySceneObject(scene, "MainMenuEventSystem");
            CreateEventSystem("MainMenuEventSystem");
            Canvas canvas = CreateCanvas("MainMenuCanvas", 10);
            Image background = CreateImage("Background", canvas.transform, new Color(0.025f, 0.006f, 0.012f, 1f));
            Stretch(background.rectTransform);
            CreateText("Title", canvas.transform, "NAVEGAÇÃO RENAL 3D", 42, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(760f, 72f), new Vector2(0f, 245f), Color.white);
            CreateText("Subtitle", canvas.transform, "Simulador de ureteroscopia • nível fácil", 21, FontStyle.Normal,
                TextAnchor.MiddleCenter, new Vector2(720f, 44f), new Vector2(0f, 190f), SoftAccentColor);
            Button mouse = CreateButton("RealisticMouseButton", canvas.transform, "REALISTA — MOUSE E TECLADO",
                new Vector2(440f, 58f), new Vector2(0f, 95f));
            Button mpu = CreateButton("RealisticMpuButton", canvas.transform, "REALISTA — MPU / ESP32",
                new Vector2(440f, 58f), new Vector2(0f, 22f));
            Button exploration = CreateButton("ExplorationButton", canvas.transform, "EXPLORAÇÃO LIVRE",
                new Vector2(440f, 58f), new Vector2(0f, -51f));
            CreateText("Description", canvas.transform,
                "Mouse: controle tradicional completo.\nMPU: orientação física, botão para mover e capturar.\nExploração: atravesse livremente o sistema urinário.",
                16, FontStyle.Normal, TextAnchor.MiddleCenter, new Vector2(780f, 96f), new Vector2(0f, -153f),
                new Color(0.88f, 0.80f, 0.82f, 1f));
            CreateText("Version", canvas.transform, "Marco 6 • ESP32 DevKit V1 + MPU6050", 14, FontStyle.Normal,
                TextAnchor.MiddleCenter, new Vector2(560f, 30f), new Vector2(0f, -305f), new Color(0.62f, 0.52f, 0.55f, 1f));
            presenter.ConfigureMarco6(mouse, mpu, exploration);
            EditorUtility.SetDirty(presenter);
            EditorSceneManager.SaveScene(scene, MenuScenePath);
        }

        private static void ConfigurePlayerSettings()
        {
            PlayerSettings.companyName = "Navegacao Renal 3D";
            PlayerSettings.productName = "Navegacao Renal 3D";
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.resizableWindow = true;
        }

        private static void CapturePreviews()
        {
            Scene scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            KidneyGameManager manager = FindSceneComponent<KidneyGameManager>(scene, "KidneyGameManager");
            KidneyGameUI gameUi = FindSceneComponent<KidneyGameUI>(scene, "GameplayCanvas");
            Camera camera = FindSceneComponent<Camera>(scene, "EndoscopeCamera");
            Camera minimap = FindSceneComponent<Camera>(scene, "MinimapCameraFinal");
            Canvas canvas = FindSceneComponent<Canvas>(scene, "GameplayCanvas");
            GameObject ready = FindSceneObject(scene, "ReadyPanel");
            GameObject connection = FindSceneObject(scene, "HardwareConnectionPanel");
            GameObject status = FindSceneObject(scene, "HardwareStatusPanel");
            Text connectionText = FindSceneObject(scene, "ConnectionStatus")?.GetComponent<Text>();
            Text hardwareStatus = FindSceneObject(scene, "HardwareStatus")?.GetComponent<Text>();
            Text direction = FindSceneObject(scene, "HardwareDirection")?.GetComponent<Text>();
            Text readyControls = FindSceneObject(scene, "ReadyControls")?.GetComponent<Text>();
            Text sensitivityLabel = FindSceneObject(scene, "SensitivityLabel")?.GetComponent<Text>();
            string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Documentation/Previews"));
            Directory.CreateDirectory(directory);
            manager?.SetMode(KidneyGameMode.Realistic, true);
            manager?.PrepareAttempt();
            gameUi?.RefreshImmediate();
            if (readyControls != null)
                readyControls.text = "Incline o MPU para orientar • Segure o botão para mover\nClique duplo: avanço/recuo • C: recalibrar • perto da pedra: capturar";
            if (sensitivityLabel != null) sensitivityLabel.text = "Resposta do MPU";
            ready?.SetActive(true);
            connection?.SetActive(true);
            status?.SetActive(false);
            if (connectionText != null) connectionText.text = "Estado: procurando\nPorta: automática\nAguardando pacote JSON v2";
            CaptureCamera(camera, minimap, canvas, Path.Combine(directory, "marco6_hardware_connection.png"));
            connection?.SetActive(false);
            status?.SetActive(true);
            if (hardwareStatus != null) hardwareStatus.text = "MPU conectado • COM3 • 50 Hz • C recalibrar";
            if (direction != null) direction.text = "Direção: Avanço";
            CaptureCamera(camera, minimap, canvas, Path.Combine(directory, "marco6_hardware_ready.png"));
            status?.SetActive(false);
        }

        private static float SimulateOrientation(float targetDegrees, float durationSeconds, int framesPerSecond)
        {
            Quaternion current = Quaternion.identity;
            Quaternion target = Quaternion.Euler(targetDegrees, 0f, 0f);
            float delta = 1f / framesPerSecond;
            int frames = Mathf.RoundToInt(durationSeconds * framesPerSecond);
            for (int index = 0; index < frames; index++)
                current = MouseEndoscopeController.AdvanceHardwareOrientation(current, target, delta, 0.12f, 70f);
            return Quaternion.Angle(Quaternion.identity, current);
        }

        private static void ValidateWallSafety(MouseEndoscopeController sceneController,
            List<string> checks, List<string> errors)
        {
            int collisionLayer = LayerMask.NameToLayer("KidneyCollision");
            if (collisionLayer < 0)
            {
                Check(false, "camada KidneyCollision disponivel para testes de seguranca", checks, errors);
                return;
            }

            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            GameObject tip = new GameObject("Marco6SafetyProbe");
            try
            {
                wall.name = "Marco6SafetyWall";
                wall.layer = collisionLayer;
                wall.transform.SetPositionAndRotation(new Vector3(0f, 0f, 0.05f), Quaternion.identity);
                wall.transform.localScale = new Vector3(1f, 1f, 0.01f);

                MouseEndoscopeController controller = tip.AddComponent<MouseEndoscopeController>();
                controller.Configure(null, 1 << collisionLayer);
                Physics.SyncTransforms();

                Check(controller.HasClearPathTo(new Vector3(0f, 0f, 0.02f)) &&
                      !controller.HasClearPathTo(new Vector3(0f, 0f, 0.10f)),
                    "linha de captura aceita alvo livre e rejeita alvo atras da parede", checks, errors);

                bool completed = controller.TryMoveDistance(0.10f);
                Vector3 safePosition = controller.transform.position;
                Check(!completed && safePosition.z < 0.04f && controller.IsWallContactLatched,
                    "movimento para antes da parede e registra um contato", checks, errors);

                controller.transform.position = wall.transform.position;
                Physics.SyncTransforms();
                bool escapedOverlap = !controller.TryMoveDistance(0.01f) &&
                                      Vector3.Distance(controller.transform.position, safePosition) < 0.0001f;
                Check(escapedOverlap,
                    "sobreposicao acidental restaura a ultima posicao segura", checks, errors);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tip);
                UnityEngine.Object.DestroyImmediate(wall);
                if (sceneController != null) Physics.SyncTransforms();
            }
        }

        private static void CaptureCamera(Camera camera, Camera minimapCamera, Canvas canvas, string outputPath)
        {
            if (camera == null || canvas == null) throw new InvalidOperationException("Camera ou Canvas ausente para preview.");
            RenderMode savedMode = canvas.renderMode;
            Camera savedWorldCamera = canvas.worldCamera;
            float savedPlaneDistance = canvas.planeDistance;
            RenderTexture previousTarget = camera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            bool cameraWasActive = camera.gameObject.activeSelf;
            bool minimapWasActive = minimapCamera != null && minimapCamera.gameObject.activeSelf;
            RenderTexture target = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            Texture2D image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            try
            {
                camera.gameObject.SetActive(true);
                if (minimapCamera != null) { minimapCamera.gameObject.SetActive(true); minimapCamera.Render(); }
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = camera.nearClipPlane + 0.002f;
                camera.targetTexture = target;
                RenderTexture.active = target;
                Canvas.ForceUpdateCanvases();
                camera.Render();
                image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                image.Apply();
                File.WriteAllBytes(outputPath, image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                canvas.renderMode = savedMode;
                canvas.worldCamera = savedWorldCamera;
                canvas.planeDistance = savedPlaneDistance;
                camera.gameObject.SetActive(cameraWasActive);
                if (minimapCamera != null) minimapCamera.gameObject.SetActive(minimapWasActive);
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(image);
            }
        }

        private static Canvas CreateCanvas(string name, int sortOrder)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortOrder;
            CanvasScaler scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        private static void CreateEventSystem(string name)
        {
            GameObject go = new GameObject(name, typeof(EventSystem), typeof(InputSystemUIInputModule));
            go.GetComponent<EventSystem>().sendNavigationEvents = true;
        }

        private static GameObject CreatePanel(string name, Transform parent, Vector2 size, Vector2 position, Vector2 anchor, Color color)
        {
            Image image = CreateImage(name, parent, color);
            SetRect(image.rectTransform, size, position, anchor);
            return image.gameObject;
        }

        private static Image CreateImage(string name, Transform parent, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static Text CreateText(string name, Transform parent, string content, int size, FontStyle style,
            TextAnchor alignment, Vector2 rectSize, Vector2 position, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            Text text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = content;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            SetRect(text.rectTransform, rectSize, position, new Vector2(0.5f, 0.5f));
            return text;
        }

        private static Button CreateButton(string name, Transform parent, string label, Vector2 size, Vector2 position)
        {
            Image image = CreateImage(name, parent, AccentColor);
            SetRect(image.rectTransform, size, position, new Vector2(0.5f, 0.5f));
            Button button = image.gameObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = AccentColor;
            colors.highlightedColor = new Color(1f, 0.34f, 0.40f, 1f);
            colors.pressedColor = new Color(0.72f, 0.08f, 0.14f, 1f);
            button.colors = colors;
            CreateText("Label", image.transform, label, 15, FontStyle.Bold, TextAnchor.MiddleCenter, size, Vector2.zero, Color.white);
            return button;
        }

        private static Dropdown CreateDropdown(string name, Transform parent, Vector2 size, Vector2 position)
        {
            GameObject go = DefaultControls.CreateDropdown(new DefaultControls.Resources());
            go.name = name;
            go.transform.SetParent(parent, false);
            SetRect(go.GetComponent<RectTransform>(), size, position, new Vector2(0.5f, 0.5f));
            Dropdown dropdown = go.GetComponent<Dropdown>();
            dropdown.options = new List<Dropdown.OptionData> { new Dropdown.OptionData("Automático") };
            return dropdown;
        }

        private static Slider CreateSlider(string name, Transform parent, Vector2 size, Vector2 position)
        {
            GameObject go = DefaultControls.CreateSlider(new DefaultControls.Resources());
            go.name = name;
            go.transform.SetParent(parent, false);
            SetRect(go.GetComponent<RectTransform>(), size, position, new Vector2(0.5f, 0.5f));
            Slider slider = go.GetComponent<Slider>();
            slider.minValue = 0.5f;
            slider.maxValue = 2f;
            slider.value = 1f;
            return slider;
        }

        private static void SetRect(RectTransform rect, Vector2 size, Vector2 position, Vector2 anchor)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static int ReadLegacyCheckCount()
        {
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../Documentation/marco5_validation.json"));
            if (!File.Exists(path)) return 0;
            LegacyValidationReport report = JsonUtility.FromJson<LegacyValidationReport>(File.ReadAllText(path));
            return report != null ? report.totalChecks : 0;
        }

        private static int CountMissingScripts(Scene scene) => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .Sum(transform => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject));

        private static void DestroySceneObject(Scene scene, string objectName)
        {
            GameObject target = FindSceneObject(scene, objectName);
            if (target != null) UnityEngine.Object.DestroyImmediate(target);
        }

        private static T FindSceneComponent<T>(Scene scene, string objectName) where T : Component
        {
            GameObject target = FindSceneObject(scene, objectName);
            return target != null ? target.GetComponent<T>() : null;
        }

        private static GameObject FindSceneObject(Scene scene, string objectName) => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .FirstOrDefault(transform => transform.name == objectName)?.gameObject;

        private static string ToAbsolute(string assetPath) => Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));

        private static string Sha256(string path)
        {
            if (!File.Exists(path)) return string.Empty;
            using FileStream stream = File.OpenRead(path);
            using SHA256 hash = SHA256.Create();
            return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static bool Approximately(float left, float right) => Mathf.Abs(left - right) < 0.0001f;

        private static void Check(bool condition, string label, List<string> checks, List<string> errors)
        {
            checks.Add(label);
            if (!condition) errors.Add(label);
        }

        [Serializable]
        private sealed class LegacyValidationReport { public int totalChecks; }

        [Serializable]
        private sealed class ValidationReport
        {
            public string milestone;
            public string unityVersion;
            public string generatedUtc;
            public bool passed;
            public string validationMode;
            public bool hardwarePhysicallyTested;
            public int legacyChecks;
            public int marco6Checks;
            public int totalChecks;
            public string fbxV003Sha256;
            public string fbxV002Sha256;
            public string meshyFbxSha256;
            public string[] checks;
            public string[] errors;
        }

        [Serializable]
        private sealed class BuildManifest
        {
            public string generatedUtc;
            public string unityVersion;
            public string target;
            public string executable;
            public string zip;
            public ulong totalSizeBytes;
            public string result;
        }
    }
}
