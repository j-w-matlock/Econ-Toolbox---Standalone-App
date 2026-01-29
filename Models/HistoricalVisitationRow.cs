using CommunityToolkit.Mvvm.ComponentModel;

namespace EconToolbox.Desktop.Models
{
    public partial class HistoricalVisitationRow : ObservableObject
    {
        [ObservableProperty]
        private string? _label;

        [ObservableProperty]
        private string _visitationText = string.Empty;
    }
}
