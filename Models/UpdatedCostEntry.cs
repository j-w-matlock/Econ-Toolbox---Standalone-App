using EconToolbox.Desktop.Models;

namespace EconToolbox.Desktop.Models
{
    public class UpdatedCostEntry : ObservableObject
    {
        private string _category = string.Empty;
        private double _actualCost;
        private double _updateFactor;
        private double _updatedCost;

        public string Category
        {
            get => _category;
            set { _category = value; OnPropertyChanged(); }
        }

        public double ActualCost
        {
            get => _actualCost;
            set { _actualCost = value; OnPropertyChanged(); }
        }

        public double UpdateFactor
        {
            get => _updateFactor;
            set { _updateFactor = value; OnPropertyChanged(); }
        }

        public double UpdatedCost
        {
            get => _updatedCost;
            set { _updatedCost = value; OnPropertyChanged(); }
        }
    }
}
