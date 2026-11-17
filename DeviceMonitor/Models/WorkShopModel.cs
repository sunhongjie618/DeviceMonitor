using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace DeviceMonitor.Models
{
    /// <summary>
    /// 车间汇总。四个数量由 PLC 采集层写入，靠 PropertyChanged 刷新界面；
    /// TotalCount 是计算属性，所以四个数量变化时要额外通知它。
    /// </summary>
    public partial class WorkShopModel : ObservableObject
    {
        // 车间名称
        [ObservableProperty]
        private string workShopName = string.Empty;

        // 作业数量
        [ObservableProperty]
        private int workingCount;

        // 等待数量
        [ObservableProperty]
        private int waitCount;

        // 故障数量
        [ObservableProperty]
        private int wrongCount;

        // 停机数量
        [ObservableProperty]
        private int stopCount;

        partial void OnWorkingCountChanged(int value) => OnPropertyChanged(nameof(TotalCount));
        partial void OnWaitCountChanged(int value) => OnPropertyChanged(nameof(TotalCount));
        partial void OnWrongCountChanged(int value) => OnPropertyChanged(nameof(TotalCount));
        partial void OnStopCountChanged(int value) => OnPropertyChanged(nameof(TotalCount));

        // 总数量(只读)
        public int TotalCount => WorkingCount + WaitCount + WrongCount + StopCount;

        // 该车间下的机台集合（车间详情页用）
        public List<MachineModel> MachineList { get; } = new();
    }
}
