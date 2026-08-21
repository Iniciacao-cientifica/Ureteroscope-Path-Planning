using System;
using UnityEngine;

public sealed class TrainingNavigationVisuals : MonoBehaviour
{
    public const int GuidanceLayer = 28;

    private Camera viewingCamera;
    private VrCaseLoader caseLoader;
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

    public Vector2 CurrentScreenDirection { get; private set; } = Vector2.up;

    public void Configure(Camera camera, VrCaseLoader loader, Transform probeTransform, Vector3[] route)
    {
        viewingCamera = camera;
        caseLoader = loader;
        probe = probeTransform;
        routeLocal = route ?? Array.Empty<Vector3>();
        EnsureArrow();
        BuildEnvironment();
        SetPresentation(false, false);
    }

    public void SetPresentation(bool showArrow, bool showEnvironment)
    {
        if (arrowRoot != null) arrowRoot.SetActive(showArrow);
        if (environmentRoot != null) environmentRoot.SetActive(showEnvironment);
    }

    public void TickArrow(float lookAheadMeters = 0.02f)
    {
        if (arrowRoot == null || !arrowRoot.activeSelf || probe == null || caseLoader == null || routeLocal.Length < 2) return;
        float distanceAlong = TrainingMetrics.ClosestDistanceAlongPolyline(probe.localPosition, routeLocal, out _);
        Vector3 targetLocal = caseLoader.SampleCurrentRouteLocal(Mathf.Min(
            caseLoader.CurrentRouteLengthMeters,
            distanceAlong + Mathf.Max(0.005f, lookAheadMeters)
        ));
        Vector3 targetWorld = caseLoader.ContentRoot.TransformPoint(targetLocal);
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
        Renderer[] renderers = caseLoader?.ContentRoot?.GetComponentsInChildren<Renderer>(true) ?? Array.Empty<Renderer>();
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
}
