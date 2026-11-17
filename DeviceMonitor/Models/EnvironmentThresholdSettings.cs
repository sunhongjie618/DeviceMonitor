namespace DeviceMonitor.Models
{
    /// <summary>
    /// 环境量报警阈值：采集值超出 [Low, High] 视为异常，由报警引擎生成报警记录。
    /// 现在只做配置与校验，接上采集后由报警引擎消费。
    /// </summary>
    public class EnvironmentThresholdSettings
    {
        public double TemperatureLow { get; set; } = 18;
        public double TemperatureHigh { get; set; } = 28;

        public double HumidityLow { get; set; } = 40;
        public double HumidityHigh { get; set; } = 60;

        public double Pm25Low { get; set; }
        public double Pm25High { get; set; } = 75;

        public double H2sLow { get; set; }
        public double H2sHigh { get; set; } = 10;
    }
}
