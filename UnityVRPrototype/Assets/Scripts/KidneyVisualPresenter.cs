using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public enum KidneyPresentationState
{
    Internal,
    External
}

public enum HraTargetKidneySide
{
    Left,
    Right
}

public class KidneyVisualPresenter : MonoBehaviour
{
    public const int ExternalAnatomyLayer = 27;
    public const string LeftKidneyResource = "HRAKidneys/VH_M_Kidney_L";
    public const string RightKidneyResource = "HRAKidneys/VH_M_Kidney_R";
    public const string LeftUreterResource = "HRAKidneys/VH_M_Ureter_L";
    public const string RightUreterResource = "HRAKidneys/VH_M_Ureter_R";
    public const string BladderResource = "HRAKidneys/VH_M_Urinary_Bladder";

    [Header("HRA urinary system")]
    public HraTargetKidneySide targetSide = HraTargetKidneySide.Right;

    public KidneyPresentationState State { get; private set; } = KidneyPresentationState.Internal;
    public float Transition01 { get; private set; }
    public float ExternalOpacity { get; private set; } = 1f;
    public float InteriorOpacity { get; private set; } = 1f;
    public bool IsReady => visualRoot != null && leftKidney != null && rightKidney != null &&
                           leftUreter != null && rightUreter != null && bladder != null;
    public GameObject VisualRoot => visualRoot;
    public GameObject LeftKidney => leftKidney;
    public GameObject RightKidney => rightKidney;
    public GameObject LeftUreter => leftUreter;
    public GameObject RightUreter => rightUreter;
    public GameObject Bladder => bladder;
    public HraTargetKidneySide TargetSide => targetSide;
    public Bounds SystemBounds { get; private set; }
    public Vector3 BladderOutletLocal { get; private set; }
    public Vector3 TargetRenalPelvisLocal { get; private set; }

    private GameObject visualRoot;
    private GameObject leftKidney;
    private GameObject rightKidney;
    private GameObject leftUreter;
    private GameObject rightUreter;
    private GameObject bladder;
    private GameObject interiorVisualRoot;
    private GameObject patientAnatomyRoot;
    private bool interiorWasActive;
    private bool anatomyWasActive;
    private bool explorationActive;
    private readonly List<Material> runtimeMaterials = new List<Material>();

    public void Configure(VrCaseLoader loader, GameObject interiorRoot)
    {
        ClearVisuals();
        if (loader == null || loader.ContentRoot == null) return;

        GameObject leftKidneyPrefab = Resources.Load<GameObject>(LeftKidneyResource);
        GameObject rightKidneyPrefab = Resources.Load<GameObject>(RightKidneyResource);
        GameObject leftUreterPrefab = Resources.Load<GameObject>(LeftUreterResource);
        GameObject rightUreterPrefab = Resources.Load<GameObject>(RightUreterResource);
        GameObject bladderPrefab = Resources.Load<GameObject>(BladderResource);
        if (leftKidneyPrefab == null || rightKidneyPrefab == null || leftUreterPrefab == null ||
            rightUreterPrefab == null || bladderPrefab == null)
        {
            Debug.LogError("Sistema urinário HRA incompleto. Reimporte os cinco GLBs em Assets/Resources/HRAKidneys.");
            return;
        }

        interiorVisualRoot = interiorRoot;
        patientAnatomyRoot = loader.AnatomyObject;
        interiorWasActive = interiorVisualRoot != null && interiorVisualRoot.activeSelf;
        anatomyWasActive = patientAnatomyRoot != null && patientAnatomyRoot.activeSelf;

        visualRoot = new GameObject("HRA External Urinary System");
        visualRoot.transform.SetParent(loader.ContentRoot, false);
        leftKidney = InstantiateOrgan(leftKidneyPrefab, "HRA Male Left Kidney");
        rightKidney = InstantiateOrgan(rightKidneyPrefab, "HRA Male Right Kidney");
        leftUreter = InstantiateOrgan(leftUreterPrefab, "HRA Male Left Ureter");
        rightUreter = InstantiateOrgan(rightUreterPrefab, "HRA Male Right Ureter");
        bladder = InstantiateOrgan(bladderPrefab, "HRA Male Urinary Bladder");

        KeepKidneyExteriorOnly(leftKidney);
        KeepKidneyExteriorOnly(rightKidney);
        PrepareOpaqueMaterials(visualRoot);
        AlignToRoute(loader);
        AddStaticMeshColliders(visualRoot);
        SetLayerRecursively(visualRoot.transform, ExternalAnatomyLayer);
        RefreshSystemBounds();
        SetExplorationActive(false);
    }

    public void SetExplorationActive(bool active)
    {
        explorationActive = active;
        if (visualRoot != null) visualRoot.SetActive(active);
        if (interiorVisualRoot != null) interiorVisualRoot.SetActive(active ? false : interiorWasActive);
        if (patientAnatomyRoot != null) patientAnatomyRoot.SetActive(active ? false : anatomyWasActive);

        Transition01 = active ? 1f : 0f;
        ExternalOpacity = 1f;
        InteriorOpacity = active ? 0f : 1f;
        State = active ? KidneyPresentationState.External : KidneyPresentationState.Internal;
        if (active) RefreshSystemBounds();
    }

    // Kept for compatibility with existing diagnostics. The external presentation is intentionally opaque.
    public void Tick(float distanceFromRouteMillimeters)
    {
        if (!explorationActive) return;
        Transition01 = 1f;
        ExternalOpacity = 1f;
        InteriorOpacity = 0f;
        State = KidneyPresentationState.External;
    }

    public void ApplyStateForTesting(KidneyPresentationState state)
    {
        SetExplorationActive(state == KidneyPresentationState.External);
    }

    private GameObject InstantiateOrgan(GameObject prefab, string objectName)
    {
        GameObject instance = Instantiate(prefab, visualRoot.transform, false);
        instance.name = objectName;
        return instance;
    }

    private static void KeepKidneyExteriorOnly(GameObject kidney)
    {
        foreach (Renderer renderer in kidney.GetComponentsInChildren<Renderer>(true))
        {
            string lower = renderer.gameObject.name.ToLowerInvariant();
            renderer.enabled = lower.Contains("capsule");
        }
    }

    private void PrepareOpaqueMaterials(GameObject root)
    {
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            Material[] source = renderer.sharedMaterials;
            Material[] adjusted = new Material[Mathf.Max(1, source.Length)];
            for (int index = 0; index < adjusted.Length; index++)
            {
                Material material = source.Length > index && source[index] != null
                    ? new Material(source[index])
                    : BuildFallbackMaterial(renderer.gameObject.name);
                material.name = (source.Length > index ? source[index]?.name : renderer.gameObject.name) + " Opaque Runtime";
                ForceOpaque(material);
                adjusted[index] = material;
                runtimeMaterials.Add(material);
            }
            renderer.sharedMaterials = adjusted;
        }
    }

    private static Material BuildFallbackMaterial(string objectName)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        string lower = objectName.ToLowerInvariant();
        Color color = lower.Contains("bladder")
            ? new Color(0.72f, 0.34f, 0.38f, 1f)
            : lower.Contains("ureter") || lower.Contains("calyx") || lower.Contains("pelvis")
                ? new Color(0.92f, 0.62f, 0.48f, 1f)
                : new Color(0.48f, 0.075f, 0.09f, 1f);
        return new Material(shader) { color = color };
    }

    private static void ForceOpaque(Material material)
    {
        string colorProperty = material.HasProperty("_BaseColor") ? "_BaseColor" : material.HasProperty("_Color") ? "_Color" : string.Empty;
        if (!string.IsNullOrEmpty(colorProperty))
        {
            Color color = material.GetColor(colorProperty);
            color.a = 1f;
            material.SetColor(colorProperty, color);
        }
        material.SetOverrideTag("RenderType", "Opaque");
        material.renderQueue = (int)RenderQueue.Geometry;
        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 0f);
        if (material.HasProperty("_Mode")) material.SetFloat("_Mode", 0f);
        if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)BlendMode.One);
        if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)BlendMode.Zero);
        if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 1f);
        if (material.HasProperty("_Cull")) material.SetFloat("_Cull", (float)CullMode.Back);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.32f);
        if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.32f);
        material.DisableKeyword("_ALPHABLEND_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHATEST_ON");
    }

    private void AlignToRoute(VrCaseLoader loader)
    {
        GameObject targetUreter = targetSide == HraTargetKidneySide.Right ? rightUreter : leftUreter;
        string pelvisToken = targetSide == HraTargetKidneySide.Right ? "renal_pelvis_r" : "renal_pelvis_l";
        if (!TryGetNamedMeshPointLocal(targetUreter, pelvisToken, false, out Vector3 modelPelvis) ||
            !TryGetNamedMeshPointLocal(bladder, "urinary_bladder_neck", true, out Vector3 modelOutlet))
        {
            Debug.LogError("Não foi possível localizar as âncoras HRA de pelve renal e saída da bexiga.");
            return;
        }

        Vector3 desiredStart = loader.GetCurrentStartLocal();
        Vector3 desiredTarget = loader.GetCurrentTargetLocal();
        Vector3 modelVector = modelPelvis - modelOutlet;
        Vector3 desiredVector = desiredTarget - desiredStart;
        if (modelVector.sqrMagnitude < 0.000001f || desiredVector.sqrMagnitude < 0.000001f) return;

        float uniformScale = desiredVector.magnitude / modelVector.magnitude;
        Quaternion rotation = Quaternion.FromToRotation(modelVector.normalized, desiredVector.normalized);
        visualRoot.transform.localScale = Vector3.one * uniformScale;
        visualRoot.transform.localRotation = rotation;
        visualRoot.transform.localPosition = desiredTarget - rotation * (modelPelvis * uniformScale);

        BladderOutletLocal = visualRoot.transform.localPosition + rotation * (modelOutlet * uniformScale);
        TargetRenalPelvisLocal = visualRoot.transform.localPosition + rotation * (modelPelvis * uniformScale);
    }

    private bool TryGetNamedMeshPointLocal(GameObject organ, string token, bool useMinimumY, out Vector3 point)
    {
        foreach (MeshFilter filter in organ.GetComponentsInChildren<MeshFilter>(true))
        {
            if (!filter.gameObject.name.ToLowerInvariant().Contains(token) || filter.sharedMesh == null) continue;
            Bounds bounds = filter.sharedMesh.bounds;
            Vector3 meshPoint = useMinimumY
                ? new Vector3(bounds.center.x, bounds.min.y, bounds.center.z)
                : bounds.center;
            point = visualRoot.transform.InverseTransformPoint(filter.transform.TransformPoint(meshPoint));
            return true;
        }
        point = default;
        return false;
    }

    private static void AddStaticMeshColliders(GameObject root)
    {
        foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            Renderer renderer = filter.GetComponent<Renderer>();
            if (renderer == null || !renderer.enabled || filter.sharedMesh == null) continue;
            MeshCollider collider = filter.GetComponent<MeshCollider>();
            if (collider == null) collider = filter.gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = filter.sharedMesh;
        }
    }

    private void RefreshSystemBounds()
    {
        Renderer[] renderers = visualRoot == null ? Array.Empty<Renderer>() : visualRoot.GetComponentsInChildren<Renderer>(false);
        bool found = false;
        Bounds bounds = default;
        foreach (Renderer renderer in renderers)
        {
            if (!renderer.enabled) continue;
            if (!found)
            {
                bounds = renderer.bounds;
                found = true;
            }
            else bounds.Encapsulate(renderer.bounds);
        }
        SystemBounds = found ? bounds : new Bounds(visualRoot != null ? visualRoot.transform.position : Vector3.zero, Vector3.one * 0.5f);
    }

    private static void SetLayerRecursively(Transform root, int layer)
    {
        root.gameObject.layer = layer;
        foreach (Transform child in root) SetLayerRecursively(child, layer);
    }

    private void ClearVisuals()
    {
        if (interiorVisualRoot != null) interiorVisualRoot.SetActive(interiorWasActive);
        if (patientAnatomyRoot != null) patientAnatomyRoot.SetActive(anatomyWasActive);
        if (visualRoot != null) Destroy(visualRoot);
        foreach (Material material in runtimeMaterials)
        {
            if (material != null) Destroy(material);
        }
        runtimeMaterials.Clear();
        visualRoot = null;
        leftKidney = null;
        rightKidney = null;
        leftUreter = null;
        rightUreter = null;
        bladder = null;
        interiorVisualRoot = null;
        patientAnatomyRoot = null;
        explorationActive = false;
        Transition01 = 0f;
        ExternalOpacity = 1f;
        InteriorOpacity = 1f;
        State = KidneyPresentationState.Internal;
        SystemBounds = default;
        BladderOutletLocal = default;
        TargetRenalPelvisLocal = default;
    }

    private void OnDestroy()
    {
        ClearVisuals();
    }
}
