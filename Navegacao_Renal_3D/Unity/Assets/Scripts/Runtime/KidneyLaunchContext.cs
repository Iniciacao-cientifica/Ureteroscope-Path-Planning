namespace NavegacaoRenal
{
    public static class KidneyLaunchContext
    {
        private static bool hasSelection;
        private static KidneyGameMode selectedMode = KidneyGameMode.Realistic;

        public static void Select(KidneyGameMode mode)
        {
            selectedMode = mode;
            hasSelection = true;
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
        }
    }
}
