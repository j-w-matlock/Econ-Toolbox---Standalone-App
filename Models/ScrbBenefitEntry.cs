using System;

namespace EconToolbox.Desktop.Models
{
    public class ScrbBenefitEntry : ObservableObject
    {
        private string _featureName = string.Empty;
        private double _originalBenefit;
        private int? _originalYear;
        private double _updateFactor = 1.0;
        private double _adjustedBenefit;
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

        public double OriginalBenefit
        {
            get => _originalBenefit;
            set
            {
                if (Math.Abs(_originalBenefit - value) < 0.000001)
                    return;
                _originalBenefit = value;
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

        public double AdjustedBenefit
        {
            get => _adjustedBenefit;
            set
            {
                if (Math.Abs(_adjustedBenefit - value) < 0.000001)
                    return;
                _adjustedBenefit = value;
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
