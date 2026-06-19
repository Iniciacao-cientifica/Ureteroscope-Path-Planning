using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class MurilloVrSceneSetup
{
    private const string RoutePath = "Assets/StreamingAssets/vr_route_unity.json";
    private const string MeshPath = "Assets/Models/urinary_tract_unity.obj";

    static MurilloVrSceneSetup()
    {
        EditorApplication.delayCall += AutoSetupWhenAssetsExist;
    }

    [MenuItem("Murillo VR/Setup Sample Scene")]
    public static void SetupSampleScene()
    {
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

        EnsureCamera();
        EnsureLight();
        EnsureLoader(meshAsset);

        EditorUtility.DisplayDialog(
            "Murillo VR",
            "Sample scene is ready. Press Play to test the route visualizer.",
            "OK"
        );
    }

    private static void AutoSetupWhenAssetsExist()
    {
        if (Object.FindObjectOfType<VrCaseLoader>() != null)
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

        EnsureCamera();
        EnsureLight();
        EnsureLoader(meshAsset);
        Debug.Log("Murillo VR sample scene was prepared automatically. Press Play to test it.");
    }

    private static void EnsureLoader(GameObject meshAsset)
    {
        VrCaseLoader existingLoader = Object.FindObjectOfType<VrCaseLoader>();
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
        loader.cameraRig = Camera.main != null ? Camera.main.transform : null;
        loader.voxelToMeterScale = 0.002f;
        loader.meshOpacity = 0.35f;

        EditorUtility.SetDirty(loaderObject);
    }

    private static void EnsureCamera()
    {
        if (Camera.main != null)
        {
            return;
        }

        GameObject cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.Skybox;
        cameraObject.AddComponent<AudioListener>();
        cameraObject.transform.position = new Vector3(0.55f, 0.35f, -0.55f);
        cameraObject.transform.rotation = Quaternion.Euler(25f, -35f, 0f);
    }

    private static void EnsureLight()
    {
        if (Object.FindObjectOfType<Light>() != null)
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
