using System;
using System.IO;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

public class TrainingEditModeTests
{
    [Test]
    public void ProtocolParsesVersionedQuaternionAndButtons()
    {
        const string json = "{\"v\":1,\"seq\":7,\"ms\":90,\"q\":[1,0,0,0],\"ticks\":25,\"buttons\":3,\"imu_ok\":true,\"fw\":\"test\"}";
        Assert.That(TrainingControllerProtocol.TryParse(json, out TrainingInputFrame frame), Is.True);
        Assert.That(frame.sequence, Is.EqualTo(7));
        Assert.That(frame.encoderTicks, Is.EqualTo(25));
        Assert.That(frame.actionPressed, Is.True);
        Assert.That(frame.calibratePressed, Is.True);
        Assert.That(frame.orientation, Is.EqualTo(Quaternion.identity));
    }

    [TestCase("")]
    [TestCase("{\"v\":2,\"q\":[1,0,0,0]}")]
    [TestCase("{\"v\":1,\"q\":[0,0,0,0]}")]
    public void ProtocolRejectsMalformedOrUnsupportedPackets(string value)
    {
        Assert.That(TrainingControllerProtocol.TryParse(value, out _), Is.False);
    }

    [Test]
    public void EncoderAndRecenteringUsePhysicalUnits()
    {
        Assert.That(TrainingInputMath.TicksToMeters(100, 0.785f), Is.EqualTo(0.0785f).Within(0.000001f));
        Quaternion neutral = Quaternion.Euler(0f, 30f, 0f);
        Quaternion current = Quaternion.Euler(0f, 50f, 0f);
        Assert.That(Quaternion.Angle(TrainingInputMath.RelativeOrientation(neutral, current), Quaternion.Euler(0f, 20f, 0f)), Is.LessThan(0.01f));
    }

    [Test]
    public void MouseRotationUsesFrameDeltaWhileKeyboardUsesElapsedTime()
    {
        Assert.That(TrainingInputMath.MouseRotationDegrees(3f, 2f), Is.EqualTo(6f).Within(0.00001f));
        Assert.That(TrainingInputMath.MouseRotationDegrees(3f, 99f), Is.EqualTo(12f).Within(0.00001f));
        Assert.That(TrainingInputMath.KeyboardRotationDegrees(1f, 70f, 0.5f), Is.EqualTo(35f).Within(0.00001f));
    }

    [Test]
    public void RouteProjectionAndSafetyCorridorUsePhysicalMeters()
    {
        Vector3[] route = { Vector3.zero, Vector3.forward };
        float distanceAlong = TrainingMetrics.ClosestDistanceAlongPolyline(
            new Vector3(0.01f, 0f, 0.4f),
            route,
            out float deviation
        );

        Assert.That(distanceAlong, Is.EqualTo(0.4f).Within(0.00001f));
        Assert.That(deviation, Is.EqualTo(0.01f).Within(0.00001f));
        Assert.That(TrainingMetrics.IsWithinRouteCorridor(new Vector3(0.01f, 0f, 0.4f), route, 0.015f), Is.True);
        Assert.That(TrainingMetrics.IsWithinRouteCorridor(new Vector3(0.01f, 0f, 0.4f), route, 0.005f), Is.False);
    }

    [Test]
    public void DistanceAndScoreAreDeterministic()
    {
        Vector3[] route = { Vector3.zero, Vector3.forward };
        Assert.That(TrainingMetrics.DistanceToPolyline(new Vector3(0.01f, 0f, 0.5f), route), Is.EqualTo(0.01f).Within(0.00001f));
        TrainingSessionResult result = new TrainingSessionResult
        {
            completed = true,
            elapsedSeconds = 30f,
            collisionEvents = 0,
            wallContactSeconds = 0f,
            rmsDeviationMillimeters = 0f,
            traveledMillimeters = 100f,
            plannedMillimeters = 100f
        };
        Assert.That(TrainingMetrics.CalculateScore(result), Is.EqualTo(100f).Within(0.001f));
        result.completed = false;
        Assert.That(TrainingMetrics.CalculateScore(result), Is.Zero);
    }

    [Test]
    public void CsvWritesHeaderAndAnonymousResult()
    {
        string directory = Path.Combine(Path.GetTempPath(), "murillo-training-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            TrainingSessionResult result = new TrainingSessionResult
            {
                participantCode = TrainingCsvLogger.SanitizeParticipantCode("aluno 01!"),
                timestampUtc = "2026-08-13T12:00:00Z",
                caseId = "case_1",
                routeId = "route_1",
                difficulty = "Tutorial",
                completed = false,
                inputSource = "Keyboard",
                firmwareVersion = "test"
            };
            string path = TrainingCsvLogger.Append(result, directory);
            string[] lines = File.ReadAllLines(path);
            Assert.That(lines.Length, Is.EqualTo(2));
            Assert.That(lines[0], Is.EqualTo(TrainingCsvLogger.Header));
            Assert.That(lines[1], Does.StartWith("ALUNO01,"));
            Assert.That(lines[1], Does.Contain(",DNF,"));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [Test]
    public void DesktopSceneContainsLoaderControllerAndTwoCameras()
    {
        EditorSceneManager.OpenScene(DesktopTrainingSceneSetup.ScenePath);
        Assert.That(GameObject.Find("Training Case Loader")?.GetComponent<VrCaseLoader>(), Is.Not.Null);
        Assert.That(GameObject.Find("Desktop Training Controller")?.GetComponent<UreteroscopyTrainingController>(), Is.Not.Null);
        Assert.That(UnityEngine.Object.FindObjectsByType<Camera>().Length, Is.GreaterThanOrEqualTo(2));
        Assert.That(UreteroscopyTrainingController.ActiveHudWidth, Is.LessThanOrEqualTo(350f));
        Assert.That(UreteroscopyTrainingController.ActiveMinimapMaximumSize, Is.LessThanOrEqualTo(220f));
    }
}
