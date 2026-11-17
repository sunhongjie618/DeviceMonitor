using CommunityToolkit.Mvvm.ComponentModel;

namespace DeviceMonitor.Models
{
    /// <summary>
    /// 设备监控项。采集层把值写进 Value，界面靠 PropertyChanged 自动刷新
    /// （所以必须是 ObservableObject，不能是普通自动属性）。
    /// </summary>
    public partial class DeviceModel : ObservableObject
    {
        // 设备监控项名称，如「电压(V)」
        [ObservableProperty]
        private string deviceItem = string.Empty;

        // 当前值
        [ObservableProperty]
        private double value;
    }
}
