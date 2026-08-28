using UnityEngine;

namespace NavegacaoRenal
{
    public sealed class MouseEndoscopeController : MonoBehaviour
    {
        [Header("Physical navigation (Unity world at 5x)")]
        [SerializeField] private float forwardSpeed = 0.10f;
        [SerializeField] private float tipRadius = 0.010f;
        [SerializeField] private float maximumSubstepDistance = 0.005f;
        [SerializeField] private float collisionSkin = 0.001f;
        [SerializeField] private float contactRearmRadius = 0.015f;
        [SerializeField] private LayerMask collisionMask;

        [Header("Steering")]
        [SerializeField] private float mouseRateGain = 4f;
        [SerializeField] private float mouseSensitivityMultiplier = 1f;
        [SerializeField] private float maximumSteeringSpeed = 70f;
        [SerializeField] private float steeringSmoothTime = 0.12f;
        [SerializeField] private float rollSpeed = 55f;
        [SerializeField] private KidneyGameManager gameManager;
        [SerializeField] private MonoBehaviour inputSourceBehaviour;

        private Vector2 smoothedSteeringVelocity;
        private Vector2 steeringSmoothDampVelocity;
        private bool wallContactLatched;
        private IEndoscopeInputSource inputSource;
        private Quaternion hardwareBaseRotation = Quaternion.identity;
        private Vector3 lastSafePosition;
        private bool hasLastSafePosition;

        private const string MouseSensitivityPreference = "NavegacaoRenal.MouseSensitivity";

        public float ForwardSpeed => forwardSpeed;
        public float TipRadius => tipRadius;
        public float MaximumSubstepDistance => maximumSubstepDistance;
        public float CollisionSkin => collisionSkin;
        public float ContactRearmRadius => contactRearmRadius;
        public float MaximumSteeringSpeed => maximumSteeringSpeed;
        public float SteeringSmoothTime => steeringSmoothTime;
        public float RollSpeed => rollSpeed;
        public int CollisionMask => collisionMask.value;
        public bool IsWallContactLatched => wallContactLatched;
        public MonoBehaviour InputSourceBehaviour => inputSourceBehaviour;
        public float MouseSensitivityMultiplier => mouseSensitivityMultiplier;

        public void Configure(KidneyGameManager manager, LayerMask kidneyCollisionMask)
        {
            gameManager = manager;
            collisionMask = kidneyCollisionMask;
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
            hardwareBaseRotation = anchor.rotation;
            smoothedSteeringVelocity = Vector2.zero;
            steeringSmoothDampVelocity = Vector2.zero;
            wallContactLatched = false;
            RememberSafePosition();
        }

        public static void ReleaseCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void Awake()
        {
            Physics.queriesHitBackfaces = true;
            mouseSensitivityMultiplier = Mathf.Clamp(PlayerPrefs.GetFloat(MouseSensitivityPreference, mouseSensitivityMultiplier), 0.5f, 2f);
            hardwareBaseRotation = transform.rotation;
            inputSource = inputSourceBehaviour as IEndoscopeInputSource;
            RememberSafePosition();
        }

        public void SetMouseSensitivity(float value)
        {
            mouseSensitivityMultiplier = Mathf.Clamp(value, 0.5f, 2f);
            PlayerPrefs.SetFloat(MouseSensitivityPreference, mouseSensitivityMultiplier);
        }

        private void OnDisable()
        {
            ReleaseCursor();
        }

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
                transform.rotation = AdvanceHardwareOrientation(transform.rotation, target, deltaTime,
                    steeringSmoothTime, maximumSteeringSpeed);
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

            transform.Rotate(
                smoothedSteeringVelocity.x * deltaTime,
                smoothedSteeringVelocity.y * deltaTime,
                0f,
                Space.Self);

            transform.Rotate(0f, 0f, input.Roll * rollSpeed * deltaTime, Space.Self);
        }

        public static Quaternion AdvanceHardwareOrientation(Quaternion current, Quaternion target,
            float deltaTime, float smoothTime, float maximumDegreesPerSecond)
        {
            float blend = 1f - Mathf.Exp(-Mathf.Max(0f, deltaTime) / Mathf.Max(0.001f, smoothTime));
            Quaternion smoothedTarget = Quaternion.Slerp(current, target, blend);
            return Quaternion.RotateTowards(current, smoothedTarget,
                Mathf.Max(0f, maximumDegreesPerSecond) * Mathf.Max(0f, deltaTime));
        }

        public bool TryMoveDistance(float signedDistance)
        {
            float remaining = Mathf.Abs(signedDistance);
            if (remaining <= Mathf.Epsilon)
            {
                if (!IsTipOverlappingWall(transform.position))
                    RememberSafePosition();
                UpdateContactLatch();
                return true;
            }

            Vector3 direction = signedDistance >= 0f ? transform.forward : -transform.forward;
            bool completed = true;

            if (IsTipOverlappingWall(transform.position))
            {
                if (hasLastSafePosition && !IsTipOverlappingWall(lastSafePosition))
                    transform.position = lastSafePosition;
                RegisterWallContact(transform.position);
                UpdateContactLatch();
                return false;
            }

            while (remaining > Mathf.Epsilon)
            {
                float step = Mathf.Min(remaining, maximumSubstepDistance);
                if (Physics.SphereCast(
                    transform.position,
                    tipRadius,
                    direction,
                    out RaycastHit hit,
                    step + collisionSkin,
                    collisionMask,
                    QueryTriggerInteraction.Ignore))
                {
                    float safeDistance = Mathf.Clamp(hit.distance - collisionSkin, 0f, step);
                    if (safeDistance > Mathf.Epsilon)
                        transform.position += direction * safeDistance;

                    if (!IsTipOverlappingWall(transform.position))
                        RememberSafePosition();

                    RegisterWallContact(hit.point);
                    completed = false;
                    break;
                }

                Vector3 candidate = transform.position + direction * step;
                if (IsTipOverlappingWall(candidate))
                {
                    RegisterWallContact(FindNearestWallPoint(candidate));
                    completed = false;
                    break;
                }

                transform.position = candidate;
                RememberSafePosition();
                remaining -= step;
            }

            UpdateContactLatch();
            return completed;
        }

        public bool HasClearPathTo(Vector3 targetPosition)
        {
            Vector3 offset = targetPosition - transform.position;
            float distance = offset.magnitude;
            if (distance <= Mathf.Epsilon)
                return !IsTipOverlappingWall(transform.position);

            float clearanceRadius = Mathf.Max(0.001f, tipRadius * 0.25f);
            if (Physics.CheckSphere(transform.position, clearanceRadius, collisionMask,
                    QueryTriggerInteraction.Ignore))
                return false;

            return !Physics.SphereCast(
                transform.position,
                clearanceRadius,
                offset / distance,
                out _,
                distance,
                collisionMask,
                QueryTriggerInteraction.Ignore);
        }

        private bool IsTipOverlappingWall(Vector3 position) => Physics.CheckSphere(
            position,
            tipRadius,
            collisionMask,
            QueryTriggerInteraction.Ignore);

        private Vector3 FindNearestWallPoint(Vector3 position)
        {
            Collider[] overlaps = Physics.OverlapSphere(
                position,
                tipRadius,
                collisionMask,
                QueryTriggerInteraction.Ignore);
            if (overlaps.Length == 0)
                return position;

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

        private void RememberSafePosition()
        {
            lastSafePosition = transform.position;
            hasLastSafePosition = true;
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

            bool wallStillNear = Physics.CheckSphere(
                transform.position,
                contactRearmRadius,
                collisionMask,
                QueryTriggerInteraction.Ignore);
            if (!wallStillNear)
                wallContactLatched = false;
        }
    }
}
