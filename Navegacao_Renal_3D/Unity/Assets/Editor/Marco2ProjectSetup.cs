using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace NavegacaoRenal.Editor
{
    public static class Marco2ProjectSetup
    {
        private const string ModelPath = "Assets/Art/Kidney/Models/Kidney_Game_v002.fbx";
        private const string ManifestPath = "Assets/Art/Kidney/Models/Kidney_Game_v002_manifest.json";
        private const string PrefabPath = "Assets/Prefabs/Kidney/KidneyLevel.prefab";
        private const string GameScenePath = "Assets/Scenes/KidneyGame.unity";
        private const string MenuScenePath = "Assets/Scenes/MainMenu.unity";
        private const string PipelinePath = "Assets/Settings/NavegacaoRenal_URP.asset";
        private const string RendererPath = "Assets/Settings/NavegacaoRenal_Renderer.asset";
        private const string MucosaBaseColorPath = "Assets/Art/Textures/Organic/T_RenalMucosa_BaseColor_v001.png";
        private const string MucosaNormalPath = "Assets/Art/Textures/Organic/T_RenalMucosa_NormalSource_v001.png";
        private const string MeshyVisualModelPath = "Assets/Art/UrinarySystem/Models/Meshy_Urinary_Visual_v002.fbx";
        private const string MeshyBaseColorPath = "Assets/Art/UrinarySystem/Textures/T_MeshyUrinary_BaseColor_v002.png";
        private const string MeshyNormalPath = "Assets/Art/UrinarySystem/Textures/T_MeshyUrinary_Normal_v002.png";
        private const string MeshyMetallicPath = "Assets/Art/UrinarySystem/Textures/T_MeshyUrinary_Metallic_v002.png";
        private const string MeshyRoughnessPath = "Assets/Art/UrinarySystem/Textures/T_MeshyUrinary_Roughness_v002.png";
        private const string MeshyMaskPath = "Assets/Art/UrinarySystem/Textures/T_MeshyUrinary_MaskMap_v002.png";
        private const string MeshyMaterialPath = "Assets/Materials/MAT_MeshyUrinary_URP.mat";

        private static readonly string[] RequiredNodes =
        {
            "KidneyExterior", "CollectingSystemVisual", "CollectingSystemCollision_Inward",
            "RouteGuide", "Stone", "StartAnchor", "TargetAnchor", "MinimapAnchor"
        };

        [MenuItem("Navegacao Renal/Construir Marco 2")]
        public static void Configure()
        {
            Debug.Log("[Marco2] Iniciando configuracao do projeto.");
            EnsureFolders();
            ConfigureLayers();
            Physics.queriesHitBackfaces = true;
            ConfigureModelImporter();
            ConfigureOrganicTextureImporters();
            ConfigureMeshyVisualImporters();
            ConfigureRenderPipeline();

            // Este é o material que o usuário já aprovou para o rim ativo.
            // Reaproveitá-lo evita que uma reconstrução posterior normalize ou
            // altere propriedades internas do shader URP.
            Material exterior = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/MAT_KidneyExterior_URP.mat");
            if (exterior == null)
            {
                exterior = CreateOrUpdateMaterial("Assets/Materials/MAT_KidneyExterior_URP.mat", new Color(0.42f, 0.055f, 0.10f, 0.48f), true, false);
            }
            Material interior = CreateOrUpdateMaterial("Assets/Materials/MAT_CollectingSystem_URP.mat", new Color(0.94f, 0.72f, 0.72f, 1f), false, false);
            Material rightKidney = CreateOrUpdateMaterial("Assets/Materials/MAT_KidneyRight_URP.mat", new Color(0.48f, 0.035f, 0.055f, 1f), false, false);
            Material route = CreateOrUpdateMaterial("Assets/Materials/MAT_Route_URP.mat", new Color(0.02f, 0.78f, 1f, 0.26f), true, true);
            route.SetFloat("_Cull", (float)CullMode.Back);
            EditorUtility.SetDirty(route);
            Material stone = CreateOrUpdateMaterial("Assets/Materials/MAT_Stone_URP.mat", new Color(0.95f, 0.62f, 0.08f, 1f), false, true);
            Material ureter = CreateOrUpdateMaterial("Assets/Materials/MAT_Ureter_URP.mat", new Color(0.82f, 0.045f, 0.09f, 0.88f), true, false);
            Material bladder = CreateOrUpdateMaterial("Assets/Materials/MAT_Bladder_URP.mat", new Color(0.72f, 0.035f, 0.07f, 0.82f), true, false);
            Material probe = CreateOrUpdateMaterial("Assets/Materials/MAT_ProbeMarker_URP.mat", new Color(0.02f, 0.85f, 1f, 1f), false, true);
            Material meshyUrinary = CreateOrUpdateMeshyMaterial();

            Texture2D mucosaBaseColor = AssetDatabase.LoadAssetAtPath<Texture2D>(MucosaBaseColorPath);
            Texture2D mucosaNormal = AssetDatabase.LoadAssetAtPath<Texture2D>(MucosaNormalPath);
            ConfigureOrganicMaterial(interior, mucosaBaseColor, mucosaNormal, new Vector2(5f, 7f), 0.42f, 0.74f, true);
            ConfigureOrganicMaterial(rightKidney, null, mucosaNormal, new Vector2(3.2f, 3.2f), 0.18f, 0.68f, false);
            ConfigureOrganicMaterial(ureter, null, mucosaNormal, new Vector2(2f, 8f), 0.16f, 0.82f, false);
            ConfigureOrganicMaterial(bladder, null, mucosaNormal, new Vector2(3f, 3f), 0.22f, 0.78f, false);

            CreateKidneyPrefab(exterior, interior, route, stone);
            CreateGameScene(meshyUrinary, probe);
            CreateMenuScene();
            ConfigureBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Validate();
            CapturePreviews();
            Debug.Log("[Marco2] Configuracao e validacao concluidas.");
        }

        [MenuItem("Navegacao Renal/Validar Marco 2")]
        public static void Validate()
        {
            List<string> checks = new List<string>();
            List<string> errors = new List<string>();

            Check(File.Exists(ToAbsolute(ModelPath)), "FBX v002 presente", checks, errors);
            Check(File.Exists(ToAbsolute(ManifestPath)), "manifesto v002 presente", checks, errors);
            Check(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null, "prefab KidneyLevel criado", checks, errors);
            Check(File.Exists(ToAbsolute(GameScenePath)), "cena KidneyGame criada", checks, errors);
            Check(File.Exists(ToAbsolute(MenuScenePath)), "cena MainMenu criada", checks, errors);

            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model != null)
            {
                foreach (string nodeName in RequiredNodes)
                    Check(FindDeep(model.transform, nodeName) != null, $"no obrigatorio {nodeName}", checks, errors);

                GameObject temporary = UnityEngine.Object.Instantiate(model);
                try
                {
                    Transform exterior = FindDeep(temporary.transform, "KidneyExterior");
                    Renderer renderer = exterior != null ? exterior.GetComponent<Renderer>() : null;
                    float height = renderer != null ? renderer.bounds.size.y : 0f;
                    Check(height > 0.145f && height < 0.156f, $"altura fisica importada {height:F6} m", checks, errors);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(temporary);
                }
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab != null)
            {
                Transform gameplayScale = FindDeep(prefab.transform, "GameplayScaleRoot");
                Check(gameplayScale != null && Approximately(gameplayScale.localScale.x, 5f), "escala visual 5x", checks, errors);
                Transform collision = FindDeep(prefab.transform, "CollectingSystemCollision_Inward");
                Check(collision != null && collision.GetComponent<MeshCollider>() != null, "MeshCollider interno criado", checks, errors);
            }

            string expectedHash = "174fabbf6ec31b3052360be995b5bbc4fb7e074b91ef2a2bba838ca45cc0fa9c";
            string actualHash = File.Exists(ToAbsolute(ModelPath)) ? Sha256(ToAbsolute(ModelPath)) : string.Empty;
            Check(string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase), "SHA-256 do FBX v002", checks, errors);
            string expectedMeshyHash = "f8408f41656011f65cb737b1b434e7611228036b76fb8fcadfaa183dcfa26ed2";
            string actualMeshyHash = File.Exists(ToAbsolute(MeshyVisualModelPath)) ? Sha256(ToAbsolute(MeshyVisualModelPath)) : string.Empty;
            Check(string.Equals(actualMeshyHash, expectedMeshyHash, StringComparison.OrdinalIgnoreCase), "SHA-256 do visual Meshy v002", checks, errors);

            Scene gameScene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            string[] sceneNames = gameScene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).Select(t => t.name).ToArray();
            Check(sceneNames.Count(n => n == "KidneyLevel_Active") == 1, "um rim ativo detalhado", checks, errors);
            Check(sceneNames.Count(n => n == "MeshyUrinaryVisualRoot") == 1, "conjunto visual Meshy na cena", checks, errors);
            Check(sceneNames.Contains("Meshy_RightKidney"), "rim direito Meshy na cena", checks, errors);
            Check(sceneNames.Contains("Meshy_UretersAndBladder"), "ureteres e bexiga Meshy na cena", checks, errors);
            Check(!sceneNames.Contains("KidneyLevel_Passive") && !sceneNames.Contains("LeftUreter") &&
                  !sceneNames.Contains("RightUreter") && !sceneNames.Contains("Bladder"),
                "malhas procedurais antigas fora da cena", checks, errors);
            Check(sceneNames.Contains("RealisticRig") && sceneNames.Contains("ExplorationRig"), "dois modos de navegacao", checks, errors);

            ValidationReport report = new ValidationReport
            {
                milestone = "Marco 2",
                unityVersion = Application.unityVersion,
                generatedUtc = DateTime.UtcNow.ToString("O"),
                passed = errors.Count == 0,
                fbxSha256 = actualHash,
                checks = checks.ToArray(),
                errors = errors.ToArray()
            };

            string reportPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../Documentation/marco2_validation.json"));
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
            File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
            Debug.Log($"[Marco2] Relatorio: {reportPath}\n{JsonUtility.ToJson(report, true)}");

            if (errors.Count > 0)
                throw new InvalidOperationException("Marco 2 falhou: " + string.Join(" | ", errors));
        }

        private static void EnsureFolders()
        {
            string[] folders =
            {
                "Assets/Art", "Assets/Materials", "Assets/Prefabs", "Assets/Prefabs/Kidney",
                "Assets/Scenes", "Assets/Settings", "Assets/Generated", "Assets/Generated/Meshes",
                "Assets/Art/Textures", "Assets/Art/Textures/Organic", "Assets/Art/UrinarySystem",
                "Assets/Art/UrinarySystem/Models", "Assets/Art/UrinarySystem/Textures"
            };

            foreach (string folder in folders)
            {
                if (AssetDatabase.IsValidFolder(folder)) continue;
                string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
                string name = Path.GetFileName(folder);
                AssetDatabase.CreateFolder(parent, name);
            }
        }

        private static void ConfigureLayers()
        {
            string[] names = { "KidneyExterior", "KidneyInteriorVisual", "KidneyCollision", "Route", "Stone", "ProbeMarker", "MinimapOnly" };
            SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            int next = 8;
            foreach (string layerName in names)
            {
                bool exists = false;
                for (int i = 8; i < 32; i++)
                {
                    SerializedProperty layer = layers.GetArrayElementAtIndex(i);
                    if (layer.stringValue == layerName) { exists = true; break; }
                }
                if (exists) continue;
                while (next < 32 && !string.IsNullOrEmpty(layers.GetArrayElementAtIndex(next).stringValue)) next++;
                if (next >= 32) throw new InvalidOperationException("Nao ha camadas livres suficientes.");
                layers.GetArrayElementAtIndex(next).stringValue = layerName;
                next++;
            }
            tagManager.ApplyModifiedProperties();
        }

        private static void ConfigureModelImporter()
        {
            AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceUpdate);
            ModelImporter importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("O FBX v002 nao foi reconhecido pelo Unity.");
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.isReadable = true;
            importer.SaveAndReimport();
        }

        private static void ConfigureOrganicTextureImporters()
        {
            ConfigureTextureImporter(MucosaBaseColorPath, false);
            ConfigureTextureImporter(MucosaNormalPath, true);
        }

        private static void ConfigureMeshyVisualImporters()
        {
            AssetDatabase.ImportAsset(MeshyVisualModelPath, ImportAssetOptions.ForceUpdate);
            ModelImporter modelImporter = AssetImporter.GetAtPath(MeshyVisualModelPath) as ModelImporter;
            if (modelImporter == null) throw new InvalidOperationException("O FBX visual Meshy v002 nao foi reconhecido pelo Unity.");
            modelImporter.globalScale = 1f;
            modelImporter.useFileScale = true;
            modelImporter.importAnimation = false;
            modelImporter.importCameras = false;
            modelImporter.importLights = false;
            modelImporter.materialImportMode = ModelImporterMaterialImportMode.None;
            modelImporter.isReadable = true;
            modelImporter.SaveAndReimport();

            ConfigureMeshyTextureImporter(MeshyBaseColorPath, true, false);
            ConfigureMeshyTextureImporter(MeshyNormalPath, false, true);
            ConfigureMeshyTextureImporter(MeshyMetallicPath, false, false);
            ConfigureMeshyTextureImporter(MeshyRoughnessPath, false, false);
            ConfigureMeshyTextureImporter(MeshyMaskPath, false, false);
        }

        private static void ConfigureMeshyTextureImporter(string path, bool sRgb, bool normalMap)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Textura Meshy ausente: " + path);
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 8;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.textureType = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = sRgb && !normalMap;
            importer.convertToNormalmap = false;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.SaveAndReimport();
        }

        private static void ConfigureTextureImporter(string path, bool normalMap)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Textura organica ausente: " + path);

            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 8;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.textureType = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !normalMap;
            importer.convertToNormalmap = normalMap;
            if (normalMap) importer.heightmapScale = 0.08f;
            importer.SaveAndReimport();
        }

        private static void ConfigureRenderPipeline()
        {
            UniversalRendererData renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, RendererPath);
            }

            UniversalRenderPipelineAsset pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
            }

            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
        }

        private static Material CreateOrUpdateMaterial(string path, Color color, bool transparent, bool emission)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("Shader URP/Lit nao encontrado.");
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else material.shader = shader;

            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0.58f);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Cull", (float)CullMode.Off);
            if (transparent)
            {
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0f);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = (int)RenderQueue.Transparent;
            }
            else
            {
                material.SetFloat("_Surface", 0f);
                material.SetFloat("_ZWrite", 1f);
                material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = (int)RenderQueue.Geometry;
            }
            if (emission)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 1.7f);
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material CreateOrUpdateMeshyMaterial()
        {
            Material material = CreateOrUpdateMaterial(MeshyMaterialPath, Color.white, false, false);
            Texture2D baseColor = AssetDatabase.LoadAssetAtPath<Texture2D>(MeshyBaseColorPath);
            Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(MeshyNormalPath);
            Texture2D mask = AssetDatabase.LoadAssetAtPath<Texture2D>(MeshyMaskPath);
            if (baseColor == null || normal == null || mask == null)
                throw new InvalidOperationException("Os mapas PBR do sistema urinario Meshy nao foram importados.");

            material.SetTexture("_BaseMap", baseColor);
            material.SetTexture("_BumpMap", normal);
            material.SetTexture("_MetallicGlossMap", mask);
            material.SetFloat("_BumpScale", 0.72f);
            material.SetFloat("_Metallic", 1f);
            material.SetFloat("_Smoothness", 1f);
            material.SetFloat("_SmoothnessTextureChannel", 0f);
            material.SetFloat("_Cull", (float)CullMode.Back);
            material.EnableKeyword("_NORMALMAP");
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void ConfigureOrganicMaterial(
            Material material,
            Texture2D baseColor,
            Texture2D normal,
            Vector2 tiling,
            float normalStrength,
            float smoothness,
            bool doubleSided)
        {
            material.SetTexture("_BaseMap", baseColor);
            material.SetTextureScale("_BaseMap", tiling);
            material.SetTexture("_BumpMap", normal);
            material.SetTextureScale("_BumpMap", tiling);
            material.SetFloat("_BumpScale", normalStrength);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Cull", doubleSided ? (float)CullMode.Off : (float)CullMode.Back);
            if (normal != null) material.EnableKeyword("_NORMALMAP");
            else material.DisableKeyword("_NORMALMAP");
            EditorUtility.SetDirty(material);
        }

        private static void CreateKidneyPrefab(Material exteriorMaterial, Material interiorMaterial, Material routeMaterial, Material stoneMaterial)
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (source == null) throw new InvalidOperationException("FBX v002 nao carregado.");

            GameObject root = new GameObject("KidneyLevel");
            GameObject physical = new GameObject("PhysicalScaleRoot");
            GameObject gameplay = new GameObject("GameplayScaleRoot");
            physical.transform.SetParent(root.transform, false);
            gameplay.transform.SetParent(physical.transform, false);
            gameplay.transform.localScale = Vector3.one * 5f;

            GameObject model = PrefabUtility.InstantiatePrefab(source) as GameObject;
            if (model == null) throw new InvalidOperationException("Nao foi possivel instanciar o FBX.");
            PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            model.name = "KidneyModel_v002";
            model.transform.SetParent(gameplay.transform, false);

            SetNodeMaterialAndLayer(model.transform, "KidneyExterior", exteriorMaterial, "KidneyExterior");
            SetNodeMaterialAndLayer(model.transform, "CollectingSystemVisual", interiorMaterial, "KidneyInteriorVisual");
            SetNodeMaterialAndLayer(model.transform, "RouteGuide", routeMaterial, "Route");
            SetNodeMaterialAndLayer(model.transform, "Stone", stoneMaterial, "Stone");

            Transform collisionNode = FindDeep(model.transform, "CollectingSystemCollision_Inward");
            if (collisionNode == null) throw new InvalidOperationException("No de colisao interna ausente.");
            SetLayerRecursively(collisionNode.gameObject, LayerMask.NameToLayer("KidneyCollision"));
            Renderer collisionRenderer = collisionNode.GetComponent<Renderer>();
            if (collisionRenderer != null) collisionRenderer.enabled = false;
            MeshFilter collisionFilter = collisionNode.GetComponent<MeshFilter>();
            MeshCollider collider = collisionNode.GetComponent<MeshCollider>();
            if (collider == null)
                collider = collisionNode.gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = collisionFilter != null ? collisionFilter.sharedMesh : null;
            collider.convex = false;

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
        }

        private static void CreateGameScene(Material meshyUrinaryMaterial, Material probeMaterial)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.22f, 0.13f, 0.16f);
            RenderSettings.ambientEquatorColor = new Color(0.12f, 0.055f, 0.07f);
            RenderSettings.ambientGroundColor = new Color(0.035f, 0.02f, 0.025f);

            GameObject systems = new GameObject("UrinarySystemRoot");
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            GameObject active = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            active.name = "KidneyLevel_Active";
            active.transform.SetParent(systems.transform, false);
            active.transform.position = new Vector3(-0.44f, 0.34f, 0f);
            active.transform.rotation = Quaternion.Euler(2f, -12f, -5f);

            CreateMeshyUrinaryVisual(systems.transform, meshyUrinaryMaterial, active.transform);
            // O alinhamento principal usa o rim direito Meshy como referencia.
            // Depois dele, aproxima somente o rim ativo esquerdo 7,5 cm no
            // mundo visual para unir sua saida ao ureter sem deslocar o resto
            // do sistema urinario.
            active.transform.position += new Vector3(0.075f, 0f, 0f);
            CreateLighting();

            GameObject managerObject = new GameObject("KidneyGameManager");
            KidneyGameManager manager = managerObject.AddComponent<KidneyGameManager>();
            Transform startAnchor = FindDeep(active.transform, "StartAnchor");
            Transform targetAnchor = FindDeep(active.transform, "TargetAnchor");
            Transform stone = FindDeep(active.transform, "Stone");
            Transform routeGuide = FindDeep(active.transform, "RouteGuide");
            if (startAnchor != null && targetAnchor != null)
                startAnchor.rotation = Quaternion.LookRotation(CalculateRouteStartDirection(routeGuide, startAnchor, targetAnchor), Vector3.up);

            GameObject realisticRig = new GameObject("RealisticRig");
            GameObject probe = new GameObject("ProbeTip");
            probe.transform.SetParent(realisticRig.transform, false);
            MouseEndoscopeController controller = probe.AddComponent<MouseEndoscopeController>();
            controller.Configure(manager, 1 << LayerMask.NameToLayer("KidneyCollision"));

            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = "ProbeMarker";
            marker.transform.SetParent(probe.transform, false);
            marker.transform.localScale = Vector3.one * 0.016f;
            marker.GetComponent<Renderer>().sharedMaterial = probeMaterial;
            UnityEngine.Object.DestroyImmediate(marker.GetComponent<Collider>());
            SetLayerRecursively(marker, LayerMask.NameToLayer("ProbeMarker"));

            Camera realCamera = CreateCamera("EndoscopeCamera", probe.transform, Vector3.zero, Quaternion.identity);
            realCamera.fieldOfView = 80f;
            realCamera.nearClipPlane = 0.002f;
            realCamera.farClipPlane = 8f;
            realCamera.cullingMask &= ~(1 << LayerMask.NameToLayer("KidneyExterior"));
            realCamera.cullingMask &= ~(1 << LayerMask.NameToLayer("ProbeMarker"));
            Light tipLight = realCamera.gameObject.AddComponent<Light>();
            tipLight.type = LightType.Spot;
            tipLight.range = 0.75f;
            tipLight.intensity = 4.2f;
            tipLight.spotAngle = 72f;
            tipLight.color = new Color(1f, 0.78f, 0.72f);

            GameObject explorationRig = new GameObject("ExplorationRig");
            Camera explorationCamera = CreateCamera("ExplorationCamera", explorationRig.transform, Vector3.zero, Quaternion.identity);
            explorationRig.transform.position = new Vector3(0f, -0.12f, -3.15f);
            explorationRig.transform.LookAt(new Vector3(0f, -0.18f, 0f));
            explorationCamera.fieldOfView = 62f;
            explorationCamera.nearClipPlane = 0.01f;
            explorationRig.AddComponent<FreeFlyCameraController>();
            explorationRig.SetActive(false);

            Camera minimap = CreateCamera("MinimapCamera", null, new Vector3(0f, 0.55f, -3.0f), Quaternion.identity, false);
            minimap.transform.LookAt(new Vector3(0f, -0.25f, 0f));
            minimap.rect = new Rect(0.75f, 0.70f, 0.23f, 0.27f);
            minimap.depth = 5f;
            minimap.fieldOfView = 35f;
            minimap.clearFlags = CameraClearFlags.SolidColor;
            minimap.backgroundColor = new Color(0.035f, 0.012f, 0.018f, 1f);

            if (routeGuide != null) routeGuide.gameObject.SetActive(false);
            manager.Configure(realisticRig, explorationRig, probe.transform, startAnchor, stone, routeGuide != null ? routeGuide.gameObject : null, minimap.gameObject);
            if (startAnchor != null) probe.transform.SetPositionAndRotation(startAnchor.position, startAnchor.rotation);

            EditorSceneManager.SaveScene(scene, GameScenePath);
        }

        private static void CreateMeshyUrinaryVisual(Transform parent, Material material, Transform activeKidney)
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(MeshyVisualModelPath);
            if (source == null) throw new InvalidOperationException("FBX visual Meshy v002 nao carregado.");
            GameObject visual = PrefabUtility.InstantiatePrefab(source) as GameObject;
            if (visual == null) throw new InvalidOperationException("Nao foi possivel instanciar o sistema urinario Meshy.");
            visual.name = "MeshyUrinaryVisualRoot";
            visual.transform.SetParent(parent, false);
            visual.transform.localScale = Vector3.one * 5f;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localPosition = Vector3.zero;

            // Alinha o centro do rim direito Meshy ao espelho do rim ativo.
            // A propria geometria importada define o deslocamento, evitando
            // depender da conversao de eixos e pivôs feita pelo FBX.
            Transform activeExterior = FindDeep(activeKidney, "KidneyExterior");
            Transform rightKidney = FindDeep(visual.transform, "Meshy_RightKidney");
            Renderer activeRenderer = activeExterior != null ? activeExterior.GetComponent<Renderer>() : null;
            Renderer rightRenderer = rightKidney != null ? rightKidney.GetComponentInChildren<Renderer>() : null;
            if (activeRenderer == null || rightRenderer == null)
                throw new InvalidOperationException("Nao foi possivel alinhar os rins visualmente.");
            Vector3 mirroredCenter = new Vector3(-activeRenderer.bounds.center.x, activeRenderer.bounds.center.y, activeRenderer.bounds.center.z);
            visual.transform.position += mirroredCenter - rightRenderer.bounds.center;

            int exteriorLayer = LayerMask.NameToLayer("KidneyExterior");
            SetLayerRecursively(visual, exteriorLayer);
            foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true))
                renderer.sharedMaterial = material;
        }

        private static void CreateMenuScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Camera camera = CreateCamera("Main Camera", null, new Vector3(0f, 0f, -10f), Quaternion.identity);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.045f, 0.012f, 0.022f, 1f);
            new GameObject("MainMenu").AddComponent<MainMenuPresenter>();
            EditorSceneManager.SaveScene(scene, MenuScenePath);
        }

        private static void CreateUrinarySystemMeshes(Transform parent, Material ureterMaterial, Material bladderMaterial)
        {
            Vector3[] leftPoints =
            {
                new Vector3(-0.73f, -0.04f, 0.01f), new Vector3(-0.73f, -0.20f, 0.015f),
                new Vector3(-0.68f, -0.40f, 0.02f), new Vector3(-0.60f, -0.61f, 0.02f),
                new Vector3(-0.50f, -0.80f, 0.018f), new Vector3(-0.38f, -0.97f, 0.014f),
                new Vector3(-0.28f, -1.06f, 0.01f)
            };
            Vector3[] rightPoints = leftPoints.Select(p => new Vector3(-p.x, p.y, p.z)).ToArray();
            Mesh leftMesh = SaveMesh(CreateSmoothTubeMesh(leftPoints, 0.026f, 0.020f, 24, 7), "Assets/Generated/Meshes/LeftUreter.asset");
            Mesh rightMesh = SaveMesh(CreateSmoothTubeMesh(rightPoints, 0.026f, 0.020f, 24, 7), "Assets/Generated/Meshes/RightUreter.asset");
            CreateMeshObject("LeftUreter", leftMesh, ureterMaterial, parent);
            CreateMeshObject("RightUreter", rightMesh, ureterMaterial, parent);

            Mesh bladderMesh = SaveMesh(CreateUvSphere(40, 28, true), "Assets/Generated/Meshes/Bladder.asset");
            GameObject bladder = CreateMeshObject("Bladder", bladderMesh, bladderMaterial, parent);
            bladder.transform.position = new Vector3(0f, -1.18f, 0.02f);
            bladder.transform.localScale = new Vector3(0.39f, 0.34f, 0.29f);

            Vector3[] outletPoints =
            {
                new Vector3(0f, -1.48f, 0.02f), new Vector3(0f, -1.60f, 0.02f)
            };
            Mesh outletMesh = SaveMesh(CreateSmoothTubeMesh(outletPoints, 0.035f, 0.025f, 24, 4), "Assets/Generated/Meshes/BladderOutlet.asset");
            CreateMeshObject("BladderOutlet", outletMesh, bladderMaterial, parent);
        }

        private static Mesh CreateSmoothTubeMesh(IReadOnlyList<Vector3> controlPoints, float startRadius, float endRadius, int sides, int samplesPerSegment)
        {
            List<Vector3> points = new List<Vector3>();
            for (int segment = 0; segment < controlPoints.Count - 1; segment++)
            {
                Vector3 p0 = controlPoints[Math.Max(0, segment - 1)];
                Vector3 p1 = controlPoints[segment];
                Vector3 p2 = controlPoints[segment + 1];
                Vector3 p3 = controlPoints[Math.Min(controlPoints.Count - 1, segment + 2)];
                for (int sample = 0; sample < samplesPerSegment; sample++)
                    points.Add(CatmullRom(p0, p1, p2, p3, (float)sample / samplesPerSegment));
            }
            points.Add(controlPoints[controlPoints.Count - 1]);

            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();
            List<Vector2> uvs = new List<Vector2>();
            for (int i = 0; i < points.Count; i++)
            {
                Vector3 tangent = i == points.Count - 1 ? points[i] - points[i - 1] : points[Math.Min(i + 1, points.Count - 1)] - points[Math.Max(0, i - 1)];
                tangent.Normalize();
                Vector3 normal = Vector3.Cross(tangent, Mathf.Abs(Vector3.Dot(tangent, Vector3.forward)) > 0.9f ? Vector3.up : Vector3.forward).normalized;
                Vector3 binormal = Vector3.Cross(tangent, normal).normalized;
                float progress = (float)i / (points.Count - 1);
                float radius = Mathf.Lerp(startRadius, endRadius, progress);
                for (int side = 0; side < sides; side++)
                {
                    float angle = side * Mathf.PI * 2f / sides;
                    vertices.Add(points[i] + (normal * Mathf.Cos(angle) + binormal * Mathf.Sin(angle)) * radius);
                    uvs.Add(new Vector2((float)side / sides, progress * 6f));
                }
            }
            for (int ring = 0; ring < points.Count - 1; ring++)
            {
                for (int side = 0; side < sides; side++)
                {
                    int next = (side + 1) % sides;
                    int a = ring * sides + side;
                    int b = ring * sides + next;
                    int c = (ring + 1) * sides + side;
                    int d = (ring + 1) * sides + next;
                    triangles.Add(a); triangles.Add(c); triangles.Add(b);
                    triangles.Add(b); triangles.Add(c); triangles.Add(d);
                }
            }
            Mesh mesh = new Mesh { name = "ProceduralUreter" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.SetUVs(0, uvs);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            return 0.5f * ((2f * p1) + (-p0 + p2) * t +
                (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        private static Mesh CreateUvSphere(int longitude, int latitude, bool pearShape)
        {
            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();
            List<Vector2> uvs = new List<Vector2>();
            for (int lat = 0; lat <= latitude; lat++)
            {
                float v = (float)lat / latitude;
                float phi = Mathf.PI * v;
                for (int lon = 0; lon <= longitude; lon++)
                {
                    float u = (float)lon / longitude;
                    float theta = Mathf.PI * 2f * u;
                    float y = Mathf.Cos(phi);
                    float profile = pearShape ? Mathf.Lerp(0.84f, 1.06f, (1f - y) * 0.5f) : 1f;
                    vertices.Add(new Vector3(Mathf.Sin(phi) * Mathf.Cos(theta) * profile, y, Mathf.Sin(phi) * Mathf.Sin(theta) * profile));
                    uvs.Add(new Vector2(u, v));
                }
            }
            for (int lat = 0; lat < latitude; lat++)
            {
                for (int lon = 0; lon < longitude; lon++)
                {
                    int a = lat * (longitude + 1) + lon;
                    int b = a + longitude + 1;
                    triangles.Add(a); triangles.Add(b); triangles.Add(a + 1);
                    triangles.Add(a + 1); triangles.Add(b); triangles.Add(b + 1);
                }
            }
            Mesh mesh = new Mesh { name = "ProceduralBladder" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.SetUVs(0, uvs);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh SaveMesh(Mesh generated, string path)
        {
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
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

        private static GameObject CreateMeshObject(string name, Mesh mesh, Material material, Transform parent)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }

        private static void CreateLighting()
        {
            GameObject key = new GameObject("SoftKeyLight");
            Light light = key.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.8f;
            light.color = new Color(1f, 0.73f, 0.78f);
            key.transform.rotation = Quaternion.Euler(32f, -35f, 0f);

            GameObject fill = new GameObject("WarmFillLight");
            Light fillLight = fill.AddComponent<Light>();
            fillLight.type = LightType.Point;
            fillLight.intensity = 5f;
            fillLight.range = 5f;
            fillLight.color = new Color(1f, 0.20f, 0.28f);
            fill.transform.position = new Vector3(0f, -0.25f, -1.2f);
        }

        private static Camera CreateCamera(string name, Transform parent, Vector3 localPosition, Quaternion localRotation, bool addAudioListener = true)
        {
            GameObject go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = localRotation;
            Camera camera = go.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.035f, 0.006f, 0.012f, 1f);
            if (addAudioListener) go.AddComponent<AudioListener>();
            return camera;
        }

        private static void CapturePreviews()
        {
            Scene scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            Camera exploration = FindSceneComponent<Camera>(scene, "ExplorationCamera");
            Camera realistic = FindSceneComponent<Camera>(scene, "EndoscopeCamera");
            string previewDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Documentation/Previews"));
            Directory.CreateDirectory(previewDirectory);
            CaptureCamera(exploration, Path.Combine(previewDirectory, "marco2_urinary_system.png"));
            CaptureCamera(realistic, Path.Combine(previewDirectory, "marco2_realistic_start.png"));
        }

        private static T FindSceneComponent<T>(Scene scene, string objectName) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T component = root.GetComponentsInChildren<T>(true).FirstOrDefault(c => c.name == objectName);
                if (component != null) return component;
            }
            return null;
        }

        private static void CaptureCamera(Camera camera, string outputPath)
        {
            if (camera == null) throw new InvalidOperationException("Camera de preview nao encontrada.");
            bool wasActive = camera.gameObject.activeSelf;
            camera.gameObject.SetActive(true);
            RenderTexture renderTexture = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            Texture2D image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                camera.targetTexture = renderTexture;
                camera.Render();
                RenderTexture.active = renderTexture;
                image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                image.Apply();
                File.WriteAllBytes(outputPath, image.EncodeToPNG());
                Debug.Log("[Marco2] Preview criado: " + outputPath);
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                UnityEngine.Object.DestroyImmediate(renderTexture);
                UnityEngine.Object.DestroyImmediate(image);
                camera.gameObject.SetActive(wasActive);
            }
        }

        private static void DisablePassiveGameplay(Transform passive)
        {
            string[] hidden = { "CollectingSystemVisual", "CollectingSystemCollision_Inward", "RouteGuide", "Stone", "StartAnchor", "TargetAnchor", "MinimapAnchor" };
            foreach (string name in hidden)
            {
                Transform node = FindDeep(passive, name);
                if (node != null) node.gameObject.SetActive(false);
            }
        }

        private static Vector3 CalculateRouteStartDirection(Transform routeGuide, Transform startAnchor, Transform targetAnchor)
        {
            if (routeGuide != null)
            {
                MeshFilter filter = routeGuide.GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh != null)
                {
                    Vector3 sum = Vector3.zero;
                    int count = 0;
                    foreach (Vector3 vertex in filter.sharedMesh.vertices)
                    {
                        Vector3 offset = routeGuide.TransformPoint(vertex) - startAnchor.position;
                        float distance = offset.magnitude;
                        if (distance > 0.018f && distance < 0.075f)
                        {
                            sum += offset.normalized;
                            count++;
                        }
                    }
                    if (count > 0 && sum.sqrMagnitude > 0.001f)
                        return sum.normalized;
                }
            }
            return (targetAnchor.position - startAnchor.position).normalized;
        }

        private static void SetNodeMaterialAndLayer(Transform root, string nodeName, Material material, string layerName)
        {
            Transform node = FindDeep(root, nodeName);
            if (node == null) throw new InvalidOperationException($"No obrigatorio ausente: {nodeName}");
            Renderer renderer = node.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = material;
            SetLayerRecursively(node.gameObject, LayerMask.NameToLayer(layerName));
        }

        private static void SetLayerRecursively(GameObject go, int layer)
        {
            if (layer < 0) return;
            go.layer = layer;
            foreach (Transform child in go.transform) SetLayerRecursively(child.gameObject, layer);
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform child in root)
            {
                Transform found = FindDeep(child, name);
                if (found != null) return found;
            }
            return null;
        }

        private static void ConfigureBuildSettings()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(MenuScenePath, true),
                new EditorBuildSettingsScene(GameScenePath, true)
            };
        }

        private static string ToAbsolute(string assetPath)
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
        }

        private static string Sha256(string path)
        {
            using FileStream stream = File.OpenRead(path);
            using SHA256 sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static bool Approximately(float a, float b) => Mathf.Abs(a - b) < 0.0001f;

        private static void Check(bool condition, string label, List<string> checks, List<string> errors)
        {
            if (condition) checks.Add("OK: " + label);
            else errors.Add("FALHA: " + label);
        }

        [Serializable]
        private sealed class ValidationReport
        {
            public string milestone;
            public string unityVersion;
            public string generatedUtc;
            public bool passed;
            public string fbxSha256;
            public string[] checks;
            public string[] errors;
        }
    }
}
