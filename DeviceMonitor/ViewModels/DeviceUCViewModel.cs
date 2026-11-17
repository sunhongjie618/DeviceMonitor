using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeviceMonitor.Models;
using DeviceMonitor.Services;
using System;
using System.Collections.ObjectModel;
using System.Windows.Threading;

namespace DeviceMonitor.ViewModels
{
    /// <summary>
    /// 设备管理页：把「系统设置 → 采集点位」里的点位表摊成一张实时表，
    /// 每 500ms 从 PlcPollingService.Latest 取值，同时显示通讯状态与最近一次采集时间。
    /// </summary>
    public partial class DeviceUCViewModel : ObservableObject
    {
        private readonly PlcPollingService _polling;
        private readonly DispatcherTimer _timer;

        public DeviceUCViewModel(ISettingsService settings, PlcPollingService polling)
        {
            _polling = polling;

            var s = settings.Current;
            PollIntervalText = s.Plc.PollIntervalMs + " ms";
            ConnectionText = $"{s.Plc.PortName} · {s.Plc.BaudRate},N,{s.Plc.DataBits},{s.Plc.StopBits}";
            SlaveText = "从站 " + s.Plc.SlaveId;

            foreach (var p in s.Points)
            {
                Rows.Add(new PointRow
                {
                    Name = p.Name,
                    Address = p.Address,
                    DataType = p.DataType,
                    Unit = p.Unit
                });
            }

            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _timer.Tick += (_, _) => Refresh();
            _timer.Start();

            Refresh();
        }

        public string Title => "设备管理";

        public string Subtitle => "点位表 / 实时值 / 通讯状态";

        /// <summary>点位表 + 实时值。</summary>
        public ObservableCollection<PointRow> Rows { get; } = new();

        [ObservableProperty]
        private string connectionStatus = "未启动";

        [ObservableProperty]
        private string lastUpdateText = "尚未成功采集";

        [ObservableProperty]
        private string pointSummary = "0 / 0";

        [ObservableProperty]
        private string pollIntervalText = "--";

        /// <summary>串口参数简述（COM3 · 9600,N,8,1）。</summary>
        [ObservableProperty]
        private string connectionText = "--";

        [ObservableProperty]
        private string slaveText = "--";

        [RelayCommand]
        private void RefreshNow() => Refresh();

        private void Refresh()
        {
            ConnectionStatus = _polling.Status;
            LastUpdateText = _polling.LastUpdate == default
                ? "尚未成功采集"
                : _polling.LastUpdate.ToString("HH:mm:ss");

            var ok = 0;
            foreach (var row in Rows)
            {
                if (_polling.Latest.TryGetValue(row.Name, out var value))
                {
                    row.ValueText = string.IsNullOrEmpty(row.Unit)
                        ? value.ToString("0.##")
                        : $"{value:0.##} {row.Unit}";
                    row.StatusText = "正常";
                    ok++;
                }
                else
                {
                    row.ValueText = "--";
                    row.StatusText = "无数据";
                }
            }

            PointSummary = $"{ok} / {Rows.Count}";
        }
    }
}
