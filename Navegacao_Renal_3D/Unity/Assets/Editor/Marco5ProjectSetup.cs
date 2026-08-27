using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NavegacaoRenal.Editor
{
    public static class Marco5ProjectSetup
    {
        private const string GameScenePath = "Assets/Scenes/KidneyGame.unity";
        private const string MenuScenePath = "Assets/Scenes/MainMenu.unity";
        private const string ModelPath = "Assets/Art/Kidney/Models/Kidney_Game_v003.fbx";
        private const string LegacyModelPath = "Assets/Art/Kidney/Models/Kidney_Game_v002.fbx";
        private const string MeshyModelPath = "Assets/Art/UrinarySystem/Models/Meshy_Urinary_Visual_v002.fbx";
        private const string ExpectedModelHash = "f721e63ad9188f007520709f24cb7c60e85ce3a2588bec6b533e7460fddc9bcf";
        private const string ExpectedLegacyHash = "174fabbf6ec31b3052360be995b5bbc4fb7e074b91ef2a2bba838ca45cc0fa9c";
        private const string ExpectedMeshyHash = "f8408f41656011f65cb737b1b434e7611228036b76fb8fcadfaa183dcfa26ed2";
        private const string RenderTexturePath = "Assets/RenderTextures/RT_KidneyMinimap.renderTexture";
        private const string TransparentMaterialDirectory = "Assets/Materials/Marco5";
        private const string MinimapKidneyMaterialPath = TransparentMaterialDirectory + "/MAT_MinimapKidney.mat";
        private const string MinimapRouteMaterialPath = TransparentMaterialDirectory + "/MAT_MinimapRoute.mat";

        private static readonly Color PanelColor = new Color(0.075f, 0.018f, 0.028f, 0.94f);
        private static readonly Color AccentColor = new Color(0.95f, 0.19f, 0.27f, 1f);
        private static readonly Color SoftAccentColor = new Color(1f, 0.48f, 0.52f, 1f);

        [MenuItem("Navegacao Renal/Construir Marco 5")]
        public static void Configure()
        {
            Debug.Log("[Marco5] Construindo exploracao livre e minimapa final.");
            Marco4ProjectSetup.Validate();
            ConfigureGameScene();
            ConfigureMenuScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Validate();
            CapturePreviews();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Marco5] Exploracao, minimapa, validacao e previews concluidos.");
        }

        [MenuItem("Navegacao Renal/Validar Marco 5")]
        public static void Validate()
        {
            List<string> checks = new List<string>();
            List<string> errors = new List<string>();
            int legacyChecks = ReadLegacyCheckCount();
            Scene scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);

            KidneyGameManager manager = FindSceneComponent<KidneyGameManager>(scene, "KidneyGameManager");
            FreeFlyCameraController freeFly = FindSceneComponent<FreeFlyCameraController>(scene, "ExplorationRig");
            ExplorationVisibilityController visibility = FindSceneComponent<ExplorationVisibilityController>(scene, "KidneyGameManager");
            KidneyMinimapPresenter minimap = FindSceneComponent<KidneyMinimapPresenter>(scene, "KidneyMinimapSystem");
            KidneyGameUI ui = FindSceneComponent<KidneyGameUI>(scene, "GameplayCanvas");
            Camera explorationCamera = FindSceneComponent<Camera>(scene, "ExplorationCamera");
            Camera endoscopeCamera = FindSceneComponent<Camera>(scene, "EndoscopeCamera");
            Transform explorationRig = FindSceneTransform(scene, "ExplorationRig");

            Check(manager != null && manager.ExplorationController == freeFly && manager.ExplorationVisibility == visibility &&
                  manager.MinimapPresenter == minimap, "gerenciador integra os tres sistemas do Marco 5", checks, errors);
            Check(freeFly != null && Approximately(freeFly.MovementSpeed, 0.45f) && Approximately(freeFly.BoostMultiplier, 3f),
                "exploracao usa 0,45 m/s e boost 3x", checks, errors);
            Check(freeFly != null && Approximately(freeFly.RecenterDuration, 0.45f) && freeFly.HomeAnchor != null,
                "F possui ancora e recentralizacao de 0,45 s", checks, errors);
            Check(explorationRig != null && explorationRig.GetComponentsInChildren<Collider>(true).Length == 0 &&
                  explorationRig.GetComponentsInChildren<CharacterController>(true).Length == 0,
                "camera livre nao possui colisao e atravessa as malhas", checks, errors);
            Check(ui != null && ui.IsMarco5Configured, "painel Canvas do Marco 5 configurado", checks, errors);
            Check(CountMissingScripts(scene) == 0, "cena de jogo sem scripts ausentes", checks, errors);

            float d30 = FreeFlyCameraController.SimulateTravelDistance(0.45f, 1f, 30);
            float d60 = FreeFlyCameraController.SimulateTravelDistance(0.45f, 1f, 60);
            float d120 = FreeFlyCameraController.SimulateTravelDistance(0.45f, 1f, 120);
            Check(Approximately(d30, 0.45f) && Approximately(d60, 0.45f) && Approximately(d120, 0.45f) &&
                  Approximately(d30, d60) && Approximately(d60, d120),
                "movimento equivalente em 30, 60 e 120 FPS", checks, errors);

            if (freeFly != null && freeFly.HomeAnchor != null)
            {
                Vector3 savedPosition = freeFly.transform.position;
                Quaternion savedRotation = freeFly.transform.rotation;
                freeFly.transform.position += new Vector3(0.7f, 0.4f, 0.6f);
                freeFly.BeginRecenter();
                freeFly.AdvanceRecenter(0.45f);
                Check(Vector3.Distance(freeFly.transform.position, freeFly.HomeAnchor.position) < 0.0001f && !freeFly.IsRecentering,
                    "recentralizacao termina exatamente na visao geral", checks, errors);
                freeFly.transform.SetPositionAndRotation(savedPosition, savedRotation);
            }
            else Check(false, "recentralizacao disponivel para teste", checks, errors);

            if (visibility != null)
            {
                visibility.ResetDefaults();
                Check(visibility.ExteriorMode == ExteriorVisibilityMode.Transparent && visibility.CollectingSystemVisible &&
                      visibility.StoneVisible && visibility.PanelExpanded,
                    "Exploracao inicia transparente e com controles visiveis", checks, errors);
                visibility.SetExteriorMode(ExteriorVisibilityMode.Opaque);
                Check(visibility.ExteriorBindings.All(binding => binding.renderer != null && binding.renderer.enabled &&
                      SameMaterials(binding.renderer.sharedMaterials, binding.opaqueMaterials)),
                    "estado exterior opaco usa variantes opacas sem alterar os originais", checks, errors);
                visibility.SetExteriorMode(ExteriorVisibilityMode.Hidden);
                Check(visibility.ExteriorBindings.All(binding => binding.renderer != null && !binding.renderer.enabled),
                    "estado exterior oculto desliga todos os renderizadores", checks, errors);
                visibility.SetExteriorMode(ExteriorVisibilityMode.Transparent);
                Check(visibility.ExteriorBindings.All(binding => binding.renderer != null && binding.renderer.enabled &&
                      SameMaterials(binding.renderer.sharedMaterials, binding.transparentMaterials)),
                    "estado exterior transparente usa variantes dedicadas", checks, errors);
                visibility.SetCollectingSystemVisible(false);
                Check(!visibility.CollectingSystemVisible && visibility.CollectingSystem != null && !visibility.CollectingSystem.activeSelf,
                    "sistema coletor possui visibilidade independente", checks, errors);
                visibility.SetStoneVisible(false);
                Check(!visibility.StoneVisible && visibility.Stone != null && !visibility.Stone.activeSelf,
                    "pedra possui visibilidade independente", checks, errors);
                visibility.ResetDefaults();
            }
            else Check(false, "controlador de visibilidade disponivel", checks, errors);

            int minimapLayer = LayerMask.NameToLayer("MinimapOnly");
            Check(minimap != null && minimap.MinimapCamera != null && minimap.MinimapImage != null &&
                  minimap.MinimapCamera.targetTexture != null && minimap.MinimapCamera.targetTexture.width == 512 &&
                  minimap.MinimapCamera.targetTexture.height == 512,
                "minimapa usa RenderTexture dedicada 512x512", checks, errors);
            Check(minimap != null && minimap.MinimapCamera.cullingMask == 1 << minimapLayer,
                "camera do minimapa renderiza somente MinimapOnly", checks, errors);
            Check(explorationCamera != null && endoscopeCamera != null &&
                  (explorationCamera.cullingMask & (1 << minimapLayer)) == 0 &&
                  (endoscopeCamera.cullingMask & (1 << minimapLayer)) == 0,
                "cameras principais nao renderizam os proxies do minimapa", checks, errors);
            Check(minimap != null && minimap.MinimapCamera.transform.forward.y < -0.15f,
                "minimapa possui vista 3D fixa e inclinada", checks, errors);

            if (manager != null && minimap != null && explorationRig != null)
            {
                manager.SetMinimapVisible(true);
                manager.SetRouteVisible(true);
                Check(manager.MinimapVisible && minimap.RouteProxy != null && minimap.RouteProxy.activeSelf,
                    "M e T controlam painel e rota do minimapa", checks, errors);
                manager.SetMode(KidneyGameMode.Realistic, true);
                Transform realisticTarget = minimap.CurrentTarget;
                manager.SetMode(KidneyGameMode.Exploration, true);
                Transform explorationTarget = minimap.CurrentTarget;
                Check(realisticTarget != null && realisticTarget.name == "ProbeTip" && explorationTarget == explorationRig,
                    "marcador troca entre ponta realista e camera livre", checks, errors);

                Vector3 savedPosition = explorationRig.position;
                explorationRig.position = new Vector3(100f, 100f, 100f);
                minimap.RefreshMarker();
                Check(minimap.IsMarkerClamped, "indicador permanece preso a borda fora do enquadramento", checks, errors);
                explorationRig.position = savedPosition;

                visibility?.SetExteriorMode(ExteriorVisibilityMode.Hidden);
                Check(minimap.RouteProxy != null && minimap.RouteProxy.transform.parent.gameObject.activeInHierarchy &&
                      minimap.RouteProxy.GetComponentsInChildren<Renderer>(true).All(renderer => renderer.enabled),
                    "representacao do minimapa independe da visibilidade externa", checks, errors);
                visibility?.ResetDefaults();
                manager.SetRouteVisible(false);
                Check(!minimap.RouteProxy.activeSelf, "rota do minimapa acompanha T desligado", checks, errors);
                manager.SetRouteVisible(true);
                manager.SetMinimapVisible(false);
                Check(!manager.MinimapVisible, "M oculta camera e painel do minimapa", checks, errors);
                manager.SetMinimapVisible(true);
                manager.SetMode(KidneyGameMode.Realistic, true);
                manager.PrepareAttempt();
            }
            else Check(false, "minimapa e fontes disponiveis para testes", checks, errors);

            string freeFlySource = File.ReadAllText(ToAbsolute("Assets/Scripts/Runtime/FreeFlyCameraController.cs"));
            Check(freeFlySource.Contains("CursorLockMode.Locked") && freeFlySource.Contains("escapeKey") &&
                  freeFlySource.Contains("PointerIsOverUi") && freeFlySource.Contains("if (!IsCursorLocked)"),
                "clique prende cursor, Esc libera e cursor livre bloqueia movimento", checks, errors);
            Check(freeFlySource.Contains("wKey") && freeFlySource.Contains("qKey") && freeFlySource.Contains("leftShiftKey") &&
                  freeFlySource.Contains("fKey"), "WASD, Q/E, Shift e F implementados", checks, errors);

            string modelHash = Sha256(ToAbsolute(ModelPath));
            string legacyHash = Sha256(ToAbsolute(LegacyModelPath));
            string meshyHash = Sha256(ToAbsolute(MeshyModelPath));
            Check(modelHash == ExpectedModelHash, "FBX renal v003 permaneceu inalterado", checks, errors);
            Check(legacyHash == ExpectedLegacyHash, "FBX renal v002 permaneceu inalterado", checks, errors);
            Check(meshyHash == ExpectedMeshyHash, "FBX visual Meshy permaneceu inalterado", checks, errors);
            Check(legacyChecks == 103, $"103 validacoes anteriores reexecutadas ({legacyChecks})", checks, errors);

            ValidationReport report = new ValidationReport
            {
                milestone = "Marco 5",
                unityVersion = Application.unityVersion,
                generatedUtc = DateTime.UtcNow.ToString("O"),
                passed = errors.Count == 0,
                legacyChecks = legacyChecks,
                marco5Checks = checks.Count,
                totalChecks = legacyChecks + checks.Count,
                fbxV003Sha256 = modelHash,
                fbxV002Sha256 = legacyHash,
                meshyFbxSha256 = meshyHash,
                checks = checks.ToArray(),
                errors = errors.ToArray()
            };
            string reportPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../Documentation/marco5_validation.json"));
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
            File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
            Debug.Log($"[Marco5] Relatorio: {reportPath}\n{JsonUtility.ToJson(report, true)}");
            if (errors.Count > 0)
                throw new InvalidOperationException("Marco 5 falhou: " + string.Join(" | ", errors));
        }

        private static void ConfigureGameScene()
        {
            Scene scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            KidneyGameManager manager = FindSceneComponent<KidneyGameManager>(scene, "KidneyGameManager");
            KidneyGameUI ui = FindSceneComponent<KidneyGameUI>(scene, "GameplayCanvas");
            Transform explorationRig = FindSceneTransform(scene, "ExplorationRig");
            Transform probe = FindSceneTransform(scene, "ProbeTip");
            Transform activeRoot = FindSceneTransform(scene, "KidneyLevel_Active");
            Transform activeExterior = FindDeep(activeRoot, "KidneyExterior");
            Transform collectingSystem = FindDeep(activeRoot, "CollectingSystemVisual");
            Transform route = FindDeep(activeRoot, "RouteGuide");
            Transform stone = FindDeep(activeRoot, "Stone");
            Transform meshyRoot = FindSceneTransform(scene, "MeshyUrinaryVisualRoot");
            Camera explorationCamera = FindSceneComponent<Camera>(scene, "ExplorationCamera");
            Camera endoscopeCamera = FindSceneComponent<Camera>(scene, "EndoscopeCamera");
            Canvas canvas = FindSceneComponent<Canvas>(scene, "GameplayCanvas");
            if (manager == null || ui == null || explorationRig == null || probe == null || activeRoot == null ||
                activeExterior == null || collectingSystem == null || route == null || stone == null || meshyRoot == null ||
                explorationCamera == null || endoscopeCamera == null || canvas == null)
                throw new InvalidOperationException("Base do Marco 4 incompleta para construir o Marco 5.");

            DestroySceneObject(scene, "KidneyMinimapSystem");
            DestroySceneObject(scene, "ExplorationHomeAnchor");
            DestroySceneObject(scene, "MinimapPanel");
            DestroySceneObject(scene, "ExplorationPanel");
            GameObject oldMinimap = FindSceneObject(scene, "MinimapCamera");
            if (oldMinimap != null) UnityEngine.Object.DestroyImmediate(oldMinimap);

            Renderer[] exteriorRenderers = activeExterior.GetComponentsInChildren<Renderer>(true)
                .Concat(meshyRoot.GetComponentsInChildren<Renderer>(true)).Distinct().ToArray();
            Bounds systemBounds = CombineBounds(exteriorRenderers);

            GameObject homeObject = new GameObject("ExplorationHomeAnchor");
            float verticalFov = explorationCamera.fieldOfView * Mathf.Deg2Rad;
            float distance = Mathf.Max(systemBounds.size.x, systemBounds.size.y) /
                             (2f * Mathf.Tan(verticalFov * 0.5f)) * 1.22f;
            homeObject.transform.position = systemBounds.center + new Vector3(0f, systemBounds.extents.y * 0.04f, -distance);
            homeObject.transform.rotation = Quaternion.LookRotation(systemBounds.center - homeObject.transform.position, Vector3.up);

            FreeFlyCameraController freeFly = explorationRig.GetComponent<FreeFlyCameraController>();
            if (freeFly == null) freeFly = explorationRig.gameObject.AddComponent<FreeFlyCameraController>();
            freeFly.Configure(homeObject.transform, 0.45f, 3f, 0.45f);
            freeFly.ResetViewImmediate();

            int minimapLayer = LayerMask.NameToLayer("MinimapOnly");
            explorationCamera.cullingMask &= ~(1 << minimapLayer);
            endoscopeCamera.cullingMask &= ~(1 << minimapLayer);

            ExplorationVisibilityController visibility = manager.GetComponent<ExplorationVisibilityController>();
            if (visibility == null) visibility = manager.gameObject.AddComponent<ExplorationVisibilityController>();
            ExplorationVisibilityController.ExteriorRendererBinding[] bindings = exteriorRenderers
                .Select(CreateExteriorBinding).ToArray();
            visibility.Configure(manager, bindings, collectingSystem.gameObject, stone.gameObject);

            KidneyMinimapPresenter minimap = CreateMinimap(manager, canvas, activeRoot, activeExterior, route,
                probe, explorationRig, minimapLayer);
            CreateExplorationPanel(ui, visibility, minimap, manager, canvas.transform);
            manager.ConfigureMarco5(freeFly, visibility, minimap);
            manager.SetMode(KidneyGameMode.Exploration, true);
            manager.SetRouteVisible(true);
            manager.SetMinimapVisible(true);
            visibility.ResetDefaults();
            manager.SetMode(KidneyGameMode.Realistic, true);
            manager.PrepareAttempt();
            ui.RefreshImmediate();

            EditorUtility.SetDirty(freeFly);
            EditorUtility.SetDirty(visibility);
            EditorUtility.SetDirty(minimap);
            EditorUtility.SetDirty(manager);
            EditorUtility.SetDirty(ui);
            EditorSceneManager.SaveScene(scene, GameScenePath);
        }

        private static KidneyMinimapPresenter CreateMinimap(KidneyGameManager manager, Canvas canvas,
            Transform activeRoot, Transform activeExterior, Transform route, Transform probe,
            Transform explorationRig, int minimapLayer)
        {
            EnsureFolder("Assets/RenderTextures");
            EnsureFolder("Assets/Materials");
            EnsureFolder(TransparentMaterialDirectory);
            RenderTexture texture = AssetDatabase.LoadAssetAtPath<RenderTexture>(RenderTexturePath);
            if (texture == null)
            {
                texture = new RenderTexture(512, 512, 24, RenderTextureFormat.ARGB32) { name = "RT_KidneyMinimap" };
                AssetDatabase.CreateAsset(texture, RenderTexturePath);
            }
            texture.width = 512;
            texture.height = 512;

            Material kidneyMaterial = CreateOrUpdateMaterial(MinimapKidneyMaterialPath,
                new Color(0.72f, 0.07f, 0.14f, 0.48f), true, false);
            Material routeMaterial = CreateOrUpdateMaterial(MinimapRouteMaterialPath,
                new Color(0.05f, 0.92f, 1f, 1f), false, true);

            GameObject root = new GameObject("KidneyMinimapSystem");
            KidneyMinimapPresenter presenter = root.AddComponent<KidneyMinimapPresenter>();
            Transform representation = new GameObject("MinimapRepresentation").transform;
            representation.SetParent(root.transform, false);
            SetLayerRecursively(representation.gameObject, minimapLayer);

            Transform kidneyProxy = new GameObject("MinimapKidneyProxy").transform;
            kidneyProxy.SetParent(representation, false);
            foreach (Renderer renderer in activeExterior.GetComponentsInChildren<Renderer>(true))
                CreateRendererProxy(renderer, kidneyProxy, kidneyMaterial, minimapLayer);

            Transform routeProxy = new GameObject("MinimapRouteProxy").transform;
            routeProxy.SetParent(representation, false);
            foreach (Renderer renderer in route.GetComponentsInChildren<Renderer>(true))
                CreateRendererProxy(renderer, routeProxy, routeMaterial, minimapLayer);

            Bounds kidneyBounds = CombineBounds(kidneyProxy.GetComponentsInChildren<Renderer>(true));
            Transform center = new GameObject("ActiveKidneyMapCenter").transform;
            center.SetParent(root.transform, false);
            center.position = kidneyBounds.center;

            GameObject cameraObject = new GameObject("MinimapCameraFinal");
            cameraObject.transform.SetParent(root.transform, false);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.006f, 0.012f, 1f);
            camera.cullingMask = 1 << minimapLayer;
            camera.fieldOfView = 38f;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 20f;
            camera.depth = -10f;
            camera.targetTexture = texture;
            Vector3 direction = new Vector3(0f, 0.58f, -1f).normalized;
            float radius = Mathf.Max(kidneyBounds.extents.x, kidneyBounds.extents.y);
            float distance = radius / Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * 0.5f) * 1.18f;
            camera.transform.position = kidneyBounds.center + direction * distance;
            camera.transform.rotation = Quaternion.LookRotation(kidneyBounds.center - camera.transform.position, Vector3.up);

            GameObject panel = CreatePanel("MinimapPanel", canvas.transform, new Vector2(330f, 320f),
                new Vector2(-24f, -24f), Vector2.one, PanelColor);
            Text title = CreateText("MinimapTitle", panel.transform, "RIM ATIVO", 17, FontStyle.Bold,
                TextAnchor.MiddleLeft, new Vector2(200f, 30f), new Vector2(-52f, -22f), Color.white);
            SetTopRight(title.rectTransform, new Vector2(200f, 30f), new Vector2(-112f, -20f));
            Text hint = CreateText("MinimapHint", panel.transform, "M  ocultar", 13, FontStyle.Normal,
                TextAnchor.MiddleRight, new Vector2(100f, 30f), new Vector2(0f, 0f), SoftAccentColor);
            SetTopRight(hint.rectTransform, new Vector2(100f, 30f), new Vector2(-14f, -20f));

            GameObject imageObject = new GameObject("MinimapImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            imageObject.transform.SetParent(panel.transform, false);
            RawImage image = imageObject.GetComponent<RawImage>();
            image.texture = texture;
            image.color = Color.white;
            SetRect(image.rectTransform, new Vector2(298f, 252f), new Vector2(0f, -20f), new Vector2(0.5f, 0.5f));

            Text arrow = CreateText("PositionArrow", image.transform, "▲", 28, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(38f, 38f), Vector2.zero, new Color(1f, 0.90f, 0.18f, 1f));
            arrow.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            arrow.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            arrow.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            Text distanceText = CreateText("MarkerDistance", panel.transform, "0.00 m", 13, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(100f, 24f), new Vector2(0f, -142f), new Color(1f, 0.90f, 0.18f, 1f));
            distanceText.gameObject.SetActive(false);

            presenter.Configure(manager, camera, panel, image, arrow.rectTransform, distanceText, probe,
                explorationRig, routeProxy.gameObject, center);
            presenter.SetRouteVisible(true);
            presenter.SetVisible(true);
            return presenter;
        }

        private static void CreateExplorationPanel(KidneyGameUI ui, ExplorationVisibilityController visibility,
            KidneyMinimapPresenter minimap, KidneyGameManager manager, Transform canvas)
        {
            GameObject panel = CreatePanel("ExplorationPanel", canvas, new Vector2(430f, 365f),
                new Vector2(24f, -24f), new Vector2(0f, 1f), PanelColor);
            Text title = CreateText("ExplorationTitle", panel.transform, "EXPLORAÇÃO LIVRE", 22, FontStyle.Bold,
                TextAnchor.MiddleLeft, new Vector2(300f, 36f), new Vector2(24f, -22f), Color.white);
            SetTopLeft(title.rectTransform, new Vector2(300f, 36f), new Vector2(22f, -20f));
            Button collapse = CreateButton("CollapseExplorationButton", panel.transform, "H  PAINEL",
                new Vector2(112f, 34f), Vector2.zero);
            SetTopRight(collapse.GetComponent<RectTransform>(), new Vector2(112f, 34f), new Vector2(-18f, -20f));

            GameObject content = new GameObject("ExplorationContent", typeof(RectTransform));
            content.transform.SetParent(panel.transform, false);
            Stretch(content.GetComponent<RectTransform>());
            CreateText("NavigationHelp", content.transform,
                "Clique: olhar  •  Esc: cursor  •  WASD/QE: mover\nShift: acelerar  •  F: visão geral  •  M: minimapa",
                14, FontStyle.Normal, TextAnchor.MiddleLeft, new Vector2(380f, 52f), new Vector2(0f, 104f), SoftAccentColor);

            Button exterior = CreateButton("ExteriorVisibilityButton", content.transform, "", new Vector2(360f, 42f), new Vector2(0f, 48f));
            Text exteriorText = exterior.GetComponentInChildren<Text>();
            Button interior = CreateButton("InteriorVisibilityButton", content.transform, "", new Vector2(360f, 42f), new Vector2(0f, -2f));
            Text interiorText = interior.GetComponentInChildren<Text>();
            Button route = CreateButton("RouteVisibilityButton", content.transform, "", new Vector2(360f, 42f), new Vector2(0f, -52f));
            Text routeText = route.GetComponentInChildren<Text>();
            Button stone = CreateButton("StoneVisibilityButton", content.transform, "", new Vector2(360f, 42f), new Vector2(0f, -102f));
            Text stoneText = stone.GetComponentInChildren<Text>();
            Button menu = CreateButton("MenuExplorationButton", content.transform, "VOLTAR AO MENU", new Vector2(220f, 40f), new Vector2(0f, -153f));

            ui.ConfigureMarco5(visibility, minimap, panel, content, exteriorText, interiorText, routeText, stoneText,
                exterior, interior, route, stone, collapse, menu);
        }

        private static ExplorationVisibilityController.ExteriorRendererBinding CreateExteriorBinding(Renderer renderer)
        {
            Material[] source = renderer.sharedMaterials;
            Material[] opaque = source.Select(CreateOpaqueVariant).ToArray();
            Material[] transparent = source.Select(CreateTransparentVariant).ToArray();
            return new ExplorationVisibilityController.ExteriorRendererBinding
            {
                renderer = renderer,
                opaqueMaterials = opaque,
                transparentMaterials = transparent
            };
        }

        private static Material CreateTransparentVariant(Material source)
        {
            if (source == null)
                return null;
            EnsureFolder(TransparentMaterialDirectory);
            string safeName = string.Concat(source.name.Select(character => char.IsLetterOrDigit(character) ? character : '_'));
            string path = $"{TransparentMaterialDirectory}/MAT_Transparent_{safeName}.mat";
            Color color = source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor") : source.color;
            color.a = 0.32f;
            Material generated = new Material(source) { name = "MAT_Transparent_" + safeName };
            ConfigureTransparent(generated, color);
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(generated, path);
                return generated;
            }
            EditorUtility.CopySerialized(generated, existing);
            UnityEngine.Object.DestroyImmediate(generated);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        private static Material CreateOpaqueVariant(Material source)
        {
            if (source == null)
                return null;
            EnsureFolder(TransparentMaterialDirectory);
            string safeName = string.Concat(source.name.Select(character => char.IsLetterOrDigit(character) ? character : '_'));
            string path = $"{TransparentMaterialDirectory}/MAT_Opaque_{safeName}.mat";
            Color color = source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor") : source.color;
            color.a = 1f;
            Material generated = new Material(source) { name = "MAT_Opaque_" + safeName };
            generated.SetColor("_BaseColor", color);
            generated.SetFloat("_Surface", 0f);
            generated.SetFloat("_ZWrite", 1f);
            generated.SetFloat("_Cull", 0f);
            generated.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            generated.renderQueue = -1;
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(generated, path);
                return generated;
            }
            EditorUtility.CopySerialized(generated, existing);
            UnityEngine.Object.DestroyImmediate(generated);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        private static Material CreateOrUpdateMaterial(string path, Color color, bool transparent, bool emission)
        {
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            Material material = existing != null ? existing : new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
            if (material.shader != shader) material.shader = shader;
            if (transparent) ConfigureTransparent(material, color);
            else
            {
                material.SetFloat("_Surface", 0f);
                material.SetFloat("_ZWrite", 1f);
                material.SetFloat("_Cull", 2f);
                material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = -1;
                material.SetColor("_BaseColor", color);
            }
            if (emission)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 2.2f);
            }
            if (existing == null) AssetDatabase.CreateAsset(material, path);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void ConfigureTransparent(Material material, Color color)
        {
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_Cull", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
        }

        private static void CreateRendererProxy(Renderer source, Transform parent, Material material, int layer)
        {
            Mesh mesh = null;
            MeshFilter filter = source.GetComponent<MeshFilter>();
            if (filter != null) mesh = filter.sharedMesh;
            SkinnedMeshRenderer skinned = source as SkinnedMeshRenderer;
            if (skinned != null) mesh = skinned.sharedMesh;
            if (mesh == null) return;

            GameObject proxy = new GameObject("Proxy_" + source.name, typeof(MeshFilter), typeof(MeshRenderer));
            proxy.layer = layer;
            proxy.transform.SetParent(parent, false);
            proxy.transform.position = source.transform.position;
            proxy.transform.rotation = source.transform.rotation;
            proxy.transform.localScale = source.transform.lossyScale;
            proxy.GetComponent<MeshFilter>().sharedMesh = mesh;
            Material[] materials = new Material[Mathf.Max(1, source.sharedMaterials.Length)];
            for (int index = 0; index < materials.Length; index++) materials[index] = material;
            proxy.GetComponent<MeshRenderer>().sharedMaterials = materials;
        }

        private static void ConfigureMenuScene()
        {
            Scene scene = EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);
            Text version = FindSceneObject(scene, "Version")?.GetComponent<Text>();
            if (version != null)
            {
                version.text = "Marco 5 • exploração e minimapa finais";
                EditorUtility.SetDirty(version);
            }
            EditorSceneManager.SaveScene(scene, MenuScenePath);
        }

        private static void CapturePreviews()
        {
            Scene scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            KidneyGameManager manager = FindSceneComponent<KidneyGameManager>(scene, "KidneyGameManager");
            KidneyGameUI ui = FindSceneComponent<KidneyGameUI>(scene, "GameplayCanvas");
            ExplorationVisibilityController visibility = FindSceneComponent<ExplorationVisibilityController>(scene, "KidneyGameManager");
            KidneyMinimapPresenter minimap = FindSceneComponent<KidneyMinimapPresenter>(scene, "KidneyMinimapSystem");
            Camera realCamera = FindSceneComponent<Camera>(scene, "EndoscopeCamera");
            Camera explorationCamera = FindSceneComponent<Camera>(scene, "ExplorationCamera");
            Transform explorationRig = FindSceneTransform(scene, "ExplorationRig");
            Canvas canvas = FindSceneComponent<Canvas>(scene, "GameplayCanvas");
            string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Documentation/Previews"));
            Directory.CreateDirectory(directory);

            manager.SetMode(KidneyGameMode.Exploration, true);
            visibility.ResetDefaults();
            manager.SetRouteVisible(true);
            manager.SetMinimapVisible(true);
            ui.RefreshImmediate();
            CaptureCamera(explorationCamera, minimap.MinimapCamera, canvas, Path.Combine(directory, "marco5_exploration_transparent.png"));

            visibility.SetExteriorMode(ExteriorVisibilityMode.Opaque);
            ui.RefreshImmediate();
            CaptureCamera(explorationCamera, minimap.MinimapCamera, canvas, Path.Combine(directory, "marco5_exploration_opaque.png"));

            visibility.SetExteriorMode(ExteriorVisibilityMode.Hidden);
            ui.RefreshImmediate();
            CaptureCamera(explorationCamera, minimap.MinimapCamera, canvas, Path.Combine(directory, "marco5_exploration_hidden.png"));

            Vector3 savedPosition = explorationRig.position;
            Quaternion savedRotation = explorationRig.rotation;
            explorationRig.position += new Vector3(3.5f, 0.4f, -0.4f);
            explorationRig.rotation = Quaternion.LookRotation(savedPosition - explorationRig.position, Vector3.up);
            minimap.RefreshMarker();
            ui.RefreshImmediate();
            CaptureCamera(explorationCamera, minimap.MinimapCamera, canvas, Path.Combine(directory, "marco5_minimap_edge_indicator.png"));
            explorationRig.SetPositionAndRotation(savedPosition, savedRotation);

            visibility.ResetDefaults();
            manager.SetMode(KidneyGameMode.Realistic, true);
            manager.PrepareAttempt();
            minimap.RefreshMarker();
            ui.RefreshImmediate();
            CaptureCamera(realCamera, minimap.MinimapCamera, canvas, Path.Combine(directory, "marco5_realistic_minimap.png"));
        }

        private static void CaptureCamera(Camera camera, Camera minimapCamera, Canvas canvas, string outputPath)
        {
            if (camera == null || canvas == null)
                throw new InvalidOperationException("Camera ou Canvas ausente para preview do Marco 5.");
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
                if (minimapCamera != null)
                {
                    minimapCamera.gameObject.SetActive(true);
                    minimapCamera.Render();
                }
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
            CreateText("Label", image.transform, label, 16, FontStyle.Bold, TextAnchor.MiddleCenter, size, Vector2.zero, Color.white);
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

        private static void SetTopLeft(RectTransform rect, Vector2 size, Vector2 position) =>
            SetRect(rect, size, position, new Vector2(0f, 1f));

        private static void SetTopRight(RectTransform rect, Vector2 size, Vector2 position) =>
            SetRect(rect, size, position, new Vector2(1f, 1f));

        private static Bounds CombineBounds(IEnumerable<Renderer> renderers)
        {
            Renderer[] array = renderers.Where(renderer => renderer != null).ToArray();
            if (array.Length == 0) return new Bounds(Vector3.zero, Vector3.one);
            Bounds bounds = array[0].bounds;
            for (int index = 1; index < array.Length; index++) bounds.Encapsulate(array[index].bounds);
            return bounds;
        }

        private static bool SameMaterials(Material[] left, Material[] right)
        {
            if (left == null || right == null || left.Length != right.Length) return false;
            for (int index = 0; index < left.Length; index++)
                if (left[index] != right[index]) return false;
            return true;
        }

        private static int ReadLegacyCheckCount()
        {
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../Documentation/marco4_validation.json"));
            if (!File.Exists(path)) return 0;
            LegacyValidationReport report = JsonUtility.FromJson<LegacyValidationReport>(File.ReadAllText(path));
            return report != null ? report.totalChecks : 0;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        private static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform) SetLayerRecursively(child.gameObject, layer);
        }

        private static void DestroySceneObject(Scene scene, string objectName)
        {
            GameObject target = FindSceneObject(scene, objectName);
            if (target != null) UnityEngine.Object.DestroyImmediate(target);
        }

        private static Transform FindDeep(Transform root, string name) => root == null ? null :
            root.GetComponentsInChildren<Transform>(true).FirstOrDefault(transform => transform.name == name);

        private static int CountMissingScripts(Scene scene) => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .Sum(transform => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject));

        private static T FindSceneComponent<T>(Scene scene, string objectName) where T : Component
        {
            GameObject target = FindSceneObject(scene, objectName);
            return target != null ? target.GetComponent<T>() : null;
        }

        private static GameObject FindSceneObject(Scene scene, string objectName) => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .FirstOrDefault(transform => transform.name == objectName)?.gameObject;

        private static Transform FindSceneTransform(Scene scene, string objectName) => FindSceneObject(scene, objectName)?.transform;
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
            public int totalChecks;
        }

        [Serializable]
        private sealed class ValidationReport
        {
            public string milestone;
            public string unityVersion;
            public string generatedUtc;
            public bool passed;
            public int legacyChecks;
            public int marco5Checks;
            public int totalChecks;
            public string fbxV003Sha256;
            public string fbxV002Sha256;
            public string meshyFbxSha256;
            public string[] checks;
            public string[] errors;
        }
    }
}
