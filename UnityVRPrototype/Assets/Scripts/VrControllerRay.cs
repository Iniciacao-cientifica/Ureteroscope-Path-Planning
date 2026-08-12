using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

[RequireComponent(typeof(LineRenderer))]
public class VrControllerRay : MonoBehaviour
{
    public XRNode controllerNode = XRNode.RightHand;
    public float maximumDistance = 4f;
    public Color idleColor = new Color(0.25f, 0.7f, 1f, 0.65f);
    public Color hoverColor = new Color(1f, 0.75f, 0.1f, 1f);

    private readonly List<InputDevice> devices = new List<InputDevice>();
    private LineRenderer line;
    private bool previousTrigger;

    private void Awake()
    {
        line = GetComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.startWidth = 0.0025f;
        line.endWidth = 0.001f;
        line.material = new Material(Shader.Find("Sprites/Default"));
    }

    private void Update()
    {
        Ray ray = new Ray(transform.position, transform.forward);
        bool hitSomething = Physics.Raycast(ray, out RaycastHit hit, maximumDistance);
        float distance = hitSomething ? hit.distance : maximumDistance;
        line.SetPosition(0, ray.origin);
        line.SetPosition(1, ray.GetPoint(distance));
        VrMenuButton button = hitSomething ? hit.collider.GetComponent<VrMenuButton>() : null;
        line.startColor = line.endColor = button != null ? hoverColor : idleColor;

        bool trigger = ReadTrigger();
        if (trigger && !previousTrigger && button != null)
        {
            button.Activate();
        }
        previousTrigger = trigger;
    }

    private bool ReadTrigger()
    {
        devices.Clear();
        InputDevices.GetDevicesAtXRNode(controllerNode, devices);
        if (devices.Count == 0 || !devices[0].isValid)
        {
            return false;
        }
        return devices[0].TryGetFeatureValue(CommonUsages.triggerButton, out bool pressed) && pressed;
    }
}
