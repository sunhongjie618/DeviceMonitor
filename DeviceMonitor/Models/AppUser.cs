using System;

namespace DeviceMonitor.Models
{
    /// <summary>
    /// 登录用户（数据库实体）。密码只存 PBKDF2 哈希 + 每用户独立盐，不存明文。
    /// </summary>
    public class AppUser
    {
        public int Id { get; set; }

        public string UserName { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        /// <summary>管理员 / 操作员</summary>
        public string Role { get; set; } = "操作员";

        public string Salt { get; set; } = string.Empty;

        public string PasswordHash { get; set; } = string.Empty;

        public bool IsEnabled { get; set; } = true;

        /// <summary>首次登录（或重置后）是否强制改密码。</summary>
        public bool MustChangePassword { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? LastLoginAt { get; set; }

        /// <summary>连续失败次数，达到阈值后锁定。</summary>
        public int FailedCount { get; set; }

        public DateTime? LockedUntil { get; set; }
    }
}
