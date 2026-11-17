namespace DeviceMonitor.Models
{
    /// <summary>
    /// 一条 Modbus 采集点位。Address 存的是界面上看到的「40001」这种展示地址，
    /// 解析层再按功能码换算成 0 基偏移（保持配置文件和设备手册一一对应，人手抄不会错）。
    /// </summary>
    public class PlcPoint
    {
        public string Name { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string DataType { get; set; } = "UInt16";
        public string Unit { get; set; } = string.Empty;

        /// <summary>CDAB 字序时置 true（绝大多数设备用默认 ABCD=false）。</summary>
        public bool WordSwap { get; set; }

        /// <summary>换算系数：物理量 = 原始值 × Scale（默认 1）。</summary>
        public double Scale { get; set; } = 1;
    }
}
