using UnityEngine;
using UnityEngine.InputSystem;

namespace NavegacaoRenal
{
    public sealed class MouseKeyboardInputSource : MonoBehaviour, IEndoscopeInputSource
    {
        public EndoscopeInputFrame ReadFrame()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;

            float advance = 0f;
            float roll = 0f;
            if (keyboard != null)
            {
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) advance += 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) advance -= 1f;
                if (keyboard.qKey.isPressed) roll += 1f;
                if (keyboard.eKey.isPressed) roll -= 1f;
            }

            return new EndoscopeInputFrame(
                mouse != null ? mouse.delta.ReadValue() : Vector2.zero,
                advance,
                roll,
                keyboard != null && keyboard.spaceKey.isPressed,
                keyboard != null && keyboard.pKey.wasPressedThisFrame,
                keyboard != null && keyboard.rKey.wasPressedThisFrame,
                keyboard != null && keyboard.tKey.wasPressedThisFrame,
                keyboard != null && keyboard.mKey.wasPressedThisFrame,
                mouse != null && mouse.leftButton.wasPressedThisFrame,
                keyboard != null && keyboard.escapeKey.wasPressedThisFrame);
        }
    }
}
