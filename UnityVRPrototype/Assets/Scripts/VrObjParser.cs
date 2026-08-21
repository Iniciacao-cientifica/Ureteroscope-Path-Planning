using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

public static class VrObjParser
{
    public static Mesh Parse(string objText, string meshName, int minimumConnectedComponentFaces = 0)
    {
        if (string.IsNullOrWhiteSpace(objText))
        {
            throw new ArgumentException("OBJ text is empty.", nameof(objText));
        }

        List<Vector3> vertices = new List<Vector3>(65536);
        List<int> triangles = new List<int>(131072);
        using StringReader reader = new StringReader(objText);
        string line;
        while ((line = reader.ReadLine()) != null)
        {
            if (line.StartsWith("v ", StringComparison.Ordinal))
            {
                string[] parts = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 4)
                {
                    vertices.Add(new Vector3(ParseFloat(parts[1]), ParseFloat(parts[2]), ParseFloat(parts[3])));
                }
            }
            else if (line.StartsWith("f ", StringComparison.Ordinal))
            {
                string[] parts = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 4)
                {
                    continue;
                }
                int first = ParseVertexIndex(parts[1], vertices.Count);
                for (int index = 2; index < parts.Length - 1; index++)
                {
                    triangles.Add(first);
                    triangles.Add(ParseVertexIndex(parts[index], vertices.Count));
                    triangles.Add(ParseVertexIndex(parts[index + 1], vertices.Count));
                }
            }
        }

        if (vertices.Count == 0 || triangles.Count == 0)
        {
            throw new FormatException("OBJ does not contain vertices and triangle faces.");
        }

        if (minimumConnectedComponentFaces > 1)
        {
            RemoveSmallConnectedComponents(vertices, triangles, minimumConnectedComponentFaces);
        }

        Mesh mesh = new Mesh { name = meshName };
        if (vertices.Count > 65535)
        {
            mesh.indexFormat = IndexFormat.UInt32;
        }
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0, true);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        mesh.UploadMeshData(false);
        return mesh;
    }

    private static void RemoveSmallConnectedComponents(List<Vector3> vertices, List<int> triangles, int minimumFaces)
    {
        int[] parent = new int[vertices.Count];
        for (int index = 0; index < parent.Length; index++) parent[index] = index;

        for (int index = 0; index < triangles.Count; index += 3)
        {
            Union(parent, triangles[index], triangles[index + 1]);
            Union(parent, triangles[index], triangles[index + 2]);
        }

        Dictionary<int, int> faceCounts = new Dictionary<int, int>();
        for (int index = 0; index < triangles.Count; index += 3)
        {
            int root = Find(parent, triangles[index]);
            faceCounts[root] = faceCounts.TryGetValue(root, out int count) ? count + 1 : 1;
        }

        List<int> keptTriangles = new List<int>(triangles.Count);
        Dictionary<int, int> remap = new Dictionary<int, int>();
        List<Vector3> keptVertices = new List<Vector3>(vertices.Count);
        for (int index = 0; index < triangles.Count; index += 3)
        {
            int root = Find(parent, triangles[index]);
            if (faceCounts[root] < minimumFaces) continue;
            for (int corner = 0; corner < 3; corner++)
            {
                int oldIndex = triangles[index + corner];
                if (!remap.TryGetValue(oldIndex, out int newIndex))
                {
                    newIndex = keptVertices.Count;
                    remap.Add(oldIndex, newIndex);
                    keptVertices.Add(vertices[oldIndex]);
                }
                keptTriangles.Add(newIndex);
            }
        }

        if (keptTriangles.Count == 0)
        {
            throw new FormatException($"OBJ has no connected component with at least {minimumFaces} faces.");
        }
        vertices.Clear();
        vertices.AddRange(keptVertices);
        triangles.Clear();
        triangles.AddRange(keptTriangles);
    }

    private static int Find(int[] parent, int value)
    {
        while (parent[value] != value)
        {
            parent[value] = parent[parent[value]];
            value = parent[value];
        }
        return value;
    }

    private static void Union(int[] parent, int left, int right)
    {
        int leftRoot = Find(parent, left);
        int rightRoot = Find(parent, right);
        if (leftRoot != rightRoot) parent[rightRoot] = leftRoot;
    }

    private static float ParseFloat(string value)
    {
        return float.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
    }

    private static int ParseVertexIndex(string token, int vertexCount)
    {
        string value = token.Split('/')[0];
        int parsed = int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);
        int index = parsed > 0 ? parsed - 1 : vertexCount + parsed;
        if (index < 0 || index >= vertexCount)
        {
            throw new FormatException($"OBJ vertex index {parsed} is outside 1..{vertexCount}.");
        }
        return index;
    }
}
