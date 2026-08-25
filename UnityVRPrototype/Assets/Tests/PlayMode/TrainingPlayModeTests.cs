using System.Collections;
using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class TrainingPlayModeTests
{
    private static IEnumerator WaitUntilCaseReady(Component loader)
    {
        PropertyInfo isReady = loader.GetType().GetProperty("IsReady");
        float timeout = Time.realtimeSinceStartup + 10f;
        while (!(bool)isReady.GetValue(loader) && Time.realtimeSinceStartup < timeout)
        {
            yield return null;
        }
        Assert.That((bool)isReady.GetValue(loader), Is.True, "The training case did not become ready.");
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
        Assert.That(loader.GetComponent("VrCaseLoader"), Is.Not.Null);
        Assert.That(GameObject.Find("Endoscopic Camera")?.GetComponent<Camera>(), Is.Not.Null);
        Assert.That(GameObject.Find("Minimap Camera")?.GetComponent<Camera>(), Is.Not.Null);
    }

    [UnityTest]
    public IEnumerator StartingRepeatedSessionsPreservesTheEndoscopicCamera()
    {
        SceneManager.LoadScene("UreteroscopyDesktopTraining");
        yield return null;

        Component controller = GameObject.Find("Desktop Training Controller")?.GetComponent("UreteroscopyTrainingController");
        Component loader = GameObject.Find("Training Case Loader")?.GetComponent("VrCaseLoader");
        Assert.That(controller, Is.Not.Null);
        Assert.That(loader, Is.Not.Null);

        yield return WaitUntilCaseReady(loader);

        MethodInfo beginSession = controller.GetType().GetMethod("BeginSession");
        MethodInfo abortSession = controller.GetType().GetMethod("AbortSession");
        FieldInfo cameraField = controller.GetType().GetField("endoscopeCamera");
        Assert.That(beginSession, Is.Not.Null);
        Assert.That(abortSession, Is.Not.Null);
        Assert.That(cameraField, Is.Not.Null);

        for (int attempt = 0; attempt < 2; attempt++)
        {
            beginSession.Invoke(controller, null);
            yield return null;
            yield return null;

            Assert.That(controller.GetType().GetProperty("State").GetValue(controller).ToString(), Is.EqualTo("Running"),
                "Keyboard/mouse sessions must calibrate automatically after Start.");
            Camera camera = cameraField.GetValue(controller) as Camera;
            Assert.That(camera, Is.Not.Null, $"Endoscopic camera was destroyed on attempt {attempt + 1}.");
            Assert.That(camera.gameObject.activeInHierarchy, Is.True);
            Assert.That(camera.enabled, Is.True);
            Assert.That(camera.targetTexture, Is.Null);
            Assert.That(Camera.main, Is.SameAs(camera));
            Assert.That(camera.transform.parent?.name, Is.EqualTo("Training Ureteroscope Tip"));

            abortSession.Invoke(controller, null);
            yield return null;
        }
    }

    [UnityTest]
    public IEnumerator PressingActionAtAnAlignedTargetCompletesOnlyOnce()
    {
        SceneManager.LoadScene("UreteroscopyDesktopTraining");
        yield return null;

        Component controller = GameObject.Find("Desktop Training Controller")?.GetComponent("UreteroscopyTrainingController");
        Component loader = GameObject.Find("Training Case Loader")?.GetComponent("VrCaseLoader");
        Assert.That(controller, Is.Not.Null);
        Assert.That(loader, Is.Not.Null);
        yield return WaitUntilCaseReady(loader);

        Type controllerType = controller.GetType();
        controllerType.GetMethod("BeginSession").Invoke(controller, null);
        yield return null;
        yield return null;
        Assert.That(controllerType.GetProperty("State").GetValue(controller).ToString(), Is.EqualTo("Running"));

        FieldInfo stableTimer = controllerType.GetField("targetStableTimer", BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo stableSeconds = controllerType.GetField("targetStableSeconds");
        FieldInfo neutralOrientation = controllerType.GetField("neutralOrientation", BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo lastEncoderTicks = controllerType.GetField("lastEncoderTicks", BindingFlags.Instance | BindingFlags.NonPublic);
        stableTimer.SetValue(controller, stableSeconds.GetValue(controller));

        MethodInfo processFrame = controllerType.GetMethod("ProcessFrame", BindingFlags.Instance | BindingFlags.NonPublic);
        Type frameType = processFrame.GetParameters()[0].ParameterType;
        object pressed = Activator.CreateInstance(frameType);
        frameType.GetField("orientation").SetValue(pressed, (Quaternion)neutralOrientation.GetValue(controller));
        frameType.GetField("encoderTicks").SetValue(pressed, (long)lastEncoderTicks.GetValue(controller));
        frameType.GetField("actionPressed").SetValue(pressed, true);
        frameType.GetField("imuOk").SetValue(pressed, true);
        frameType.GetField("firmwareVersion").SetValue(pressed, "mpu6050-text-test");
        processFrame.Invoke(controller, new object[] { pressed });

        Assert.That(controllerType.GetProperty("State").GetValue(controller).ToString(), Is.EqualTo("Finished"));
        object result = controllerType.GetField("lastResult", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
        Assert.That(result, Is.Not.Null);
        Assert.That((bool)result.GetType().GetField("completed").GetValue(result), Is.True);

        processFrame.Invoke(controller, new object[] { pressed });
        Assert.That(controllerType.GetField("lastResult", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller), Is.SameAs(result));
    }

    [UnityTest]
    public IEnumerator ExplorationShowsGuidanceAllowsFreeMovementAndDoesNotCreateResult()
    {
        SceneManager.LoadScene("UreteroscopyDesktopTraining");
        yield return null;

        Component controller = GameObject.Find("Desktop Training Controller")?.GetComponent("UreteroscopyTrainingController");
        Component loader = GameObject.Find("Training Case Loader")?.GetComponent("VrCaseLoader");
        Assert.That(controller, Is.Not.Null);
        Assert.That(loader, Is.Not.Null);
        yield return WaitUntilCaseReady(loader);

        Type controllerType = controller.GetType();
        FieldInfo experienceMode = controllerType.GetField("experienceMode");
        experienceMode.SetValue(controller, Enum.Parse(experienceMode.FieldType, "Exploration"));
        controllerType.GetMethod("BeginSession").Invoke(controller, null);
        yield return null;
        yield return null;

        Assert.That(controllerType.GetProperty("State").GetValue(controller).ToString(), Is.EqualTo("Running"));
        GameObject arrow = GameObject.Find("Route Guidance Arrow");
        GameObject environment = GameObject.Find("Scientific Exploration Environment");
        Assert.That(arrow, Is.Not.Null);
        Assert.That(arrow.activeInHierarchy, Is.True);
        Assert.That(environment, Is.Not.Null);
        Assert.That(environment.activeInHierarchy, Is.True);
        Assert.That(arrow.layer, Is.EqualTo(28));
        Camera minimap = controllerType.GetField("minimapCamera").GetValue(controller) as Camera;
        Assert.That(minimap.cullingMask & (1 << 28), Is.Zero, "Guidance visuals must not be rendered by the minimap.");

        FieldInfo probeField = controllerType.GetField("probe", BindingFlags.Instance | BindingFlags.NonPublic);
        Transform probe = probeField.GetValue(controller) as Transform;
        Transform contentRoot = loader.GetType().GetProperty("ContentRoot").GetValue(loader) as Transform;
        Vector3 nextRouteLocal = (Vector3)loader.GetType().GetMethod("SampleCurrentRouteLocal").Invoke(loader, new object[] { 0.02f });
        Vector3 expectedArrowDirection = (contentRoot.TransformPoint(nextRouteLocal) - probe.position).normalized;
        Camera endoscope = controllerType.GetField("endoscopeCamera").GetValue(controller) as Camera;
        Vector3 cameraDirection = endoscope.transform.InverseTransformDirection(expectedArrowDirection);
        Component navigation = controller.GetComponent("TrainingNavigationVisuals");
        MethodInfo computeScreenDirection = navigation.GetType().GetMethod("ComputeScreenDirection", BindingFlags.Public | BindingFlags.Static);
        Vector2 expectedScreenDirection = (Vector2)computeScreenDirection.Invoke(null, new object[] { cameraDirection });
        Vector2 actualScreenDirection = (Vector2)navigation.GetType().GetProperty("CurrentScreenDirection").GetValue(navigation);
        Assert.That(Vector2.Dot(actualScreenDirection, expectedScreenDirection), Is.GreaterThan(0.99f));
        Vector3 displayedDirection = arrow.transform.localRotation * Vector3.up;
        Assert.That(Vector2.Dot(new Vector2(displayedDirection.x, displayedDirection.y).normalized, expectedScreenDirection), Is.GreaterThan(0.99f));

        GameObject face = GameObject.Find("Guidance Arrow Face");
        GameObject outline = GameObject.Find("Guidance Arrow Outline");
        Assert.That(face, Is.Not.Null);
        Assert.That(outline, Is.Not.Null);
        Material[] faceMaterials = face.GetComponent<Renderer>().sharedMaterials;
        Assert.That(faceMaterials.Length, Is.EqualTo(3));
        Assert.That(faceMaterials[0].color, Is.Not.EqualTo(faceMaterials[1].color), "Arrow head and shaft need contrasting colors.");
        Assert.That(faceMaterials[2].color, Is.Not.EqualTo(faceMaterials[0].color), "Extruded arrow sides need a darker material.");
        Mesh arrowMesh = face.GetComponent<MeshFilter>().sharedMesh;
        Assert.That(arrowMesh.bounds.size.z, Is.GreaterThanOrEqualTo(0.0039f), "Guidance arrow must have real 3D depth.");
        Assert.That(Quaternion.Angle(face.transform.localRotation, Quaternion.identity), Is.GreaterThan(5f),
            "The 3D arrow should be tilted so its depth remains visible.");
        Vector3 before = probe.position;
        MethodInfo tryMove = controllerType.GetMethod("TryMoveProbe", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That((bool)tryMove.Invoke(controller, new object[] { 0.03f, false }), Is.True);
        Assert.That(Vector3.Distance(before, probe.position), Is.EqualTo(0.03f).Within(0.0001f));

        controllerType.GetMethod("ExitExploration").Invoke(controller, null);
        yield return null;
        Assert.That(controllerType.GetProperty("State").GetValue(controller).ToString(), Is.EqualTo("Ready"));
        Assert.That(controllerType.GetField("lastResult", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller), Is.Null);
        Assert.That(controllerType.GetField("lastCsvPath", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller), Is.EqualTo(""));
    }

    [UnityTest]
    public IEnumerator CollisionEpisodesCountOncePerContinuousContactAndFifthEndsAsDnf()
    {
        SceneManager.LoadScene("UreteroscopyDesktopTraining");
        yield return null;

        Component controller = GameObject.Find("Desktop Training Controller")?.GetComponent("UreteroscopyTrainingController");
        Component loader = GameObject.Find("Training Case Loader")?.GetComponent("VrCaseLoader");
        Assert.That(controller, Is.Not.Null);
        Assert.That(loader, Is.Not.Null);
        yield return WaitUntilCaseReady(loader);

        Type controllerType = controller.GetType();
        controllerType.GetMethod("BeginSession").Invoke(controller, null);
        MethodInfo updateCollision = controllerType.GetMethod("UpdateCollisionState", BindingFlags.Instance | BindingFlags.NonPublic);
        updateCollision.Invoke(controller, new object[] { true });
        updateCollision.Invoke(controller, new object[] { true });
        Assert.That(controllerType.GetProperty("CollisionEvents").GetValue(controller), Is.EqualTo(1));
        updateCollision.Invoke(controller, new object[] { false });

        for (int collision = 2; collision <= 5; collision++)
        {
            updateCollision.Invoke(controller, new object[] { true });
            if (collision < 5) updateCollision.Invoke(controller, new object[] { false });
        }

        Assert.That(controllerType.GetProperty("CollisionEvents").GetValue(controller), Is.EqualTo(5));
        Assert.That(controllerType.GetProperty("State").GetValue(controller).ToString(), Is.EqualTo("Finished"));
        object result = controllerType.GetField("lastResult", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
        Assert.That(result, Is.Not.Null);
        Assert.That((bool)result.GetType().GetField("completed").GetValue(result), Is.False);
    }

    [UnityTest]
    public IEnumerator GiveUpDialogPausesSessionAndRequiresConfirmation()
    {
        SceneManager.LoadScene("UreteroscopyDesktopTraining");
        yield return null;

        Component controller = GameObject.Find("Desktop Training Controller")?.GetComponent("UreteroscopyTrainingController");
        Component loader = GameObject.Find("Training Case Loader")?.GetComponent("VrCaseLoader");
        Assert.That(controller, Is.Not.Null);
        Assert.That(loader, Is.Not.Null);
        yield return WaitUntilCaseReady(loader);

        Type controllerType = controller.GetType();
        controllerType.GetMethod("BeginSession").Invoke(controller, null);
        yield return null;
        yield return null;
        Assert.That(controllerType.GetProperty("State").GetValue(controller).ToString(), Is.EqualTo("Running"));

        controllerType.GetMethod("RequestGiveUpConfirmation").Invoke(controller, null);
        FieldInfo confirmation = controllerType.GetField("showGiveUpConfirmation", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That((bool)confirmation.GetValue(controller), Is.True);
        float elapsedBefore = (float)controllerType.GetProperty("ElapsedSeconds").GetValue(controller);
        yield return null;
        yield return null;
        float elapsedDuringDialog = (float)controllerType.GetProperty("ElapsedSeconds").GetValue(controller);
        Assert.That(elapsedDuringDialog, Is.EqualTo(elapsedBefore).Within(0.0001f));

        controllerType.GetMethod("CancelGiveUpConfirmation").Invoke(controller, null);
        yield return null;
        Assert.That(controllerType.GetProperty("State").GetValue(controller).ToString(), Is.EqualTo("Running"));
        controllerType.GetMethod("RequestGiveUpConfirmation").Invoke(controller, null);
        controllerType.GetMethod("ConfirmGiveUp").Invoke(controller, null);
        Assert.That(controllerType.GetProperty("State").GetValue(controller).ToString(), Is.EqualTo("Finished"));
    }

    [UnityTest]
    public IEnumerator SphereCastsCanDetectBothFrontFacesAndInternalBackfaces()
    {
        GameObject wall = new GameObject("Backface Test Wall");
        wall.layer = 30;
        wall.transform.position = new Vector3(10000f, 0f, 0f);
        Mesh mesh = new Mesh
        {
            vertices = new[]
            {
                new Vector3(0f, -1f, -1f),
                new Vector3(0f, 1f, -1f),
                new Vector3(0f, 0f, 1f)
            },
            triangles = new[] { 0, 1, 2 }
        };
        MeshCollider collider = wall.AddComponent<MeshCollider>();
        collider.sharedMesh = mesh;
        Physics.SyncTransforms();

        bool previous = Physics.queriesHitBackfaces;
        try
        {
            Physics.queriesHitBackfaces = true;
            bool front = Physics.SphereCast(new Vector3(10001f, 0f, 0f), 0.05f, Vector3.left, out _, 2f, 1 << 30);
            bool back = Physics.SphereCast(new Vector3(9999f, 0f, 0f), 0.05f, Vector3.right, out _, 2f, 1 << 30);
            Assert.That(front, Is.True);
            Assert.That(back, Is.True);
        }
        finally
        {
            Physics.queriesHitBackfaces = previous;
            UnityEngine.Object.Destroy(wall);
            UnityEngine.Object.Destroy(mesh);
        }
        yield return null;
    }
}
