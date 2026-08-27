using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace NavegacaoRenal
{
    public sealed class FreeFlyCameraController : MonoBehaviour
    {
        [SerializeField] private float movementSpeed = 0.45f;
        [SerializeField] private float boostMultiplier = 3f;
        [SerializeField] private float lookSensitivity = 0.09f;
        [SerializeField] private float recenterDuration = 0.45f;
        [SerializeField] private Transform homeAnchor;

        private float pitch;
        private float yaw;
        private bool recentering;
        private float recenterElapsed;
        private Vector3 recenterStartPosition;
        private Quaternion recenterStartRotation;

        public float MovementSpeed => movementSpeed;
        public float BoostMultiplier => boostMultiplier;
        public float RecenterDuration => recenterDuration;
        public Transform HomeAnchor => homeAnchor;
        public bool IsRecentering => recentering;
        public bool IsCursorLocked => Cursor.lockState == CursorLockMode.Locked;

        public void Configure(Transform anchor, float speed = 0.45f, float boost = 3f, float duration = 0.45f)
        {
            homeAnchor = anchor;
            movementSpeed = speed;
            boostMultiplier = boost;
            recenterDuration = duration;
        }

        private void OnEnable()
        {
            ReleaseCursor();
            ResetViewImmediate();
        }

        private void OnDisable() => ReleaseCursor();

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            if (keyboard == null)
                return;

            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                ReleaseCursor();
                recentering = false;
                return;
            }

            if (keyboard.fKey.wasPressedThisFrame)
                BeginRecenter();

            if (!IsCursorLocked && mouse != null && mouse.leftButton.wasPressedThisFrame && !PointerIsOverUi())
                LockCursor();

            Vector3 input = ReadMovement(keyboard);
            Vector2 lookDelta = IsCursorLocked && mouse != null ? mouse.delta.ReadValue() : Vector2.zero;
            bool hasManualInput = IsCursorLocked && (input.sqrMagnitude > 0f || lookDelta.sqrMagnitude > 0f);
            if (hasManualInput)
                recentering = false;

            if (recentering)
            {
                AdvanceRecenter(Time.unscaledDeltaTime);
                return;
            }

            if (!IsCursorLocked)
                return;

            ApplyLook(lookDelta);
            bool boost = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
            ApplyMovement(input, boost, Time.unscaledDeltaTime);
        }

        public void ResetViewImmediate()
        {
            recentering = false;
            if (homeAnchor != null)
                transform.SetPositionAndRotation(homeAnchor.position, homeAnchor.rotation);
            SyncAngles();
        }

        public void BeginRecenter()
        {
            if (homeAnchor == null)
                return;
            recenterElapsed = 0f;
            recenterStartPosition = transform.position;
            recenterStartRotation = transform.rotation;
            recentering = true;
        }

        public void AdvanceRecenter(float deltaTime)
        {
            if (!recentering || homeAnchor == null)
                return;
            recenterElapsed += Mathf.Max(0f, deltaTime);
            float t = Mathf.Clamp01(recenterElapsed / Mathf.Max(0.01f, recenterDuration));
            float smooth = t * t * (3f - 2f * t);
            transform.position = Vector3.LerpUnclamped(recenterStartPosition, homeAnchor.position, smooth);
            transform.rotation = Quaternion.SlerpUnclamped(recenterStartRotation, homeAnchor.rotation, smooth);
            if (t >= 1f)
            {
                recentering = false;
                SyncAngles();
            }
        }

        public Vector3 CalculateLocalDisplacement(Vector3 input, bool boost, float deltaTime)
        {
            if (input.sqrMagnitude > 1f)
                input.Normalize();
            float multiplier = boost ? boostMultiplier : 1f;
            return input * (movementSpeed * multiplier * Mathf.Max(0f, deltaTime));
        }

        public static float SimulateTravelDistance(float speed, float duration, int framesPerSecond)
        {
            if (framesPerSecond <= 0 || duration <= 0f)
                return 0f;
            int steps = Mathf.RoundToInt(duration * framesPerSecond);
            float step = 1f / framesPerSecond;
            float distance = 0f;
            for (int index = 0; index < steps; index++)
                distance += speed * step;
            return distance;
        }

        private void ApplyLook(Vector2 delta)
        {
            yaw += delta.x * lookSensitivity;
            pitch = Mathf.Clamp(pitch - delta.y * lookSensitivity, -89f, 89f);
            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        private void ApplyMovement(Vector3 input, bool boost, float deltaTime)
        {
            transform.Translate(CalculateLocalDisplacement(input, boost, deltaTime), Space.Self);
        }

        private static Vector3 ReadMovement(Keyboard keyboard)
        {
            Vector3 input = Vector3.zero;
            if (keyboard.wKey.isPressed) input += Vector3.forward;
            if (keyboard.sKey.isPressed) input += Vector3.back;
            if (keyboard.aKey.isPressed) input += Vector3.left;
            if (keyboard.dKey.isPressed) input += Vector3.right;
            if (keyboard.qKey.isPressed) input += Vector3.down;
            if (keyboard.eKey.isPressed) input += Vector3.up;
            return input;
        }

        private void SyncAngles()
        {
            Vector3 angles = transform.eulerAngles;
            pitch = NormalizeAngle(angles.x);
            yaw = NormalizeAngle(angles.y);
        }

        private static float NormalizeAngle(float angle) => angle > 180f ? angle - 360f : angle;

        private static bool PointerIsOverUi() => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        private static void LockCursor()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public static void ReleaseCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
