using DeviceMonitor.Models;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DeviceMonitor.Views
{
    /// <summary>
    /// 雷达图控件：根据 ItemRaderSource 画五边形数据图。
    /// </summary>
    public partial class RaderUC : UserControl
    {
        public RaderUC()
        {
            InitializeComponent();
            SizeChanged += OnSizeChanged;
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            Drag();
        }

        // 数据源，支持数据绑定（依赖属性）
        public List<RaderModel> ItemRaderSource
        {
            get { return (List<RaderModel>)GetValue(ItemRaderSourceProperty); }
            set { SetValue(ItemRaderSourceProperty, value); }
        }

        public static readonly DependencyProperty ItemRaderSourceProperty =
            DependencyProperty.Register(
                "ItemRaderSource",
                typeof(List<RaderModel>),
                typeof(RaderUC),
                new PropertyMetadata(null, OnItemRaderSourceChanged));

        private static void OnItemRaderSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((RaderUC)d).Drag();
        }

        // 画图方法
        public void Drag()
        {
            if (ItemRaderSource == null || ItemRaderSource.Count <= 0)
            {
                return;
            }

            mainCanvas.Children.Clear();
            P1.Points.Clear();
            P2.Points.Clear();
            P3.Points.Clear();
            P4.Points.Clear();
            P5.Points.Clear();

            // 调整为正方形
            double size = Math.Min(RenderSize.Width, RenderSize.Height);
            LayGrid.Height = size;
            LayGrid.Width = size;

            double raduis = size / 2;
            double step = 360.0 / ItemRaderSource.Count;

            for (int i = 0; i < ItemRaderSource.Count; i++)
            {
                double x = (raduis - 20) * Math.Cos((step * i - 90) * Math.PI / 180);
                double y = (raduis - 20) * Math.Sin((step * i - 90) * Math.PI / 180);

                P1.Points.Add(new Point(raduis + x, raduis + y));
                P2.Points.Add(new Point(raduis + x * 0.75, raduis + y * 0.75));
                P3.Points.Add(new Point(raduis + x * 0.5, raduis + y * 0.5));
                P4.Points.Add(new Point(raduis + x * 0.25, raduis + y * 0.25));
                P5.Points.Add(new Point(raduis + x * ItemRaderSource[i].Value * 0.01, raduis + y * ItemRaderSource[i].Value * 0.01));

                TextBlock txt = new TextBlock();
                txt.Width = 60;
                txt.FontSize = 10;
                txt.TextAlignment = TextAlignment.Center;
                txt.Text = ItemRaderSource[i].ItemName;
                txt.Foreground = new SolidColorBrush(Color.FromArgb(100, 255, 255, 255));
                txt.SetValue(Canvas.LeftProperty, raduis + (raduis - 10) * Math.Cos((step * i - 90) * Math.PI / 180) - 30);
                txt.SetValue(Canvas.TopProperty, raduis + (raduis - 10) * Math.Sin((step * i - 90) * Math.PI / 180) - 7);
                mainCanvas.Children.Add(txt);
            }
        }
    }
}
