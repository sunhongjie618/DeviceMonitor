using CommunityToolkit.Mvvm.ComponentModel;

namespace DeviceMonitor.Models
{
    /// <summary>
    /// 设备管理页表格的一行：点位元数据（来自配置文件的点位表）+ 实时值。
    /// 元数据只读，实时值由 500ms 定时器从 PlcPollingService.Latest 拉过来覆盖。
    /// </summary>
    public partial class PointRow : ObservableObject
    {
        public string Name { get; init; } = string.Empty;

        public string Address { get; init; } = string.Empty;

        public string DataType { get; init; } = string.Empty;

        public string Unit { get; init; } = string.Empty;

        /// <summary>格式化后的值文本，例如 "26.5 ℃"、"31 台"，没采到时为 "--"。</summary>
        [ObservableProperty]
        private string valueText = "--";

        [ObservableProperty]
        private string statusText = "无数据";
    }
}
