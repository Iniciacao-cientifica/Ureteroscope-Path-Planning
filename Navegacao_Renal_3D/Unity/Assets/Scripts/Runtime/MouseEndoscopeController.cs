using UnityEngine;

namespace NavegacaoRenal
{
    public sealed class MouseEndoscopeController : MonoBehaviour
    {
        [Header("Physical navigation (Unity world at 5x)")]
        [SerializeField] private float forwardSpeed = 0.10f;
        [SerializeField] private float tipRadius = 0.010f;
        [SerializeField] private float maximumSubstepDistance = 0.001f;
        [SerializeField] private float maximumRotationSubstepDegrees = 1f;
        [SerializeField] private float collisionSkin = 0.001f;
        [SerializeField] private float contactRearmRadius = 0.015f;
        [SerializeField] private LayerMask collisionMask;
        [SerializeField] private VirtualGripperController virtualGripper;

        [Header("Steering")]
        [SerializeField] private float mouseRateGain = 4f;
        [SerializeField] private float mouseSensitivityMultiplier = 1f;
        [SerializeField] private float maximumSteeringSpeed = 50f;
        [SerializeField] private float steeringSmoothTime = 0.18f;
        [SerializeField] private float rollSpeed = 55f;
        [SerializeField] private KidneyGameManager gameManager;
        [SerializeField] private MonoBehaviour inputSourceBehaviour;

        private Vector2 smoothedSteeringVelocity;
        private Vector2 steeringSmoothDampVelocity;
        private bool wallContactLatched;
        private IEndoscopeInputSource inputSource;
        private Quaternion hardwareBaseRotation = Quaternion.identity;
        private Vector3 lastSafePosition;
        private Quaternion lastSafeRotation = Quaternion.identity;
        private bool hasLastSafePose;

        private const string MouseSensitivityPreference = "NavegacaoRenal.MouseSensitivity";

        public float ForwardSpeed => forwardSpeed;
        public float TipRadius => tipRadius;
        public float MaximumSubstepDistance => maximumSubstepDistance;
        public float MaximumRotationSubstepDegrees => maximumRotationSubstepDegrees;
        public float CollisionSkin => collisionSkin;
        public float ContactRearmRadius => contactRearmRadius;
        public float MaximumSteeringSpeed => maximumSteeringSpeed;
        public float SteeringSmoothTime => steeringSmoothTime;
        public float RollSpeed => rollSpeed;
        public int CollisionMask => collisionMask.value;
        public bool IsWallContactLatched => wallContactLatched;
        public MonoBehaviour InputSourceBehaviour => inputSourceBehaviour;
        public float MouseSensitivityMultiplier => mouseSensitivityMultiplier;
        public VirtualGripperController VirtualGripper => virtualGripper;

        public void Configure(KidneyGameManager manager, LayerMask kidneyCollisionMask)
        {
            gameManager = manager;
            collisionMask = kidneyCollisionMask;
        }

        public void ConfigureGripper(VirtualGripperController gripper)
        {
            virtualGripper = gripper;
            RememberSafePoseIfClear();
        }

        public void ConfigureInputSource(MonoBehaviour source)
        {
            inputSourceBehaviour = source;
            inputSource = source as IEndoscopeInputSource;
        }

        public void ResetTo(Transform anchor)
        {
            if (anchor == null)
                return;

            transform.SetPositionAndRotation(anchor.position, anchor.rotation);
            Physics.SyncTransforms();
            hardwareBaseRotation = anchor.rotation;
            smoothedSteeringVelocity = Vector2.zero;
            steeringSmoothDampVelocity = Vector2.zero;
            wallContactLatched = false;
            hasLastSafePose = false;
            if (!RememberSafePoseIfClear())
                Debug.LogError("StartAnchor posiciona parte da camera/garra dentro de KidneyCollision.");
        }

        public static void ReleaseCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void Awake()
        {
            Physics.queriesHitBackfaces = true;
            mouseSensitivityMultiplier = Mathf.Clamp(
                PlayerPrefs.GetFloat(MouseSensitivityPreference, mouseSensitivityMultiplier), 0.5f, 2f);
            hardwareBaseRotation = transform.rotation;
            inputSource = inputSourceBehaviour as IEndoscopeInputSource;
            if (virtualGripper == null)
                virtualGripper = GetComponentInChildren<VirtualGripperController>(true);
            RememberSafePoseIfClear();
        }

        public void SetMouseSensitivity(float value)
        {
            mouseSensitivityMultiplier = Mathf.Clamp(value, 0.5f, 2f);
            PlayerPrefs.SetFloat(MouseSensitivityPreference, mouseSensitivityMultiplier);
        }

        private void OnDisable() => ReleaseCursor();

        private void Update()
        {
            if (inputSource == null)
                inputSource = inputSourceBehaviour as IEndoscopeInputSource;
            EndoscopeInputFrame input = inputSource != null ? inputSource.ReadFrame() : default;
            HandleCursor(input);

            if (gameManager == null || !gameManager.CanNavigate)
                return;

            float deltaTime = Time.deltaTime;
            ApplySteering(input, deltaTime);

            if (!Mathf.Approximately(input.Advance, 0f))
                TryMoveDistance(input.Advance * forwardSpeed * deltaTime);
            else
                UpdateContactLatch();
        }

        private void HandleCursor(EndoscopeInputFrame input)
        {
            if (input.CursorReleasePressed)
            {
                ReleaseCursor();
                return;
            }

            if (gameManager != null && gameManager.CanNavigate && input.CursorLockPressed)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private void ApplySteering(EndoscopeInputFrame input, float deltaTime)
        {
            if (input.SteeringMode == EndoscopeSteeringMode.RelativeOrientation)
            {
                Quaternion target = hardwareBaseRotation * input.RelativeOrientation;
                Quaternion candidate = AdvanceHardwareOrientation(transform.rotation, target, deltaTime,
                    steeringSmoothTime, maximumSteeringSpeed);
                TryRotateTo(candidate);
                smoothedSteeringVelocity = Vector2.zero;
                steeringSmoothDampVelocity = Vector2.zero;
                return;
            }

            Vector2 targetAngularVelocity = Vector2.zero;
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                targetAngularVelocity = new Vector2(-input.SteeringDelta.y, input.SteeringDelta.x) *
                                        mouseRateGain * mouseSensitivityMultiplier;
                targetAngularVelocity = Vector2.ClampMagnitude(targetAngularVelocity, maximumSteeringSpeed);
            }

            smoothedSteeringVelocity = Vector2.SmoothDamp(
                smoothedSteeringVelocity,
                targetAngularVelocity,
                ref steeringSmoothDampVelocity,
                steeringSmoothTime,
                maximumSteeringSpeed,
                deltaTime);

            Quaternion candidateRotation = transform.rotation * Quaternion.Euler(
                smoothedSteeringVelocity.x * deltaTime,
                smoothedSteeringVelocity.y * deltaTime,
                0f) * Quaternion.Euler(0f, 0f, input.Roll * rollSpeed * deltaTime);
            TryRotateTo(candidateRotation);
        }

        public static Quaternion AdvanceHardwareOrientation(Quaternion current, Quaternion target,
            float deltaTime, float smoothTime, float maximumDegreesPerSecond)
        {
            float blend = 1f - Mathf.Exp(-Mathf.Max(0f, deltaTime) / Mathf.Max(0.001f, smoothTime));
            Quaternion smoothedTarget = Quaternion.Slerp(current, target, blend);
            return Quaternion.RotateTowards(current, smoothedTarget,
                Mathf.Max(0f, maximumDegreesPerSecond) * Mathf.Max(0f, deltaTime));
        }

        public bool TryRotateTo(Quaternion targetRotation)
        {
            if (!RestoreSafePoseWhenOverlapping())
                return false;

            Quaternion startRotation = transform.rotation;
            float angle = Quaternion.Angle(startRotation, targetRotation);
            int steps = Mathf.Max(1, Mathf.CeilToInt(angle / Mathf.Max(0.1f, maximumRotationSubstepDegrees)));
            for (int index = 1; index <= steps; index++)
            {
                Quaternion candidate = Quaternion.Slerp(startRotation, targetRotation, index / (float)steps);
                if (IsPoseOverlapping(transform.position, candidate, 0f, out Vector3 contactPoint))
                {
                    RestoreLastSafePose();
                    RegisterWallContact(contactPoint);
                    UpdateContactLatch();
                    return false;
                }

                transform.rotation = candidate;
                RememberSafePose();
            }

            UpdateContactLatch();
            return true;
        }

        public bool TryMoveDistance(float signedDistance)
        {
            float remaining = Mathf.Abs(signedDistance);
            if (remaining <= Mathf.Epsilon)
            {
                RememberSafePoseIfClear();
                UpdateContactLatch();
                return true;
            }

            if (!RestoreSafePoseWhenOverlapping())
                return false;

            Vector3 direction = signedDistance >= 0f ? transform.forward : -transform.forward;
            bool completed = true;

            while (remaining > Mathf.Epsilon)
            {
                float step = Mathf.Min(remaining, maximumSubstepDistance);
                if (TryFindSweepHit(direction, step, out RaycastHit hit))
                {
                    float safeDistance = Mathf.Clamp(hit.distance, 0f, step);
                    if (safeDistance > Mathf.Epsilon)
                    {
                        Vector3 candidate = transform.position + direction * safeDistance;
                        if (!IsPoseOverlapping(candidate, transform.rotation, 0f, out _))
                        {
                            transform.position = candidate;
                            RememberSafePose();
                        }
                    }

                    RestoreLastSafePose();
                    RegisterWallContact(hit.point);
                    completed = false;
                    break;
                }

                Vector3 nextPosition = transform.position + direction * step;
                if (IsPoseOverlapping(nextPosition, transform.rotation, 0f, out Vector3 contactPoint))
                {
                    RestoreLastSafePose();
                    RegisterWallContact(contactPoint);
                    completed = false;
                    break;
                }

                transform.position = nextPosition;
                RememberSafePose();
                remaining -= step;
            }

            UpdateContactLatch();
            return completed;
        }

        public bool TrySetGripperClosure(float value)
        {
            if (virtualGripper == null)
                return false;

            float previous = virtualGripper.Closure;
            virtualGripper.SetClosure(value);
            Physics.SyncTransforms();
            if (!IsPoseOverlapping(transform.position, transform.rotation, 0f, out Vector3 contactPoint))
                return true;

            virtualGripper.SetClosure(previous);
            Physics.SyncTransforms();
            RegisterWallContact(contactPoint);
            return false;
        }

        public bool IsCurrentPoseClear() =>
            !IsPoseOverlapping(transform.position, transform.rotation, 0f, out _);

        public bool HasClearPathFrom(Vector3 origin, Vector3 targetPosition)
        {
            Vector3 offset = targetPosition - origin;
            float distance = offset.magnitude;
            const float clearanceRadius = 0.001f;
            if (Physics.CheckSphere(origin, clearanceRadius, collisionMask, QueryTriggerInteraction.Ignore))
                return false;
            if (distance <= Mathf.Epsilon)
                return true;

            return !Physics.SphereCast(origin, clearanceRadius, offset / distance, out _, distance,
                collisionMask, QueryTriggerInteraction.Ignore);
        }

        public bool HasClearPathTo(Vector3 targetPosition) => HasClearPathFrom(transform.position, targetPosition);

        private bool TryFindSweepHit(Vector3 direction, float distance, out RaycastHit nearestHit)
        {
            nearestHit = default;
            bool blocked = false;
            float nearestDistance = float.PositiveInfinity;

            if (Physics.SphereCast(transform.position, tipRadius + collisionSkin, direction,
                    out RaycastHit tipHit, distance, collisionMask, QueryTriggerInteraction.Ignore))
            {
                blocked = true;
                nearestDistance = tipHit.distance;
                nearestHit = tipHit;
            }

            if (virtualGripper != null && virtualGripper.TrySweep(transform, transform.position, transform.rotation,
                    direction, distance, collisionMask, collisionSkin, out RaycastHit gripperHit) &&
                gripperHit.distance < nearestDistance)
            {
                blocked = true;
                nearestHit = gripperHit;
            }

            return blocked;
        }

        private bool IsPoseOverlapping(Vector3 position, Quaternion rotation, float margin,
            out Vector3 contactPoint)
        {
            float radius = tipRadius + Mathf.Max(0f, margin);
            Collider[] tipOverlaps = Physics.OverlapSphere(position, radius, collisionMask,
                QueryTriggerInteraction.Ignore);
            if (tipOverlaps.Length > 0)
            {
                contactPoint = FindNearestPoint(tipOverlaps, position);
                return true;
            }

            if (virtualGripper != null && virtualGripper.IsPoseOverlapping(transform, position, rotation,
                    collisionMask, margin, out contactPoint))
                return true;

            contactPoint = position;
            return false;
        }

        private bool RestoreSafePoseWhenOverlapping()
        {
            if (!IsPoseOverlapping(transform.position, transform.rotation, 0f, out Vector3 contactPoint))
                return true;

            RestoreLastSafePose();
            RegisterWallContact(contactPoint);
            UpdateContactLatch();
            return false;
        }

        private bool RememberSafePoseIfClear()
        {
            if (IsPoseOverlapping(transform.position, transform.rotation, 0f, out _))
                return false;
            RememberSafePose();
            return true;
        }

        private void RememberSafePose()
        {
            lastSafePosition = transform.position;
            lastSafeRotation = transform.rotation;
            hasLastSafePose = true;
        }

        private void RestoreLastSafePose()
        {
            if (!hasLastSafePose)
                return;
            transform.SetPositionAndRotation(lastSafePosition, lastSafeRotation);
            Physics.SyncTransforms();
        }

        private static Vector3 FindNearestPoint(Collider[] overlaps, Vector3 position)
        {
            Vector3 nearest = overlaps[0].ClosestPoint(position);
            float nearestSquaredDistance = (nearest - position).sqrMagnitude;
            for (int index = 1; index < overlaps.Length; index++)
            {
                Vector3 candidate = overlaps[index].ClosestPoint(position);
                float squaredDistance = (candidate - position).sqrMagnitude;
                if (squaredDistance >= nearestSquaredDistance)
                    continue;
                nearest = candidate;
                nearestSquaredDistance = squaredDistance;
            }
            return nearest;
        }

        private void RegisterWallContact(Vector3 point)
        {
            if (wallContactLatched)
                return;
            wallContactLatched = true;
            gameManager?.ReportWallContact(point);
        }

        private void UpdateContactLatch()
        {
            if (!wallContactLatched)
                return;
            if (!IsPoseOverlapping(transform.position, transform.rotation, contactRearmRadius, out _))
                wallContactLatched = false;
        }
    }
}
