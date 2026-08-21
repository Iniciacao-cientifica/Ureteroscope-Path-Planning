using System;
using UnityEngine;

public sealed class TrainingNavigationVisuals : MonoBehaviour
{
    public const int GuidanceLayer = 28;
    public const int MinimapOnlyLayer = 30;

    [Header("External exploration route")]
    [Min(0.0005f)] public float explorationRouteWidthMeters = 0.001f;
    [Min(0.0005f)] public float minimapRouteWidthMeters = 0.0015f;
    [Min(0.0001f)] public float stoneHaloWidthMeters = 0.0004f;
    [Min(1f)] public float stoneHaloDiameterMultiplier = 1.6f;

    [Header("Internal training route")]
    [Min(0.0005f)] public float trainingRouteWidthMeters = 0.001f;
    [Min(0.0005f)] public float trainingMinimapRouteWidthMeters = 0.0015f;

    private Camera viewingCamera;
    private ITrainingCourseView course;
    private Transform probe;
    private Vector3[] routeLocal = Array.Empty<Vector3>();
    private GameObject arrowRoot;
    private GameObject environmentRoot;
    private Material arrowOutlineMaterial;
    private Material arrowShaftMaterial;
    private Material arrowHeadMaterial;
    private Material arrowSideMaterial;
    private Material environmentMaterial;
    private Material gridMaterial;
    private GameObject explorationRouteRoot;
    private GameObject minimapRouteRoot;
    private GameObject stoneHaloRoot;
    private LineRenderer explorationRouteLine;
    private LineRenderer minimapRouteLine;
    private LineRenderer stoneHaloLine;
    private Material explorationRouteMaterial;
    private Material minimapRouteMaterial;
    private Material stoneHaloMaterial;
    private GameObject trainingRouteRoot;
    private GameObject trainingMinimapRouteRoot;
    private LineRenderer trainingRouteLine;
    private LineRenderer trainingMinimapRouteLine;
    private Material trainingRouteMaterial;
    private Material trainingMinimapRouteMaterial;
    private GameObject fullRouteObject;
    private Renderer fullRouteRenderer;
    private int fullRouteOriginalLayer;
    private float fullRouteOriginalCull;
    private bool fullRouteHasCull;
    private bool explorationRouteActive;
    private bool trainingGuidanceActive;

    public Vector2 CurrentScreenDirection { get; private set; } = Vector2.up;
    public bool AdaptiveRouteActive => explorationRouteActive;
    public bool NearRouteMode => false;
    public LineRenderer NearRouteLine => explorationRouteLine;
    public LineRenderer ExplorationRouteLine => explorationRouteLine;
    public LineRenderer MinimapRouteLine => minimapRouteLine;
    public LineRenderer StoneHaloLine => stoneHaloLine;
    public bool TrainingGuidanceActive => trainingGuidanceActive;
    public LineRenderer TrainingRouteLine => trainingRouteLine;
    public LineRenderer TrainingMinimapRouteLine => trainingMinimapRouteLine;
    public Renderer FullRouteRenderer => fullRouteRenderer;

    public void Configure(Camera camera, ITrainingCourseView courseView, Transform probeTransform, Vector3[] route)
    {
        RestoreFullRoutePresentation();
        DestroyExplorationVisuals();
        DestroyTrainingVisuals();
        viewingCamera = camera;
        course = courseView;
        probe = probeTransform;
        routeLocal = route ?? Array.Empty<Vector3>();
        EnsureArrow();
        if (environmentRoot != null) Destroy(environmentRoot);
        environmentRoot = null;
        SetPresentation(false, false);
        SetExternalExplorationActive(false);
        SetTrainingGuidanceActive(false);
    }

    public void SetPresentation(bool showArrow, bool showEnvironment)
    {
        if (arrowRoot != null) arrowRoot.SetActive(showArrow);
        if (environmentRoot != null) environmentRoot.SetActive(false);
    }

    public void SetExternalExplorationActive(bool active)
    {
        bool shouldActivate = active && course != null && course.SmoothedPathObject != null && routeLocal.Length > 1;
        if (!shouldActivate)
        {
            explorationRouteActive = false;
            DestroyExplorationVisuals();
            UpdateFullRouteVisibility();
            return;
        }

        if (explorationRouteRoot == null) BuildExplorationVisuals();
        explorationRouteActive = explorationRouteRoot != null;
        if (!explorationRouteActive) return;
        CaptureFullRoutePresentation();
        UpdateFullRouteVisibility();
        explorationRouteRoot.SetActive(true);
        if (minimapRouteRoot != null) minimapRouteRoot.SetActive(true);
        if (stoneHaloRoot != null) stoneHaloRoot.SetActive(true);
        UpdateStoneHalo();
    }

    public void SetTrainingGuidanceActive(bool active)
    {
        bool shouldActivate = active && course != null && course.SmoothedPathObject != null && routeLocal.Length > 1;
        if (!shouldActivate)
        {
            trainingGuidanceActive = false;
            DestroyTrainingVisuals();
            UpdateFullRouteVisibility();
            return;
        }

        if (trainingRouteRoot == null) BuildTrainingVisuals();
        trainingGuidanceActive = trainingRouteRoot != null;
        if (!trainingGuidanceActive) return;
        CaptureFullRoutePresentation();
        trainingRouteRoot.SetActive(true);
        if (trainingMinimapRouteRoot != null) trainingMinimapRouteRoot.SetActive(true);
        UpdateFullRouteVisibility();
    }

    public void SetAdaptiveRouteActive(bool active)
    {
        SetExternalExplorationActive(active);
    }

    public void TickAdaptiveRoute(float distanceFromRouteMillimeters)
    {
        if (explorationRouteActive) UpdateStoneHalo();
    }

    private void BuildExplorationVisuals()
    {
        if (course?.ContentRoot == null || routeLocal.Length < 2) return;

        Shader routeShader = Shader.Find("Murillo/Training Route Opaque");
        if (routeShader == null) routeShader = Shader.Find("Sprites/Default");
        explorationRouteMaterial = new Material(routeShader)
        {
            name = "External Exploration Route Material",
            color = new Color(0.04f, 0.32f, 1f, 1f),
            renderQueue = 4997
        };
        SetDepthTest(explorationRouteMaterial, UnityEngine.Rendering.CompareFunction.Always);

        explorationRouteRoot = new GameObject("External Exploration Route");
        explorationRouteRoot.layer = GuidanceLayer;
        explorationRouteRoot.transform.SetParent(course.ContentRoot, false);
        explorationRouteLine = CreateRouteLine("Thin Blue Full Route", explorationRouteRoot.transform, GuidanceLayer,
            explorationRouteWidthMeters, explorationRouteMaterial);
        explorationRouteLine.positionCount = routeLocal.Length;
        explorationRouteLine.SetPositions(routeLocal);

        Color minimapColor = course.RouteColor;
        minimapColor.a = 1f;
        minimapRouteMaterial = new Material(routeShader)
        {
            name = "External Route Minimap Material",
            color = minimapColor,
            renderQueue = 4997
        };
        SetDepthTest(minimapRouteMaterial, UnityEngine.Rendering.CompareFunction.Always);
        minimapRouteRoot = new GameObject("External Route Minimap");
        minimapRouteRoot.layer = MinimapOnlyLayer;
        minimapRouteRoot.transform.SetParent(course.ContentRoot, false);
        minimapRouteLine = CreateRouteLine("Full Route Minimap", minimapRouteRoot.transform, MinimapOnlyLayer,
            minimapRouteWidthMeters, minimapRouteMaterial);
        minimapRouteLine.positionCount = routeLocal.Length;
        minimapRouteLine.SetPositions(routeLocal);

        Shader sprite = Shader.Find("Sprites/Default");
        Shader haloShader = Shader.Find("Murillo/Training Overlay Unlit");
        if (haloShader == null) haloShader = sprite;
        stoneHaloMaterial = CreateOverlayMaterial(haloShader, new Color(1f, 0.48f, 0.06f, 0.86f), 4998);
        stoneHaloRoot = new GameObject("Target Stone Halo");
        stoneHaloRoot.layer = GuidanceLayer;
        stoneHaloLine = CreateRouteLine("Amber Stone Halo Ring", stoneHaloRoot.transform, GuidanceLayer,
            stoneHaloWidthMeters, stoneHaloMaterial);
        stoneHaloLine.useWorldSpace = true;
        stoneHaloLine.loop = true;

        SetLayerRecursively(explorationRouteRoot.transform, GuidanceLayer);
        SetLayerRecursively(minimapRouteRoot.transform, MinimapOnlyLayer);
        SetLayerRecursively(stoneHaloRoot.transform, GuidanceLayer);
    }

    private void BuildTrainingVisuals()
    {
        if (course?.ContentRoot == null || routeLocal.Length < 2) return;

        Shader routeShader = Shader.Find("Murillo/Training Route Opaque");
        if (routeShader == null) routeShader = Shader.Find("Sprites/Default");
        trainingRouteMaterial = new Material(routeShader)
        {
            name = "Internal Training Route Material",
            color = new Color(0.035f, 0.24f, 0.95f, 1f),
            renderQueue = 2450
        };
        SetDepthTest(trainingRouteMaterial, UnityEngine.Rendering.CompareFunction.LessEqual);

        trainingRouteRoot = new GameObject("Internal Training Guidance Route");
        trainingRouteRoot.layer = GuidanceLayer;
        trainingRouteRoot.transform.SetParent(course.ContentRoot, false);
        trainingRouteLine = CreateRouteLine("Internal Thin Blue Route", trainingRouteRoot.transform, GuidanceLayer,
            trainingRouteWidthMeters, trainingRouteMaterial);
        trainingRouteLine.positionCount = routeLocal.Length;
        trainingRouteLine.SetPositions(routeLocal);

        Color minimapColor = course.RouteColor;
        minimapColor.a = 1f;
        trainingMinimapRouteMaterial = new Material(routeShader)
        {
            name = "Training Route Minimap Material",
            color = minimapColor,
            renderQueue = 4997
        };
        SetDepthTest(trainingMinimapRouteMaterial, UnityEngine.Rendering.CompareFunction.Always);
        trainingMinimapRouteRoot = new GameObject("Training Route Minimap");
        trainingMinimapRouteRoot.layer = MinimapOnlyLayer;
        trainingMinimapRouteRoot.transform.SetParent(course.ContentRoot, false);
        trainingMinimapRouteLine = CreateRouteLine("Full Training Route Minimap", trainingMinimapRouteRoot.transform,
            MinimapOnlyLayer, trainingMinimapRouteWidthMeters, trainingMinimapRouteMaterial);
        trainingMinimapRouteLine.positionCount = routeLocal.Length;
        trainingMinimapRouteLine.SetPositions(routeLocal);

        SetLayerRecursively(trainingRouteRoot.transform, GuidanceLayer);
        SetLayerRecursively(trainingMinimapRouteRoot.transform, MinimapOnlyLayer);
    }

    private static void SetDepthTest(Material material, UnityEngine.Rendering.CompareFunction comparison)
    {
        if (material != null && material.HasProperty("_ZTest")) material.SetFloat("_ZTest", (float)comparison);
    }

    private static LineRenderer CreateRouteLine(string objectName, Transform parent, int layer, float width, Material material)
    {
        GameObject lineObject = new GameObject(objectName);
        lineObject.layer = layer;
        lineObject.transform.SetParent(parent, false);
        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.alignment = LineAlignment.View;
        line.textureMode = LineTextureMode.Stretch;
        line.numCapVertices = 4;
        line.numCornerVertices = 4;
        line.startWidth = width;
        line.endWidth = width;
        line.sharedMaterial = material;
        line.startColor = Color.white;
        line.endColor = Color.white;
        return line;
    }

    private void UpdateStoneHalo()
    {
        if (!explorationRouteActive || stoneHaloLine == null || viewingCamera == null || course?.CurrentTargetObject == null) return;
        const int samples = 48;
        float radius = course.CurrentStoneDiameterMeters * stoneHaloDiameterMultiplier * 0.5f;
        Vector3 center = course.CurrentTargetObject.transform.position;
        Vector3 right = viewingCamera.transform.right;
        Vector3 up = viewingCamera.transform.up;
        stoneHaloLine.positionCount = samples;
        for (int index = 0; index < samples; index++)
        {
            float angle = index / (float)samples * Mathf.PI * 2f;
            stoneHaloLine.SetPosition(index, center + right * (Mathf.Cos(angle) * radius) + up * (Mathf.Sin(angle) * radius));
        }
    }

    private void CaptureFullRoutePresentation()
    {
        if (fullRouteObject != null || course?.SmoothedPathObject == null) return;
        fullRouteObject = course.SmoothedPathObject;
        fullRouteOriginalLayer = fullRouteObject.layer;
        fullRouteRenderer = fullRouteObject.GetComponent<Renderer>();
        Material material = fullRouteRenderer != null ? fullRouteRenderer.sharedMaterial : null;
        fullRouteHasCull = material != null && material.HasProperty("_Cull");
        if (fullRouteHasCull) fullRouteOriginalCull = material.GetFloat("_Cull");
    }

    private void UpdateFullRouteVisibility()
    {
        if (explorationRouteActive || trainingGuidanceActive)
        {
            CaptureFullRoutePresentation();
            if (fullRouteRenderer != null) fullRouteRenderer.enabled = false;
            return;
        }
        RestoreFullRoutePresentation();
    }

    private void RestoreFullRoutePresentation()
    {
        if (fullRouteRenderer != null)
        {
            fullRouteRenderer.enabled = true;
            Material material = fullRouteRenderer.sharedMaterial;
            if (fullRouteHasCull && material != null && material.HasProperty("_Cull"))
            {
                material.SetFloat("_Cull", fullRouteOriginalCull);
            }
        }
        if (fullRouteObject != null) SetLayerRecursively(fullRouteObject.transform, fullRouteOriginalLayer);
        fullRouteObject = null;
        fullRouteRenderer = null;
        fullRouteHasCull = false;
    }

    private void DestroyExplorationVisuals()
    {
        if (explorationRouteRoot != null) Destroy(explorationRouteRoot);
        if (minimapRouteRoot != null) Destroy(minimapRouteRoot);
        if (stoneHaloRoot != null) Destroy(stoneHaloRoot);
        if (explorationRouteMaterial != null) Destroy(explorationRouteMaterial);
        if (minimapRouteMaterial != null) Destroy(minimapRouteMaterial);
        if (stoneHaloMaterial != null) Destroy(stoneHaloMaterial);
        explorationRouteRoot = null;
        minimapRouteRoot = null;
        stoneHaloRoot = null;
        explorationRouteLine = null;
        minimapRouteLine = null;
        stoneHaloLine = null;
        explorationRouteMaterial = null;
        minimapRouteMaterial = null;
        stoneHaloMaterial = null;
        explorationRouteActive = false;
    }

    private void DestroyTrainingVisuals()
    {
        if (trainingRouteRoot != null) Destroy(trainingRouteRoot);
        if (trainingMinimapRouteRoot != null) Destroy(trainingMinimapRouteRoot);
        if (trainingRouteMaterial != null) Destroy(trainingRouteMaterial);
        if (trainingMinimapRouteMaterial != null) Destroy(trainingMinimapRouteMaterial);
        trainingRouteRoot = null;
        trainingMinimapRouteRoot = null;
        trainingRouteLine = null;
        trainingMinimapRouteLine = null;
        trainingRouteMaterial = null;
        trainingMinimapRouteMaterial = null;
        trainingGuidanceActive = false;
    }

    public void TickArrow(float lookAheadMeters = 0.02f)
    {
        if (arrowRoot == null || !arrowRoot.activeSelf || probe == null || course == null || routeLocal.Length < 2) return;
        float distanceAlong = TrainingMetrics.ClosestDistanceAlongPolyline(probe.localPosition, routeLocal, out _);
        Vector3 targetLocal = course.SampleRouteLocal(Mathf.Min(
            course.RouteLengthMeters,
            distanceAlong + Mathf.Max(0.005f, lookAheadMeters)
        ));
        Vector3 targetWorld = course.ContentRoot.TransformPoint(targetLocal);
        Vector3 direction = targetWorld - probe.position;
        if (direction.sqrMagnitude < 0.0000001f) direction = probe.forward;
        Vector3 cameraDirection = viewingCamera.transform.InverseTransformDirection(direction.normalized);
        CurrentScreenDirection = ComputeScreenDirection(cameraDirection);
        float angle = -Mathf.Atan2(CurrentScreenDirection.x, CurrentScreenDirection.y) * Mathf.Rad2Deg;
        arrowRoot.transform.localPosition = new Vector3(0f, 0.04f, 0.12f);
        arrowRoot.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
        float pulse = 1f + Mathf.Sin(Time.unscaledTime * 5f) * 0.035f;
        arrowRoot.transform.localScale = Vector3.one * pulse;
    }

    public static Vector2 ComputeScreenDirection(Vector3 cameraDirection)
    {
        if (cameraDirection.sqrMagnitude < 0.000001f) return Vector2.right;
        cameraDirection.Normalize();
        const float horizontalDeadZone = 0.035f;
        const float maximumVerticalAngle = 52f;
        float horizontalSign = Mathf.Abs(cameraDirection.x) >= horizontalDeadZone
            ? Mathf.Sign(cameraDirection.x)
            : cameraDirection.z >= 0f ? 1f : -1f;
        float verticalAngle = Mathf.Atan2(cameraDirection.y * 0.7f, Mathf.Max(horizontalDeadZone, Mathf.Abs(cameraDirection.x))) * Mathf.Rad2Deg;
        verticalAngle = Mathf.Clamp(verticalAngle, -maximumVerticalAngle, maximumVerticalAngle) * Mathf.Deg2Rad;
        return new Vector2(horizontalSign * Mathf.Cos(verticalAngle), Mathf.Sin(verticalAngle)).normalized;
    }

    private void EnsureArrow()
    {
        if (viewingCamera == null) return;
        if (arrowRoot == null)
        {
            arrowRoot = new GameObject("Route Guidance Arrow");
            arrowRoot.layer = GuidanceLayer;

            Shader shader = Shader.Find("Murillo/Training Overlay Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            arrowOutlineMaterial = CreateOverlayMaterial(shader, new Color(0.96f, 1f, 1f, 0.98f), 4998);
            arrowShaftMaterial = CreateOverlayMaterial(shader, new Color(0.05f, 0.48f, 1f, 1f), 5000);
            arrowHeadMaterial = CreateOverlayMaterial(shader, new Color(0.20f, 0.82f, 1f, 1f), 5000);
            arrowSideMaterial = CreateOverlayMaterial(shader, new Color(0.015f, 0.10f, 0.42f, 1f), 4999);
            Mesh arrowMesh = BuildExtrudedArrowMesh();
            Quaternion modelTilt = Quaternion.Euler(13f, -18f, 0f);

            GameObject outline = new GameObject("Guidance Arrow Outline");
            outline.layer = GuidanceLayer;
            outline.transform.SetParent(arrowRoot.transform, false);
            outline.transform.localPosition = new Vector3(0f, 0f, 0.0004f);
            outline.transform.localRotation = modelTilt;
            outline.transform.localScale = new Vector3(1.22f, 1.22f, 1.12f);
            outline.AddComponent<MeshFilter>().sharedMesh = arrowMesh;
            MeshRenderer outlineRenderer = outline.AddComponent<MeshRenderer>();
            outlineRenderer.sharedMaterials = new[] { arrowOutlineMaterial, arrowOutlineMaterial, arrowOutlineMaterial };
            outlineRenderer.sortingOrder = 10;

            GameObject face = new GameObject("Guidance Arrow Face");
            face.layer = GuidanceLayer;
            face.transform.SetParent(arrowRoot.transform, false);
            face.transform.localRotation = modelTilt;
            face.AddComponent<MeshFilter>().sharedMesh = arrowMesh;
            MeshRenderer faceRenderer = face.AddComponent<MeshRenderer>();
            faceRenderer.sharedMaterials = new[] { arrowShaftMaterial, arrowHeadMaterial, arrowSideMaterial };
            faceRenderer.sortingOrder = 11;
            SetLayerRecursively(arrowRoot.transform, GuidanceLayer);
        }
        arrowRoot.transform.SetParent(viewingCamera.transform, false);
        arrowRoot.transform.localPosition = new Vector3(0f, 0.04f, 0.12f);
        arrowRoot.transform.localRotation = Quaternion.identity;
        arrowRoot.transform.localScale = Vector3.one;
    }

    private static Material CreateOverlayMaterial(Shader shader, Color color, int renderQueue)
    {
        Material material = new Material(shader) { color = color, renderQueue = renderQueue };
        return material;
    }

    private void BuildEnvironment()
    {
        if (environmentRoot != null) Destroy(environmentRoot);
        environmentRoot = new GameObject("Scientific Exploration Environment");
        environmentRoot.layer = GuidanceLayer;

        Bounds bounds = CalculateCaseBounds();
        float radius = Mathf.Max(0.35f, Mathf.Max(bounds.size.x, bounds.size.z) * 0.75f);
        float floorY = bounds.min.y - Mathf.Max(0.04f, bounds.size.y * 0.15f);

        Shader standard = Shader.Find("Universal Render Pipeline/Lit");
        if (standard == null) standard = Shader.Find("Standard");
        environmentMaterial = new Material(standard) { color = new Color(0.025f, 0.22f, 0.25f, 1f) };
        if (environmentMaterial.HasProperty("_EmissionColor"))
        {
            environmentMaterial.SetColor("_EmissionColor", new Color(0.01f, 0.12f, 0.14f));
            environmentMaterial.EnableKeyword("_EMISSION");
        }
        Shader sprite = Shader.Find("Sprites/Default");
        gridMaterial = new Material(sprite) { color = new Color(0.08f, 0.9f, 0.85f, 0.32f) };

        GameObject dome = new GameObject("Teal Gradient Dome");
        dome.layer = GuidanceLayer;
        dome.transform.SetParent(environmentRoot.transform, false);
        dome.transform.position = bounds.center;
        dome.AddComponent<MeshFilter>().sharedMesh = BuildGradientDomeMesh(Mathf.Max(2f, radius * 5f), 32, 14);
        dome.AddComponent<MeshRenderer>().sharedMaterial = new Material(sprite) { color = Color.white };

        GameObject platform = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        platform.name = "Scientific Display Platform";
        platform.transform.SetParent(environmentRoot.transform, false);
        platform.transform.position = new Vector3(bounds.center.x, floorY, bounds.center.z);
        platform.transform.localScale = new Vector3(radius, 0.008f, radius);
        RemoveCollider(platform);
        platform.GetComponent<Renderer>().sharedMaterial = environmentMaterial;

        BuildGrid(bounds.center, floorY + 0.009f, radius);
        BuildRing(bounds.center, floorY + 0.018f, radius * 0.9f);
        BuildRing(bounds.center, bounds.center.y, radius * 1.1f);
        SetLayerRecursively(environmentRoot.transform, GuidanceLayer);
    }

    private Bounds CalculateCaseBounds()
    {
        Renderer[] renderers = course?.ContentRoot?.GetComponentsInChildren<Renderer>(true) ?? Array.Empty<Renderer>();
        if (renderers.Length == 0) return new Bounds(Vector3.zero, Vector3.one * 0.5f);
        Bounds bounds = renderers[0].bounds;
        for (int index = 1; index < renderers.Length; index++) bounds.Encapsulate(renderers[index].bounds);
        return bounds;
    }

    private void BuildGrid(Vector3 center, float y, float radius)
    {
        const int divisions = 12;
        for (int index = -divisions; index <= divisions; index++)
        {
            float offset = radius * index / divisions;
            BuildLine(
                $"Grid X {index}",
                new Vector3(center.x - radius, y, center.z + offset),
                new Vector3(center.x + radius, y, center.z + offset)
            );
            BuildLine(
                $"Grid Z {index}",
                new Vector3(center.x + offset, y, center.z - radius),
                new Vector3(center.x + offset, y, center.z + radius)
            );
        }
    }

    private void BuildLine(string lineName, Vector3 start, Vector3 end)
    {
        GameObject lineObject = new GameObject(lineName);
        lineObject.layer = GuidanceLayer;
        lineObject.transform.SetParent(environmentRoot.transform, false);
        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.SetPosition(0, start);
        line.SetPosition(1, end);
        line.startWidth = 0.0012f;
        line.endWidth = 0.0012f;
        line.sharedMaterial = gridMaterial;
        line.startColor = gridMaterial.color;
        line.endColor = gridMaterial.color;
    }

    private void BuildRing(Vector3 center, float y, float radius)
    {
        GameObject ringObject = new GameObject("Scientific Halo Ring");
        ringObject.layer = GuidanceLayer;
        ringObject.transform.SetParent(environmentRoot.transform, false);
        LineRenderer ring = ringObject.AddComponent<LineRenderer>();
        ring.useWorldSpace = true;
        ring.loop = true;
        ring.positionCount = 64;
        ring.startWidth = 0.0025f;
        ring.endWidth = 0.0025f;
        ring.sharedMaterial = gridMaterial;
        for (int index = 0; index < ring.positionCount; index++)
        {
            float angle = Mathf.PI * 2f * index / ring.positionCount;
            ring.SetPosition(index, new Vector3(center.x + Mathf.Cos(angle) * radius, y, center.z + Mathf.Sin(angle) * radius));
        }
    }

    private static Mesh BuildExtrudedArrowMesh()
    {
        Vector2[] profile =
        {
            new Vector2(-0.0028f, -0.009f),
            new Vector2(0.0028f, -0.009f),
            new Vector2(0.0028f, 0.002f),
            new Vector2(0.008f, 0.002f),
            new Vector2(0f, 0.014f),
            new Vector2(-0.008f, 0.002f),
            new Vector2(-0.0028f, 0.002f)
        };
        Vector3[] vertices = new Vector3[profile.Length * 2];
        const float halfDepth = 0.002f;
        for (int index = 0; index < profile.Length; index++)
        {
            vertices[index] = new Vector3(profile[index].x, profile[index].y, -halfDepth);
            vertices[index + profile.Length] = new Vector3(profile[index].x, profile[index].y, halfDepth);
        }
        int[] sides = new int[profile.Length * 6];
        int sideTriangle = 0;
        for (int index = 0; index < profile.Length; index++)
        {
            int next = (index + 1) % profile.Length;
            sides[sideTriangle++] = index;
            sides[sideTriangle++] = next;
            sides[sideTriangle++] = next + profile.Length;
            sides[sideTriangle++] = index;
            sides[sideTriangle++] = next + profile.Length;
            sides[sideTriangle++] = index + profile.Length;
        }
        Mesh mesh = new Mesh { name = "Extruded Route Guidance Arrow", subMeshCount = 3 };
        mesh.vertices = vertices;
        mesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 6, 7, 13, 9, 7, 9, 8 }, 0);
        mesh.SetTriangles(new[] { 6, 2, 3, 6, 3, 5, 5, 3, 4, 13, 10, 9, 13, 12, 10, 12, 11, 10 }, 1);
        mesh.SetTriangles(sides, 2);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Mesh BuildGradientDomeMesh(float radius, int segments, int rings)
    {
        Vector3[] vertices = new Vector3[(rings + 1) * (segments + 1)];
        Color[] colors = new Color[vertices.Length];
        int[] triangles = new int[rings * segments * 6];
        for (int ring = 0; ring <= rings; ring++)
        {
            float v = ring / (float)rings;
            float latitude = Mathf.Lerp(-Mathf.PI * 0.5f, Mathf.PI * 0.5f, v);
            float horizontal = Mathf.Cos(latitude) * radius;
            float y = Mathf.Sin(latitude) * radius;
            Color color = Color.Lerp(new Color(0.005f, 0.035f, 0.055f), new Color(0.03f, 0.28f, 0.32f), v);
            for (int segment = 0; segment <= segments; segment++)
            {
                float angle = Mathf.PI * 2f * segment / segments;
                int vertex = ring * (segments + 1) + segment;
                vertices[vertex] = new Vector3(Mathf.Cos(angle) * horizontal, y, Mathf.Sin(angle) * horizontal);
                colors[vertex] = color;
            }
        }
        int triangle = 0;
        for (int ring = 0; ring < rings; ring++)
        {
            for (int segment = 0; segment < segments; segment++)
            {
                int current = ring * (segments + 1) + segment;
                int next = current + segments + 1;
                triangles[triangle++] = current;
                triangles[triangle++] = current + 1;
                triangles[triangle++] = next;
                triangles[triangle++] = current + 1;
                triangles[triangle++] = next + 1;
                triangles[triangle++] = next;
            }
        }
        Mesh mesh = new Mesh { name = "Scientific Gradient Dome" };
        mesh.vertices = vertices;
        mesh.colors = colors;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void RemoveCollider(GameObject value)
    {
        Collider collider = value.GetComponent<Collider>();
        if (collider != null) Destroy(collider);
    }

    private static void SetLayerRecursively(Transform root, int layer)
    {
        root.gameObject.layer = layer;
        foreach (Transform child in root) SetLayerRecursively(child, layer);
    }

    private void OnDestroy()
    {
        RestoreFullRoutePresentation();
        DestroyExplorationVisuals();
        DestroyTrainingVisuals();
    }
}
