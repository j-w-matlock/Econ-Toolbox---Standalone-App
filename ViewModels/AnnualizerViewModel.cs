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
        private ObservableCollection<ScrbCostEntry> _scrbCostEntries = new();
        private ObservableCollection<ScrbBenefitEntry> _scrbBenefitEntries = new();
        private readonly ObservableCollection<ScrbEntry> _scrbSummaries = new();
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
        private int _scrbEvaluationYear;
        private double _scrbDiscountRate = 2.5;
        private double _scrbBaseTotalCost;
        private double _scrbBaseTotalBenefit;
        private double? _scrbBaseOverallRatio;
        private double _scrbAdjustedTotalCost;
        private double _scrbAdjustedTotalBenefit;
        private double? _scrbAdjustedOverallRatio;
        private bool _scrbHasBelowUnity;
        private bool _scrbHasDataIssues;
        private readonly ObservableCollection<string> _scrbComplianceFindings = new();
        private readonly ObservableCollection<AnnualizerScenario> _scenarioComparisons = new();
        private int _scenarioCounter = 1;

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
            set
            {
                RewireFutureCostEntries(_futureCosts, value);
                _futureCosts = value ?? new ObservableCollection<FutureCostEntry>();
                AttachFutureCostHandlers(_futureCosts);
                OnPropertyChanged();
                UpdatePvFactors();
                Compute();
            }
        }

        public ObservableCollection<FutureCostEntry> IdcEntries
        {
            get => _idcEntries;
            set
            {
                RewireFutureCostEntries(_idcEntries, value);
                _idcEntries = value ?? new ObservableCollection<FutureCostEntry>();
                AttachFutureCostHandlers(_idcEntries);
                OnPropertyChanged();
                UpdatePvFactors();
                Compute();
            }
        }

        public ObservableCollection<ScrbCostEntry> ScrbCostEntries
        {
            get => _scrbCostEntries;
            set
            {
                RewireScrbCostEntries(_scrbCostEntries);
                _scrbCostEntries = value ?? new ObservableCollection<ScrbCostEntry>();
                AttachScrbCostHandlers(_scrbCostEntries);
                OnPropertyChanged();
                RecalculateScrb();
            }
        }

        public ObservableCollection<ScrbBenefitEntry> ScrbBenefitEntries
        {
            get => _scrbBenefitEntries;
            set
            {
                RewireScrbBenefitEntries(_scrbBenefitEntries);
                _scrbBenefitEntries = value ?? new ObservableCollection<ScrbBenefitEntry>();
                AttachScrbBenefitHandlers(_scrbBenefitEntries);
                OnPropertyChanged();
                RecalculateScrb();
            }
        }

        public ObservableCollection<ScrbEntry> ScrbSummaries => _scrbSummaries;

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

        public int ScrbEvaluationYear
        {
            get => _scrbEvaluationYear;
            set
            {
                if (_scrbEvaluationYear == value)
                    return;
                _scrbEvaluationYear = value;
                OnPropertyChanged();
                RecalculateScrb();
            }
        }

        public double ScrbDiscountRate
        {
            get => _scrbDiscountRate;
            set
            {
                if (Math.Abs(_scrbDiscountRate - value) < 0.000001)
                    return;
                _scrbDiscountRate = value;
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

        public ObservableCollection<AnnualizerScenario> ScenarioComparisons => _scenarioComparisons;

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
        public IRelayCommand AddScenarioComparisonCommand { get; }
        public IRelayCommand EvaluateScenarioComparisonsCommand { get; }

        private readonly IExcelExportService _excelExportService;

        public AnnualizerViewModel(IExcelExportService excelExportService)
        {
            _excelExportService = excelExportService;

            ComputeCommand = new RelayCommand(Compute);
            CalculateUnityFirstCostCommand = new RelayCommand(CalculateUnityFirstCost);
            ExportCommand = new AsyncRelayCommand(ExportAsync);
            ResetIdcCommand = new RelayCommand(ResetIdcEntries);
            ResetFutureCostsCommand = new RelayCommand(ResetFutureCostEntries);
            AddScenarioComparisonCommand = new RelayCommand(AddScenarioComparison);
            EvaluateScenarioComparisonsCommand = new RelayCommand(EvaluateScenarioComparisons);

            AttachFutureCostHandlers(_futureCosts);
            AttachFutureCostHandlers(_idcEntries);
            AttachScrbCostHandlers(_scrbCostEntries);
            AttachScrbBenefitHandlers(_scrbBenefitEntries);

            ScrbEvaluationYear = BaseYear;
            ScrbDiscountRate = Rate;

            RecalculateScrb();

            InitializeExampleAnnualizerData();
            AddScenarioComparison();
        }

        private void InitializeExampleAnnualizerData()
        {
            if (FirstCost != 0 || AnnualOm != 0 || AnnualBenefits != 0 || FutureCosts.Count > 0 || IdcEntries.Count > 0)
                return;

            FirstCost = 45_000_000d;
            Rate = 3.5d;
            AnalysisPeriod = 50;
            AnnualOm = 250_000d;
            AnnualBenefits = 2_500_000d;

            FutureCosts.Add(new FutureCostEntry
            {
                Cost = 1_100_000d,
                Year = BaseYear + 25,
                Timing = "midpoint"
            });

            IdcEntries.Add(new FutureCostEntry
            {
                Cost = 15_000_000d,
                Year = 1,
                Timing = "beginning"
            });

            IdcEntries.Add(new FutureCostEntry
            {
                Cost = 30_000_000d,
                Year = 12,
                Timing = "end"
            });

            UpdatePvFactors();
            Compute();
        }

        private void RewireFutureCostEntries(ObservableCollection<FutureCostEntry>? oldCollection,
            ObservableCollection<FutureCostEntry>? newCollection)
        {
            if (oldCollection != null)
            {
                oldCollection.CollectionChanged -= EntriesChanged;
                foreach (var entry in oldCollection)
                {
                    if (entry != null)
                        entry.PropertyChanged -= EntryOnPropertyChanged;
                }
            }

            if (newCollection != null)
            {
                newCollection.CollectionChanged -= EntriesChanged;
                foreach (var entry in newCollection)
                {
                    if (entry != null)
                        entry.PropertyChanged -= EntryOnPropertyChanged;
                }
            }
        }

        private void AttachFutureCostHandlers(ObservableCollection<FutureCostEntry> collection)
        {
            collection.CollectionChanged += EntriesChanged;
            foreach (var entry in collection)
            {
                if (entry != null)
                    entry.PropertyChanged += EntryOnPropertyChanged;
            }
        }

        private void RewireScrbCostEntries(ObservableCollection<ScrbCostEntry>? collection)
        {
            if (collection == null)
                return;

            collection.CollectionChanged -= ScrbCostEntriesChanged;
            foreach (var entry in collection)
                entry.PropertyChanged -= ScrbCostEntryOnPropertyChanged;
        }

        private void AttachScrbCostHandlers(ObservableCollection<ScrbCostEntry> collection)
        {
            collection.CollectionChanged += ScrbCostEntriesChanged;
            foreach (var entry in collection)
                entry.PropertyChanged += ScrbCostEntryOnPropertyChanged;
        }

        private void RewireScrbBenefitEntries(ObservableCollection<ScrbBenefitEntry>? collection)
        {
            if (collection == null)
                return;

            collection.CollectionChanged -= ScrbBenefitEntriesChanged;
            foreach (var entry in collection)
                entry.PropertyChanged -= ScrbBenefitEntryOnPropertyChanged;
        }

        private void AttachScrbBenefitHandlers(ObservableCollection<ScrbBenefitEntry> collection)
        {
            collection.CollectionChanged += ScrbBenefitEntriesChanged;
            foreach (var entry in collection)
                entry.PropertyChanged += ScrbBenefitEntryOnPropertyChanged;
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

        private void ScrbCostEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (ScrbCostEntry entry in e.OldItems)
                    entry.PropertyChanged -= ScrbCostEntryOnPropertyChanged;
            }

            if (e.NewItems != null)
            {
                foreach (ScrbCostEntry entry in e.NewItems)
                    entry.PropertyChanged += ScrbCostEntryOnPropertyChanged;
            }

            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                foreach (var entry in ScrbCostEntries)
                    entry.PropertyChanged += ScrbCostEntryOnPropertyChanged;
            }

            RecalculateScrb();
        }

        private void ScrbCostEntryOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ScrbCostEntry.OriginalCost) ||
                e.PropertyName == nameof(ScrbCostEntry.OriginalYear) ||
                e.PropertyName == nameof(ScrbCostEntry.UpdateFactor) ||
                e.PropertyName == nameof(ScrbCostEntry.FeatureName))
            {
                RecalculateScrb();
            }
        }

        private void ScrbBenefitEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (ScrbBenefitEntry entry in e.OldItems)
                    entry.PropertyChanged -= ScrbBenefitEntryOnPropertyChanged;
            }

            if (e.NewItems != null)
            {
                foreach (ScrbBenefitEntry entry in e.NewItems)
                    entry.PropertyChanged += ScrbBenefitEntryOnPropertyChanged;
            }

            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                foreach (var entry in ScrbBenefitEntries)
                    entry.PropertyChanged += ScrbBenefitEntryOnPropertyChanged;
            }

            RecalculateScrb();
        }

        private void ScrbBenefitEntryOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ScrbBenefitEntry.OriginalBenefit) ||
                e.PropertyName == nameof(ScrbBenefitEntry.OriginalYear) ||
                e.PropertyName == nameof(ScrbBenefitEntry.UpdateFactor) ||
                e.PropertyName == nameof(ScrbBenefitEntry.FeatureName))
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
                if (entry == null)
                    continue;
                double offset = GetTimingOffset(entry.Timing);
                double yearOffset = entry.Year - BaseYear;
                entry.PvFactor = Math.Pow(1.0 + r, -(yearOffset + offset));
            }

            foreach (var entry in IdcEntries)
            {
                if (entry == null)
                    continue;
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
            double discountRate = ScrbDiscountRate / 100.0;

            double baseCost = 0.0;
            double baseBenefit = 0.0;
            double adjustedCostTotal = 0.0;
            double adjustedBenefitTotal = 0.0;
            bool hasBelowUnity = false;
            bool hasDataIssues = false;

            var costGroups = new Dictionary<string, List<ScrbCostEntry>>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in ScrbCostEntries)
            {
                string featureName = FormatFeatureName(entry.FeatureName);
                bool entryIssue = string.IsNullOrWhiteSpace(entry.FeatureName);

                double originalCost = entry.OriginalCost;
                if (double.IsNaN(originalCost) || double.IsInfinity(originalCost))
                {
                    originalCost = 0.0;
                    entryIssue = true;
                }

                if (originalCost <= 0.0)
                    entryIssue = true;

                double updateFactor = entry.UpdateFactor;
                if (double.IsNaN(updateFactor) || double.IsInfinity(updateFactor) || updateFactor <= 0.0)
                {
                    updateFactor = 0.0;
                    entryIssue = true;
                }

                double adjustedCost = originalCost * updateFactor;
                if (entry.OriginalYear.HasValue)
                {
                    double yearsDifference = entry.OriginalYear.Value - ScrbEvaluationYear;
                    double discountFactor = Math.Pow(1.0 + discountRate, yearsDifference);
                    if (double.IsNaN(discountFactor) || double.IsInfinity(discountFactor) || Math.Abs(discountFactor) < 0.0000001)
                    {
                        discountFactor = 1.0;
                        entryIssue = true;
                    }
                    adjustedCost /= discountFactor;
                }
                else
                {
                    entryIssue = true;
                }

                adjustedCost *= costFactor;

                if (double.IsNaN(adjustedCost) || double.IsInfinity(adjustedCost))
                {
                    adjustedCost = 0.0;
                    entryIssue = true;
                }

                entry.AdjustedCost = adjustedCost;
                entry.HasDataIssue = entryIssue;

                baseCost += originalCost;
                adjustedCostTotal += adjustedCost;

                if (!costGroups.TryGetValue(featureName, out var list))
                {
                    list = new List<ScrbCostEntry>();
                    costGroups.Add(featureName, list);
                }
                list.Add(entry);

                hasDataIssues |= entryIssue;
            }

            var benefitGroups = new Dictionary<string, List<ScrbBenefitEntry>>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in ScrbBenefitEntries)
            {
                string featureName = FormatFeatureName(entry.FeatureName);
                bool entryIssue = string.IsNullOrWhiteSpace(entry.FeatureName);

                double originalBenefit = entry.OriginalBenefit;
                if (double.IsNaN(originalBenefit) || double.IsInfinity(originalBenefit))
                {
                    originalBenefit = 0.0;
                    entryIssue = true;
                }

                if (originalBenefit < 0.0)
                    entryIssue = true;

                double updateFactor = entry.UpdateFactor;
                if (double.IsNaN(updateFactor) || double.IsInfinity(updateFactor) || updateFactor <= 0.0)
                {
                    updateFactor = 0.0;
                    entryIssue = true;
                }

                double adjustedBenefit = originalBenefit * updateFactor;
                if (entry.OriginalYear.HasValue)
                {
                    double yearsDifference = entry.OriginalYear.Value - ScrbEvaluationYear;
                    double discountFactor = Math.Pow(1.0 + discountRate, yearsDifference);
                    if (double.IsNaN(discountFactor) || double.IsInfinity(discountFactor) || Math.Abs(discountFactor) < 0.0000001)
                    {
                        discountFactor = 1.0;
                        entryIssue = true;
                    }
                    adjustedBenefit /= discountFactor;
                }
                else
                {
                    entryIssue = true;
                }

                adjustedBenefit *= benefitFactor;

                if (double.IsNaN(adjustedBenefit) || double.IsInfinity(adjustedBenefit))
                {
                    adjustedBenefit = 0.0;
                    entryIssue = true;
                }

                entry.AdjustedBenefit = adjustedBenefit;
                entry.HasDataIssue = entryIssue;

                baseBenefit += originalBenefit;
                adjustedBenefitTotal += adjustedBenefit;

                if (!benefitGroups.TryGetValue(featureName, out var list))
                {
                    list = new List<ScrbBenefitEntry>();
                    benefitGroups.Add(featureName, list);
                }
                list.Add(entry);

                hasDataIssues |= entryIssue;
            }

            var featureOrder = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            int order = 0;
            foreach (var entry in ScrbCostEntries)
            {
                string featureName = FormatFeatureName(entry.FeatureName);
                if (!featureOrder.ContainsKey(featureName))
                    featureOrder[featureName] = order++;
            }
            foreach (var entry in ScrbBenefitEntries)
            {
                string featureName = FormatFeatureName(entry.FeatureName);
                if (!featureOrder.ContainsKey(featureName))
                    featureOrder[featureName] = order++;
            }

            var features = new HashSet<string>(costGroups.Keys, StringComparer.OrdinalIgnoreCase);
            features.UnionWith(benefitGroups.Keys);

            var summaries = features
                .OrderBy(f => featureOrder.TryGetValue(f, out var idx) ? idx : int.MaxValue)
                .ThenBy(f => f, StringComparer.OrdinalIgnoreCase)
                .Select(featureName =>
                {
                    costGroups.TryGetValue(featureName, out var costEntries);
                    benefitGroups.TryGetValue(featureName, out var benefitEntries);

                    double featureBaseCost = costEntries?.Sum(e => e.OriginalCost) ?? 0.0;
                    double featureBaseBenefit = benefitEntries?.Sum(e => e.OriginalBenefit) ?? 0.0;
                    double featureAdjustedCost = costEntries?.Sum(e => e.AdjustedCost) ?? 0.0;
                    double featureAdjustedBenefit = benefitEntries?.Sum(e => e.AdjustedBenefit) ?? 0.0;

                    var summary = new ScrbEntry
                    {
                        FeatureName = featureName,
                        SeparableCost = featureBaseCost,
                        RemainingBenefit = featureBaseBenefit,
                        AdjustedCost = featureAdjustedCost,
                        AdjustedBenefit = featureAdjustedBenefit
                    };

                    summary.ScrbRatio = featureAdjustedCost > 0.0 ? featureAdjustedBenefit / featureAdjustedCost : null;
                    summary.IsBelowUnity = summary.ScrbRatio.HasValue && summary.ScrbRatio.Value < 1.0;

                    bool invalidCost = featureBaseCost <= 0.0;
                    bool invalidBenefit = featureBaseBenefit < 0.0;
                    bool missingName = featureName == "(unnamed element)";
                    bool missingYear = (costEntries?.Any(e => !e.OriginalYear.HasValue) ?? false) ||
                                       (benefitEntries?.Any(e => !e.OriginalYear.HasValue) ?? false);
                    bool invalidFactor = (costEntries?.Any(e => e.UpdateFactor <= 0.0) ?? false) ||
                                         (benefitEntries?.Any(e => e.UpdateFactor <= 0.0) ?? false);

                    var complianceNotes = new List<string>();
                    if (missingName)
                        complianceNotes.Add("Identify the separable element per ER 1105-2-100, Chapter 2.");
                    if (invalidCost)
                        complianceNotes.Add("Report separable costs greater than zero per ER 1105-2-100, Appendix E.");
                    if (invalidBenefit)
                        complianceNotes.Add("Confirm remaining benefits are non-negative and supported by documentation.");
                    if (missingYear)
                        complianceNotes.Add("Specify original years so discounting aligns with the evaluation period.");
                    if (invalidFactor)
                        complianceNotes.Add("Use positive update factors when escalating SCRB inputs.");
                    if (summary.IsBelowUnity)
                        complianceNotes.Add("SCRB ratio below 1.0; coordinate management decision per SMART Planning policy.");

                    summary.HasDataIssue = complianceNotes.Count > 0;
                    summary.ComplianceNote = complianceNotes.Count > 0 ? string.Join(' ', complianceNotes) : null;

                    hasDataIssues |= summary.HasDataIssue;
                    hasBelowUnity |= summary.IsBelowUnity;

                    return summary;
                })
                .ToList();

            _scrbSummaries.Clear();
            foreach (var summary in summaries)
                _scrbSummaries.Add(summary);

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

            if (ScrbCostEntries.Count == 0 && ScrbBenefitEntries.Count == 0)
            {
                findings.Add("No SCRB features are defined. Provide the current separable elements list in the decision file per ER 1105-2-100, Chapter 2.");
            }
            else
            {
                var unnamedFeatures = ScrbCostEntries
                    .Where(e => string.IsNullOrWhiteSpace(e.FeatureName))
                    .Select(e => FormatFeatureName(e.FeatureName))
                    .Concat(ScrbBenefitEntries.Where(e => string.IsNullOrWhiteSpace(e.FeatureName)).Select(e => FormatFeatureName(e.FeatureName)))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (unnamedFeatures.Count > 0)
                    findings.Add($"{unnamedFeatures.Count} separable element input(s) are missing names. Identify each feature to align with the approved PMP and economic appendix.");

                var invalidCosts = ScrbCostEntries
                    .Where(e => e.OriginalCost <= 0.0)
                    .Select(e => FormatFeatureName(e.FeatureName))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (invalidCosts.Count > 0)
                    findings.Add($"Costs for {string.Join(", ", invalidCosts)} are not greater than zero. Reconcile the estimate with current price level guidance.");

                var negativeBenefits = ScrbBenefitEntries
                    .Where(e => e.OriginalBenefit < 0.0)
                    .Select(e => FormatFeatureName(e.FeatureName))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (negativeBenefits.Count > 0)
                    findings.Add($"Benefits for {string.Join(", ", negativeBenefits)} are negative. Document risk adjustments or correct the inputs per ER 1105-2-100, Appendix E.");

                var missingYears = ScrbCostEntries
                    .Where(e => !e.OriginalYear.HasValue)
                    .Select(e => FormatFeatureName(e.FeatureName))
                    .Concat(ScrbBenefitEntries.Where(e => !e.OriginalYear.HasValue).Select(e => FormatFeatureName(e.FeatureName)))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (missingYears.Count > 0)
                    findings.Add($"Provide the original year for {string.Join(", ", missingYears)} so discounting aligns with the evaluation period.");

                var invalidFactors = ScrbCostEntries
                    .Where(e => e.UpdateFactor <= 0.0)
                    .Select(e => FormatFeatureName(e.FeatureName))
                    .Concat(ScrbBenefitEntries.Where(e => e.UpdateFactor <= 0.0).Select(e => FormatFeatureName(e.FeatureName)))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (invalidFactors.Count > 0)
                    findings.Add($"Update factors for {string.Join(", ", invalidFactors)} must be positive to establish adjusted SCRB values.");

                var belowUnity = ScrbSummaries
                    .Where(e => e.IsBelowUnity)
                    .Select(e => FormatFeatureName(e.FeatureName))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
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
                .Where(f => f != null)
                .Select(f => (f.Cost, (double)(f.Year - BaseYear), GetTimingOffset(f.Timing)))
                .ToList();

            double[]? costArr = null;
            string[]? timingArr = null;
            int[]? monthArr = null;

            if (IdcEntries.Count > 0)
            {
                var schedule = IdcEntries
                    .Where(e => e != null)
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
            return RunAnnualizer(firstCost, Rate, AnnualOm, AnnualBenefits, inputs);
        }

        private AnnualizerModel.Result RunAnnualizer(double firstCost, double rate, double annualOm, double annualBenefits,
            AnnualizerComputationInputs inputs)
        {
            return AnnualizerModel.Compute(firstCost, rate / 100.0, annualOm, annualBenefits, inputs.FutureCosts,
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

                var message = TrySolveUnityFirstCost(FirstCost, Rate, AnnualOm, AnnualBenefits, inputs, out var unityCost);
                if (unityCost.HasValue)
                    AppendResultMessage($"Unity BCR First Cost: {unityCost.Value.ToString("C2", CultureInfo.CurrentCulture)}");
                else
                    AppendResultMessage($"Unity BCR First Cost: {message}");
            }
            catch (Exception)
            {
                AppendResultMessage("Unity BCR First Cost: Error calculating.");
            }
        }

        private string TrySolveUnityFirstCost(double startingFirstCost, double rate, double annualOm, double annualBenefits,
            AnnualizerComputationInputs inputs, out double? unityCost)
        {
            unityCost = null;

            if (annualBenefits <= 0)
                return "Requires positive annual benefits.";

            double targetAnnual = annualBenefits;
            double lowerCost = 0.0;
            double lowerAnnual = RunAnnualizer(lowerCost, rate, annualOm, annualBenefits, inputs).AnnualCost;

            if (double.IsNaN(lowerAnnual) || lowerAnnual > targetAnnual)
                return "Not attainable with current benefits.";

            double upperCost = Math.Max(Math.Max(startingFirstCost, 1.0), lowerCost + 1.0);
            double upperAnnual = RunAnnualizer(upperCost, rate, annualOm, annualBenefits, inputs).AnnualCost;
            int expandAttempts = 0;
            while (!double.IsNaN(upperAnnual) && upperAnnual <= targetAnnual && expandAttempts < 60)
            {
                upperCost = upperCost <= 0 ? 1.0 : upperCost * 2.0;
                upperAnnual = RunAnnualizer(upperCost, rate, annualOm, annualBenefits, inputs).AnnualCost;
                expandAttempts++;
                if (upperCost > 1_000_000_000_000d)
                    break;
            }

            if (double.IsNaN(upperAnnual) || upperAnnual <= targetAnnual)
                return "Unable to locate a solution with the current benefits.";

            double solvedCost = upperCost;
            for (int i = 0; i < 80; i++)
            {
                double midCost = (lowerCost + upperCost) / 2.0;
                double midAnnual = RunAnnualizer(midCost, rate, annualOm, annualBenefits, inputs).AnnualCost;

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

            unityCost = finalCost;
            return "Calculated";
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

        private void AddScenarioComparison()
        {
            var scenario = new AnnualizerScenario
            {
                Name = $"Scenario {_scenarioCounter}",
                FirstCost = FirstCost,
                AnnualOm = AnnualOm,
                AnnualBenefits = AnnualBenefits,
                Rate = Rate
            };

            _scenarioCounter++;
            ScenarioComparisons.Add(scenario);
        }

        private void EvaluateScenarioComparisons()
        {
            if (ScenarioComparisons.Count == 0)
                return;

            var inputs = BuildComputationInputs();

            foreach (var scenario in ScenarioComparisons)
            {
                var result = RunAnnualizer(scenario.FirstCost, scenario.Rate, scenario.AnnualOm, scenario.AnnualBenefits, inputs);

                scenario.Idc = result.Idc;
                scenario.TotalInvestment = result.TotalInvestment;
                scenario.Crf = result.Crf;
                scenario.AnnualCost = result.AnnualCost;
                scenario.Bcr = result.Bcr;
                scenario.Notes = null;
                scenario.UnityBcrFirstCost = null;

                var unityMessage = TrySolveUnityFirstCost(scenario.FirstCost, scenario.Rate, scenario.AnnualOm,
                    scenario.AnnualBenefits, inputs, out var unityCost);

                if (unityCost.HasValue)
                    scenario.UnityBcrFirstCost = unityCost;
                else
                    scenario.Notes = unityMessage;
            }
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

