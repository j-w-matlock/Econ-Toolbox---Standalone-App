using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
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
        private string? _unityFirstCostMessage;
        private string _idcTimingBasis = "Middle";
        private bool _calculateInterestAtPeriod;
        private string _idcFirstPaymentTiming = "Beginning";
        private string _idcLastPaymentTiming = "Middle";

        private double _idc;
        private double _totalInvestment;
        private double _crf;
        private double _annualCost;
        private double _bcr;

        private readonly record struct AnnualizerComputationInputs(
            List<(double cost, double yearOffset, double timingOffset)> FutureCosts,
            double[]? IdcCosts,
            string[]? IdcTimings,
            int[]? IdcMonths);

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
            set
            {
                if (_baseYear != value)
                {
                    _baseYear = value;
                    OnPropertyChanged();
                    UpdatePvFactors();
                    Compute();
                }
            }
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

        public IReadOnlyList<string> IdcTimingOptions { get; } = new[] { "Beginning", "Middle", "End" };
        public IReadOnlyList<string> IdcFirstPaymentOptions { get; } = new[] { "Beginning", "End" };
        public IReadOnlyList<string> IdcLastPaymentOptions { get; } = new[] { "Beginning", "Middle", "End" };

        public string IdcTimingBasis
        {
            get => _idcTimingBasis;
            set
            {
                if (_idcTimingBasis != value)
                {
                    _idcTimingBasis = value;
                    OnPropertyChanged();
                    Compute();
                }
            }
        }

        public bool CalculateInterestAtPeriod
        {
            get => _calculateInterestAtPeriod;
            set
            {
                if (_calculateInterestAtPeriod != value)
                {
                    _calculateInterestAtPeriod = value;
                    OnPropertyChanged();
                    Compute();
                }
            }
        }

        public string IdcFirstPaymentTiming
        {
            get => _idcFirstPaymentTiming;
            set
            {
                if (_idcFirstPaymentTiming != value)
                {
                    _idcFirstPaymentTiming = value;
                    OnPropertyChanged();
                    Compute();
                }
            }
        }

        public string IdcLastPaymentTiming
        {
            get => _idcLastPaymentTiming;
            set
            {
                if (_idcLastPaymentTiming != value)
                {
                    _idcLastPaymentTiming = value;
                    OnPropertyChanged();
                    Compute();
                }
            }
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

        public string? UnityFirstCostMessage
        {
            get => _unityFirstCostMessage;
            set { _unityFirstCostMessage = value; OnPropertyChanged(); }
        }

        public IRelayCommand ComputeCommand { get; }
        public IRelayCommand CalculateUnityFirstCostCommand { get; }
        public IAsyncRelayCommand ExportCommand { get; }
        public IRelayCommand ResetIdcCommand { get; }
        public IRelayCommand ResetFutureCostsCommand { get; }

        private readonly IExcelExportService _excelExportService;

        public AnnualizerViewModel(IExcelExportService excelExportService)
        {
            _excelExportService = excelExportService;

            ComputeCommand = new RelayCommand(Compute);
            CalculateUnityFirstCostCommand = new RelayCommand(CalculateUnityFirstCost);
            ExportCommand = new AsyncRelayCommand(ExportAsync);
            ResetIdcCommand = new RelayCommand(ResetIdcEntries);
            ResetFutureCostsCommand = new RelayCommand(ResetFutureCostEntries);

            FutureCosts.CollectionChanged += EntriesChanged;
            IdcEntries.CollectionChanged += EntriesChanged;
        }

        private void EntriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (FutureCostEntry entry in e.OldItems)
                    entry.PropertyChanged -= EntryOnPropertyChanged;
            }
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

        private static double GetTimingOffset(string? timing)
        {
            if (string.IsNullOrWhiteSpace(timing))
                return 1.0;

            return timing.Trim().ToLowerInvariant() switch
            {
                "beginning" => 0.0,
                "midpoint" => 0.5,
                "middle" => 0.5,
                _ => 1.0
            };
        }

        private void UpdatePvFactors()
        {
            double r = Rate / 100.0;
            foreach (var entry in FutureCosts)
            {
                double offset = GetTimingOffset(entry.Timing);
                double yearOffset = entry.Year - BaseYear;
                entry.PvFactor = Math.Pow(1.0 + r, -(yearOffset + offset));
            }

            foreach (var entry in IdcEntries)
            {
                double offsetMonths = GetTimingOffset(entry.Timing);
                int monthIndex = entry.Year <= 0 ? 0 : entry.Year - 1;
                double eventMonth = monthIndex + offsetMonths;
                entry.PvFactor = Math.Pow(1.0 + r, -(eventMonth / 12.0));
            }
        }

        private void Compute()
        {
            try
            {
                var inputs = BuildComputationInputs();
                var result = RunAnnualizer(FirstCost, inputs);
                ApplyResult(result);
            }
            catch (Exception)
            {
                Idc = TotalInvestment = Crf = AnnualCost = Bcr = double.NaN;
                Results = new ObservableCollection<string> { "Error computing results" };
            }
        }

        private AnnualizerComputationInputs BuildComputationInputs()
        {
            var future = FutureCosts
                .Select(f => (f.Cost, (double)(f.Year - BaseYear), GetTimingOffset(f.Timing)))
                .ToList();

            double[]? costArr = null;
            string[]? timingArr = null;
            int[]? monthArr = null;

            if (IdcEntries.Count > 0)
            {
                var schedule = IdcEntries
                    .Select(e => new { e.Cost, Timing = string.IsNullOrWhiteSpace(e.Timing) ? "midpoint" : e.Timing, e.Year })
                    .OrderBy(e => e.Year)
                    .ToList();

                if (schedule.Count > 0)
                {
                    costArr = new double[schedule.Count];
                    timingArr = new string[schedule.Count];
                    monthArr = new int[schedule.Count];

                    for (int i = 0; i < schedule.Count; i++)
                    {
                        costArr[i] = schedule[i].Cost;
                        timingArr[i] = schedule[i].Timing;
                        int monthValue = schedule[i].Year;
                        monthArr[i] = monthValue <= 0 ? 0 : monthValue - 1;
                    }
                }
            }

            return new AnnualizerComputationInputs(future, costArr, timingArr, monthArr);
        }

        private AnnualizerModel.Result RunAnnualizer(double firstCost, AnnualizerComputationInputs inputs)
        {
            return AnnualizerModel.Compute(firstCost, Rate / 100.0, AnnualOm, AnnualBenefits, inputs.FutureCosts,
                AnalysisPeriod, BaseYear, ConstructionMonths, inputs.IdcCosts, inputs.IdcTimings, inputs.IdcMonths,
                NormalizeTimingChoice(IdcTimingBasis), CalculateInterestAtPeriod,
                NormalizeFirstPaymentChoice(IdcFirstPaymentTiming), NormalizeLastPaymentChoice(IdcLastPaymentTiming));
        }

        private void ApplyResult(AnnualizerModel.Result result)
        {
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

        private void AppendResultMessage(string message)
        {
            UnityFirstCostMessage = message;

            var updated = Results != null ? new ObservableCollection<string>(Results) : new ObservableCollection<string>();
            updated.Add(message);
            Results = updated;
        }

        private void CalculateUnityFirstCost()
        {
            try
            {
                var inputs = BuildComputationInputs();
                var currentResult = RunAnnualizer(FirstCost, inputs);
                ApplyResult(currentResult);

                if (AnnualBenefits <= 0)
                {
                    AppendResultMessage("Unity BCR First Cost: Requires positive annual benefits.");
                    return;
                }

                double targetAnnual = AnnualBenefits;
                double lowerCost = 0.0;
                double lowerAnnual = RunAnnualizer(lowerCost, inputs).AnnualCost;

                if (double.IsNaN(lowerAnnual) || lowerAnnual > targetAnnual)
                {
                    AppendResultMessage("Unity BCR First Cost: Not attainable with current benefits.");
                    return;
                }

                double upperCost = Math.Max(Math.Max(FirstCost, 1.0), lowerCost + 1.0);
                double upperAnnual = RunAnnualizer(upperCost, inputs).AnnualCost;
                int expandAttempts = 0;
                while (!double.IsNaN(upperAnnual) && upperAnnual <= targetAnnual && expandAttempts < 60)
                {
                    upperCost = upperCost <= 0 ? 1.0 : upperCost * 2.0;
                    upperAnnual = RunAnnualizer(upperCost, inputs).AnnualCost;
                    expandAttempts++;
                    if (upperCost > 1_000_000_000_000d)
                        break;
                }

                if (double.IsNaN(upperAnnual) || upperAnnual <= targetAnnual)
                {
                    AppendResultMessage("Unity BCR First Cost: Unable to locate a solution with the current benefits.");
                    return;
                }

                double solvedCost = upperCost;
                for (int i = 0; i < 80; i++)
                {
                    double midCost = (lowerCost + upperCost) / 2.0;
                    double midAnnual = RunAnnualizer(midCost, inputs).AnnualCost;

                    if (double.IsNaN(midAnnual))
                    {
                        upperCost = midCost;
                        continue;
                    }

                    if (Math.Abs(midAnnual - targetAnnual) < 0.01 || Math.Abs(upperCost - lowerCost) < 0.01)
                    {
                        solvedCost = midCost;
                        break;
                    }

                    if (midAnnual > targetAnnual)
                    {
                        upperCost = midCost;
                        upperAnnual = midAnnual;
                    }
                    else
                    {
                        lowerCost = midCost;
                        lowerAnnual = midAnnual;
                    }

                    solvedCost = midCost;
                }

                double finalCost = Math.Max(0.0, (lowerCost + upperCost) / 2.0);
                if (!double.IsNaN(solvedCost))
                    finalCost = Math.Max(0.0, solvedCost);

                AppendResultMessage($"Unity BCR First Cost: {finalCost:F2}");
            }
            catch (Exception)
            {
                AppendResultMessage("Unity BCR First Cost: Error calculating.");
            }
        }

        private async Task ExportAsync()
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Excel Workbook (*.xlsx)|*.xlsx",
                FileName = "annualizer.xlsx"
            };

            if (dlg.ShowDialog() == true)
            {
                await Task.Run(() =>
                    _excelExportService.ExportAnnualizer(
                        FirstCost,
                        Rate,
                        AnnualOm,
                        AnnualBenefits,
                        FutureCosts,
                        Idc,
                        TotalInvestment,
                        Crf,
                        AnnualCost,
                        Bcr,
                        dlg.FileName));
            }
        }

        private void ResetIdcEntries()
        {
            if (IdcEntries.Count == 0)
                return;
            foreach (var entry in IdcEntries.ToList())
                entry.PropertyChanged -= EntryOnPropertyChanged;
            IdcEntries.Clear();
        }

        private void ResetFutureCostEntries()
        {
            if (FutureCosts.Count == 0)
                return;
            foreach (var entry in FutureCosts.ToList())
                entry.PropertyChanged -= EntryOnPropertyChanged;
            FutureCosts.Clear();
        }

        private static string NormalizeTimingChoice(string? choice)
        {
            if (string.IsNullOrWhiteSpace(choice))
                return "midpoint";

            return choice.Trim().ToLowerInvariant() switch
            {
                "beginning" => "beginning",
                "middle" => "midpoint",
                "midpoint" => "midpoint",
                "end" => "end",
                _ => "midpoint"
            };
        }

        private static string NormalizeFirstPaymentChoice(string? choice)
        {
            if (string.IsNullOrWhiteSpace(choice))
                return "beginning";

            return choice.Trim().ToLowerInvariant() switch
            {
                "beginning" => "beginning",
                "end" => "end",
                _ => "beginning"
            };
        }

        private static string NormalizeLastPaymentChoice(string? choice)
        {
            if (string.IsNullOrWhiteSpace(choice))
                return "midpoint";

            return choice.Trim().ToLowerInvariant() switch
            {
                "beginning" => "beginning",
                "middle" => "midpoint",
                "midpoint" => "midpoint",
                "end" => "end",
                _ => "midpoint"
            };
        }
    }
}

