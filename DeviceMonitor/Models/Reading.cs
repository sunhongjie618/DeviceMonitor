using System;

namespace DeviceMonitor.Models
{
    /// <summary>
    /// 一次采集的原始值（数据库实体）。按 5 秒一批批量写入，不跟着 500ms 的采集周期走。
    /// </summary>
    public class Reading
    {
        public long Id { get; set; }

        public string PointName { get; set; } = string.Empty;

        public DateTime SampleTime { get; set; }

        public double Value { get; set; }
    }
}
