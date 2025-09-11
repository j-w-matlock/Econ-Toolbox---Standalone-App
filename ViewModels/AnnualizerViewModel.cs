using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using EconToolbox.Desktop.Models;
using EconToolbox.Desktop.Services;

namespace EconToolbox.Desktop.ViewModels
{
    public class AnnualizerViewModel : BaseViewModel
    {
        private double _firstCost;
        private double _rate = 5.0;
        private int _periods = 1;
        private double _annualOm;
        private double _annualBenefits;
        private ObservableCollection<FutureCostEntry> _futureCosts = new();

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

        public int Periods
        {
            get => _periods;
            set { _periods = value; OnPropertyChanged(); }
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

        public ObservableCollection<FutureCostEntry> FutureCosts
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
        public ICommand ExportCommand { get; }

        public AnnualizerViewModel()
        {
            ComputeCommand = new RelayCommand(Compute);
            ExportCommand = new RelayCommand(Export);
        }

        private void Compute()
        {
            try
            {
                List<(double cost, int year)> future = FutureCosts
                    .Select(f => (f.Cost, f.Year))
                    .ToList();

                var result = AnnualizerModel.Compute(FirstCost, Rate / 100.0, AnnualOm, AnnualBenefits, future, Periods);
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

        private void Export()
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Excel Workbook (*.xlsx)|*.xlsx",
                FileName = "annualizer.xlsx"
            };
            if (dlg.ShowDialog() == true)
            {
                Services.ExcelExporter.ExportAnnualizer(FirstCost, Rate, AnnualOm, AnnualBenefits,
                    FutureCosts, Idc, TotalInvestment, Crf, AnnualCost, Bcr, dlg.FileName);
            }
        }
    }
}

