using System.Collections.Generic;

namespace DeviceMonitor.Models
{
    /// <summary>
    /// 配置文件的整体内容：通讯参数 + 采集点位 + 环境阈值。
    /// 一次保存就是一个完整快照，避免多处各存一份。
    /// </summary>
    public class AppSettings
    {
        public PlcConnectionSettings Plc { get; set; } = new();

        public EnvironmentThresholdSettings Environment { get; set; } = new();

        /// <summary>
        /// 采集点位表。地址写 40001 这类 1 基展示地址（和手册一致），
        /// 解析层再换算成 0 基偏移；相邻点位会合并成一次读请求。
        ///
        /// 默认表覆盖大屏全部指标，40001 起连续 20 个保持寄存器：
        ///   0-1 车间温度(Float32) 2-3 车间湿度(Float32) 4 光照 5 噪音
        ///   6 PM2.5 7 硫化氢 8 氮气 9-10 当日产量(UInt32)
        ///   11-12 电能(Float32) 13 电压 14 电流 15 压差
        ///   16 设备温度 17 振动 18 转速 19 气压
        /// 从站寄存器数量（Quantity）至少要 20 个，否则尾部点位会越界。
        /// </summary>
        public List<PlcPoint> Points { get; set; } = new()
        {
            new PlcPoint { Name = "车间温度", Address = "40001", DataType = "Float32", Unit = "℃" },
            new PlcPoint { Name = "车间湿度", Address = "40003", DataType = "Float32", Unit = "%" },
            new PlcPoint { Name = "光照", Address = "40005", DataType = "UInt16", Unit = "Lux" },
            new PlcPoint { Name = "噪音", Address = "40006", DataType = "UInt16", Unit = "db" },
            new PlcPoint { Name = "PM2.5", Address = "40007", DataType = "UInt16", Unit = "µg/m³" },
            new PlcPoint { Name = "硫化氢", Address = "40008", DataType = "UInt16", Unit = "PPM" },
            new PlcPoint { Name = "氮气", Address = "40009", DataType = "UInt16", Unit = "PPM" },
            new PlcPoint { Name = "当日产量", Address = "40010", DataType = "UInt32", Unit = "件" },
            new PlcPoint { Name = "电能", Address = "40012", DataType = "Float32", Unit = "Kw.h" },
            new PlcPoint { Name = "电压", Address = "40014", DataType = "UInt16", Unit = "V" },
            new PlcPoint { Name = "电流", Address = "40015", DataType = "UInt16", Unit = "A" },
            new PlcPoint { Name = "压差", Address = "40016", DataType = "Int16", Unit = "kpa" },
            new PlcPoint { Name = "设备温度", Address = "40017", DataType = "Int16", Unit = "℃" },
            // 振动、气压现场多是「整数放大 10 倍」，用 Scale 换算成小数
            new PlcPoint { Name = "振动", Address = "40018", DataType = "UInt16", Unit = "mm/s", Scale = 0.1 },
            new PlcPoint { Name = "转速", Address = "40019", DataType = "UInt16", Unit = "r/min" },
            new PlcPoint { Name = "气压", Address = "40020", DataType = "Int16", Unit = "kpa", Scale = 0.1 },

            // 第二批：车间汇总，4 车间 × 作业/等待/故障/停机（每车间 4 个寄存器）
            // 贴片 40021~40024，封装 40025~40028，焊接 40029~40032，测试 40033~40036
            new PlcPoint { Name = "贴片车间作业", Address = "40021", DataType = "UInt16", Unit = "台" },
            new PlcPoint { Name = "贴片车间等待", Address = "40022", DataType = "UInt16", Unit = "台" },
            new PlcPoint { Name = "贴片车间故障", Address = "40023", DataType = "UInt16", Unit = "台" },
            new PlcPoint { Name = "贴片车间停机", Address = "40024", DataType = "UInt16", Unit = "台" },
            new PlcPoint { Name = "封装车间作业", Address = "40025", DataType = "UInt16", Unit = "台" },
            new PlcPoint { Name = "封装车间等待", Address = "40026", DataType = "UInt16", Unit = "台" },
            new PlcPoint { Name = "封装车间故障", Address = "40027", DataType = "UInt16", Unit = "台" },
            new PlcPoint { Name = "封装车间停机", Address = "40028", DataType = "UInt16", Unit = "台" },
            new PlcPoint { Name = "焊接车间作业", Address = "40029", DataType = "UInt16", Unit = "台" },
            new PlcPoint { Name = "焊接车间等待", Address = "40030", DataType = "UInt16", Unit = "台" },
            new PlcPoint { Name = "焊接车间故障", Address = "40031", DataType = "UInt16", Unit = "台" },
            new PlcPoint { Name = "焊接车间停机", Address = "40032", DataType = "UInt16", Unit = "台" },
            new PlcPoint { Name = "测试车间作业", Address = "40033", DataType = "UInt16", Unit = "台" },
            new PlcPoint { Name = "测试车间等待", Address = "40034", DataType = "UInt16", Unit = "台" },
            new PlcPoint { Name = "测试车间故障", Address = "40035", DataType = "UInt16", Unit = "台" },
            new PlcPoint { Name = "测试车间停机", Address = "40036", DataType = "UInt16", Unit = "台" },

            // 不良计数也是累计值：和「当日产量」一起按小时做差，报表就能算出每小时产量与不良率
            new PlcPoint { Name = "不良计数", Address = "40037", DataType = "UInt32", Unit = "件" }
        };

        /// <summary>
        /// 第三批：机台级点位块（每台机 3 个寄存器：状态 / 计划数 / 完成数）。
        /// 默认从 0 基偏移 100 开始，避开前两批用掉的 0~40：
        ///   贴片 40101 起 44 台 → 0 基 100~231
        ///   封装 40233 起 32 台 → 232~327
        ///   焊接 40329 起 56 台 → 328~495
        ///   测试 40497 起 80 台 → 496~735
        /// 从站寄存器数量（Quantity）至少要 736 才能读全；不够时只读得到前面几块。
        /// </summary>
        public List<MachineBlock> MachineBlocks { get; set; } = new()
        {
            new MachineBlock { WorkshopName = "贴片车间", BaseAddress = "40101", MachineCount = 44 },
            new MachineBlock { WorkshopName = "封装车间", BaseAddress = "40233", MachineCount = 32 },
            new MachineBlock { WorkshopName = "焊接车间", BaseAddress = "40329", MachineCount = 56 },
            new MachineBlock { WorkshopName = "测试车间", BaseAddress = "40497", MachineCount = 80 }
        };
    }
}
