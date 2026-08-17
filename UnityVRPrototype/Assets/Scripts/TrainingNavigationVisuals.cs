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
    private Material arrowMaterial;
    private Material environmentMaterial;
    private Material gridMaterial;

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
        arrowRoot.transform.localPosition = new Vector3(0f, 0.028f, 0.075f);
        arrowRoot.transform.rotation = Quaternion.LookRotation(direction.normalized, viewingCamera.transform.up);
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
            arrowMaterial = new Material(shader) { color = new Color(0.05f, 1f, 0.92f, 0.95f) };
            arrowMaterial.renderQueue = 5000;

            GameObject shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            shaft.name = "Guidance Arrow Shaft";
            shaft.transform.SetParent(arrowRoot.transform, false);
            shaft.transform.localPosition = new Vector3(0f, 0f, 0.012f);
            shaft.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            shaft.transform.localScale = new Vector3(0.0022f, 0.012f, 0.0022f);
            RemoveCollider(shaft);
            shaft.GetComponent<Renderer>().sharedMaterial = arrowMaterial;

            GameObject head = new GameObject("Guidance Arrow Head");
            head.layer = GuidanceLayer;
            head.transform.SetParent(arrowRoot.transform, false);
            head.transform.localPosition = new Vector3(0f, 0f, 0.024f);
            head.AddComponent<MeshFilter>().sharedMesh = BuildConeMesh(0.0065f, 0.014f, 12);
            head.AddComponent<MeshRenderer>().sharedMaterial = arrowMaterial;
            SetLayerRecursively(arrowRoot.transform, GuidanceLayer);
        }
        arrowRoot.transform.SetParent(viewingCamera.transform, false);
        arrowRoot.transform.localPosition = new Vector3(0f, 0.028f, 0.075f);
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

    private static Mesh BuildConeMesh(float radius, float length, int sides)
    {
        Vector3[] vertices = new Vector3[sides + 2];
        vertices[0] = Vector3.zero;
        vertices[1] = Vector3.forward * length;
        for (int index = 0; index < sides; index++)
        {
            float angle = Mathf.PI * 2f * index / sides;
            vertices[index + 2] = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
        }
        int[] triangles = new int[sides * 6];
        int triangle = 0;
        for (int index = 0; index < sides; index++)
        {
            int next = (index + 1) % sides;
            triangles[triangle++] = 1;
            triangles[triangle++] = index + 2;
            triangles[triangle++] = next + 2;
            triangles[triangle++] = 0;
            triangles[triangle++] = next + 2;
            triangles[triangle++] = index + 2;
        }
        Mesh mesh = new Mesh { name = "Guidance Arrow Cone" };
        mesh.vertices = vertices;
        mesh.triangles = triangles;
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
