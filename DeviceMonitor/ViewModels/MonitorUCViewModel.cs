using CommunityToolkit.Mvvm.ComponentModel;
using DeviceMonitor.Models;
using DeviceMonitor.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Threading;

namespace DeviceMonitor.ViewModels
{
    /// <summary>
    /// 总览大屏页视图模型。
    /// 静态数据（车间/报警/雷达/人力等）来自 MonitorDataService；环境 7 项与设备 8 项
    /// 由 PlcPollingService 实时采集后，通过 200ms 的 DispatcherTimer 批量拉到界面。
    /// </summary>
    public partial class MonitorUCViewModel : ObservableObject
    {
        private readonly DispatcherTimer _clockTimer;
        private readonly DispatcherTimer _refreshTimer;
        private readonly PlcPollingService _polling;

        public MonitorUCViewModel(MonitorDataService data, PlcPollingService polling)
        {
            _polling = polling;

            EnvironmentList = data.EnvironmentList;
            AlarmList = data.AlarmList;
            DeviceList = data.DeviceList;
            RaderList = data.RaderList;
            StaffOutWorkList = data.StaffOutWorkList;
            WorkShopList = data.WorkShopList;

            // 顶部数字由数据汇总而来，保证与下方车间卡片对得上
            MachineCount = data.TotalMachineCount.ToString("D4");
            ProductCount = "1643";
            BadCount = "023";

            UpdateClock();

            // 教程里时间只在构造时取一次、且属性不通知，钟是停的；这里每秒刷新
            _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _clockTimer.Tick += (_, _) => UpdateClock();
            _clockTimer.Start();

            // 采集快、UI 慢：采集线程只写 Latest，这里 200ms 批量拉到界面
            _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            _refreshTimer.Tick += (_, _) => RefreshRealtime();
            _refreshTimer.Start();
        }

        [ObservableProperty] private string timeStr = string.Empty;
        [ObservableProperty] private string dateStr = string.Empty;
        [ObservableProperty] private string weekStr = string.Empty;
        [ObservableProperty] private string machineCount = string.Empty;
        [ObservableProperty] private string productCount = string.Empty;
        [ObservableProperty] private string badCount = string.Empty;

        public ObservableCollection<EnvironmentModel> EnvironmentList { get; }
        public List<AlarmModel> AlarmList { get; }
        public List<DeviceModel> DeviceList { get; }
        public List<RaderModel> RaderList { get; }
        public List<StaffOutModel> StaffOutWorkList { get; }
        public List<WorkShopModel> WorkShopList { get; }

        private void UpdateClock()
        {
            var now = DateTime.Now;
            TimeStr = now.ToString("HH:mm");
            DateStr = now.ToString("yyyy-MM-dd");   // 修掉教程的 "yyyy-MM--dd"
            WeekStr = new[] { "星期日", "星期一", "星期二", "星期三", "星期四", "星期五", "星期六" }[(int)now.DayOfWeek];
        }

        /// <summary>
        /// 把采集层的最新值搬到界面对象上。
        /// 只有采集层真的读到该点位才覆盖，串口未接时大屏保持默认演示值。
        /// </summary>
        private void RefreshRealtime()
        {
            // 环境 7 项
            SetEnvValue("光照(Lux)", "光照");
            SetEnvValue("噪音(db)", "噪音");
            SetEnvValue("温度(℃)", "车间温度");
            SetEnvValue("湿度(%)", "车间湿度");
            SetEnvValue("PM2.5(µg/m³)", "PM2.5");
            SetEnvValue("硫化氢(PPM)", "硫化氢");
            SetEnvValue("氮气(PPM)", "氮气");

            // 设备 8 项（注意「温度(℃)」环境与设备里都有，所以分开两个方法按列表匹配）
            SetDeviceValue("电能(Kw.h)", "电能");
            SetDeviceValue("电压(V)", "电压");
            SetDeviceValue("电流(A)", "电流");
            SetDeviceValue("压差(kpa)", "压差");
            SetDeviceValue("温度(℃)", "设备温度");
            SetDeviceValue("振动(mm/s)", "振动");
            SetDeviceValue("转速(r/min)", "转速");
            SetDeviceValue("气压(kpa)", "气压");

            // 车间汇总（第二批）
            RefreshWorkShops();

            // 机台级状态（第三批）：拿到数据后反过来覆盖车间汇总，保证四张卡片与详情页机台数一致
            RefreshMachines();
        }

        /// <summary>
        /// 把机台块的实时状态（0 运行 / 1 等待 / 2 故障 / 3 停机）搬到每个车间的机台列表上，
        /// 再用机台状态重新统计车间四类数量与顶部机台总数。
        /// </summary>
        private void RefreshMachines()
        {
            bool any = false;

            foreach (var ws in WorkShopList)
            {
                var list = ws.MachineList;
                for (int i = 0; i < list.Count; i++)
                {
                    if (!_polling.Machines.TryGetValue($"{ws.WorkShopName}#{i}", out var st)) continue;

                    any = true;
                    var machine = list[i];
                    machine.Status = st.Status switch
                    {
                        0 => "作业中",
                        1 => "等待",
                        2 => "故障",
                        _ => "停机"
                    };
                    machine.PlanCount = st.Plan;
                    machine.FinishedCount = st.Finished;
                }
            }

            if (!any) return;

            foreach (var ws in WorkShopList)
            {
                ws.WorkingCount = ws.MachineList.Count(m => m.Status == "作业中");
                ws.WaitCount = ws.MachineList.Count(m => m.Status == "等待");
                ws.WrongCount = ws.MachineList.Count(m => m.Status == "故障");
                ws.StopCount = ws.MachineList.Count(m => m.Status == "停机");
            }

            MachineCount = WorkShopList.Sum(x => x.TotalCount).ToString("D4");
        }

        /// <summary>
        /// 车间汇总：4 车间 × 作业/等待/故障/停机，点位名形如「贴片车间作业」。
        /// 顶部「机台总数」由四个车间汇总得到，跟着 PLC 一起变化。
        /// </summary>
        private void RefreshWorkShops()
        {
            foreach (var ws in WorkShopList)
            {
                if (_polling.Latest.TryGetValue(ws.WorkShopName + "作业", out var w)) ws.WorkingCount = (int)Math.Round(w);
                if (_polling.Latest.TryGetValue(ws.WorkShopName + "等待", out var a)) ws.WaitCount = (int)Math.Round(a);
                if (_polling.Latest.TryGetValue(ws.WorkShopName + "故障", out var f)) ws.WrongCount = (int)Math.Round(f);
                if (_polling.Latest.TryGetValue(ws.WorkShopName + "停机", out var s)) ws.StopCount = (int)Math.Round(s);
            }

            MachineCount = WorkShopList.Sum(x => x.TotalCount).ToString("D4");
        }

        private void SetEnvValue(string itemName, string pointName)
        {
            if (!_polling.Latest.TryGetValue(pointName, out var value)) return;

            foreach (var item in EnvironmentList)
            {
                if (item.EnItemName == itemName)
                {
                    item.EnItemValue = value;
                    return;
                }
            }
        }

        private void SetDeviceValue(string itemName, string pointName)
        {
            if (!_polling.Latest.TryGetValue(pointName, out var value)) return;

            foreach (var item in DeviceList)
            {
                if (item.DeviceItem == itemName)
                {
                    item.Value = value;
                    return;
                }
            }
        }
    }
}
