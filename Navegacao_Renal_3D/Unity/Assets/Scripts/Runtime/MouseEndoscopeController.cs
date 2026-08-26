using UnityEngine;
using UnityEngine.InputSystem;

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

        [Header("Mouse steering")]
        [SerializeField] private float mouseRateGain = 4f;
        [SerializeField] private float maximumSteeringSpeed = 70f;
        [SerializeField] private float steeringSmoothTime = 0.12f;
        [SerializeField] private float rollSpeed = 55f;
        [SerializeField] private KidneyGameManager gameManager;

        private Vector2 smoothedSteeringVelocity;
        private Vector2 steeringSmoothDampVelocity;
        private bool wallContactLatched;

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

        public void Configure(KidneyGameManager manager, LayerMask kidneyCollisionMask)
        {
            gameManager = manager;
            collisionMask = kidneyCollisionMask;
        }

        public void ResetTo(Transform anchor)
        {
            if (anchor == null)
                return;

            transform.SetPositionAndRotation(anchor.position, anchor.rotation);
            smoothedSteeringVelocity = Vector2.zero;
            steeringSmoothDampVelocity = Vector2.zero;
            wallContactLatched = false;
        }

        public static void ReleaseCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void Awake()
        {
            Physics.queriesHitBackfaces = true;
        }

        private void OnDisable()
        {
            ReleaseCursor();
        }

        private void Update()
        {
            HandleCursor();

            if (gameManager == null || !gameManager.CanNavigate || Keyboard.current == null)
                return;

            float deltaTime = Time.deltaTime;
            ApplySteering(deltaTime);

            float movementDirection = 0f;
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) movementDirection += 1f;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) movementDirection -= 1f;

            if (!Mathf.Approximately(movementDirection, 0f))
                TryMoveDistance(movementDirection * forwardSpeed * deltaTime);
            else
                UpdateContactLatch();
        }

        private void HandleCursor()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                ReleaseCursor();
                return;
            }

            if (gameManager != null && gameManager.CanNavigate && Mouse.current != null &&
                Mouse.current.leftButton.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private void ApplySteering(float deltaTime)
        {
            Vector2 targetAngularVelocity = Vector2.zero;
            if (Cursor.lockState == CursorLockMode.Locked && Mouse.current != null)
            {
                Vector2 mouseDelta = Mouse.current.delta.ReadValue();
                targetAngularVelocity = new Vector2(-mouseDelta.y, mouseDelta.x) * mouseRateGain;
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

            float roll = 0f;
            if (Keyboard.current.qKey.isPressed) roll += 1f;
            if (Keyboard.current.eKey.isPressed) roll -= 1f;
            transform.Rotate(0f, 0f, roll * rollSpeed * deltaTime, Space.Self);
        }

        public bool TryMoveDistance(float signedDistance)
        {
            float remaining = Mathf.Abs(signedDistance);
            if (remaining <= Mathf.Epsilon)
            {
                UpdateContactLatch();
                return true;
            }

            Vector3 direction = signedDistance >= 0f ? transform.forward : -transform.forward;
            bool completed = true;

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

                    RegisterWallContact(hit.point);
                    completed = false;
                    break;
                }

                transform.position += direction * step;
                remaining -= step;
            }

            UpdateContactLatch();
            return completed;
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
