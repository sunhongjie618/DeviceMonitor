namespace DeviceMonitor.Models
{
    /// <summary>
    /// 数据报表的一行（按时段）。数值来自 SQLite 里「当日产量 / 不良计数」两个累计点位的按小时做差；
    /// 该小时没有采集记录时显示「--」而不是 0，避免把「没数据」误读成「产量为 0」。
    /// </summary>
    public class ReportRow
    {
        public string Hour { get; init; } = string.Empty;

        public int Produce { get; init; }

        public int Bad { get; init; }

        /// <summary>该时段是否有采集数据</summary>
        public bool HasData { get; init; }

        public string ProduceText => HasData ? Produce.ToString("N0") : "--";

        public string BadText => HasData ? Bad.ToString("N0") : "--";

        public double PassRate => Produce <= 0 ? 0 : (Produce - Bad) * 100.0 / Produce;

        public string PassRateText => HasData && Produce > 0 ? PassRate.ToString("0.0") + "%" : "--";
    }
}
