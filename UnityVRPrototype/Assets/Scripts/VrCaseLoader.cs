using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Rendering;
using UnityEngine.XR;
using Unity.XR.CoreUtils;

public class VrCaseLoader : MonoBehaviour, ITrainingCourseView
{
    public event Action CaseReady;
    public event Action RouteChanged;

    [Header("Case catalog")]
    public string catalogRelativePath = "Cases/catalog.json";
    public string routeFileName = "vr_route_unity.json";
    public GameObject urinaryTractMesh;

    [Header("Scene")]
    public Transform sceneRoot;
    public Transform cameraRig;
    public bool frameCaseOnLoad = true;
    public bool enableVrRuntimeObjects = true;
    public bool enableInformationPanel = true;
    public bool enableKeyboardShortcuts = true;
    public bool mapMedicalZToUnityY = true;
    public float voxelToMeterScale = 0.002f;
    public float maximumTabletopSize = 0.75f;
    public float tabletopDistance = 1.05f;
    public float tabletopHeight = -0.12f;

    [Header("Visuals")]
    public float routeWidth = 0.007f;
    public bool renderSmoothedPathAsTube = true;
    [Range(6, 18)] public int routeTubeSides = 8;
    public float pointRadius = 0.018f;
    public float followMarkerRadius = 0.014f;
    [Range(0.05f, 1f)] public float meshOpacity = 0.32f;
    [Min(0)] public int minimumAnatomyComponentFaces = 500;
    public Color anatomyColor = new Color(0.92f, 0.2f, 0.12f, 0.32f);
    public Color stoneColor = new Color(1f, 0.7f, 0.08f, 1f);
    public Color originalPathColor = new Color(0.1f, 0.9f, 0.25f, 1f);
    public Color smoothedPathColor = new Color(0.1f, 0.35f, 1f, 1f);
    public Color startColor = new Color(0.05f, 0.9f, 0.2f, 1f);
    public Color targetColor = new Color(1f, 0.7f, 0.05f, 1f);

    [Header("Route animation")]
    public float followDurationSeconds = 18f;
    public float followRotationSmoothing = 12f;

    [Header("Input")]
    public bool enableQuestControllerInput = true;
    public float controllerRepeatSeconds = 0.25f;
    public KeyCode toggleOriginalKey = KeyCode.O;
    public KeyCode toggleSmoothedKey = KeyCode.P;
    public KeyCode followRouteKey = KeyCode.F;

    public string StatusMessage { get; private set; } = "Starting";
    public int CurrentCaseIndex => currentCaseIndex;
    public int CaseCount => catalog?.cases?.Length ?? 0;
    public int CurrentRouteIndex => currentRouteIndex;
    public int RouteCount => routesDocument?.routes?.Length ?? 0;
    public string CurrentCaseName => manifest?.display_name ?? routeData?.case_name ?? "No case";
    public bool IsReady => !loading && routeData != null && visualRoutePositions.Length > 1;
    public Transform SceneRoot => sceneRoot;
    public Transform ContentRoot => contentRoot;
    public Transform RouteRoot => routeRoot;
    public GameObject AnatomyObject => anatomyObject;
    public GameObject SmoothedPathObject => smoothedPathObject;
    public GameObject StartMarkerObject => startMarkerObject;
    public GameObject CurrentTargetObject => targetObject;
    public float CurrentStoneDiameterMeters => Mathf.Max(0.006f, GetCurrentStoneRadiusMeters() * 2f);
    public Color RouteColor => smoothedPathColor;
    float ITrainingCourseView.RouteLengthMeters => CurrentRouteLengthMeters;
    public VrCaseManifest CurrentManifest => manifest;
    public VrRouteData CurrentRoute => routeData;
    public float CurrentRouteLengthMeters => visualRouteLength;

    private VrCaseCatalog catalog;
    private VrCaseManifest manifest;
    private VrRoutesDocument routesDocument;
    private VrRouteData routeData;
    private Transform contentRoot;
    private Transform routeRoot;
    private GameObject anatomyObject;
    private GameObject stoneObject;
    private GameObject originalPathObject;
    private GameObject smoothedPathObject;
    private GameObject startMarkerObject;
    private GameObject targetObject;
    private GameObject followMarkerObject;
    private Mesh targetStoneMesh;
    private TextMesh informationText;
    private Material anatomyMaterial;
    private Vector3[] visualRoutePositions = Array.Empty<Vector3>();
    private float[] visualRouteDistances = Array.Empty<float>();
    private float visualRouteLength;
    private float followTimer;
    private bool followingRoute;
    private bool routeVisible = true;
    private bool stonesVisible = true;
    private bool loading;
    private int currentCaseIndex;
    private int currentRouteIndex;
    private float nextControllerRepeatTime;
    private ControllerState previousLeft;
    private ControllerState previousRight;
    private readonly List<InputDevice> controllerDevices = new List<InputDevice>();
    private VrModelManipulator manipulator;

    private struct ControllerState
    {
        public bool valid;
        public bool primary;
        public bool secondary;
        public bool trigger;
        public bool axisClick;
        public Vector2 axis;
    }

    private IEnumerator Start()
    {
        EnsureSceneRoot();
        EnsureRuntimeLighting();
        if (enableInformationPanel) EnsureInformationText();
        EnsureManipulator();
        if (enableVrRuntimeObjects) EnsureRuntimeInteractionObjects();
        yield return LoadCatalog();
    }

    private void Update()
    {
        if (enableKeyboardShortcuts)
        {
            HandleKeyboardInput();
        }
        if (enableQuestControllerInput)
        {
            HandleQuestControllerInput();
        }
        if (followingRoute)
        {
            AnimateRouteMarker();
        }
        UpdateInformationText();
    }

    private IEnumerator LoadCatalog()
    {
        SetStatus("Loading case catalog...");
        string json = null;
        string error = null;
        yield return LoadStreamingText(catalogRelativePath, value => json = value, value => error = value);
        if (string.IsNullOrEmpty(error) && !string.IsNullOrEmpty(json))
        {
            try
            {
                catalog = JsonUtility.FromJson<VrCaseCatalog>(json);
            }
            catch (Exception exception)
            {
                error = $"Invalid catalog JSON: {exception.Message}";
            }
        }

        if (catalog != null && catalog.cases != null && catalog.cases.Length > 0)
        {
            yield return LoadCaseAt(0);
            yield break;
        }

        Debug.LogWarning($"Case catalog unavailable ({error}). Trying the legacy sample case.");
        yield return LoadLegacyCase();
    }

    private IEnumerator LoadCaseAt(int requestedIndex)
    {
        if (loading || catalog?.cases == null || catalog.cases.Length == 0)
        {
            yield break;
        }

        loading = true;
        currentCaseIndex = WrapIndex(requestedIndex, catalog.cases.Length);
        VrCaseCatalogEntry entry = catalog.cases[currentCaseIndex];
        SetStatus($"Loading {entry.display_name}...");

        string manifestRelative = CombineRelative("Cases", entry.manifest_file);
        string manifestJson = null;
        string loadError = null;
        yield return LoadStreamingText(manifestRelative, value => manifestJson = value, value => loadError = value);
        if (!string.IsNullOrEmpty(loadError))
        {
            FailLoad(loadError);
            yield break;
        }

        try
        {
            manifest = JsonUtility.FromJson<VrCaseManifest>(manifestJson);
            ValidateManifest(manifest);
        }
        catch (Exception exception)
        {
            FailLoad($"Invalid manifest: {exception.Message}");
            yield break;
        }

        string caseDirectory = GetRelativeDirectory(manifestRelative);
        string routesJson = null;
        string anatomyText = null;
        string stonesText = null;
        yield return LoadStreamingText(
            CombineRelative(caseDirectory, manifest.files.routes),
            value => routesJson = value,
            value => loadError = value
        );
        if (string.IsNullOrEmpty(loadError))
        {
            yield return LoadStreamingText(
                CombineRelative(caseDirectory, manifest.files.anatomy),
                value => anatomyText = value,
                value => loadError = value
            );
        }
        if (string.IsNullOrEmpty(loadError) && !string.IsNullOrWhiteSpace(manifest.files.stones))
        {
            yield return LoadStreamingText(
                CombineRelative(caseDirectory, manifest.files.stones),
                value => stonesText = value,
                value => loadError = value
            );
        }
        if (!string.IsNullOrEmpty(loadError))
        {
            FailLoad(loadError);
            yield break;
        }

        try
        {
            routesDocument = JsonUtility.FromJson<VrRoutesDocument>(routesJson);
            if (routesDocument?.routes == null || routesDocument.routes.Length == 0)
            {
                throw new InvalidOperationException("routes.json contains no routes.");
            }
            DestroyCurrentCase();
            EnsureContentRoot();
            anatomyObject = BuildObjectFromObj("Anatomy", anatomyText, anatomyColor, true);
            if (!string.IsNullOrEmpty(stonesText))
            {
                stoneObject = BuildObjectFromObj("Segmented Stones", stonesText, stoneColor, false);
            }
            currentRouteIndex = 0;
            SelectRoute(0);
            if (frameCaseOnLoad) FrameCaseInFrontOfViewer();
            manipulator?.CaptureResetPose();
            SetStatus($"Loaded {manifest.display_name}");
            loading = false;
            CaseReady?.Invoke();
        }
        catch (Exception exception)
        {
            FailLoad($"Could not build case: {exception.Message}");
            yield break;
        }
        loading = false;
    }

    private IEnumerator LoadLegacyCase()
    {
        string json = null;
        string error = null;
        yield return LoadStreamingText(routeFileName, value => json = value, value => error = value);
        if (!string.IsNullOrEmpty(error))
        {
            FailLoad("No v2 catalog or legacy route is available. Run build_vr_case.ps1.");
            yield break;
        }
        try
        {
            routeData = JsonUtility.FromJson<VrRouteData>(json);
            if (routeData?.path_smoothed == null || routeData.path_smoothed.Length < 2)
            {
                throw new InvalidOperationException("Legacy route has fewer than two points.");
            }
            DestroyCurrentCase();
            EnsureContentRoot();
            if (urinaryTractMesh != null)
            {
                anatomyObject = urinaryTractMesh.scene.IsValid() ? urinaryTractMesh : Instantiate(urinaryTractMesh);
                anatomyObject.name = "Legacy Anatomy";
                anatomyObject.transform.SetParent(contentRoot, false);
                anatomyObject.transform.localScale = Vector3.one * voxelToMeterScale;
                ApplyMaterial(anatomyObject, anatomyColor, true);
                EnsureAnatomyColliders(anatomyObject);
            }
            SelectLegacyRoute(routeData);
            if (frameCaseOnLoad) FrameCaseInFrontOfViewer();
            manipulator?.CaptureResetPose();
            SetStatus("Loaded legacy sample case");
            loading = false;
            CaseReady?.Invoke();
        }
        catch (Exception exception)
        {
            FailLoad(exception.Message);
            yield break;
        }
        loading = false;
    }

    private IEnumerator LoadStreamingText(string relativePath, Action<string> onSuccess, Action<string> onError)
    {
        string path = Application.streamingAssetsPath.TrimEnd('/', '\\') + "/" + relativePath.Replace('\\', '/');
        string uri = path.Contains("://") ? path : new Uri(path).AbsoluteUri;
        using UnityWebRequest request = UnityWebRequest.Get(uri);
        yield return request.SendWebRequest();
        if (request.result != UnityWebRequest.Result.Success)
        {
            onError?.Invoke($"Could not load {relativePath}: {request.error}");
        }
        else
        {
            onSuccess?.Invoke(request.downloadHandler.text);
        }
    }

    private void ValidateManifest(VrCaseManifest value)
    {
        if (value == null || value.schema_version != 2)
        {
            throw new InvalidOperationException("Only case schema version 2 is supported.");
        }
        if (string.IsNullOrWhiteSpace(value.case_id) || value.files == null)
        {
            throw new InvalidOperationException("case_id or files is missing.");
        }
        if (string.IsNullOrWhiteSpace(value.files.anatomy) || string.IsNullOrWhiteSpace(value.files.routes))
        {
            throw new InvalidOperationException("Anatomy or routes file is missing.");
        }
    }

    private GameObject BuildObjectFromObj(string objectName, string objText, Color color, bool transparent)
    {
        int componentThreshold = transparent ? minimumAnatomyComponentFaces : 0;
        Mesh mesh = VrObjParser.Parse(objText, objectName + " Mesh", componentThreshold);
        GameObject result = new GameObject(objectName);
        result.transform.SetParent(contentRoot, false);
        MeshFilter filter = result.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;
        MeshRenderer renderer = result.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = BuildMaterial(color, transparent, !transparent);
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        if (transparent)
        {
            anatomyMaterial = renderer.sharedMaterial;
            MeshCollider collider = result.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
        }
        return result;
    }

    private static void EnsureAnatomyColliders(GameObject target)
    {
        foreach (MeshFilter filter in target.GetComponentsInChildren<MeshFilter>(true))
        {
            MeshCollider collider = filter.GetComponent<MeshCollider>();
            if (collider == null) collider = filter.gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = filter.sharedMesh;
        }
    }

    private void SelectLegacyRoute(VrRouteData value)
    {
        routesDocument = null;
        currentRouteIndex = 0;
        routeData = value;
        BuildSelectedRoute();
    }

    private void SelectRoute(int requestedIndex)
    {
        if (routesDocument?.routes == null || routesDocument.routes.Length == 0)
        {
            return;
        }
        currentRouteIndex = WrapIndex(requestedIndex, routesDocument.routes.Length);
        routeData = routesDocument.routes[currentRouteIndex];
        BuildSelectedRoute();
        SetStatus($"Route {currentRouteIndex + 1}/{routesDocument.routes.Length}: {routeData.stone_id}");
        RouteChanged?.Invoke();
    }

    private void BuildSelectedRoute()
    {
        if (targetStoneMesh != null)
        {
            Destroy(targetStoneMesh);
            targetStoneMesh = null;
        }
        if (routeRoot != null)
        {
            Destroy(routeRoot.gameObject);
        }
        GameObject root = new GameObject("Selected Route");
        routeRoot = root.transform;
        routeRoot.SetParent(contentRoot, false);
        BuildVisualRouteCache();
        originalPathObject = BuildLine("A* Original Path", routeData.path_original, originalPathColor, routeWidth * 0.7f);
        smoothedPathObject = renderSmoothedPathAsTube
            ? BuildTube("Smoothed Route", visualRoutePositions, smoothedPathColor, routeWidth * 0.5f)
            : BuildLine("Smoothed Route", GetVisualRoutePoints(), smoothedPathColor, routeWidth);
        if (originalPathObject != null)
        {
            originalPathObject.SetActive(false);
        }
        startMarkerObject = BuildPoint("Start", routeData.start, startColor, pointRadius, routeRoot);
        targetObject = BuildTargetStone();
        followMarkerObject = BuildPoint("Route Marker", routeData.start, new Color(0.1f, 0.85f, 1f, 1f), followMarkerRadius, routeRoot);
        followMarkerObject.SetActive(false);
        routeRoot.gameObject.SetActive(routeVisible);
        followingRoute = false;
        followTimer = 0f;
    }

    private GameObject BuildTargetStone()
    {
        GameObject stone = new GameObject("Target Kidney Stone");
        stone.transform.SetParent(routeRoot, false);
        stone.transform.localPosition = MapPoint(routeData.target);
        targetStoneMesh = VrStoneMeshBuilder.Build(CurrentStoneDiameterMeters, routeData.stone_id);
        stone.AddComponent<MeshFilter>().sharedMesh = targetStoneMesh;
        MeshRenderer renderer = stone.AddComponent<MeshRenderer>();
        Color naturalStone = new Color(0.55f, 0.29f, 0.10f, 1f);
        Material material = BuildMaterial(naturalStone, false, true);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.12f);
        if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.12f);
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.On;
        renderer.receiveShadows = true;
        stone.SetActive(stonesVisible);
        return stone;
    }

    private void BuildVisualRouteCache()
    {
        VrPoint[] points = GetVisualRoutePoints();
        visualRoutePositions = new Vector3[points.Length];
        visualRouteDistances = new float[points.Length];
        visualRouteLength = 0f;
        for (int index = 0; index < points.Length; index++)
        {
            visualRoutePositions[index] = MapPoint(points[index]);
            if (index > 0)
            {
                visualRouteLength += Vector3.Distance(visualRoutePositions[index - 1], visualRoutePositions[index]);
            }
            visualRouteDistances[index] = visualRouteLength;
        }
    }

    private VrPoint[] GetVisualRoutePoints()
    {
        if (routeData?.path_visual != null && routeData.path_visual.Length > 1)
        {
            return routeData.path_visual;
        }
        return routeData?.path_smoothed ?? Array.Empty<VrPoint>();
    }

    private GameObject BuildLine(string lineName, VrPoint[] points, Color color, float width)
    {
        if (points == null || points.Length < 2)
        {
            return null;
        }
        GameObject lineObject = new GameObject(lineName);
        lineObject.transform.SetParent(routeRoot, false);
        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.positionCount = points.Length;
        line.startWidth = width;
        line.endWidth = width;
        line.numCapVertices = 4;
        line.numCornerVertices = 4;
        line.sharedMaterial = BuildMaterial(color, false, true);
        for (int index = 0; index < points.Length; index++)
        {
            line.SetPosition(index, MapPoint(points[index]));
        }
        return lineObject;
    }

    private GameObject BuildTube(string tubeName, Vector3[] positions, Color color, float radius)
    {
        if (positions == null || positions.Length < 2)
        {
            return null;
        }
        Mesh mesh = VrTubeMeshBuilder.Build(positions, radius, routeTubeSides, tubeName);
        GameObject tubeObject = new GameObject(tubeName);
        tubeObject.transform.SetParent(routeRoot, false);
        tubeObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = tubeObject.AddComponent<MeshRenderer>();
        Material routeMaterial = BuildMaterial(new Color(color.r, color.g, color.b, 1f), false, true);
        ConfigureOpaqueMaterial(routeMaterial);
        renderer.sharedMaterial = routeMaterial;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return tubeObject;
    }

    private GameObject BuildPoint(string pointName, VrPoint point, Color color, float radius, Transform parent)
    {
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = pointName;
        sphere.transform.SetParent(parent, false);
        sphere.transform.localPosition = MapPoint(point);
        sphere.transform.localScale = Vector3.one * radius;
        sphere.GetComponent<Renderer>().sharedMaterial = BuildMaterial(color, false, true);
        Collider collider = sphere.GetComponent<Collider>();
        if (collider != null)
        {
            Destroy(collider);
        }
        return sphere;
    }

    private Vector3 MapPoint(VrPoint point)
    {
        if (point == null)
        {
            return Vector3.zero;
        }
        if (string.Equals(routeData?.coordinate_space, "unity_meters_y_up", StringComparison.OrdinalIgnoreCase))
        {
            return new Vector3(point.x, point.y, point.z);
        }
        return mapMedicalZToUnityY
            ? new Vector3(point.x, point.z, point.y) * voxelToMeterScale
            : new Vector3(point.x, point.y, point.z) * voxelToMeterScale;
    }

    private void AnimateRouteMarker()
    {
        if (followMarkerObject == null || visualRoutePositions.Length < 2 || visualRouteLength <= 0f)
        {
            StopRouteAnimation();
            return;
        }
        followTimer += Time.deltaTime;
        float normalized = Mathf.Clamp01(followTimer / Mathf.Max(0.1f, followDurationSeconds));
        float distance = normalized * visualRouteLength;
        Vector3 position = SampleRoute(distance);
        Vector3 lookAhead = SampleRoute(Mathf.Min(distance + 0.02f, visualRouteLength));
        Vector3 forward = lookAhead - position;
        followMarkerObject.SetActive(true);
        followMarkerObject.transform.localPosition = position;
        if (forward.sqrMagnitude > 0.000001f)
        {
            Quaternion desired = Quaternion.LookRotation(forward.normalized, Vector3.up);
            followMarkerObject.transform.localRotation = Quaternion.Slerp(
                followMarkerObject.transform.localRotation,
                desired,
                Time.deltaTime * followRotationSmoothing
            );
        }
        if (normalized >= 1f)
        {
            StopRouteAnimation();
        }
    }

    private Vector3 SampleRoute(float distance)
    {
        if (distance <= 0f)
        {
            return visualRoutePositions[0];
        }
        if (distance >= visualRouteLength)
        {
            return visualRoutePositions[visualRoutePositions.Length - 1];
        }
        int upper = Array.BinarySearch(visualRouteDistances, distance);
        if (upper >= 0)
        {
            return visualRoutePositions[upper];
        }
        upper = Mathf.Clamp(~upper, 1, visualRoutePositions.Length - 1);
        int lower = upper - 1;
        float segment = visualRouteDistances[upper] - visualRouteDistances[lower];
        float amount = segment > 0.000001f ? (distance - visualRouteDistances[lower]) / segment : 0f;
        return Vector3.Lerp(visualRoutePositions[lower], visualRoutePositions[upper], amount);
    }

    public Vector3 SampleCurrentRouteLocal(float distanceMeters)
    {
        return SampleRoute(Mathf.Clamp(distanceMeters, 0f, visualRouteLength));
    }

    Vector3 ITrainingCourseView.SampleRouteLocal(float distanceMeters) => SampleCurrentRouteLocal(distanceMeters);

    public Vector3 GetCurrentStartLocal()
    {
        return routeData?.start != null ? MapPoint(routeData.start) : Vector3.zero;
    }

    public Vector3 GetCurrentTargetLocal()
    {
        return routeData?.target != null ? MapPoint(routeData.target) : Vector3.zero;
    }

    public Vector3 GetCurrentTargetForwardLocal()
    {
        if (visualRoutePositions.Length < 2) return Vector3.forward;
        return (visualRoutePositions[visualRoutePositions.Length - 1] -
            visualRoutePositions[visualRoutePositions.Length - 2]).normalized;
    }

    public Vector3[] CopyCurrentRoutePositions()
    {
        return (Vector3[])visualRoutePositions.Clone();
    }

    public float GetCurrentStoneRadiusMeters()
    {
        if (manifest?.stones != null && routeData != null)
        {
            foreach (VrStoneData stone in manifest.stones)
            {
                if (stone != null && string.Equals(stone.stone_id, routeData.stone_id, StringComparison.Ordinal))
                {
                    return Mathf.Max(0f, stone.equivalent_diameter_mm * 0.0005f);
                }
            }
        }
        return pointRadius * 0.675f;
    }

    private void HandleKeyboardInput()
    {
        if (Input.GetKeyDown(toggleOriginalKey)) ToggleOriginalPath();
        if (Input.GetKeyDown(toggleSmoothedKey)) ToggleRouteVisibility();
        if (Input.GetKeyDown(followRouteKey)) ToggleRouteFollow();
        if (Input.GetKeyDown(KeyCode.N)) NextRoute();
        if (Input.GetKeyDown(KeyCode.C)) NextCase();
        if (Input.GetKeyDown(KeyCode.R)) ResetView();
        if (Input.GetKeyDown(KeyCode.Equals)) AdjustMeshOpacity(0.05f);
        if (Input.GetKeyDown(KeyCode.Minus)) AdjustMeshOpacity(-0.05f);
    }

    private void HandleQuestControllerInput()
    {
        ControllerState left = ReadController(XRNode.LeftHand);
        ControllerState right = ReadController(XRNode.RightHand);
        if (left.primary && !previousLeft.primary) ToggleAnatomy();
        if (left.secondary && !previousLeft.secondary) ToggleRouteVisibility();
        if (right.primary && !previousRight.primary) ToggleRouteFollow();
        if (right.secondary && !previousRight.secondary) NextRoute();
        if (left.axisClick && !previousLeft.axisClick) ResetView();

        if (Time.time >= nextControllerRepeatTime)
        {
            if (Mathf.Abs(left.axis.y) > 0.7f)
            {
                AdjustMeshOpacity(left.axis.y > 0 ? 0.05f : -0.05f);
                nextControllerRepeatTime = Time.time + controllerRepeatSeconds;
            }
            else if (Mathf.Abs(right.axis.x) > 0.75f)
            {
                if (right.axis.x > 0) NextCase(); else PreviousCase();
                nextControllerRepeatTime = Time.time + controllerRepeatSeconds;
            }
        }
        previousLeft = left;
        previousRight = right;
    }

    private ControllerState ReadController(XRNode node)
    {
        ControllerState state = new ControllerState();
        controllerDevices.Clear();
        InputDevices.GetDevicesAtXRNode(node, controllerDevices);
        if (controllerDevices.Count == 0 || !controllerDevices[0].isValid)
        {
            return state;
        }
        InputDevice device = controllerDevices[0];
        state.valid = true;
        device.TryGetFeatureValue(CommonUsages.primaryButton, out state.primary);
        device.TryGetFeatureValue(CommonUsages.secondaryButton, out state.secondary);
        device.TryGetFeatureValue(CommonUsages.triggerButton, out state.trigger);
        device.TryGetFeatureValue(CommonUsages.primary2DAxisClick, out state.axisClick);
        device.TryGetFeatureValue(CommonUsages.primary2DAxis, out state.axis);
        return state;
    }

    public void NextCase()
    {
        if (!loading && CaseCount > 1) StartCoroutine(LoadCaseAt(currentCaseIndex + 1));
    }

    public void PreviousCase()
    {
        if (!loading && CaseCount > 1) StartCoroutine(LoadCaseAt(currentCaseIndex - 1));
    }

    public void NextRoute()
    {
        if (!loading && RouteCount > 1) SelectRoute(currentRouteIndex + 1);
    }

    public void ToggleAnatomy()
    {
        if (anatomyObject != null) anatomyObject.SetActive(!anatomyObject.activeSelf);
    }

    public void ToggleStones()
    {
        stonesVisible = !stonesVisible;
        if (stoneObject != null) stoneObject.SetActive(stonesVisible);
        if (targetObject != null) targetObject.SetActive(stonesVisible);
        if (contentRoot != null)
        {
            foreach (Transform child in contentRoot)
            {
                if (child.name.EndsWith(" marker", StringComparison.Ordinal)) child.gameObject.SetActive(stonesVisible);
            }
        }
    }

    public void ToggleOriginalPath()
    {
        if (originalPathObject != null) originalPathObject.SetActive(!originalPathObject.activeSelf);
    }

    public void ToggleRouteVisibility()
    {
        routeVisible = !routeVisible;
        if (routeRoot != null) routeRoot.gameObject.SetActive(routeVisible);
        if (!routeVisible) StopRouteAnimation();
    }

    public void ToggleRouteFollow()
    {
        if (!routeVisible || followMarkerObject == null) return;
        followingRoute = !followingRoute;
        followTimer = 0f;
        followMarkerObject.SetActive(followingRoute);
    }

    public void StopRouteAnimation()
    {
        followingRoute = false;
        if (followMarkerObject != null) followMarkerObject.SetActive(false);
    }

    public void AdjustMeshOpacity(float delta)
    {
        meshOpacity = Mathf.Clamp(meshOpacity + delta, 0.05f, 1f);
        if (anatomyMaterial == null) return;
        Color color = anatomyMaterial.color;
        color.a = meshOpacity;
        anatomyMaterial.color = color;
        ConfigureTransparentMaterial(anatomyMaterial);
    }

    public void ResetView()
    {
        StopRouteAnimation();
        manipulator?.ResetPose();
    }

    private void EnsureSceneRoot()
    {
        if (sceneRoot == null)
        {
            GameObject root = new GameObject("VR Case Root");
            sceneRoot = root.transform;
            sceneRoot.SetParent(transform, false);
        }
        EnsureContentRoot();
    }

    private void EnsureContentRoot()
    {
        if (contentRoot == null)
        {
            GameObject root = new GameObject("Case Content");
            contentRoot = root.transform;
            contentRoot.SetParent(sceneRoot, false);
        }
    }

    private void EnsureManipulator()
    {
        manipulator = GetComponent<VrModelManipulator>();
        if (manipulator == null) manipulator = gameObject.AddComponent<VrModelManipulator>();
        manipulator.target = sceneRoot;
        manipulator.trackingOrigin = cameraRig;
    }

    private void EnsureRuntimeInteractionObjects()
    {
        Transform origin = cameraRig != null ? cameraRig : Camera.main != null ? Camera.main.transform.parent : null;
        Camera mainCamera = Camera.main;
        if (origin != null && mainCamera != null)
        {
            XROrigin xrOrigin = origin.GetComponent<XROrigin>();
            if (xrOrigin == null)
            {
                xrOrigin = origin.gameObject.AddComponent<XROrigin>();
            }
            xrOrigin.Camera = mainCamera;
            xrOrigin.CameraFloorOffsetObject = mainCamera.transform.parent != null
                ? mainCamera.transform.parent.gameObject
                : origin.gameObject;
            xrOrigin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;
            EnsureRuntimeController(origin, "Runtime Left Controller", XRNode.LeftHand);
            EnsureRuntimeController(origin, "Runtime Right Controller", XRNode.RightHand);
        }

        VrWorldMenu menu = FindAnyObjectByType<VrWorldMenu>();
        if (menu == null)
        {
            GameObject menuObject = new GameObject("Runtime VR World Menu");
            menu = menuObject.AddComponent<VrWorldMenu>();
        }
        menu.loader = this;
    }

    private static void EnsureRuntimeController(Transform parent, string objectName, XRNode node)
    {
        foreach (XrHeadPoseDriver existing in parent.GetComponentsInChildren<XrHeadPoseDriver>(true))
        {
            if (existing.trackedNode == node)
            {
                VrControllerRay existingRay = existing.GetComponent<VrControllerRay>();
                if (existingRay == null) existingRay = existing.gameObject.AddComponent<VrControllerRay>();
                existingRay.controllerNode = node;
                return;
            }
        }
        Transform controller = parent.Find(objectName);
        if (controller == null)
        {
            GameObject controllerObject = new GameObject(objectName);
            controller = controllerObject.transform;
            controller.SetParent(parent, false);
        }
        XrHeadPoseDriver driver = controller.GetComponent<XrHeadPoseDriver>();
        if (driver == null) driver = controller.gameObject.AddComponent<XrHeadPoseDriver>();
        driver.trackedNode = node;
        VrControllerRay ray = controller.GetComponent<VrControllerRay>();
        if (ray == null) ray = controller.gameObject.AddComponent<VrControllerRay>();
        ray.controllerNode = node;
    }

    private void DestroyCurrentCase()
    {
        StopRouteAnimation();
        if (contentRoot != null)
        {
            Destroy(contentRoot.gameObject);
            contentRoot = null;
        }
        anatomyObject = null;
        stoneObject = null;
        startMarkerObject = null;
        targetObject = null;
        if (targetStoneMesh != null)
        {
            Destroy(targetStoneMesh);
            targetStoneMesh = null;
        }
        routeRoot = null;
        routeData = null;
        anatomyMaterial = null;
        EnsureContentRoot();
    }

    private void FrameCaseInFrontOfViewer()
    {
        Renderer[] renderers = contentRoot.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;
        Bounds bounds = renderers[0].bounds;
        for (int index = 1; index < renderers.Length; index++) bounds.Encapsulate(renderers[index].bounds);
        float largest = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        if (largest > maximumTabletopSize && largest > 0.0001f)
        {
            sceneRoot.localScale = Vector3.one * (maximumTabletopSize / largest);
            renderers = contentRoot.GetComponentsInChildren<Renderer>();
            bounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++) bounds.Encapsulate(renderers[index].bounds);
        }
        Transform viewer = Camera.main != null ? Camera.main.transform : cameraRig;
        if (viewer == null) return;
        Vector3 horizontalForward = Vector3.ProjectOnPlane(viewer.forward, Vector3.up).normalized;
        if (horizontalForward.sqrMagnitude < 0.01f) horizontalForward = Vector3.forward;
        Vector3 desiredCenter = viewer.position + horizontalForward * tabletopDistance + Vector3.up * tabletopHeight;
        sceneRoot.position += desiredCenter - bounds.center;
    }

    private void EnsureInformationText()
    {
        GameObject label = new GameObject("VR Information Panel");
        label.transform.SetParent(transform, false);
        informationText = label.AddComponent<TextMesh>();
        informationText.characterSize = 0.015f;
        informationText.fontSize = 48;
        informationText.anchor = TextAnchor.UpperLeft;
        informationText.color = Color.white;
        label.AddComponent<VrBillboard>();
    }

    private void UpdateInformationText()
    {
        if (informationText == null) return;
        Transform viewer = Camera.main != null ? Camera.main.transform : cameraRig;
        if (viewer != null)
        {
            informationText.transform.position = viewer.position + viewer.forward * 0.72f + viewer.right * -0.42f + viewer.up * 0.28f;
        }
        string routeMetrics = "";
        if (routeData?.metrics != null)
        {
            float length = routeData.metrics.smoothed_length_mm > 0
                ? routeData.metrics.smoothed_length_mm
                : routeData.metrics.smoothed_length_voxels;
            string units = routeData.metrics.smoothed_length_mm > 0 ? "mm" : "voxels";
            routeMetrics = $"\nRoute {currentRouteIndex + 1}/{Mathf.Max(1, RouteCount)}: {length:F1} {units}" +
                $" | outside: {routeData.metrics.outside_points}";
        }
        informationText.text =
            $"{CurrentCaseName}  Case {currentCaseIndex + 1}/{Mathf.Max(1, CaseCount)}{routeMetrics}\n" +
            $"{StatusMessage}\n" +
            "X anatomy | Y route | A animate | B next route\n" +
            "Left stick opacity/reset | Right stick cases | Grips move/scale\n" +
            "ACADEMIC PROTOTYPE - NOT FOR PATIENT CARE";
    }

    private void EnsureRuntimeLighting()
    {
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.36f, 0.39f, 0.44f);
        if (FindObjectsByType<Light>(FindObjectsInactive.Exclude).Length > 0) return;
        CreateLight("Runtime Key Light", 1.1f, Color.white, Quaternion.Euler(48f, -32f, 0f));
        CreateLight("Runtime Fill Light", 0.4f, new Color(0.55f, 0.65f, 1f), Quaternion.Euler(-25f, 130f, 0f));
    }

    private static void CreateLight(string name, float intensity, Color color, Quaternion rotation)
    {
        GameObject lightObject = new GameObject(name);
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = intensity;
        light.color = color;
        light.shadows = LightShadows.None;
        lightObject.transform.rotation = rotation;
    }

    private void ApplyMaterial(GameObject target, Color color, bool transparent)
    {
        Material material = BuildMaterial(color, transparent, !transparent);
        if (transparent) anatomyMaterial = material;
        foreach (Renderer renderer in target.GetComponentsInChildren<Renderer>())
        {
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }

    private Material BuildMaterial(Color color, bool transparent, bool emissive)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        Material material = new Material(shader) { color = color };
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", transparent ? 0.5f : 0.32f);
        if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", transparent ? 0.5f : 0.32f);
        if (emissive)
        {
            if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", color * 0.35f);
            material.EnableKeyword("_EMISSION");
        }
        if (transparent) ConfigureTransparentMaterial(material);
        material.doubleSidedGI = true;
        if (material.HasProperty("_Cull")) material.SetFloat("_Cull", (float)CullMode.Off);
        return material;
    }

    private static void ConfigureTransparentMaterial(Material material)
    {
        material.SetOverrideTag("RenderType", "Transparent");
        material.renderQueue = (int)RenderQueue.Transparent;
        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);
        if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
        if (material.HasProperty("_Mode")) material.SetFloat("_Mode", 3f);
        material.EnableKeyword("_ALPHABLEND_ON");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHATEST_ON");
    }

    private static void ConfigureOpaqueMaterial(Material material)
    {
        material.SetOverrideTag("RenderType", "Opaque");
        material.renderQueue = (int)RenderQueue.Geometry;
        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 0f);
        if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)BlendMode.One);
        if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)BlendMode.Zero);
        if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 1f);
        if (material.HasProperty("_Mode")) material.SetFloat("_Mode", 0f);
        material.DisableKeyword("_ALPHABLEND_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHATEST_ON");
    }

    private void SetStatus(string message)
    {
        StatusMessage = message;
        Debug.Log(message);
    }

    private void FailLoad(string message)
    {
        loading = false;
        SetStatus("ERROR: " + message);
        Debug.LogError(message);
    }

    private static int WrapIndex(int value, int count)
    {
        if (count <= 0) return 0;
        return ((value % count) + count) % count;
    }

    private static string CombineRelative(string first, string second)
    {
        return (first.TrimEnd('/', '\\') + "/" + second.TrimStart('/', '\\')).Replace('\\', '/');
    }

    private static string GetRelativeDirectory(string path)
    {
        int separator = path.LastIndexOf('/');
        return separator >= 0 ? path.Substring(0, separator) : string.Empty;
    }
}
