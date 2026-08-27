using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace NavegacaoRenal
{
    public enum Esp32ConnectionStatus
    {
        Stopped,
        Searching,
        Connecting,
        Streaming,
        Error
    }

    public interface IEsp32PacketTransport : IDisposable
    {
        Esp32ConnectionStatus Status { get; }
        string ConnectedPort { get; }
        string LastError { get; }
        float PacketRateHz { get; }
        string[] GetPortNames();
        void Start(string preferredPort = null);
        void Stop();
        bool TryGetLatest(out Esp32MpuPacket packet, out double ageSeconds);
    }

    public sealed class SystemEsp32PacketTransport : IEsp32PacketTransport
    {
        private readonly object sync = new object();
        private Thread worker;
        private Win32SerialPort activePort;
        private bool stopRequested;
        private Esp32ConnectionStatus status = Esp32ConnectionStatus.Stopped;
        private string connectedPort = string.Empty;
        private string lastError = string.Empty;
        private float packetRateHz;
        private Esp32MpuPacket latestPacket;
        private long latestPacketTimestamp;

        public Esp32ConnectionStatus Status { get { lock (sync) return status; } }
        public string ConnectedPort { get { lock (sync) return connectedPort; } }
        public string LastError { get { lock (sync) return lastError; } }
        public float PacketRateHz { get { lock (sync) return packetRateHz; } }

        public string[] GetPortNames()
        {
            try { return Win32SerialPort.GetPortNames(); }
            catch { return Array.Empty<string>(); }
        }

        public void Start(string preferredPort = null)
        {
            Stop();
            lock (sync)
            {
                stopRequested = false;
                latestPacket = null;
                latestPacketTimestamp = 0;
                lastError = string.Empty;
                status = Esp32ConnectionStatus.Searching;
                worker = new Thread(() => WorkerLoop(preferredPort))
                {
                    IsBackground = true,
                    Name = "ESP32 MPU serial reader"
                };
                worker.Start();
            }
        }

        public void Stop()
        {
            Thread thread;
            Win32SerialPort port;
            lock (sync)
            {
                stopRequested = true;
                thread = worker;
                port = activePort;
            }
            try { port?.Close(); } catch { }
            if (thread != null && thread != Thread.CurrentThread) thread.Join(1200);
            lock (sync)
            {
                worker = null;
                activePort = null;
                connectedPort = string.Empty;
                packetRateHz = 0f;
                status = Esp32ConnectionStatus.Stopped;
            }
        }

        public bool TryGetLatest(out Esp32MpuPacket packet, out double ageSeconds)
        {
            long timestamp;
            lock (sync)
            {
                packet = latestPacket;
                timestamp = latestPacketTimestamp;
            }
            if (packet == null || timestamp == 0)
            {
                ageSeconds = double.PositiveInfinity;
                return false;
            }
            ageSeconds = (Stopwatch.GetTimestamp() - timestamp) / (double)Stopwatch.Frequency;
            return true;
        }

        public void Dispose() => Stop();

        private void WorkerLoop(string preferredPort)
        {
            while (!ShouldStop())
            {
                string[] ports = GetPortNames();
                IEnumerable<string> candidates = string.IsNullOrWhiteSpace(preferredPort)
                    ? ports
                    : new[] { preferredPort }.Concat(ports.Where(port => !string.Equals(port, preferredPort, StringComparison.OrdinalIgnoreCase)));
                bool attempted = false;
                foreach (string portName in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    if (ShouldStop()) return;
                    attempted = true;
                    if (TryStreamPort(portName)) break;
                }
                if (!attempted) SetStatus(Esp32ConnectionStatus.Searching, string.Empty, "Nenhuma porta COM disponível.");
                WaitInterruptibly(500);
            }
        }

        private bool TryStreamPort(string portName)
        {
            Win32SerialPort port = null;
            try
            {
                SetStatus(Esp32ConnectionStatus.Connecting, portName, string.Empty);
                port = new Win32SerialPort(portName, 115200, 200);
                lock (sync) activePort = port;
                port.Open();

                long handshakeStarted = Stopwatch.GetTimestamp();
                long lastValid = 0;
                long rateStarted = handshakeStarted;
                int ratePackets = 0;
                bool hasSequence = false;
                uint previousSequence = 0;
                while (!ShouldStop() && port.IsOpen)
                {
                    try
                    {
                        string line = port.ReadLine().Trim();
                        if (!Esp32MpuPacketParser.TryParse(line, out Esp32MpuPacket packet)) continue;
                        if (hasSequence && !Esp32MpuPacketParser.IsNewerSequence(packet.Sequence, previousSequence)) continue;
                        hasSequence = true;
                        previousSequence = packet.Sequence;
                        long now = Stopwatch.GetTimestamp();
                        lastValid = now;
                        ratePackets++;
                        lock (sync)
                        {
                            latestPacket = packet;
                            latestPacketTimestamp = now;
                            connectedPort = portName;
                            status = Esp32ConnectionStatus.Streaming;
                            lastError = string.Empty;
                        }
                        double rateWindow = (now - rateStarted) / (double)Stopwatch.Frequency;
                        if (rateWindow >= 1.0)
                        {
                            lock (sync) packetRateHz = (float)(ratePackets / rateWindow);
                            ratePackets = 0;
                            rateStarted = now;
                        }
                    }
                    catch (TimeoutException)
                    {
                        long now = Stopwatch.GetTimestamp();
                        double handshakeAge = (now - handshakeStarted) / (double)Stopwatch.Frequency;
                        double streamAge = lastValid == 0 ? 0 : (now - lastValid) / (double)Stopwatch.Frequency;
                        if ((lastValid == 0 && handshakeAge > 4.5) || (lastValid != 0 && streamAge > 1.5))
                            throw new TimeoutException("ESP32 não enviou pacotes JSON v2 no tempo esperado.");
                    }
                }
                return true;
            }
            catch (Exception exception)
            {
                if (!ShouldStop()) SetStatus(Esp32ConnectionStatus.Error, string.Empty, exception.Message);
                return false;
            }
            finally
            {
                try { port?.Close(); } catch { }
                lock (sync)
                {
                    if (activePort == port) activePort = null;
                    if (!stopRequested && status == Esp32ConnectionStatus.Streaming)
                        status = Esp32ConnectionStatus.Searching;
                    connectedPort = string.Empty;
                    packetRateHz = 0f;
                }
            }
        }

        private bool ShouldStop() { lock (sync) return stopRequested; }

        private void SetStatus(Esp32ConnectionStatus value, string port, string error)
        {
            lock (sync)
            {
                status = value;
                connectedPort = port ?? string.Empty;
                lastError = error ?? string.Empty;
            }
        }

        private void WaitInterruptibly(int milliseconds)
        {
            int remaining = milliseconds;
            while (remaining > 0 && !ShouldStop())
            {
                int slice = Math.Min(50, remaining);
                Thread.Sleep(slice);
                remaining -= slice;
            }
        }

        private sealed class Win32SerialPort
        {
            private const uint GenericRead = 0x80000000;
            private const uint OpenExisting = 3;
            private const uint PurgeRxClear = 0x0008;
            private static readonly IntPtr InvalidHandle = new IntPtr(-1);

            private readonly string portName;
            private readonly uint baudRate;
            private readonly uint readTimeoutMs;
            private IntPtr handle = InvalidHandle;

            public Win32SerialPort(string portName, uint baudRate, uint readTimeoutMs)
            {
                this.portName = portName;
                this.baudRate = baudRate;
                this.readTimeoutMs = readTimeoutMs;
            }

            public bool IsOpen => handle != InvalidHandle && handle != IntPtr.Zero;

            public static string[] GetPortNames()
            {
                List<string> ports = new List<string>();
                StringBuilder target = new StringBuilder(512);
                for (int index = 1; index <= 256; index++)
                {
                    string candidate = "COM" + index;
                    target.Clear();
                    if (QueryDosDevice(candidate, target, target.Capacity) != 0) ports.Add(candidate);
                }
                return ports.OrderBy(value => int.Parse(value.Substring(3))).ToArray();
            }

            public void Open()
            {
                if (IsOpen) return;
                handle = CreateFile("\\\\.\\" + portName, GenericRead, 0, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);
                if (!IsOpen) throw new InvalidOperationException($"Não foi possível abrir {portName} (erro {Marshal.GetLastWin32Error()}).");

                Dcb dcb = new Dcb { Length = (uint)Marshal.SizeOf<Dcb>() };
                if (!GetCommState(handle, ref dcb)) FailAndClose("ler configuração");
                dcb.BaudRate = baudRate;
                dcb.Flags = 1; // fBinary enabled; parity and hardware flow control disabled.
                dcb.ByteSize = 8;
                dcb.Parity = 0;
                dcb.StopBits = 0;
                if (!SetCommState(handle, ref dcb)) FailAndClose("configurar 115200 8N1");
                CommTimeouts timeouts = new CommTimeouts
                {
                    ReadIntervalTimeout = 50,
                    ReadTotalTimeoutMultiplier = 0,
                    ReadTotalTimeoutConstant = readTimeoutMs,
                    WriteTotalTimeoutMultiplier = 0,
                    WriteTotalTimeoutConstant = 200
                };
                if (!SetCommTimeouts(handle, ref timeouts)) FailAndClose("configurar timeout");
                SetupComm(handle, 4096, 1024);
                PurgeComm(handle, PurgeRxClear);
            }

            public string ReadLine()
            {
                if (!IsOpen) throw new InvalidOperationException("Porta serial fechada.");
                StringBuilder line = new StringBuilder(256);
                long started = Stopwatch.GetTimestamp();
                byte[] buffer = new byte[1];
                while (IsOpen)
                {
                    if (!ReadFile(handle, buffer, 1, out uint bytesRead, IntPtr.Zero))
                        throw new InvalidOperationException($"Falha de leitura serial (erro {Marshal.GetLastWin32Error()}).");
                    if (bytesRead == 1)
                    {
                        char character = (char)buffer[0];
                        if (character == '\n') return line.ToString();
                        if (character != '\r' && line.Length < 511) line.Append(character);
                    }
                    double elapsedMs = (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency;
                    if (elapsedMs >= readTimeoutMs) throw new TimeoutException();
                }
                throw new InvalidOperationException("Porta serial fechada.");
            }

            public void Close()
            {
                IntPtr value = handle;
                handle = InvalidHandle;
                if (value != InvalidHandle && value != IntPtr.Zero) CloseHandle(value);
            }

            private void FailAndClose(string operation)
            {
                int error = Marshal.GetLastWin32Error();
                Close();
                throw new InvalidOperationException($"Falha ao {operation} em {portName} (erro {error}).");
            }

            [StructLayout(LayoutKind.Sequential)]
            private struct Dcb
            {
                public uint Length;
                public uint BaudRate;
                public uint Flags;
                public ushort Reserved;
                public ushort XonLimit;
                public ushort XoffLimit;
                public byte ByteSize;
                public byte Parity;
                public byte StopBits;
                public sbyte XonChar;
                public sbyte XoffChar;
                public sbyte ErrorChar;
                public sbyte EofChar;
                public sbyte EventChar;
                public ushort Reserved1;
            }

            [StructLayout(LayoutKind.Sequential)]
            private struct CommTimeouts
            {
                public uint ReadIntervalTimeout;
                public uint ReadTotalTimeoutMultiplier;
                public uint ReadTotalTimeoutConstant;
                public uint WriteTotalTimeoutMultiplier;
                public uint WriteTotalTimeoutConstant;
            }

            [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            private static extern IntPtr CreateFile(string fileName, uint desiredAccess, uint shareMode,
                IntPtr securityAttributes, uint creationDisposition, uint flagsAndAttributes, IntPtr templateFile);

            [DllImport("kernel32.dll", SetLastError = true)]
            private static extern bool CloseHandle(IntPtr handle);

            [DllImport("kernel32.dll", SetLastError = true)]
            private static extern bool GetCommState(IntPtr handle, ref Dcb dcb);

            [DllImport("kernel32.dll", SetLastError = true)]
            private static extern bool SetCommState(IntPtr handle, ref Dcb dcb);

            [DllImport("kernel32.dll", SetLastError = true)]
            private static extern bool SetCommTimeouts(IntPtr handle, ref CommTimeouts timeouts);

            [DllImport("kernel32.dll", SetLastError = true)]
            private static extern bool SetupComm(IntPtr handle, uint inputQueueSize, uint outputQueueSize);

            [DllImport("kernel32.dll", SetLastError = true)]
            private static extern bool PurgeComm(IntPtr handle, uint flags);

            [DllImport("kernel32.dll", SetLastError = true)]
            private static extern bool ReadFile(IntPtr handle, [Out] byte[] buffer, uint bytesToRead,
                out uint bytesRead, IntPtr overlapped);

            [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            private static extern uint QueryDosDevice(string deviceName, StringBuilder targetPath, int maximumLength);
        }
    }

    public sealed class ReplayEsp32PacketTransport : IEsp32PacketTransport
    {
        private Esp32MpuPacket packet;
        private double ageSeconds;
        public Esp32ConnectionStatus Status { get; private set; } = Esp32ConnectionStatus.Stopped;
        public string ConnectedPort => Status == Esp32ConnectionStatus.Streaming ? "REPLAY" : string.Empty;
        public string LastError { get; private set; } = string.Empty;
        public float PacketRateHz { get; set; } = 50f;
        public string[] GetPortNames() => new[] { "REPLAY" };
        public void Start(string preferredPort = null) => Status = Esp32ConnectionStatus.Streaming;
        public void Stop() => Status = Esp32ConnectionStatus.Stopped;
        public void Dispose() => Stop();

        public void Push(Esp32MpuPacket value, double packetAgeSeconds = 0)
        {
            packet = value;
            ageSeconds = packetAgeSeconds;
            Status = Esp32ConnectionStatus.Streaming;
            LastError = string.Empty;
        }

        public void Disconnect(string error = "Conexão simulada interrompida.")
        {
            ageSeconds = double.PositiveInfinity;
            Status = Esp32ConnectionStatus.Error;
            LastError = error;
        }

        public bool TryGetLatest(out Esp32MpuPacket value, out double age)
        {
            value = packet;
            age = ageSeconds;
            return value != null;
        }
    }
}
