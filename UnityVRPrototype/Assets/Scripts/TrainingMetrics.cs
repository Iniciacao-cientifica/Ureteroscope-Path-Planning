using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

[Serializable]
public class TrainingSessionResult
{
    public string participantCode;
    public string timestampUtc;
    public string caseId;
    public string routeId;
    public string difficulty;
    public bool completed;
    public float elapsedSeconds;
    public int collisionEvents;
    public float wallContactSeconds;
    public float rmsDeviationMillimeters;
    public float traveledMillimeters;
    public float plannedMillimeters;
    public float score;
    public string inputSource;
    public string firmwareVersion;
}

public static class TrainingMetrics
{
    public static float DistanceToPolyline(Vector3 point, Vector3[] route)
    {
        ClosestDistanceAlongPolyline(point, route, out float deviation);
        return deviation;
    }

    public static float ClosestDistanceAlongPolyline(Vector3 point, Vector3[] route, out float deviation)
    {
        deviation = float.PositiveInfinity;
        if (route == null || route.Length == 0) return float.PositiveInfinity;
        if (route.Length == 1)
        {
            deviation = Vector3.Distance(point, route[0]);
            return 0f;
        }
        float closestSquared = float.PositiveInfinity;
        float closestDistanceAlong = 0f;
        float accumulated = 0f;
        for (int index = 1; index < route.Length; index++)
        {
            Vector3 start = route[index - 1];
            Vector3 segment = route[index] - start;
            float denominator = segment.sqrMagnitude;
            float amount = denominator > 0.0000001f
                ? Mathf.Clamp01(Vector3.Dot(point - start, segment) / denominator)
                : 0f;
            float squared = (point - (start + segment * amount)).sqrMagnitude;
            float segmentLength = Mathf.Sqrt(denominator);
            if (squared < closestSquared)
            {
                closestSquared = squared;
                closestDistanceAlong = accumulated + segmentLength * amount;
            }
            accumulated += segmentLength;
        }
        deviation = Mathf.Sqrt(closestSquared);
        return closestDistanceAlong;
    }

    public static bool IsWithinRouteCorridor(Vector3 point, Vector3[] route, float radiusMeters)
    {
        return DistanceToPolyline(point, route) <= Mathf.Max(0f, radiusMeters);
    }

    public static float CalculateScore(TrainingSessionResult result)
    {
        if (result == null || !result.completed) return 0f;
        float safety = 40f * Mathf.Clamp01(1f - (2f * result.collisionEvents + result.wallContactSeconds) / 20f);
        float accuracy = 30f * Mathf.Clamp01(1f - result.rmsDeviationMillimeters / 15f);
        float efficiencyRatio = result.traveledMillimeters > 0.001f
            ? result.plannedMillimeters / Mathf.Max(result.plannedMillimeters, result.traveledMillimeters)
            : 0f;
        float efficiency = 20f * Mathf.Clamp01(efficiencyRatio);
        float parSeconds = Mathf.Max(30f, result.plannedMillimeters / 3f);
        float time = 10f * Mathf.Clamp01(parSeconds / Mathf.Max(0.001f, result.elapsedSeconds));
        return Mathf.Clamp(safety + accuracy + efficiency + time, 0f, 100f);
    }
}

public static class TrainingCsvLogger
{
    public const string Header = "participant_code,timestamp_utc,case_id,route_id,difficulty,completed,elapsed_seconds,collision_events,wall_contact_seconds,rms_deviation_mm,traveled_mm,planned_mm,score,input_source,firmware_version";

    public static string Append(TrainingSessionResult result, string directoryOverride = null)
    {
        if (result == null) throw new ArgumentNullException(nameof(result));
        string directory = string.IsNullOrWhiteSpace(directoryOverride)
            ? Path.Combine(Application.persistentDataPath, "Sessions")
            : directoryOverride;
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "ureteroscopy_sessions.csv");
        bool writeHeader = !File.Exists(path) || new FileInfo(path).Length == 0;
        using StreamWriter writer = new StreamWriter(path, true, new UTF8Encoding(false));
        if (writeHeader) writer.WriteLine(Header);
        writer.WriteLine(ToCsv(result));
        return path;
    }

    public static string ToCsv(TrainingSessionResult value)
    {
        CultureInfo invariant = CultureInfo.InvariantCulture;
        string score = value.completed ? value.score.ToString("F2", invariant) : "DNF";
        return string.Join(",", new[]
        {
            Escape(value.participantCode), Escape(value.timestampUtc), Escape(value.caseId), Escape(value.routeId),
            Escape(value.difficulty), value.completed ? "true" : "false",
            value.elapsedSeconds.ToString("F3", invariant), value.collisionEvents.ToString(invariant),
            value.wallContactSeconds.ToString("F3", invariant), value.rmsDeviationMillimeters.ToString("F3", invariant),
            value.traveledMillimeters.ToString("F3", invariant), value.plannedMillimeters.ToString("F3", invariant),
            score, Escape(value.inputSource), Escape(value.firmwareVersion)
        });
    }

    public static string SanitizeParticipantCode(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "ANON";
        StringBuilder builder = new StringBuilder(16);
        foreach (char character in value.Trim().ToUpperInvariant())
        {
            if ((character >= 'A' && character <= 'Z') || (character >= '0' && character <= '9') || character == '-' || character == '_')
            {
                builder.Append(character);
                if (builder.Length == 16) break;
            }
        }
        return builder.Length > 0 ? builder.ToString() : "ANON";
    }

    private static string Escape(string value)
    {
        value ??= "";
        if (!value.Contains(",") && !value.Contains("\"") && !value.Contains("\n") && !value.Contains("\r")) return value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
