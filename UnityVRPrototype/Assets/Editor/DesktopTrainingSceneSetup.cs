using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class DesktopTrainingSceneSetup
{
    public const string ScenePath = "Assets/Scenes/UreteroscopyDesktopTraining.unity";

    [MenuItem("Murillo VR/Setup Desktop Training Scene")]
    public static void SetupFromMenu()
    {
        SetupDesktopTrainingScene(true);
    }

    public static void SetupDesktopTrainingSceneBatch()
    {
        SetupDesktopTrainingScene(false);
    }

    private static void SetupDesktopTrainingScene(bool showDialog)
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "Ureteroscopy Desktop Training";

        GameObject loaderObject = new GameObject("Training Case Loader");
        VrCaseLoader loader = loaderObject.AddComponent<VrCaseLoader>();
        loader.catalogRelativePath = "Cases/catalog.json";
        loader.frameCaseOnLoad = false;
        loader.enableVrRuntimeObjects = false;
        loader.enableInformationPanel = false;
        loader.enableQuestControllerInput = false;
        loader.maximumTabletopSize = 10f;

        GameObject cameraObject = new GameObject("Endoscopic Camera");
        cameraObject.tag = "MainCamera";
        Camera camera = cameraObject.AddComponent<Camera>();
        cameraObject.AddComponent<AudioListener>();

        GameObject minimapObject = new GameObject("Minimap Camera");
        Camera minimap = minimapObject.AddComponent<Camera>();
        minimap.enabled = true;

        GameObject controllerObject = new GameObject("Desktop Training Controller");
        UreteroscopyTrainingController controller = controllerObject.AddComponent<UreteroscopyTrainingController>();
        controller.caseLoader = loader;
        controller.endoscopeCamera = camera;
        controller.minimapCamera = minimap;
        controller.difficulty = TrainingDifficulty.Tutorial;
        controller.inputMode = TrainingInputMode.Keyboard;
        controller.serialPort = "AUTO";

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.025f, 0.025f, 0.03f);
        EnsureRuntimeShaders();
        Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
        EditorSceneManager.SaveScene(scene, ScenePath);
        EnsureSceneInBuildSettings();
        AssetDatabase.SaveAssets();

        if (showDialog)
        {
            EditorUtility.DisplayDialog(
                "Treinamento desktop",
                "Cena criada. Pressione Play, escolha teclado ou vareta USB, calibre e inicie o treinamento.",
                "OK"
            );
        }
        Debug.Log("Desktop training scene prepared at " + ScenePath);
    }

    [MenuItem("Murillo VR/Build Desktop Training (Windows)")]
    public static void BuildWindowsFromMenu()
    {
        BuildWindows();
    }

    public static void BuildWindowsBatch()
    {
        BuildWindows();
    }

    private static void BuildWindows()
    {
        if (!File.Exists(ScenePath)) SetupDesktopTrainingScene(false);
        EnsureRuntimeShaders();
        string output = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", "Desktop", "UreteroscopyTraining.exe"));
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = output,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };
        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
        {
            throw new InvalidOperationException($"Desktop build failed: {report.summary.result} ({report.summary.totalErrors} errors)");
        }
        Debug.Log("Desktop training build created at " + output);
    }

    private static void EnsureSceneInBuildSettings()
    {
        EditorBuildSettingsScene[] current = EditorBuildSettings.scenes;
        foreach (EditorBuildSettingsScene item in current)
        {
            if (item.path == ScenePath)
            {
                item.enabled = true;
                EditorBuildSettings.scenes = current;
                return;
            }
        }
        Array.Resize(ref current, current.Length + 1);
        current[current.Length - 1] = new EditorBuildSettingsScene(ScenePath, true);
        EditorBuildSettings.scenes = current;
    }

    private static void EnsureRuntimeShaders()
    {
        GraphicsSettings settings = AssetDatabase.LoadAssetAtPath<GraphicsSettings>("ProjectSettings/GraphicsSettings.asset");
        if (settings == null) throw new InvalidOperationException("Could not load GraphicsSettings.asset.");
        SerializedObject serialized = new SerializedObject(settings);
        SerializedProperty included = serialized.FindProperty("m_AlwaysIncludedShaders");
        string[] shaderNames = { "Standard", "Sprites/Default" };
        foreach (string shaderName in shaderNames)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader == null) throw new InvalidOperationException($"Required runtime shader is unavailable: {shaderName}");
            bool present = false;
            for (int index = 0; index < included.arraySize; index++)
            {
                if (included.GetArrayElementAtIndex(index).objectReferenceValue == shader)
                {
                    present = true;
                    break;
                }
            }
            if (!present)
            {
                included.InsertArrayElementAtIndex(included.arraySize);
                included.GetArrayElementAtIndex(included.arraySize - 1).objectReferenceValue = shader;
            }
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
    }
}
