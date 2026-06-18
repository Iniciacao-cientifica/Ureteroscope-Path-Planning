using System;
using System.IO;
using System.Text;
using UnityEngine;

public class VrCaseLoader : MonoBehaviour
{
    [Header("Data")]
    public string routeFileName = "vr_route_unity.json";
    public GameObject urinaryTractMesh;

    [Header("Scene")]
    public Transform sceneRoot;
    public Transform cameraRig;
    public bool mapMedicalZToUnityY = true;
    public float voxelToMeterScale = 0.002f;
    public float routeWidth = 0.01f;
    public float pointRadius = 0.025f;
    [Range(0.05f, 1f)] public float meshOpacity = 0.35f;

    [Header("Route Colors")]
    public Color originalPathColor = new Color(0.1f, 0.9f, 0.25f, 1f);
    public Color smoothedPathColor = new Color(0.1f, 0.35f, 1f, 1f);
    public Color startColor = new Color(0.05f, 0.9f, 0.2f, 1f);
    public Color targetColor = new Color(0.9f, 0.65f, 0.05f, 1f);

    [Header("Controls")]
    public KeyCode toggleOriginalKey = KeyCode.O;
    public KeyCode toggleSmoothedKey = KeyCode.P;
    public KeyCode followRouteKey = KeyCode.F;
    public KeyCode increaseOpacityKey = KeyCode.Equals;
    public KeyCode decreaseOpacityKey = KeyCode.Minus;
    public float followDurationSeconds = 18f;

    private VrRouteData routeData;
    private GameObject originalPathObject;
    private GameObject smoothedPathObject;
    private Renderer[] meshRenderers;
    private Material meshMaterial;
    private float followTimer;
    private bool followingRoute;

    private void Start()
    {
        EnsureSceneRoot();
        LoadCase();
        SetupImportedMesh();
        BuildRouteVisuals();
        BuildPoint("Start", routeData.start, startColor, pointRadius);
        BuildPoint("Target Stone", routeData.target, targetColor, pointRadius * 1.5f);
        BuildMetricsLabel();
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleOriginalKey) && originalPathObject != null)
        {
            originalPathObject.SetActive(!originalPathObject.activeSelf);
        }

        if (Input.GetKeyDown(toggleSmoothedKey) && smoothedPathObject != null)
        {
            smoothedPathObject.SetActive(!smoothedPathObject.activeSelf);
        }

        if (Input.GetKeyDown(increaseOpacityKey))
        {
            SetMeshOpacity(Mathf.Clamp01(meshOpacity + 0.05f));
        }

        if (Input.GetKeyDown(decreaseOpacityKey))
        {
            SetMeshOpacity(Mathf.Clamp01(meshOpacity - 0.05f));
        }

        if (Input.GetKeyDown(followRouteKey))
        {
            followingRoute = !followingRoute;
            followTimer = 0f;
        }

        if (followingRoute)
        {
            FollowSmoothedRoute();
        }
    }

    private void EnsureSceneRoot()
    {
        if (sceneRoot != null)
        {
            return;
        }

        GameObject root = new GameObject("VR Case Root");
        sceneRoot = root.transform;
        sceneRoot.SetParent(transform, false);
    }

    private void LoadCase()
    {
        string path = Path.Combine(Application.streamingAssetsPath, routeFileName);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"Route JSON not found. Copy vr_route_unity.json to StreamingAssets. Expected: {path}"
            );
        }

        string json = File.ReadAllText(path, Encoding.UTF8);
        routeData = JsonUtility.FromJson<VrRouteData>(json);
        if (routeData == null || routeData.path_smoothed == null || routeData.path_smoothed.Length == 0)
        {
            throw new InvalidOperationException("Route JSON is empty or incompatible with VrRouteData.");
        }
    }

    private void SetupImportedMesh()
    {
        if (urinaryTractMesh == null)
        {
            return;
        }

        urinaryTractMesh.transform.SetParent(sceneRoot, false);
        urinaryTractMesh.transform.localScale = Vector3.one * voxelToMeterScale;
        urinaryTractMesh.transform.localRotation = Quaternion.identity;
        urinaryTractMesh.transform.localPosition = Vector3.zero;

        meshRenderers = urinaryTractMesh.GetComponentsInChildren<Renderer>();
        meshMaterial = BuildMaterial(new Color(0.95f, 0.12f, 0.05f, meshOpacity));
        foreach (Renderer meshRenderer in meshRenderers)
        {
            meshRenderer.material = meshMaterial;
        }

        SetMeshOpacity(meshOpacity);
    }

    private void BuildRouteVisuals()
    {
        originalPathObject = BuildLine("A* Original Path", routeData.path_original, originalPathColor, routeWidth * 0.75f);
        smoothedPathObject = BuildLine("B-Spline Smoothed Path", routeData.path_smoothed, smoothedPathColor, routeWidth);
        if (originalPathObject != null)
        {
            originalPathObject.SetActive(false);
        }
    }

    private GameObject BuildLine(string lineName, VrPoint[] points, Color color, float width)
    {
        if (points == null || points.Length == 0)
        {
            return null;
        }

        GameObject lineObject = new GameObject(lineName);
        lineObject.transform.SetParent(sceneRoot, false);
        LineRenderer lineRenderer = lineObject.AddComponent<LineRenderer>();
        lineRenderer.useWorldSpace = false;
        lineRenderer.positionCount = points.Length;
        lineRenderer.startWidth = width;
        lineRenderer.endWidth = width;
        lineRenderer.numCapVertices = 8;
        lineRenderer.numCornerVertices = 8;
        lineRenderer.material = BuildMaterial(color);

        for (int i = 0; i < points.Length; i++)
        {
            lineRenderer.SetPosition(i, MapPoint(points[i]));
        }

        return lineObject;
    }

    private void BuildPoint(string pointName, VrPoint point, Color color, float radius)
    {
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = pointName;
        sphere.transform.SetParent(sceneRoot, false);
        sphere.transform.localPosition = MapPoint(point);
        sphere.transform.localScale = Vector3.one * radius;
        sphere.GetComponent<Renderer>().material = BuildMaterial(color);
    }

    private void BuildMetricsLabel()
    {
        GameObject label = new GameObject("Case Metrics Label");
        label.transform.SetParent(sceneRoot, false);
        label.transform.localPosition = MapPoint(routeData.target) + new Vector3(0.05f, 0.08f, 0.05f);
        TextMesh text = label.AddComponent<TextMesh>();
        text.characterSize = 0.025f;
        text.anchor = TextAnchor.MiddleLeft;
        text.alignment = TextAlignment.Left;
        text.color = Color.white;
        text.text = BuildMetricsText();
    }

    private string BuildMetricsText()
    {
        VrRouteMetrics metrics = routeData.metrics;
        return
            $"{routeData.case_name}\n" +
            $"B-Spline length: {metrics.smoothed_length_voxels:F1} voxels\n" +
            $"Risk points: {metrics.risk_points}\n" +
            $"Outside points: {metrics.outside_points}\n" +
            $"Curvature max: {metrics.curvature_max:F3}\n" +
            $"Torsion max: {metrics.torsion_max:F3}\n" +
            "Prototype for training/planning only";
    }

    private Vector3 MapPoint(VrPoint point)
    {
        if (mapMedicalZToUnityY)
        {
            return new Vector3(point.x, point.z, point.y) * voxelToMeterScale;
        }

        return new Vector3(point.x, point.y, point.z) * voxelToMeterScale;
    }

    private Material BuildMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        Material material = new Material(shader);
        material.color = color;
        return material;
    }

    private void SetMeshOpacity(float opacity)
    {
        meshOpacity = opacity;
        if (meshMaterial == null)
        {
            return;
        }

        Color color = meshMaterial.color;
        color.a = meshOpacity;
        meshMaterial.color = color;
        meshMaterial.SetFloat("_Surface", 1f);
        meshMaterial.SetFloat("_Blend", 0f);
        meshMaterial.SetOverrideTag("RenderType", "Transparent");
        meshMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }

    private void FollowSmoothedRoute()
    {
        if (routeData.path_smoothed.Length < 2)
        {
            followingRoute = false;
            return;
        }

        Transform rig = cameraRig != null ? cameraRig : Camera.main != null ? Camera.main.transform : null;
        if (rig == null)
        {
            followingRoute = false;
            return;
        }

        followTimer += Time.deltaTime;
        float normalizedTime = Mathf.Clamp01(followTimer / Mathf.Max(0.01f, followDurationSeconds));
        float exactIndex = normalizedTime * (routeData.path_smoothed.Length - 1);
        int indexA = Mathf.FloorToInt(exactIndex);
        int indexB = Mathf.Min(indexA + 1, routeData.path_smoothed.Length - 1);
        float localT = exactIndex - indexA;

        Vector3 position = Vector3.Lerp(
            MapPoint(routeData.path_smoothed[indexA]),
            MapPoint(routeData.path_smoothed[indexB]),
            localT
        );
        Vector3 lookAt = MapPoint(routeData.path_smoothed[Mathf.Min(indexB + 3, routeData.path_smoothed.Length - 1)]);

        rig.position = sceneRoot.TransformPoint(position);
        Vector3 direction = sceneRoot.TransformPoint(lookAt) - rig.position;
        if (direction.sqrMagnitude > 0.0001f)
        {
            rig.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }

        if (normalizedTime >= 1f)
        {
            followingRoute = false;
        }
    }
}
