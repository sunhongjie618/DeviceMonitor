using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeviceMonitor.Models;
using DeviceMonitor.PlcClient;
using DeviceMonitor.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

namespace DeviceMonitor.ViewModels
{
    /// <summary>
    /// 系统配置窗口的视图模型：把所有可编辑项以字符串暴露给 TextBox，
    /// 保存时集中解析 + 校验，再交给 ISettingsService 落盘。
    /// 之所以用字符串而不是 int：TextBox 里用户可能输入空串/"abc"，直接绑 int 会静默丢掉输入。
    /// </summary>
    public partial class SettingsViewModel : ObservableObject
    {
        private readonly ISettingsService _settings;

        public SettingsViewModel(ISettingsService settings)
        {
            _settings = settings;
            LoadFrom(settings.Current);
        }

        /// <summary>配置文件路径，界面上显示给用户（出问题时可自己去改/删）。</summary>
        public string FilePath => _settings.FilePath;

        /// <summary>采集点位表：可增删行，随配置一起保存。</summary>
        public ObservableCollection<PlcPointViewModel> Points { get; } = new();

        private static readonly string[] AllowedDataTypes = { "UInt16", "Int16", "UInt32", "Int32", "Float32", "Bool" };

        // ---------- 通讯参数 ----------
        [ObservableProperty] private string mode = "Modbus RTU";
        [ObservableProperty] private string portName = "COM3";
        [ObservableProperty] private string baudRate = "9600";
        [ObservableProperty] private string dataBits = "8";
        [ObservableProperty] private string parity = "None";
        [ObservableProperty] private string stopBits = "1";
        [ObservableProperty] private string slaveId = "1";
        [ObservableProperty] private string readTimeoutMs = "350";
        [ObservableProperty] private string pollIntervalMs = "500";

        // ---------- 环境阈值 ----------
        [ObservableProperty] private string temperatureLow = "18";
        [ObservableProperty] private string temperatureHigh = "28";
        [ObservableProperty] private string humidityLow = "40";
        [ObservableProperty] private string humidityHigh = "60";
        [ObservableProperty] private string pm25Low = "0";
        [ObservableProperty] private string pm25High = "75";
        [ObservableProperty] private string h2sLow = "0";
        [ObservableProperty] private string h2sHigh = "10";

        // ---------- 状态提示 ----------
        [ObservableProperty] private string statusText = "修改参数后点击「保存」写入本地配置文件。";
        [ObservableProperty] private bool hasError;

        [RelayCommand]
        private void Save()
        {
            if (!TryBuild(out var settings, out var error))
            {
                Fail(error);
                return;
            }

            _settings.Save(settings);
            HasError = false;
            StatusText = $"已保存到 {FilePath}";
        }

        [RelayCommand]
        private async Task TestConnection()
        {
            if (!TryBuild(out var settings, out var error))
            {
                Fail(error);
                return;
            }

            try
            {
                HasError = false;
                StatusText = "正在连接并试读寄存器...";

                using var plc = new ModbusRtuPlcClient(settings.Plc);
                await plc.ConnectAsync();
                var regs = await plc.ReadHoldingRegisters(settings.Plc.SlaveId, 0, 1);
                await plc.DisconnectAsync();

                StatusText = $"连接成功：寄存器 40001 = {regs[0]} (0x{regs[0]:X4})";
            }
            catch (Exception ex)
            {
                Fail($"连接失败：{ex.Message}");
            }
        }

        [RelayCommand]
        private void ResetDefault()
        {
            LoadFrom(new AppSettings());
            HasError = false;
            StatusText = "已恢复默认值，尚未保存。";
        }

        [RelayCommand]
        private void AddPoint()
        {
            Points.Add(new PlcPointViewModel
            {
                Name = "新点位",
                Address = "40010",
                DataType = "UInt16",
                Unit = string.Empty
            });
        }

        [RelayCommand]
        private void DeletePoint(PlcPointViewModel? point)
        {
            if (point is null) return;
            Points.Remove(point);
            StatusText = $"已删除点位「{point.Name}」，保存后生效。";
        }

        private void Fail(string message)
        {
            HasError = true;
            StatusText = message;
        }

        private void LoadFrom(AppSettings s)
        {
            Mode = s.Plc.Mode;
            PortName = s.Plc.PortName;
            BaudRate = s.Plc.BaudRate.ToString(CultureInfo.InvariantCulture);
            DataBits = s.Plc.DataBits.ToString(CultureInfo.InvariantCulture);
            Parity = s.Plc.Parity;
            StopBits = s.Plc.StopBits.ToString(CultureInfo.InvariantCulture);
            SlaveId = s.Plc.SlaveId.ToString(CultureInfo.InvariantCulture);
            ReadTimeoutMs = s.Plc.ReadTimeoutMs.ToString(CultureInfo.InvariantCulture);
            PollIntervalMs = s.Plc.PollIntervalMs.ToString(CultureInfo.InvariantCulture);

            TemperatureLow = Text(s.Environment.TemperatureLow);
            TemperatureHigh = Text(s.Environment.TemperatureHigh);
            HumidityLow = Text(s.Environment.HumidityLow);
            HumidityHigh = Text(s.Environment.HumidityHigh);
            Pm25Low = Text(s.Environment.Pm25Low);
            Pm25High = Text(s.Environment.Pm25High);
            H2sLow = Text(s.Environment.H2sLow);
            H2sHigh = Text(s.Environment.H2sHigh);

            Points.Clear();
            foreach (var point in s.Points)
                Points.Add(new PlcPointViewModel(point));
        }

        private static string Text(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);

        /// <summary>把界面上的字符串解析成强类型配置，并做业务校验。</summary>
        private bool TryBuild(out AppSettings settings, out string error)
        {
            settings = new AppSettings();

            if (!TryInt(BaudRate, out var baud)) { error = "波特率必须是整数。"; return false; }
            if (baud < 1200 || baud > 115200) { error = "波特率超出范围（1200 ~ 115200）。"; return false; }

            if (!TryInt(DataBits, out var dataBitsValue)) { error = "数据位必须是整数。"; return false; }
            if (dataBitsValue < 5 || dataBitsValue > 8) { error = "数据位只能是 5 ~ 8。"; return false; }

            if (!TryInt(StopBits, out var stopBitsValue)) { error = "停止位必须是整数。"; return false; }
            if (stopBitsValue != 1 && stopBitsValue != 2) { error = "停止位只能是 1 或 2。"; return false; }

            if (!TryInt(SlaveId, out var slave)) { error = "从站地址必须是整数。"; return false; }
            if (slave < 1 || slave > 247) { error = "从站地址范围是 1 ~ 247。"; return false; }

            if (!TryInt(ReadTimeoutMs, out var readTimeout)) { error = "读取超时必须是整数（毫秒）。"; return false; }
            if (readTimeout < 50 || readTimeout > 5000) { error = "读取超时范围是 50 ~ 5000 毫秒。"; return false; }

            if (!TryInt(PollIntervalMs, out var poll)) { error = "采集周期必须是整数（毫秒）。"; return false; }
            if (poll < 100 || poll > 10000) { error = "采集周期范围是 100 ~ 10000 毫秒。"; return false; }

            // 关键约束：超时 >= 周期时，一次读失败会把整个采集节拍拖死
            if (readTimeout >= poll)
            {
                error = $"读取超时（{readTimeout}ms）必须小于采集周期（{poll}ms），否则一次读失败会把采集线程拖住。";
                return false;
            }

            if (string.IsNullOrWhiteSpace(PortName)) { error = "串口号不能为空。"; return false; }

            if (!TryDouble(TemperatureLow, out var tLow) || !TryDouble(TemperatureHigh, out var tHigh))
            { error = "温度阈值必须是数字。"; return false; }
            if (tLow >= tHigh) { error = "温度下限必须小于上限。"; return false; }

            if (!TryDouble(HumidityLow, out var hLow) || !TryDouble(HumidityHigh, out var hHigh))
            { error = "湿度阈值必须是数字。"; return false; }
            if (hLow >= hHigh) { error = "湿度下限必须小于上限。"; return false; }

            if (!TryDouble(Pm25Low, out var pLow) || !TryDouble(Pm25High, out var pHigh))
            { error = "PM2.5 阈值必须是数字。"; return false; }
            if (pLow >= pHigh) { error = "PM2.5 下限必须小于上限。"; return false; }

            if (!TryDouble(H2sLow, out var gLow) || !TryDouble(H2sHigh, out var gHigh))
            { error = "硫化氢阈值必须是数字。"; return false; }
            if (gLow >= gHigh) { error = "硫化氢下限必须小于上限。"; return false; }

            // ---- 点位表 ----
            var points = new List<PlcPoint>(Points.Count);
            for (var i = 0; i < Points.Count; i++)
            {
                var row = Points[i];
                var rowNo = i + 1;

                if (string.IsNullOrWhiteSpace(row.Name))
                { error = $"点位表第 {rowNo} 行：名称不能为空。"; return false; }

                if (!int.TryParse(row.Address?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var address)
                    || address < 1 || address > 99999)
                { error = $"点位表第 {rowNo} 行：地址「{row.Address}」不合法，应填 40001 这种数字。"; return false; }

                var type = row.DataType?.Trim() ?? "UInt16";
                if (!AllowedDataTypes.Any(t => string.Equals(t, type, StringComparison.OrdinalIgnoreCase)))
                { error = $"点位表第 {rowNo} 行：数据类型「{type}」不支持，可选 {string.Join(" / ", AllowedDataTypes)}。"; return false; }

                points.Add(new PlcPoint
                {
                    Name = row.Name.Trim(),
                    Address = address.ToString(CultureInfo.InvariantCulture),
                    DataType = type,
                    Unit = row.Unit?.Trim() ?? string.Empty
                });
            }

            settings.Points = points;

            settings.Plc = new PlcConnectionSettings
            {
                Mode = Mode?.Trim() ?? "Modbus RTU",
                PortName = PortName.Trim(),
                BaudRate = baud,
                DataBits = dataBitsValue,
                Parity = Parity?.Trim() ?? "None",
                StopBits = stopBitsValue,
                SlaveId = (byte)slave,
                ReadTimeoutMs = readTimeout,
                PollIntervalMs = poll
            };

            settings.Environment = new EnvironmentThresholdSettings
            {
                TemperatureLow = tLow,
                TemperatureHigh = tHigh,
                HumidityLow = hLow,
                HumidityHigh = hHigh,
                Pm25Low = pLow,
                Pm25High = pHigh,
                H2sLow = gLow,
                H2sHigh = gHigh
            };

            error = string.Empty;
            return true;
        }

        private static bool TryInt(string? text, out int value)
            => int.TryParse(text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

        private static bool TryDouble(string? text, out double value)
            => double.TryParse(text?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }
}
