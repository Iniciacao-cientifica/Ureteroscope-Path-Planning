using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NavegacaoRenal.Editor
{
    public static class Marco3ProjectSetup
    {
        private const string GameScenePath = "Assets/Scenes/KidneyGame.unity";
        private const string ModelPath = "Assets/Art/Kidney/Models/Kidney_Game_v002.fbx";
        private const string ExpectedModelHash = "174fabbf6ec31b3052360be995b5bbc4fb7e074b91ef2a2bba838ca45cc0fa9c";
        private const string MucosaBaseColorPath = "Assets/Art/Textures/Organic/T_RenalMucosa_BaseColor_v001.png";
        private const string MucosaNormalPath = "Assets/Art/Textures/Organic/T_RenalMucosa_NormalSource_v001.png";

        [MenuItem("Navegacao Renal/Construir Marco 3")]
        public static void Configure()
        {
            Debug.Log("[Marco3] Gerando cenas com o controlador SphereCast.");
            Marco2ProjectSetup.Configure();
            Physics.queriesHitBackfaces = true;
            AssetDatabase.SaveAssets();
            Validate();
            CapturePreviews();
            AssetDatabase.Refresh();
            Debug.Log("[Marco3] Configuracao, validacao e previews concluidos.");
        }

        [MenuItem("Navegacao Renal/Validar Marco 3")]
        public static void Validate()
        {
            List<string> checks = new List<string>();
            List<string> errors = new List<string>();
            Scene scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            Physics.queriesHitBackfaces = true;
            Physics.SyncTransforms();

            MouseEndoscopeController controller = FindSceneComponent<MouseEndoscopeController>(scene, "ProbeTip");
            KidneyGameManager manager = FindSceneComponent<KidneyGameManager>(scene, "KidneyGameManager");
            Transform startAnchor = FindSceneTransform(scene, "StartAnchor");
            GameObject realisticRig = FindSceneObject(scene, "RealisticRig");
            GameObject explorationRig = FindSceneObject(scene, "ExplorationRig");
            Camera endoscopeCamera = FindSceneComponent<Camera>(scene, "EndoscopeCamera");
            Transform collisionNode = FindSceneTransform(scene, "CollectingSystemCollision_Inward");
            Transform activeExterior = FindSceneTransformUnder(scene, "KidneyLevel_Active", "KidneyExterior");
            Transform passiveExterior = FindSceneTransformUnder(scene, "KidneyLevel_Passive", "KidneyExterior");
            Transform collectingVisual = FindSceneTransformUnder(scene, "KidneyLevel_Active", "CollectingSystemVisual");
            Transform leftUreter = FindSceneTransform(scene, "LeftUreter");
            Transform rightUreter = FindSceneTransform(scene, "RightUreter");
            Transform bladder = FindSceneTransform(scene, "Bladder");
            Transform bladderOutlet = FindSceneTransform(scene, "BladderOutlet");
            int collisionLayer = LayerMask.NameToLayer("KidneyCollision");
            int collisionMask = collisionLayer >= 0 ? 1 << collisionLayer : 0;

            Check(controller != null, "MouseEndoscopeController preservado na ponta", checks, errors);
            Check(manager != null, "KidneyGameManager presente", checks, errors);
            Check(startAnchor != null, "StartAnchor presente", checks, errors);
            Check(realisticRig != null && explorationRig != null, "modos Realista e Exploracao presentes", checks, errors);
            Check(collisionLayer >= 0, "camada KidneyCollision presente", checks, errors);
            Check(Physics.queriesHitBackfaces, "consultas a faces internas habilitadas", checks, errors);
            Check(FindAllComponents<CharacterController>(scene).Length == 0, "CharacterController removido", checks, errors);
            Check(CountMissingScripts(scene) == 0, "nenhum script ausente na cena", checks, errors);
            ValidateVisualRevision(activeExterior, passiveExterior, collectingVisual, leftUreter, rightUreter, bladder, bladderOutlet, checks, errors);

            if (controller != null)
            {
                Check(Approximately(controller.TipRadius, 0.010f), "raio da ponta = 0,010 m (2 mm fisicos em 5x)", checks, errors);
                Check(Approximately(controller.ForwardSpeed, 0.10f), "velocidade = 0,10 m/s (20 mm/s fisicos)", checks, errors);
                Check(Approximately(controller.MaximumSubstepDistance, 0.005f), "subpasso maximo = 0,005 m", checks, errors);
                Check(Approximately(controller.CollisionSkin, 0.001f), "margem de seguranca = 0,001 m", checks, errors);
                Check(Approximately(controller.ContactRearmRadius, 0.015f), "rearme do toque = 0,015 m", checks, errors);
                Check(Approximately(controller.MaximumSteeringSpeed, 70f), "direcao limitada a 70 graus/s", checks, errors);
                Check(Approximately(controller.SteeringSmoothTime, 0.12f), "suavizacao da direcao = 0,12 s", checks, errors);
                Check(Approximately(controller.RollSpeed, 55f), "rolamento Q/E = 55 graus/s", checks, errors);
                Check(controller.CollisionMask == collisionMask, "SphereCast consulta somente KidneyCollision", checks, errors);
            }

            MeshCollider meshCollider = collisionNode != null ? collisionNode.GetComponent<MeshCollider>() : null;
            Check(collisionNode != null && collisionNode.gameObject.layer == collisionLayer,
                "malha interna usa a camada KidneyCollision", checks, errors);
            Check(meshCollider != null && !meshCollider.convex && meshCollider.enabled,
                "MeshCollider interno nao convexo e ativo", checks, errors);

            if (endoscopeCamera != null)
            {
                int exteriorLayer = LayerMask.NameToLayer("KidneyExterior");
                Check(Approximately(endoscopeCamera.fieldOfView, 80f), "camera interna com FOV 80", checks, errors);
                Check(exteriorLayer >= 0 && (endoscopeCamera.cullingMask & (1 << exteriorLayer)) == 0,
                    "camera interna exclui o exterior", checks, errors);
                Check(endoscopeCamera.GetComponent<Light>() != null, "luz da ponta preservada", checks, errors);
            }
            else Check(false, "camera interna presente", checks, errors);

            ValidateFrameRateEquivalence(checks, errors);
            if (controller != null && manager != null && startAnchor != null && collisionMask != 0)
                ValidateCollisionAndContactLatch(controller, manager, startAnchor, collisionMask, checks, errors);

            if (manager != null && realisticRig != null && explorationRig != null && startAnchor != null && controller != null)
                ValidateModesPauseAndReset(manager, controller, startAnchor, realisticRig, explorationRig, checks, errors);

            string modelHash = File.Exists(ToAbsolute(ModelPath)) ? Sha256(ToAbsolute(ModelPath)) : string.Empty;
            Check(string.Equals(modelHash, ExpectedModelHash, StringComparison.OrdinalIgnoreCase),
                "FBX v002 permaneceu inalterado", checks, errors);

            ValidationReport report = new ValidationReport
            {
                milestone = "Marco 3",
                unityVersion = Application.unityVersion,
                generatedUtc = DateTime.UtcNow.ToString("O"),
                passed = errors.Count == 0,
                fbxSha256 = modelHash,
                controller = "SphereCast substep controller",
                checks = checks.ToArray(),
                errors = errors.ToArray()
            };

            string reportPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../Documentation/marco3_validation.json"));
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
            File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
            Debug.Log($"[Marco3] Relatorio: {reportPath}\n{JsonUtility.ToJson(report, true)}");

            if (errors.Count > 0)
                throw new InvalidOperationException("Marco 3 falhou: " + string.Join(" | ", errors));
        }

        private static void ValidateFrameRateEquivalence(List<string> checks, List<string> errors)
        {
            float[] distances = new float[3];
            int[] frameRates = { 30, 60, 120 };
            for (int index = 0; index < frameRates.Length; index++)
            {
                GameObject temporary = new GameObject("Marco3_FrameRateProbe");
                MouseEndoscopeController testController = temporary.AddComponent<MouseEndoscopeController>();
                testController.Configure(null, 0);
                Vector3 origin = temporary.transform.position;
                for (int frame = 0; frame < frameRates[index]; frame++)
                    testController.TryMoveDistance(testController.ForwardSpeed / frameRates[index]);
                distances[index] = Vector3.Distance(origin, temporary.transform.position);
                UnityEngine.Object.DestroyImmediate(temporary);
            }

            float spread = distances.Max() - distances.Min();
            Check(distances.All(distance => Mathf.Abs(distance - 0.10f) < 0.0001f) && spread < 0.00001f,
                $"deslocamento equivalente em 30/60/120 FPS ({distances[0]:F6}/{distances[1]:F6}/{distances[2]:F6} m)",
                checks, errors);

            GameObject reverseObject = new GameObject("Marco3_ReverseProbe");
            MouseEndoscopeController reverseController = reverseObject.AddComponent<MouseEndoscopeController>();
            reverseController.Configure(null, 0);
            Vector3 reverseOrigin = reverseObject.transform.position;
            reverseController.TryMoveDistance(0.10f);
            reverseController.TryMoveDistance(-0.10f);
            Check(Vector3.Distance(reverseOrigin, reverseObject.transform.position) < 0.0001f,
                "avanco e recuo usam a mesma integracao", checks, errors);
            UnityEngine.Object.DestroyImmediate(reverseObject);
        }

        private static void ValidateVisualRevision(
            Transform activeExterior,
            Transform passiveExterior,
            Transform collectingVisual,
            Transform leftUreter,
            Transform rightUreter,
            Transform bladder,
            Transform bladderOutlet,
            List<string> checks,
            List<string> errors)
        {
            Texture2D baseColor = AssetDatabase.LoadAssetAtPath<Texture2D>(MucosaBaseColorPath);
            Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(MucosaNormalPath);
            TextureImporter normalImporter = AssetImporter.GetAtPath(MucosaNormalPath) as TextureImporter;
            Check(baseColor != null, "textura organica interna presente", checks, errors);
            Check(normal != null && normalImporter != null && normalImporter.textureType == TextureImporterType.NormalMap,
                "normal map organico importado corretamente", checks, errors);

            Material activeMaterial = activeExterior != null ? activeExterior.GetComponent<Renderer>()?.sharedMaterial : null;
            Material passiveMaterial = passiveExterior != null ? passiveExterior.GetComponent<Renderer>()?.sharedMaterial : null;
            Material interiorMaterial = collectingVisual != null ? collectingVisual.GetComponent<Renderer>()?.sharedMaterial : null;
            Color approvedActiveColor = new Color(0.42f, 0.055f, 0.10f, 0.48f);
            Check(activeMaterial != null &&
                  activeMaterial.name == "MAT_KidneyExterior_URP" &&
                  AssetDatabase.GetAssetPath(activeMaterial) == "Assets/Materials/MAT_KidneyExterior_URP.mat" &&
                  Approximately(activeMaterial.color.r, approvedActiveColor.r) &&
                  Approximately(activeMaterial.color.g, approvedActiveColor.g) &&
                  Approximately(activeMaterial.color.b, approvedActiveColor.b) &&
                  Approximately(activeMaterial.color.a, approvedActiveColor.a) &&
                  Approximately(activeMaterial.GetFloat("_Smoothness"), 0.58f) &&
                  Approximately(activeMaterial.GetFloat("_Surface"), 1f),
                "rim superior esquerdo preserva o material aprovado", checks, errors);
            Check(passiveMaterial != null && passiveMaterial.name == "MAT_KidneyRight_URP" &&
                  Approximately(passiveMaterial.GetFloat("_Surface"), 0f),
                "rim direito fechado usa material organico proprio", checks, errors);
            Check(interiorMaterial != null && interiorMaterial.GetTexture("_BaseMap") == baseColor &&
                  interiorMaterial.GetTexture("_BumpMap") == normal,
                "parede interna usa cor e relevo organicos", checks, errors);

            Check(MeshVertexCount(leftUreter) >= 900 && MeshVertexCount(rightUreter) >= 900,
                "ureteres suavizados com alta densidade", checks, errors);
            Check(MeshVertexCount(bladder) >= 1000, "bexiga organica suavizada", checks, errors);
            Check(bladderOutlet != null && MeshVertexCount(bladderOutlet) > 0,
                "saida inferior da bexiga criada", checks, errors);
        }

        private static int MeshVertexCount(Transform transform)
        {
            MeshFilter filter = transform != null ? transform.GetComponent<MeshFilter>() : null;
            return filter != null && filter.sharedMesh != null ? filter.sharedMesh.vertexCount : 0;
        }

        private static void ValidateCollisionAndContactLatch(
            MouseEndoscopeController controller,
            KidneyGameManager manager,
            Transform startAnchor,
            int collisionMask,
            List<string> checks,
            List<string> errors)
        {
            controller.ResetTo(startAnchor);
            Physics.SyncTransforms();
            bool wallFound = TryFindWallDirection(
                controller.transform.position,
                controller.TipRadius,
                collisionMask,
                out Vector3 direction,
                out RaycastHit expectedHit);
            Check(wallFound, "SphereCast interno encontra a parede a partir do StartAnchor", checks, errors);
            if (!wallFound)
                return;

            controller.transform.rotation = SafeLookRotation(direction);
            Vector3 origin = controller.transform.position;
            int contactsBefore = manager.WallContacts;
            bool completedLargeMove = controller.TryMoveDistance(2f);
            float travelled = Vector3.Distance(origin, controller.transform.position);
            Check(!completedLargeMove && travelled <= expectedHit.distance + 0.0001f,
                $"deslocamento grande bloqueado sem atravessar ({travelled:F6} m antes da parede)", checks, errors);
            Check(controller.IsWallContactLatched && manager.WallContacts == contactsBefore + 1,
                "primeiro contato conta exatamente um toque", checks, errors);

            controller.TryMoveDistance(0.10f);
            Check(manager.WallContacts == contactsBefore + 1,
                "pressao continua contra a mesma parede nao repete o toque", checks, errors);

            controller.transform.position = origin;
            Physics.SyncTransforms();
            controller.TryMoveDistance(0f);
            Check(!controller.IsWallContactLatched, "toque rearma apos nao haver parede em 0,015 m", checks, errors);

            controller.transform.rotation = SafeLookRotation(direction);
            controller.TryMoveDistance(2f);
            Check(manager.WallContacts == contactsBefore + 2,
                "novo contato apos afastamento conta outro toque", checks, errors);
        }

        private static void ValidateModesPauseAndReset(
            KidneyGameManager manager,
            MouseEndoscopeController controller,
            Transform startAnchor,
            GameObject realisticRig,
            GameObject explorationRig,
            List<string> checks,
            List<string> errors)
        {
            manager.SetPaused(true);
            Check(manager.IsPaused && !manager.CanNavigate, "pausa interrompe a navegacao", checks, errors);
            manager.SetPaused(false);

            manager.SetMode(KidneyGameMode.Exploration);
            Check(!realisticRig.activeSelf && explorationRig.activeSelf && !manager.CanNavigate,
                "F2/modo Exploracao libera a camera externa", checks, errors);
            manager.SetMode(KidneyGameMode.Realistic);
            Check(realisticRig.activeSelf && !explorationRig.activeSelf && manager.CanNavigate,
                "F1/modo Realista restaura a navegacao interna", checks, errors);

            controller.transform.position += Vector3.one;
            manager.ResetAttempt();
            Check(Vector3.Distance(controller.transform.position, startAnchor.position) < 0.00001f &&
                  Quaternion.Angle(controller.transform.rotation, startAnchor.rotation) < 0.01f,
                "reset reposiciona diretamente no StartAnchor e na tangente inicial", checks, errors);

            MouseEndoscopeController.ReleaseCursor();
            Check(Cursor.lockState == CursorLockMode.None && Cursor.visible,
                "rotina de liberacao do cursor deixa cursor visivel", checks, errors);
        }

        private static bool TryFindWallDirection(
            Vector3 origin,
            float radius,
            int collisionMask,
            out Vector3 bestDirection,
            out RaycastHit bestHit)
        {
            bestDirection = Vector3.forward;
            bestHit = default;
            float bestDistance = float.PositiveInfinity;
            const int samples = 96;
            float goldenAngle = Mathf.PI * (3f - Mathf.Sqrt(5f));

            for (int index = 0; index < samples; index++)
            {
                float y = 1f - 2f * (index + 0.5f) / samples;
                float radial = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
                float angle = goldenAngle * index;
                Vector3 direction = new Vector3(Mathf.Cos(angle) * radial, y, Mathf.Sin(angle) * radial);
                if (Physics.SphereCast(origin, radius, direction, out RaycastHit hit, 3f, collisionMask, QueryTriggerInteraction.Ignore) &&
                    hit.distance < bestDistance)
                {
                    bestDistance = hit.distance;
                    bestDirection = direction;
                    bestHit = hit;
                }
            }
            return bestDistance < float.PositiveInfinity;
        }

        private static Quaternion SafeLookRotation(Vector3 direction)
        {
            Vector3 up = Mathf.Abs(Vector3.Dot(direction.normalized, Vector3.up)) > 0.98f ? Vector3.forward : Vector3.up;
            return Quaternion.LookRotation(direction.normalized, up);
        }

        private static void CapturePreviews()
        {
            Scene scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            Camera camera = FindSceneComponent<Camera>(scene, "EndoscopeCamera");
            Camera explorationCamera = FindSceneComponent<Camera>(scene, "ExplorationCamera");
            GameObject route = FindSceneObject(scene, "RouteGuide");
            string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Documentation/Previews"));
            Directory.CreateDirectory(directory);

            CaptureCamera(explorationCamera, Path.Combine(directory, "marco3_visual_system.png"));
            if (route != null) route.SetActive(false);
            CaptureCamera(camera, Path.Combine(directory, "marco3_realistic_route_off.png"));
            if (route != null) route.SetActive(true);
            CaptureCamera(camera, Path.Combine(directory, "marco3_realistic_route_on.png"));
            if (route != null) route.SetActive(false);
        }

        private static void CaptureCamera(Camera camera, string outputPath)
        {
            if (camera == null) throw new InvalidOperationException("Camera interna nao encontrada para preview.");
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
                Debug.Log("[Marco3] Preview criado: " + outputPath);
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

        private static int CountMissingScripts(Scene scene)
        {
            int count = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
                count += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject);
            return count;
        }

        private static T[] FindAllComponents<T>(Scene scene) where T : Component =>
            scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();

        private static T FindSceneComponent<T>(Scene scene, string objectName) where T : Component =>
            FindAllComponents<T>(scene).FirstOrDefault(component => component.name == objectName);

        private static GameObject FindSceneObject(Scene scene, string objectName)
        {
            Transform transform = FindSceneTransform(scene, objectName);
            return transform != null ? transform.gameObject : null;
        }

        private static Transform FindSceneTransform(Scene scene, string objectName) =>
            scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(transform => transform.name == objectName);

        private static Transform FindSceneTransformUnder(Scene scene, string parentName, string childName)
        {
            Transform parent = FindSceneTransform(scene, parentName);
            return parent == null
                ? null
                : parent.GetComponentsInChildren<Transform>(true).FirstOrDefault(transform => transform.name == childName);
        }

        private static string ToAbsolute(string assetPath) =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));

        private static string Sha256(string path)
        {
            using FileStream stream = File.OpenRead(path);
            using System.Security.Cryptography.SHA256 sha = System.Security.Cryptography.SHA256.Create();
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
            public string controller;
            public string[] checks;
            public string[] errors;
        }
    }
}
