using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEngine;

public struct Mpu6050TextSample
{
    public Vector3 acceleration;
    public Vector3 angularVelocity;
    public bool actionPressed;
}

public struct TrainingInputFrame
{
    public long sequence;
    public long timestampMilliseconds;
    public Quaternion orientation;
    public long encoderTicks;
    public float longitudinalTiltDegrees;
    public bool actionPressed;
    public bool calibratePressed;
    public bool imuOk;
    public string firmwareVersion;
}

public sealed class Mpu6050TextProtocol
{
    private const string Number = @"[-+]?(?:\d+(?:\.\d*)?|\.\d+)";
    private static readonly Regex AccelerationPattern = new Regex(
        @"Aceleracao\s*\([^)]*\)\s*:\s*X\s*=\s*(?<x>" + Number + @")\s+Y\s*=\s*(?<y>" + Number + @")\s+Z\s*=\s*(?<z>" + Number + @")",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    );
    private static readonly Regex GyroscopePattern = new Regex(
        @"Giroscopio\s*\([^)]*\)\s*:\s*X\s*=\s*(?<x>" + Number + @")\s+Y\s*=\s*(?<y>" + Number + @")\s+Z\s*=\s*(?<z>" + Number + @")",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    );

    private Vector3 acceleration;
    private Vector3 angularVelocity;
    private bool hasAcceleration;
    private bool hasAngularVelocity;

    public bool AcceptLine(string line, out Mpu6050TextSample sample)
    {
        sample = default;
        if (string.IsNullOrWhiteSpace(line) || line.Length > 512) return false;

        Match accelerationMatch = AccelerationPattern.Match(line);
        if (accelerationMatch.Success)
        {
            if (!TryReadVector(accelerationMatch, out acceleration)) return false;
            hasAcceleration = true;
            hasAngularVelocity = false;
            return false;
        }

        Match gyroscopeMatch = GyroscopePattern.Match(line);
        if (gyroscopeMatch.Success)
        {
            if (!TryReadVector(gyroscopeMatch, out angularVelocity)) return false;
            hasAngularVelocity = true;
            return false;
        }

        int actionLabel = line.IndexOf("Agarrando", StringComparison.OrdinalIgnoreCase);
        if (actionLabel < 0 || !hasAcceleration || !hasAngularVelocity) return false;
        int separator = line.IndexOf(':', actionLabel);
        if (separator < 0) return false;
        string value = line.Substring(separator + 1).Trim();
        bool actionPressed;
        if (value.StartsWith("SIM", StringComparison.OrdinalIgnoreCase)) actionPressed = true;
        else if (value.StartsWith("NAO", StringComparison.OrdinalIgnoreCase) ||
                 value.StartsWith("NÃO", StringComparison.OrdinalIgnoreCase)) actionPressed = false;
        else return false;

        sample = new Mpu6050TextSample
        {
            acceleration = acceleration,
            angularVelocity = angularVelocity,
            actionPressed = actionPressed
        };
        hasAcceleration = false;
        hasAngularVelocity = false;
        return true;
    }

    private static bool TryReadVector(Match match, out Vector3 value)
    {
        value = default;
        if (!float.TryParse(match.Groups["x"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float x) ||
            !float.TryParse(match.Groups["y"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float y) ||
            !float.TryParse(match.Groups["z"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float z))
        {
            return false;
        }
        if (!IsFinite(x) || !IsFinite(y) || !IsFinite(z)) return false;
        value = new Vector3(x, y, z);
        return true;
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}

public static class TrainingInputMath
{
    public static float TicksToMeters(long tickDelta, float millimetersPerTick)
    {
        return tickDelta * millimetersPerTick * 0.001f;
    }

    public static Quaternion RelativeOrientation(Quaternion neutral, Quaternion current)
    {
        return Quaternion.Inverse(neutral) * current;
    }

    public static float MouseRotationDegrees(float rawDelta, float sensitivity)
    {
        return rawDelta * Mathf.Clamp(sensitivity, 0.5f, 4f);
    }

    public static float KeyboardRotationDegrees(float input, float degreesPerSecond, float deltaTime)
    {
        return input * degreesPerSecond * Mathf.Max(0f, deltaTime);
    }

    public static float LongitudinalTiltDegrees(Vector3 acceleration)
    {
        if (acceleration.sqrMagnitude < 0.0001f) return 0f;
        return Mathf.Atan2(-acceleration.x, Mathf.Sqrt(acceleration.y * acceleration.y + acceleration.z * acceleration.z)) * Mathf.Rad2Deg;
    }

    public static float TiltToAdvance(float relativeTiltDegrees, bool inverted, float deadZoneDegrees = 8f, float fullSpeedDegrees = 30f)
    {
        float sign = inverted ? -1f : 1f;
        float signedTilt = Mathf.DeltaAngle(0f, relativeTiltDegrees) * sign;
        float magnitude = Mathf.Abs(signedTilt);
        float deadZone = Mathf.Max(0f, deadZoneDegrees);
        float fullSpeed = Mathf.Max(deadZone + 0.01f, fullSpeedDegrees);
        if (magnitude <= deadZone) return 0f;
        float normalized = Mathf.InverseLerp(deadZone, fullSpeed, magnitude);
        return Mathf.Sign(signedTilt) * normalized;
    }

    public static bool IsActionPressedEdge(bool current, bool previous)
    {
        return current && !previous;
    }
}

public interface ITrainingInputSource : IDisposable
{
    bool IsConnected { get; }
    string DisplayName { get; }
    string FirmwareVersion { get; }
    void Tick();
    bool TryGetLatestFrame(out TrainingInputFrame frame);
}

public sealed class KeyboardTrainingInput : ITrainingInputSource
{
    private readonly float ticksPerMillimeter;
    private readonly float mouseSensitivity;
    private TrainingInputFrame latest;
    private bool hasFrame;
    private float pitch;
    private float yaw;
    private float roll;
    private float tickAccumulator;
    private long sequence;

    public KeyboardTrainingInput(float millimetersPerTick, float mouseSensitivity = 2f)
    {
        ticksPerMillimeter = 1f / Mathf.Max(0.001f, millimetersPerTick);
        this.mouseSensitivity = Mathf.Clamp(mouseSensitivity, 0.5f, 4f);
    }

    public bool IsConnected => true;
    public string DisplayName => "Teclado e mouse";
    public string FirmwareVersion => "keyboard-v1";

    public void Tick()
    {
        float yawInput = TrainingInputMath.MouseRotationDegrees(Input.GetAxisRaw("Mouse X"), mouseSensitivity) +
            TrainingInputMath.KeyboardRotationDegrees(Axis(KeyCode.RightArrow, KeyCode.LeftArrow), 70f, Time.deltaTime);
        float pitchInput = TrainingInputMath.MouseRotationDegrees(-Input.GetAxisRaw("Mouse Y"), mouseSensitivity) +
            TrainingInputMath.KeyboardRotationDegrees(Axis(KeyCode.DownArrow, KeyCode.UpArrow), 70f, Time.deltaTime);
        float rollInput = Axis(KeyCode.E, KeyCode.Q);
        yaw += yawInput;
        pitch = Mathf.Clamp(pitch + pitchInput, -80f, 80f);
        roll += rollInput * 70f * Time.deltaTime;
        float keyboardAdvance = Axis(KeyCode.W, KeyCode.S);
        float mouseAdvance = (Input.GetMouseButton(0) ? 1f : 0f) - (Input.GetMouseButton(1) ? 1f : 0f);
        float advanceMillimeters = Mathf.Clamp(keyboardAdvance + mouseAdvance, -1f, 1f) * 18f * Time.deltaTime;
        tickAccumulator += advanceMillimeters * ticksPerMillimeter;
        latest = new TrainingInputFrame
        {
            sequence = ++sequence,
            timestampMilliseconds = (long)(Time.realtimeSinceStartupAsDouble * 1000.0),
            orientation = Quaternion.Euler(pitch, yaw, roll),
            encoderTicks = (long)Math.Round(tickAccumulator),
            actionPressed = Input.GetKey(KeyCode.Space) || Input.GetMouseButton(2),
            calibratePressed = Input.GetKey(KeyCode.C),
            imuOk = true,
            firmwareVersion = FirmwareVersion
        };
        hasFrame = true;
    }

    public bool TryGetLatestFrame(out TrainingInputFrame frame)
    {
        frame = latest;
        bool result = hasFrame;
        hasFrame = false;
        return result;
    }

    public void Dispose() { }

    private static float Axis(KeyCode positive, KeyCode negative)
    {
        return (Input.GetKey(positive) ? 1f : 0f) - (Input.GetKey(negative) ? 1f : 0f);
    }
}

public sealed class SerialControllerInput : ITrainingInputSource
{
    public const double ConnectionTimeoutSeconds = 2.0;
    public const string ExperimentalFirmwareVersion = "mpu6050-text-test";

    private readonly ConcurrentQueue<Mpu6050TextSample> samples = new ConcurrentQueue<Mpu6050TextSample>();
    private readonly string requestedPort;
    private readonly Mpu6050TextProtocol parser = new Mpu6050TextProtocol();
    private Thread readerThread;
    private volatile bool stopRequested;
    private volatile bool connected;
    private long lastPacketUtcTicks;
    private string activePort = "";
    private float nextConnectAttempt;
    private Vector3 latestAcceleration;
    private Vector3 latestAngularVelocity;
    private bool latestActionPressed;
    private bool hasSensorSample;
    private float pitch;
    private float yaw;
    private float roll;
    private long sequence;
    private TrainingInputFrame generatedFrame;
    private bool hasGeneratedFrame;

    public SerialControllerInput(string portName)
    {
        requestedPort = string.IsNullOrWhiteSpace(portName) ? "AUTO" : portName.Trim();
    }

    public bool IsConnected
    {
        get
        {
            if (!connected) return false;
            return IsPacketFresh(Interlocked.Read(ref lastPacketUtcTicks), DateTime.UtcNow.Ticks);
        }
    }

    public string DisplayName => string.IsNullOrEmpty(activePort) ? "Controle USB procurando..." : $"Controle USB ({activePort})";
    public string FirmwareVersion => ExperimentalFirmwareVersion;

    public static bool IsPacketFresh(long lastPacketTicks, long nowTicks)
    {
        if (lastPacketTicks <= 0 || nowTicks < lastPacketTicks) return false;
        double age = (nowTicks - lastPacketTicks) / (double)TimeSpan.TicksPerSecond;
        return age <= ConnectionTimeoutSeconds;
    }

    public void Tick()
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        if ((readerThread == null || !readerThread.IsAlive) && Time.unscaledTime >= nextConnectAttempt)
        {
            nextConnectAttempt = Time.unscaledTime + 2f;
            stopRequested = false;
            readerThread = new Thread(ReadLoop) { IsBackground = true, Name = "Ureteroscope serial reader" };
            readerThread.Start();
        }
#endif

        bool receivedSample = false;
        while (samples.TryDequeue(out Mpu6050TextSample sample))
        {
            latestAcceleration = sample.acceleration;
            latestAngularVelocity = sample.angularVelocity;
            latestActionPressed = sample.actionPressed;
            receivedSample = true;
        }
        if (receivedSample && !hasSensorSample)
        {
            float accelerationPitch = TrainingInputMath.LongitudinalTiltDegrees(latestAcceleration);
            float accelerationRoll = Mathf.Atan2(latestAcceleration.y, latestAcceleration.z) * Mathf.Rad2Deg;
            pitch = accelerationPitch;
            roll = accelerationRoll;
            hasSensorSample = true;
        }
        if (!hasSensorSample || !IsConnected)
        {
            hasGeneratedFrame = false;
            return;
        }

        float deltaTime = Mathf.Max(0f, Time.unscaledDeltaTime);
        const float gyroDeadZoneRadians = 0.03f;
        Vector3 gyro = new Vector3(
            ApplyDeadZone(latestAngularVelocity.x, gyroDeadZoneRadians),
            ApplyDeadZone(latestAngularVelocity.y, gyroDeadZoneRadians),
            ApplyDeadZone(latestAngularVelocity.z, gyroDeadZoneRadians)
        );
        pitch += gyro.x * Mathf.Rad2Deg * deltaTime;
        yaw += gyro.y * Mathf.Rad2Deg * deltaTime;
        roll += gyro.z * Mathf.Rad2Deg * deltaTime;

        float magnitude = latestAcceleration.magnitude;
        if (magnitude >= 2f && magnitude <= 20f)
        {
            float accelerationPitch = TrainingInputMath.LongitudinalTiltDegrees(latestAcceleration);
            float accelerationRoll = Mathf.Atan2(latestAcceleration.y, latestAcceleration.z) * Mathf.Rad2Deg;
            float correction = 1f - Mathf.Exp(-2f * deltaTime);
            pitch = Mathf.LerpAngle(pitch, accelerationPitch, correction);
            roll = Mathf.LerpAngle(roll, accelerationRoll, correction);
        }

        generatedFrame = new TrainingInputFrame
        {
            sequence = ++sequence,
            timestampMilliseconds = (long)(Time.realtimeSinceStartupAsDouble * 1000.0),
            orientation = Quaternion.Euler(pitch, yaw, roll),
            encoderTicks = 0,
            longitudinalTiltDegrees = TrainingInputMath.LongitudinalTiltDegrees(latestAcceleration),
            actionPressed = latestActionPressed,
            calibratePressed = false,
            imuOk = true,
            firmwareVersion = ExperimentalFirmwareVersion
        };
        hasGeneratedFrame = true;
    }

    public bool TryGetLatestFrame(out TrainingInputFrame frame)
    {
        frame = generatedFrame;
        bool result = hasGeneratedFrame;
        hasGeneratedFrame = false;
        return result;
    }

    public void Dispose()
    {
        stopRequested = true;
        if (readerThread != null && readerThread.IsAlive) readerThread.Join(300);
        connected = false;
    }

    private static float ApplyDeadZone(float value, float deadZone)
    {
        return Mathf.Abs(value) < deadZone ? 0f : value;
    }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    private void ReadLoop()
    {
        string[] ports = string.Equals(requestedPort, "AUTO", StringComparison.OrdinalIgnoreCase)
            ? GetWindowsSerialPorts()
            : new[] { requestedPort };
        foreach (string portName in ports)
        {
            if (stopRequested) break;
            if (ReadWindowsPort(portName)) return;
        }
    }

    private bool ReadWindowsPort(string portName)
    {
        IntPtr handle = CreateFile(
            "\\\\.\\" + portName,
            GenericRead | GenericWrite,
            0,
            IntPtr.Zero,
            OpenExisting,
            0,
            IntPtr.Zero
        );
        if (handle == InvalidHandleValue) return false;
        try
        {
            Dcb dcb = new Dcb { length = (uint)Marshal.SizeOf<Dcb>() };
            if (!GetCommState(handle, ref dcb)) return false;
            dcb.baudRate = 115200;
            dcb.flags = 1;
            dcb.byteSize = 8;
            dcb.parity = 0;
            dcb.stopBits = 0;
            if (!SetCommState(handle, ref dcb)) return false;
            CommTimeouts timeouts = new CommTimeouts
            {
                readIntervalTimeout = 50,
                readTotalTimeoutConstant = 50,
                readTotalTimeoutMultiplier = 0
            };
            SetCommTimeouts(handle, ref timeouts);
            activePort = portName;
            byte[] buffer = new byte[256];
            StringBuilder line = new StringBuilder(512);
            while (!stopRequested)
            {
                if (!ReadFile(handle, buffer, buffer.Length, out int count, IntPtr.Zero)) break;
                for (int index = 0; index < count; index++)
                {
                    char character = (char)buffer[index];
                    if (character == '\n')
                    {
                        AcceptLine(line.ToString().Trim());
                        line.Clear();
                    }
                    else if (character != '\r' && line.Length < 512)
                    {
                        line.Append(character);
                    }
                }
            }
            return stopRequested;
        }
        finally
        {
            CloseHandle(handle);
            connected = false;
            activePort = "";
        }
    }

    private void AcceptLine(string line)
    {
        if (!parser.AcceptLine(line, out Mpu6050TextSample sample)) return;
        samples.Enqueue(sample);
        Interlocked.Exchange(ref lastPacketUtcTicks, DateTime.UtcNow.Ticks);
        connected = true;
    }

    private static string[] GetWindowsSerialPorts()
    {
        List<string> ports = new List<string>();
        StringBuilder target = new StringBuilder(256);
        for (int number = 1; number <= 64; number++)
        {
            string port = "COM" + number;
            target.Clear();
            if (QueryDosDevice(port, target, target.Capacity) != 0)
            {
                ports.Add(port);
            }
        }
        return ports.ToArray();
    }

    private const uint GenericRead = 0x80000000;
    private const uint GenericWrite = 0x40000000;
    private const uint OpenExisting = 3;
    private static readonly IntPtr InvalidHandleValue = new IntPtr(-1);

    [StructLayout(LayoutKind.Sequential)]
    private struct Dcb
    {
        public uint length;
        public uint baudRate;
        public uint flags;
        public ushort reserved;
        public ushort xonLimit;
        public ushort xoffLimit;
        public byte byteSize;
        public byte parity;
        public byte stopBits;
        public sbyte xonChar;
        public sbyte xoffChar;
        public sbyte errorChar;
        public sbyte eofChar;
        public sbyte eventChar;
        public ushort reserved1;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CommTimeouts
    {
        public uint readIntervalTimeout;
        public uint readTotalTimeoutMultiplier;
        public uint readTotalTimeoutConstant;
        public uint writeTotalTimeoutMultiplier;
        public uint writeTotalTimeoutConstant;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateFile(string name, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetCommState(IntPtr handle, ref Dcb dcb);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetCommState(IntPtr handle, ref Dcb dcb);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetCommTimeouts(IntPtr handle, ref CommTimeouts timeouts);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool ReadFile(IntPtr handle, byte[] buffer, int bytesToRead, out int bytesRead, IntPtr overlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint QueryDosDevice(string deviceName, StringBuilder targetPath, int maximumLength);
#endif
}
