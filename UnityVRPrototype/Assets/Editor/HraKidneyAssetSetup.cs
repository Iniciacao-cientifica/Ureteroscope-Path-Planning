using UnityEditor;
using UnityEngine;

public static class HraKidneyAssetSetup
{
    private static readonly string[] UrinarySystemPaths =
    {
        "Assets/Resources/HRAKidneys/VH_M_Kidney_L.glb",
        "Assets/Resources/HRAKidneys/VH_M_Kidney_R.glb",
        "Assets/Resources/HRAKidneys/VH_M_Ureter_L.glb",
        "Assets/Resources/HRAKidneys/VH_M_Ureter_R.glb",
        "Assets/Resources/HRAKidneys/VH_M_Urinary_Bladder.glb"
    };

    [InitializeOnLoadMethod]
    private static void ScheduleImportCheck()
    {
        EditorApplication.delayCall += EnsureUrinarySystemUsesGltfImporter;
    }

    [MenuItem("Murilo VR/Reimportar sistema urinário HRA")]
    public static void EnsureUrinarySystemUsesGltfImporter()
    {
        foreach (string path in UrinarySystemPaths)
        {
            if (!System.IO.File.Exists(path))
            {
                Debug.LogError($"Modelo HRA ausente: {path}");
                continue;
            }

            AssetImporter importer = AssetImporter.GetAtPath(path);
            if (importer == null || importer.GetType().Name != "GltfImporter")
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                importer = AssetImporter.GetAtPath(path);
            }

            if (importer == null || importer.GetType().Name != "GltfImporter")
            {
                Debug.LogError(
                    $"O glTFast ainda não assumiu a importação de {path}. " +
                    "Aguarde o Package Manager concluir e use Murilo VR > Reimportar rins HRA."
                );
            }
        }
    }
}
