using DeviceMonitor.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace DeviceMonitor.Services
{
    /// <summary>
    /// 全局监控数据源（单例）。
    /// 总览大屏、车间列表、车间详情都从这一个地方取数，避免每个页面各写一份导致数字对不上。
    /// 现在返回的是写死的静态数据，接 Modbus 后由轮询服务往这里写，视图层不用改。
    /// </summary>
    public class MonitorDataService
    {
        public ObservableCollection<EnvironmentModel> EnvironmentList { get; } = new();
        public List<AlarmModel> AlarmList { get; } = new();
        public List<DeviceModel> DeviceList { get; } = new();
        public List<RaderModel> RaderList { get; } = new();
        public List<StaffOutModel> StaffOutWorkList { get; } = new();
        public List<WorkShopModel> WorkShopList { get; } = new();

        /// <summary>全厂机台总数（由各车间汇总，保证与车间卡片数字一致）</summary>
        public int TotalMachineCount => WorkShopList.Sum(w => w.TotalCount);

        public MonitorDataService()
        {
            InitEnvironment();
            InitAlarm();
            InitDevice();
            InitRader();
            InitStaff();
            InitWorkShop();
        }

        private void InitEnvironment()
        {
            EnvironmentList.Add(new EnvironmentModel { EnItemName = "光照(Lux)", EnItemValue = 123 });
            EnvironmentList.Add(new EnvironmentModel { EnItemName = "噪音(db)", EnItemValue = 55 });
            EnvironmentList.Add(new EnvironmentModel { EnItemName = "温度(℃)", EnItemValue = 26 });
            EnvironmentList.Add(new EnvironmentModel { EnItemName = "湿度(%)", EnItemValue = 43 });
            EnvironmentList.Add(new EnvironmentModel { EnItemName = "PM2.5(µg/m³)", EnItemValue = 20 });
            EnvironmentList.Add(new EnvironmentModel { EnItemName = "硫化氢(PPM)", EnItemValue = 3 });
            EnvironmentList.Add(new EnvironmentModel { EnItemName = "氮气(PPM)", EnItemValue = 18 });
        }

        private void InitAlarm()
        {
            AlarmList.Add(new AlarmModel { Num = "01", Msg = "设备温度过高", Time = "2024-02-04 18:34:56", Duration = 7 });
            AlarmList.Add(new AlarmModel { Num = "02", Msg = "车间温度过高", Time = "2024-02-04 20:40:59", Duration = 12 });
            AlarmList.Add(new AlarmModel { Num = "03", Msg = "电压越限", Time = "2024-02-04 12:24:34", Duration = 3 });
            AlarmList.Add(new AlarmModel { Num = "04", Msg = "设备温度过高", Time = "2024-02-04 08:58:00", Duration = 25 });
        }

        private void InitDevice()
        {
            DeviceList.Add(new DeviceModel { DeviceItem = "电能(Kw.h)", Value = 60.8 });
            DeviceList.Add(new DeviceModel { DeviceItem = "电压(V)", Value = 390 });
            DeviceList.Add(new DeviceModel { DeviceItem = "电流(A)", Value = 5 });
            DeviceList.Add(new DeviceModel { DeviceItem = "压差(kpa)", Value = 13 });
            DeviceList.Add(new DeviceModel { DeviceItem = "温度(℃)", Value = 36 });
            DeviceList.Add(new DeviceModel { DeviceItem = "振动(mm/s)", Value = 4.1 });
            DeviceList.Add(new DeviceModel { DeviceItem = "转速(r/min)", Value = 2600 });
            DeviceList.Add(new DeviceModel { DeviceItem = "气压(kpa)", Value = 0.5 });
        }

        private void InitRader()
        {
            RaderList.Add(new RaderModel { ItemName = "排烟风机", Value = 90 });
            RaderList.Add(new RaderModel { ItemName = "客梯", Value = 30 });
            RaderList.Add(new RaderModel { ItemName = "供水机", Value = 34.89 });
            RaderList.Add(new RaderModel { ItemName = "喷淋水泵", Value = 69.59 });
            RaderList.Add(new RaderModel { ItemName = "稳压设备", Value = 20 });
        }

        private void InitStaff()
        {
            StaffOutWorkList.Add(new StaffOutModel { StaffName = "张晓婷", Position = "技术员", OutWorkCount = 123 });
            StaffOutWorkList.Add(new StaffOutModel { StaffName = "李晓", Position = "操作员", OutWorkCount = 23 });
            StaffOutWorkList.Add(new StaffOutModel { StaffName = "王克建", Position = "技术员", OutWorkCount = 134 });
            StaffOutWorkList.Add(new StaffOutModel { StaffName = "陈家栋", Position = "统计员", OutWorkCount = 143 });
            StaffOutWorkList.Add(new StaffOutModel { StaffName = "杨过", Position = "技术员", OutWorkCount = 12 });
        }

        private void InitWorkShop()
        {
            AddWorkShop("贴片车间", 32, 8, 4, 0, "贴片机", 1);
            AddWorkShop("封装车间", 20, 8, 4, 0, "封装机", 2);
            AddWorkShop("焊接车间", 32, 10, 4, 10, "焊接机", 3);
            AddWorkShop("测试车间", 68, 8, 4, 0, "测试机", 4);
        }

        /// <summary>按「作业/等待/故障/停机」四类数量生成机台，保证详情页数量与卡片一致</summary>
        private void AddWorkShop(string name, int working, int wait, int wrong, int stop,
                                 string machinePrefix, int seed)
        {
            var workShop = new WorkShopModel
            {
                WorkShopName = name,
                WorkingCount = working,
                WaitCount = wait,
                WrongCount = wrong,
                StopCount = stop
            };

            var random = new Random(seed * 1000);   // 固定种子：每次运行数据一致，方便演示与截图
            int index = 1;

            for (int i = 0; i < working; i++)
                workShop.MachineList.Add(CreateMachine($"{machinePrefix}-{index++}", "作业中", random));
            for (int i = 0; i < wait; i++)
                workShop.MachineList.Add(CreateMachine($"{machinePrefix}-{index++}", "等待", random));
            for (int i = 0; i < wrong; i++)
                workShop.MachineList.Add(CreateMachine($"{machinePrefix}-{index++}", "故障", random));
            for (int i = 0; i < stop; i++)
                workShop.MachineList.Add(CreateMachine($"{machinePrefix}-{index++}", "停机", random));

            WorkShopList.Add(workShop);
        }

        private static MachineModel CreateMachine(string machineName, string status, Random random)
        {
            int plan = random.Next(60, 120);
            int finished = status switch
            {
                "作业中" => random.Next(plan / 2, plan + 1),
                "等待" => random.Next(0, plan / 3 + 1),
                "故障" => random.Next(0, plan / 4 + 1),
                _ => 0
            };

            return new MachineModel
            {
                MachineName = machineName,
                Status = status,
                PlanCount = plan,
                FinishedCount = finished,
                OrderNo = "#2022" + random.Next(10000000, 99999999)
            };
        }
    }
}
