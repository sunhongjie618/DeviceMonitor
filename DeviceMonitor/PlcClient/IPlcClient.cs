using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeviceMonitor.PlcClient
{
    public interface IPlcClient
    {
        public bool IsConnected { get;}
        public Task ConnectAsync(CancellationToken ct = default);
        public Task DisconnectAsync();

        public Task<ushort[]> ReadHoldingRegisters(byte slaveId, ushort start, ushort length,CancellationToken ct =default);
    }
}
