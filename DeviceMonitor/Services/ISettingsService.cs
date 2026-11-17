using DeviceMonitor.Models;

namespace DeviceMonitor.Services
{
    
    public interface ISettingsService
    {
        /// <summary>配置文件完整路径（界面上要显示给用户看）。</summary>
        string FilePath { get; }

        /// <summary>当前生效的配置（已从磁盘载入）。</summary>
        AppSettings Current { get; }

        /// <summary>写盘并替换 Current。</summary>
        void Save(AppSettings settings);
    }
}
