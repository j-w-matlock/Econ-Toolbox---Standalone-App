using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using EconToolbox.Desktop.Models;
using EconToolbox.Desktop.Services;
using System.Windows;

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
        private double _futureCostPv;
        private double _totalInvestment;
        private double _crf;
        private double _annualCost;
        private double _bcr;
        private readonly ObservableCollection<AnnualizerScenario> _scenarioComparisons = new();
        private int _scenarioCounter = 1;
        private AnnualizerScenario? _selectedScenario;
        private bool _suppressScenarioSync;

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

        public double FutureCostPv
        {
            get => _futureCostPv;
            set { _futureCostPv = value; OnPropertyChanged(); }
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

        public ObservableCollection<AnnualizerScenario> ScenarioComparisons => _scenarioComparisons;

        public AnnualizerScenario? SelectedScenario
        {
            get => _selectedScenario;
            set
            {
                if (_selectedScenario == value)
                    return;

                if (_selectedScenario != null)
                    _selectedScenario.PropertyChanged -= SelectedScenarioOnPropertyChanged;

                _selectedScenario = value;
                if (_selectedScenario != null)
                    _selectedScenario.PropertyChanged += SelectedScenarioOnPropertyChanged;

                SyncScenarioToInputs(_selectedScenario);
                OnPropertyChanged();
            }
        }

        public string? UnityFirstCostMessage
        {
            get => _unityFirstCostMessage;
            set { _unityFirstCostMessage = value; OnPropertyChanged(); }
        }

        public IRelayCommand ComputeCommand { get; }
        public IAsyncRelayCommand ExportCommand { get; }
        public IRelayCommand ResetIdcCommand { get; }
        public IRelayCommand ResetFutureCostsCommand { get; }
        public IRelayCommand AddScenarioComparisonCommand { get; }
        public IRelayCommand ResetScenarioComparisonsCommand { get; }

        private readonly IExcelExportService _excelExportService;

        public AnnualizerViewModel(IExcelExportService excelExportService)
        {
            _excelExportService = excelExportService;

            ComputeCommand = new RelayCommand(Compute);
            ExportCommand = new AsyncRelayCommand(ExportAsync);
            ResetIdcCommand = new RelayCommand(ResetIdcEntries);
            ResetFutureCostsCommand = new RelayCommand(ResetFutureCostEntries);
            AddScenarioComparisonCommand = new RelayCommand(AddScenarioComparison);
            ResetScenarioComparisonsCommand = new RelayCommand(ResetScenarioComparisons);

            AttachFutureCostHandlers(_futureCosts);
            AttachFutureCostHandlers(_idcEntries);

            TryInitializeExampleAnnualizerData();
            TryAddScenarioComparison();
        }

        private void TryInitializeExampleAnnualizerData()
        {
            try
            {
                InitializeExampleAnnualizerData();
            }
            catch (Exception ex)
            {
                HandleComputationException(ex, "Error seeding default Annualizer inputs");
            }
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
                Month = 1,
                Timing = "beginning"
            });

            IdcEntries.Add(new FutureCostEntry
            {
                Cost = 30_000_000d,
                Month = 12,
                Timing = "end"
            });

            UpdatePvFactors();
            Compute();
        }

        private void TryAddScenarioComparison()
        {
            try
            {
                AddScenarioComparison();
            }
            catch (Exception ex)
            {
                HandleComputationException(ex, "Error initializing default scenario comparison");
            }
        }

        private void SelectedScenarioOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_suppressScenarioSync)
                return;

            if (sender is not AnnualizerScenario scenario)
                return;

            if (!ReferenceEquals(scenario, SelectedScenario))
                return;

            SyncScenarioToInputs(scenario);
        }

        private void SyncScenarioToInputs(AnnualizerScenario? scenario)
        {
            if (scenario == null)
                return;

            FirstCost = scenario.FirstCost;
            AnnualOm = scenario.AnnualOm;
            AnnualBenefits = scenario.AnnualBenefits;
            Rate = scenario.Rate;
            UnityFirstCostMessage = null;
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

        private void EntriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (var item in e.OldItems)
                {
                    if (item is FutureCostEntry entry)
                        entry.PropertyChanged -= EntryOnPropertyChanged;
                }
            }

            if (e.NewItems != null)
            {
                foreach (var item in e.NewItems)
                {
                    if (item is FutureCostEntry entry)
                        entry.PropertyChanged += EntryOnPropertyChanged;
                }
            }
            UpdatePvFactors();
            Compute();
        }

        private void EntryOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(FutureCostEntry.Year) ||
                e.PropertyName == nameof(FutureCostEntry.Month) ||
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
                int monthIndex = entry.Month <= 0 ? 0 : entry.Month - 1;
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
                UpdateScenarioComparisons(inputs, result);
            }
            catch (Exception ex)
            {
                Idc = TotalInvestment = Crf = AnnualCost = Bcr = double.NaN;
                Results = new ObservableCollection<string> { $"Error computing results: {ex.Message}" };
                HandleComputationException(ex, "Annualizer computation failed");
            }
        }

        private static void HandleComputationException(Exception ex, string context)
        {
            var details = $"{context}: {ex}";
            Debug.WriteLine(details);
            Console.Error.WriteLine(details);
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
                    .Select(e => new { e.Cost, Timing = string.IsNullOrWhiteSpace(e.Timing) ? "midpoint" : e.Timing, e.Month })
                    .OrderBy(e => e.Month)
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
                        int monthValue = schedule[i].Month;
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
            FutureCostPv = result.FutureCostPv;
            TotalInvestment = result.TotalInvestment;
            Crf = result.Crf;
            AnnualCost = result.AnnualCost;
            Bcr = result.Bcr;

            var culture = CultureInfo.CurrentCulture;

            Results = new ObservableCollection<string>
            {
                $"IDC: {Idc.ToString("C2", culture)}",
                $"PV of Future Costs: {FutureCostPv.ToString("C2", culture)}",
                $"Total Investment: {TotalInvestment.ToString("C2", culture)}",
                $"CRF: {Crf:F4}",
                $"Annual Cost: {AnnualCost.ToString("C2", culture)}",
                $"BCR: {Bcr:F2}"
            };
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
                try
                {
                    await Task.Run(() =>
                        _excelExportService.ExportAnnualizer(
                            FirstCost,
                            Rate,
                            AnnualOm,
                            AnnualBenefits,
                            FutureCosts,
                            FutureCostPv,
                            Idc,
                            TotalInvestment,
                            Crf,
                            AnnualCost,
                            Bcr,
                            dlg.FileName));
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex);
                    MessageBox.Show($"Export failed: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
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

        private void ResetScenarioComparisons()
        {
            _suppressScenarioSync = true;
            try
            {
                ScenarioComparisons.Clear();
                _scenarioCounter = 1;
                SelectedScenario = null;
            }
            finally
            {
                _suppressScenarioSync = false;
            }

            AddScenarioComparison();
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
            SelectedScenario = scenario;
        }

        private void UpdateScenarioComparisons(AnnualizerComputationInputs inputs, AnnualizerModel.Result selectedResult)
        {
            if (ScenarioComparisons.Count == 0)
                return;

            _suppressScenarioSync = true;
            try
            {
                foreach (var scenario in ScenarioComparisons)
                {
                    var result = ReferenceEquals(scenario, SelectedScenario)
                        ? selectedResult
                        : RunAnnualizer(scenario.FirstCost, scenario.Rate, scenario.AnnualOm, scenario.AnnualBenefits, inputs);

                    scenario.Idc = result.Idc;
                    scenario.FutureCostPv = result.FutureCostPv;
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

                    if (ReferenceEquals(scenario, SelectedScenario))
                        UnityFirstCostMessage = unityCost.HasValue ? null : unityMessage;
                }
            }
            finally
            {
                _suppressScenarioSync = false;
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

