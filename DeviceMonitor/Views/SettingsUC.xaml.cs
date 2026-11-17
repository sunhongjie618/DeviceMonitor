using System.Windows.Controls;

namespace DeviceMonitor.Views
{
    /// <summary>
    /// 系统设置页。View 由 App.xaml 的隐式 DataTemplate 创建（DataContext 自动是 SettingsViewModel），
    /// 所以这里只需要无参构造，不要再写 DataContext = xx。
    /// </summary>
    public partial class SettingsUC : UserControl
    {
        public SettingsUC()
        {
            InitializeComponent();
        }
    }
}
