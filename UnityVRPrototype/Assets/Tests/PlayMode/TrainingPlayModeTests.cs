using System.Collections;
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
}
