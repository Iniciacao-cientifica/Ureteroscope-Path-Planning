namespace NavegacaoRenal
{
    public readonly struct Esp32ButtonState
    {
        public Esp32ButtonState(float advance, bool captureHeld, int direction)
        {
            Advance = advance;
            CaptureHeld = captureHeld;
            Direction = direction;
        }

        public float Advance { get; }
        public bool CaptureHeld { get; }
        public int Direction { get; }
    }

    public sealed class Esp32ButtonInterpreter
    {
        private readonly double doubleClickSeconds;
        private bool previousPressed;
        private double previousPressTime = double.NegativeInfinity;

        public Esp32ButtonInterpreter(double doubleClickSeconds = 0.35)
        {
            this.doubleClickSeconds = doubleClickSeconds;
            Direction = 1;
        }

        public int Direction { get; private set; }
        public string DirectionLabel => Direction > 0 ? "Avanço" : "Recuo";

        public Esp32ButtonState Update(bool pressed, double nowSeconds, bool withinCaptureRange)
        {
            bool pressedThisFrame = pressed && !previousPressed;
            if (withinCaptureRange)
            {
                previousPressTime = double.NegativeInfinity;
            }
            else if (pressedThisFrame)
            {
                if (nowSeconds - previousPressTime <= doubleClickSeconds)
                {
                    Direction *= -1;
                    previousPressTime = double.NegativeInfinity;
                }
                else previousPressTime = nowSeconds;
            }

            previousPressed = pressed;
            return withinCaptureRange
                ? new Esp32ButtonState(0f, pressed, Direction)
                : new Esp32ButtonState(pressed ? Direction : 0f, false, Direction);
        }

        public void Reset()
        {
            Direction = 1;
            previousPressed = false;
            previousPressTime = double.NegativeInfinity;
        }
    }
}
