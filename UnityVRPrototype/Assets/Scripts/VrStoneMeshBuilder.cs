using System.Collections.Generic;
using UnityEngine;

public static class VrStoneMeshBuilder
{
    public static Mesh Build(float diameterMeters, string stableId)
    {
        const int segments = 18;
        const int rings = 11;
        float radius = Mathf.Max(0.001f, diameterMeters * 0.5f);
        uint seed = StableHash(stableId ?? "stone");
        float phaseA = (seed & 1023u) * 0.006135923f;
        float phaseB = ((seed >> 10) & 1023u) * 0.006135923f;

        List<Vector3> vertices = new List<Vector3>((rings + 1) * (segments + 1));
        List<Vector2> uv = new List<Vector2>(vertices.Capacity);
        List<int> triangles = new List<int>(rings * segments * 6);
        for (int ring = 0; ring <= rings; ring++)
        {
            float v = ring / (float)rings;
            float latitude = v * Mathf.PI;
            for (int segment = 0; segment <= segments; segment++)
            {
                float u = segment / (float)segments;
                float longitude = u * Mathf.PI * 2f;
                Vector3 direction = new Vector3(
                    Mathf.Sin(latitude) * Mathf.Cos(longitude),
                    Mathf.Cos(latitude),
                    Mathf.Sin(latitude) * Mathf.Sin(longitude)
                );
                float coarse = Mathf.Sin(direction.x * 5.7f + phaseA) *
                               Mathf.Sin(direction.y * 6.3f - phaseB) *
                               Mathf.Sin(direction.z * 5.1f + phaseB);
                float fine = Mathf.Sin((direction.x + direction.z) * 13f + phaseA) * 0.035f;
                float deformation = 1f + coarse * 0.16f + fine;
                Vector3 ellipsoid = Vector3.Scale(direction, new Vector3(1f, 0.82f, 1.14f));
                vertices.Add(ellipsoid * (radius * deformation));
                uv.Add(new Vector2(u, v));
            }
        }
        int stride = segments + 1;
        for (int ring = 0; ring < rings; ring++)
        {
            for (int segment = 0; segment < segments; segment++)
            {
                int current = ring * stride + segment;
                int next = current + stride;
                triangles.Add(current);
                triangles.Add(next);
                triangles.Add(current + 1);
                triangles.Add(current + 1);
                triangles.Add(next);
                triangles.Add(next + 1);
            }
        }

        Mesh mesh = new Mesh { name = "Procedural Kidney Stone Mesh" };
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uv);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static uint StableHash(string value)
    {
        unchecked
        {
            uint hash = 2166136261u;
            foreach (char character in value)
            {
                hash ^= character;
                hash *= 16777619u;
            }
            return hash;
        }
    }
}
