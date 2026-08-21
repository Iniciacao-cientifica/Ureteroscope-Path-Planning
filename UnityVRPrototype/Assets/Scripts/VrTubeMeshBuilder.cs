using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public static class VrTubeMeshBuilder
{
    public static Mesh Build(IReadOnlyList<Vector3> sourcePositions, float radius, int requestedSides, string meshName)
    {
        if (sourcePositions == null) throw new ArgumentNullException(nameof(sourcePositions));
        List<Vector3> positions = RemoveConsecutiveDuplicates(sourcePositions);
        if (positions.Count < 2) throw new ArgumentException("A tube needs at least two distinct positions.", nameof(sourcePositions));

        int sides = Mathf.Max(6, requestedSides);
        float safeRadius = Mathf.Max(0.00005f, radius);
        Vector3[] tangents = BuildTangents(positions);
        Vector3[] radialFrames = BuildParallelTransportFrames(tangents);
        Vector3[] vertices = new Vector3[positions.Count * sides + 2];

        for (int index = 0; index < positions.Count; index++)
        {
            Vector3 radial = radialFrames[index];
            Vector3 binormal = Vector3.Cross(radial, tangents[index]).normalized;
            for (int sideIndex = 0; sideIndex < sides; sideIndex++)
            {
                float angle = 2f * Mathf.PI * sideIndex / sides;
                vertices[index * sides + sideIndex] = positions[index] +
                    (Mathf.Cos(angle) * radial + Mathf.Sin(angle) * binormal) * safeRadius;
            }
        }

        int startCenter = positions.Count * sides;
        int endCenter = startCenter + 1;
        vertices[startCenter] = positions[0];
        vertices[endCenter] = positions[positions.Count - 1];
        int[] triangles = new int[((positions.Count - 1) * sides * 2 + sides * 2) * 3];
        int triangleIndex = 0;
        for (int index = 0; index < positions.Count - 1; index++)
        {
            for (int sideIndex = 0; sideIndex < sides; sideIndex++)
            {
                int nextSide = (sideIndex + 1) % sides;
                int current = index * sides + sideIndex;
                int currentNext = index * sides + nextSide;
                int following = (index + 1) * sides + sideIndex;
                int followingNext = (index + 1) * sides + nextSide;
                AddTriangle(triangles, ref triangleIndex, current, following, currentNext);
                AddTriangle(triangles, ref triangleIndex, currentNext, following, followingNext);
            }
        }

        int lastRing = (positions.Count - 1) * sides;
        for (int sideIndex = 0; sideIndex < sides; sideIndex++)
        {
            int nextSide = (sideIndex + 1) % sides;
            AddTriangle(triangles, ref triangleIndex, startCenter, sideIndex, nextSide);
            AddTriangle(triangles, ref triangleIndex, endCenter, lastRing + nextSide, lastRing + sideIndex);
        }

        Mesh mesh = new Mesh { name = meshName };
        if (vertices.Length > 65535) mesh.indexFormat = IndexFormat.UInt32;
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static List<Vector3> RemoveConsecutiveDuplicates(IReadOnlyList<Vector3> source)
    {
        List<Vector3> result = new List<Vector3>(source.Count);
        for (int index = 0; index < source.Count; index++)
        {
            if (result.Count == 0 || Vector3.SqrMagnitude(source[index] - result[result.Count - 1]) > 0.0000000001f)
            {
                result.Add(source[index]);
            }
        }
        return result;
    }

    private static Vector3[] BuildTangents(IReadOnlyList<Vector3> positions)
    {
        Vector3[] tangents = new Vector3[positions.Count];
        for (int index = 0; index < positions.Count; index++)
        {
            Vector3 tangent = index == 0
                ? positions[1] - positions[0]
                : index == positions.Count - 1
                    ? positions[index] - positions[index - 1]
                    : positions[index + 1] - positions[index - 1];
            if (tangent.sqrMagnitude < 0.0000000001f)
            {
                tangent = index > 0 ? positions[index] - positions[index - 1] : positions[index + 1] - positions[index];
            }
            tangents[index] = tangent.normalized;
        }
        return tangents;
    }

    private static Vector3[] BuildParallelTransportFrames(IReadOnlyList<Vector3> tangents)
    {
        Vector3[] frames = new Vector3[tangents.Count];
        frames[0] = Vector3.ProjectOnPlane(Vector3.up, tangents[0]);
        if (frames[0].sqrMagnitude < 0.000001f) frames[0] = Vector3.ProjectOnPlane(Vector3.right, tangents[0]);
        frames[0].Normalize();

        for (int index = 1; index < tangents.Count; index++)
        {
            Quaternion transport = Quaternion.FromToRotation(tangents[index - 1], tangents[index]);
            Vector3 frame = Vector3.ProjectOnPlane(transport * frames[index - 1], tangents[index]);
            if (frame.sqrMagnitude < 0.000001f) frame = Vector3.ProjectOnPlane(frames[index - 1], tangents[index]);
            if (frame.sqrMagnitude < 0.000001f) frame = Vector3.ProjectOnPlane(Vector3.right, tangents[index]);
            frames[index] = frame.normalized;
        }
        return frames;
    }

    private static void AddTriangle(int[] triangles, ref int index, int first, int second, int third)
    {
        triangles[index++] = first;
        triangles[index++] = second;
        triangles[index++] = third;
    }
}
