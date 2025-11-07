using System;

namespace EconToolbox.Desktop.Models
{
    public class ScrbEntry : ObservableObject
    {
        private string _featureName = string.Empty;
        private double _separableCost;
        private double _remainingBenefit;
        private double _adjustedCost;
        private double _adjustedBenefit;
        private double? _scrbRatio;

        public string FeatureName
        {
            get => _featureName;
            set
            {
                if (_featureName == value)
                    return;
                _featureName = value;
                OnPropertyChanged();
            }
        }

        public double SeparableCost
        {
            get => _separableCost;
            set
            {
                if (Math.Abs(_separableCost - value) < 0.000001)
                    return;
                _separableCost = value;
                OnPropertyChanged();
            }
        }

        public double RemainingBenefit
        {
            get => _remainingBenefit;
            set
            {
                if (Math.Abs(_remainingBenefit - value) < 0.000001)
                    return;
                _remainingBenefit = value;
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

        public double? ScrbRatio
        {
            get => _scrbRatio;
            set
            {
                if (_scrbRatio.HasValue == value.HasValue &&
                    (!_scrbRatio.HasValue || Math.Abs(_scrbRatio.Value - value!.Value) < 0.000001))
                    return;
                _scrbRatio = value;
                OnPropertyChanged();
            }
        }
    }
}
