namespace DeviceMonitor.Models
{
    /// <summary>
    /// 第三批：机台级点位块。每台机占 3 个连续寄存器：状态 / 计划数 / 完成数。
    /// 一个车间一块，块内第 i 台机的状态寄存器偏移 = 3 * i。
    /// </summary>
    public class MachineBlock
    {
        /// <summary>车间名称，必须与 WorkShopModel.WorkShopName 一致</summary>
        public string WorkshopName { get; set; } = string.Empty;

        /// <summary>块起始地址（1 基展示地址，如 40101 表示 0 基偏移 100）</summary>
        public string BaseAddress { get; set; } = string.Empty;

        /// <summary>该车间的机台数量</summary>
        public int MachineCount { get; set; }
    }
}
