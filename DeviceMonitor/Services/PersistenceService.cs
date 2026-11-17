using DeviceMonitor.Models;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DeviceMonitor.Services
{
    /// <summary>
    /// 采集数据持久化：独立后台任务，每 5 秒把<em>当前</em>点位值快照批量写库。
    /// 为什么要降频：500ms 周期 × 32 点位 = 64 行/秒（一天 550 万行），SQLite 扛不住；
    /// 界面刷新仍走 500ms，落库降到 5 秒，是工控上位机的常规分频做法。
    /// </summary>
    public class PersistenceService : IDisposable
    {
        /// <summary>落库间隔。</summary>
        public static readonly TimeSpan SampleInterval = TimeSpan.FromSeconds(5);

        /// <summary>原始数据保留天数（超期按天清理）。</summary>
        public static readonly TimeSpan Retention = TimeSpan.FromDays(7);

        private readonly PlcPollingService _polling;
        private readonly IDataStore _store;

        private CancellationTokenSource? _cts;
        private Task? _loop;
        private DateTime _lastPurge = DateTime.MinValue;

        public PersistenceService(PlcPollingService polling, IDataStore store)
        {
            _polling = polling;
            _store = store;
        }

        /// <summary>已写入的原始记录条数（页面上可以显示，作为落库是否活着的证据）。</summary>
        public long SavedCount { get; private set; }

        public DateTime? LastSavedAt { get; private set; }

        public string Status { get; private set; } = "未启动";

        public void Start()
        {
            if (_cts is not null) return;
            _cts = new CancellationTokenSource();
            _loop = Task.Run(() => LoopAsync(_cts.Token));
        }

        public void Stop()
        {
            if (_cts is null) return;
            _cts.Cancel();
            try { _loop?.Wait(TimeSpan.FromSeconds(3)); } catch { /* 停止即可 */ }
            _cts.Dispose();
            _cts = null;
            _loop = null;
            Status = "已停止";
        }

        private async Task LoopAsync(CancellationToken ct)
        {
            Status = "运行中";

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var now = DateTime.Now;

                    // 只写「当前活着的点位」，采集中断时不写 0，避免报表被 0 值污染。
                    var snapshot = _polling.Latest
                        .Where(kv => !string.IsNullOrWhiteSpace(kv.Key))
                        .Select(kv => new Reading { PointName = kv.Key, SampleTime = now, Value = kv.Value })
                        .ToList();

                    if (snapshot.Count > 0)
                    {
                        await _store.AddReadingsAsync(snapshot);
                        SavedCount += snapshot.Count;
                        LastSavedAt = now;
                        Status = "运行中";
                    }
                    else
                    {
                        Status = "等待采集数据";
                    }

                    // 每小时清理一次超过保留期的原始数据
                    if (now - _lastPurge > TimeSpan.FromHours(1))
                    {
                        _lastPurge = now;
                        await _store.PurgeOlderThanAsync(now - Retention);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // 落库失败绝不能影响采集：记状态字符串，下个周期重试。
                    Status = "落库失败: " + ex.Message;
                }

                try { await Task.Delay(SampleInterval, ct); }
                catch (OperationCanceledException) { break; }
            }
        }

        public void Dispose() => Stop();
    }
}
