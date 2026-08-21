using System;
using UnityEngine;

public enum ExplorationNavigationMode
{
    FreeCamera,
    ProbeFollow
}

public sealed class ExternalExplorationCameraController : MonoBehaviour
{
    [Header("External overview")]
    [Range(0.05f, 0.5f)] public float overviewMargin = 0.2f;

    [Header("Free camera")]
    [Min(0.01f)] public float freeMovementSpeedMetersPerSecond = 0.18f;
    [Min(1f)] public float freeMovementBoostMultiplier = 3f;
    [Min(0.01f)] public float freeMovementSmoothingSeconds = 0.12f;
    [Range(0.1f, 6f)] public float freeLookSensitivity = 2f;

    [Header("External follow")]
    [Min(0.02f)] public float distanceBehindMeters = 0.16f;
    [Min(0f)] public float heightMeters = 0.09f;
    [Min(0f)] public float lateralMeters = 0.08f;
    [Min(0.01f)] public float lookAheadMeters = 0.035f;
    [Min(0.02f)] public float followSmoothingSeconds = 0.25f;
    [Min(0.05f)] public float navigationTransitionSeconds = 0.35f;

    [Header("External collision")]
    [Min(0.001f)] public float collisionRadiusMeters = 0.01f;
    [Min(0f)] public float collisionSurfaceMarginMeters = 0.003f;

    public bool IsActive { get; private set; }
    public bool IsOverview { get; private set; }
    public bool IsFollowing => IsActive && Mode == ExplorationNavigationMode.ProbeFollow;
    public ExplorationNavigationMode Mode { get; private set; } = ExplorationNavigationMode.FreeCamera;
    public event Action<ExplorationNavigationMode> NavigationModeChanged;

    private Camera controlledCamera;
    private Transform probe;
    private Bounds anatomyBounds;
    private bool hasAnatomyBounds;
    private float transitionElapsed;
    private Vector3 transitionStartPosition;
    private Quaternion transitionStartRotation;
    private Vector3 freeVelocity;
    private Vector3 freeVelocityDerivative;
    private float freeYaw;
    private float freePitch;
    private CursorLockMode savedCursorLockMode;
    private bool savedCursorVisible;
    private bool cursorStateCaptured;

    public void Configure(Camera camera, Transform probeTransform, Bounds bounds)
    {
        // The previous probe can already be queued for destruction while a case is
        // rebuilt. The training controller re-parents the camera to the new probe.
        IsActive = false;
        IsOverview = false;
        transitionElapsed = 0f;
        freeVelocity = Vector3.zero;
        freeVelocityDerivative = Vector3.zero;
        controlledCamera = camera;
        probe = probeTransform;
        anatomyBounds = bounds;
        hasAnatomyBounds = bounds.size.sqrMagnitude > 0.000001f;
        Mode = ExplorationNavigationMode.FreeCamera;
    }

    public void SetExplorationActive(bool active)
    {
        if (controlledCamera == null || probe == null)
        {
            IsActive = false;
            IsOverview = false;
            RestoreCursorState();
            return;
        }

        if (active && !IsActive)
        {
            savedCursorLockMode = Cursor.lockState;
            savedCursorVisible = Cursor.visible;
            cursorStateCaptured = true;
        }

        IsActive = active;
        transitionElapsed = 0f;
        freeVelocity = Vector3.zero;
        freeVelocityDerivative = Vector3.zero;
        if (!active)
        {
            IsOverview = false;
            Mode = ExplorationNavigationMode.FreeCamera;
            RestoreCursorState();
            controlledCamera.transform.SetParent(probe, false);
            controlledCamera.transform.localPosition = Vector3.zero;
            controlledCamera.transform.localRotation = Quaternion.identity;
            return;
        }

        controlledCamera.transform.SetParent(null, true);
        Mode = ExplorationNavigationMode.FreeCamera;
        ResetOverview();
        NavigationModeChanged?.Invoke(Mode);
    }

    public void SetNavigationMode(ExplorationNavigationMode mode)
    {
        if (!IsActive || controlledCamera == null || probe == null || Mode == mode) return;
        ReleaseMouseLook();
        Mode = mode;
        IsOverview = false;
        transitionElapsed = 0f;
        transitionStartPosition = controlledCamera.transform.position;
        transitionStartRotation = controlledCamera.transform.rotation;
        freeVelocity = Vector3.zero;
        freeVelocityDerivative = Vector3.zero;
        if (mode == ExplorationNavigationMode.FreeCamera) CaptureFreeLookAngles();
        NavigationModeChanged?.Invoke(mode);
    }

    public void ToggleNavigationMode()
    {
        SetNavigationMode(Mode == ExplorationNavigationMode.FreeCamera
            ? ExplorationNavigationMode.ProbeFollow
            : ExplorationNavigationMode.FreeCamera);
    }

    public void ResetOverview()
    {
        if (controlledCamera == null || !hasAnatomyBounds) return;
        if (Mode != ExplorationNavigationMode.FreeCamera)
        {
            Mode = ExplorationNavigationMode.FreeCamera;
            NavigationModeChanged?.Invoke(Mode);
        }
        Bounds bounds = anatomyBounds;
        float radius = Mathf.Max(0.08f, bounds.extents.magnitude);
        float halfFov = Mathf.Max(10f, controlledCamera.fieldOfView * 0.5f) * Mathf.Deg2Rad;
        float distance = radius / Mathf.Tan(halfFov) * (1f + overviewMargin);
        Vector3 direction = new Vector3(1f, 0.55f, -1f).normalized;
        controlledCamera.transform.position = bounds.center + direction * distance;
        controlledCamera.transform.rotation = Quaternion.LookRotation(bounds.center - controlledCamera.transform.position, Vector3.up);
        IsOverview = true;
        transitionElapsed = 0f;
        freeVelocity = Vector3.zero;
        freeVelocityDerivative = Vector3.zero;
        CaptureFreeLookAngles();
    }

    // Compatibility hook: movement no longer changes camera mode automatically.
    public void NotifyProbeAdvanced(float deltaMeters) { }

    private void Update()
    {
        if (!IsActive || controlledCamera == null) return;
        if (Input.GetKeyDown(KeyCode.Tab)) ToggleNavigationMode();
        if (Input.GetKeyDown(KeyCode.F)) ResetOverview();
        if (Mode == ExplorationNavigationMode.FreeCamera) TickFreeCameraInput();
    }

    private void TickFreeCameraInput()
    {
        bool looking = Input.GetMouseButton(1);
        if (looking)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else ReleaseMouseLook();

        Vector2 lookDelta = looking
            ? new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"))
            : Vector2.zero;
        Vector3 movement = new Vector3(Axis(KeyCode.D, KeyCode.A), Axis(KeyCode.E, KeyCode.Q), Axis(KeyCode.W, KeyCode.S));
        bool boosted = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        ApplyFreeCameraInput(movement, lookDelta, boosted, Time.unscaledDeltaTime);
    }

    public void ApplyFreeCameraInput(Vector3 movementInput, Vector2 lookDelta, bool boosted, float deltaTime)
    {
        if (!IsActive || Mode != ExplorationNavigationMode.FreeCamera || controlledCamera == null) return;
        float safeDelta = Mathf.Max(0f, deltaTime);
        if (lookDelta.sqrMagnitude > 0f)
        {
            freeYaw += lookDelta.x * freeLookSensitivity;
            freePitch = Mathf.Clamp(freePitch - lookDelta.y * freeLookSensitivity, -89f, 89f);
            controlledCamera.transform.rotation = Quaternion.Euler(freePitch, freeYaw, 0f);
            IsOverview = false;
        }

        Vector3 clampedInput = Vector3.ClampMagnitude(movementInput, 1f);
        Vector3 desiredDirection = controlledCamera.transform.right * clampedInput.x +
                                   Vector3.up * clampedInput.y + controlledCamera.transform.forward * clampedInput.z;
        if (desiredDirection.sqrMagnitude > 1f) desiredDirection.Normalize();
        float speed = freeMovementSpeedMetersPerSecond * (boosted ? freeMovementBoostMultiplier : 1f);
        Vector3 desiredVelocity = desiredDirection * speed;
        freeVelocity = Vector3.SmoothDamp(freeVelocity, desiredVelocity, ref freeVelocityDerivative,
            Mathf.Max(0.01f, freeMovementSmoothingSeconds), Mathf.Infinity, safeDelta);
        Vector3 displacement = freeVelocity * safeDelta;
        if (displacement.sqrMagnitude > 0.0000000001f)
        {
            controlledCamera.transform.position = ResolveMovementCollision(controlledCamera.transform.position, displacement);
            IsOverview = false;
        }
    }

    private void LateUpdate()
    {
        if (!IsFollowing || controlledCamera == null || probe == null) return;
        CalculateFollowPose(out Vector3 desiredPosition, out Quaternion desiredRotation);
        desiredPosition = ResolveFollowDestination(desiredPosition);

        Vector3 nextPosition;
        Quaternion nextRotation;
        if (transitionElapsed < navigationTransitionSeconds)
        {
            transitionElapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f,
                Mathf.Clamp01(transitionElapsed / Mathf.Max(0.05f, navigationTransitionSeconds)));
            nextPosition = Vector3.Lerp(transitionStartPosition, desiredPosition, t);
            nextRotation = Quaternion.Slerp(transitionStartRotation, desiredRotation, t);
        }
        else
        {
            float amount = 1f - Mathf.Exp(-Time.unscaledDeltaTime / Mathf.Max(0.02f, followSmoothingSeconds));
            nextPosition = Vector3.Lerp(controlledCamera.transform.position, desiredPosition, amount);
            nextRotation = Quaternion.Slerp(controlledCamera.transform.rotation, desiredRotation, amount);
        }

        controlledCamera.transform.position = ResolveMovementCollision(controlledCamera.transform.position,
            nextPosition - controlledCamera.transform.position);
        controlledCamera.transform.rotation = nextRotation;
    }

    private void CalculateFollowPose(out Vector3 position, out Quaternion rotation)
    {
        Vector3 forward = probe.forward.normalized;
        Vector3 up = Vector3.up;
        Vector3 side = Vector3.Cross(up, forward);
        if (side.sqrMagnitude < 0.0001f) side = probe.right;
        else side.Normalize();
        Vector3 focus = probe.position + forward * lookAheadMeters;
        position = probe.position - forward * distanceBehindMeters + up * heightMeters + side * lateralMeters;
        rotation = Quaternion.LookRotation(focus - position, up);
    }

    private Vector3 ResolveFollowDestination(Vector3 desiredPosition)
    {
        Vector3 offset = desiredPosition - probe.position;
        float distance = offset.magnitude;
        if (distance < 0.001f) return desiredPosition;
        bool previousBackfaces = Physics.queriesHitBackfaces;
        try
        {
            Physics.queriesHitBackfaces = true;
            if (Physics.SphereCast(probe.position, collisionRadiusMeters, offset / distance, out RaycastHit hit,
                    distance, 1 << KidneyVisualPresenter.ExternalAnatomyLayer, QueryTriggerInteraction.Ignore))
            {
                float outsideDistance = hit.distance + collisionRadiusMeters + collisionSurfaceMarginMeters;
                if (outsideDistance <= distance) return probe.position + offset.normalized * outsideDistance;
            }
        }
        finally { Physics.queriesHitBackfaces = previousBackfaces; }
        return desiredPosition;
    }

    private Vector3 ResolveMovementCollision(Vector3 origin, Vector3 displacement)
    {
        float distance = displacement.magnitude;
        if (distance < 0.000001f) return origin;
        bool previousBackfaces = Physics.queriesHitBackfaces;
        try
        {
            Physics.queriesHitBackfaces = true;
            if (Physics.SphereCast(origin, collisionRadiusMeters, displacement / distance, out RaycastHit hit,
                    distance + collisionSurfaceMarginMeters,
                    1 << KidneyVisualPresenter.ExternalAnatomyLayer, QueryTriggerInteraction.Ignore))
            {
                float permitted = Mathf.Clamp(hit.distance - collisionSurfaceMarginMeters, 0f, distance);
                return origin + displacement.normalized * permitted;
            }
        }
        finally { Physics.queriesHitBackfaces = previousBackfaces; }
        return origin + displacement;
    }

    private void CaptureFreeLookAngles()
    {
        if (controlledCamera == null) return;
        Vector3 angles = controlledCamera.transform.eulerAngles;
        freeYaw = angles.y;
        freePitch = NormalizeAngle(angles.x);
    }

    private void ReleaseMouseLook()
    {
        if (!cursorStateCaptured) return;
        Cursor.lockState = savedCursorLockMode;
        Cursor.visible = savedCursorVisible;
    }

    private void RestoreCursorState()
    {
        ReleaseMouseLook();
        cursorStateCaptured = false;
    }

    private static float Axis(KeyCode positive, KeyCode negative)
    {
        return (Input.GetKey(positive) ? 1f : 0f) - (Input.GetKey(negative) ? 1f : 0f);
    }

    private static float NormalizeAngle(float angle) => angle > 180f ? angle - 360f : angle;

    private void OnDisable() => RestoreCursorState();
}
