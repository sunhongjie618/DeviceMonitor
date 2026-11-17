namespace DeviceMonitor.Models
{
    /// <summary>
    /// PLC 通讯参数。会被序列化成本地 JSON。
    /// 命名与 SemaphoreSlim/串口参数一一对应，接入 NModbus 时可直接映射到 SerialPortSettings。
    /// </summary>
    public class PlcConnectionSettings
    {
        public string Mode { get; set; } = "Modbus RTU";
        public string PortName { get; set; } = "COM3";
        public int BaudRate { get; set; } = 9600;
        public int DataBits { get; set; } = 8;
        public string Parity { get; set; } = "None";
        public int StopBits { get; set; } = 1;
        public byte SlaveId { get; set; } = 1;
        public int ReadTimeoutMs { get; set; } = 350;
        public int PollIntervalMs { get; set; } = 500;
    }
}
