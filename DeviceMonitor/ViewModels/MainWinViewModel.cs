using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeviceMonitor.Models;
using DeviceMonitor.Services;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DeviceMonitor.ViewModels
{
    /// <summary>
    /// 主窗口视图模型：负责左侧菜单、区域导航（ContentControl 换页面）和跨页命令。
    /// View 一律由 App.xaml 的隐式 DataTemplate 根据 VM 类型创建，这里只产出 VM。
    /// </summary>
    public partial class MainWinViewModel : ObservableObject
    {
        private readonly IServiceProvider _services;
        private readonly Dictionary<string, object> _pageCache = new();
        private readonly Stack<object> _backStack = new();

        private readonly CurrentUserService _currentUser;

        public MainWinViewModel(IServiceProvider services, CurrentUserService currentUser)
        {
            _services = services;
            _currentUser = currentUser;

            CreateMenu();

            // 权限：操作员看不到「系统设置」（菜单级过滤是最省事也最直观的落点）
            if (!_currentUser.IsAdmin)
                LeftMenuList.RemoveAll(m => m.ViewName == "SettingsUC");

            SelectedMenu = LeftMenuList[0];   // 默认选中首项「总览大屏」，触发导航
        }

        [ObservableProperty]
        private List<LeftMenuInfo> leftMenuList = new();

        [ObservableProperty]
        private LeftMenuInfo? selectedMenu;

        [ObservableProperty]
        private object? currentPage;

        // 菜单选中变化 → 切换区域页面
        partial void OnSelectedMenuChanged(LeftMenuInfo? value)
        {
            if (value is null) return;

            _backStack.Clear();
            CurrentPage = GetOrCreatePage(value.ViewName);
        }

        /// <summary>按菜单 ViewName 取页面 VM（缓存 = 页面保活，切回来不重建）</summary>
        private object GetOrCreatePage(string viewName)
        {
            if (_pageCache.TryGetValue(viewName, out var cached))
                return cached;

            object page = viewName switch
            {
                "MonitorUC" => _services.GetRequiredService<MonitorUCViewModel>(),
                "WorkshopUC" => _services.GetRequiredService<WorkshopListUCViewModel>(),
                "DeviceUC" => _services.GetRequiredService<DeviceUCViewModel>(),
                "AlarmUC" => _services.GetRequiredService<AlarmUCViewModel>(),
                "ReportUC" => _services.GetRequiredService<ReportUCViewModel>(),
                "SettingsUC" => _services.GetRequiredService<SettingsViewModel>(),
                _ => _services.GetRequiredService<MonitorUCViewModel>()
            };

            _pageCache[viewName] = page;
            return page;
        }

        /// <summary>大屏右上角「配置」按钮：跳到系统设置页</summary>
        [RelayCommand]
        private void ShowSettings()
        {
            var item = LeftMenuList.FirstOrDefault(m => m.ViewName == "SettingsUC");
            if (item is null) return;   // 操作员没有这一项，直接忽略

            // 已经停在系统设置页时要主动刷新一次，否则 SelectedMenu 没变、不会触发导航
            if (ReferenceEquals(SelectedMenu, item))
            {
                _backStack.Clear();
                CurrentPage = GetOrCreatePage(item.ViewName);
                return;
            }

            SelectedMenu = item;
        }

        /// <summary>大屏车间卡片上的 [详情]：下钻到该车间的详情页</summary>
        [RelayCommand]
        private void ShowDetail(WorkShopModel? workShop)
        {
            if (workShop is null) return;

            Navigate(ActivatorUtilities.CreateInstance<WorkShopDetailUCViewModel>(_services, workShop));
        }

        /// <summary>车间详情页的「返回」：回到进入前的页面（大屏或车间列表）</summary>
        [RelayCommand]
        private void GoBackMonitor()
        {
            CurrentPage = _backStack.Count > 0
                ? _backStack.Pop()
                : GetOrCreatePage("MonitorUC");
        }

        private void Navigate(object page)
        {
            if (CurrentPage is not null)
                _backStack.Push(CurrentPage);

            CurrentPage = page;
        }

        // 静态菜单数据
        private void CreateMenu()
        {
            LeftMenuList.Add(new LeftMenuInfo { Icon = "Home", MenuName = "总览大屏", ViewName = "MonitorUC" });
            LeftMenuList.Add(new LeftMenuInfo { Icon = "Factory", MenuName = "车间监控", ViewName = "WorkshopUC" });
            LeftMenuList.Add(new LeftMenuInfo { Icon = "Wrench", MenuName = "设备管理", ViewName = "DeviceUC" });
            LeftMenuList.Add(new LeftMenuInfo { Icon = "Bell", MenuName = "报警中心", ViewName = "AlarmUC" });
            LeftMenuList.Add(new LeftMenuInfo { Icon = "ChartBar", MenuName = "数据报表", ViewName = "ReportUC" });
            LeftMenuList.Add(new LeftMenuInfo { Icon = "Cog", MenuName = "系统设置", ViewName = "SettingsUC" });
        }
    }
}
