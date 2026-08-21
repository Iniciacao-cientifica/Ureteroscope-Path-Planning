using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class TrainingPlayModeTests
{
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

        PropertyInfo isReady = loader.GetType().GetProperty("IsReady");
        float timeout = Time.realtimeSinceStartup + 10f;
        while (!(bool)isReady.GetValue(loader) && Time.realtimeSinceStartup < timeout)
        {
            yield return null;
        }
        Assert.That((bool)isReady.GetValue(loader), Is.True, "The training case did not become ready.");

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
}
