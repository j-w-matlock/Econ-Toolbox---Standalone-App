namespace EconToolbox.Desktop.Models
{
    public class HistoricalVisitationRow : ObservableObject
    {
        private string? _label;
        private string _visitationText = string.Empty;

        public string? Label
        {
            get => _label;
            set
            {
                if (_label != value)
                {
                    _label = value;
                    OnPropertyChanged();
                }
            }
        }

        public string VisitationText
        {
            get => _visitationText;
            set
            {
                if (_visitationText != value)
                {
                    _visitationText = value;
                    OnPropertyChanged();
                }
            }
        }
    }
}
