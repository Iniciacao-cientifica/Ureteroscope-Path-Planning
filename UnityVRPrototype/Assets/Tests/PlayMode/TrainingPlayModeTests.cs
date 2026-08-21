using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class TrainingPlayModeTests
{
    private static Bounds EncapsulateRenderers(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        Assert.That(renderers.Length, Is.GreaterThan(0));
        Bounds bounds = renderers[0].bounds;
        for (int index = 1; index < renderers.Length; index++) bounds.Encapsulate(renderers[index].bounds);
        return bounds;
    }

    private static IEnumerator WaitUntilCourseReady(Component course)
    {
        Assert.That(course, Is.Not.Null, "The generic HRA training course is missing.");
        PropertyInfo isReady = course.GetType().GetProperty("IsReady");
        float timeout = Time.realtimeSinceStartup + 15f;
        while (!(bool)isReady.GetValue(course) && Time.realtimeSinceStartup < timeout) yield return null;
        Assert.That((bool)isReady.GetValue(course), Is.True, "The generic HRA course did not become ready.");
    }

    [UnityTest]
    public IEnumerator DesktopSceneStartsWithTrainingComponents()
    {
        SceneManager.LoadScene("UreteroscopyDesktopTraining");
        yield return null;
        GameObject controller = GameObject.Find("Desktop Training Controller");
        GameObject loader = GameObject.Find("Training Case Loader");
        Assert.That(controller, Is.Not.Null);
        Assert.That(loader, Is.Not.Null);
        Assert.That(controller.GetComponent("UreteroscopyTrainingController"), Is.Not.Null);
        Assert.That(controller.GetComponent("HraTrainingCourse"), Is.Not.Null);
        Assert.That(controller.GetComponent("ExternalExplorationCameraController"), Is.Not.Null);
        Assert.That(loader.GetComponent("VrCaseLoader"), Is.Not.Null);
        Assert.That(GameObject.Find("Endoscopic Camera")?.GetComponent<Camera>(), Is.Not.Null);
        Assert.That(GameObject.Find("Minimap Camera")?.GetComponent<Camera>(), Is.Not.Null);
    }

    [UnityTest]
    public IEnumerator GenericTrainingStartsInsideBladderAndTargetsRightKidneyWithoutPatientCase()
    {
        SceneManager.LoadScene("UreteroscopyDesktopTraining");
        yield return null;
        Component controller = GameObject.Find("Desktop Training Controller")?.GetComponent("UreteroscopyTrainingController");
        Component course = controller?.GetComponent("HraTrainingCourse");
        yield return WaitUntilCourseReady(course);

        Type type = course.GetType();
        Transform contentRoot = type.GetProperty("ContentRoot").GetValue(course) as Transform;
        Vector3 startLocal = (Vector3)type.GetProperty("StartLocal").GetValue(course);
        Vector3 targetLocal = (Vector3)type.GetProperty("TargetLocal").GetValue(course);
        float routeLength = (float)type.GetProperty("RouteLengthMeters").GetValue(course);
        Assert.That(routeLength, Is.GreaterThan(0.15f));
        Assert.That((bool)type.GetMethod("ContainsProbe").Invoke(course,
            new object[] { contentRoot.TransformPoint(startLocal), 0.002f }), Is.True);

        GameObject bladder = type.GetProperty("Bladder").GetValue(course) as GameObject;
        Bounds bladderBounds = EncapsulateRenderers(bladder);
        Assert.That(bladderBounds.Contains(contentRoot.TransformPoint(startLocal)), Is.True);
        GameObject rightKidney = type.GetProperty("RightKidney").GetValue(course) as GameObject;
        Bounds kidneyBounds = EncapsulateRenderers(rightKidney);
        kidneyBounds.Expand(0.02f);
        Assert.That(kidneyBounds.Contains(contentRoot.TransformPoint(targetLocal)), Is.True);

        Component legacyLoader = GameObject.Find("Training Case Loader")?.GetComponent("VrCaseLoader");
        Assert.That(((Behaviour)legacyLoader).enabled, Is.False);
        Assert.That(legacyLoader.GetType().GetProperty("AnatomyObject").GetValue(legacyLoader), Is.Null);
    }

    [UnityTest]
    public IEnumerator StartingRepeatedTrainingSessionsPreservesTheEndoscopicCamera()
    {
        SceneManager.LoadScene("UreteroscopyDesktopTraining");
        yield return null;
        Component controller = GameObject.Find("Desktop Training Controller")?.GetComponent("UreteroscopyTrainingController");
        Component course = controller.GetComponent("HraTrainingCourse");
        yield return WaitUntilCourseReady(course);

        Type type = controller.GetType();
        MethodInfo begin = type.GetMethod("BeginSession");
        MethodInfo abort = type.GetMethod("AbortSession");
        FieldInfo cameraField = type.GetField("endoscopeCamera");
        for (int attempt = 0; attempt < 2; attempt++)
        {
            begin.Invoke(controller, null);
            yield return null;
            yield return null;
            Camera camera = cameraField.GetValue(controller) as Camera;
            Assert.That(type.GetProperty("State").GetValue(controller).ToString(), Is.EqualTo("Running"));
            Assert.That(camera, Is.Not.Null);
            Assert.That(camera.transform.parent?.name, Is.EqualTo("Training Ureteroscope Tip"));
            Assert.That(Camera.main, Is.SameAs(camera));
            abort.Invoke(controller, null);
            yield return null;
        }
    }

    [UnityTest]
    public IEnumerator ExplorationUsesConnectedHraSystemThinRouteStoneAndExternalCamera()
    {
        SceneManager.LoadScene("UreteroscopyDesktopTraining");
        yield return null;
        Component controller = GameObject.Find("Desktop Training Controller")?.GetComponent("UreteroscopyTrainingController");
        Assert.That(controller, Is.Not.Null);
        Component course = controller.GetComponent("HraTrainingCourse");
        yield return WaitUntilCourseReady(course);

        Type controllerType = controller.GetType();
        Type courseType = course.GetType();
        FieldInfo experienceMode = controllerType.GetField("experienceMode");
        experienceMode.SetValue(controller, Enum.Parse(experienceMode.FieldType, "Exploration"));
        float routeLength = (float)courseType.GetProperty("RouteLengthMeters").GetValue(course);
        controllerType.GetMethod("BeginSession").Invoke(controller, null);
        yield return null;
        yield return null;

        Assert.That(controllerType.GetProperty("State").GetValue(controller).ToString(), Is.EqualTo("Running"));
        Assert.That(GameObject.Find("Scientific Exploration Environment"), Is.Null);
        GameObject arrow = GameObject.Find("Route Guidance Arrow");
        Assert.That(arrow == null || !arrow.activeInHierarchy, Is.True,
            "The centered arrow must be absent or hidden in external exploration.");

        GameObject root = courseType.GetProperty("ExternalVisualRoot").GetValue(course) as GameObject;
        Assert.That(root.activeInHierarchy, Is.True);
        Assert.That(root.layer, Is.EqualTo(27));
        foreach (string propertyName in new[] { "LeftKidney", "RightKidney", "LeftUreter", "RightUreter", "Bladder" })
        {
            Assert.That(courseType.GetProperty(propertyName).GetValue(course), Is.Not.Null, propertyName);
        }
        Assert.That(UnityEngine.Object.FindObjectsByType<Transform>()
            .Count(item => item.name == "HRA Generic External Urinary System"), Is.EqualTo(1));

        GameObject interior = courseType.GetProperty("InteriorVisualRoot").GetValue(course) as GameObject;
        Assert.That(interior == null || !interior.activeInHierarchy, Is.True,
            "The procedural lumen must be hidden in external exploration.");
        Component legacyLoader = GameObject.Find("Training Case Loader")?.GetComponent("VrCaseLoader");
        Assert.That(((Behaviour)legacyLoader).enabled, Is.False,
            "Patient-derived cases must not load in the generic desktop training scene.");
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            if (!renderer.enabled) continue;
            Assert.That(renderer.sharedMaterial.renderQueue, Is.LessThan(3000));
            if (renderer.sharedMaterial.HasProperty("_Cull"))
                Assert.That(renderer.sharedMaterial.GetFloat("_Cull"), Is.EqualTo(2f));
        }

        Vector3 start = (Vector3)courseType.GetProperty("StartLocal").GetValue(course);
        Vector3 target = (Vector3)courseType.GetProperty("TargetLocal").GetValue(course);
        Assert.That(Vector3.Distance(start, target), Is.GreaterThan(0.15f));

        GameObject routeTube = GameObject.Find("Smoothed Route");
        Assert.That(routeTube.GetComponent<Renderer>().enabled, Is.False);
        Component navigation = controller.GetComponent("TrainingNavigationVisuals");
        Type navigationType = navigation.GetType();
        LineRenderer routeLine = navigationType.GetProperty("ExplorationRouteLine").GetValue(navigation) as LineRenderer;
        LineRenderer minimapLine = navigationType.GetProperty("MinimapRouteLine").GetValue(navigation) as LineRenderer;
        Assert.That(routeLine, Is.Not.Null);
        Assert.That(routeLine.startWidth, Is.EqualTo(0.001f).Within(0.00001f));
        Assert.That(routeLine.endWidth, Is.EqualTo(0.001f).Within(0.00001f));
        Assert.That(routeLine.sharedMaterial.shader.name, Is.EqualTo("Murillo/Training Route Opaque"));
        Vector3[] routePositions = (Vector3[])courseType.GetMethod("CopyRoutePositions").Invoke(course, null);
        Assert.That(routeLine.positionCount, Is.EqualTo(routePositions.Length));
        Assert.That(minimapLine, Is.Not.Null);
        Assert.That(minimapLine.startWidth, Is.EqualTo(0.0015f).Within(0.00001f));
        Assert.That(minimapLine.gameObject.layer, Is.EqualTo(30));

        Camera minimap = controllerType.GetField("minimapCamera").GetValue(controller) as Camera;
        Assert.That(minimap.cullingMask & (1 << 30), Is.Not.Zero);
        Assert.That(minimap.cullingMask & (1 << 27), Is.Not.Zero);
        Assert.That(minimap.cullingMask & (1 << 28), Is.Zero);

        GameObject startMarker = courseType.GetProperty("StartMarkerObject").GetValue(course) as GameObject;
        GameObject stone = courseType.GetProperty("CurrentTargetObject").GetValue(course) as GameObject;
        Assert.That(startMarker == null || !startMarker.activeSelf, Is.True,
            "The spherical start marker must be absent or hidden in external exploration.");
        Assert.That(stone, Is.Not.Null);
        Assert.That(stone.GetComponent<SphereCollider>(), Is.Null);
        Vector3[] stoneVertices = stone.GetComponent<MeshFilter>().sharedMesh.vertices;
        float minRadius = stoneVertices.Min(vertex => vertex.magnitude);
        float maxRadius = stoneVertices.Max(vertex => vertex.magnitude);
        Assert.That(maxRadius / minRadius, Is.GreaterThan(1.2f));
        LineRenderer halo = navigationType.GetProperty("StoneHaloLine").GetValue(navigation) as LineRenderer;
        Assert.That(halo, Is.Not.Null);
        Assert.That(halo.loop, Is.True);
        Assert.That(halo.startWidth, Is.EqualTo(0.0004f).Within(0.00001f));

        Component externalCamera = controller.GetComponent("ExternalExplorationCameraController");
        Type externalCameraType = externalCamera.GetType();
        Camera mainCamera = controllerType.GetField("endoscopeCamera").GetValue(controller) as Camera;
        Assert.That((bool)externalCameraType.GetProperty("IsOverview").GetValue(externalCamera), Is.True);
        Assert.That(externalCameraType.GetProperty("Mode").GetValue(externalCamera).ToString(), Is.EqualTo("FreeCamera"));
        Assert.That((bool)externalCameraType.GetProperty("IsFollowing").GetValue(externalCamera), Is.False);
        Assert.That(mainCamera.transform.parent, Is.Null);
        Transform probe = GameObject.Find("Training Ureteroscope Tip").transform;
        Vector3 probeBeforeFreeCamera = probe.position;
        Vector3 cameraBeforeFreeMovement = mainCamera.transform.position;
        externalCameraType.GetMethod("ApplyFreeCameraInput").Invoke(externalCamera,
            new object[] { Vector3.right, Vector2.zero, false, 1f });
        Assert.That(Vector3.Distance(mainCamera.transform.position, cameraBeforeFreeMovement), Is.GreaterThan(0.001f));
        Assert.That(Vector3.Distance(probe.position, probeBeforeFreeCamera), Is.LessThan(0.000001f));
        Assert.That((bool)externalCameraType.GetProperty("IsOverview").GetValue(externalCamera), Is.False);

        externalCameraType.GetMethod("NotifyProbeAdvanced").Invoke(externalCamera, new object[] { 0.0006f });
        yield return null;
        Assert.That((bool)externalCameraType.GetProperty("IsFollowing").GetValue(externalCamera), Is.False,
            "Probe movement must not switch camera modes automatically.");
        Type navigationModeType = externalCameraType.GetProperty("Mode").PropertyType;
        externalCameraType.GetMethod("SetNavigationMode").Invoke(externalCamera,
            new[] { Enum.Parse(navigationModeType, "ProbeFollow") });
        yield return null;
        Assert.That((bool)externalCameraType.GetProperty("IsFollowing").GetValue(externalCamera), Is.True);
        Assert.That(externalCameraType.GetProperty("Mode").GetValue(externalCamera).ToString(), Is.EqualTo("ProbeFollow"));
        Assert.That(Vector3.Distance(probe.position, probeBeforeFreeCamera), Is.LessThan(0.000001f));
        probe.rotation = Quaternion.LookRotation(Vector3.right, Vector3.up);
        object[] followPose = { Vector3.zero, Quaternion.identity };
        externalCameraType.GetMethod("CalculateFollowPose", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(externalCamera, followPose);
        Vector3 desiredFollowPosition = (Vector3)followPose[0];
        Assert.That(Vector3.Dot(desiredFollowPosition - probe.position, probe.forward), Is.LessThan(0f),
            "The follow pose must be behind probe.forward rather than behind the route tangent.");
        Assert.That(mainCamera.transform.parent, Is.Null);
        Assert.That((float)courseType.GetProperty("RouteLengthMeters").GetValue(course),
            Is.EqualTo(routeLength).Within(0.000001f));

        controllerType.GetMethod("ExitExploration").Invoke(controller, null);
        yield return null;
        Assert.That(controllerType.GetProperty("State").GetValue(controller).ToString(), Is.EqualTo("Ready"));
        Assert.That(interior.activeInHierarchy, Is.True);
        Assert.That(mainCamera.transform.parent?.name, Is.EqualTo("Training Ureteroscope Tip"));
        Assert.That(controllerType.GetField("lastResult", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller), Is.Null);
    }

    [UnityTest]
    public IEnumerator ThinInternalTrainingRouteRemainsVisibleForEveryDifficulty()
    {
        SceneManager.LoadScene("UreteroscopyDesktopTraining");
        yield return null;
        Component controller = GameObject.Find("Desktop Training Controller")?.GetComponent("UreteroscopyTrainingController");
        Component course = controller.GetComponent("HraTrainingCourse");
        yield return WaitUntilCourseReady(course);
        Type type = controller.GetType();
        type.GetField("experienceMode").SetValue(controller, Enum.Parse(type.GetField("experienceMode").FieldType, "Training"));
        type.GetMethod("BeginSession").Invoke(controller, null);
        yield return null;
        yield return null;
        MethodInfo apply = type.GetMethod("ApplyDifficultyVisuals", BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo difficultyField = type.GetField("difficulty");
        Component visuals = controller.GetComponent("TrainingNavigationVisuals");
        Type visualsType = visuals.GetType();
        LineRenderer trainingLine = visualsType.GetProperty("TrainingRouteLine").GetValue(visuals) as LineRenderer;
        LineRenderer minimapLine = visualsType.GetProperty("TrainingMinimapRouteLine").GetValue(visuals) as LineRenderer;
        Assert.That((bool)visualsType.GetProperty("TrainingGuidanceActive").GetValue(visuals), Is.True);
        Assert.That(trainingLine, Is.Not.Null);
        Assert.That(trainingLine.startWidth, Is.EqualTo(0.001f).Within(0.00001f));
        Assert.That(trainingLine.sharedMaterial.shader.name, Is.EqualTo("Murillo/Training Route Opaque"));
        Assert.That(trainingLine.sharedMaterial.GetFloat("_ZTest"),
            Is.EqualTo((float)UnityEngine.Rendering.CompareFunction.LessEqual));
        Assert.That(minimapLine, Is.Not.Null);
        Assert.That(minimapLine.startWidth, Is.EqualTo(0.0015f).Within(0.00001f));
        Assert.That(minimapLine.gameObject.layer, Is.EqualTo(30));
        Assert.That(GameObject.Find("Smoothed Route").GetComponent<Renderer>().enabled, Is.False);
        foreach (string difficulty in new[] { "Tutorial", "Intermediate", "Advanced" })
        {
            difficultyField.SetValue(controller, Enum.Parse(difficultyField.FieldType, difficulty));
            apply.Invoke(controller, null);
            Assert.That(trainingLine.gameObject.activeInHierarchy, Is.True, difficulty);
            Assert.That((bool)visualsType.GetProperty("AdaptiveRouteActive").GetValue(visuals), Is.False);
        }
    }

    [UnityTest]
    public IEnumerator CollisionEpisodesCountOncePerContinuousContactAndFifthEndsAsDnf()
    {
        SceneManager.LoadScene("UreteroscopyDesktopTraining");
        yield return null;
        Component controller = GameObject.Find("Desktop Training Controller")?.GetComponent("UreteroscopyTrainingController");
        Component course = controller.GetComponent("HraTrainingCourse");
        yield return WaitUntilCourseReady(course);
        Type type = controller.GetType();
        type.GetMethod("BeginSession").Invoke(controller, null);
        MethodInfo updateCollision = type.GetMethod("UpdateCollisionState", BindingFlags.Instance | BindingFlags.NonPublic);
        updateCollision.Invoke(controller, new object[] { true });
        updateCollision.Invoke(controller, new object[] { true });
        Assert.That(type.GetProperty("CollisionEvents").GetValue(controller), Is.EqualTo(1));
        Assert.That(type.GetProperty("RemainingCollisionEvents").GetValue(controller), Is.EqualTo(4));
        Assert.That((float)type.GetField("collisionFlashUntil", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(controller), Is.GreaterThan(Time.unscaledTime));
        updateCollision.Invoke(controller, new object[] { false });
        for (int collision = 2; collision <= 5; collision++)
        {
            updateCollision.Invoke(controller, new object[] { true });
            if (collision < 5) updateCollision.Invoke(controller, new object[] { false });
        }
        Assert.That(type.GetProperty("CollisionEvents").GetValue(controller), Is.EqualTo(5));
        Assert.That(type.GetProperty("RemainingCollisionEvents").GetValue(controller), Is.EqualTo(0));
        Assert.That(type.GetProperty("State").GetValue(controller).ToString(), Is.EqualTo("Finished"));
        Assert.That((string)type.GetField("feedbackMessage", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(controller), Does.Contain("COLISÃO — 5/5"));
        Assert.That((string)type.GetField("lastCsvPath", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(controller), Is.Not.Empty);
    }

    [UnityTest]
    public IEnumerator GiveUpDialogPausesSessionAndRequiresConfirmation()
    {
        SceneManager.LoadScene("UreteroscopyDesktopTraining");
        yield return null;
        Component controller = GameObject.Find("Desktop Training Controller")?.GetComponent("UreteroscopyTrainingController");
        Component course = controller.GetComponent("HraTrainingCourse");
        yield return WaitUntilCourseReady(course);
        Type type = controller.GetType();
        type.GetMethod("BeginSession").Invoke(controller, null);
        yield return null;
        yield return null;
        type.GetMethod("RequestGiveUpConfirmation").Invoke(controller, null);
        float before = (float)type.GetProperty("ElapsedSeconds").GetValue(controller);
        yield return null;
        yield return null;
        Assert.That((float)type.GetProperty("ElapsedSeconds").GetValue(controller), Is.EqualTo(before).Within(0.0001f));
        type.GetMethod("CancelGiveUpConfirmation").Invoke(controller, null);
        type.GetMethod("RequestGiveUpConfirmation").Invoke(controller, null);
        type.GetMethod("ConfirmGiveUp").Invoke(controller, null);
        Assert.That(type.GetProperty("State").GetValue(controller).ToString(), Is.EqualTo("Finished"));
    }

    [UnityTest]
    public IEnumerator SphereCastsCanDetectBothFrontFacesAndInternalBackfaces()
    {
        GameObject wall = new GameObject("Backface Test Wall");
        wall.layer = 30;
        wall.transform.position = new Vector3(10000f, 0f, 0f);
        Mesh mesh = new Mesh
        {
            vertices = new[] { new Vector3(0f, -1f, -1f), new Vector3(0f, 1f, -1f), new Vector3(0f, 0f, 1f) },
            triangles = new[] { 0, 1, 2 }
        };
        wall.AddComponent<MeshCollider>().sharedMesh = mesh;
        Physics.SyncTransforms();
        bool previous = Physics.queriesHitBackfaces;
        try
        {
            Physics.queriesHitBackfaces = true;
            Assert.That(Physics.SphereCast(new Vector3(10001f, 0f, 0f), 0.05f, Vector3.left, out _, 2f, 1 << 30), Is.True);
            Assert.That(Physics.SphereCast(new Vector3(9999f, 0f, 0f), 0.05f, Vector3.right, out _, 2f, 1 << 30), Is.True);
        }
        finally
        {
            Physics.queriesHitBackfaces = previous;
            UnityEngine.Object.Destroy(wall);
            UnityEngine.Object.Destroy(mesh);
        }
        yield return null;
    }

    [UnityTest]
    public IEnumerator FreeCameraCollisionStopsBeforeExternalAnatomyLayer()
    {
        SceneManager.LoadScene("UreteroscopyDesktopTraining");
        yield return null;
        Component controller = GameObject.Find("Desktop Training Controller")?.GetComponent("UreteroscopyTrainingController");
        Component externalCamera = controller.GetComponent("ExternalExplorationCameraController");
        Assert.That(externalCamera, Is.Not.Null);

        GameObject wall = new GameObject("External Camera Collision Wall");
        wall.layer = 27;
        wall.transform.position = new Vector3(10000f, 0f, 0f);
        Mesh mesh = new Mesh
        {
            vertices = new[] { new Vector3(0f, -1f, -1f), new Vector3(0f, 1f, -1f), new Vector3(0f, 0f, 1f) },
            triangles = new[] { 0, 1, 2 }
        };
        wall.AddComponent<MeshCollider>().sharedMesh = mesh;
        Physics.SyncTransforms();

        MethodInfo resolve = externalCamera.GetType().GetMethod("ResolveMovementCollision",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Vector3 resolved = (Vector3)resolve.Invoke(externalCamera,
            new object[] { new Vector3(9999f, 0f, 0f), Vector3.right * 2f });
        Assert.That(resolved.x, Is.LessThan(10000f));
        Assert.That(resolved.x, Is.GreaterThanOrEqualTo(9999f));

        UnityEngine.Object.Destroy(wall);
        UnityEngine.Object.Destroy(mesh);
        yield return null;
    }
}
