using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NavegacaoRenal
{
    public sealed class Esp32MpuInputSource : MonoBehaviour, IEndoscopeInputSource
    {
        private const string ResponsePreference = "NavegacaoRenal.MpuResponse";
        private const string PortPreference = "NavegacaoRenal.Esp32Port";

        [SerializeField] private KidneyGameManager gameManager;
        [SerializeField] private float stalePacketSeconds = 0.25f;
        [SerializeField] private float orientationDeadZoneDegrees = 1.5f;
        [SerializeField] private float responseGain = 1f;

        private IEsp32PacketTransport transport;
        private bool ownsTransport;
        private readonly MpuOrientationMapper orientationMapper = new MpuOrientationMapper();
        private readonly Esp32ButtonInterpreter buttonInterpreter = new Esp32ButtonInterpreter(0.35);
        private int cachedFrame = -1;
        private EndoscopeInputFrame cachedInput;
        private bool active;

        public float StalePacketSeconds => stalePacketSeconds;
        public float OrientationDeadZoneDegrees => orientationDeadZoneDegrees;
        public float ResponseGain => responseGain;
        public bool IsCalibrated => orientationMapper.IsCalibrated;
        public bool IsReady => TryGetFreshPacket(out _);
        public int Direction => buttonInterpreter.Direction;
        public string DirectionLabel => buttonInterpreter.DirectionLabel;
        public Esp32ConnectionStatus ConnectionStatus => transport?.Status ?? Esp32ConnectionStatus.Stopped;
        public string ConnectedPort => transport?.ConnectedPort ?? string.Empty;
        public string LastError => transport?.LastError ?? string.Empty;
        public float PacketRateHz => transport?.PacketRateHz ?? 0f;
        public bool IsActive => active;

        public void Configure(KidneyGameManager manager) => gameManager = manager;

        public void SetTransport(IEsp32PacketTransport replacement, bool takeOwnership = false)
        {
            if (ownsTransport) transport?.Dispose();
            transport = replacement;
            ownsTransport = takeOwnership;
            cachedFrame = -1;
            orientationMapper.ResetCalibration();
            buttonInterpreter.Reset();
        }

        public void SetActiveInput(bool value)
        {
            if (active == value) return;
            active = value;
            if (active) Reconnect(PlayerPrefs.GetString(PortPreference, string.Empty));
            else transport?.Stop();
        }

        public void Reconnect(string preferredPort = null)
        {
            EnsureTransport();
            string selected = preferredPort ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(selected)) PlayerPrefs.SetString(PortPreference, selected);
            orientationMapper.ResetCalibration();
            buttonInterpreter.Reset();
            transport.Start(selected);
        }

        public string[] GetAvailablePorts()
        {
            EnsureTransport();
            return transport.GetPortNames();
        }

        public void CalibrateNow()
        {
            if (TryGetFreshPacket(out Esp32MpuPacket packet)) orientationMapper.Calibrate(packet.SensorOrientation);
            else orientationMapper.ResetCalibration();
            cachedFrame = -1;
        }

        public void ResetAttemptState()
        {
            buttonInterpreter.Reset();
            CalibrateNow();
        }

        public void SetResponseGain(float value)
        {
            responseGain = Mathf.Clamp(value, 0.5f, 2f);
            PlayerPrefs.SetFloat(ResponsePreference, responseGain);
        }

        public EndoscopeInputFrame ReadFrame()
        {
            if (cachedFrame == Time.frameCount) return cachedInput;
            cachedFrame = Time.frameCount;
            Keyboard keyboard = Keyboard.current;
            bool calibrate = keyboard != null && keyboard.cKey.wasPressedThisFrame;
            bool pause = keyboard != null && keyboard.pKey.wasPressedThisFrame;
            bool reset = keyboard != null && keyboard.rKey.wasPressedThisFrame;
            bool route = keyboard != null && keyboard.tKey.wasPressedThisFrame;
            bool minimap = keyboard != null && keyboard.mKey.wasPressedThisFrame;
            bool releaseCursor = keyboard != null && keyboard.escapeKey.wasPressedThisFrame;

            if (!active || !TryGetFreshPacket(out Esp32MpuPacket packet))
            {
                cachedInput = new EndoscopeInputFrame(Vector2.zero, 0f, 0f, false, pause, reset, route,
                    minimap, false, releaseCursor, EndoscopeSteeringMode.RelativeOrientation,
                    Quaternion.identity, calibrate);
                return cachedInput;
            }

            if (calibrate || !orientationMapper.IsCalibrated) orientationMapper.Calibrate(packet.SensorOrientation);
            Quaternion relative = orientationMapper.MapRelative(packet.SensorOrientation, responseGain, orientationDeadZoneDegrees);
            bool captureRange = gameManager != null && gameManager.IsWithinCaptureRange;
            Esp32ButtonState button = buttonInterpreter.Update(packet.ButtonPressed, Time.unscaledTimeAsDouble, captureRange);
            cachedInput = new EndoscopeInputFrame(Vector2.zero, button.Advance, 0f, button.CaptureHeld,
                pause, reset, route, minimap, false, releaseCursor, EndoscopeSteeringMode.RelativeOrientation,
                relative, calibrate);
            return cachedInput;
        }

        private void Awake()
        {
            responseGain = Mathf.Clamp(PlayerPrefs.GetFloat(ResponsePreference, responseGain), 0.5f, 2f);
            EnsureTransport();
        }

        private void OnDestroy()
        {
            transport?.Stop();
            if (ownsTransport) transport?.Dispose();
        }

        private bool TryGetFreshPacket(out Esp32MpuPacket packet)
        {
            packet = null;
            return transport != null && transport.TryGetLatest(out packet, out double age) &&
                   age <= stalePacketSeconds && packet.ImuOk && packet.ProtocolVersion == 2;
        }

        private void EnsureTransport()
        {
            if (transport != null) return;
            transport = new SystemEsp32PacketTransport();
            ownsTransport = true;
        }
    }
}
