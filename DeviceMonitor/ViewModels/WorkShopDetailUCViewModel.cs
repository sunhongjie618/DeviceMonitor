using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeviceMonitor.Models;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Threading;

namespace DeviceMonitor.ViewModels
{
    /// <summary>
    /// 车间详情页视图模型：由某个 WorkShopModel 驱动，负责机台列表展示与状态筛选。
    /// 机台状态/计划/完成由采集层每 1.5 秒刷新一次；这里用 800ms 的定时器检测
    /// 「当前筛选命中数量」有没有变化，变了才重建列表，避免每 800ms 清空重填 80 个控件。
    /// </summary>
    public partial class WorkShopDetailUCViewModel : ObservableObject, IDisposable
    {
        private readonly WorkShopModel _workShop;
        private readonly DispatcherTimer _timer;

        public WorkShopDetailUCViewModel(WorkShopModel workShop)
        {
            _workShop = workShop;
            _workShop.PropertyChanged += OnWorkShopChanged;

            ApplyFilter();

            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(800) };
            _timer.Tick += (_, _) => RefreshIfNeeded();
            _timer.Start();
        }

        public string WorkShopName => _workShop.WorkShopName;
        public int TotalCount => _workShop.TotalCount;
        public int WorkingCount => _workShop.WorkingCount;
        public int WaitCount => _workShop.WaitCount;
        public int WrongCount => _workShop.WrongCount;
        public int StopCount => _workShop.StopCount;

        /// <summary>当前筛选状态：全部 / 作业中 / 等待 / 故障 / 停机</summary>
        [ObservableProperty]
        private string statusFilter = "全部";

        /// <summary>筛选后的机台列表，绑定到 ItemsControl</summary>
        public ObservableCollection<MachineModel> MachineList { get; } = new();

        partial void OnStatusFilterChanged(string value) => ApplyFilter();

        [RelayCommand]
        private void ChangeFilter(string? status) => StatusFilter = status ?? "全部";

        private void OnWorkShopChanged(object? sender, PropertyChangedEventArgs e)
        {
            OnPropertyChanged(nameof(TotalCount));
            OnPropertyChanged(nameof(WorkingCount));
            OnPropertyChanged(nameof(WaitCount));
            OnPropertyChanged(nameof(WrongCount));
            OnPropertyChanged(nameof(StopCount));
        }

        private void RefreshIfNeeded()
        {
            if (ExpectedCount() != MachineList.Count)
                ApplyFilter();
        }

        private int ExpectedCount() => StatusFilter == "全部"
            ? _workShop.MachineList.Count
            : _workShop.MachineList.Count(m => m.Status == StatusFilter);

        private void ApplyFilter()
        {
            MachineList.Clear();

            var source = StatusFilter == "全部"
                ? _workShop.MachineList
                : _workShop.MachineList.Where(m => m.Status == StatusFilter);

            foreach (var machine in source)
                MachineList.Add(machine);
        }

        public void Dispose()
        {
            _timer.Stop();
            _workShop.PropertyChanged -= OnWorkShopChanged;
        }
    }
}
