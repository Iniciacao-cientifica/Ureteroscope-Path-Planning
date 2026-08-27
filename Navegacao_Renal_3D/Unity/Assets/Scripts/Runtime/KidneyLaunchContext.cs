namespace NavegacaoRenal
{
    public static class KidneyLaunchContext
    {
        private static bool hasSelection;
        private static KidneyGameMode selectedMode = KidneyGameMode.Realistic;
        private static EndoscopeControlMode selectedControlMode = EndoscopeControlMode.MouseKeyboard;

        public readonly struct Selection
        {
            public Selection(KidneyGameMode mode, EndoscopeControlMode controlMode)
            {
                Mode = mode;
                ControlMode = controlMode;
            }
            public KidneyGameMode Mode { get; }
            public EndoscopeControlMode ControlMode { get; }
        }

        public static void Select(KidneyGameMode mode)
        {
            Select(mode, EndoscopeControlMode.MouseKeyboard);
        }

        public static void Select(KidneyGameMode mode, EndoscopeControlMode controlMode)
        {
            selectedMode = mode;
            selectedControlMode = controlMode;
            hasSelection = true;
        }

        public static Selection ConsumeSelection(KidneyGameMode fallback)
        {
            Selection result = hasSelection
                ? new Selection(selectedMode, selectedControlMode)
                : new Selection(fallback, EndoscopeControlMode.MouseKeyboard);
            hasSelection = false;
            return result;
        }

        public static KidneyGameMode Consume(KidneyGameMode fallback)
        {
            KidneyGameMode result = hasSelection ? selectedMode : fallback;
            hasSelection = false;
            return result;
        }

        public static void Reset()
        {
            hasSelection = false;
            selectedMode = KidneyGameMode.Realistic;
            selectedControlMode = EndoscopeControlMode.MouseKeyboard;
        }
    }
}
