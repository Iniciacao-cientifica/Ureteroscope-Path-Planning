using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[Serializable]
public struct TrainingCourseKnot
{
    public Vector3 position;
    [Min(0.001f)] public float lumenRadiusMeters;

    public TrainingCourseKnot(Vector3 position, float lumenRadiusMeters)
    {
        this.position = position;
        this.lumenRadiusMeters = lumenRadiusMeters;
    }
}

public static class TrainingCoursePath
{
    public static Vector3[] ResampleCatmullRom(
        IReadOnlyList<TrainingCourseKnot> controlPoints,
        float requestedSpacingMeters,
        out float[] sampledRadii)
    {
        if (controlPoints == null) throw new ArgumentNullException(nameof(controlPoints));
        if (controlPoints.Count < 2) throw new ArgumentException("A training course needs at least two knots.", nameof(controlPoints));

        float spacing = Mathf.Max(0.0005f, requestedSpacingMeters);
        List<Vector3> positions = new List<Vector3> { controlPoints[0].position };
        List<float> radii = new List<float> { SafeRadius(controlPoints[0].lumenRadiusMeters) };

        for (int segment = 0; segment < controlPoints.Count - 1; segment++)
        {
            TrainingCourseKnot first = controlPoints[segment];
            TrainingCourseKnot second = controlPoints[segment + 1];
            Vector3 p0 = controlPoints[Mathf.Max(0, segment - 1)].position;
            Vector3 p1 = first.position;
            Vector3 p2 = second.position;
            Vector3 p3 = controlPoints[Mathf.Min(controlPoints.Count - 1, segment + 2)].position;
            int samples = Mathf.Max(2, Mathf.CeilToInt(Vector3.Distance(p1, p2) / spacing));
            for (int sample = 1; sample <= samples; sample++)
            {
                float t = sample / (float)samples;
                Vector3 position = CatmullRom(p0, p1, p2, p3, t);
                if ((position - positions[positions.Count - 1]).sqrMagnitude < 0.0000000001f) continue;
                positions.Add(position);
                radii.Add(Mathf.Lerp(SafeRadius(first.lumenRadiusMeters), SafeRadius(second.lumenRadiusMeters), t));
            }
        }

        sampledRadii = radii.ToArray();
        return positions.ToArray();
    }

    private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;
        return 0.5f * ((2f * p1) + (-p0 + p2) * t +
                       (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                       (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }

    private static float SafeRadius(float value) => Mathf.Max(0.001f, value);
}

public static class TrainingLumenMath
{
    public static bool TryProject(
        Vector3 point,
        IReadOnlyList<Vector3> centerline,
        IReadOnlyList<float> radii,
        out float distanceAlong,
        out float deviation,
        out float lumenRadius)
    {
        distanceAlong = 0f;
        deviation = float.PositiveInfinity;
        lumenRadius = 0f;
        if (centerline == null || radii == null || centerline.Count == 0 || radii.Count != centerline.Count) return false;
        if (centerline.Count == 1)
        {
            deviation = Vector3.Distance(point, centerline[0]);
            lumenRadius = radii[0];
            return true;
        }

        float closestSquared = float.PositiveInfinity;
        float accumulated = 0f;
        for (int index = 1; index < centerline.Count; index++)
        {
            Vector3 start = centerline[index - 1];
            Vector3 segment = centerline[index] - start;
            float denominator = segment.sqrMagnitude;
            float amount = denominator > 0.0000000001f
                ? Mathf.Clamp01(Vector3.Dot(point - start, segment) / denominator)
                : 0f;
            Vector3 projected = start + segment * amount;
            float squared = (point - projected).sqrMagnitude;
            float segmentLength = Mathf.Sqrt(denominator);
            if (squared < closestSquared)
            {
                closestSquared = squared;
                distanceAlong = accumulated + segmentLength * amount;
                lumenRadius = Mathf.Lerp(radii[index - 1], radii[index], amount);
            }
            accumulated += segmentLength;
        }
        deviation = Mathf.Sqrt(closestSquared);
        return true;
    }

    public static bool IsInside(
        Vector3 point,
        IReadOnlyList<Vector3> centerline,
        IReadOnlyList<float> radii,
        float probeRadiusMeters)
    {
        return TryProject(point, centerline, radii, out _, out float deviation, out float lumenRadius) &&
               deviation <= Mathf.Max(0f, lumenRadius - Mathf.Max(0f, probeRadiusMeters)) + 0.000001f;
    }

    public static float FindAllowedDistance(
        Vector3 origin,
        Vector3 direction,
        float requestedDistance,
        IReadOnlyList<Vector3> centerline,
        IReadOnlyList<float> radii,
        float probeRadiusMeters,
        float maximumStepMeters = 0.001f)
    {
        if (requestedDistance <= 0f || direction.sqrMagnitude < 0.0000000001f) return 0f;
        if (!IsInside(origin, centerline, radii, probeRadiusMeters)) return 0f;

        Vector3 normalizedDirection = direction.normalized;
        int steps = Mathf.Max(1, Mathf.CeilToInt(requestedDistance / Mathf.Max(0.00025f, maximumStepMeters)));
        float previous = 0f;
        for (int step = 1; step <= steps; step++)
        {
            float distance = requestedDistance * step / steps;
            if (IsInside(origin + normalizedDirection * distance, centerline, radii, probeRadiusMeters))
            {
                previous = distance;
                continue;
            }

            float low = previous;
            float high = distance;
            for (int iteration = 0; iteration < 10; iteration++)
            {
                float middle = (low + high) * 0.5f;
                if (IsInside(origin + normalizedDirection * middle, centerline, radii, probeRadiusMeters)) low = middle;
                else high = middle;
            }
            return low;
        }
        return requestedDistance;
    }
}

public static class TrainingLumenMeshBuilder
{
    public static Mesh Build(
        IReadOnlyList<Vector3> positions,
        IReadOnlyList<float> radii,
        int requestedSides,
        string meshName)
    {
        if (positions == null) throw new ArgumentNullException(nameof(positions));
        if (radii == null) throw new ArgumentNullException(nameof(radii));
        if (positions.Count < 2 || radii.Count != positions.Count)
            throw new ArgumentException("The lumen needs matching position and radius samples.");

        int sides = Mathf.Max(8, requestedSides);
        Vector3[] tangents = BuildTangents(positions);
        Vector3[] frames = BuildParallelTransportFrames(tangents);
        Vector3[] vertices = new Vector3[positions.Count * sides + 2];
        Vector2[] uvs = new Vector2[vertices.Length];
        float totalLength = 0f;
        float[] distances = new float[positions.Count];
        for (int index = 1; index < positions.Count; index++)
        {
            totalLength += Vector3.Distance(positions[index - 1], positions[index]);
            distances[index] = totalLength;
        }

        for (int index = 0; index < positions.Count; index++)
        {
            Vector3 radial = frames[index];
            Vector3 binormal = Vector3.Cross(radial, tangents[index]).normalized;
            float radius = Mathf.Max(0.001f, radii[index]);
            for (int side = 0; side < sides; side++)
            {
                float amount = side / (float)sides;
                float angle = Mathf.PI * 2f * amount;
                int vertex = index * sides + side;
                vertices[vertex] = positions[index] +
                    (Mathf.Cos(angle) * radial + Mathf.Sin(angle) * binormal) * radius;
                uvs[vertex] = new Vector2(amount, totalLength > 0f ? distances[index] / totalLength : 0f);
            }
        }

        int startCenter = positions.Count * sides;
        int endCenter = startCenter + 1;
        vertices[startCenter] = positions[0];
        vertices[endCenter] = positions[positions.Count - 1];
        uvs[startCenter] = new Vector2(0.5f, 0f);
        uvs[endCenter] = new Vector2(0.5f, 1f);

        int[] triangles = new int[((positions.Count - 1) * sides * 2 + sides * 2) * 3];
        int triangle = 0;
        for (int index = 0; index < positions.Count - 1; index++)
        {
            for (int side = 0; side < sides; side++)
            {
                int next = (side + 1) % sides;
                int current = index * sides + side;
                int currentNext = index * sides + next;
                int following = (index + 1) * sides + side;
                int followingNext = (index + 1) * sides + next;
                AddTriangle(triangles, ref triangle, current, following, currentNext);
                AddTriangle(triangles, ref triangle, currentNext, following, followingNext);
            }
        }
        int lastRing = (positions.Count - 1) * sides;
        for (int side = 0; side < sides; side++)
        {
            int next = (side + 1) % sides;
            AddTriangle(triangles, ref triangle, startCenter, side, next);
            AddTriangle(triangles, ref triangle, endCenter, lastRing + next, lastRing + side);
        }

        Mesh mesh = new Mesh { name = meshName };
        if (vertices.Length > 65535) mesh.indexFormat = IndexFormat.UInt32;
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Vector3[] BuildTangents(IReadOnlyList<Vector3> positions)
    {
        Vector3[] result = new Vector3[positions.Count];
        for (int index = 0; index < positions.Count; index++)
        {
            Vector3 tangent = index == 0
                ? positions[1] - positions[0]
                : index == positions.Count - 1
                    ? positions[index] - positions[index - 1]
                    : positions[index + 1] - positions[index - 1];
            result[index] = tangent.sqrMagnitude > 0.0000000001f ? tangent.normalized : Vector3.forward;
        }
        return result;
    }

    private static Vector3[] BuildParallelTransportFrames(IReadOnlyList<Vector3> tangents)
    {
        Vector3[] result = new Vector3[tangents.Count];
        result[0] = Vector3.ProjectOnPlane(Vector3.up, tangents[0]);
        if (result[0].sqrMagnitude < 0.000001f) result[0] = Vector3.ProjectOnPlane(Vector3.right, tangents[0]);
        result[0].Normalize();
        for (int index = 1; index < tangents.Count; index++)
        {
            Vector3 frame = Quaternion.FromToRotation(tangents[index - 1], tangents[index]) * result[index - 1];
            frame = Vector3.ProjectOnPlane(frame, tangents[index]);
            if (frame.sqrMagnitude < 0.000001f) frame = Vector3.ProjectOnPlane(Vector3.right, tangents[index]);
            result[index] = frame.normalized;
        }
        return result;
    }

    private static void AddTriangle(int[] triangles, ref int index, int first, int second, int third)
    {
        triangles[index++] = first;
        triangles[index++] = second;
        triangles[index++] = third;
    }
}
