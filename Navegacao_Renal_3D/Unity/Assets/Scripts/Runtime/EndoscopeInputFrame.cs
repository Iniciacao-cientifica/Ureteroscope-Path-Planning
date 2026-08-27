using UnityEngine;

namespace NavegacaoRenal
{
    public readonly struct EndoscopeInputFrame
    {
        public EndoscopeInputFrame(
            Vector2 steeringDelta,
            float advance,
            float roll,
            bool captureHeld,
            bool pausePressed,
            bool resetPressed,
            bool routePressed,
            bool minimapPressed,
            bool cursorLockPressed,
            bool cursorReleasePressed)
        {
            SteeringDelta = steeringDelta;
            Advance = advance;
            Roll = roll;
            CaptureHeld = captureHeld;
            PausePressed = pausePressed;
            ResetPressed = resetPressed;
            RoutePressed = routePressed;
            MinimapPressed = minimapPressed;
            CursorLockPressed = cursorLockPressed;
            CursorReleasePressed = cursorReleasePressed;
        }

        public Vector2 SteeringDelta { get; }
        public float Advance { get; }
        public float Roll { get; }
        public bool CaptureHeld { get; }
        public bool PausePressed { get; }
        public bool ResetPressed { get; }
        public bool RoutePressed { get; }
        public bool MinimapPressed { get; }
        public bool CursorLockPressed { get; }
        public bool CursorReleasePressed { get; }
    }
}
