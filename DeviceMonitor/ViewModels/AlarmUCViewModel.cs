using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeviceMonitor.Models;
using DeviceMonitor.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace DeviceMonitor.ViewModels
{
    /// <summary>
    /// 报警中心：一个轻量报警引擎。
    /// 历史记录来自 MonitorDataService；实时报警由每秒一次的环境越限检测生成，
    /// 越限时开一条（EndedAt 为 null 表示进行中），恢复正常时收尾，同一来源不会重复开单。
    /// </summary>
    public partial class AlarmUCViewModel : ObservableObject
    {
        private sealed class Rule
        {
            public string PointName = string.Empty;
            public string Source = string.Empty;
            public double Low;
            public double High;
            public string Level = "中";
            public string Unit = string.Empty;
        }

        private readonly PlcPollingService _polling;
        private readonly ISettingsService _settings;
        private readonly IDataStore _store;
        private readonly CurrentUserService _currentUser;
        private readonly DispatcherTimer _timer;

        /// <summary>UI 行 → 数据库主键（落库成功后才有值）。</summary>
        private readonly Dictionary<AlarmItem, int> _dbIds = new();

        /// <summary>key = 来源名，value = 正在进行的报警。</summary>
        private readonly Dictionary<string, AlarmItem> _active = new(StringComparer.Ordinal);

        private readonly List<AlarmItem> _all = new();

        private int _nextId;

        public AlarmUCViewModel(MonitorDataService data, ISettingsService settings, PlcPollingService polling,
                                IDataStore store, CurrentUserService currentUser)
        {
            _polling = polling;
            _settings = settings;
            _store = store;
            _currentUser = currentUser;

            // 历史报警（与大屏「报警记录」同一份数据，只读、已确认）
            foreach (var a in data.AlarmList)
            {
                var started = ParseTime(a.Time);
                _all.Add(new AlarmItem
                {
                    Id = ++_nextId,
                    Source = "历史记录",
                    Message = a.Msg,
                    Level = "中",
                    StartedAt = started,
                    EndedAt = started.AddSeconds(Math.Max(a.Duration, 1)),
                    IsAcked = true
                });
            }

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += (_, _) => Tick();
            _timer.Start();

            Tick();
        }

        public string Title => "报警中心";

        public string Subtitle => "越限报警 / 确认处理 / 统计";

        /// <summary>按当前筛选显示的行。</summary>
        public ObservableCollection<AlarmItem> Alarms { get; } = new();

        [ObservableProperty]
        private string filter = "全部";

        [ObservableProperty]
        private string totalText = "0";

        [ObservableProperty]
        private string unackedText = "0";

        [ObservableProperty]
        private string activeText = "0";

        [ObservableProperty]
        private string thresholdText = string.Empty;

        private static DateTime ParseTime(string text) =>
            DateTime.TryParse(text, out var t) ? t : DateTime.Now;

        private IEnumerable<Rule> BuildRules()
        {
            var e = _settings.Current.Environment;
            yield return new Rule { PointName = "车间温度", Source = "车间温度", Low = e.TemperatureLow, High = e.TemperatureHigh, Level = "高", Unit = "℃" };
            yield return new Rule { PointName = "车间湿度", Source = "车间湿度", Low = e.HumidityLow, High = e.HumidityHigh, Level = "中", Unit = "%" };
            yield return new Rule { PointName = "PM2.5", Source = "PM2.5", Low = e.Pm25Low, High = e.Pm25High, Level = "中", Unit = "µg/m³" };
            yield return new Rule { PointName = "硫化氢", Source = "硫化氢", Low = e.H2sLow, High = e.H2sHigh, Level = "高", Unit = "PPM" };
        }

        private void Tick()
        {
            var now = DateTime.Now;

            foreach (var rule in BuildRules())
            {
                if (!_polling.Latest.TryGetValue(rule.PointName, out var value))
                    continue;

                var outOfRange = value < rule.Low || value > rule.High;
                _active.TryGetValue(rule.Source, out var activeItem);

                if (outOfRange && activeItem is null)
                {
                    var item = new AlarmItem
                    {
                        Id = ++_nextId,
                        Source = rule.Source,
                        Message = value > rule.High
                            ? $"{rule.Source}超上限（{value:0.##}{rule.Unit} > {rule.High:0.##}{rule.Unit}）"
                            : $"{rule.Source}低于下限（{value:0.##}{rule.Unit} < {rule.Low:0.##}{rule.Unit}）",
                        Level = rule.Level,
                        StartedAt = now
                    };
                    _active[rule.Source] = item;
                    _all.Insert(0, item);

                    // 报警一开就落库（失败不影响界面，异常已在内部吞掉）
                    _ = PersistOpenAsync(item);
                }
                else if (!outOfRange && activeItem is not null)
                {
                    activeItem.EndedAt = now;
                    _active.Remove(rule.Source);
                    _ = PersistCloseAsync(activeItem, now);
                }
            }

            foreach (var item in _all)
                item.RefreshDuration(now);

            UpdateCounters();
            ApplyFilter();
        }

        private void UpdateCounters()
        {
            TotalText = _all.Count.ToString();
            UnackedText = _all.Count(a => !a.IsAcked).ToString();
            ActiveText = _active.Count.ToString();

            var e = _settings.Current.Environment;
            ThresholdText = $"温度 {e.TemperatureLow}~{e.TemperatureHigh}℃ · 湿度 {e.HumidityLow}~{e.HumidityHigh}%";
        }

        private void ApplyFilter()
        {
            IEnumerable<AlarmItem> query = Filter switch
            {
                "未确认" => _all.Where(a => !a.IsAcked),
                "已确认" => _all.Where(a => a.IsAcked),
                "进行中" => _all.Where(a => a.EndedAt is null),
                _ => _all
            };

            Alarms.Clear();
            foreach (var item in query)
                Alarms.Add(item);
        }

        [RelayCommand]
        private void SetFilter(string? value)
        {
            Filter = string.IsNullOrEmpty(value) ? "全部" : value;
            ApplyFilter();
        }

        [RelayCommand]
        private void Ack(AlarmItem? item)
        {
            if (item is null || item.IsAcked) return;
            item.IsAcked = true;
            _ = PersistAckAsync(item);
            UpdateCounters();
            ApplyFilter();
        }

        [RelayCommand]
        private void AckAll()
        {
            foreach (var item in _all)
            {
                if (item.IsAcked) continue;
                item.IsAcked = true;
                _ = PersistAckAsync(item);
            }
            UpdateCounters();
            ApplyFilter();
        }

        private async Task PersistOpenAsync(AlarmItem item)
        {
            try
            {
                var id = await _store.AddAlarmAsync(new AlarmRecord
                {
                    Level = item.Level,
                    Source = item.Source,
                    Message = item.Message,
                    StartedAt = item.StartedAt
                });
                _dbIds[item] = id;
            }
            catch
            {
                // 数据库不可用时报警界面照常工作
            }
        }

        private async Task PersistCloseAsync(AlarmItem item, DateTime endedAt)
        {
            try
            {
                if (_dbIds.TryGetValue(item, out var id))
                    await _store.CloseAlarmAsync(id, endedAt);
            }
            catch { }
        }

        private async Task PersistAckAsync(AlarmItem item)
        {
            try
            {
                if (_dbIds.TryGetValue(item, out var id))
                    await _store.AckAlarmAsync(id, _currentUser.DisplayName);
            }
            catch { }
        }
    }
}
