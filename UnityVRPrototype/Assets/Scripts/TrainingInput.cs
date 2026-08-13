using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using UnityEngine;

[Serializable]
public class TrainingControllerPacket
{
    public int v;
    public long seq;
    public long ms;
    public float[] q;
    public long ticks;
    public int buttons;
    public bool imu_ok;
    public string fw;
}

public struct TrainingInputFrame
{
    public long sequence;
    public long timestampMilliseconds;
    public Quaternion orientation;
    public long encoderTicks;
    public bool actionPressed;
    public bool calibratePressed;
    public bool imuOk;
    public string firmwareVersion;
}

public static class TrainingControllerProtocol
{
    public const int CurrentVersion = 1;

    public static bool TryParse(string line, out TrainingInputFrame frame)
    {
        frame = default;
        if (string.IsNullOrWhiteSpace(line) || line.Length > 512) return false;
        TrainingControllerPacket packet;
        try
        {
            packet = JsonUtility.FromJson<TrainingControllerPacket>(line);
        }
        catch
        {
            return false;
        }
        if (packet == null || packet.v != CurrentVersion || packet.q == null || packet.q.Length != 4)
        {
            return false;
        }
        for (int index = 0; index < packet.q.Length; index++)
        {
            if (float.IsNaN(packet.q[index]) || float.IsInfinity(packet.q[index])) return false;
        }
        Quaternion orientation = new Quaternion(packet.q[1], packet.q[2], packet.q[3], packet.q[0]);
        float magnitudeSquared = orientation.x * orientation.x + orientation.y * orientation.y +
            orientation.z * orientation.z + orientation.w * orientation.w;
        if (magnitudeSquared < 0.25f) return false;
        frame = new TrainingInputFrame
        {
            sequence = packet.seq,
            timestampMilliseconds = packet.ms,
            orientation = Quaternion.Normalize(orientation),
            encoderTicks = packet.ticks,
            actionPressed = (packet.buttons & 1) != 0,
            calibratePressed = (packet.buttons & 2) != 0,
            imuOk = packet.imu_ok,
            firmwareVersion = string.IsNullOrWhiteSpace(packet.fw) ? "unknown" : packet.fw
        };
        return true;
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
    private TrainingInputFrame latest;
    private bool hasFrame;
    private float pitch;
    private float yaw;
    private float roll;
    private float tickAccumulator;
    private long sequence;

    public KeyboardTrainingInput(float millimetersPerTick)
    {
        ticksPerMillimeter = 1f / Mathf.Max(0.001f, millimetersPerTick);
    }

    public bool IsConnected => true;
    public string DisplayName => "Teclado e mouse";
    public string FirmwareVersion => "keyboard-v1";

    public void Tick()
    {
        float yawInput = Input.GetAxisRaw("Mouse X") + Axis(KeyCode.RightArrow, KeyCode.LeftArrow);
        float pitchInput = -Input.GetAxisRaw("Mouse Y") + Axis(KeyCode.DownArrow, KeyCode.UpArrow);
        float rollInput = Axis(KeyCode.E, KeyCode.Q);
        yaw += yawInput * 55f * Time.deltaTime;
        pitch = Mathf.Clamp(pitch + pitchInput * 55f * Time.deltaTime, -80f, 80f);
        roll += rollInput * 70f * Time.deltaTime;
        float advanceMillimeters = Axis(KeyCode.W, KeyCode.S) * 18f * Time.deltaTime;
        tickAccumulator += advanceMillimeters * ticksPerMillimeter;
        latest = new TrainingInputFrame
        {
            sequence = ++sequence,
            timestampMilliseconds = (long)(Time.realtimeSinceStartupAsDouble * 1000.0),
            orientation = Quaternion.Euler(pitch, yaw, roll),
            encoderTicks = (long)Math.Round(tickAccumulator),
            actionPressed = Input.GetKey(KeyCode.Space),
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
    private readonly ConcurrentQueue<TrainingInputFrame> frames = new ConcurrentQueue<TrainingInputFrame>();
    private readonly string requestedPort;
    private Thread readerThread;
    private volatile bool stopRequested;
    private volatile bool connected;
    private long lastPacketUtcTicks;
    private string activePort = "";
    private string firmwareVersion = "unknown";
    private float nextConnectAttempt;

    public SerialControllerInput(string portName)
    {
        requestedPort = string.IsNullOrWhiteSpace(portName) ? "AUTO" : portName.Trim();
    }

    public bool IsConnected
    {
        get
        {
            if (!connected) return false;
            double age = (DateTime.UtcNow.Ticks - Interlocked.Read(ref lastPacketUtcTicks)) / (double)TimeSpan.TicksPerSecond;
            return age <= 0.5;
        }
    }

    public string DisplayName => string.IsNullOrEmpty(activePort) ? "Controle USB procurando..." : $"Controle USB ({activePort})";
    public string FirmwareVersion => firmwareVersion;

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
    }

    public bool TryGetLatestFrame(out TrainingInputFrame frame)
    {
        frame = default;
        bool found = false;
        while (frames.TryDequeue(out TrainingInputFrame candidate))
        {
            frame = candidate;
            found = true;
        }
        return found;
    }

    public void Dispose()
    {
        stopRequested = true;
        if (readerThread != null && readerThread.IsAlive) readerThread.Join(300);
        connected = false;
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
        if (!TrainingControllerProtocol.TryParse(line, out TrainingInputFrame frame)) return;
        frames.Enqueue(frame);
        firmwareVersion = frame.firmwareVersion;
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
