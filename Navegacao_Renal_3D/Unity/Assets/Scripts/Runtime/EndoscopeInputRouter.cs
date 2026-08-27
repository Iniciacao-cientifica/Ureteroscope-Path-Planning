using UnityEngine;

namespace NavegacaoRenal
{
    public sealed class EndoscopeInputRouter : MonoBehaviour, IEndoscopeInputSource
    {
        [SerializeField] private MouseKeyboardInputSource mouseKeyboard;
        [SerializeField] private Esp32MpuInputSource esp32Mpu;
        [SerializeField] private EndoscopeControlMode controlMode = EndoscopeControlMode.MouseKeyboard;

        public EndoscopeControlMode ControlMode => controlMode;
        public MouseKeyboardInputSource MouseKeyboard => mouseKeyboard;
        public Esp32MpuInputSource Esp32Mpu => esp32Mpu;
        public bool HardwareReady => controlMode == EndoscopeControlMode.Esp32Mpu && esp32Mpu != null && esp32Mpu.IsReady;

        public void Configure(MouseKeyboardInputSource mouse, Esp32MpuInputSource hardware)
        {
            mouseKeyboard = mouse;
            esp32Mpu = hardware;
        }

        public void SelectMode(EndoscopeControlMode mode)
        {
            controlMode = mode;
            esp32Mpu?.SetActiveInput(mode == EndoscopeControlMode.Esp32Mpu);
        }

        public EndoscopeInputFrame ReadFrame()
        {
            if (controlMode == EndoscopeControlMode.Esp32Mpu && esp32Mpu != null) return esp32Mpu.ReadFrame();
            return mouseKeyboard != null ? mouseKeyboard.ReadFrame() : default;
        }

        public void ResetAttemptState()
        {
            if (controlMode == EndoscopeControlMode.Esp32Mpu) esp32Mpu?.ResetAttemptState();
        }

        public void StopHardware() => esp32Mpu?.SetActiveInput(false);
    }
}
