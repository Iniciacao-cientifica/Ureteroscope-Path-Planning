using UnityEngine;
using UnityEngine.InputSystem;

namespace NavegacaoRenal
{
    public sealed class FreeFlyCameraController : MonoBehaviour
    {
        [SerializeField] private float movementSpeed = 1.5f;
        [SerializeField] private float boostMultiplier = 2.5f;
        [SerializeField] private float lookSensitivity = 0.09f;

        private float pitch;
        private float yaw;

        private void OnEnable()
        {
            Vector3 angles = transform.eulerAngles;
            pitch = angles.x;
            yaw = angles.y;
        }

        private void Update()
        {
            if (Keyboard.current == null)
                return;

            bool looking = Mouse.current != null && Mouse.current.rightButton.isPressed;
            if (looking)
            {
                Vector2 delta = Mouse.current.delta.ReadValue();
                yaw += delta.x * lookSensitivity;
                pitch = Mathf.Clamp(pitch - delta.y * lookSensitivity, -89f, 89f);
                transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
            }

            Vector3 input = Vector3.zero;
            if (Keyboard.current.wKey.isPressed) input += Vector3.forward;
            if (Keyboard.current.sKey.isPressed) input += Vector3.back;
            if (Keyboard.current.aKey.isPressed) input += Vector3.left;
            if (Keyboard.current.dKey.isPressed) input += Vector3.right;
            if (Keyboard.current.qKey.isPressed) input += Vector3.down;
            if (Keyboard.current.eKey.isPressed) input += Vector3.up;

            float boost = Keyboard.current.leftShiftKey.isPressed ? boostMultiplier : 1f;
            transform.Translate(input.normalized * (movementSpeed * boost * Time.unscaledDeltaTime), Space.Self);
        }
    }
}
