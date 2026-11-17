using DeviceMonitor.Data;
using DeviceMonitor.PlcClient;
using DeviceMonitor.Services;
using DeviceMonitor.ViewModels;
using DeviceMonitor.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace DeviceMonitor
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static IServiceProvider Services { get; private set; } = null!;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 登录窗关闭到主窗显示之间会短暂「一个窗口都没有」，
            // 默认的 OnLastWindowClose 会在这时直接关掉进程 —— 改成显式退出。
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // 全局异常兜底：任何线程上的未处理异常都先写日志，便于定位（日志在 %TEMP%\dm_crash.log）。
            DispatcherUnhandledException += (_, args) => LogCrash("Dispatcher", args.Exception);
            AppDomain.CurrentDomain.UnhandledException += (_, args) => LogCrash("AppDomain", args.ExceptionObject as Exception);
            TaskScheduler.UnobservedTaskException += (_, args) => LogCrash("UnobservedTask", args.Exception);

            var services = new ServiceCollection();

            // 数据库：WPF 没有请求作用域，用工厂创建短生命周期的 DbContext
            services.AddDbContextFactory<AppDbContext>(o => o.UseSqlite(DbBootstrapper.ConnectionString));

            // 数据与基础设施：整个应用一份
            services.AddSingleton<MonitorDataService>();
            services.AddSingleton<ISettingsService, SettingsService>();
            services.AddSingleton<IPlcClient>(sp => new ModbusRtuPlcClient(sp.GetRequiredService<ISettingsService>().Current.Plc));
            services.AddSingleton<PlcPollingService>();
            services.AddSingleton<IDataStore, DataStore>();
            services.AddSingleton<PersistenceService>();

            // 登录与当前用户
            services.AddSingleton<CurrentUserService>();
            services.AddSingleton<IAuthService, AuthService>();
            services.AddTransient<LoginViewModel>();
            services.AddTransient<LoginWindow>();

            // 外壳：主窗口与其 VM 各一份
            services.AddSingleton<MainWinViewModel>();
            services.AddSingleton<MainWin>();

            // 页面 VM：Transient + 导航层缓存做保活。
            // View 由 App.xaml 的 DataTemplate 创建，不需要注册。
            services.AddTransient<MonitorUCViewModel>();
            services.AddTransient<WorkshopListUCViewModel>();
            services.AddTransient<DeviceUCViewModel>();
            services.AddTransient<AlarmUCViewModel>();
            services.AddTransient<ReportUCViewModel>();
            services.AddTransient<SettingsViewModel>();

            Services = services.BuildServiceProvider();

            // 建库建表 + 首次写入默认账号。失败就不让进主界面，避免后面到处 NRE。
            try
            {
                using var db = Services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext();
                DbBootstrapper.EnsureDatabase(db);
            }
            catch (Exception ex)
            {
                LogCrash("Database", ex);
                MessageBox.Show("数据库初始化失败：\n" + ex.Message + "\n\n路径：" + DbBootstrapper.DbPath,
                    "启动失败", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
                return;
            }

            // 登录闸门：取消或关闭登录窗直接退出
            var login = Services.GetRequiredService<LoginWindow>();
            if (login.ShowDialog() != true)
            {
                Shutdown();
                return;
            }

            MainWindow = Services.GetRequiredService<MainWin>();
            MainWindow.Closed += (_, _) => Shutdown();
            MainWindow.Show();

            // 启动后台采集（串口未接时只是后台重试，不影响界面）与落库
            Services.GetRequiredService<PlcPollingService>().Start();
            Services.GetRequiredService<PersistenceService>().Start();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Services.GetRequiredService<PersistenceService>().Stop();
            Services.GetRequiredService<PlcPollingService>().Stop();
            base.OnExit(e);
        }

        private static void LogCrash(string source, Exception? ex)
        {
            if (ex is null) return;

            try
            {
                var path = Path.Combine(Path.GetTempPath(), "dm_crash.log");
                var text = $"""
                    ===== {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{source}] =====
                    thread id   : {Thread.CurrentThread.ManagedThreadId} (ui={Thread.CurrentThread.ManagedThreadId == Application.Current?.Dispatcher.Thread.ManagedThreadId})
                    has access  : {Application.Current?.Dispatcher.CheckAccess()}
                    {"\n"}{ex}
                    {"\n"}
                    """;
                File.AppendAllText(path, text);
            }
            catch
            {
                // 记日志本身失败就放弃，绝不能在异常处理里再抛
            }
        }
    }
}
