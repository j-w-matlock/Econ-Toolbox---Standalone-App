
namespace EconToolbox.Desktop.Models
{
    public class AnnualizerScenario : ObservableObject
    {
        private string _name = string.Empty;
        public string Name
        {
            get => _name;
            set
            {
                if (_name == value)
                    return;
                _name = value;
                OnPropertyChanged();
            }
        }

        private double _firstCost;
        public double FirstCost
        {
            get => _firstCost;
            set
            {
                if (value == _firstCost)
                    return;
                _firstCost = value;
                OnPropertyChanged();
            }
        }

        private double _annualOm;
        public double AnnualOm
        {
            get => _annualOm;
            set
            {
                if (value == _annualOm)
                    return;
                _annualOm = value;
                OnPropertyChanged();
            }
        }

        private double _annualBenefits;
        public double AnnualBenefits
        {
            get => _annualBenefits;
            set
            {
                if (value == _annualBenefits)
                    return;
                _annualBenefits = value;
                OnPropertyChanged();
            }
        }

        private double _rate;
        public double Rate
        {
            get => _rate;
            set
            {
                if (value == _rate)
                    return;
                _rate = value;
                OnPropertyChanged();
            }
        }

        private double _idc;
        public double Idc
        {
            get => _idc;
            set
            {
                if (value == _idc)
                    return;
                _idc = value;
                OnPropertyChanged();
            }
        }

        private double _futureCostPv;
        public double FutureCostPv
        {
            get => _futureCostPv;
            set
            {
                if (value.Equals(_futureCostPv))
                    return;
                _futureCostPv = value;
                OnPropertyChanged();
            }
        }

        private double _totalInvestment;
        public double TotalInvestment
        {
            get => _totalInvestment;
            set
            {
                if (value == _totalInvestment)
                    return;
                _totalInvestment = value;
                OnPropertyChanged();
            }
        }

        private double _crf;
        public double Crf
        {
            get => _crf;
            set
            {
                if (value == _crf)
                    return;
                _crf = value;
                OnPropertyChanged();
            }
        }

        private double _annualCost;
        public double AnnualCost
        {
            get => _annualCost;
            set
            {
                if (value == _annualCost)
                    return;
                _annualCost = value;
                OnPropertyChanged();
            }
        }

        private double _bcr;
        public double Bcr
        {
            get => _bcr;
            set
            {
                if (value == _bcr)
                    return;
                _bcr = value;
                OnPropertyChanged();
            }
        }

        private double? _unityBcrFirstCost;
        public double? UnityBcrFirstCost
        {
            get => _unityBcrFirstCost;
            set
            {
                if (_unityBcrFirstCost.HasValue == value.HasValue &&
                    (!_unityBcrFirstCost.HasValue || _unityBcrFirstCost.Value.Equals(value!.Value)))
                    return;
                _unityBcrFirstCost = value;
                OnPropertyChanged();
            }
        }

        private string? _notes;
        public string? Notes
        {
            get => _notes;
            set
            {
                if (_notes == value)
                    return;
                _notes = value;
                OnPropertyChanged();
            }
        }
    }
}
