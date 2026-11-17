using CommunityToolkit.Mvvm.ComponentModel;

namespace DeviceMonitor.Models
{
    /// <summary>
    /// 机台数据模型。采集层写入 Status/PlanCount/FinishedCount 后界面自动刷新。
    /// </summary>
    public partial class MachineModel : ObservableObject
    {
        [ObservableProperty] private string machineName = string.Empty;
        [ObservableProperty] private string status = string.Empty;
        [ObservableProperty] private int planCount;
        [ObservableProperty] private int finishedCount;
        [ObservableProperty] private string orderNo = string.Empty;

        /// <summary>完成百分比（只读，防御 PlanCount 为 0）</summary>
        public double Percent => PlanCount == 0 ? 0 : FinishedCount * 100.0 / PlanCount;

        partial void OnPlanCountChanged(int value) => OnPropertyChanged(nameof(Percent));
        partial void OnFinishedCountChanged(int value) => OnPropertyChanged(nameof(Percent));
    }
}
