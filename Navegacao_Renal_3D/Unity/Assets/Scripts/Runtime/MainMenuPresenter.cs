using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NavegacaoRenal
{
    public sealed class MainMenuPresenter : MonoBehaviour
    {
        [SerializeField] private Button realisticButton;
        [SerializeField] private Button explorationButton;

        public Button RealisticButton => realisticButton;
        public Button ExplorationButton => explorationButton;

        public void Configure(Button realistic, Button exploration)
        {
            realisticButton = realistic;
            explorationButton = exploration;
        }

        private void Awake()
        {
            KidneyLaunchContext.Reset();
            if (realisticButton != null) realisticButton.onClick.AddListener(OpenRealistic);
            if (explorationButton != null) explorationButton.onClick.AddListener(OpenExploration);
        }

        private void OnDestroy()
        {
            if (realisticButton != null) realisticButton.onClick.RemoveListener(OpenRealistic);
            if (explorationButton != null) explorationButton.onClick.RemoveListener(OpenExploration);
        }

        public void OpenRealistic()
        {
            KidneyLaunchContext.Select(KidneyGameMode.Realistic);
            SceneManager.LoadScene("KidneyGame");
        }

        public void OpenExploration()
        {
            KidneyLaunchContext.Select(KidneyGameMode.Exploration);
            SceneManager.LoadScene("KidneyGame");
        }
    }
}
