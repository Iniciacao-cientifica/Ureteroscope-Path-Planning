using UnityEngine;
using UnityEngine.UI;

namespace NavegacaoRenal
{
    public sealed class KidneyGameUI : MonoBehaviour
    {
        [SerializeField] private KidneyGameManager gameManager;
        [SerializeField] private GameObject readyPanel;
        [SerializeField] private GameObject hudPanel;
        [SerializeField] private GameObject pausePanel;
        [SerializeField] private GameObject resultPanel;
        [SerializeField] private GameObject explorationPanel;
        [SerializeField] private GameObject capturePanel;
        [SerializeField] private Text timerText;
        [SerializeField] private Text contactsText;
        [SerializeField] private Text captureText;
        [SerializeField] private Text resultTitleText;
        [SerializeField] private Text resultSummaryText;
        [SerializeField] private Image captureFill;
        [SerializeField] private Image wallFlash;
        [SerializeField] private Button startButton;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button restartPauseButton;
        [SerializeField] private Button restartResultButton;
        [SerializeField] private Button menuPauseButton;
        [SerializeField] private Button menuResultButton;
        [SerializeField] private Button menuExplorationButton;

        private float flashUntil;

        public bool IsConfigured => gameManager != null && readyPanel != null && hudPanel != null &&
                                    pausePanel != null && resultPanel != null && captureFill != null;

        public void Configure(
            KidneyGameManager manager,
            GameObject ready,
            GameObject hud,
            GameObject pause,
            GameObject result,
            GameObject exploration,
            GameObject capture,
            Text timer,
            Text contacts,
            Text capturePrompt,
            Text resultTitle,
            Text resultSummary,
            Image progressFill,
            Image flash,
            Button start,
            Button resume,
            Button restartPause,
            Button restartResult,
            Button menuPause,
            Button menuResult,
            Button menuExploration)
        {
            gameManager = manager;
            readyPanel = ready;
            hudPanel = hud;
            pausePanel = pause;
            resultPanel = result;
            explorationPanel = exploration;
            capturePanel = capture;
            timerText = timer;
            contactsText = contacts;
            captureText = capturePrompt;
            resultTitleText = resultTitle;
            resultSummaryText = resultSummary;
            captureFill = progressFill;
            wallFlash = flash;
            startButton = start;
            resumeButton = resume;
            restartPauseButton = restartPause;
            restartResultButton = restartResult;
            menuPauseButton = menuPause;
            menuResultButton = menuResult;
            menuExplorationButton = menuExploration;
        }

        private void Awake()
        {
            BindButtons(true);
            RefreshImmediate();
        }

        private void OnDestroy() => BindButtons(false);

        private void Update()
        {
            RefreshImmediate();
            if (wallFlash != null)
            {
                Color color = wallFlash.color;
                color.a = Time.unscaledTime < flashUntil ? 0.30f : 0f;
                wallFlash.color = color;
            }
        }

        public void ShowWallFlash(float duration)
        {
            flashUntil = Mathf.Max(flashUntil, Time.unscaledTime + duration);
            if (wallFlash != null)
            {
                Color color = wallFlash.color;
                color.a = 0.30f;
                wallFlash.color = color;
            }
        }

        public void RefreshImmediate()
        {
            if (gameManager == null)
                return;

            bool exploration = gameManager.CurrentMode == KidneyGameMode.Exploration;
            KidneySessionState state = gameManager.SessionState;
            SetActive(readyPanel, !exploration && state == KidneySessionState.Ready);
            SetActive(hudPanel, !exploration && state != KidneySessionState.Ready);
            SetActive(pausePanel, !exploration && state == KidneySessionState.Paused);
            SetActive(resultPanel, !exploration && (state == KidneySessionState.Won || state == KidneySessionState.Lost));
            SetActive(explorationPanel, exploration);
            SetActive(capturePanel, !exploration && state == KidneySessionState.Playing && gameManager.IsWithinCaptureRange);

            if (timerText != null) timerText.text = $"Tempo  {gameManager.ElapsedTime:0.0}s";
            if (contactsText != null) contactsText.text = $"Contatos  {gameManager.WallContacts}/{gameManager.MaximumWallContacts}";
            if (captureFill != null) captureFill.fillAmount = gameManager.CaptureProgress01;
            if (captureText != null)
                captureText.text = gameManager.CaptureProgress01 > 0f ? "Fechando a garra..." : "Segure ESPAÇO para capturar";

            if (resultTitleText != null)
                resultTitleText.text = state == KidneySessionState.Won ? "PEDRA CAPTURADA" : "LIMITE DE CONTATOS";
            if (resultSummaryText != null)
                resultSummaryText.text = $"Tempo: {gameManager.ElapsedTime:0.0}s\nContatos: {gameManager.WallContacts}/{gameManager.MaximumWallContacts}";
        }

        private void BindButtons(bool bind)
        {
            if (gameManager == null)
                return;
            Bind(startButton, gameManager.BeginAttempt, bind);
            Bind(resumeButton, gameManager.ResumeAttempt, bind);
            Bind(restartPauseButton, gameManager.ResetAttempt, bind);
            Bind(restartResultButton, gameManager.ResetAttempt, bind);
            Bind(menuPauseButton, gameManager.ReturnToMenu, bind);
            Bind(menuResultButton, gameManager.ReturnToMenu, bind);
            Bind(menuExplorationButton, gameManager.ReturnToMenu, bind);
        }

        private static void Bind(Button button, UnityEngine.Events.UnityAction action, bool bind)
        {
            if (button == null || action == null)
                return;
            if (bind) button.onClick.AddListener(action);
            else button.onClick.RemoveListener(action);
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active)
                target.SetActive(active);
        }
    }
}
