using System;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEngine;

namespace NavegacaoRenal
{
    public sealed class Esp32MpuPacket
    {
        public int ProtocolVersion { get; set; }
        public uint Sequence { get; set; }
        public uint DeviceMilliseconds { get; set; }
        public Quaternion SensorOrientation { get; set; }
        public bool ButtonPressed { get; set; }
        public bool ImuOk { get; set; }
        public string FirmwareVersion { get; set; }
    }

    public static class Esp32MpuPacketParser
    {
        private const string Number = @"([-+0-9.eE]+)";
        private static readonly Regex VersionPattern = new Regex("\\\"v\\\"\\s*:\\s*(\\d+)", RegexOptions.Compiled);
        private static readonly Regex SequencePattern = new Regex("\\\"seq\\\"\\s*:\\s*(\\d+)", RegexOptions.Compiled);
        private static readonly Regex MillisecondsPattern = new Regex("\\\"ms\\\"\\s*:\\s*(\\d+)", RegexOptions.Compiled);
        private static readonly Regex QuaternionPattern = new Regex("\\\"q\\\"\\s*:\\s*\\[\\s*" + Number + "\\s*,\\s*" + Number + "\\s*,\\s*" + Number + "\\s*,\\s*" + Number + "\\s*\\]", RegexOptions.Compiled);
        private static readonly Regex ButtonPattern = new Regex("\\\"button\\\"\\s*:\\s*(true|false)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex ImuPattern = new Regex("\\\"imu_ok\\\"\\s*:\\s*(true|false)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex FirmwarePattern = new Regex("\\\"fw\\\"\\s*:\\s*\\\"([^\\\"]+)\\\"", RegexOptions.Compiled);

        public static bool TryParse(string line, out Esp32MpuPacket packet)
        {
            packet = null;
            if (string.IsNullOrWhiteSpace(line) || line.Length > 512 || line[0] != '{') return false;
            Match version = VersionPattern.Match(line);
            Match sequence = SequencePattern.Match(line);
            Match milliseconds = MillisecondsPattern.Match(line);
            Match quaternion = QuaternionPattern.Match(line);
            Match button = ButtonPattern.Match(line);
            Match imu = ImuPattern.Match(line);
            Match firmware = FirmwarePattern.Match(line);
            if (!version.Success || !sequence.Success || !milliseconds.Success || !quaternion.Success ||
                !button.Success || !imu.Success || !firmware.Success) return false;

            if (!int.TryParse(version.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int protocol) || protocol != 2 ||
                !uint.TryParse(sequence.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint seq) ||
                !uint.TryParse(milliseconds.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint ms) ||
                !TryFloat(quaternion.Groups[1].Value, out float w) || !TryFloat(quaternion.Groups[2].Value, out float x) ||
                !TryFloat(quaternion.Groups[3].Value, out float y) || !TryFloat(quaternion.Groups[4].Value, out float z)) return false;

            float magnitude = Mathf.Sqrt(w * w + x * x + y * y + z * z);
            if (!float.IsFinite(magnitude) || magnitude < 0.5f || magnitude > 1.5f) return false;
            Quaternion orientation = new Quaternion(x / magnitude, y / magnitude, z / magnitude, w / magnitude);
            packet = new Esp32MpuPacket
            {
                ProtocolVersion = protocol,
                Sequence = seq,
                DeviceMilliseconds = ms,
                SensorOrientation = orientation,
                ButtonPressed = string.Equals(button.Groups[1].Value, "true", StringComparison.OrdinalIgnoreCase),
                ImuOk = string.Equals(imu.Groups[1].Value, "true", StringComparison.OrdinalIgnoreCase),
                FirmwareVersion = firmware.Groups[1].Value
            };
            return true;
        }

        public static bool IsNewerSequence(uint candidate, uint previous)
        {
            uint difference = unchecked(candidate - previous);
            return difference != 0 && difference < 0x80000000u;
        }

        private static bool TryFloat(string value, out float result) =>
            float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result) && float.IsFinite(result);
    }
}
