using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace NavegacaoRenal
{
    public sealed class KidneyHardwareUI : MonoBehaviour
    {
        [SerializeField] private KidneyGameManager gameManager;
        [SerializeField] private EndoscopeInputRouter inputRouter;
        [SerializeField] private MouseEndoscopeController endoscopeController;
        [SerializeField] private GameObject connectionPanel;
        [SerializeField] private GameObject hardwareStatusPanel;
        [SerializeField] private Text connectionStatusText;
        [SerializeField] private Text hardwareStatusText;
        [SerializeField] private Text directionText;
        [SerializeField] private Text readyControlsText;
        [SerializeField] private Text sensitivityLabel;
        [SerializeField] private Dropdown portDropdown;
        [SerializeField] private Button reconnectButton;
        [SerializeField] private Button calibrateButton;
        [SerializeField] private Button startButton;
        [SerializeField] private Slider sensitivitySlider;

        private float nextPortRefresh;
        private string[] listedPorts = Array.Empty<string>();
        private bool suppressSlider;
        private EndoscopeControlMode configuredSensitivityMode = (EndoscopeControlMode)(-1);

        public bool IsConfigured => gameManager != null && inputRouter != null && endoscopeController != null &&
                                    connectionPanel != null && hardwareStatusPanel != null &&
                                    connectionStatusText != null && hardwareStatusText != null && directionText != null &&
                                    portDropdown != null && reconnectButton != null && calibrateButton != null &&
                                    startButton != null && sensitivitySlider != null;

        public void Configure(KidneyGameManager manager, EndoscopeInputRouter router,
            MouseEndoscopeController controller, GameObject connection, GameObject statusPanel,
            Text connectionText, Text statusText, Text direction, Text controls, Text sensitivityText,
            Dropdown dropdown, Button reconnect, Button calibrate, Button start, Slider slider)
        {
            gameManager = manager;
            inputRouter = router;
            endoscopeController = controller;
            connectionPanel = connection;
            hardwareStatusPanel = statusPanel;
            connectionStatusText = connectionText;
            hardwareStatusText = statusText;
            directionText = direction;
            readyControlsText = controls;
            sensitivityLabel = sensitivityText;
            portDropdown = dropdown;
            reconnectButton = reconnect;
            calibrateButton = calibrate;
            startButton = start;
            sensitivitySlider = slider;
        }

        private void Awake()
        {
            Bind(true);
            ConfigureSensitivity();
            RefreshPorts(true);
            RefreshImmediate();
        }

        private void OnDestroy() => Bind(false);

        private void Update()
        {
            if (Time.unscaledTime >= nextPortRefresh) RefreshPorts(false);
            RefreshImmediate();
        }

        public void RefreshImmediate()
        {
            if (gameManager == null || inputRouter == null) return;
            if (configuredSensitivityMode != gameManager.CurrentControlMode) ConfigureSensitivity();
            bool hardware = gameManager.CurrentControlMode == EndoscopeControlMode.Esp32Mpu;
            Esp32MpuInputSource mpu = inputRouter.Esp32Mpu;
            bool ready = hardware && inputRouter.HardwareReady;
            SetActive(connectionPanel, hardware && !ready);
            SetActive(hardwareStatusPanel, hardware);
            if (startButton != null) startButton.interactable = gameManager.CanBeginAttempt;
            if (calibrateButton != null) calibrateButton.interactable = ready;

            if (readyControlsText != null)
            {
                readyControlsText.text = hardware
                    ? "Incline o MPU para orientar • Segure o botão para mover\nClique duplo: avanço/recuo • C: recalibrar • perto da pedra: capturar"
                    : "Mouse: orientar   W/S: avançar/recuar   Q/E: rolar\nSegure Espaço por 1 segundo perto da pedra";
            }
            if (sensitivityLabel != null)
                sensitivityLabel.text = hardware ? "Resposta do MPU" : "Sensibilidade do mouse";

            if (mpu != null)
            {
                string port = string.IsNullOrEmpty(mpu.ConnectedPort) ? "sem porta" : mpu.ConnectedPort;
                if (connectionStatusText != null)
                {
                    string error = string.IsNullOrWhiteSpace(mpu.LastError) ? string.Empty : "\n" + mpu.LastError;
                    connectionStatusText.text = $"Estado: {StatusLabel(mpu.ConnectionStatus)}\nPorta: {port}{error}";
                }
                if (hardwareStatusText != null)
                    hardwareStatusText.text = ready
                        ? $"MPU conectado • {port} • {mpu.PacketRateHz:0} Hz • C recalibrar"
                        : "MPU desconectado • tentativa pausada";
                if (directionText != null) directionText.text = $"Direção: {mpu.DirectionLabel}";
            }
        }

        private void Reconnect()
        {
            Esp32MpuInputSource source = inputRouter?.Esp32Mpu;
            if (source == null) return;
            string selected = portDropdown != null && portDropdown.value > 0 && portDropdown.value - 1 < listedPorts.Length
                ? listedPorts[portDropdown.value - 1]
                : string.Empty;
            source.Reconnect(selected);
        }

        private void Calibrate() => inputRouter?.Esp32Mpu?.CalibrateNow();

        private void SensitivityChanged(float value)
        {
            if (suppressSlider || gameManager == null) return;
            if (gameManager.CurrentControlMode == EndoscopeControlMode.Esp32Mpu)
                inputRouter?.Esp32Mpu?.SetResponseGain(value);
            else endoscopeController?.SetMouseSensitivity(value);
        }

        private void ConfigureSensitivity()
        {
            if (sensitivitySlider == null || gameManager == null) return;
            configuredSensitivityMode = gameManager.CurrentControlMode;
            suppressSlider = true;
            sensitivitySlider.minValue = 0.5f;
            sensitivitySlider.maxValue = 2f;
            sensitivitySlider.wholeNumbers = false;
            sensitivitySlider.value = gameManager.CurrentControlMode == EndoscopeControlMode.Esp32Mpu
                ? inputRouter.Esp32Mpu.ResponseGain
                : endoscopeController.MouseSensitivityMultiplier;
            suppressSlider = false;
        }

        private void RefreshPorts(bool force)
        {
            nextPortRefresh = Time.unscaledTime + 1f;
            if (inputRouter?.Esp32Mpu == null || portDropdown == null) return;
            string[] ports = inputRouter.Esp32Mpu.GetAvailablePorts();
            if (!force && ports.SequenceEqual(listedPorts)) return;
            listedPorts = ports;
            int previous = portDropdown.value;
            portDropdown.ClearOptions();
            portDropdown.AddOptions(new[] { "Automático" }.Concat(ports).ToList());
            portDropdown.value = Mathf.Clamp(previous, 0, ports.Length);
            portDropdown.RefreshShownValue();
        }

        private void Bind(bool value)
        {
            if (reconnectButton != null)
            {
                if (value) reconnectButton.onClick.AddListener(Reconnect);
                else reconnectButton.onClick.RemoveListener(Reconnect);
            }
            if (calibrateButton != null)
            {
                if (value) calibrateButton.onClick.AddListener(Calibrate);
                else calibrateButton.onClick.RemoveListener(Calibrate);
            }
            if (sensitivitySlider != null)
            {
                if (value) sensitivitySlider.onValueChanged.AddListener(SensitivityChanged);
                else sensitivitySlider.onValueChanged.RemoveListener(SensitivityChanged);
            }
        }

        private static void SetActive(GameObject target, bool value)
        {
            if (target != null && target.activeSelf != value) target.SetActive(value);
        }

        private static string StatusLabel(Esp32ConnectionStatus status)
        {
            switch (status)
            {
                case Esp32ConnectionStatus.Searching: return "procurando";
                case Esp32ConnectionStatus.Connecting: return "conectando";
                case Esp32ConnectionStatus.Streaming: return "recebendo dados";
                case Esp32ConnectionStatus.Error: return "erro";
                default: return "parado";
            }
        }
    }
}
