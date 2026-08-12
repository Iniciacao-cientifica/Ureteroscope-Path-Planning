using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features.Interactions;

public static class QuestBuild
{
    private const string ScenePath = "Assets/Scenes/MurilloQuestSample.unity";
    private const string LoaderType = "UnityEngine.XR.OpenXR.OpenXRLoader";

    [MenuItem("Murillo VR/Configure OpenXR Android")]
    public static void ConfigureOpenXrAndroid()
    {
        XRGeneralSettingsPerBuildTarget perTarget = GetOrCreatePerBuildTargetSettings();
        if (!perTarget.HasSettingsForBuildTarget(BuildTargetGroup.Android))
        {
            perTarget.CreateDefaultSettingsForBuildTarget(BuildTargetGroup.Android);
        }
        if (!perTarget.HasManagerSettingsForBuildTarget(BuildTargetGroup.Android))
        {
            perTarget.CreateDefaultManagerSettingsForBuildTarget(BuildTargetGroup.Android);
        }

        XRManagerSettings manager = perTarget.ManagerSettingsForBuildTarget(BuildTargetGroup.Android);
        if (!XRPackageMetadataStore.AssignLoader(manager, LoaderType, BuildTargetGroup.Android))
        {
            bool alreadyAssigned = manager.activeLoaders.Any(loader => loader != null && loader.GetType().FullName == LoaderType);
            if (!alreadyAssigned)
            {
                throw new InvalidOperationException("Could not assign OpenXR loader to Android.");
            }
        }

        FeatureHelpers.RefreshFeatures(BuildTargetGroup.Android);
        OpenXRSettings settings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
        if (settings == null)
        {
            throw new InvalidOperationException("OpenXR Android settings were not created.");
        }

        EnableFeature(settings.GetFeature<OculusTouchControllerProfile>());
        EnableFeature(settings.GetFeature<MetaQuestTouchPlusControllerProfile>());
        EditorUtility.SetDirty(perTarget);
        EditorUtility.SetDirty(manager);
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
        Debug.Log("OpenXR Android loader and Meta Quest controller profiles are enabled.");
    }

    [MenuItem("Murillo VR/Build Quest APK")]
    public static void BuildApk()
    {
        ConfigurePlayer();
        ConfigureOpenXrAndroid();
        MurilloVrSceneSetup.SetupSampleSceneBatch();
        ValidateProject();

        if (!File.Exists(ScenePath))
        {
            throw new FileNotFoundException($"Build scene not found: {ScenePath}");
        }
        string catalog = Path.Combine(Application.dataPath, "StreamingAssets", "Cases", "catalog.json");
        if (!File.Exists(catalog))
        {
            throw new FileNotFoundException("No case catalog found. Run build_vr_case.ps1 first.", catalog);
        }

        string output = GetCommandLineValue("-murilloOutput");
        if (string.IsNullOrWhiteSpace(output))
        {
            output = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", "UreteroscopyVR.apk"));
        }
        Directory.CreateDirectory(Path.GetDirectoryName(output));

        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
        {
            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
            {
                throw new InvalidOperationException("Could not switch the active build target to Android.");
            }
        }

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = output,
            target = BuildTarget.Android,
            targetGroup = BuildTargetGroup.Android,
            options = BuildOptions.Development
        };
        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
        {
            throw new InvalidOperationException(
                $"Quest build failed: {report.summary.result}; errors={report.summary.totalErrors}."
            );
        }
        Debug.Log($"Quest APK created: {output} ({report.summary.totalSize} bytes)");
    }

    [MenuItem("Murillo VR/Validate Project")]
    public static void ValidateProject()
    {
        string casesRoot = Path.Combine(Application.dataPath, "StreamingAssets", "Cases");
        string catalogPath = Path.Combine(casesRoot, "catalog.json");
        if (!File.Exists(catalogPath))
        {
            throw new FileNotFoundException("Case catalog is missing. Run build_vr_case.ps1.", catalogPath);
        }

        VrCaseCatalog catalog = JsonUtility.FromJson<VrCaseCatalog>(File.ReadAllText(catalogPath));
        if (catalog?.cases == null || catalog.cases.Length == 0)
        {
            throw new InvalidOperationException("The case catalog is empty.");
        }
        foreach (VrCaseCatalogEntry entry in catalog.cases)
        {
            string manifestPath = Path.Combine(casesRoot, entry.manifest_file.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(manifestPath))
            {
                throw new FileNotFoundException($"Manifest is missing for {entry.case_id}.", manifestPath);
            }
            VrCaseManifest manifest = JsonUtility.FromJson<VrCaseManifest>(File.ReadAllText(manifestPath));
            if (manifest == null || manifest.schema_version != 2 || manifest.route_count < 1)
            {
                throw new InvalidOperationException($"Case {entry.case_id} has an invalid manifest.");
            }
            string caseDirectory = Path.GetDirectoryName(manifestPath);
            if (!File.Exists(Path.Combine(caseDirectory, manifest.files.anatomy)) ||
                !File.Exists(Path.Combine(caseDirectory, manifest.files.routes)))
            {
                throw new InvalidOperationException($"Case {entry.case_id} is missing anatomy or routes.");
            }
        }

        Mesh parserSmokeTest = VrObjParser.Parse(
            "v 0 0 0\nv 1 0 0\nv 0 1 0\nf 1 2 3\n",
            "OBJ Parser Smoke Test"
        );
        if (parserSmokeTest.vertexCount != 3 || parserSmokeTest.triangles.Length != 3)
        {
            throw new InvalidOperationException("Runtime OBJ parser smoke test failed.");
        }
        UnityEngine.Object.DestroyImmediate(parserSmokeTest);
        Debug.Log($"Project validation passed with {catalog.cases.Length} case(s).");
    }

    public static void ConfigurePlayer()
    {
        PlayerSettings.companyName = "Ureteroscopy Research";
        PlayerSettings.productName = "Ureteroscopy Planning VR";
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "br.edu.ureteroscopy.planningvr");
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });
    }

    private static XRGeneralSettingsPerBuildTarget GetOrCreatePerBuildTargetSettings()
    {
        if (EditorBuildSettings.TryGetConfigObject(
            XRGeneralSettings.k_SettingsKey,
            out XRGeneralSettingsPerBuildTarget existing
        ) && existing != null)
        {
            return existing;
        }

        MethodInfo getOrCreate = typeof(XRGeneralSettingsPerBuildTarget).GetMethod(
            "GetOrCreate",
            BindingFlags.Static | BindingFlags.NonPublic
        );
        XRGeneralSettingsPerBuildTarget created = getOrCreate?.Invoke(null, null) as XRGeneralSettingsPerBuildTarget;
        if (created == null)
        {
            throw new InvalidOperationException("Could not create XR Plug-in Management settings.");
        }
        return created;
    }

    private static void EnableFeature(UnityEngine.XR.OpenXR.Features.OpenXRFeature feature)
    {
        if (feature == null)
        {
            throw new InvalidOperationException("Required Meta Quest OpenXR interaction feature was not created.");
        }
        feature.enabled = true;
        EditorUtility.SetDirty(feature);
    }

    private static string GetCommandLineValue(string name)
    {
        string[] arguments = Environment.GetCommandLineArgs();
        for (int index = 0; index < arguments.Length - 1; index++)
        {
            if (string.Equals(arguments[index], name, StringComparison.OrdinalIgnoreCase))
            {
                return arguments[index + 1];
            }
        }
        return null;
    }
}
