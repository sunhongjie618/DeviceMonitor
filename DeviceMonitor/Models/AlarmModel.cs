namespace DeviceMonitor.Models
{
    // 报警记录数据模型
    public class AlarmModel
    {
        // 编号
        public string Num { get; set; } = string.Empty;
        // 报警信息
        public string Msg { get; set; } = string.Empty;
        // 报警时间
        public string Time { get; set; } = string.Empty;
        // 报警时长 单位:秒
        public int Duration { get; set; }
    }
}
