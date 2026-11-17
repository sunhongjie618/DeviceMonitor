using DeviceMonitor.Models;
using DeviceMonitor.Services;
using System.Collections.Generic;

namespace DeviceMonitor.ViewModels
{
    /// <summary>
    /// 车间监控（列表页）视图模型：菜单进入的入口页，点「进入详情」再下钻到车间详情。
    /// </summary>
    public class WorkshopListUCViewModel
    {
        public WorkshopListUCViewModel(MonitorDataService data)
        {
            WorkShops = data.WorkShopList;
        }

        public List<WorkShopModel> WorkShops { get; }
    }
}
