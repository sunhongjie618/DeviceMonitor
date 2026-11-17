using System;

namespace DeviceMonitor.Models
{
    /// <summary>
    /// 报警落库记录（数据库实体）。EndedAt 为 null 表示仍在进行中。
    /// </summary>
    public class AlarmRecord
    {
        public int Id { get; set; }

        /// <summary>高 / 中 / 低</summary>
        public string Level { get; set; } = "中";

        /// <summary>报警来源（车间温度、硫化氢…）。</summary>
        public string Source { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;

        public DateTime StartedAt { get; set; }

        public DateTime? EndedAt { get; set; }

        public bool IsAcked { get; set; }

        public string? AckedBy { get; set; }

        public DateTime? AckedAt { get; set; }
    }
}
