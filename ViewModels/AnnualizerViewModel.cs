using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
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
        private int _analysisPeriod = 1;
        private int _baseYear = DateTime.Now.Year;
        private int _constructionMonths = 12;
        private double _annualOm;
        private double _annualBenefits;
        private ObservableCollection<FutureCostEntry> _futureCosts = new();
        private ObservableCollection<FutureCostEntry> _idcEntries = new();
        private ObservableCollection<string> _results = new();

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
            set { _rate = value; OnPropertyChanged(); UpdatePvFactors(); }
        }

        public int AnalysisPeriod
        {
            get => _analysisPeriod;
            set { _analysisPeriod = value; OnPropertyChanged(); }
        }

        public int BaseYear
        {
            get => _baseYear;
            set { _baseYear = value; OnPropertyChanged(); }
        }

        public int ConstructionMonths
        {
            get => _constructionMonths;
            set { _constructionMonths = value; OnPropertyChanged(); }
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

        public ObservableCollection<FutureCostEntry> IdcEntries
        {
            get => _idcEntries;
            set { _idcEntries = value; OnPropertyChanged(); }
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

        public ObservableCollection<string> Results
        {
            get => _results;
            set { _results = value; OnPropertyChanged(); }
        }

        public ICommand ComputeCommand { get; }
        public ICommand ExportCommand { get; }

        public AnnualizerViewModel()
        {
            ComputeCommand = new RelayCommand(Compute);
            ExportCommand = new RelayCommand(Export);

            FutureCosts.CollectionChanged += EntriesChanged;
            IdcEntries.CollectionChanged += EntriesChanged;
        }

        private void EntriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
                foreach (FutureCostEntry entry in e.NewItems)
                    entry.PropertyChanged += EntryOnPropertyChanged;
            UpdatePvFactors();
            Compute();
        }

        private void EntryOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(FutureCostEntry.Year) ||
                e.PropertyName == nameof(FutureCostEntry.Timing) ||
                e.PropertyName == nameof(FutureCostEntry.Cost))
            {
                UpdatePvFactors();
                Compute();
            }
        }

        private void UpdatePvFactors()
        {
            double r = Rate / 100.0;
            foreach (var entry in FutureCosts)
            {
                double offset = entry.Timing switch
                {
                    "beginning" => 0.0,
                    "midpoint" => 0.5,
                    _ => 1.0
                };
                entry.PvFactor = Math.Pow(1.0 + r, -(entry.Year + offset));
            }

            foreach (var entry in IdcEntries)
            {
                double offsetMonths = entry.Timing switch
                {
                    "beginning" => 0.0,
                    "midpoint" => 0.5,
                    _ => 1.0
                };
                double months = entry.Year + offsetMonths;
                entry.PvFactor = Math.Pow(1.0 + r, -(months / 12.0));
            }
        }

        private void Compute()
        {
            try
            {
                List<(double cost, int year)> future = FutureCosts
                    .Select(f => (f.Cost, f.Year))
                    .ToList();

                double[]? costArr = IdcEntries.Count > 0 ? IdcEntries.Select(e => e.Cost).ToArray() : null;
                string[]? timingArr = IdcEntries.Count > 0 ? IdcEntries.Select(e => e.Timing).ToArray() : null;

                var result = AnnualizerModel.Compute(FirstCost, Rate / 100.0, AnnualOm, AnnualBenefits, future,
                    AnalysisPeriod, ConstructionMonths, costArr, timingArr);
                Idc = result.Idc;
                TotalInvestment = result.TotalInvestment;
                Crf = result.Crf;
                AnnualCost = result.AnnualCost;
                Bcr = result.Bcr;

                Results = new ObservableCollection<string>
                {
                    $"IDC: {Idc:F2}",
                    $"Total Investment: {TotalInvestment:F2}",
                    $"CRF: {Crf:F4}",
                    $"Annual Cost: {AnnualCost:F2}",
                    $"BCR: {Bcr:F2}"
                };
            }
            catch (Exception)
            {
                Idc = TotalInvestment = Crf = AnnualCost = Bcr = double.NaN;
                Results = new ObservableCollection<string> { "Error computing results" };
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

