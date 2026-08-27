using UnityEngine;
using UnityEngine.SceneManagement;

namespace NavegacaoRenal
{
    public sealed class KidneyGameManager : MonoBehaviour
    {
        [Header("Modes")]
        [SerializeField] private GameObject realisticRig;
        [SerializeField] private GameObject explorationRig;
        [SerializeField] private Transform probe;
        [SerializeField] private Transform startAnchor;
        [SerializeField] private Transform targetStone;
        [SerializeField] private GameObject routeGuide;
        [SerializeField] private GameObject minimapCamera;
        [SerializeField] private KidneyGameMode initialMode = KidneyGameMode.Realistic;

        [Header("Easy level")]
        [SerializeField] private int maximumWallContacts = 5;
        [SerializeField] private float captureDistance = 0.10f;
        [SerializeField] private float captureHoldDuration = 1f;

        [Header("Marco 4")]
        [SerializeField] private MonoBehaviour inputSourceBehaviour;
        [SerializeField] private VirtualGripperController virtualGripper;
        [SerializeField] private KidneyGameUI gameUI;
        [SerializeField] private KidneyAudioFeedback audioFeedback;

        [Header("Marco 5")]
        [SerializeField] private FreeFlyCameraController explorationController;
        [SerializeField] private ExplorationVisibilityController explorationVisibility;
        [SerializeField] private KidneyMinimapPresenter minimapPresenter;

        [Header("Marco 6")]
        [SerializeField] private EndoscopeInputRouter inputRouter;
        [SerializeField] private KidneyHardwareUI hardwareUI;

        private IEndoscopeInputSource inputSource;
        private KidneyGameMode currentMode;
        // Playing keeps the edit-mode Marco 3 validators backwards compatible.
        // Start() always applies the launch flow and enters Ready in Realistic mode.
        private KidneySessionState sessionState = KidneySessionState.Playing;
        private KidneySessionState stateBeforePause = KidneySessionState.Playing;
        private int wallContacts;
        private float elapsedTime;
        private float captureProgress;
        private Transform stoneOriginalParent;
        private Vector3 stoneOriginalLocalPosition;
        private Quaternion stoneOriginalLocalRotation;
        private Vector3 stoneOriginalLocalScale;
        private bool stonePoseCached;
        private bool stoneCaptured;
        private EndoscopeControlMode currentControlMode = EndoscopeControlMode.MouseKeyboard;
        private bool pausedForHardwareReconnect;

        public bool CanNavigate => currentMode == KidneyGameMode.Realistic && sessionState == KidneySessionState.Playing;
        public bool CanBeginAttempt => currentMode == KidneyGameMode.Realistic &&
                                       (currentControlMode != EndoscopeControlMode.Esp32Mpu || HardwareReady);
        public KidneyGameMode CurrentMode => currentMode;
        public KidneySessionState SessionState => sessionState;
        public int WallContacts => wallContacts;
        public int MaximumWallContacts => maximumWallContacts;
        public float CaptureDistance => captureDistance;
        public float CaptureHoldDuration => captureHoldDuration;
        public float CaptureProgress01 => captureProgress;
        public float ElapsedTime => elapsedTime;
        public bool IsPaused => sessionState == KidneySessionState.Paused;
        public bool IsWithinCaptureRange => probe != null && targetStone != null &&
                                            Vector3.Distance(probe.position, targetStone.position) <= captureDistance;
        public bool RouteVisible => routeGuide != null && routeGuide.activeSelf;
        public bool MinimapVisible => minimapPresenter != null
            ? minimapPresenter.IsVisible
            : minimapCamera != null && minimapCamera.activeSelf;
        public bool HasCapturedStone => stoneCaptured && virtualGripper != null && targetStone != null &&
                                        virtualGripper.CaptureAnchor != null &&
                                        Vector3.Distance(targetStone.position, virtualGripper.CaptureAnchor.position) < 0.0001f;
        public MonoBehaviour InputSourceBehaviour => inputSourceBehaviour;
        public VirtualGripperController VirtualGripper => virtualGripper;
        public KidneyGameUI GameUI => gameUI;
        public KidneyAudioFeedback AudioFeedback => audioFeedback;
        public FreeFlyCameraController ExplorationController => explorationController;
        public ExplorationVisibilityController ExplorationVisibility => explorationVisibility;
        public KidneyMinimapPresenter MinimapPresenter => minimapPresenter;
        public EndoscopeInputRouter InputRouter => inputRouter;
        public KidneyHardwareUI HardwareUI => hardwareUI;
        public EndoscopeControlMode CurrentControlMode => currentControlMode;
        public bool HardwareReady => inputRouter != null && inputRouter.HardwareReady;
        public bool PausedForHardwareReconnect => pausedForHardwareReconnect;

        public void Configure(
            GameObject realRig,
            GameObject freeRig,
            Transform probeTransform,
            Transform start,
            Transform stone,
            GameObject route,
            GameObject minimap)
        {
            realisticRig = realRig;
            explorationRig = freeRig;
            probe = probeTransform;
            startAnchor = start;
            targetStone = stone;
            routeGuide = route;
            minimapCamera = minimap;
            CacheStonePose();
        }

        public void ConfigureGameplay(
            MonoBehaviour source,
            VirtualGripperController gripper,
            KidneyGameUI ui,
            KidneyAudioFeedback feedback)
        {
            inputSourceBehaviour = source;
            inputSource = source as IEndoscopeInputSource;
            virtualGripper = gripper;
            gameUI = ui;
            audioFeedback = feedback;
        }

        public void ConfigureMarco5(FreeFlyCameraController freeController,
            ExplorationVisibilityController visibility, KidneyMinimapPresenter presenter)
        {
            explorationController = freeController;
            explorationVisibility = visibility;
            minimapPresenter = presenter;
        }

        public void ConfigureMarco6(EndoscopeInputRouter router, KidneyHardwareUI ui)
        {
            inputRouter = router;
            hardwareUI = ui;
            inputSourceBehaviour = router;
            inputSource = router;
        }

        private void Awake()
        {
            inputSource = inputSourceBehaviour as IEndoscopeInputSource;
            CacheStonePose();
        }

        private void Start()
        {
            Application.targetFrameRate = 120;
            KidneyLaunchContext.Selection launch = KidneyLaunchContext.ConsumeSelection(initialMode);
            currentControlMode = launch.Mode == KidneyGameMode.Exploration
                ? EndoscopeControlMode.MouseKeyboard
                : launch.ControlMode;
            inputRouter?.SelectMode(currentControlMode);
            ApplyMode(launch.Mode, true);
            SetRouteVisible(true);
            SetMinimapVisible(true);

            if (launch.Mode == KidneyGameMode.Realistic)
                PrepareAttempt();
            else
                sessionState = KidneySessionState.Playing;

            gameUI?.RefreshImmediate();
        }

        private void Update()
        {
            if (inputSource == null)
                inputSource = inputSourceBehaviour as IEndoscopeInputSource;

            EndoscopeInputFrame input = inputSource != null ? inputSource.ReadFrame() : default;
            if (input.RoutePressed) ToggleRoute();
            if (input.MinimapPressed) ToggleMinimap();

            if (currentMode == KidneyGameMode.Exploration)
                return;

            HandleHardwareConnection();

            if (input.PausePressed)
            {
                if (sessionState == KidneySessionState.Paused) ResumeAttempt();
                else if (sessionState == KidneySessionState.Playing) SetPaused(true);
            }
            if (input.ResetPressed) ResetAttempt();

            if (sessionState == KidneySessionState.Playing)
            {
                elapsedTime += Time.deltaTime;
                ProcessCapture(Time.deltaTime, input.CaptureHeld);
            }
            else if (captureProgress > 0f)
            {
                CancelCapture();
            }
        }

        private void LateUpdate()
        {
            if (!stoneCaptured || targetStone == null || virtualGripper == null || virtualGripper.CaptureAnchor == null)
                return;
            targetStone.SetPositionAndRotation(virtualGripper.CaptureAnchor.position, virtualGripper.CaptureAnchor.rotation);
        }

        public void PrepareAttempt()
        {
            RestoreStone();
            wallContacts = 0;
            elapsedTime = 0f;
            CancelCapture();
            ResetProbePosition();
            inputRouter?.ResetAttemptState();
            pausedForHardwareReconnect = false;
            SetRouteVisible(true);
            SetMinimapVisible(true);
            sessionState = KidneySessionState.Ready;
            MouseEndoscopeController.ReleaseCursor();
            gameUI?.RefreshImmediate();
        }

        public void BeginAttempt()
        {
            if (currentMode != KidneyGameMode.Realistic || !CanBeginAttempt)
                return;

            RestoreStone();
            wallContacts = 0;
            elapsedTime = 0f;
            CancelCapture();
            ResetProbePosition();
            inputRouter?.ResetAttemptState();
            pausedForHardwareReconnect = false;
            sessionState = KidneySessionState.Playing;
            gameUI?.RefreshImmediate();
        }

        public void ResumeAttempt()
        {
            if (sessionState != KidneySessionState.Paused)
                return;
            if (currentControlMode == EndoscopeControlMode.Esp32Mpu && !HardwareReady)
                return;
            pausedForHardwareReconnect = false;
            sessionState = stateBeforePause == KidneySessionState.Ready
                ? KidneySessionState.Ready
                : KidneySessionState.Playing;
            gameUI?.RefreshImmediate();
        }

        public void ReportWallContact(Vector3 point)
        {
            if (!CanNavigate)
                return;

            wallContacts++;
            gameUI?.ShowWallFlash(0.4f);
            audioFeedback?.PlayWallContact();
            if (wallContacts >= maximumWallContacts)
            {
                wallContacts = maximumWallContacts;
                sessionState = KidneySessionState.Lost;
                CancelCapture();
                audioFeedback?.PlayDefeat();
                MouseEndoscopeController.ReleaseCursor();
                gameUI?.RefreshImmediate();
            }
        }

        public void ProcessCapture(float deltaTime, bool captureHeld)
        {
            if (!CanNavigate || !captureHeld || !IsWithinCaptureRange)
            {
                CancelCapture();
                return;
            }

            captureProgress = Mathf.Clamp01(captureProgress + Mathf.Max(0f, deltaTime) / Mathf.Max(0.01f, captureHoldDuration));
            virtualGripper?.SetClosure(captureProgress);
            if (captureProgress >= 1f)
                CompleteCapture();
        }

        public void SetPaused(bool value)
        {
            if (currentMode != KidneyGameMode.Realistic)
                return;

            if (value)
            {
                if (sessionState == KidneySessionState.Won || sessionState == KidneySessionState.Lost)
                    return;
                stateBeforePause = sessionState;
                sessionState = KidneySessionState.Paused;
                CancelCapture();
                MouseEndoscopeController.ReleaseCursor();
            }
            else if (sessionState == KidneySessionState.Paused)
            {
                if (currentControlMode == EndoscopeControlMode.Esp32Mpu && !HardwareReady)
                    return;
                pausedForHardwareReconnect = false;
                sessionState = stateBeforePause == KidneySessionState.Ready
                    ? KidneySessionState.Ready
                    : KidneySessionState.Playing;
            }
            gameUI?.RefreshImmediate();
        }

        public void ResetAttempt()
        {
            if (currentMode == KidneyGameMode.Realistic) PrepareAttempt();
            else ApplyMode(KidneyGameMode.Exploration, true);
        }

        // Preserved for Marco 2/3 validation and editor tooling. Runtime mode selection
        // now happens only through the main menu.
        public void SetMode(KidneyGameMode mode, bool force = false)
        {
            if (!force && currentMode == mode)
                return;
            ApplyMode(mode, true);
            sessionState = KidneySessionState.Playing;
            if (mode == KidneyGameMode.Realistic) ResetProbePosition();
            gameUI?.RefreshImmediate();
        }

        public void ToggleRoute() => SetRouteVisible(!RouteVisible);
        public void ToggleMinimap() => SetMinimapVisible(!MinimapVisible);

        public void SetRouteVisible(bool visible)
        {
            if (routeGuide != null) routeGuide.SetActive(visible);
            if (minimapPresenter != null) minimapPresenter.SetRouteVisible(visible);
        }

        public void SetMinimapVisible(bool visible)
        {
            if (minimapPresenter != null) minimapPresenter.SetVisible(visible);
            else if (minimapCamera != null) minimapCamera.SetActive(visible);
        }

        public void ReturnToMenu()
        {
            MouseEndoscopeController.ReleaseCursor();
            inputRouter?.StopHardware();
            KidneyLaunchContext.Reset();
            SceneManager.LoadScene("MainMenu");
        }

        private void ApplyMode(KidneyGameMode mode, bool resetProbe)
        {
            currentMode = mode;
            if (realisticRig != null) realisticRig.SetActive(mode == KidneyGameMode.Realistic);
            if (explorationRig != null) explorationRig.SetActive(mode == KidneyGameMode.Exploration);
            if (mode == KidneyGameMode.Exploration)
            {
                MouseEndoscopeController.ReleaseCursor();
                explorationController?.ResetViewImmediate();
                explorationVisibility?.ResetDefaults();
            }
            if (resetProbe && mode == KidneyGameMode.Realistic) ResetProbePosition();
            minimapPresenter?.RefreshMarker();
        }

        private void HandleHardwareConnection()
        {
            if (currentControlMode != EndoscopeControlMode.Esp32Mpu) return;
            if (!HardwareReady)
            {
                if (sessionState == KidneySessionState.Playing)
                {
                    stateBeforePause = KidneySessionState.Playing;
                    sessionState = KidneySessionState.Paused;
                    pausedForHardwareReconnect = true;
                    CancelCapture();
                    MouseEndoscopeController.ReleaseCursor();
                    gameUI?.RefreshImmediate();
                }
                return;
            }

            if (pausedForHardwareReconnect && sessionState == KidneySessionState.Paused)
            {
                pausedForHardwareReconnect = false;
                sessionState = KidneySessionState.Playing;
                gameUI?.RefreshImmediate();
            }
        }

        private void CompleteCapture()
        {
            captureProgress = 1f;
            virtualGripper?.SetClosure(1f);
            Transform anchor = virtualGripper != null ? virtualGripper.CaptureAnchor : null;
            if (targetStone != null && anchor != null)
            {
                targetStone.SetPositionAndRotation(anchor.position, anchor.rotation);
                stoneCaptured = true;
            }

            audioFeedback?.PlayCapture();
            audioFeedback?.PlayVictory();
            sessionState = KidneySessionState.Won;
            MouseEndoscopeController.ReleaseCursor();
            gameUI?.RefreshImmediate();
        }

        private void CancelCapture()
        {
            captureProgress = 0f;
            virtualGripper?.ResetGripper();
        }

        private void ResetProbePosition()
        {
            if (probe == null || startAnchor == null)
                return;

            MouseEndoscopeController controller = probe.GetComponent<MouseEndoscopeController>();
            if (controller != null) controller.ResetTo(startAnchor);
            else probe.SetPositionAndRotation(startAnchor.position, startAnchor.rotation);
        }

        private void CacheStonePose()
        {
            if (stonePoseCached || targetStone == null)
                return;
            stoneOriginalParent = targetStone.parent;
            stoneOriginalLocalPosition = targetStone.localPosition;
            stoneOriginalLocalRotation = targetStone.localRotation;
            stoneOriginalLocalScale = targetStone.localScale;
            stonePoseCached = true;
        }

        private void RestoreStone()
        {
            CacheStonePose();
            if (!stonePoseCached || targetStone == null)
                return;
            stoneCaptured = false;
            if (targetStone.parent != stoneOriginalParent)
                targetStone.SetParent(stoneOriginalParent, false);
            targetStone.localPosition = stoneOriginalLocalPosition;
            targetStone.localRotation = stoneOriginalLocalRotation;
            targetStone.localScale = stoneOriginalLocalScale;
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
                MouseEndoscopeController.ReleaseCursor();
        }

        private void OnApplicationQuit() => inputRouter?.StopHardware();
    }
}
