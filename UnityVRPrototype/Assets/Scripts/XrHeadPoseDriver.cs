using UnityEngine;
using UnityEngine.XR;

public class XrHeadPoseDriver : MonoBehaviour
{
    public XRNode trackedNode = XRNode.CenterEye;
    public bool recenterOnStart = true;

    private void Start()
    {
        if (recenterOnStart && XRSettings.enabled)
        {
#pragma warning disable 0618
            InputTracking.Recenter();
#pragma warning restore 0618
        }
    }

    private void LateUpdate()
    {
        if (!XRSettings.enabled)
        {
            return;
        }

#pragma warning disable 0618
        transform.localPosition = InputTracking.GetLocalPosition(trackedNode);
        transform.localRotation = InputTracking.GetLocalRotation(trackedNode);
#pragma warning restore 0618
    }
}
