using CommunityToolkit.Mvvm.ComponentModel;

namespace DeviceMonitor.Models
{
    /// <summary>环境信息（采集层写入后界面自动刷新）。</summary>
    public partial class EnvironmentModel : ObservableObject
    {
        [ObservableProperty] private string enItemName = string.Empty;
        [ObservableProperty] private double enItemValue;
    }
}
