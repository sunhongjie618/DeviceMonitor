using DeviceMonitor.Models;
using System.Threading.Tasks;

namespace DeviceMonitor.Services
{
    public interface IAuthService
    {
        /// <summary>校验账号密码。成功返回用户，失败返回错误文案。</summary>
        Task<(AppUser? User, string? Error)> SignInAsync(string userName, string password);
    }
}
