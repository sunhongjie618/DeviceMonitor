using DeviceMonitor.Models;
using Modbus.Device;
using System;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;

namespace DeviceMonitor.PlcClient
{
    public class ModbusRtuPlcClient : IPlcClient, IDisposable
    {
        private readonly SemaphoreSlim _gate = new(1, 1);   // 串口非线程安全，必须串行化
        private SerialPort? _port;
        private IModbusSerialMaster? _master;
        private readonly PlcConnectionSettings _settings;
        private int _disposed;

        public bool IsConnected => _port?.IsOpen == true;

        public ModbusRtuPlcClient(PlcConnectionSettings settings)
        {
            _settings = settings;
        }

        public async Task ConnectAsync(CancellationToken ct = default)
        {
            await _gate.WaitAsync(ct);
            try
            {
                ThrowIfDisposed();
                ClosePortInternal();

                _port = new SerialPort(
                    _settings.PortName,
                    _settings.BaudRate,
                    ParseParity(_settings.Parity),
                    _settings.DataBits,
                    ParseStopBits(_settings.StopBits))
                {
                    ReadTimeout = _settings.ReadTimeoutMs,   // 350
                    WriteTimeout = _settings.ReadTimeoutMs,
                    ReadBufferSize = 4096,
                };
                _port.Open();

                // 创建 Modbus RTU 通讯对象（NModbus4 的静态工厂入口）
                _master = ModbusSerialMaster.CreateRtu(_port);
                _master.Transport.ReadTimeout = _settings.ReadTimeoutMs;
                _master.Transport.Retries = 1;
                _master.Transport.WaitToRetryMilliseconds = 50;
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task DisconnectAsync()
        {
            await _gate.WaitAsync();
            try { ClosePortInternal(); }
            finally { _gate.Release(); }
        }

        public async Task<ushort[]> ReadHoldingRegisters(byte slaveId, ushort start, ushort length, CancellationToken ct = default)
        {
            await _gate.WaitAsync(ct);
            try
            {
                ThrowIfDisposed();

                // 用局部变量，避免和 Dispose 的 ClosePortInternal 竞争读到半关的端口
                var master = _master;
                if (master is null || !IsConnected)
                    throw new InvalidOperationException("PLC 未连接");

                return await master.ReadHoldingRegistersAsync(slaveId, start, length);
            }
            finally
            {
                _gate.Release();
            }
        }

        public void Dispose()
        {
            // 防重入：只允许关一次
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            _gate.Wait();   // 等正在进行的读写完成（读超时 350ms，不会无限等）
            try { ClosePortInternal(); }
            finally { _gate.Release(); }

            _gate.Dispose();
        }

        private void ThrowIfDisposed()
        {
            if (Volatile.Read(ref _disposed) != 0)
                throw new ObjectDisposedException(nameof(ModbusRtuPlcClient));
        }

        private static Parity ParseParity(string? text) => (text ?? string.Empty).Trim().ToUpperInvariant() switch
        {
            "EVEN" or "E" => Parity.Even,
            "ODD" or "O" => Parity.Odd,
            "MARK" or "M" => Parity.Mark,
            "SPACE" or "S" => Parity.Space,
            _ => Parity.None
        };

        private static StopBits ParseStopBits(int bits) => bits switch
        {
            2 => StopBits.Two,
            3 => StopBits.OnePointFive,   // 1.5 位用 3 表示
            _ => StopBits.One
        };

        private void ClosePortInternal()
        {
            var master = _master;
            _master = null;
            if (master is not null)
            {
                try { master.Dispose(); } catch { /* 关闭失败不抛，避免掩盖真实错误 */ }
            }

            var port = _port;
            _port = null;
            CloseDispose(port);
        }

        private static void CloseDispose(SerialPort? port)
        {
            if (port is null) return;
            try { if (port.IsOpen) port.Close(); } catch { }
            try { port.Dispose(); } catch { }
        }
    }
}
