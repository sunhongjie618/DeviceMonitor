using CommunityToolkit.Mvvm.ComponentModel;
using DeviceMonitor.Models;

namespace DeviceMonitor.ViewModels
{
    /// <summary>点位表的一行（可编辑）。Model 只负责存取，编辑态放在 VM 里。</summary>
    public partial class PlcPointViewModel : ObservableObject
    {
        [ObservableProperty] private string name = string.Empty;
        [ObservableProperty] private string address = string.Empty;
        [ObservableProperty] private string dataType = "UInt16";
        [ObservableProperty] private string unit = string.Empty;

        public PlcPointViewModel() { }

        public PlcPointViewModel(PlcPoint point)
        {
            Name = point.Name;
            Address = point.Address;
            DataType = point.DataType;
            Unit = point.Unit;
        }

        public PlcPoint ToModel() => new()
        {
            Name = Name?.Trim() ?? string.Empty,
            Address = Address?.Trim() ?? string.Empty,
            DataType = DataType?.Trim() ?? "UInt16",
            Unit = Unit?.Trim() ?? string.Empty
        };
    }
}
