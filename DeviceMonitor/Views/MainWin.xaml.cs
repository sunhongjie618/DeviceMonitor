using DeviceMonitor.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace DeviceMonitor.Views
{
    /// <summary>
    /// MainWin.xaml 的交互逻辑
    /// </summary>
    public partial class MainWin : Window
    {
        public MainWin(MainWinViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
        }

        //最小化
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        //最大化
        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            if(this.WindowState== WindowState.Normal)
            {
                this.WindowState = WindowState.Maximized;
            }
            else if(this.WindowState== WindowState.Maximized)
            {
                this.WindowState = WindowState.Normal;
            }
            
        }

        //关闭：走 WPF 正常关停流程（Environment.Exit 会跳过 Dispatcher 关闭，调试器下容易假死）
        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void DrawerMask_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            MenuToggleButton.IsChecked = false;   // 关抽屉：让汉堡按钮回到未选中
            e.Handled = true;                     // 别再往下传，避免"点遮罩"顺带点到底下的内容
        }

        // 选完菜单项自动收起抽屉（覆盖式抽屉开着会挡住左侧内容）
        private void MenuListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            MenuToggleButton.IsChecked = false;
        }
    }
}
