using DeviceMonitor.Models;
using DeviceMonitor.PlcClient;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DeviceMonitor.Services
{
    /// <summary>
    /// 后台采集服务：单线程顺序轮询 PLC，把解码结果写进线程安全的 Latest 字典，
    /// 由页面 VM 的 DispatcherTimer（200ms）拉取。采集线程绝不直接改 UI 对象。
    /// </summary>
    public class PlcPollingService : IDisposable
    {
        /// <summary>Modbus 单次读保持寄存器上限是 125，留点余量。</summary>
        private const int MaxBlockRegisters = 120;

        /// <summary>每台机占 3 个寄存器：状态 / 计划数 / 完成数。</summary>
        private const int RegistersPerMachine = 3;

        /// <summary>机台块每 N 个采集周期刷新一次：200 台约 600 个寄存器，每个周期全读会挤爆 500ms 周期。</summary>
        private const int MachineRefreshCycles = 3;

        private readonly IPlcClient _plc;
        private readonly ISettingsService _settings;

        private CancellationTokenSource? _cts;
        private Task? _loop;

        /// <summary>最近一次成功采集的值，key 为点位名称（"车间温度"等）。线程安全。</summary>
        public ConcurrentDictionary<string, double> Latest { get; } = new();

        /// <summary>
        /// 第三批：机台级最新状态，key 为 "{车间名}#{块内序号(0 基)}"。
        /// 机台块比环境/设备块大得多（200 台 ≈ 600 个寄存器），所以每 3 个采集周期才读一次。
        /// </summary>
        public ConcurrentDictionary<string, MachineState> Machines { get; } = new();

        /// <summary>连接/通讯状态文本（供页面显示或排查）。</summary>
        public string Status { get; private set; } = "未启动";

        public DateTime LastUpdate { get; private set; }

        private int _cycle;

        public PlcPollingService(IPlcClient plc, ISettingsService settings)
        {
            _plc = plc;
            _settings = settings;
        }

        public void Start()
        {
            if (_cts is not null) return;
            _cts = new CancellationTokenSource();
            _loop = Task.Run(() => LoopAsync(_cts.Token));
        }

        public void Stop()
        {
            _cts?.Cancel();
            try { _loop?.Wait(TimeSpan.FromSeconds(2)); } catch { /* 等待结束即可 */ }
            _cts?.Dispose();
            _cts = null;
        }

        private async Task LoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    if (!_plc.IsConnected)
                    {
                        Status = "正在连接...";
                        await _plc.ConnectAsync(ct);
                        Status = "已连接";
                    }

                    await ReadOnceAsync(ct);
                    LastUpdate = DateTime.Now;
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Status = "通讯中断: " + ex.Message;
                    try { await _plc.DisconnectAsync(); } catch { /* 忽略 */ }
                }

                try
                {
                    await Task.Delay(_settings.Current.Plc.PollIntervalMs, ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        private async Task ReadOnceAsync(CancellationToken ct)
        {
            var s = _settings.Current;

            foreach (var block in MergeBlocks(s.Points))
            {
                try
                {
                    var regs = await _plc.ReadHoldingRegisters(s.Plc.SlaveId, block.Start, block.Length, ct);
                    foreach (var item in block.Points)
                    {
                        Latest[item.Point.Name] = PlcValue.Decode(
                            regs, item.Offset, item.Point.DataType, item.Point.WordSwap, item.Point.Scale);
                    }
                }
                catch (Modbus.SlaveException)
                {
                    // 整块越界（异常码 0x02）时降级为逐点读：能读到的先上，读不到的跳过。
                    // 这样从站寄存器数量不够时，不会因为尾部点位把整块数据全丢掉。
                    Status = "部分点位越界（Illegal Data Address），已降级逐点读取";
                    await ReadPointsIndividuallyAsync(block, s, ct);
                }
            }

            // 机台级点位：降低刷新频率，避免和前两批抢串口带宽
            _cycle++;
            if (_cycle % MachineRefreshCycles == 0)
                await ReadMachineBlocksAsync(s, ct);
        }

        /// <summary>
        /// 读机台块：每台机 3 个连续寄存器（状态 0/1/2/3、计划数、完成数）。
        /// 一次最多读 120 个寄存器 = 40 台，所以 200 台会拆成 5~6 次请求。
        /// </summary>
        private async Task ReadMachineBlocksAsync(AppSettings s, CancellationToken ct)
        {
            int machinesPerRequest = MaxBlockRegisters / RegistersPerMachine;

            foreach (var block in s.MachineBlocks)
            {
                if (block.MachineCount <= 0) continue;
                if (!PlcValue.TryParseHoldingAddress(block.BaseAddress, out var baseOffset)) continue;

                for (int start = 0; start < block.MachineCount; start += machinesPerRequest)
                {
                    int count = Math.Min(machinesPerRequest, block.MachineCount - start);
                    var addr = (ushort)(baseOffset + start * RegistersPerMachine);

                    try
                    {
                        var regs = await _plc.ReadHoldingRegisters(
                            s.Plc.SlaveId, addr, (ushort)(count * RegistersPerMachine), ct);

                        for (int i = 0; i < count; i++)
                        {
                            int o = i * RegistersPerMachine;
                            Machines[$"{block.WorkshopName}#{start + i}"] =
                                new MachineState(regs[o], regs[o + 1], regs[o + 2]);
                        }
                    }
                    catch (Modbus.SlaveException)
                    {
                        // 这一段越界，后面的段只会更越界，整块放弃（保留上一次的值）
                        Status = "机台点位越界（Illegal Data Address），请把从站 Quantity 加大";
                        break;
                    }
                }
            }
        }

        /// <summary>整块失败后的兜底：一个点位一次请求，失败就跳过。</summary>
        private async Task ReadPointsIndividuallyAsync(Block block, AppSettings s, CancellationToken ct)
        {
            foreach (var item in block.Points)
            {
                var start = (ushort)(block.Start + item.Offset);
                var count = (ushort)PlcValue.RegisterCount(item.Point.DataType);
                try
                {
                    var regs = await _plc.ReadHoldingRegisters(s.Plc.SlaveId, start, count, ct);
                    Latest[item.Point.Name] = PlcValue.Decode(
                        regs, 0, item.Point.DataType, item.Point.WordSwap, item.Point.Scale);
                }
                catch (Modbus.SlaveException)
                {
                    // 单点越界，保持上一次的值
                }
            }
        }

        private sealed class Block
        {
            public ushort Start;
            public ushort Length;
            public List<(PlcPoint Point, int Offset)> Points = new();
        }

        /// <summary>把相邻/重叠的点位合并成尽量少的读块，减少请求次数。</summary>
        private static List<Block> MergeBlocks(IReadOnlyList<PlcPoint> points)
        {
            var items = new List<(int Off, int Count, PlcPoint P)>();
            foreach (var p in points)
            {
                if (PlcValue.TryParseHoldingAddress(p.Address, out var off))
                    items.Add((off, PlcValue.RegisterCount(p.DataType), p));
            }

            items.Sort((a, b) => a.Off.CompareTo(b.Off));

            var blocks = new List<Block>();
            Block? cur = null;
            foreach (var it in items)
            {
                int end = it.Off + it.Count;
                bool needNew = cur is null
                               || it.Off > cur.Start + cur.Length          // 不连续
                               || end - cur.Start > MaxBlockRegisters;     // 超过单次请求上限

                if (needNew)
                {
                    cur = new Block { Start = (ushort)it.Off, Length = (ushort)it.Count };
                    blocks.Add(cur);
                }
                else if (end > cur.Start + cur.Length)
                {
                    cur.Length = (ushort)(end - cur.Start);
                }
                cur!.Points.Add((it.P, it.Off - cur.Start));
            }
            return blocks;
        }

        public void Dispose() => Stop();
    }

    /// <summary>机台级原始值：状态码（0 运行 / 1 等待 / 2 故障 / 3 停机）、计划数、完成数。</summary>
    public readonly record struct MachineState(int Status, int Plan, int Finished);
}
