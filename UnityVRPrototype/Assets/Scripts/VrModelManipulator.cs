using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

public class VrModelManipulator : MonoBehaviour
{
    public Transform target;
    public Transform trackingOrigin;
    public float minimumScale = 0.2f;
    public float maximumScale = 4f;

    private readonly List<InputDevice> devices = new List<InputDevice>();
    private bool manipulating;
    private bool twoHanded;
    private Vector3 initialTargetPosition;
    private Quaternion initialTargetRotation;
    private Vector3 initialTargetScale;
    private Vector3 initialControllerPosition;
    private Quaternion initialControllerRotation;
    private Vector3 initialMidpoint;
    private float initialDistance;
    private Vector3 resetPosition;
    private Quaternion resetRotation;
    private Vector3 resetScale = Vector3.one;

    public void CaptureResetPose()
    {
        if (target == null)
        {
            return;
        }
        resetPosition = target.position;
        resetRotation = target.rotation;
        resetScale = target.localScale;
    }

    public void ResetPose()
    {
        if (target == null)
        {
            return;
        }
        target.position = resetPosition;
        target.rotation = resetRotation;
        target.localScale = resetScale;
    }

    private void Update()
    {
        if (target == null)
        {
            return;
        }

        bool hasLeft = TryReadHand(XRNode.LeftHand, out Vector3 leftPosition, out Quaternion leftRotation, out bool leftGrip);
        bool hasRight = TryReadHand(XRNode.RightHand, out Vector3 rightPosition, out Quaternion rightRotation, out bool rightGrip);
        bool oneGrip = (hasLeft && leftGrip) ^ (hasRight && rightGrip);
        bool bothGrips = hasLeft && hasRight && leftGrip && rightGrip;

        if (!oneGrip && !bothGrips)
        {
            manipulating = false;
            twoHanded = false;
            return;
        }

        if (bothGrips)
        {
            Vector3 midpoint = (leftPosition + rightPosition) * 0.5f;
            float distance = Vector3.Distance(leftPosition, rightPosition);
            if (!manipulating || !twoHanded)
            {
                BeginTwoHanded(midpoint, distance);
                return;
            }
            float scaleRatio = initialDistance > 0.001f ? distance / initialDistance : 1f;
            float referenceScale = Mathf.Max(0.0001f, resetScale.x);
            float desired = Mathf.Clamp(initialTargetScale.x * scaleRatio, referenceScale * minimumScale, referenceScale * maximumScale);
            target.localScale = Vector3.one * desired;
            target.position = initialTargetPosition + (midpoint - initialMidpoint);
            return;
        }

        Vector3 controllerPosition = leftGrip ? leftPosition : rightPosition;
        Quaternion controllerRotation = leftGrip ? leftRotation : rightRotation;
        if (!manipulating || twoHanded)
        {
            BeginOneHanded(controllerPosition, controllerRotation);
            return;
        }
        Quaternion rotationDelta = controllerRotation * Quaternion.Inverse(initialControllerRotation);
        target.rotation = rotationDelta * initialTargetRotation;
        target.position = controllerPosition + rotationDelta * (initialTargetPosition - initialControllerPosition);
    }

    private void BeginOneHanded(Vector3 controllerPosition, Quaternion controllerRotation)
    {
        manipulating = true;
        twoHanded = false;
        initialTargetPosition = target.position;
        initialTargetRotation = target.rotation;
        initialTargetScale = target.localScale;
        initialControllerPosition = controllerPosition;
        initialControllerRotation = controllerRotation;
    }

    private void BeginTwoHanded(Vector3 midpoint, float distance)
    {
        manipulating = true;
        twoHanded = true;
        initialTargetPosition = target.position;
        initialTargetRotation = target.rotation;
        initialTargetScale = target.localScale;
        initialMidpoint = midpoint;
        initialDistance = Mathf.Max(0.001f, distance);
    }

    private bool TryReadHand(XRNode node, out Vector3 worldPosition, out Quaternion worldRotation, out bool grip)
    {
        worldPosition = Vector3.zero;
        worldRotation = Quaternion.identity;
        grip = false;
        devices.Clear();
        InputDevices.GetDevicesAtXRNode(node, devices);
        if (devices.Count == 0 || !devices[0].isValid)
        {
            return false;
        }
        if (!devices[0].TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 localPosition) ||
            !devices[0].TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion localRotation))
        {
            return false;
        }
        devices[0].TryGetFeatureValue(CommonUsages.gripButton, out grip);
        Transform origin = trackingOrigin != null ? trackingOrigin : transform;
        worldPosition = origin.TransformPoint(localPosition);
        worldRotation = origin.rotation * localRotation;
        return true;
    }
}
