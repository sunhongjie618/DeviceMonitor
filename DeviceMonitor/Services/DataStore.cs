using DeviceMonitor.Data;
using DeviceMonitor.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DeviceMonitor.Services
{
    /// <summary>
    /// IDataStore 的 EF Core 实现。每次操作从工厂取一个短命 DbContext，
    /// 不在长生命周期对象里持有 DbContext（SQLite 连接非线程安全）。
    /// </summary>
    public class DataStore : IDataStore
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public DataStore(IDbContextFactory<AppDbContext> factory) => _factory = factory;

        public async Task AddReadingsAsync(IReadOnlyCollection<Reading> items)
        {
            if (items.Count == 0) return;

            using var db = _factory.CreateDbContext();
            db.Readings.AddRange(items);
            await db.SaveChangesAsync();
        }

        public async Task<int> AddAlarmAsync(AlarmRecord record)
        {
            using var db = _factory.CreateDbContext();
            db.Alarms.Add(record);
            await db.SaveChangesAsync();
            return record.Id;
        }

        public async Task CloseAlarmAsync(int id, DateTime endedAt)
        {
            using var db = _factory.CreateDbContext();
            var item = await db.Alarms.FirstOrDefaultAsync(a => a.Id == id);
            if (item is null || item.EndedAt is not null) return;

            item.EndedAt = endedAt;
            await db.SaveChangesAsync();
        }

        public async Task AckAlarmAsync(int id, string ackedBy)
        {
            using var db = _factory.CreateDbContext();
            var item = await db.Alarms.FirstOrDefaultAsync(a => a.Id == id);
            if (item is null || item.IsAcked) return;

            item.IsAcked = true;
            item.AckedBy = ackedBy;
            item.AckedAt = DateTime.Now;
            await db.SaveChangesAsync();
        }

        public async Task<long> CountTodayReadingsAsync()
        {
            using var db = _factory.CreateDbContext();
            var today = DateTime.Today;
            return await db.Readings.LongCountAsync(r => r.SampleTime >= today);
        }

        public async Task<DateTime?> EarliestTodayAsync()
        {
            using var db = _factory.CreateDbContext();
            var today = DateTime.Today;
            var q = db.Readings.Where(r => r.SampleTime >= today);
            if (!await q.AnyAsync()) return null;
            return await q.MinAsync(r => r.SampleTime);
        }

        public async Task<List<Reading>> GetReadingsAsync(string pointName, DateTime from, DateTime to)
        {
            using var db = _factory.CreateDbContext();
            return await db.Readings
                .AsNoTracking()
                .Where(r => r.PointName == pointName && r.SampleTime >= from && r.SampleTime <= to)
                .OrderBy(r => r.SampleTime)
                .ToListAsync();
        }

        public async Task<List<Reading>> GetTodayReadingsAsync(int max = 50000)
        {
            using var db = _factory.CreateDbContext();
            var today = DateTime.Today;
            return await db.Readings
                .AsNoTracking()
                .Where(r => r.SampleTime >= today)
                .OrderByDescending(r => r.SampleTime)
                .Take(max)
                .ToListAsync();
        }

        public async Task<List<Reading>> GetTodaySeriesAsync(IReadOnlyCollection<string> pointNames, int max = 200000)
        {
            if (pointNames.Count == 0) return new List<Reading>();

            using var db = _factory.CreateDbContext();
            var today = DateTime.Today;
            var names = pointNames.ToList();

            return await db.Readings
                .AsNoTracking()
                .Where(r => r.SampleTime >= today && names.Contains(r.PointName))
                .OrderBy(r => r.SampleTime)
                .Take(max)
                .ToListAsync();
        }

        public async Task<int> PurgeOlderThanAsync(DateTime cutoff)
        {
            using var db = _factory.CreateDbContext();
            var count = await db.Readings
                .Where(r => r.SampleTime < cutoff)
                .ExecuteDeleteAsync();
            return count;
        }
    }
}
