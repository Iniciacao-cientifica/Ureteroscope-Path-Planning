using UnityEngine;
using UnityEngine.InputSystem;

namespace NavegacaoRenal
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class MouseEndoscopeController : MonoBehaviour
    {
        [SerializeField] private float forwardSpeed = 0.10f;
        [SerializeField] private float mouseSensitivity = 0.08f;
        [SerializeField] private float rollSpeed = 55f;
        [SerializeField] private KidneyGameManager gameManager;

        private CharacterController characterController;
        private float lastCollisionTime = -10f;

        public void Configure(KidneyGameManager manager)
        {
            gameManager = manager;
        }

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
        }

        private void Update()
        {
            if (gameManager == null || !gameManager.CanNavigate || Keyboard.current == null)
                return;

            Vector2 mouseDelta = Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
            transform.Rotate(-mouseDelta.y * mouseSensitivity, mouseDelta.x * mouseSensitivity, 0f, Space.Self);

            float roll = 0f;
            if (Keyboard.current.qKey.isPressed) roll += 1f;
            if (Keyboard.current.eKey.isPressed) roll -= 1f;
            transform.Rotate(0f, 0f, roll * rollSpeed * Time.deltaTime, Space.Self);

            float direction = 0f;
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) direction += 1f;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) direction -= 1f;

            characterController.Move(transform.forward * (direction * forwardSpeed * Time.deltaTime));
        }

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (Time.unscaledTime - lastCollisionTime < 0.35f)
                return;

            lastCollisionTime = Time.unscaledTime;
            gameManager?.ReportWallContact(hit.point);
        }
    }
}
