using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DeviceMonitor.Views
{
    /// <summary>
    /// RingUC.xaml 的交互逻辑：环形进度。
    /// </summary>
    public partial class RingUC : UserControl
    {
        public RingUC()
        {
            InitializeComponent();
            SizeChanged += OnSizeChanged;
        }

        // 界面尺寸改变时重画圆环
        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            Drug();
        }

        // 百分比（0~100），支持数据绑定
        public double PercentValue
        {
            get { return (double)GetValue(PercentValueProperty); }
            set { SetValue(PercentValueProperty, value); }
        }

        public static readonly DependencyProperty PercentValueProperty =
            DependencyProperty.Register(
                "PercentValue",
                typeof(double),
                typeof(RingUC),
                new PropertyMetadata(0.0, OnPercentValueChanged));

        private static void OnPercentValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((RingUC)d).Drug();
        }

        // 画圆环
        private void Drug()
        {
            if (RenderSize.Width <= 0 || RenderSize.Height <= 0)
                return;

            LayOutGrid.Width = Math.Min(RenderSize.Width, RenderSize.Height);
            LayOutGrid.Height = LayOutGrid.Width;
            double raduis = LayOutGrid.Width / 2;

            double x = raduis + (raduis - 3) * Math.Cos((PercentValue % 100 * 3.6 - 90) * Math.PI / 180);
            double y = raduis + (raduis - 3) * Math.Sin((PercentValue % 100 * 3.6 - 90) * Math.PI / 180);

            int isFifty = PercentValue < 50 ? 0 : 1;
            string pathStr = $"M{raduis + 0.01} 3A{raduis - 3}  {raduis - 3} 0 {isFifty} 1 {x} {y}";

            var converter = TypeDescriptor.GetConverter(typeof(Geometry));
            path.Data = (Geometry)converter.ConvertFrom(pathStr);
        }
    }
}
