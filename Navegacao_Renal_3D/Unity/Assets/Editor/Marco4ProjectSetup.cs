using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NavegacaoRenal.Editor
{
    public static class Marco4ProjectSetup
    {
        private const string GameScenePath = "Assets/Scenes/KidneyGame.unity";
        private const string MenuScenePath = "Assets/Scenes/MainMenu.unity";
        private const string ModelPath = "Assets/Art/Kidney/Models/Kidney_Game_v003.fbx";
        private const string LegacyModelPath = "Assets/Art/Kidney/Models/Kidney_Game_v002.fbx";
        private const string MeshyModelPath = "Assets/Art/UrinarySystem/Models/Meshy_Urinary_Visual_v002.fbx";
        private const string ExpectedModelHash = "f721e63ad9188f007520709f24cb7c60e85ce3a2588bec6b533e7460fddc9bcf";
        private const string ExpectedLegacyHash = "174fabbf6ec31b3052360be995b5bbc4fb7e074b91ef2a2bba838ca45cc0fa9c";
        private const string ExpectedMeshyHash = "f8408f41656011f65cb737b1b434e7611228036b76fb8fcadfaa183dcfa26ed2";
        private const string GripperMaterialPath = "Assets/Materials/MAT_VirtualGripper_URP.mat";

        private static readonly Color PanelColor = new Color(0.075f, 0.018f, 0.028f, 0.92f);
        private static readonly Color AccentColor = new Color(0.95f, 0.19f, 0.27f, 1f);
        private static readonly Color SoftAccentColor = new Color(1f, 0.48f, 0.52f, 1f);

        [MenuItem("Navegacao Renal/Construir Marco 4")]
        public static void Configure()
        {
            Debug.Log("[Marco4] Construindo gameplay completo e garra virtual.");
            Marco3ProjectSetup.Configure();
            ConfigureGameScene();
            ConfigureMenuScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Regression gates: the prior milestones must still pass on the Marco 4 scene.
            Marco2ProjectSetup.Validate();
            Marco3ProjectSetup.Validate();
            Validate();
            CapturePreviews();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Marco4] Gameplay, regressao e previews concluidos.");
        }

        [MenuItem("Navegacao Renal/Validar Marco 4")]
        public static void Validate()
        {
            List<string> checks = new List<string>();
            List<string> errors = new List<string>();
            int legacyChecks = ReadLegacyCheckCount();

            Scene menuScene = EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);
            MainMenuPresenter menu = FindSceneComponent<MainMenuPresenter>(menuScene, "MainMenu");
            Check(menu != null, "menu principal presente", checks, errors);
            Check(menu != null && menu.RealisticButton != null && menu.ExplorationButton != null,
                "menu possui entradas separadas para Realista e Exploracao", checks, errors);
            Check(FindAllComponents<Canvas>(menuScene).Length == 1, "menu usa uma interface Canvas", checks, errors);
            Check(CountMissingScripts(menuScene) == 0, "menu sem scripts ausentes", checks, errors);

            Scene scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            KidneyGameManager manager = FindSceneComponent<KidneyGameManager>(scene, "KidneyGameManager");
            MouseEndoscopeController controller = FindSceneComponent<MouseEndoscopeController>(scene, "ProbeTip");
            MouseKeyboardInputSource input = FindSceneComponent<MouseKeyboardInputSource>(scene, "KidneyGameManager");
            VirtualGripperController gripper = FindSceneComponent<VirtualGripperController>(scene, "VirtualGripper");
            KidneyGameUI ui = FindSceneComponent<KidneyGameUI>(scene, "GameplayCanvas");
            KidneyAudioFeedback audio = FindSceneComponent<KidneyAudioFeedback>(scene, "KidneyGameManager");
            Transform probe = FindSceneTransform(scene, "ProbeTip");
            Transform stone = FindSceneTransformUnder(scene, "KidneyLevel_Active", "Stone");

            Check(manager != null, "KidneyGameManager do Marco 4 presente", checks, errors);
            Check(controller != null, "controlador SphereCast preservado", checks, errors);
            Check(input != null && input is IEndoscopeInputSource, "MouseKeyboardInputSource implementa IEndoscopeInputSource", checks, errors);
            Check(controller != null && controller.InputSourceBehaviour == input, "controlador consome a fonte de entrada abstrata", checks, errors);
            Check(manager != null && manager.InputSourceBehaviour == input, "gameplay consome a mesma fonte de entrada", checks, errors);
            Check(gripper != null && gripper.IsConfigured, "garra virtual configurada com duas mandibulas e ancora", checks, errors);
            Check(ui != null && ui.IsConfigured, "HUD Canvas configurado", checks, errors);
            Check(audio != null && audio.UsesProceduralOriginalAudio, "feedback usa sons procedurais originais", checks, errors);
            Check(CountMissingScripts(scene) == 0, "cena de jogo sem scripts ausentes", checks, errors);

            if (manager != null)
            {
                Check(manager.MaximumWallContacts == 5, "nivel facil encerra no quinto contato", checks, errors);
                Check(Approximately(manager.CaptureDistance, 0.018f), "captura limitada a 0,018 m entre as mandibulas", checks, errors);
                Check(Approximately(manager.CaptureHoldDuration, 1f), "captura exige 1 segundo continuo", checks, errors);
                Check(manager.RouteVisible && manager.MinimapVisible, "rota e minimapa iniciam ligados", checks, errors);

                manager.SetMode(KidneyGameMode.Realistic, true);
                manager.PrepareAttempt();
                Check(manager.SessionState == KidneySessionState.Ready && !manager.CanNavigate,
                    "Ready bloqueia movimento e cronometro", checks, errors);
                manager.BeginAttempt();
                Check(manager.SessionState == KidneySessionState.Playing && manager.CanNavigate,
                    "Iniciar libera a tentativa Realista", checks, errors);
                manager.SetPaused(true);
                Check(manager.SessionState == KidneySessionState.Paused && !manager.CanNavigate,
                    "Pausa bloqueia navegacao e captura", checks, errors);
                manager.ResumeAttempt();
                Check(manager.SessionState == KidneySessionState.Playing && manager.CanNavigate,
                    "Continuar restaura a tentativa", checks, errors);

                for (int index = 0; index < 4; index++) manager.ReportWallContact(Vector3.zero);
                Check(manager.WallContacts == 4 && manager.SessionState == KidneySessionState.Playing,
                    "quatro contatos ainda permitem jogar", checks, errors);
                manager.ReportWallContact(Vector3.zero);
                Check(manager.WallContacts == 5 && manager.SessionState == KidneySessionState.Lost && !manager.CanNavigate,
                    "quinto contato encerra com derrota", checks, errors);

                manager.ResetAttempt();
                Check(manager.SessionState == KidneySessionState.Ready && manager.WallContacts == 0 &&
                      Approximately(manager.ElapsedTime, 0f), "reinicio limpa estado, contatos e tempo", checks, errors);

                if (probe != null && stone != null)
                {
                    manager.BeginAttempt();
                    stone.position = gripper.CaptureAnchor.position;
                    Physics.SyncTransforms();
                    manager.ProcessCapture(0.5f, true);
                    Check(manager.CaptureProgress01 > 0.49f && manager.CaptureProgress01 < 0.51f,
                        "captura sustentada progride proporcionalmente", checks, errors);
                    manager.ProcessCapture(0f, false);
                    Check(Approximately(manager.CaptureProgress01, 0f) && gripper != null && Approximately(gripper.Closure, 0f),
                        "soltar Espaco cancela progresso e reabre a garra", checks, errors);
                    manager.ProcessCapture(1.01f, true);
                    Check(manager.SessionState == KidneySessionState.Won && manager.HasCapturedStone && !manager.CanNavigate,
                        "um segundo captura, anexa a pedra e vence", checks, errors);
                }
                else Check(false, "ponta e pedra disponiveis para teste de captura", checks, errors);

                manager.ResetAttempt();
                Check(!manager.HasCapturedStone && stone != null,
                    "reinicio devolve a pedra ao sistema coletor", checks, errors);

                manager.SetRouteVisible(true);
                manager.ToggleRoute();
                Check(!manager.RouteVisible, "T alterna a rota", checks, errors);
                manager.SetMinimapVisible(true);
                manager.ToggleMinimap();
                Check(!manager.MinimapVisible, "M alterna o minimapa", checks, errors);
                manager.SetRouteVisible(true);
                manager.SetMinimapVisible(true);

                int contactsBeforeExploration = manager.WallContacts;
                manager.SetMode(KidneyGameMode.Exploration, true);
                manager.ReportWallContact(Vector3.zero);
                manager.ProcessCapture(2f, true);
                Check(manager.CurrentMode == KidneyGameMode.Exploration && !manager.CanNavigate &&
                      manager.WallContacts == contactsBeforeExploration && manager.CaptureProgress01 == 0f,
                    "Exploracao nao conta contatos, captura ou resultado", checks, errors);
                manager.SetMode(KidneyGameMode.Realistic, true);
                manager.PrepareAttempt();
            }

            string managerSource = File.ReadAllText(ToAbsolute("Assets/Scripts/Runtime/KidneyGameManager.cs"));
            Check(!managerSource.Contains("f1Key") && !managerSource.Contains("f2Key"),
                "troca F1/F2 removida da partida", checks, errors);
            Check(!managerSource.Contains("OnGUI("), "OnGUI provisoria removida do gameplay", checks, errors);
            string menuSource = File.ReadAllText(ToAbsolute("Assets/Scripts/Runtime/MainMenuPresenter.cs"));
            Check(!menuSource.Contains("OnGUI("), "OnGUI provisoria removida do menu", checks, errors);

            string modelHash = Sha256(ToAbsolute(ModelPath));
            string legacyHash = Sha256(ToAbsolute(LegacyModelPath));
            string meshyHash = Sha256(ToAbsolute(MeshyModelPath));
            Check(modelHash == ExpectedModelHash, "FBX renal v003 permaneceu inalterado", checks, errors);
            Check(legacyHash == ExpectedLegacyHash, "FBX renal v002 permaneceu inalterado", checks, errors);
            Check(meshyHash == ExpectedMeshyHash, "FBX visual Meshy permaneceu inalterado", checks, errors);
            Check(legacyChecks == 65, $"65 validacoes anteriores reexecutadas ({legacyChecks})", checks, errors);

            ValidationReport report = new ValidationReport
            {
                milestone = "Marco 4",
                unityVersion = Application.unityVersion,
                generatedUtc = DateTime.UtcNow.ToString("O"),
                passed = errors.Count == 0,
                legacyChecks = legacyChecks,
                marco4Checks = checks.Count,
                totalChecks = legacyChecks + checks.Count,
                fbxV003Sha256 = modelHash,
                fbxV002Sha256 = legacyHash,
                meshyFbxSha256 = meshyHash,
                checks = checks.ToArray(),
                errors = errors.ToArray()
            };

            string reportPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../Documentation/marco4_validation.json"));
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
            File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
            Debug.Log($"[Marco4] Relatorio: {reportPath}\n{JsonUtility.ToJson(report, true)}");
            if (errors.Count > 0)
                throw new InvalidOperationException("Marco 4 falhou: " + string.Join(" | ", errors));
        }

        private static void ConfigureGameScene()
        {
            Scene scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            KidneyGameManager manager = FindSceneComponent<KidneyGameManager>(scene, "KidneyGameManager");
            MouseEndoscopeController controller = FindSceneComponent<MouseEndoscopeController>(scene, "ProbeTip");
            Transform probe = FindSceneTransform(scene, "ProbeTip");
            if (manager == null || controller == null || probe == null)
                throw new InvalidOperationException("Base do Marco 3 ausente na cena.");

            DestroySceneObject(scene, "GameplayCanvas");
            DestroySceneObject(scene, "GameplayEventSystem");
            Transform oldGripper = FindSceneTransform(scene, "VirtualGripper");
            if (oldGripper != null) UnityEngine.Object.DestroyImmediate(oldGripper.gameObject);

            MouseKeyboardInputSource input = manager.GetComponent<MouseKeyboardInputSource>();
            if (input == null) input = manager.gameObject.AddComponent<MouseKeyboardInputSource>();
            KidneyAudioFeedback audio = manager.GetComponent<KidneyAudioFeedback>();
            if (audio == null) audio = manager.gameObject.AddComponent<KidneyAudioFeedback>();
            AudioSource audioSource = manager.GetComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;

            VirtualGripperController gripper = CreateVirtualGripper(probe);
            KidneyGameUI ui = CreateGameplayUI(manager);
            controller.ConfigureInputSource(input);
            controller.ConfigureGripper(gripper);
            manager.ConfigureGameplay(input, gripper, ui, audio);
            manager.SetRouteVisible(true);
            manager.SetMinimapVisible(true);
            manager.PrepareAttempt();

            EditorUtility.SetDirty(controller);
            EditorUtility.SetDirty(manager);
            EditorUtility.SetDirty(gripper);
            EditorUtility.SetDirty(ui);
            EditorSceneManager.SaveScene(scene, GameScenePath);
        }

        private static void ConfigureMenuScene()
        {
            Scene scene = EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);
            MainMenuPresenter presenter = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<MainMenuPresenter>(true)).FirstOrDefault();
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
                TextAnchor.MiddleCenter, new Vector2(760f, 72f), new Vector2(0f, 220f), Color.white);
            CreateText("Subtitle", canvas.transform, "Simulador de ureteroscopia • nível fácil", 21, FontStyle.Normal,
                TextAnchor.MiddleCenter, new Vector2(720f, 44f), new Vector2(0f, 162f), SoftAccentColor);
            Button realistic = CreateButton("RealisticButton", canvas.transform, "INICIAR MODO REALISTA",
                new Vector2(410f, 64f), new Vector2(0f, 55f));
            Button exploration = CreateButton("ExplorationButton", canvas.transform, "ABRIR MODO EXPLORAÇÃO",
                new Vector2(410f, 64f), new Vector2(0f, -30f));
            CreateText("Description", canvas.transform,
                "Realista: permaneça dentro do rim, evite 5 contatos e capture a pedra.\nExploração: navegue livremente por dentro e por fora do sistema urinário.",
                17, FontStyle.Normal, TextAnchor.MiddleCenter, new Vector2(780f, 86f), new Vector2(0f, -135f),
                new Color(0.88f, 0.80f, 0.82f, 1f));
            CreateText("Version", canvas.transform, "Marco 4 • mouse e teclado", 14, FontStyle.Normal,
                TextAnchor.MiddleCenter, new Vector2(500f, 30f), new Vector2(0f, -300f), new Color(0.62f, 0.52f, 0.55f, 1f));

            presenter.Configure(realistic, exploration);
            EditorUtility.SetDirty(presenter);
            EditorSceneManager.SaveScene(scene, MenuScenePath);
        }

        private static VirtualGripperController CreateVirtualGripper(Transform probe)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(GripperMaterialPath);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                material = new Material(shader) { name = "MAT_VirtualGripper_URP" };
                AssetDatabase.CreateAsset(material, GripperMaterialPath);
            }
            Color steel = new Color(0.10f, 0.16f, 0.20f, 1f);
            material.color = steel;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", steel);
            material.SetFloat("_Smoothness", 0.72f);
            material.SetFloat("_Metallic", 0.80f);
            EditorUtility.SetDirty(material);

            GameObject root = new GameObject("VirtualGripper");
            root.transform.SetParent(probe, false);
            root.transform.localPosition = new Vector3(0f, -0.026f, 0.055f);
            VirtualGripperController gripper = root.AddComponent<VirtualGripperController>();

            Transform leftPivot = new GameObject("LeftJawPivot").transform;
            leftPivot.SetParent(root.transform, false);
            leftPivot.localPosition = new Vector3(-0.003f, 0f, 0.010f);
            Transform rightPivot = new GameObject("RightJawPivot").transform;
            rightPivot.SetParent(root.transform, false);
            rightPivot.localPosition = new Vector3(0.003f, 0f, 0.010f);
            Transform leftJaw = CreateJaw("LeftJaw", leftPivot, material, -0.002f);
            Transform rightJaw = CreateJaw("RightJaw", rightPivot, material, 0.002f);

            GameObject shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            shaft.name = "GripperShaft";
            shaft.transform.SetParent(root.transform, false);
            shaft.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            shaft.transform.localPosition = new Vector3(0f, 0f, -0.006f);
            shaft.transform.localScale = new Vector3(0.0015f, 0.016f, 0.0015f);
            shaft.GetComponent<Renderer>().sharedMaterial = material;
            UnityEngine.Object.DestroyImmediate(shaft.GetComponent<Collider>());

            Transform captureAnchor = new GameObject("StoneCaptureAnchor").transform;
            captureAnchor.SetParent(root.transform, false);
            captureAnchor.localPosition = new Vector3(0f, 0f, 0.052f);
            gripper.Configure(leftPivot, rightPivot, captureAnchor, leftJaw, rightJaw, shaft.transform, 0.018f);
            return gripper;
        }

        private static Transform CreateJaw(string name, Transform pivot, Material material, float x)
        {
            GameObject jaw = GameObject.CreatePrimitive(PrimitiveType.Cube);
            jaw.name = name;
            jaw.transform.SetParent(pivot, false);
            jaw.transform.localPosition = new Vector3(x, 0f, 0.020f);
            jaw.transform.localScale = new Vector3(0.0022f, 0.0022f, 0.036f);
            jaw.GetComponent<Renderer>().sharedMaterial = material;
            UnityEngine.Object.DestroyImmediate(jaw.GetComponent<Collider>());
            return jaw.transform;
        }

        private static KidneyGameUI CreateGameplayUI(KidneyGameManager manager)
        {
            CreateEventSystem("GameplayEventSystem");
            Canvas canvas = CreateCanvas("GameplayCanvas", 20);
            KidneyGameUI ui = canvas.gameObject.AddComponent<KidneyGameUI>();

            Image flash = CreateImage("WallFlash", canvas.transform, new Color(1f, 0.02f, 0.03f, 0f));
            Stretch(flash.rectTransform);
            flash.raycastTarget = false;

            GameObject hud = CreatePanel("HUD", canvas.transform, new Vector2(390f, 128f), new Vector2(215f, -84f),
                new Vector2(0f, 1f), PanelColor);
            Text timer = CreateText("Timer", hud.transform, "Tempo  0.0s", 20, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(330f, 36f), new Vector2(0f, 28f), Color.white);
            Text contacts = CreateText("Contacts", hud.transform, "Contatos  0/5", 20, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(330f, 36f), new Vector2(0f, -12f), SoftAccentColor);
            CreateText("Help", hud.transform, "T rota  •  M minimapa  •  P pausa", 14, FontStyle.Normal, TextAnchor.MiddleLeft,
                new Vector2(340f, 28f), new Vector2(0f, -48f), new Color(0.82f, 0.75f, 0.77f, 1f));

            GameObject ready = CreatePanel("ReadyPanel", canvas.transform, new Vector2(620f, 410f), Vector2.zero,
                new Vector2(0.5f, 0.5f), PanelColor);
            CreateText("ReadyTitle", ready.transform, "MODO REALISTA", 34, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(540f, 52f), new Vector2(0f, 142f), Color.white);
            CreateText("ReadyObjective", ready.transform,
                "Navegue pelo caminho iluminado e capture a pedra.\nA parede bloqueia a ponta. Ao atingir 5 contatos, a tentativa termina.",
                19, FontStyle.Normal, TextAnchor.MiddleCenter, new Vector2(540f, 92f), new Vector2(0f, 68f),
                new Color(0.92f, 0.84f, 0.86f, 1f));
            CreateText("ReadyControls", ready.transform,
                "Mouse: orientar   W/S: avançar/recuar   Q/E: rolar\nSegure Espaço por 1 segundo perto da pedra",
                16, FontStyle.Normal, TextAnchor.MiddleCenter, new Vector2(540f, 72f), new Vector2(0f, -28f), SoftAccentColor);
            Button start = CreateButton("StartButton", ready.transform, "INICIAR TENTATIVA", new Vector2(320f, 58f), new Vector2(0f, -132f));

            GameObject pause = CreatePanel("PausePanel", canvas.transform, new Vector2(500f, 390f), Vector2.zero,
                new Vector2(0.5f, 0.5f), PanelColor);
            CreateText("PauseTitle", pause.transform, "PAUSADO", 34, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(420f, 54f), new Vector2(0f, 125f), Color.white);
            Button resume = CreateButton("ResumeButton", pause.transform, "CONTINUAR", new Vector2(300f, 54f), new Vector2(0f, 45f));
            Button restartPause = CreateButton("RestartPauseButton", pause.transform, "REINICIAR", new Vector2(300f, 54f), new Vector2(0f, -25f));
            Button menuPause = CreateButton("MenuPauseButton", pause.transform, "VOLTAR AO MENU", new Vector2(300f, 54f), new Vector2(0f, -95f));

            GameObject result = CreatePanel("ResultPanel", canvas.transform, new Vector2(540f, 410f), Vector2.zero,
                new Vector2(0.5f, 0.5f), PanelColor);
            Text resultTitle = CreateText("ResultTitle", result.transform, "PEDRA CAPTURADA", 32, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(470f, 58f), new Vector2(0f, 132f), Color.white);
            Text resultSummary = CreateText("ResultSummary", result.transform, "Tempo: 0.0s\nContatos: 0/5", 21,
                FontStyle.Normal, TextAnchor.MiddleCenter, new Vector2(420f, 78f), new Vector2(0f, 55f), SoftAccentColor);
            Button restartResult = CreateButton("RestartResultButton", result.transform, "NOVA TENTATIVA", new Vector2(310f, 54f), new Vector2(0f, -38f));
            Button menuResult = CreateButton("MenuResultButton", result.transform, "VOLTAR AO MENU", new Vector2(310f, 54f), new Vector2(0f, -108f));

            GameObject exploration = CreatePanel("ExplorationPanel", canvas.transform, new Vector2(470f, 128f),
                new Vector2(255f, -84f), new Vector2(0f, 1f), PanelColor);
            CreateText("ExplorationTitle", exploration.transform, "EXPLORAÇÃO LIVRE", 22, FontStyle.Bold,
                TextAnchor.MiddleLeft, new Vector2(360f, 34f), new Vector2(-24f, 28f), Color.white);
            CreateText("ExplorationHelp", exploration.transform, "Botão direito + mouse • WASD/QE • Shift", 14,
                FontStyle.Normal, TextAnchor.MiddleLeft, new Vector2(390f, 28f), new Vector2(-9f, -9f), SoftAccentColor);
            Button menuExploration = CreateButton("MenuExplorationButton", exploration.transform, "MENU", new Vector2(110f, 36f), new Vector2(160f, 31f));

            GameObject capture = CreatePanel("CapturePanel", canvas.transform, new Vector2(470f, 104f),
                new Vector2(0f, 78f), new Vector2(0.5f, 0f), PanelColor);
            Text capturePrompt = CreateText("CapturePrompt", capture.transform, "Segure ESPAÇO para capturar", 18,
                FontStyle.Bold, TextAnchor.MiddleCenter, new Vector2(420f, 34f), new Vector2(0f, 22f), Color.white);
            Image track = CreateImage("CaptureTrack", capture.transform, new Color(0.22f, 0.08f, 0.10f, 1f));
            SetRect(track.rectTransform, new Vector2(390f, 15f), new Vector2(0f, -23f), new Vector2(0.5f, 0.5f));
            Image fill = CreateImage("CaptureFill", track.transform, AccentColor);
            Stretch(fill.rectTransform);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = 0;
            fill.fillAmount = 0f;

            ui.Configure(manager, ready, hud, pause, result, exploration, capture, timer, contacts, capturePrompt,
                resultTitle, resultSummary, fill, flash, start, resume, restartPause, restartResult, menuPause,
                menuResult, menuExploration);
            hud.SetActive(false);
            pause.SetActive(false);
            result.SetActive(false);
            exploration.SetActive(false);
            capture.SetActive(false);
            return ui;
        }

        private static void CapturePreviews()
        {
            Scene scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            KidneyGameManager manager = FindSceneComponent<KidneyGameManager>(scene, "KidneyGameManager");
            KidneyGameUI ui = FindSceneComponent<KidneyGameUI>(scene, "GameplayCanvas");
            Camera realCamera = FindSceneComponent<Camera>(scene, "EndoscopeCamera");
            Camera explorationCamera = FindSceneComponent<Camera>(scene, "ExplorationCamera");
            Transform probe = FindSceneTransform(scene, "ProbeTip");
            Transform stone = FindSceneTransformUnder(scene, "KidneyLevel_Active", "Stone");
            Canvas canvas = FindSceneComponent<Canvas>(scene, "GameplayCanvas");
            string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Documentation/Previews"));
            Directory.CreateDirectory(directory);

            manager.SetMode(KidneyGameMode.Realistic, true);
            manager.PrepareAttempt();
            ui.RefreshImmediate();
            CaptureCamera(realCamera, canvas, Path.Combine(directory, "marco4_ready.png"));

            manager.BeginAttempt();
            manager.ReportWallContact(Vector3.zero);
            ui.RefreshImmediate();
            CaptureCamera(realCamera, canvas, Path.Combine(directory, "marco4_wall_contact.png"));

            manager.ResetAttempt();
            manager.BeginAttempt();
            if (probe != null && stone != null) probe.position = stone.position;
            manager.ProcessCapture(0.55f, true);
            ui.RefreshImmediate();
            CaptureCamera(realCamera, canvas, Path.Combine(directory, "marco4_capture_progress.png"));
            manager.ProcessCapture(0.50f, true);
            ui.RefreshImmediate();
            CaptureCamera(realCamera, canvas, Path.Combine(directory, "marco4_victory.png"));

            manager.ResetAttempt();
            manager.BeginAttempt();
            for (int index = 0; index < 5; index++) manager.ReportWallContact(Vector3.zero);
            ui.RefreshImmediate();
            CaptureCamera(realCamera, canvas, Path.Combine(directory, "marco4_defeat.png"));

            manager.ResetAttempt();
            manager.SetMode(KidneyGameMode.Exploration, true);
            ui.RefreshImmediate();
            CaptureCamera(explorationCamera, canvas, Path.Combine(directory, "marco4_exploration.png"));

            manager.SetMode(KidneyGameMode.Realistic, true);
            manager.PrepareAttempt();
            ui.RefreshImmediate();
        }

        private static void CaptureCamera(Camera camera, Canvas canvas, string outputPath)
        {
            if (camera == null || canvas == null)
                throw new InvalidOperationException("Camera ou Canvas ausente para preview do Marco 4.");

            RenderMode savedMode = canvas.renderMode;
            Camera savedWorldCamera = canvas.worldCamera;
            float savedPlaneDistance = canvas.planeDistance;
            RenderTexture previousTarget = camera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            bool cameraWasActive = camera.gameObject.activeSelf;
            RenderTexture target = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            Texture2D image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            try
            {
                camera.gameObject.SetActive(true);
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
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(image);
            }
        }

        private static Canvas CreateCanvas(string name, int sortingOrder)
        {
            GameObject root = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        private static void CreateEventSystem(string name)
        {
            GameObject eventSystem = new GameObject(name, typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventSystem.GetComponent<EventSystem>().sendNavigationEvents = true;
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
            CreateText("Label", image.transform, label, 17, FontStyle.Bold, TextAnchor.MiddleCenter, size,
                Vector2.zero, Color.white);
            return button;
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
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../Documentation/marco32_validation.json"));
            if (!File.Exists(path)) return 0;
            LegacyValidationReport report = JsonUtility.FromJson<LegacyValidationReport>(File.ReadAllText(path));
            return report != null && report.checks != null ? report.checks.Length : 0;
        }

        private static void DestroySceneObject(Scene scene, string objectName)
        {
            GameObject target = FindSceneObject(scene, objectName);
            if (target != null) UnityEngine.Object.DestroyImmediate(target);
        }

        private static int CountMissingScripts(Scene scene) => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .Sum(transform => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject));

        private static T[] FindAllComponents<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();

        private static T FindSceneComponent<T>(Scene scene, string objectName) where T : Component
        {
            GameObject target = FindSceneObject(scene, objectName);
            return target != null ? target.GetComponent<T>() : null;
        }

        private static GameObject FindSceneObject(Scene scene, string objectName) => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .FirstOrDefault(transform => transform.name == objectName)?.gameObject;

        private static Transform FindSceneTransform(Scene scene, string objectName) => FindSceneObject(scene, objectName)?.transform;

        private static Transform FindSceneTransformUnder(Scene scene, string parentName, string childName)
        {
            Transform parent = FindSceneTransform(scene, parentName);
            return parent != null ? parent.GetComponentsInChildren<Transform>(true).FirstOrDefault(item => item.name == childName) : null;
        }

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
        private sealed class LegacyValidationReport
        {
            public string[] checks;
        }

        [Serializable]
        private sealed class ValidationReport
        {
            public string milestone;
            public string unityVersion;
            public string generatedUtc;
            public bool passed;
            public int legacyChecks;
            public int marco4Checks;
            public int totalChecks;
            public string fbxV003Sha256;
            public string fbxV002Sha256;
            public string meshyFbxSha256;
            public string[] checks;
            public string[] errors;
        }
    }
}
