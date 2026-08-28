using UnityEngine;

namespace NavegacaoRenal
{
    public sealed class MpuOrientationMapper
    {
        private const float MaximumTiltDegrees = 85f;
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

            // The board is held flat: sensor X is right, Y is forward and Z points up.
            // Its normal carries the two useful joystick tilts and ignores axial yaw drift.
            Vector3 boardNormal = relativeSensor * Vector3.forward;
            float forwardTilt = Mathf.Atan2(-boardNormal.y, boardNormal.z) * Mathf.Rad2Deg;
            float sideTilt = Mathf.Atan2(boardNormal.x, boardNormal.z) * Mathf.Rad2Deg;
            float tiltMagnitude = new Vector2(forwardTilt, sideTilt).magnitude;
            if (tiltMagnitude <= Mathf.Max(0f, deadZoneDegrees))
                return Quaternion.identity;

            float gain = Mathf.Clamp(responseGain, 0.5f, 2f);
            float unityPitch = Mathf.Clamp(-forwardTilt * gain, -MaximumTiltDegrees, MaximumTiltDegrees);
            float unityYaw = Mathf.Clamp(sideTilt * gain, -MaximumTiltDegrees, MaximumTiltDegrees);
            return Quaternion.Euler(unityPitch, unityYaw, 0f);
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
