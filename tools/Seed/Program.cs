using System.IO.Ports;
using Modbus.Device;

// 往 Modbus Slave（默认 COM3，从站 1）灌入三批点位的数据。
//  0~36   第一批/第二批：环境、设备、四车间汇总、不良计数
//  100~735 第三批：机台级（每台机 3 个寄存器：状态/计划/完成）
// 用法：Seed.exe [port] [slave]
var portName = args.Length > 0 ? args[0] : "COM3";
var slaveId = args.Length > 1 ? byte.Parse(args[1]) : (byte)1;

using var sp = new SerialPort(portName, 9600, Parity.None, 8, StopBits.One)
{
    ReadTimeout = 1000,
    WriteTimeout = 1000
};
sp.Open();

var master = ModbusSerialMaster.CreateRtu(sp);
master.Transport.ReadTimeout = 500;
master.Transport.WriteTimeout = 500;
master.Transport.Retries = 1;

int ok = 0, fail = 0;

void WriteOne(ushort addr, ushort value, string desc)
{
    try
    {
        master.WriteSingleRegister(slaveId, addr, value);
        Console.WriteLine($"OK   [{addr,3}] {value,6}  {desc}");
        ok++;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"ERR  [{addr,3}] {value,6}  {desc}  <- {ex.GetType().Name}: {ex.Message}");
        fail++;
    }
}

void WriteFloat(ushort addr, float value, string desc)
{
    // 大端字序 ABCD：高位字在前
    var b = BitConverter.GetBytes(value);
    ushort hi = (ushort)((b[3] << 8) | b[2]);
    ushort lo = (ushort)((b[1] << 8) | b[0]);
    try
    {
        master.WriteMultipleRegisters(slaveId, addr, new[] { hi, lo });
        Console.WriteLine($"OK   [{addr,3}-{(ushort)(addr + 1),3}] {value,6}  {desc}");
        ok++;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"ERR  [{addr,3}-{(ushort)(addr + 1),3}] {value,6}  {desc}  <- {ex.GetType().Name}: {ex.Message}");
        fail++;
    }
}

void WriteRegisters(ushort addr, ushort[] values, string desc)
{
    try
    {
        master.WriteMultipleRegisters(slaveId, addr, values);
        Console.WriteLine($"OK   [{addr,3}-{(ushort)(addr + values.Length - 1),3}] {values.Length,4} regs  {desc}");
        ok++;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"ERR  [{addr,3}-{(ushort)(addr + values.Length - 1),3}] {values.Length,4} regs  {desc}  <- {ex.GetType().Name}: {ex.Message}");
        fail++;
    }
}

void WriteUInt32(ushort addr, uint value, string desc)
    => WriteRegisters(addr, new[] { (ushort)(value >> 16), (ushort)(value & 0xFFFF) }, desc);

Console.WriteLine($"== 写入 {portName} 从站 {slaveId} ==");

// ---------- 第一批：环境 7 + 设备 9（0~19） ----------
WriteFloat(0, 26.5f, "车间温度 ℃");
WriteFloat(2, 43.0f, "车间湿度 %");
WriteOne(4, 123, "光照 Lux");
WriteOne(5, 55, "噪音 db");
WriteOne(6, 20, "PM2.5 µg/m³");
WriteOne(7, 3, "硫化氢 PPM");
WriteOne(8, 18, "氮气 PPM");
WriteUInt32(9, 1643, "当日产量 件");
WriteFloat(11, 60.8f, "电能 Kw.h");
WriteOne(13, 390, "电压 V");
WriteOne(14, 5, "电流 A");
WriteOne(15, 13, "压差 kpa");
WriteOne(16, 36, "设备温度 ℃");
WriteOne(17, 41, "振动 41*0.1=4.1 mm/s");
WriteOne(18, 2600, "转速 r/min");
WriteOne(19, 5, "气压 5*0.1=0.5 kpa");

// ---------- 第二批：四车间汇总（20~35）+ 不良计数（36~37） ----------
Console.WriteLine("-- 车间汇总 / 不良计数 --");
WriteOne(20, 31, "贴片车间 作业");
WriteOne(21, 9, "贴片车间 等待");
WriteOne(22, 3, "贴片车间 故障");
WriteOne(23, 1, "贴片车间 停机");
WriteOne(24, 19, "封装车间 作业");
WriteOne(25, 9, "封装车间 等待");
WriteOne(26, 3, "封装车间 故障");
WriteOne(27, 1, "封装车间 停机");
WriteOne(28, 31, "焊接车间 作业");
WriteOne(29, 9, "焊接车间 等待");
WriteOne(30, 3, "焊接车间 故障");
WriteOne(31, 13, "焊接车间 停机");
WriteOne(32, 67, "测试车间 作业");
WriteOne(33, 9, "测试车间 等待");
WriteOne(34, 3, "测试车间 故障");
WriteOne(35, 1, "测试车间 停机");
WriteUInt32(36, 223, "不良计数 件");

// ---------- 第三批：机台级（100 起，每台机 3 个寄存器） ----------
// 状态码：0 运行 / 1 等待 / 2 故障 / 3 停机
// 这里按第二批的「作业/等待/故障/停机」数量排布，保证「详情页机台状态统计 == 大屏车间卡片」
Console.WriteLine("-- 机台级点位 --");

var workshops = new (string Name, ushort BaseAddr, int Working, int Wait, int Wrong, int Stop)[]
{
    ("贴片车间", 100, 31, 9, 3, 1),
    ("封装车间", 232, 19, 9, 3, 1),
    ("焊接车间", 328, 31, 9, 3, 13),
    ("测试车间", 496, 67, 9, 3, 1)
};

foreach (var ws in workshops)
{
    int total = ws.Working + ws.Wait + ws.Wrong + ws.Stop;

    // 按 40 台（120 个寄存器）一段写，避免一次请求过长
    const int MachinesPerRequest = 40;
    for (int start = 0; start < total; start += MachinesPerRequest)
    {
        int count = Math.Min(MachinesPerRequest, total - start);
        var regs = new ushort[count * 3];

        for (int i = 0; i < count; i++)
        {
            int idx = start + i;
            int status = idx < ws.Working ? 0
                       : idx < ws.Working + ws.Wait ? 1
                       : idx < ws.Working + ws.Wait + ws.Wrong ? 2
                       : 3;

            int plan = 100 + (idx % 20);
            int finished = status switch
            {
                0 => Math.Max(0, plan - (idx % 7)),          // 运行：接近计划
                1 => plan / 2,                               // 等待：做了一半
                2 => plan / 4,                               // 故障：少量
                _ => 0                                       // 停机：0
            };

            regs[i * 3] = (ushort)status;
            regs[i * 3 + 1] = (ushort)plan;
            regs[i * 3 + 2] = (ushort)finished;
        }

        WriteRegisters((ushort)(ws.BaseAddr + start * 3), regs,
            $"{ws.Name} 机台 {start + 1}~{start + count}");
    }
}

Console.WriteLine($"== 完成：成功 {ok} 批，失败 {fail} 批 ==");
sp.Close();
