using CommunityToolkit.Mvvm.ComponentModel;
using DeviceMonitor.Services;
using System.Threading.Tasks;

namespace DeviceMonitor.ViewModels
{
    /// <summary>
    /// 登录窗 VM。密码由 PasswordBox 直接传给 <see cref="SignInAsync"/>（PasswordBox 不支持绑定）。
    /// </summary>
    public partial class LoginViewModel : ObservableObject
    {
        private readonly IAuthService _auth;
        private readonly CurrentUserService _currentUser;

        public LoginViewModel(IAuthService auth, CurrentUserService currentUser)
        {
            _auth = auth;
            _currentUser = currentUser;
        }

        [ObservableProperty]
        private string userName = "admin";

        [ObservableProperty]
        private string errorText = string.Empty;

        [ObservableProperty]
        private bool isBusy;

        public string HintText => "默认账号：admin / admin123　操作员：operator / 123456";

        public string CaptionText => "生产监控平台 · 设备数据采集系统";

        public async Task<bool> SignInAsync(string password)
        {
            if (IsBusy) return false;

            IsBusy = true;
            ErrorText = string.Empty;
            try
            {
                var (user, error) = await _auth.SignInAsync(UserName, password);
                if (user is null)
                {
                    ErrorText = error ?? "登录失败";
                    return false;
                }

                _currentUser.SetUser(user);
                return true;
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
