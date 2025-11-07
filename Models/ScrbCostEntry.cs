using System;

namespace EconToolbox.Desktop.Models
{
    public class ScrbCostEntry : ObservableObject
    {
        private string _featureName = string.Empty;
        private double _originalCost;
        private int? _originalYear;
        private double _updateFactor = 1.0;
        private double _adjustedCost;
        private bool _hasDataIssue;

        public string FeatureName
        {
            get => _featureName;
            set
            {
                if (string.Equals(_featureName, value, StringComparison.Ordinal))
                    return;
                _featureName = value ?? string.Empty;
                OnPropertyChanged();
            }
        }

        public double OriginalCost
        {
            get => _originalCost;
            set
            {
                if (Math.Abs(_originalCost - value) < 0.000001)
                    return;
                _originalCost = value;
                OnPropertyChanged();
            }
        }

        public int? OriginalYear
        {
            get => _originalYear;
            set
            {
                if (_originalYear == value)
                    return;
                _originalYear = value;
                OnPropertyChanged();
            }
        }

        public double UpdateFactor
        {
            get => _updateFactor;
            set
            {
                if (Math.Abs(_updateFactor - value) < 0.000001)
                    return;
                _updateFactor = value;
                OnPropertyChanged();
            }
        }

        public double AdjustedCost
        {
            get => _adjustedCost;
            set
            {
                if (Math.Abs(_adjustedCost - value) < 0.000001)
                    return;
                _adjustedCost = value;
                OnPropertyChanged();
            }
        }

        public bool HasDataIssue
        {
            get => _hasDataIssue;
            set
            {
                if (_hasDataIssue == value)
                    return;
                _hasDataIssue = value;
                OnPropertyChanged();
            }
        }
    }
}
