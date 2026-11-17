using Microsoft.Data.Sqlite;

// 往 DeviceMonitor 的 SQLite 里补一段「今天 8:00~17:00」的累计计数器历史，
// 用来验证数据报表页的「按小时做差」聚合逻辑（这不是造假数据，只是把采集过程压缩成几行）。
// 用法：History.exe [dbPath]
var dbPath = args.Length > 0
    ? args[0]
    : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                   "DeviceMonitor", "data", "device.db");

if (!File.Exists(dbPath))
{
    Console.WriteLine("找不到数据库：" + dbPath);
    return 1;
}

var hourlyProduce = new[] { 300, 400, 480, 450, 380, 450, 450, 330, 340 };
var hourlyBad = new[] { 12, 28, 35, 30, 22, 30, 30, 18, 18 };

using var conn = new SqliteConnection($"Data Source={dbPath}");
conn.Open();

// 关掉 WAL，避免和正在运行的采集进程抢 -wal 文件；历史补数只在程序关闭时做
using (var pragma = conn.CreateCommand())
{
    pragma.CommandText = "PRAGMA journal_mode=DELETE;";
    pragma.ExecuteNonQuery();
}

// 先清掉今天这两个点位的旧数据，避免和真实采集混在一起
using (var del = conn.CreateCommand())
{
    del.CommandText = @"DELETE FROM Readings
                        WHERE PointName IN ('当日产量','不良计数') AND SampleTime >= $today";
    del.Parameters.AddWithValue("$today", DateTime.Today.ToString("yyyy-MM-dd 00:00:00"));
    var removed = del.ExecuteNonQuery();
    Console.WriteLine($"清掉旧累计点位记录 {removed} 条");
}

double produce = 1000;   // 累计计数器起点
double bad = 50;
var today = DateTime.Today;

using var tx = conn.BeginTransaction();

for (int i = 0; i < hourlyProduce.Length; i++)
{
    int hour = 8 + i;
    var startTime = today.AddHours(hour).AddMinutes(2);
    var endTime = today.AddHours(hour).AddMinutes(57);

    Insert("当日产量", startTime, produce);
    produce += hourlyProduce[i];
    Insert("当日产量", endTime, produce);

    Insert("不良计数", startTime, bad);
    bad += hourlyBad[i];
    Insert("不良计数", endTime, bad);
}

tx.Commit();
Console.WriteLine($"已写入 8:00~16:00 共 {hourlyProduce.Length} 个时段的累计值（产量 {produce:N0}，不良 {bad:N0}）");
conn.Close();
return 0;

void Insert(string point, DateTime time, double value)
{
    using var cmd = conn.CreateCommand();
    cmd.CommandText = "INSERT INTO Readings (PointName, SampleTime, Value) VALUES ($p, $t, $v)";
    cmd.Parameters.AddWithValue("$p", point);
    cmd.Parameters.AddWithValue("$t", time);
    cmd.Parameters.AddWithValue("$v", value);
    cmd.ExecuteNonQuery();
}
