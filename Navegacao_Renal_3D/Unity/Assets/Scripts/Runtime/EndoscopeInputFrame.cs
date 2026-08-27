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
            : this(steeringDelta, advance, roll, captureHeld, pausePressed, resetPressed,
                routePressed, minimapPressed, cursorLockPressed, cursorReleasePressed,
                EndoscopeSteeringMode.MouseDelta, Quaternion.identity, false)
        {
        }

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
            bool cursorReleasePressed,
            EndoscopeSteeringMode steeringMode,
            Quaternion relativeOrientation,
            bool calibratePressed)
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
            SteeringMode = steeringMode;
            RelativeOrientation = relativeOrientation;
            CalibratePressed = calibratePressed;
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
        public EndoscopeSteeringMode SteeringMode { get; }
        public Quaternion RelativeOrientation { get; }
        public bool CalibratePressed { get; }
    }
}
