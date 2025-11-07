using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
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
        private ObservableCollection<ScrbEntry> _scrbEntries = new();
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
        private double _scrbCostSensitivity = 100.0;
        private double _scrbBenefitSensitivity = 100.0;
        private double _scrbBaseTotalCost;
        private double _scrbBaseTotalBenefit;
        private double? _scrbBaseOverallRatio;
        private double _scrbAdjustedTotalCost;
        private double _scrbAdjustedTotalBenefit;
        private double? _scrbAdjustedOverallRatio;
        private bool _scrbHasBelowUnity;
        private bool _scrbHasDataIssues;
        private readonly ObservableCollection<string> _scrbComplianceFindings = new();

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

        public ObservableCollection<ScrbEntry> ScrbEntries
        {
            get => _scrbEntries;
            set
            {
                if (_scrbEntries == value)
                    return;

                if (_scrbEntries != null)
                {
                    _scrbEntries.CollectionChanged -= ScrbEntriesChanged;
                    foreach (var entry in _scrbEntries)
                        entry.PropertyChanged -= ScrbEntryOnPropertyChanged;
                }

                _scrbEntries = value;
                OnPropertyChanged();

                if (_scrbEntries != null)
                {
                    _scrbEntries.CollectionChanged += ScrbEntriesChanged;
                    foreach (var entry in _scrbEntries)
                        entry.PropertyChanged += ScrbEntryOnPropertyChanged;
                }

                RecalculateScrb();
            }
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

        public double ScrbCostSensitivity
        {
            get => _scrbCostSensitivity;
            set
            {
                double sanitized = SanitizeSensitivity(value);
                if (Math.Abs(_scrbCostSensitivity - sanitized) < 0.000001)
                    return;
                _scrbCostSensitivity = sanitized;
                OnPropertyChanged();
                RecalculateScrb();
            }
        }

        public double ScrbBenefitSensitivity
        {
            get => _scrbBenefitSensitivity;
            set
            {
                double sanitized = SanitizeSensitivity(value);
                if (Math.Abs(_scrbBenefitSensitivity - sanitized) < 0.000001)
                    return;
                _scrbBenefitSensitivity = sanitized;
                OnPropertyChanged();
                RecalculateScrb();
            }
        }

        public double ScrbBaseTotalCost
        {
            get => _scrbBaseTotalCost;
            private set
            {
                if (Math.Abs(_scrbBaseTotalCost - value) < 0.000001)
                    return;
                _scrbBaseTotalCost = value;
                OnPropertyChanged();
            }
        }

        public double ScrbBaseTotalBenefit
        {
            get => _scrbBaseTotalBenefit;
            private set
            {
                if (Math.Abs(_scrbBaseTotalBenefit - value) < 0.000001)
                    return;
                _scrbBaseTotalBenefit = value;
                OnPropertyChanged();
            }
        }

        public double? ScrbBaseOverallRatio
        {
            get => _scrbBaseOverallRatio;
            private set
            {
                if (_scrbBaseOverallRatio.HasValue == value.HasValue &&
                    (!_scrbBaseOverallRatio.HasValue || Math.Abs(_scrbBaseOverallRatio.Value - value!.Value) < 0.000001))
                    return;
                _scrbBaseOverallRatio = value;
                OnPropertyChanged();
            }
        }

        public double ScrbAdjustedTotalCost
        {
            get => _scrbAdjustedTotalCost;
            private set
            {
                if (Math.Abs(_scrbAdjustedTotalCost - value) < 0.000001)
                    return;
                _scrbAdjustedTotalCost = value;
                OnPropertyChanged();
            }
        }

        public double ScrbAdjustedTotalBenefit
        {
            get => _scrbAdjustedTotalBenefit;
            private set
            {
                if (Math.Abs(_scrbAdjustedTotalBenefit - value) < 0.000001)
                    return;
                _scrbAdjustedTotalBenefit = value;
                OnPropertyChanged();
            }
        }

        public double? ScrbAdjustedOverallRatio
        {
            get => _scrbAdjustedOverallRatio;
            private set
            {
                if (_scrbAdjustedOverallRatio.HasValue == value.HasValue &&
                    (!_scrbAdjustedOverallRatio.HasValue || Math.Abs(_scrbAdjustedOverallRatio.Value - value!.Value) < 0.000001))
                    return;
                _scrbAdjustedOverallRatio = value;
                OnPropertyChanged();
            }
        }

        public bool ScrbHasBelowUnity
        {
            get => _scrbHasBelowUnity;
            private set
            {
                if (_scrbHasBelowUnity == value)
                    return;
                _scrbHasBelowUnity = value;
                OnPropertyChanged();
            }
        }

        public bool ScrbHasDataIssues
        {
            get => _scrbHasDataIssues;
            private set
            {
                if (_scrbHasDataIssues == value)
                    return;
                _scrbHasDataIssues = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<string> ScrbComplianceFindings => _scrbComplianceFindings;

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
            ScrbEntries.CollectionChanged += ScrbEntriesChanged;

            RecalculateScrb();
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

        private void ScrbEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (ScrbEntry entry in e.OldItems)
                    entry.PropertyChanged -= ScrbEntryOnPropertyChanged;
            }

            if (e.NewItems != null)
            {
                foreach (ScrbEntry entry in e.NewItems)
                    entry.PropertyChanged += ScrbEntryOnPropertyChanged;
            }

            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                foreach (var entry in ScrbEntries)
                    entry.PropertyChanged += ScrbEntryOnPropertyChanged;
            }

            RecalculateScrb();
        }

        private void ScrbEntryOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ScrbEntry.SeparableCost) ||
                e.PropertyName == nameof(ScrbEntry.RemainingBenefit) ||
                e.PropertyName == nameof(ScrbEntry.FeatureName))
            {
                RecalculateScrb();
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

        private void RecalculateScrb()
        {
            double costFactor = ScrbCostSensitivity / 100.0;
            double benefitFactor = ScrbBenefitSensitivity / 100.0;

            double baseCost = 0.0;
            double baseBenefit = 0.0;
            double adjustedCostTotal = 0.0;
            double adjustedBenefitTotal = 0.0;
            bool hasBelowUnity = false;
            bool hasDataIssues = false;

            foreach (var entry in ScrbEntries)
            {
                baseCost += entry.SeparableCost;
                baseBenefit += entry.RemainingBenefit;

                double adjustedCost = entry.SeparableCost * costFactor;
                double adjustedBenefit = entry.RemainingBenefit * benefitFactor;

                entry.AdjustedCost = adjustedCost;
                entry.AdjustedBenefit = adjustedBenefit;
                entry.ScrbRatio = adjustedCost > 0.0 ? adjustedBenefit / adjustedCost : null;

                bool missingName = string.IsNullOrWhiteSpace(entry.FeatureName);
                bool invalidCost = entry.SeparableCost <= 0.0;
                bool invalidBenefit = entry.RemainingBenefit < 0.0;

                entry.HasDataIssue = missingName || invalidCost || invalidBenefit;
                entry.IsBelowUnity = entry.ScrbRatio.HasValue && entry.ScrbRatio.Value < 1.0;

                var complianceNotes = new List<string>();
                if (missingName)
                    complianceNotes.Add("Identify the separable element per ER 1105-2-100, Chapter 2.");
                if (invalidCost)
                    complianceNotes.Add("Report separable costs greater than zero per ER 1105-2-100, Appendix E.");
                if (invalidBenefit)
                    complianceNotes.Add("Confirm remaining benefits are non-negative and supported by documentation.");
                if (entry.IsBelowUnity)
                    complianceNotes.Add("SCRB ratio below 1.0; coordinate management decision per SMART Planning policy.");

                entry.ComplianceNote = complianceNotes.Count > 0 ? string.Join(' ', complianceNotes) : null;

                hasDataIssues |= entry.HasDataIssue;
                hasBelowUnity |= entry.IsBelowUnity;

                adjustedCostTotal += adjustedCost;
                adjustedBenefitTotal += adjustedBenefit;
            }

            ScrbBaseTotalCost = baseCost;
            ScrbBaseTotalBenefit = baseBenefit;
            ScrbBaseOverallRatio = baseCost > 0.0 ? baseBenefit / baseCost : null;

            ScrbAdjustedTotalCost = adjustedCostTotal;
            ScrbAdjustedTotalBenefit = adjustedBenefitTotal;
            ScrbAdjustedOverallRatio = adjustedCostTotal > 0.0 ? adjustedBenefitTotal / adjustedCostTotal : null;

            ScrbHasDataIssues = hasDataIssues;
            ScrbHasBelowUnity = hasBelowUnity;

            UpdateScrbComplianceFindings();
        }

        private static double SanitizeSensitivity(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                return 0.0;

            if (value < 0.0)
                return 0.0;

            if (value > 200.0)
                return 200.0;

            return value;
        }

        private void UpdateScrbComplianceFindings()
        {
            var findings = new List<string>();

            if (ScrbEntries.Count == 0)
            {
                findings.Add("No SCRB features are defined. Provide the current separable elements list in the decision file per ER 1105-2-100, Chapter 2.");
            }
            else
            {
                int unnamedCount = ScrbEntries.Count(e => string.IsNullOrWhiteSpace(e.FeatureName));
                if (unnamedCount > 0)
                    findings.Add($"{unnamedCount} separable element(s) are missing names. Identify each feature to align with the approved PMP and economic appendix.");

                var invalidCosts = ScrbEntries
                    .Where(e => e.SeparableCost <= 0.0)
                    .Select(e => FormatFeatureName(e.FeatureName))
                    .ToList();
                if (invalidCosts.Count > 0)
                    findings.Add($"Costs for {string.Join(", ", invalidCosts)} are not greater than zero. Reconcile the estimate with current price level guidance.");

                var negativeBenefits = ScrbEntries
                    .Where(e => e.RemainingBenefit < 0.0)
                    .Select(e => FormatFeatureName(e.FeatureName))
                    .ToList();
                if (negativeBenefits.Count > 0)
                    findings.Add($"Benefits for {string.Join(", ", negativeBenefits)} are negative. Document risk adjustments or correct the inputs per ER 1105-2-100, Appendix E.");

                var belowUnity = ScrbEntries
                    .Where(e => e.IsBelowUnity)
                    .Select(e => FormatFeatureName(e.FeatureName))
                    .ToList();
                if (belowUnity.Count > 0)
                    findings.Add($"SCRB ratios below 1.0 for {string.Join(", ", belowUnity)}. Coordinate mitigation or scope decisions with the PDT and MSC reviewers per SMART Planning guidance.");
            }

            if (ScrbBaseOverallRatio.HasValue && ScrbAdjustedOverallRatio.HasValue)
            {
                double delta = ScrbAdjustedOverallRatio.Value - ScrbBaseOverallRatio.Value;
                if (Math.Abs(delta) >= 0.01)
                {
                    string direction = delta > 0 ? "increased" : "decreased";
                    findings.Add($"Overall SCRB ratio {direction} by {Math.Abs(delta):0.00} after applying sensitivities. Explain the drivers in the annual economic update memorandum.");
                }
            }

            _scrbComplianceFindings.Clear();

            if (findings.Count == 0)
            {
                _scrbComplianceFindings.Add("All SCRB inputs pass basic checks. Document reviewer concurrence and attach supporting schedules per ER 1105-2-100, Appendix E.");
            }
            else
            {
                foreach (var finding in findings)
                    _scrbComplianceFindings.Add(finding);
            }
        }

        private static string FormatFeatureName(string? featureName)
        {
            if (string.IsNullOrWhiteSpace(featureName))
                return "(unnamed element)";

            return featureName.Trim();
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

            var culture = CultureInfo.CurrentCulture;

            Results = new ObservableCollection<string>
            {
                $"IDC: {Idc.ToString("C2", culture)}",
                $"Total Investment: {TotalInvestment.ToString("C2", culture)}",
                $"CRF: {Crf:F4}",
                $"Annual Cost: {AnnualCost.ToString("C2", culture)}",
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

                AppendResultMessage($"Unity BCR First Cost: {finalCost.ToString("C2", CultureInfo.CurrentCulture)}");
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

