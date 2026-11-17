namespace DeviceMonitor.Models
{
    // 缺岗数据模型
    public class StaffOutModel
    {
        public string StaffName { get; set; } = string.Empty;
        public string Position { get; set; } = string.Empty;
        public int OutWorkCount { get; set; }

        // 进度条宽度（只读）
        public int ShowWidth
        {
            get
            {
                return OutWorkCount * 45 / 100;
            }
        }
    }
}
