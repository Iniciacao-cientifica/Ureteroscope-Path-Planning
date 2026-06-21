using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

[InitializeOnLoad]
public static class MurilloVrSceneSetup
{
    private const string RoutePath = "Assets/StreamingAssets/vr_route_unity.json";
    private const string MeshPath = "Assets/Models/urinary_tract_unity.obj";
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
        ConfigureQuestProject();

        if (!File.Exists(RoutePath))
        {
            Debug.LogWarning($"Route file not found: {RoutePath}");
            return;
        }

        if (!File.Exists(MeshPath))
        {
            Debug.LogWarning($"Urinary tract mesh not found: {MeshPath}");
            return;
        }

        GameObject meshAsset = AssetDatabase.LoadAssetAtPath<GameObject>(MeshPath);
        if (meshAsset == null)
        {
            Debug.LogWarning($"Unity has not imported the mesh yet: {MeshPath}. Wait for import and run the menu again.");
            return;
        }

        Transform xrOrigin = EnsureXrRig();
        EnsureLight();
        EnsureLoader(meshAsset, xrOrigin);
        SaveSampleScene();
        EnsureSceneInBuildSettings();

        if (showDialog)
        {
            EditorUtility.DisplayDialog(
                "Murillo VR",
                "Quest-ready sample scene is prepared. Confirm OpenXR is enabled for Android in XR Plug-in Management, then press Play or build to the headset.",
                "OK"
            );
        }
    }

    [MenuItem("Murillo VR/Configure Quest Project")]
    public static void ConfigureQuestProject()
    {
        PlayerSettings.companyName = "Murillo Research";
        PlayerSettings.productName = "Murillo Ureteroscopy VR";
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "br.edu.murillo.ureteroscopyvr");
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });

        Debug.Log("Murillo VR Android/Quest player settings applied. Use XR Plug-in Management to enable OpenXR for Android if Unity has not created those settings yet.");
    }

    private static void AutoSetupWhenAssetsExist()
    {
        if (Object.FindAnyObjectByType<VrCaseLoader>() != null)
        {
            return;
        }

        if (!File.Exists(RoutePath) || !File.Exists(MeshPath))
        {
            return;
        }

        GameObject meshAsset = AssetDatabase.LoadAssetAtPath<GameObject>(MeshPath);
        if (meshAsset == null)
        {
            return;
        }

        Transform xrOrigin = EnsureXrRig();
        EnsureLight();
        EnsureLoader(meshAsset, xrOrigin);
        Debug.Log("Murillo VR sample scene was prepared automatically. Press Play to test it.");
    }

    private static void EnsureLoader(GameObject meshAsset, Transform xrOrigin)
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
        loader.urinaryTractMesh = meshAsset;
        loader.cameraRig = xrOrigin != null ? xrOrigin : Camera.main != null ? Camera.main.transform : null;
        loader.voxelToMeterScale = 0.002f;
        loader.meshOpacity = 0.35f;
        loader.enableQuestControllerInput = true;

        EditorUtility.SetDirty(loaderObject);
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
            origin.transform.position = new Vector3(0.55f, 0.35f, -0.55f);
            origin.transform.rotation = Quaternion.Euler(25f, -35f, 0f);
        }

        Camera camera = Camera.main;
        GameObject cameraObject = camera != null ? camera.gameObject : new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        cameraObject.transform.SetParent(origin.transform, false);
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

        if (cameraObject.GetComponent<XrHeadPoseDriver>() == null)
        {
            cameraObject.AddComponent<XrHeadPoseDriver>();
        }

        EditorUtility.SetDirty(origin);
        EditorUtility.SetDirty(cameraObject);
        return origin.transform;
    }

    private static void EnsureLight()
    {
        if (Object.FindAnyObjectByType<Light>() != null)
        {
            return;
        }

        GameObject lightObject = new GameObject("Directional Light");
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.2f;
        lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
    }
}
