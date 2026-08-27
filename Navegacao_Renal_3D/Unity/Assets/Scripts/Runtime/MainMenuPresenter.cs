using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NavegacaoRenal
{
    public sealed class MainMenuPresenter : MonoBehaviour
    {
        [SerializeField] private Button realisticButton;
        [SerializeField] private Button realisticMpuButton;
        [SerializeField] private Button explorationButton;

        public Button RealisticButton => realisticButton;
        public Button ExplorationButton => explorationButton;
        public Button RealisticMpuButton => realisticMpuButton;

        public void Configure(Button realistic, Button exploration)
        {
            realisticButton = realistic;
            explorationButton = exploration;
        }

        public void ConfigureMarco6(Button realisticMouse, Button realisticMpu, Button exploration)
        {
            realisticButton = realisticMouse;
            realisticMpuButton = realisticMpu;
            explorationButton = exploration;
        }

        private void Awake()
        {
            KidneyLaunchContext.Reset();
            if (realisticButton != null) realisticButton.onClick.AddListener(OpenRealistic);
            if (realisticMpuButton != null) realisticMpuButton.onClick.AddListener(OpenRealisticMpu);
            if (explorationButton != null) explorationButton.onClick.AddListener(OpenExploration);
        }

        private void OnDestroy()
        {
            if (realisticButton != null) realisticButton.onClick.RemoveListener(OpenRealistic);
            if (realisticMpuButton != null) realisticMpuButton.onClick.RemoveListener(OpenRealisticMpu);
            if (explorationButton != null) explorationButton.onClick.RemoveListener(OpenExploration);
        }

        public void OpenRealistic()
        {
            KidneyLaunchContext.Select(KidneyGameMode.Realistic, EndoscopeControlMode.MouseKeyboard);
            SceneManager.LoadScene("KidneyGame");
        }

        public void OpenRealisticMpu()
        {
            KidneyLaunchContext.Select(KidneyGameMode.Realistic, EndoscopeControlMode.Esp32Mpu);
            SceneManager.LoadScene("KidneyGame");
        }

        public void OpenExploration()
        {
            KidneyLaunchContext.Select(KidneyGameMode.Exploration);
            SceneManager.LoadScene("KidneyGame");
        }
    }
}
