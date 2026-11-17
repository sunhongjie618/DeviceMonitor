using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace DeviceMonitor.Models
{
    /// <summary>
    /// 报警中心的一条记录。历史记录来自 MonitorDataService（写死），
    /// 实时记录由 AlarmUCViewModel 按环境阈值的越限检测生成。
    /// </summary>
    public partial class AlarmItem : ObservableObject
    {
        public int Id { get; init; }

        /// <summary>来源：车间温度 / 湿度 / PM2.5 / 硫化氢 / 历史记录。</summary>
        public string Source { get; init; } = string.Empty;

        public string Message { get; init; } = string.Empty;

        /// <summary>高 / 中 / 低。</summary>
        public string Level { get; init; } = "低";

        public DateTime StartedAt { get; init; }

        /// <summary>结束时间；null 表示报警仍在进行中。</summary>
        public DateTime? EndedAt { get; set; }

        [ObservableProperty]
        private bool isAcked;

        [ObservableProperty]
        private string durationText = "--";

        public string TimeText => StartedAt.ToString("MM-dd HH:mm:ss");

        public string StatusText => EndedAt is null ? "进行中" : "已恢复";

        /// <summary>整点后刷新一次，不依赖通知（列表里只是展示用）。</summary>
        public void RefreshDuration(DateTime now)
        {
            var end = EndedAt ?? now;
            var span = end - StartedAt;
            if (span < TimeSpan.Zero) span = TimeSpan.Zero;
            DurationText = span.TotalHours >= 1
                ? $"{(int)span.TotalHours}小时{span.Minutes}分"
                : $"{span.Minutes}分{span.Seconds}秒";
        }
    }
}
