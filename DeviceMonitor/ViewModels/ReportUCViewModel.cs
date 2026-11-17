using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeviceMonitor.Models;
using DeviceMonitor.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace DeviceMonitor.ViewModels
{
    /// <summary>
    /// 数据报表页。
    /// 时段明细现在**从 SQLite 读**：取今日「当日产量 / 不良计数」两个累计点位的原始记录，
    /// 按小时分组后用「该小时最后一个值 − 第一个值」得到该小时增量（累计计数器只能做差）。
    /// 底部同时显示今日落库条数与落库服务状态。
    /// </summary>
    public partial class ReportUCViewModel : ObservableObject
    {
        private const int FirstHour = 8;
        private const int HourCount = 9;   // 8:00-9:00 ... 16:00-17:00

        private static readonly string[] CounterPoints = { "当日产量", "不良计数" };

        private readonly PlcPollingService _polling;
        private readonly IDataStore _store;
        private readonly PersistenceService _persistence;
        private readonly DispatcherTimer _timer;

        public ReportUCViewModel(PlcPollingService polling, IDataStore store, PersistenceService persistence)
        {
            _polling = polling;
            _store = store;
            _persistence = persistence;

            BuildEmptyRows();

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
            _timer.Tick += async (_, _) => await RefreshAsync();
            _timer.Start();

            _ = RefreshAsync();
        }

        public string Title => "数据报表";

        public string Subtitle => "今日产量 / 良品率 / 导出";

        public ObservableCollection<ReportRow> Rows { get; } = new();

        [ObservableProperty]
        private string totalProduceText = "--";

        [ObservableProperty]
        private string totalBadText = "--";

        [ObservableProperty]
        private string passRateText = "--";

        [ObservableProperty]
        private string peakText = "--";

        [ObservableProperty]
        private string realProduceText = "--";

        [ObservableProperty]
        private string exportText = "尚未导出";

        [ObservableProperty]
        private string sourceText = "时段明细：本机 SQLite（当日产量/不良计数按小时做差）；原始采集：本机 SQLite";

        [ObservableProperty]
        private string dbInfoText = "数据库：读取中...";

        private void BuildEmptyRows()
        {
            Rows.Clear();
            for (int i = 0; i < HourCount; i++)
                Rows.Add(new ReportRow { Hour = $"{FirstHour + i}:00 - {FirstHour + i + 1}:00" });
        }

        /// <summary>从数据库读今日两个累计点位，按小时做差，刷新表格与 KPI。</summary>
        private async Task RefreshAsync()
        {
            try
            {
                await RefreshDbInfoAsync();

                var series = await _store.GetTodaySeriesAsync(CounterPoints);

                var produceByHour = AggregateByHour(series.Where(r => r.PointName == "当日产量"));
                var badByHour = AggregateByHour(series.Where(r => r.PointName == "不良计数"));

                int totalProduce = 0, totalBad = 0, peakProduce = -1;
                string peakHour = "--";

                var rows = new List<ReportRow>(HourCount);
                for (int i = 0; i < HourCount; i++)
                {
                    int hour = FirstHour + i;
                    bool has = produceByHour.TryGetValue(hour, out var p);

                    int produce = has ? p : 0;
                    int bad = badByHour.TryGetValue(hour, out var b) ? b : 0;

                    rows.Add(new ReportRow
                    {
                        Hour = $"{hour}:00 - {hour + 1}:00",
                        Produce = produce,
                        Bad = bad,
                        HasData = has
                    });

                    if (!has) continue;
                    totalProduce += produce;
                    totalBad += bad;
                    if (produce > peakProduce)
                    {
                        peakProduce = produce;
                        peakHour = $"{hour}:00 - {hour + 1}:00（{produce:N0} 件）";
                    }
                }

                Rows.Clear();
                foreach (var row in rows) Rows.Add(row);

                if (peakProduce < 0)
                {
                    TotalProduceText = "--";
                    TotalBadText = "--";
                    PassRateText = "--";
                    PeakText = "暂无数据（采集跑起来后自动出现）";
                }
                else
                {
                    TotalProduceText = totalProduce.ToString("N0");
                    TotalBadText = totalBad.ToString("N0");
                    PassRateText = totalProduce <= 0
                        ? "--"
                        : ((totalProduce - totalBad) * 100.0 / totalProduce).ToString("0.0") + "%";
                    PeakText = peakHour;
                }

                RealProduceText = _polling.Latest.TryGetValue("当日产量", out var produced)
                    ? produced.ToString("N0") + " 件（PLC 实时累计）"
                    : "尚未采集到「当日产量」点位";
            }
            catch (Exception ex)
            {
                DbInfoText = "数据库：读取失败 - " + ex.Message;
            }
        }

        /// <summary>
        /// 按小时做差：同一小时内取最后一个值减第一个值（累计计数器只增不减，差即该小时增量）。
        /// 只有一个样本时增量按 0 计。
        /// </summary>
        private static Dictionary<int, int> AggregateByHour(IEnumerable<Reading> readings)
        {
            var result = new Dictionary<int, int>();

            foreach (var group in readings.GroupBy(r => r.SampleTime.Hour))
            {
                var ordered = group.OrderBy(r => r.SampleTime).ToList();
                if (ordered.Count == 0) continue;

                double first = ordered[0].Value;
                double last = ordered[^1].Value;
                double delta = last - first;
                if (delta < 0) delta = 0;   // 计数器归零/被重置时，不产生负数

                result[group.Key] = (int)Math.Round(delta);
            }

            return result;
        }

        private async Task RefreshDbInfoAsync()
        {
            try
            {
                var count = await _store.CountTodayReadingsAsync();
                var earliest = await _store.EarliestTodayAsync();

                DbInfoText = earliest is null
                    ? $"数据库：今日尚无记录（{_persistence.Status}）"
                    : $"数据库：今日已采集 {count:N0} 条原始记录（最早 {earliest:HH:mm:ss}） · 落库状态：{_persistence.Status}";
            }
            catch (Exception ex)
            {
                DbInfoText = "数据库：读取失败 - " + ex.Message;
            }
        }

        [RelayCommand]
        private async Task RefreshRowsAsync() => await RefreshAsync();

        [RelayCommand]
        private void ExportCsv()
        {
            try
            {
                var dir = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                var file = Path.Combine(dir, $"DeviceMonitor_今日产量_{DateTime.Now:yyyyMMdd_HHmmss}.csv");

                var sb = new StringBuilder();
                sb.AppendLine("时段,生产数,不良数,良品率,有无数据");
                foreach (var row in Rows)
                    sb.AppendLine($"{row.Hour},{row.ProduceText},{row.BadText},{row.PassRateText},{(row.HasData ? "有" : "无")}");
                sb.AppendLine($"合计,{TotalProduceText},{TotalBadText},{PassRateText},");

                File.WriteAllText(file, sb.ToString(), new UTF8Encoding(true));
                ExportText = "已导出（数据库聚合结果）：" + file;
            }
            catch (Exception ex)
            {
                ExportText = "导出失败：" + ex.Message;
            }
        }

        /// <summary>导出数据库里的真实采集原始记录（今日，最多 5 万条）。</summary>
        [RelayCommand]
        private async Task ExportRawCsvAsync()
        {
            try
            {
                var rows = await _store.GetTodayReadingsAsync();
                if (rows.Count == 0)
                {
                    ExportText = "尚无采集记录可导出（先让采集跑一会儿）";
                    return;
                }

                var dir = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                var file = Path.Combine(dir, $"DeviceMonitor_原始采集_{DateTime.Now:yyyyMMdd_HHmmss}.csv");

                var sb = new StringBuilder();
                sb.AppendLine("采样时间,点位名称,数值");
                foreach (var r in rows.OrderBy(r => r.SampleTime))
                    sb.AppendLine($"{r.SampleTime:yyyy-MM-dd HH:mm:ss},{r.PointName},{r.Value}");

                File.WriteAllText(file, sb.ToString(), new UTF8Encoding(true));
                ExportText = $"已导出 {rows.Count:N0} 条原始记录：" + file;
            }
            catch (Exception ex)
            {
                ExportText = "导出失败：" + ex.Message;
            }
        }
    }
}
