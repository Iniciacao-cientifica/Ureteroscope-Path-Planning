using UnityEngine;

namespace NavegacaoRenal
{
    public sealed class MpuOrientationMapper
    {
        private Quaternion reference = Quaternion.identity;
        public bool IsCalibrated { get; private set; }

        public void Calibrate(Quaternion sensorOrientation)
        {
            reference = Normalize(sensorOrientation);
            IsCalibrated = true;
        }

        public void ResetCalibration()
        {
            reference = Quaternion.identity;
            IsCalibrated = false;
        }

        public Quaternion MapRelative(Quaternion sensorOrientation, float responseGain, float deadZoneDegrees)
        {
            if (!IsCalibrated) Calibrate(sensorOrientation);
            Quaternion relativeSensor = Quaternion.Inverse(reference) * Normalize(sensorOrientation);
            // MPU: X right, Y forward, Z up. Unity: X right, Y up, Z forward.
            Quaternion unityRelative = Normalize(new Quaternion(
                -relativeSensor.x,
                -relativeSensor.z,
                -relativeSensor.y,
                relativeSensor.w));
            if (Quaternion.Angle(Quaternion.identity, unityRelative) <= Mathf.Max(0f, deadZoneDegrees))
                return Quaternion.identity;
            return Quaternion.SlerpUnclamped(Quaternion.identity, unityRelative, Mathf.Clamp(responseGain, 0.5f, 2f));
        }

        private static Quaternion Normalize(Quaternion value)
        {
            float magnitude = Mathf.Sqrt(value.x * value.x + value.y * value.y + value.z * value.z + value.w * value.w);
            if (magnitude < 0.00001f) return Quaternion.identity;
            float inverse = 1f / magnitude;
            return new Quaternion(value.x * inverse, value.y * inverse, value.z * inverse, value.w * inverse);
        }
    }
}
