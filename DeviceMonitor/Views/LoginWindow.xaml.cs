using DeviceMonitor.ViewModels;
using System.Windows;
using System.Windows.Input;

namespace DeviceMonitor.Views
{
    /// <summary>
    /// 登录窗：PasswordBox 不支持绑定，所以密码在 code-behind 里取，再交给 VM。
    /// </summary>
    public partial class LoginWindow : Window
    {
        private readonly LoginViewModel _vm;

        public LoginWindow(LoginViewModel vm)
        {
            InitializeComponent();
            _vm = vm;
            DataContext = vm;

            Loaded += (_, _) =>
            {
                UserNameBox.Focus();
                UserNameBox.SelectAll();
            };
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }

        private async void Login_Click(object sender, RoutedEventArgs e) => await TryLoginAsync();

        private async void Password_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                await TryLoginAsync();
        }

        private async System.Threading.Tasks.Task TryLoginAsync()
        {
            LoginButton.IsEnabled = false;
            try
            {
                if (await _vm.SignInAsync(PasswordBox.Password))
                {
                    DialogResult = true;
                    return;
                }

                PasswordBox.Clear();
                PasswordBox.Focus();
            }
            finally
            {
                LoginButton.IsEnabled = true;
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
