using System;
using System.Collections.Generic;
using System.Windows.Input;
using EconToolbox.Desktop.Models;

namespace EconToolbox.Desktop.ViewModels
{
    public class AnnualizerViewModel : BaseViewModel
    {
        private double _firstCost;
        private double _rate = 5.0;
        private double _annualOm;
        private double _annualBenefits;
        private string _futureCosts = string.Empty;

        private double _idc;
        private double _totalInvestment;
        private double _crf;
        private double _annualCost;
        private double _bcr;

        public double FirstCost
        {
            get => _firstCost;
            set { _firstCost = value; OnPropertyChanged(); }
        }

        public double Rate
        {
            get => _rate;
            set { _rate = value; OnPropertyChanged(); }
        }

        public double AnnualOm
        {
            get => _annualOm;
            set { _annualOm = value; OnPropertyChanged(); }
        }

        public double AnnualBenefits
        {
            get => _annualBenefits;
            set { _annualBenefits = value; OnPropertyChanged(); }
        }

        public string FutureCosts
        {
            get => _futureCosts;
            set { _futureCosts = value; OnPropertyChanged(); }
        }

        public double Idc
        {
            get => _idc;
            set { _idc = value; OnPropertyChanged(); }
        }

        public double TotalInvestment
        {
            get => _totalInvestment;
            set { _totalInvestment = value; OnPropertyChanged(); }
        }

        public double Crf
        {
            get => _crf;
            set { _crf = value; OnPropertyChanged(); }
        }

        public double AnnualCost
        {
            get => _annualCost;
            set { _annualCost = value; OnPropertyChanged(); }
        }

        public double Bcr
        {
            get => _bcr;
            set { _bcr = value; OnPropertyChanged(); }
        }

        public ICommand ComputeCommand { get; }

        public AnnualizerViewModel()
        {
            ComputeCommand = new RelayCommand(Compute);
        }

        private void Compute()
        {
            try
            {
                List<(double cost, int year)> future = new();
                if (!string.IsNullOrWhiteSpace(FutureCosts))
                {
                    var items = FutureCosts.Split(',', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var item in items)
                    {
                        var parts = item.Split(':', '@');
                        if (parts.Length == 2)
                        {
                            double c = double.Parse(parts[0].Trim());
                            int y = int.Parse(parts[1].Trim());
                            future.Add((c, y));
                        }
                    }
                }

                var result = AnnualizerModel.Compute(FirstCost, Rate / 100.0, AnnualOm, AnnualBenefits, future);
                Idc = result.Idc;
                TotalInvestment = result.TotalInvestment;
                Crf = result.Crf;
                AnnualCost = result.AnnualCost;
                Bcr = result.Bcr;
            }
            catch (Exception ex)
            {
                Idc = TotalInvestment = Crf = AnnualCost = Bcr = double.NaN;
            }
        }
    }
}

