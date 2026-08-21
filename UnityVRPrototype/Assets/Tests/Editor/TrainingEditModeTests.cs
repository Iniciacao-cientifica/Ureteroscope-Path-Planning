using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class TrainingEditModeTests
{
    [Test]
    public void HraUrinarySystemAssetsAreImportedAsPrefabs()
    {
        foreach (string asset in new[]
        {
            "VH_M_Kidney_L.glb", "VH_M_Kidney_R.glb", "VH_M_Ureter_L.glb",
            "VH_M_Ureter_R.glb", "VH_M_Urinary_Bladder.glb"
        })
        {
            string path = "Assets/Resources/HRAKidneys/" + asset;
            Assert.That(File.Exists(path), Is.True, path);
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(path), Is.Not.Null, path);
        }
    }

    [Test]
    public void ProceduralStoneIsDeterministicIrregularAndUsesRequestedDiameter()
    {
        Mesh first = VrStoneMeshBuilder.Build(0.006f, "stone_001");
        Mesh second = VrStoneMeshBuilder.Build(0.006f, "stone_001");
        try
        {
            Assert.That(second.vertices, Is.EqualTo(first.vertices));
            float minimumRadius = float.PositiveInfinity;
            float maximumRadius = 0f;
            foreach (Vector3 vertex in first.vertices)
            {
                minimumRadius = Mathf.Min(minimumRadius, vertex.magnitude);
                maximumRadius = Mathf.Max(maximumRadius, vertex.magnitude);
            }
            Assert.That(maximumRadius / minimumRadius, Is.GreaterThan(1.2f));
            Assert.That(first.bounds.size.x, Is.InRange(0.0045f, 0.0085f));
            Assert.That(first.triangles.Length, Is.GreaterThan(500));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(first);
            UnityEngine.Object.DestroyImmediate(second);
        }
    }

    [Test]
    public void RouteTubeIsClosedContinuousAndHasNoDegenerateFaces()
    {
        Vector3[] path =
        {
            new Vector3(0f, 0f, 0f),
            new Vector3(0f, 1f, 0f),
            new Vector3(0f, 1f, 0f),
            new Vector3(0.1f, 2f, 0f),
            new Vector3(1f, 2f, 0f),
            new Vector3(1f, 2f, 1f)
        };
        Vector3[] distinctPath = { path[0], path[1], path[3], path[4], path[5] };
        const int sides = 8;
        const float radius = 0.01f;
        Mesh mesh = VrTubeMeshBuilder.Build(path, radius, sides, "Route Tube Test");
        try
        {
            Assert.That(mesh.vertexCount, Is.EqualTo(distinctPath.Length * sides + 2),
                "Consecutive duplicate route points must be removed.");
            Assert.That(mesh.triangles.Length / 3, Is.EqualTo(80),
                "The tube must include all side faces and two closed caps.");

            Vector3[] vertices = mesh.vertices;
            for (int ring = 0; ring < distinctPath.Length; ring++)
            {
                for (int side = 0; side < sides; side++)
                {
                    float measuredRadius = Vector3.Distance(vertices[ring * sides + side], distinctPath[ring]);
                    Assert.That(measuredRadius, Is.EqualTo(radius).Within(0.00001f));
                }
            }

            int[] triangles = mesh.triangles;
            for (int index = 0; index < triangles.Length; index += 3)
            {
                Vector3 first = vertices[triangles[index]];
                Vector3 second = vertices[triangles[index + 1]];
                Vector3 third = vertices[triangles[index + 2]];
                Assert.That(Vector3.Cross(second - first, third - first).sqrMagnitude,
                    Is.GreaterThan(0.000000000001f), "The route tube must not contain collapsed faces.");
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(mesh);
        }
    }

    [Test]
    public void VariableLumenUsesTheSameRadiusSamplesAsCollisionMath()
    {
        Vector3[] centerline =
        {
            Vector3.zero,
            new Vector3(0f, 0f, 0.05f),
            new Vector3(0.01f, 0f, 0.1f)
        };
        float[] radii = { 0.02f, 0.006f, 0.012f };
        const int sides = 12;
        Mesh mesh = TrainingLumenMeshBuilder.Build(centerline, radii, sides, "Variable Lumen Test");
        try
        {
            Assert.That(mesh.vertexCount, Is.EqualTo(centerline.Length * sides + 2));
            Vector3[] vertices = mesh.vertices;
            for (int ring = 0; ring < centerline.Length; ring++)
            {
                for (int side = 0; side < sides; side++)
                {
                    Assert.That(Vector3.Distance(vertices[ring * sides + side], centerline[ring]),
                        Is.EqualTo(radii[ring]).Within(0.00001f));
                }
            }
            Assert.That(TrainingLumenMath.IsInside(new Vector3(0.003f, 0f, 0.05f), centerline, radii, 0.002f), Is.True);
            Assert.That(TrainingLumenMath.IsInside(new Vector3(0.005f, 0f, 0.05f), centerline, radii, 0.002f), Is.False);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(mesh);
        }
    }

    [Test]
    public void LumenCollisionStopsAtFirstWallAndCannotTunnelThroughIt()
    {
        Vector3[] centerline = { Vector3.zero, Vector3.forward * 0.1f };
        float[] radii = { 0.006f, 0.006f };
        const float tipRadius = 0.002f;
        float permitted = TrainingLumenMath.FindAllowedDistance(
            new Vector3(0f, 0f, 0.05f),
            Vector3.right,
            0.02f,
            centerline,
            radii,
            tipRadius);
        Assert.That(permitted, Is.EqualTo(0.004f).Within(0.0001f));
        Assert.That(permitted, Is.LessThan(0.02f));

        float reverse = TrainingLumenMath.FindAllowedDistance(
            new Vector3(0f, 0f, 0.05f),
            Vector3.back,
            0.08f,
            centerline,
            radii,
            tipRadius);
        Assert.That(reverse, Is.EqualTo(0.054f).Within(0.001f),
            "The spherical end envelope must also block reverse movement.");
    }

    [Test]
    public void EditableCourseSplineKeepsEndpointsAndPositiveRadii()
    {
        TrainingCourseKnot[] control =
        {
            new TrainingCourseKnot(Vector3.zero, 0.02f),
            new TrainingCourseKnot(new Vector3(0.01f, 0.02f, 0.03f), 0.004f),
            new TrainingCourseKnot(new Vector3(0.02f, 0.05f, 0.04f), 0.004f),
            new TrainingCourseKnot(new Vector3(0.03f, 0.08f, 0.05f), 0.01f)
        };
        Vector3[] sampled = TrainingCoursePath.ResampleCatmullRom(control, 0.001f, out float[] radii);
        Assert.That(Vector3.Distance(sampled[0], control[0].position), Is.LessThan(0.000001f));
        Assert.That(
            Vector3.Distance(sampled[sampled.Length - 1], control[control.Length - 1].position),
            Is.LessThan(0.000001f));
        Assert.That(sampled.Length, Is.EqualTo(radii.Length));
        Assert.That(radii.All(radius => radius > 0f), Is.True);
    }

    [Test]
    public void ObjParserRemovesSmallDisconnectedFragmentsAndCompactsVertices()
    {
        const string obj =
            "v 0 0 0\n" +
            "v 1 0 0\n" +
            "v 0 1 0\n" +
            "v 0 0 1\n" +
            "v 10 0 0\n" +
            "v 11 0 0\n" +
            "v 10 1 0\n" +
            "f 1 2 3\n" +
            "f 1 4 2\n" +
            "f 2 4 3\n" +
            "f 1 3 4\n" +
            "f 5 6 7\n";

        Mesh unfiltered = VrObjParser.Parse(obj, "Unfiltered");
        Mesh filtered = VrObjParser.Parse(obj, "Filtered", 2);
        try
        {
            Assert.That(unfiltered.triangles.Length / 3, Is.EqualTo(5));
            Assert.That(filtered.triangles.Length / 3, Is.EqualTo(4));
            Assert.That(filtered.vertexCount, Is.EqualTo(4));
            Assert.That(filtered.bounds.max.x, Is.LessThan(2f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(unfiltered);
            UnityEngine.Object.DestroyImmediate(filtered);
        }
    }

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
        VrCaseLoader legacyLoader = GameObject.Find("Training Case Loader")?.GetComponent<VrCaseLoader>();
        UreteroscopyTrainingController controller = GameObject.Find("Desktop Training Controller")?.GetComponent<UreteroscopyTrainingController>();
        Assert.That(legacyLoader, Is.Not.Null);
        Assert.That(legacyLoader.enabled, Is.False);
        Assert.That(controller, Is.Not.Null);
        Assert.That(controller.GetComponent<HraTrainingCourse>(), Is.Not.Null);
        Assert.That(UnityEngine.Object.FindObjectsByType<Camera>().Length, Is.GreaterThanOrEqualTo(2));
        Assert.That(UreteroscopyTrainingController.ActiveHudWidth, Is.LessThanOrEqualTo(350f));
        Assert.That(UreteroscopyTrainingController.ActiveMinimapMaximumSize, Is.LessThanOrEqualTo(220f));
    }
}
