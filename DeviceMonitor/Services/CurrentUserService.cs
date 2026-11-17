using DeviceMonitor.Models;
using System;

namespace DeviceMonitor.Services
{
    /// <summary>
    /// 当前登录用户（进程内单例）。菜单/按钮的权限判断都读这里，避免到处传参。
    /// </summary>
    public class CurrentUserService
    {
        public AppUser? User { get; private set; }

        public bool IsSignedIn => User is not null;

        public bool IsAdmin => string.Equals(User?.Role, "管理员", StringComparison.Ordinal);

        public string DisplayName => User?.DisplayName ?? "未登录";

        public void SetUser(AppUser user) => User = user;

        public void Clear() => User = null;
    }
}
