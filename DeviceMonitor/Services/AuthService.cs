using DeviceMonitor.Data;
using DeviceMonitor.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;

namespace DeviceMonitor.Services
{
    /// <summary>
    /// 登录校验：查用户 → 校验 PBKDF2 → 记录登录时间 / 失败次数 / 锁定。
    /// </summary>
    public class AuthService : IAuthService
    {
        private const int MaxFailedCount = 5;
        private static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(5);

        private readonly IDbContextFactory<AppDbContext> _factory;

        public AuthService(IDbContextFactory<AppDbContext> factory) => _factory = factory;

        public async Task<(AppUser? User, string? Error)> SignInAsync(string userName, string password)
        {
            userName = (userName ?? string.Empty).Trim();
            if (userName.Length == 0 || string.IsNullOrEmpty(password))
                return (null, "请输入用户名和密码");

            using var db = _factory.CreateDbContext();
            var user = await db.Users.FirstOrDefaultAsync(u => u.UserName == userName);
            if (user is null)
                return (null, "用户名或密码错误");

            var now = DateTime.Now;
            if (!user.IsEnabled)
                return (null, "该账号已被禁用，请联系管理员");

            if (user.LockedUntil is { } until && until > now)
                return (null, $"登录失败次数过多，请 {Math.Ceiling((until - now).TotalMinutes)} 分钟后再试");

            if (!PasswordHasher.Verify(password, user.Salt, user.PasswordHash))
            {
                user.FailedCount++;
                var left = MaxFailedCount - user.FailedCount;
                if (user.FailedCount >= MaxFailedCount)
                {
                    user.FailedCount = 0;
                    user.LockedUntil = now.Add(LockDuration);
                }
                await db.SaveChangesAsync();
                return (null, left > 0 ? $"用户名或密码错误，还可尝试 {left} 次" : "失败次数过多，账号已锁定 5 分钟");
            }

            user.FailedCount = 0;
            user.LockedUntil = null;
            user.LastLoginAt = now;
            await db.SaveChangesAsync();
            return (user, null);
        }
    }
}
