using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class HraTrainingCourse : MonoBehaviour, ITrainingCourseView
{
    public const string GenericCourseId = "hra_generic_urinary_system";
    public const string RightRouteId = "hra_right_manual_01";

    [Header("Editable generic course")]
    [Min(0.5f)] public float uniformScale = 1f;
    [Min(0.0005f)] public float routeSampleSpacingMeters = 0.001f;
    [Range(12, 32)] public int lumenSides = 24;
    [Min(0.005f)] public float bladderRadiusMeters = 0.024f;
    [Min(0.0025f)] public float ureterRadiusMeters = 0.004f;
    [Min(0.006f)] public float renalPelvisRadiusMeters = 0.010f;
    [Min(0.004f)] public float stoneDiameterMeters = 0.006f;
    [Range(0.1f, 0.8f)] public float targetKidneyMinimapOpacity = 0.34f;
    public TrainingCourseKnot[] manualControlPoints = Array.Empty<TrainingCourseKnot>();

    public event Action CourseReady;

    public bool IsReady { get; private set; }
    public string CourseId => GenericCourseId;
    public string RouteId => RightRouteId;
    public string DisplayName => "Treino HRA — bexiga ao rim direito";
    public Transform ContentRoot => contentRoot;
    public GameObject SmoothedPathObject => smoothedPathObject;
    public GameObject CurrentTargetObject => targetObject;
    public float CurrentStoneDiameterMeters => stoneDiameterMeters;
    public Color RouteColor => new Color(0.035f, 0.24f, 0.95f, 1f);
    public GameObject InteriorVisualRoot => interiorRoot;
    public GameObject ExternalVisualRoot => externalRoot;
    public GameObject StartMarkerObject => startMarkerObject;
    public Vector3 StartLocal { get; private set; }
    public Vector3 TargetLocal { get; private set; }
    public float RouteLengthMeters { get; private set; }
    public Bounds SystemBounds { get; private set; }
    public GameObject LeftKidney => leftKidney;
    public GameObject RightKidney => rightKidney;
    public GameObject LeftUreter => leftUreter;
    public GameObject RightUreter => rightUreter;
    public GameObject Bladder => bladder;
    public IReadOnlyList<Vector3> LumenPositions => lumenPositions;
    public IReadOnlyList<float> LumenRadii => lumenRadii;

    private Transform contentRoot;
    private GameObject externalRoot;
    private GameObject interiorRoot;
    private GameObject routeRoot;
    private GameObject leftKidney;
    private GameObject rightKidney;
    private GameObject leftUreter;
    private GameObject rightUreter;
    private GameObject bladder;
    private GameObject smoothedPathObject;
    private GameObject targetObject;
    private GameObject startMarkerObject;
    private Mesh lumenMesh;
    private Mesh stoneMesh;
    private Material lumenMaterial;
    private Material routeMaterial;
    private Material stoneMaterial;
    private Vector3[] routePositions = Array.Empty<Vector3>();
    private float[] routeRadii = Array.Empty<float>();
    private Vector3[] lumenPositions = Array.Empty<Vector3>();
    private float[] lumenRadii = Array.Empty<float>();
    private readonly List<Material> externalMaterials = new List<Material>();
    private readonly List<Material> rightKidneyMaterials = new List<Material>();

    private void Awake()
    {
        EnsureReady();
    }

    public void EnsureReady()
    {
        if (IsReady) return;
        try
        {
            BuildCourse();
            IsReady = routePositions.Length > 1 && targetObject != null && interiorRoot != null;
            if (IsReady) CourseReady?.Invoke();
        }
        catch (Exception exception)
        {
            IsReady = false;
            Debug.LogError("Não foi possível criar o percurso HRA genérico: " + exception);
        }
    }

    public void RebuildCourse()
    {
        ClearCourse();
        EnsureReady();
    }

    public Vector3[] CopyRoutePositions()
    {
        Vector3[] copy = new Vector3[routePositions.Length];
        Array.Copy(routePositions, copy, copy.Length);
        return copy;
    }

    public Vector3 SampleRouteLocal(float distanceMeters)
    {
        if (routePositions.Length == 0) return Vector3.zero;
        if (routePositions.Length == 1 || distanceMeters <= 0f) return routePositions[0];
        float remaining = Mathf.Clamp(distanceMeters, 0f, RouteLengthMeters);
        for (int index = 1; index < routePositions.Length; index++)
        {
            float length = Vector3.Distance(routePositions[index - 1], routePositions[index]);
            if (remaining <= length)
                return Vector3.Lerp(routePositions[index - 1], routePositions[index], length > 0f ? remaining / length : 0f);
            remaining -= length;
        }
        return routePositions[routePositions.Length - 1];
    }

    public float FindAllowedTravel(
        Vector3 worldOrigin,
        Vector3 worldDirection,
        float requestedWorldDistance,
        float probeWorldRadius)
    {
        if (!IsReady || contentRoot == null || requestedWorldDistance <= 0f) return 0f;
        Vector3 localOrigin = contentRoot.InverseTransformPoint(worldOrigin);
        Vector3 localDisplacement = contentRoot.InverseTransformVector(worldDirection.normalized * requestedWorldDistance);
        float localDistance = localDisplacement.magnitude;
        if (localDistance <= 0.0000001f) return 0f;
        float minimumScale = Mathf.Max(0.0001f, Mathf.Min(
            Mathf.Abs(contentRoot.lossyScale.x),
            Mathf.Abs(contentRoot.lossyScale.y),
            Mathf.Abs(contentRoot.lossyScale.z)));
        float allowedLocal = TrainingLumenMath.FindAllowedDistance(
            localOrigin,
            localDisplacement / localDistance,
            localDistance,
            lumenPositions,
            lumenRadii,
            probeWorldRadius / minimumScale);
        return requestedWorldDistance * Mathf.Clamp01(allowedLocal / localDistance);
    }

    public bool ContainsProbe(Vector3 worldPosition, float probeWorldRadius)
    {
        if (!IsReady || contentRoot == null) return false;
        float minimumScale = Mathf.Max(0.0001f, Mathf.Min(
            Mathf.Abs(contentRoot.lossyScale.x),
            Mathf.Abs(contentRoot.lossyScale.y),
            Mathf.Abs(contentRoot.lossyScale.z)));
        return TrainingLumenMath.IsInside(
            contentRoot.InverseTransformPoint(worldPosition),
            lumenPositions,
            lumenRadii,
            probeWorldRadius / minimumScale);
    }

    public void SetPresentation(bool exploration)
    {
        if (externalRoot != null) externalRoot.SetActive(true);
        if (interiorRoot != null) interiorRoot.SetActive(!exploration);
        if (targetObject != null) targetObject.SetActive(true);
        if (startMarkerObject != null) startMarkerObject.SetActive(!exploration);
        SetRightKidneyOpacity(exploration ? 1f : targetKidneyMinimapOpacity);
        RefreshSystemBounds();
    }

    private void BuildCourse()
    {
        GameObject contentObject = new GameObject("HRA Generic Training Course");
        contentRoot = contentObject.transform;
        contentRoot.SetParent(transform, false);
        contentRoot.localScale = Vector3.one * Mathf.Max(0.5f, uniformScale);

        externalRoot = new GameObject("HRA Generic External Urinary System");
        externalRoot.transform.SetParent(contentRoot, false);
        leftKidney = InstantiateResource(KidneyVisualPresenter.LeftKidneyResource, "HRA Male Left Kidney");
        rightKidney = InstantiateResource(KidneyVisualPresenter.RightKidneyResource, "HRA Male Right Kidney");
        leftUreter = InstantiateResource(KidneyVisualPresenter.LeftUreterResource, "HRA Male Left Ureter");
        rightUreter = InstantiateResource(KidneyVisualPresenter.RightUreterResource, "HRA Male Right Ureter");
        bladder = InstantiateResource(KidneyVisualPresenter.BladderResource, "HRA Male Urinary Bladder");

        KeepKidneyExteriorOnly(leftKidney);
        KeepKidneyExteriorOnly(rightKidney);
        PrepareExternalMaterials();
        SetLayerRecursively(externalRoot.transform, KidneyVisualPresenter.ExternalAnatomyLayer);
        AddStaticMeshColliders(externalRoot);

        IReadOnlyList<TrainingCourseKnot> controlPoints = manualControlPoints != null && manualControlPoints.Length >= 4
            ? manualControlPoints
            : BuildAnatomyDerivedControlPoints();
        routePositions = TrainingCoursePath.ResampleCatmullRom(controlPoints, routeSampleSpacingMeters, out routeRadii);
        if (routePositions.Length < 2) throw new InvalidOperationException("O percurso HRA possui menos de dois pontos.");
        RouteLengthMeters = 0f;
        for (int index = 1; index < routePositions.Length; index++)
            RouteLengthMeters += Vector3.Distance(routePositions[index - 1], routePositions[index]);

        Vector3 startTangent = (routePositions[1] - routePositions[0]).normalized;
        Vector3 endTangent = (routePositions[routePositions.Length - 1] - routePositions[routePositions.Length - 2]).normalized;
        StartLocal = routePositions[0] + startTangent * Mathf.Min(0.005f, bladderRadiusMeters * 0.25f);
        TargetLocal = routePositions[routePositions.Length - 1];
        BuildExtendedLumen(startTangent, endTangent);
        BuildInterior();
        BuildRouteObjects();
        SetPresentation(false);
    }

    private GameObject InstantiateResource(string resourceName, string objectName)
    {
        GameObject prefab = Resources.Load<GameObject>(resourceName);
        if (prefab == null) throw new InvalidOperationException("Modelo HRA ausente: " + resourceName);
        GameObject instance = Instantiate(prefab, externalRoot.transform, false);
        instance.name = objectName;
        return instance;
    }

    private IReadOnlyList<TrainingCourseKnot> BuildAnatomyDerivedControlPoints()
    {
        Bounds bladderBounds = CalculateLocalBounds(bladder);
        if (!TryFindNamedBoundsLocal(bladder, "ureteral_orifice_r", out Bounds orificeBounds))
            throw new InvalidOperationException("O óstio ureteral direito não foi localizado no modelo HRA.");
        if (!TryFindNamedBoundsLocal(rightUreter, "renal_pelvis_r", out Bounds pelvisBounds))
            throw new InvalidOperationException("A pelve renal direita não foi localizada no modelo HRA.");

        List<TrainingCourseKnot> result = new List<TrainingCourseKnot>
        {
            new TrainingCourseKnot(bladderBounds.center, bladderRadiusMeters),
            new TrainingCourseKnot(orificeBounds.center, Mathf.Max(ureterRadiusMeters, 0.007f))
        };

        MeshFilter ureterFilter = rightUreter.GetComponentsInChildren<MeshFilter>(true)
            .FirstOrDefault(filter => filter.sharedMesh != null &&
                filter.gameObject.name.Equals("VH_M_ureter_R", StringComparison.OrdinalIgnoreCase));
        if (ureterFilter == null)
            ureterFilter = rightUreter.GetComponentsInChildren<MeshFilter>(true)
                .FirstOrDefault(filter => filter.sharedMesh != null && filter.gameObject.name.ToLowerInvariant().Contains("ureter_r"));
        if (ureterFilter == null) throw new InvalidOperationException("A malha central do ureter direito não foi localizada.");

        List<Vector3> vertices = new List<Vector3>(ureterFilter.sharedMesh.vertexCount);
        foreach (Vector3 vertex in ureterFilter.sharedMesh.vertices)
            vertices.Add(contentRoot.InverseTransformPoint(ureterFilter.transform.TransformPoint(vertex)));
        float minimumY = vertices.Min(point => point.y);
        float maximumY = vertices.Max(point => point.y);
        const int bins = 18;
        for (int bin = 0; bin < bins; bin++)
        {
            float low = Mathf.Lerp(minimumY, maximumY, bin / (float)bins);
            float high = Mathf.Lerp(minimumY, maximumY, (bin + 1f) / bins);
            List<Vector3> slice = vertices.Where(point => point.y >= low && (bin == bins - 1 ? point.y <= high : point.y < high)).ToList();
            if (slice.Count == 0) continue;
            Vector3 center = Vector3.zero;
            foreach (Vector3 point in slice) center += point;
            center /= slice.Count;
            if (Vector3.Distance(center, result[result.Count - 1].position) > 0.0015f)
                result.Add(new TrainingCourseKnot(center, ureterRadiusMeters));
        }

        result.Add(new TrainingCourseKnot(pelvisBounds.center, renalPelvisRadiusMeters));
        return result;
    }

    private void BuildExtendedLumen(Vector3 startTangent, Vector3 endTangent)
    {
        lumenPositions = new Vector3[routePositions.Length + 2];
        lumenRadii = new float[routeRadii.Length + 2];
        lumenPositions[0] = routePositions[0] - startTangent * Mathf.Max(0.012f, bladderRadiusMeters * 0.55f);
        lumenRadii[0] = routeRadii[0];
        Array.Copy(routePositions, 0, lumenPositions, 1, routePositions.Length);
        Array.Copy(routeRadii, 0, lumenRadii, 1, routeRadii.Length);
        lumenPositions[lumenPositions.Length - 1] = routePositions[routePositions.Length - 1] +
                                                    endTangent * Mathf.Max(0.008f, renalPelvisRadiusMeters);
        lumenRadii[lumenRadii.Length - 1] = routeRadii[routeRadii.Length - 1];
    }

    private void BuildInterior()
    {
        interiorRoot = new GameObject("Training Procedural Lumen");
        interiorRoot.layer = 29;
        interiorRoot.transform.SetParent(contentRoot, false);
        lumenMesh = TrainingLumenMeshBuilder.Build(lumenPositions, lumenRadii, lumenSides, "HRA Right Training Lumen");
        interiorRoot.AddComponent<MeshFilter>().sharedMesh = lumenMesh;
        MeshRenderer renderer = interiorRoot.AddComponent<MeshRenderer>();
        lumenMaterial = BuildInteriorMaterial();
        renderer.sharedMaterial = lumenMaterial;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    private void BuildRouteObjects()
    {
        routeRoot = new GameObject("HRA Generic Selected Route");
        routeRoot.transform.SetParent(contentRoot, false);

        smoothedPathObject = new GameObject("Smoothed Route");
        smoothedPathObject.transform.SetParent(routeRoot.transform, false);
        LineRenderer line = smoothedPathObject.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.positionCount = routePositions.Length;
        line.SetPositions(routePositions);
        line.startWidth = 0.001f;
        line.endWidth = 0.001f;
        line.numCapVertices = 4;
        line.numCornerVertices = 4;
        Shader routeShader = Shader.Find("Murillo/Training Route Opaque");
        if (routeShader == null) routeShader = Shader.Find("Sprites/Default");
        routeMaterial = new Material(routeShader) { color = RouteColor, name = "HRA Base Route Material" };
        line.sharedMaterial = routeMaterial;

        startMarkerObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        startMarkerObject.name = "HRA Training Start";
        startMarkerObject.transform.SetParent(routeRoot.transform, false);
        startMarkerObject.transform.localPosition = StartLocal;
        startMarkerObject.transform.localScale = Vector3.one * 0.003f;
        Collider startCollider = startMarkerObject.GetComponent<Collider>();
        if (startCollider != null) Destroy(startCollider);
        startMarkerObject.GetComponent<Renderer>().sharedMaterial = BuildSimpleMaterial(new Color(0.05f, 0.9f, 0.2f, 1f));

        targetObject = new GameObject("Target Kidney Stone");
        targetObject.transform.SetParent(routeRoot.transform, false);
        targetObject.transform.localPosition = TargetLocal;
        stoneMesh = VrStoneMeshBuilder.Build(stoneDiameterMeters, RightRouteId);
        targetObject.AddComponent<MeshFilter>().sharedMesh = stoneMesh;
        MeshRenderer stoneRenderer = targetObject.AddComponent<MeshRenderer>();
        stoneMaterial = BuildSimpleMaterial(new Color(0.55f, 0.29f, 0.10f, 1f));
        if (stoneMaterial.HasProperty("_Smoothness")) stoneMaterial.SetFloat("_Smoothness", 0.12f);
        if (stoneMaterial.HasProperty("_Glossiness")) stoneMaterial.SetFloat("_Glossiness", 0.12f);
        stoneRenderer.sharedMaterial = stoneMaterial;
        stoneRenderer.shadowCastingMode = ShadowCastingMode.On;
    }

    private void PrepareExternalMaterials()
    {
        foreach (Renderer renderer in externalRoot.GetComponentsInChildren<Renderer>(true))
        {
            Material[] source = renderer.sharedMaterials;
            Material[] adjusted = new Material[Mathf.Max(1, source.Length)];
            for (int index = 0; index < adjusted.Length; index++)
            {
                Material material = source.Length > index && source[index] != null
                    ? new Material(source[index])
                    : BuildSimpleMaterial(FallbackOrganColor(renderer.gameObject.name));
                material.name = renderer.gameObject.name + " HRA Runtime";
                SetMaterialOpacity(material, 1f);
                adjusted[index] = material;
                externalMaterials.Add(material);
                if (renderer.transform.IsChildOf(rightKidney.transform)) rightKidneyMaterials.Add(material);
            }
            renderer.sharedMaterials = adjusted;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
        }
    }

    private static void KeepKidneyExteriorOnly(GameObject kidney)
    {
        foreach (Renderer renderer in kidney.GetComponentsInChildren<Renderer>(true))
            renderer.enabled = renderer.gameObject.name.ToLowerInvariant().Contains("capsule");
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

    private void SetRightKidneyOpacity(float opacity)
    {
        foreach (Material material in rightKidneyMaterials)
            if (material != null) SetMaterialOpacity(material, opacity);
    }

    private static void SetMaterialOpacity(Material material, float requestedOpacity)
    {
        float opacity = Mathf.Clamp01(requestedOpacity);
        string property = material.HasProperty("_BaseColor") ? "_BaseColor" : material.HasProperty("_Color") ? "_Color" : string.Empty;
        if (!string.IsNullOrEmpty(property))
        {
            Color color = material.GetColor(property);
            color.a = opacity;
            material.SetColor(property, color);
        }
        bool transparent = opacity < 0.999f;
        material.SetOverrideTag("RenderType", transparent ? "Transparent" : "Opaque");
        material.renderQueue = transparent ? (int)RenderQueue.Transparent : (int)RenderQueue.Geometry;
        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", transparent ? 1f : 0f);
        if (material.HasProperty("_Mode")) material.SetFloat("_Mode", transparent ? 3f : 0f);
        if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", transparent ? (float)BlendMode.SrcAlpha : (float)BlendMode.One);
        if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", transparent ? (float)BlendMode.OneMinusSrcAlpha : (float)BlendMode.Zero);
        if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", transparent ? 0f : 1f);
        if (material.HasProperty("_Cull")) material.SetFloat("_Cull", (float)CullMode.Back);
        if (transparent)
        {
            material.EnableKeyword("_ALPHABLEND_ON");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        }
        else
        {
            material.DisableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        }
    }

    private static Material BuildInteriorMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        Material material = new Material(shader)
        {
            name = "Simplified HRA Training Lumen Material",
            color = new Color(0.62f, 0.085f, 0.11f, 1f)
        };
        material.doubleSidedGI = true;
        if (material.HasProperty("_Cull")) material.SetFloat("_Cull", (float)CullMode.Off);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.22f);
        if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.22f);
        if (material.HasProperty("_EmissionColor"))
        {
            material.SetColor("_EmissionColor", new Color(0.018f, 0.0015f, 0.002f));
            material.EnableKeyword("_EMISSION");
        }
        return material;
    }

    private static Material BuildSimpleMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        return new Material(shader) { color = color };
    }

    private static Color FallbackOrganColor(string objectName)
    {
        string lower = objectName.ToLowerInvariant();
        if (lower.Contains("bladder")) return new Color(0.72f, 0.34f, 0.38f, 1f);
        if (lower.Contains("ureter") || lower.Contains("calyx") || lower.Contains("pelvis"))
            return new Color(0.92f, 0.62f, 0.48f, 1f);
        return new Color(0.48f, 0.075f, 0.09f, 1f);
    }

    private Bounds CalculateLocalBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true).Where(renderer => renderer.enabled).ToArray();
        if (renderers.Length == 0) return new Bounds(Vector3.zero, Vector3.one * 0.01f);
        Bounds bounds = new Bounds(contentRoot.InverseTransformPoint(renderers[0].bounds.center), Vector3.zero);
        foreach (Renderer renderer in renderers)
        {
            Bounds world = renderer.bounds;
            foreach (Vector3 corner in BoundsCorners(world)) bounds.Encapsulate(contentRoot.InverseTransformPoint(corner));
        }
        return bounds;
    }

    private bool TryFindNamedBoundsLocal(GameObject root, string token, out Bounds bounds)
    {
        Renderer renderer = root.GetComponentsInChildren<Renderer>(true)
            .FirstOrDefault(value => value.gameObject.name.ToLowerInvariant().Contains(token));
        if (renderer == null)
        {
            bounds = default;
            return false;
        }
        Bounds world = renderer.bounds;
        bounds = new Bounds(contentRoot.InverseTransformPoint(world.center), Vector3.zero);
        foreach (Vector3 corner in BoundsCorners(world)) bounds.Encapsulate(contentRoot.InverseTransformPoint(corner));
        return true;
    }

    private static IEnumerable<Vector3> BoundsCorners(Bounds bounds)
    {
        for (int x = -1; x <= 1; x += 2)
        for (int y = -1; y <= 1; y += 2)
        for (int z = -1; z <= 1; z += 2)
            yield return bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z));
    }

    private void RefreshSystemBounds()
    {
        Renderer[] renderers = externalRoot == null
            ? Array.Empty<Renderer>()
            : externalRoot.GetComponentsInChildren<Renderer>(false).Where(renderer => renderer.enabled).ToArray();
        if (renderers.Length == 0)
        {
            SystemBounds = new Bounds(transform.position, Vector3.one * 0.5f);
            return;
        }
        Bounds bounds = renderers[0].bounds;
        for (int index = 1; index < renderers.Length; index++) bounds.Encapsulate(renderers[index].bounds);
        SystemBounds = bounds;
    }

    private static void SetLayerRecursively(Transform root, int layer)
    {
        root.gameObject.layer = layer;
        foreach (Transform child in root) SetLayerRecursively(child, layer);
    }

    private void ClearCourse()
    {
        IsReady = false;
        if (contentRoot != null) DestroyImmediate(contentRoot.gameObject);
        if (lumenMesh != null) DestroyImmediate(lumenMesh);
        if (stoneMesh != null) DestroyImmediate(stoneMesh);
        if (lumenMaterial != null) DestroyImmediate(lumenMaterial);
        if (routeMaterial != null) DestroyImmediate(routeMaterial);
        if (stoneMaterial != null) DestroyImmediate(stoneMaterial);
        foreach (Material material in externalMaterials)
            if (material != null) DestroyImmediate(material);
        externalMaterials.Clear();
        rightKidneyMaterials.Clear();
        contentRoot = null;
        externalRoot = null;
        interiorRoot = null;
        routeRoot = null;
        routePositions = Array.Empty<Vector3>();
        routeRadii = Array.Empty<float>();
        lumenPositions = Array.Empty<Vector3>();
        lumenRadii = Array.Empty<float>();
    }

    private void OnDestroy()
    {
        ClearCourse();
    }

    private void OnDrawGizmosSelected()
    {
        if (manualControlPoints == null || manualControlPoints.Length < 2) return;
        Gizmos.color = new Color(0.05f, 0.35f, 1f, 0.9f);
        for (int index = 0; index < manualControlPoints.Length; index++)
        {
            Vector3 world = transform.TransformPoint(manualControlPoints[index].position);
            Gizmos.DrawWireSphere(world, manualControlPoints[index].lumenRadiusMeters);
            if (index > 0) Gizmos.DrawLine(transform.TransformPoint(manualControlPoints[index - 1].position), world);
        }
    }
}
