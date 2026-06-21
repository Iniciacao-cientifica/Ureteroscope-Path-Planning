using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.XR;

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
    public float followCameraDistance = 0.16f;
    public float followCameraHeight = 0.08f;
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
    public bool enableQuestControllerInput = true;
    public float controllerOpacityRepeatSeconds = 0.18f;

    private VrRouteData routeData;
    private GameObject originalPathObject;
    private GameObject smoothedPathObject;
    private GameObject urinaryTractInstance;
    private Renderer[] meshRenderers;
    private Material meshMaterial;
    private float followTimer;
    private bool followingRoute;
    private bool previousPrimaryButton;
    private bool previousSecondaryButton;
    private bool previousTriggerButton;
    private float nextControllerOpacityTime;
    private readonly List<InputDevice> controllerDevices = new List<InputDevice>();

    private void Start()
    {
        EnsureSceneRoot();
        LoadCase();
        SetupImportedMesh();
        BuildRouteVisuals();
        BuildPoint("Start", routeData.start, startColor, pointRadius);
        BuildPoint("Target Stone", routeData.target, targetColor, pointRadius * 1.5f);
        BuildMetricsLabel();
        FrameCameraOverview();
    }

    private void Update()
    {
        HandleKeyboardInput();

        if (enableQuestControllerInput)
        {
            HandleQuestControllerInput();
        }

        if (followingRoute)
        {
            FollowSmoothedRoute();
        }
    }

    private void HandleKeyboardInput()
    {
        if (Input.GetKeyDown(toggleOriginalKey))
        {
            ToggleOriginalPath();
        }

        if (Input.GetKeyDown(toggleSmoothedKey))
        {
            ToggleSmoothedPath();
        }

        if (Input.GetKeyDown(increaseOpacityKey))
        {
            AdjustMeshOpacity(0.05f);
        }

        if (Input.GetKeyDown(decreaseOpacityKey))
        {
            AdjustMeshOpacity(-0.05f);
        }

        if (Input.GetKeyDown(followRouteKey))
        {
            ToggleRouteFollow();
        }
    }

    private void HandleQuestControllerInput()
    {
        bool primaryButton = false;
        bool secondaryButton = false;
        bool triggerButton = false;
        Vector2 thumbstick = Vector2.zero;

        ReadController(InputDeviceCharacteristics.Left, ref primaryButton, ref secondaryButton, ref triggerButton, ref thumbstick);
        ReadController(InputDeviceCharacteristics.Right, ref primaryButton, ref secondaryButton, ref triggerButton, ref thumbstick);

        if (primaryButton && !previousPrimaryButton)
        {
            ToggleOriginalPath();
        }

        if (secondaryButton && !previousSecondaryButton)
        {
            ToggleSmoothedPath();
        }

        if (triggerButton && !previousTriggerButton)
        {
            ToggleRouteFollow();
        }

        if (Mathf.Abs(thumbstick.y) > 0.65f && Time.time >= nextControllerOpacityTime)
        {
            AdjustMeshOpacity(thumbstick.y > 0f ? 0.05f : -0.05f);
            nextControllerOpacityTime = Time.time + controllerOpacityRepeatSeconds;
        }

        previousPrimaryButton = primaryButton;
        previousSecondaryButton = secondaryButton;
        previousTriggerButton = triggerButton;
    }

    private void ReadController(
        InputDeviceCharacteristics hand,
        ref bool primaryButton,
        ref bool secondaryButton,
        ref bool triggerButton,
        ref Vector2 thumbstick
    )
    {
        controllerDevices.Clear();
        InputDevices.GetDevicesWithCharacteristics(
            InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.HeldInHand | hand,
            controllerDevices
        );

        if (controllerDevices.Count == 0)
        {
            return;
        }

        InputDevice device = controllerDevices[0];
        if (!device.isValid)
        {
            return;
        }

        if (device.TryGetFeatureValue(CommonUsages.primaryButton, out bool primary))
        {
            primaryButton |= primary;
        }

        if (device.TryGetFeatureValue(CommonUsages.secondaryButton, out bool secondary))
        {
            secondaryButton |= secondary;
        }

        if (device.TryGetFeatureValue(CommonUsages.triggerButton, out bool trigger))
        {
            triggerButton |= trigger;
        }

        if (device.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 axis) && axis.sqrMagnitude > thumbstick.sqrMagnitude)
        {
            thumbstick = axis;
        }
    }

    private void ToggleOriginalPath()
    {
        if (originalPathObject != null)
        {
            originalPathObject.SetActive(!originalPathObject.activeSelf);
        }
    }

    private void ToggleSmoothedPath()
    {
        if (smoothedPathObject != null)
        {
            smoothedPathObject.SetActive(!smoothedPathObject.activeSelf);
        }
    }

    private void ToggleRouteFollow()
    {
        followingRoute = !followingRoute;
        followTimer = 0f;
    }

    private void AdjustMeshOpacity(float delta)
    {
        SetMeshOpacity(Mathf.Clamp01(meshOpacity + delta));
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
        routeData = VrRouteJsonParser.Parse(json);
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

        urinaryTractInstance = urinaryTractMesh.scene.IsValid()
            ? urinaryTractMesh
            : Instantiate(urinaryTractMesh);

        urinaryTractInstance.name = "Urinary Tract Mesh";
        urinaryTractInstance.transform.SetParent(sceneRoot, false);
        urinaryTractInstance.transform.localScale = Vector3.one * voxelToMeterScale;
        urinaryTractInstance.transform.localRotation = Quaternion.identity;
        urinaryTractInstance.transform.localPosition = Vector3.zero;

        meshRenderers = urinaryTractInstance.GetComponentsInChildren<Renderer>();
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
        Bounds bounds = CalculateRouteBounds();
        label.transform.localPosition = bounds.center + new Vector3(bounds.size.x * 0.6f + 0.08f, bounds.size.y * 0.35f + 0.06f, 0f);
        label.transform.localRotation = Quaternion.Euler(20f, -25f, 0f);
        TextMesh text = label.AddComponent<TextMesh>();
        text.characterSize = 0.012f;
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
            "Quest: A/X original, B/Y smoothed\n" +
            "Trigger follow, thumbstick opacity\n" +
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

    private Bounds CalculateRouteBounds()
    {
        VrPoint[] points = routeData.path_smoothed != null && routeData.path_smoothed.Length > 0
            ? routeData.path_smoothed
            : routeData.path_original;

        Bounds bounds = new Bounds(MapPoint(points[0]), Vector3.zero);
        for (int i = 1; i < points.Length; i++)
        {
            bounds.Encapsulate(MapPoint(points[i]));
        }

        bounds.Expand(0.15f);
        return bounds;
    }

    private void FrameCameraOverview()
    {
        Transform rig = cameraRig != null ? cameraRig : Camera.main != null ? Camera.main.transform : null;
        if (rig == null)
        {
            return;
        }

        Bounds bounds = CalculateRouteBounds();
        float size = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        Vector3 center = sceneRoot.TransformPoint(bounds.center);
        Vector3 offset = new Vector3(size * 0.75f, size * 0.45f, -size * 1.25f);

        rig.position = center + offset;
        Vector3 direction = center - rig.position;
        if (direction.sqrMagnitude > 0.0001f)
        {
            rig.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }
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

        Vector3 routePosition = Vector3.Lerp(
            MapPoint(routeData.path_smoothed[indexA]),
            MapPoint(routeData.path_smoothed[indexB]),
            localT
        );
        Vector3 lookAt = MapPoint(routeData.path_smoothed[Mathf.Min(indexB + 3, routeData.path_smoothed.Length - 1)]);

        Vector3 forward = (lookAt - routePosition).normalized;
        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = Vector3.forward;
        }

        Vector3 cameraPosition = routePosition - forward * followCameraDistance + Vector3.up * followCameraHeight;
        rig.position = sceneRoot.TransformPoint(cameraPosition);
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

public static class VrRouteJsonParser
{
    public static VrRouteData Parse(string json)
    {
        VrRouteData data = new VrRouteData
        {
            case_name = MatchString(json, "case_name"),
            clinical_notice = MatchString(json, "clinical_notice"),
            start = MatchPoint(json, "start"),
            target = MatchPoint(json, "target"),
            path_original = MatchPointArray(json, "path_original"),
            path_smoothed = MatchPointArray(json, "path_smoothed"),
            metrics = MatchMetrics(json)
        };

        return data;
    }

    private static VrRouteMetrics MatchMetrics(string json)
    {
        string block = MatchObjectBlock(json, "metrics");
        return new VrRouteMetrics
        {
            path_points = (int)MatchFloat(block, "path_points"),
            exported_path_points = (int)MatchFloat(block, "exported_path_points"),
            smoothed_points = (int)MatchFloat(block, "smoothed_points"),
            path_length_voxels = MatchFloat(block, "path_length_voxels"),
            smoothed_length_voxels = MatchFloat(block, "smoothed_length_voxels"),
            risk_points = (int)MatchFloat(block, "risk_points"),
            final_error_voxels = MatchFloat(block, "final_error_voxels"),
            processing_seconds = MatchFloat(block, "processing_seconds"),
            curvature_mean = MatchFloat(block, "curvature_mean"),
            curvature_max = MatchFloat(block, "curvature_max"),
            torsion_mean = MatchFloat(block, "torsion_mean"),
            torsion_max = MatchFloat(block, "torsion_max"),
            outside_points = (int)MatchFloat(block, "outside_points"),
            outside_percent = MatchFloat(block, "outside_percent"),
            outside_max_distance = MatchFloat(block, "outside_max_distance"),
            outside_mean_distance = MatchFloat(block, "outside_mean_distance")
        };
    }

    private static VrPoint MatchPoint(string json, string key)
    {
        return MatchPointFromBlock(MatchObjectBlock(json, key));
    }

    private static VrPoint[] MatchPointArray(string json, string key)
    {
        string array = MatchArrayBlock(json, key);
        MatchCollection matches = Regex.Matches(
            array,
            "\\{\\s*\"x\"\\s*:\\s*(?<x>-?\\d+(?:\\.\\d+)?(?:[eE][+-]?\\d+)?)\\s*,\\s*\"y\"\\s*:\\s*(?<y>-?\\d+(?:\\.\\d+)?(?:[eE][+-]?\\d+)?)\\s*,\\s*\"z\"\\s*:\\s*(?<z>-?\\d+(?:\\.\\d+)?(?:[eE][+-]?\\d+)?)\\s*\\}"
        );

        VrPoint[] points = new VrPoint[matches.Count];
        for (int i = 0; i < matches.Count; i++)
        {
            points[i] = new VrPoint
            {
                x = ParseFloat(matches[i].Groups["x"].Value),
                y = ParseFloat(matches[i].Groups["y"].Value),
                z = ParseFloat(matches[i].Groups["z"].Value)
            };
        }

        return points;
    }

    private static VrPoint MatchPointFromBlock(string block)
    {
        return new VrPoint
        {
            x = MatchFloat(block, "x"),
            y = MatchFloat(block, "y"),
            z = MatchFloat(block, "z")
        };
    }

    private static string MatchString(string json, string key)
    {
        Match match = Regex.Match(json, $"\"{key}\"\\s*:\\s*\"(?<value>[^\"]*)\"");
        return match.Success ? match.Groups["value"].Value : string.Empty;
    }

    private static float MatchFloat(string json, string key)
    {
        Match match = Regex.Match(
            json,
            $"\"{key}\"\\s*:\\s*(?<value>-?\\d+(?:\\.\\d+)?(?:[eE][+-]?\\d+)?)"
        );
        return match.Success ? ParseFloat(match.Groups["value"].Value) : 0f;
    }

    private static string MatchObjectBlock(string json, string key)
    {
        int keyIndex = json.IndexOf($"\"{key}\"", StringComparison.Ordinal);
        if (keyIndex < 0)
        {
            return string.Empty;
        }

        int start = json.IndexOf('{', keyIndex);
        return ReadBalanced(json, start, '{', '}');
    }

    private static string MatchArrayBlock(string json, string key)
    {
        int keyIndex = json.IndexOf($"\"{key}\"", StringComparison.Ordinal);
        if (keyIndex < 0)
        {
            return string.Empty;
        }

        int start = json.IndexOf('[', keyIndex);
        return ReadBalanced(json, start, '[', ']');
    }

    private static string ReadBalanced(string text, int start, char open, char close)
    {
        if (start < 0)
        {
            return string.Empty;
        }

        int depth = 0;
        for (int i = start; i < text.Length; i++)
        {
            if (text[i] == open)
            {
                depth++;
            }
            else if (text[i] == close)
            {
                depth--;
                if (depth == 0)
                {
                    return text.Substring(start, i - start + 1);
                }
            }
        }

        return string.Empty;
    }

    private static float ParseFloat(string value)
    {
        return float.Parse(value, CultureInfo.InvariantCulture);
    }
}
