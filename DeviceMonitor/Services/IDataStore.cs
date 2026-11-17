using DeviceMonitor.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DeviceMonitor.Services
{
    /// <summary>
    /// 采集数据/报警的落库入口。所有写操作都是「批量 + 单事务」，
    /// 上层（采集循环）不需要关心 EF 的存在。
    /// </summary>
    public interface IDataStore
    {
        Task AddReadingsAsync(IReadOnlyCollection<Reading> items);

        Task<int> AddAlarmAsync(AlarmRecord record);

        Task CloseAlarmAsync(int id, DateTime endedAt);

        Task AckAlarmAsync(int id, string ackedBy);

        Task<long> CountTodayReadingsAsync();

        Task<DateTime?> EarliestTodayAsync();

        Task<List<Reading>> GetReadingsAsync(string pointName, DateTime from, DateTime to);

        /// <summary>取今天的原始记录（倒序截取 max 条），导出用。</summary>
        Task<List<Reading>> GetTodayReadingsAsync(int max = 50000);

        /// <summary>取今天指定点位的原始记录（升序），报表按小时聚合用。</summary>
        Task<List<Reading>> GetTodaySeriesAsync(IReadOnlyCollection<string> pointNames, int max = 200000);

        /// <summary>清理早于 cutoff 的原始数据，返回删除行数。</summary>
        Task<int> PurgeOlderThanAsync(DateTime cutoff);
    }
}
