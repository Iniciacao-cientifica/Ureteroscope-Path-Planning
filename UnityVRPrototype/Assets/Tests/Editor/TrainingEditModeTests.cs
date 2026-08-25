using System;
using System.IO;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

public class TrainingEditModeTests
{
    [Test]
    public void MpuTextProtocolCombinesAccelerationGyroscopeAndButton()
    {
        Mpu6050TextProtocol parser = new Mpu6050TextProtocol();
        Assert.That(parser.AcceptLine("   Aceleracao (m/s^2):  X=-1.25  Y=0.50  Z=9.70", out _), Is.False);
        Assert.That(parser.AcceptLine("   Giroscopio (rad/s):  X=0.10  Y=-0.20  Z=0.30", out _), Is.False);
        Assert.That(parser.AcceptLine("   Agarrando: SIM", out Mpu6050TextSample sample), Is.True);
        Assert.That(sample.acceleration, Is.EqualTo(new Vector3(-1.25f, 0.5f, 9.7f)));
        Assert.That(sample.angularVelocity, Is.EqualTo(new Vector3(0.1f, -0.2f, 0.3f)));
        Assert.That(sample.actionPressed, Is.True);
    }

    [Test]
    public void MpuTextProtocolRejectsIncompleteSamplesAndParsesReleasedButton()
    {
        Mpu6050TextProtocol parser = new Mpu6050TextProtocol();
        Assert.That(parser.AcceptLine("Agarrando: SIM", out _), Is.False);
        Assert.That(parser.AcceptLine("Aceleracao (m/s^2): X=0 Y=0 Z=9.81", out _), Is.False);
        Assert.That(parser.AcceptLine("Agarrando: nao", out _), Is.False);
        Assert.That(parser.AcceptLine("Giroscopio (rad/s): X=0 Y=0 Z=0", out _), Is.False);
        Assert.That(parser.AcceptLine("Agarrando: nao", out Mpu6050TextSample sample), Is.True);
        Assert.That(sample.actionPressed, Is.False);
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
    public void MpuTiltUsesDeadZoneProgressiveSpeedAndInversion()
    {
        Assert.That(TrainingInputMath.TiltToAdvance(7.9f, false), Is.Zero);
        Assert.That(TrainingInputMath.TiltToAdvance(19f, false), Is.EqualTo(0.5f).Within(0.0001f));
        Assert.That(TrainingInputMath.TiltToAdvance(30f, false), Is.EqualTo(1f).Within(0.0001f));
        Assert.That(TrainingInputMath.TiltToAdvance(-30f, false), Is.EqualTo(-1f).Within(0.0001f));
        Assert.That(TrainingInputMath.TiltToAdvance(30f, true), Is.EqualTo(-1f).Within(0.0001f));
        Assert.That(TrainingInputMath.LongitudinalTiltDegrees(new Vector3(-9.81f, 0f, 0f)), Is.EqualTo(90f).Within(0.001f));
    }

    [Test]
    public void ExperimentalSerialTimeoutAcceptsSevenHundredMillisecondPackets()
    {
        long now = DateTime.UtcNow.Ticks;
        Assert.That(SerialControllerInput.IsPacketFresh(now - TimeSpan.FromSeconds(0.7).Ticks, now), Is.True);
        Assert.That(SerialControllerInput.IsPacketFresh(now - TimeSpan.FromSeconds(1.9).Ticks, now), Is.True);
        Assert.That(SerialControllerInput.IsPacketFresh(now - TimeSpan.FromSeconds(2.1).Ticks, now), Is.False);
    }

    [TestCase("16", "COM16")]
    [TestCase("COM16", "COM16")]
    [TestCase("com 16", "COM16")]
    [TestCase("", "COM16")]
    public void ExperimentalSerialNormalizesPortSixteen(string value, string expected)
    {
        Assert.That(SerialControllerInput.NormalizePortName(value), Is.EqualTo(expected));
    }

    [Test]
    public void ActionButtonOnlyCreatesAnEdgeWhenFirstPressed()
    {
        Assert.That(TrainingInputMath.IsActionPressedEdge(true, false), Is.True);
        Assert.That(TrainingInputMath.IsActionPressedEdge(true, true), Is.False);
        Assert.That(TrainingInputMath.IsActionPressedEdge(false, true), Is.False);
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
    public void CenteredGuidanceArrowStaysHorizontalAndVerticalTiltIsLimited()
    {
        Vector2 centered = TrainingNavigationVisuals.ComputeScreenDirection(Vector3.forward);
        Vector2 highTarget = TrainingNavigationVisuals.ComputeScreenDirection(new Vector3(0f, 1f, 0.1f));

        Assert.That(Vector2.Dot(centered, Vector2.right), Is.GreaterThan(0.999f));
        Assert.That(Vector2.Angle(Vector2.right, highTarget), Is.LessThanOrEqualTo(52.01f));
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
