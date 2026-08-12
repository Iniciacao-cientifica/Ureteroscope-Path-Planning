using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

public class XrHeadPoseDriver : MonoBehaviour
{
    public XRNode trackedNode = XRNode.CenterEye;
    public bool recenterOnStart;
    private readonly List<InputDevice> devices = new List<InputDevice>();

    private void Start()
    {
        if (!recenterOnStart)
        {
            return;
        }
        List<XRInputSubsystem> subsystems = new List<XRInputSubsystem>();
        SubsystemManager.GetSubsystems(subsystems);
        foreach (XRInputSubsystem subsystem in subsystems)
        {
            if (subsystem.running)
            {
                subsystem.TryRecenter();
            }
        }
    }

    private void LateUpdate()
    {
        devices.Clear();
        InputDevices.GetDevicesAtXRNode(trackedNode, devices);
        if (devices.Count == 0 || !devices[0].isValid)
        {
            return;
        }
        if (devices[0].TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 position))
        {
            transform.localPosition = position;
        }
        if (devices[0].TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion rotation))
        {
            transform.localRotation = rotation;
        }
    }
}
