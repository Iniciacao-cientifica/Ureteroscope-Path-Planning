using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR;
using Unity.XR.CoreUtils;

[InitializeOnLoad]
public static class MurilloVrSceneSetup
{
    private const string RoutePath = "Assets/StreamingAssets/vr_route_unity.json";
    private const string MeshPath = "Assets/Models/urinary_tract_unity.obj";
    private const string CatalogPath = "Assets/StreamingAssets/Cases/catalog.json";
    private const string ScenePath = "Assets/Scenes/MurilloQuestSample.unity";
    private const string XrOriginName = "Murillo XR Origin";

    static MurilloVrSceneSetup()
    {
        EditorApplication.delayCall += AutoSetupWhenAssetsExist;
    }

    [MenuItem("Murillo VR/Setup Sample Scene")]
    public static void SetupSampleScene()
    {
        SetupSampleSceneInternal(true);
    }

    public static void SetupSampleSceneBatch()
    {
        SetupSampleSceneInternal(false);
    }

    private static void SetupSampleSceneInternal(bool showDialog)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Exit Play Mode before setting up the VR scene.");
            return;
        }
        ConfigureQuestProject();

        bool hasCatalog = File.Exists(CatalogPath);
        bool hasLegacyCase = File.Exists(RoutePath) && File.Exists(MeshPath);
        if (!hasCatalog && !hasLegacyCase)
        {
            Debug.LogWarning("No VR case found. Run build_vr_case.ps1 before setting up the scene.");
            return;
        }

        GameObject meshAsset = hasLegacyCase ? AssetDatabase.LoadAssetAtPath<GameObject>(MeshPath) : null;

        Transform xrOrigin = EnsureXrRig();
        EnsureLight();
        VrCaseLoader loader = EnsureLoader(meshAsset, xrOrigin);
        EnsureWorldMenu(loader);
        SaveSampleScene();
        EnsureSceneInBuildSettings();

        if (showDialog)
        {
            EditorUtility.DisplayDialog(
                "Murillo VR",
                "Quest-ready scene is prepared. Use Murillo VR > Configure OpenXR Android, then test in Play Mode or build the APK.",
                "OK"
            );
        }
    }

    [MenuItem("Murillo VR/Configure Quest Project")]
    public static void ConfigureQuestProject()
    {
        QuestBuild.ConfigurePlayer();
        Debug.Log("Android ARM64, IL2CPP, Vulkan, and application settings applied.");
    }

    private static void AutoSetupWhenAssetsExist()
    {
        if (Application.isBatchMode)
        {
            return;
        }
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }
        if (Object.FindAnyObjectByType<VrCaseLoader>() != null)
        {
            return;
        }

        bool hasCatalog = File.Exists(CatalogPath);
        bool hasLegacyCase = File.Exists(RoutePath) && File.Exists(MeshPath);
        if (!hasCatalog && !hasLegacyCase)
        {
            return;
        }

        GameObject meshAsset = hasLegacyCase ? AssetDatabase.LoadAssetAtPath<GameObject>(MeshPath) : null;

        Transform xrOrigin = EnsureXrRig();
        EnsureLight();
        VrCaseLoader loader = EnsureLoader(meshAsset, xrOrigin);
        EnsureWorldMenu(loader);
        Debug.Log("Murillo VR sample scene was prepared automatically. Press Play to test it.");
    }

    private static VrCaseLoader EnsureLoader(GameObject meshAsset, Transform xrOrigin)
    {
        VrCaseLoader existingLoader = Object.FindAnyObjectByType<VrCaseLoader>();
        GameObject loaderObject = existingLoader != null
            ? existingLoader.gameObject
            : new GameObject("VR Case Loader");

        VrCaseLoader loader = loaderObject.GetComponent<VrCaseLoader>();
        if (loader == null)
        {
            loader = loaderObject.AddComponent<VrCaseLoader>();
        }

        loader.routeFileName = "vr_route_unity.json";
        loader.catalogRelativePath = "Cases/catalog.json";
        loader.urinaryTractMesh = meshAsset;
        loader.cameraRig = xrOrigin != null ? xrOrigin : Camera.main != null ? Camera.main.transform : null;
        loader.voxelToMeterScale = 0.002f;
        loader.meshOpacity = 0.35f;
        loader.renderSmoothedPathAsTube = true;
        loader.routeTubeSides = 8;
        loader.followMarkerRadius = 0.018f;
        loader.enableQuestControllerInput = true;

        EditorUtility.SetDirty(loaderObject);
        return loader;
    }

    private static void EnsureWorldMenu(VrCaseLoader loader)
    {
        VrWorldMenu existing = Object.FindAnyObjectByType<VrWorldMenu>();
        GameObject menuObject = existing != null ? existing.gameObject : new GameObject("VR World Menu");
        VrWorldMenu menu = menuObject.GetComponent<VrWorldMenu>();
        if (menu == null)
        {
            menu = menuObject.AddComponent<VrWorldMenu>();
        }
        menu.loader = loader;
        EditorUtility.SetDirty(menuObject);
    }

    private static void SaveSampleScene()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
    }

    private static void EnsureSceneInBuildSettings()
    {
        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
        foreach (EditorBuildSettingsScene scene in scenes)
        {
            if (scene.path == ScenePath)
            {
                scene.enabled = true;
                EditorBuildSettings.scenes = scenes;
                return;
            }
        }

        EditorBuildSettingsScene[] updatedScenes = new EditorBuildSettingsScene[scenes.Length + 1];
        for (int i = 0; i < scenes.Length; i++)
        {
            updatedScenes[i] = scenes[i];
        }

        updatedScenes[updatedScenes.Length - 1] = new EditorBuildSettingsScene(ScenePath, true);
        EditorBuildSettings.scenes = updatedScenes;
    }

    private static Transform EnsureXrRig()
    {
        GameObject origin = GameObject.Find(XrOriginName);
        if (origin == null)
        {
            origin = new GameObject(XrOriginName);
            origin.transform.position = Vector3.zero;
            origin.transform.rotation = Quaternion.identity;
        }

        Transform cameraOffset = origin.transform.Find("Camera Offset");
        if (cameraOffset == null)
        {
            GameObject offsetObject = new GameObject("Camera Offset");
            cameraOffset = offsetObject.transform;
            cameraOffset.SetParent(origin.transform, false);
        }

        Camera camera = Camera.main;
        GameObject cameraObject = camera != null ? camera.gameObject : new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        cameraObject.transform.SetParent(cameraOffset, false);
        cameraObject.transform.localPosition = Vector3.zero;
        cameraObject.transform.localRotation = Quaternion.identity;

        if (camera == null)
        {
            camera = cameraObject.AddComponent<Camera>();
        }

        camera.clearFlags = CameraClearFlags.Skybox;
        camera.nearClipPlane = 0.01f;

        if (cameraObject.GetComponent<AudioListener>() == null)
        {
            cameraObject.AddComponent<AudioListener>();
        }

        XrHeadPoseDriver headDriver = cameraObject.GetComponent<XrHeadPoseDriver>();
        if (headDriver == null)
        {
            headDriver = cameraObject.AddComponent<XrHeadPoseDriver>();
        }
        headDriver.trackedNode = XRNode.CenterEye;

        XROrigin xrOrigin = origin.GetComponent<XROrigin>();
        if (xrOrigin == null)
        {
            xrOrigin = origin.AddComponent<XROrigin>();
        }
        xrOrigin.Camera = camera;
        xrOrigin.CameraFloorOffsetObject = cameraOffset.gameObject;
        xrOrigin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;
        xrOrigin.CameraYOffset = 0f;

        EnsureController(cameraOffset, "Left Controller", XRNode.LeftHand);
        EnsureController(cameraOffset, "Right Controller", XRNode.RightHand);

        EditorUtility.SetDirty(origin);
        EditorUtility.SetDirty(cameraObject);
        return origin.transform;
    }

    private static void EnsureController(Transform parent, string name, XRNode node)
    {
        Transform controller = parent.Find(name);
        if (controller == null)
        {
            GameObject controllerObject = new GameObject(name);
            controller = controllerObject.transform;
            controller.SetParent(parent, false);
        }
        XrHeadPoseDriver poseDriver = controller.GetComponent<XrHeadPoseDriver>();
        if (poseDriver == null)
        {
            poseDriver = controller.gameObject.AddComponent<XrHeadPoseDriver>();
        }
        poseDriver.trackedNode = node;
        VrControllerRay ray = controller.GetComponent<VrControllerRay>();
        if (ray == null)
        {
            ray = controller.gameObject.AddComponent<VrControllerRay>();
        }
        ray.controllerNode = node;
        EditorUtility.SetDirty(controller.gameObject);
    }

    private static void EnsureLight()
    {
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.38f, 0.4f, 0.44f);

        EnsureDirectionalLight("Murillo Key Light", 1.15f, Color.white, Quaternion.Euler(48f, -32f, 0f));
        EnsureDirectionalLight("Murillo Fill Light", 0.45f, new Color(0.55f, 0.65f, 1f), Quaternion.Euler(-25f, 130f, 0f));
    }

    private static void EnsureDirectionalLight(string lightName, float intensity, Color color, Quaternion rotation)
    {
        GameObject lightObject = GameObject.Find(lightName);
        if (lightObject == null)
        {
            lightObject = new GameObject(lightName);
        }

        Light light = lightObject.GetComponent<Light>();
        if (light == null)
        {
            light = lightObject.AddComponent<Light>();
        }

        light.type = LightType.Directional;
        light.intensity = intensity;
        light.color = color;
        light.shadows = LightShadows.None;
        lightObject.transform.rotation = rotation;
        EditorUtility.SetDirty(lightObject);
    }
}
