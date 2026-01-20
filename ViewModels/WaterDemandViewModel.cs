using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Windows.Media;
using System.Windows;
using System.ComponentModel;
using System.Collections.Specialized;
using EconToolbox.Desktop.Models;
using EconToolbox.Desktop.Services;
using EconToolbox.Desktop.Themes;

namespace EconToolbox.Desktop.ViewModels
{
    /// <summary>
    /// View model allowing users to enter historical demand data and
    /// project future demand using either linear regression or growth
    /// rate methods. Results can be displayed in a simple chart.
    /// </summary>
    public class WaterDemandViewModel : DiagnosticViewModelBase, IComputeModule
    {
        private ObservableCollection<DemandEntry> _historicalData = new();
        private int _forecastYears = 5;
        private ObservableCollection<DemandEntry> _results = new();
        private string _explanation = string.Empty;
        public ObservableCollection<Scenario> Scenarios { get; } = new();
        private Scenario? _selectedScenario;
        private Scenario? _baselineScenario;
        private bool _isImportingProject;
        private readonly ObservableCollection<ChartSeries> _chartSeries = new();
        public ObservableCollection<ChartSeries> ChartSeries
        {
            get => _chartSeries;
        }

        public ObservableCollection<LegendItem> LegendItems { get; } = new();

        private string _chartStatusMessage = "Add scenarios and forecast to view the adjusted demand chart.";
        public string ChartStatusMessage
        {
            get => _chartStatusMessage;
            private set { _chartStatusMessage = value; OnPropertyChanged(); }
        }

        private string _chartTitle = "Adjusted Demand Forecast";
        public string ChartTitle
        {
            get => _chartTitle;
            set { _chartTitle = value; OnPropertyChanged(); }
        }

        private static readonly IReadOnlyDictionary<string, (double Current, double Future)> DefaultSectorPercents
            = new Dictionary<string, (double Current, double Future)>(StringComparer.OrdinalIgnoreCase)
            {
                ["Industrial"] = (10.0, 5.0),
                ["Residential"] = (80.0, 80.0),
                ["Commercial"] = (10.0, 15.0)
            };

        private double _alternative1PopulationAdjustment = 20.0;
        public double Alternative1PopulationAdjustment
        {
            get => _alternative1PopulationAdjustment;
            set
            {
                if (Math.Abs(_alternative1PopulationAdjustment - value) < 0.0001)
                    return;
                _alternative1PopulationAdjustment = value;
                OnPropertyChanged();
                ApplyScenarioAdjustments();
            }
        }

        private double _alternative1PerCapitaAdjustment = 20.0;
        public double Alternative1PerCapitaAdjustment
        {
            get => _alternative1PerCapitaAdjustment;
            set
            {
                if (Math.Abs(_alternative1PerCapitaAdjustment - value) < 0.0001)
                    return;
                _alternative1PerCapitaAdjustment = value;
                OnPropertyChanged();
                ApplyScenarioAdjustments();
            }
        }

        private double _alternative1ImprovementsAdjustment = -20.0;
        public double Alternative1ImprovementsAdjustment
        {
            get => _alternative1ImprovementsAdjustment;
            set
            {
                if (Math.Abs(_alternative1ImprovementsAdjustment - value) < 0.0001)
                    return;
                _alternative1ImprovementsAdjustment = value;
                OnPropertyChanged();
                ApplyScenarioAdjustments();
            }
        }

        private double _alternative1LossesAdjustment = 20.0;
        public double Alternative1LossesAdjustment
        {
            get => _alternative1LossesAdjustment;
            set
            {
                if (Math.Abs(_alternative1LossesAdjustment - value) < 0.0001)
                    return;
                _alternative1LossesAdjustment = value;
                OnPropertyChanged();
                ApplyScenarioAdjustments();
            }
        }

        private double _alternative2PopulationAdjustment = -20.0;
        public double Alternative2PopulationAdjustment
        {
            get => _alternative2PopulationAdjustment;
            set
            {
                if (Math.Abs(_alternative2PopulationAdjustment - value) < 0.0001)
                    return;
                _alternative2PopulationAdjustment = value;
                OnPropertyChanged();
                ApplyScenarioAdjustments();
            }
        }

        private double _alternative2PerCapitaAdjustment = -20.0;
        public double Alternative2PerCapitaAdjustment
        {
            get => _alternative2PerCapitaAdjustment;
            set
            {
                if (Math.Abs(_alternative2PerCapitaAdjustment - value) < 0.0001)
                    return;
                _alternative2PerCapitaAdjustment = value;
                OnPropertyChanged();
                ApplyScenarioAdjustments();
            }
        }

        private double _alternative2ImprovementsAdjustment = 20.0;
        public double Alternative2ImprovementsAdjustment
        {
            get => _alternative2ImprovementsAdjustment;
            set
            {
                if (Math.Abs(_alternative2ImprovementsAdjustment - value) < 0.0001)
                    return;
                _alternative2ImprovementsAdjustment = value;
                OnPropertyChanged();
                ApplyScenarioAdjustments();
            }
        }

        private double _alternative2LossesAdjustment = -20.0;
        public double Alternative2LossesAdjustment
        {
            get => _alternative2LossesAdjustment;
            set
            {
                if (Math.Abs(_alternative2LossesAdjustment - value) < 0.0001)
                    return;
                _alternative2LossesAdjustment = value;
                OnPropertyChanged();
                ApplyScenarioAdjustments();
            }
        }

        public ObservableCollection<DemandEntry> HistoricalData
        {
            get => _historicalData;
            set
            {
                if (ReferenceEquals(_historicalData, value))
                {
                    return;
                }

                DetachHistoricalHandlers(_historicalData);
                _historicalData = value ?? new ObservableCollection<DemandEntry>();
                AttachHistoricalHandlers(_historicalData);
                OnPropertyChanged();
                AutoPopulateBaseline();
            }
        }

        public int ForecastYears
        {
            get => _forecastYears;
            set
            {
                int bounded = Math.Clamp(value, 1, 100);
                if (_forecastYears == bounded)
                {
                    return;
                }

                _forecastYears = bounded;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<DemandEntry> Results
        {
            get => _results;
            set { _results = value; OnPropertyChanged(); }
        }

        public string Explanation
        {
            get => _explanation;
            set { _explanation = value; OnPropertyChanged(); }
        }

        public Scenario? SelectedScenario
        {
            get => _selectedScenario;
            set
            {
                _selectedScenario = value;
                OnPropertyChanged();
                Results = value?.Results ?? new ObservableCollection<DemandEntry>();
                OnPropertyChanged(nameof(SystemImprovementsPercent));
                OnPropertyChanged(nameof(SystemLossesPercent));
                OnPropertyChanged(nameof(BaseYear));
                OnPropertyChanged(nameof(BasePopulation));
                OnPropertyChanged(nameof(BasePerCapitaDemand));
                OnPropertyChanged(nameof(PopulationGrowthRate));
                OnPropertyChanged(nameof(PerCapitaDemandChangeRate));
            }
        }
        public double SystemImprovementsPercent
        {
            get => SelectedScenario?.SystemImprovementsPercent ?? 0;
            set { if (SelectedScenario != null) { SelectedScenario.SystemImprovementsPercent = value; OnPropertyChanged(); } }
        }

        public double SystemLossesPercent
        {
            get => SelectedScenario?.SystemLossesPercent ?? 0;
            set { if (SelectedScenario != null) { SelectedScenario.SystemLossesPercent = value; OnPropertyChanged(); } }
        }

        public int BaseYear
        {
            get => SelectedScenario?.BaseYear ?? 0;
            set { if (SelectedScenario != null) { SelectedScenario.BaseYear = value; OnPropertyChanged(); } }
        }

        public double BasePopulation
        {
            get => SelectedScenario?.BasePopulation ?? 0;
            set { if (SelectedScenario != null) { SelectedScenario.BasePopulation = value; OnPropertyChanged(); } }
        }

        public double BasePerCapitaDemand
        {
            get => SelectedScenario?.BasePerCapitaDemand ?? 0;
            set { if (SelectedScenario != null) { SelectedScenario.BasePerCapitaDemand = value; OnPropertyChanged(); } }
        }

        public double PopulationGrowthRate
        {
            get => SelectedScenario?.PopulationGrowthRate ?? 0;
            set { if (SelectedScenario != null) { SelectedScenario.PopulationGrowthRate = value; OnPropertyChanged(); } }
        }

        public double PerCapitaDemandChangeRate
        {
            get => SelectedScenario?.PerCapitaDemandChangeRate ?? 0;
            set { if (SelectedScenario != null) { SelectedScenario.PerCapitaDemandChangeRate = value; OnPropertyChanged(); } }
        }

        public IRelayCommand ForecastCommand { get; }
        public IAsyncRelayCommand ExportCommand { get; }
        public IRelayCommand ComputeCommand { get; }

        private readonly IExcelExportService _excelExportService;

        public WaterDemandViewModel(IExcelExportService excelExportService)
        {
            _excelExportService = excelExportService;
            Scenarios.Add(new Scenario
            {
                Name = "Baseline",
                LineBrush = ThemeResourceHelper.GetBrush("App.Chart.Series1", Brushes.Blue),
                Description = "Most likely projection based on expected population and demand changes"
            });
            Scenarios.Add(new Scenario
            {
                Name = "Alternative Forecast 1",
                LineBrush = ThemeResourceHelper.GetBrush("App.Chart.Series2", Brushes.Green),
                Description = "User-adjustable alternative that scales baseline assumptions upward"
            });
            Scenarios.Add(new Scenario
            {
                Name = "Alternative Forecast 2",
                LineBrush = ThemeResourceHelper.GetBrush("App.Chart.Series4", Brushes.Red),
                Description = "User-adjustable alternative that scales baseline assumptions downward"
            });

            foreach (var s in Scenarios)
            {
                InitializeScenario(s);
                s.PropertyChanged += Scenario_PropertyChanged;
            }

            _baselineScenario = Scenarios.FirstOrDefault(s => string.Equals(s.Name, "Baseline", StringComparison.OrdinalIgnoreCase));

            SelectedScenario = Scenarios.FirstOrDefault();

            ApplyScenarioAdjustments();

            AttachHistoricalHandlers(HistoricalData);

            ForecastCommand = new RelayCommand(Forecast);
            ExportCommand = new AsyncRelayCommand(ExportAsync);
            ComputeCommand = ForecastCommand;
            RefreshDiagnostics();
        }

        private void HistoricalData_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            MarkDirty();
            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                DetachHistoricalHandlers(HistoricalData);
                AttachHistoricalHandlers(HistoricalData);
                AutoPopulateBaseline();
                RefreshDiagnostics();
                return;
            }

            if (e.OldItems != null)
            {
                foreach (DemandEntry entry in e.OldItems)
                {
                    entry.PropertyChanged -= HistoricalEntryChanged;
                }
            }

            if (e.NewItems != null)
            {
                foreach (DemandEntry d in e.NewItems)
                    d.PropertyChanged += HistoricalEntryChanged;
            }
            AutoPopulateBaseline();
            RefreshDiagnostics();
        }

        private void HistoricalEntryChanged(object? sender, PropertyChangedEventArgs e)
        {
            MarkDirty();
            if (e.PropertyName == nameof(DemandEntry.Demand) || e.PropertyName == nameof(DemandEntry.Year))
                AutoPopulateBaseline();
            RefreshDiagnostics();
        }

        private void AttachHistoricalHandlers(ObservableCollection<DemandEntry> entries)
        {
            entries.CollectionChanged += HistoricalData_CollectionChanged;
            foreach (var entry in entries)
            {
                entry.PropertyChanged += HistoricalEntryChanged;
            }
        }

        private void DetachHistoricalHandlers(ObservableCollection<DemandEntry> entries)
        {
            entries.CollectionChanged -= HistoricalData_CollectionChanged;
            foreach (var entry in entries)
            {
                entry.PropertyChanged -= HistoricalEntryChanged;
            }
        }

        private void AutoPopulateBaseline()
        {
            if (_isImportingProject)
            {
                return;
            }

            if (HistoricalData.Count == 0) return;
            var last = HistoricalData[^1];
            foreach (var s in Scenarios)
            {
                s.BaseYear = last.Year;
                s.BasePerCapitaDemand = last.Demand;
                if (HistoricalData.Count >= 2)
                {
                    var prev = HistoricalData[^2];
                    if (prev.Demand != 0)
                        s.PerCapitaDemandChangeRate = (last.Demand - prev.Demand) / prev.Demand * 100.0;
                }
            }
            OnPropertyChanged(nameof(BaseYear));
            OnPropertyChanged(nameof(BasePerCapitaDemand));
            OnPropertyChanged(nameof(PerCapitaDemandChangeRate));
            ApplyScenarioAdjustments();
        }

        private void InitializeScenario(Scenario scenario)
        {
            ApplyDefaultSectorPercents(scenario);
            foreach (var sector in scenario.Sectors.Where(se => !se.IsResidual))
                sector.PropertyChanged += (_, __) => UpdateResidualShares(scenario);
            UpdateResidualShares(scenario);
        }

        private void Scenario_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_isImportingProject)
            {
                return;
            }

            if (sender is not Scenario scenario)
                return;

            if (!ReferenceEquals(scenario, _baselineScenario))
                return;

            MarkDirty();
            if (e.PropertyName == nameof(Scenario.PopulationGrowthRate) ||
                e.PropertyName == nameof(Scenario.PerCapitaDemandChangeRate) ||
                e.PropertyName == nameof(Scenario.SystemImprovementsPercent) ||
                e.PropertyName == nameof(Scenario.SystemLossesPercent) ||
                e.PropertyName == nameof(Scenario.BaseYear) ||
                e.PropertyName == nameof(Scenario.BasePerCapitaDemand) ||
                e.PropertyName == nameof(Scenario.BasePopulation))
            {
                ApplyScenarioAdjustments();
            }
            RefreshDiagnostics();
        }

        public WaterDemandProjectData ExportProjectData()
        {
            return new WaterDemandProjectData
            {
                ForecastYears = ForecastYears,
                ChartTitle = ChartTitle,
                Alternative1PopulationAdjustment = Alternative1PopulationAdjustment,
                Alternative1PerCapitaAdjustment = Alternative1PerCapitaAdjustment,
                Alternative1ImprovementsAdjustment = Alternative1ImprovementsAdjustment,
                Alternative1LossesAdjustment = Alternative1LossesAdjustment,
                Alternative2PopulationAdjustment = Alternative2PopulationAdjustment,
                Alternative2PerCapitaAdjustment = Alternative2PerCapitaAdjustment,
                Alternative2ImprovementsAdjustment = Alternative2ImprovementsAdjustment,
                Alternative2LossesAdjustment = Alternative2LossesAdjustment,
                HistoricalData = HistoricalData.Select(entry => new WaterDemandEntryData
                {
                    Year = entry.Year,
                    Demand = entry.Demand
                }).ToList(),
                Scenarios = Scenarios.Select(scenario => new WaterDemandScenarioData
                {
                    Name = scenario.Name,
                    Description = scenario.Description,
                    BaseYear = scenario.BaseYear,
                    BasePopulation = scenario.BasePopulation,
                    BasePerCapitaDemand = scenario.BasePerCapitaDemand,
                    PopulationGrowthRate = scenario.PopulationGrowthRate,
                    PerCapitaDemandChangeRate = scenario.PerCapitaDemandChangeRate,
                    SystemImprovementsPercent = scenario.SystemImprovementsPercent,
                    SystemLossesPercent = scenario.SystemLossesPercent,
                    Sectors = scenario.Sectors.Select(sector => new WaterDemandSectorShareData
                    {
                        Name = sector.Name,
                        CurrentPercent = sector.CurrentPercent,
                        FuturePercent = sector.FuturePercent,
                        IsResidual = sector.IsResidual
                    }).ToList()
                }).ToList(),
                SelectedScenarioName = SelectedScenario?.Name
            };
        }

        public void ImportProjectData(WaterDemandProjectData? data)
        {
            if (data == null)
            {
                return;
            }

            _isImportingProject = true;
            try
            {
                ForecastYears = data.ForecastYears;
                if (!string.IsNullOrWhiteSpace(data.ChartTitle))
                {
                    ChartTitle = data.ChartTitle;
                }

                Alternative1PopulationAdjustment = data.Alternative1PopulationAdjustment;
                Alternative1PerCapitaAdjustment = data.Alternative1PerCapitaAdjustment;
                Alternative1ImprovementsAdjustment = data.Alternative1ImprovementsAdjustment;
                Alternative1LossesAdjustment = data.Alternative1LossesAdjustment;
                Alternative2PopulationAdjustment = data.Alternative2PopulationAdjustment;
                Alternative2PerCapitaAdjustment = data.Alternative2PerCapitaAdjustment;
                Alternative2ImprovementsAdjustment = data.Alternative2ImprovementsAdjustment;
                Alternative2LossesAdjustment = data.Alternative2LossesAdjustment;

                DetachHistoricalHandlers(HistoricalData);
                HistoricalData = new ObservableCollection<DemandEntry>(data.HistoricalData.Select(entry => new DemandEntry
                {
                    Year = entry.Year,
                    Demand = entry.Demand
                }));
                AttachHistoricalHandlers(HistoricalData);

                Scenarios.Clear();
                int brushIndex = 0;
                foreach (var scenarioData in data.Scenarios)
                {
                    var scenario = new Scenario
                    {
                        Name = scenarioData.Name,
                        Description = scenarioData.Description,
                        BaseYear = scenarioData.BaseYear,
                        BasePopulation = scenarioData.BasePopulation,
                        BasePerCapitaDemand = scenarioData.BasePerCapitaDemand,
                        PopulationGrowthRate = scenarioData.PopulationGrowthRate,
                        PerCapitaDemandChangeRate = scenarioData.PerCapitaDemandChangeRate,
                        SystemImprovementsPercent = scenarioData.SystemImprovementsPercent,
                        SystemLossesPercent = scenarioData.SystemLossesPercent,
                        LineBrush = ThemeResourceHelper.GetBrush($"App.Chart.Series{Math.Min(brushIndex + 1, 6)}", Brushes.Blue)
                    };

                    InitializeScenario(scenario);
                    scenario.PropertyChanged += Scenario_PropertyChanged;

                    foreach (var sectorData in scenarioData.Sectors)
                    {
                        var sector = scenario.Sectors.FirstOrDefault(s =>
                            string.Equals(s.Name, sectorData.Name, StringComparison.OrdinalIgnoreCase));
                        if (sector == null)
                        {
                            sector = new SectorShare
                            {
                                Name = sectorData.Name,
                                IsResidual = sectorData.IsResidual
                            };
                            scenario.Sectors.Add(sector);
                        }

                        sector.CurrentPercent = sectorData.CurrentPercent;
                        sector.FuturePercent = sectorData.FuturePercent;
                        sector.IsResidual = sectorData.IsResidual;
                    }

                    UpdateResidualShares(scenario);
                    Scenarios.Add(scenario);
                    brushIndex++;
                }

                _baselineScenario = Scenarios.FirstOrDefault(s => string.Equals(s.Name, "Baseline", StringComparison.OrdinalIgnoreCase));
                SelectedScenario = Scenarios.FirstOrDefault(s =>
                    string.Equals(s.Name, data.SelectedScenarioName, StringComparison.OrdinalIgnoreCase))
                    ?? Scenarios.FirstOrDefault();
            }
            finally
            {
                _isImportingProject = false;
            }

            ApplyScenarioAdjustments();
            Forecast();
            RefreshDiagnostics();
        }

        protected override IEnumerable<DiagnosticItem> BuildDiagnostics()
        {
            var diagnostics = new List<DiagnosticItem>();

            if (HistoricalData.Count == 0)
            {
                diagnostics.Add(new DiagnosticItem(
                    DiagnosticLevel.Error,
                    "Missing historical demand data",
                    "Add at least one year of historical demand data to build a forecast."));
                return diagnostics;
            }

            if (HistoricalData.Any(h => h.Year <= 0))
            {
                diagnostics.Add(new DiagnosticItem(
                    DiagnosticLevel.Error,
                    "Invalid year",
                    "Historical entries must use a valid calendar year."));
            }

            if (HistoricalData.Any(h => h.Demand < 0))
            {
                diagnostics.Add(new DiagnosticItem(
                    DiagnosticLevel.Warning,
                    "Negative demand values",
                    "One or more demand entries are negative. Verify the units and inputs."));
            }

            bool yearsOutOfOrder = HistoricalData
                .Zip(HistoricalData.Skip(1), (current, next) => next.Year < current.Year)
                .Any(isOutOfOrder => isOutOfOrder);
            if (yearsOutOfOrder)
            {
                diagnostics.Add(new DiagnosticItem(
                    DiagnosticLevel.Warning,
                    "Years not ascending",
                    "Sort historical demand rows by year from earliest to latest."));
            }

            if (Scenarios.Count == 0)
            {
                diagnostics.Add(new DiagnosticItem(
                    DiagnosticLevel.Warning,
                    "No scenarios configured",
                    "Add at least one scenario to forecast adjusted demand."));
            }

            if (diagnostics.Count == 0)
            {
                diagnostics.Add(new DiagnosticItem(
                    DiagnosticLevel.Info,
                    "Water demand inputs look good",
                    "Historical data and scenario inputs are ready for forecasting."));
            }

            return diagnostics;
        }

        private void UpdateResidualShares(Scenario scenario)
        {
            MarkDirty();
            var residual = scenario.Sectors.FirstOrDefault(se => se.IsResidual);
            if (residual == null) return;
            residual.CurrentPercent = Math.Max(0, 100 - scenario.Sectors.Where(se => !se.IsResidual).Sum(se => se.CurrentPercent));
            residual.FuturePercent = Math.Max(0, 100 - scenario.Sectors.Where(se => !se.IsResidual).Sum(se => se.FuturePercent));
        }

        private void ApplyDefaultSectorPercents(Scenario scenario)
        {
            if (scenario == null)
                return;

            bool hasValues = scenario.Sectors
                .Where(se => !se.IsResidual)
                .Any(se => !IsApproximatelyZero(se.CurrentPercent) || !IsApproximatelyZero(se.FuturePercent));

            if (hasValues)
                return;

            foreach (var sector in scenario.Sectors.Where(se => !se.IsResidual))
            {
                if (DefaultSectorPercents.TryGetValue(sector.Name, out var defaults))
                {
                    sector.CurrentPercent = defaults.Current;
                    sector.FuturePercent = defaults.Future;
                }
            }
        }

        private void FillMissingSectorPercentsFromBaseline(Scenario scenario)
        {
            if (_baselineScenario == null || scenario == null || ReferenceEquals(scenario, _baselineScenario))
                return;

            foreach (var sector in scenario.Sectors.Where(se => !se.IsResidual))
            {
                var baselineSector = _baselineScenario.Sectors
                    .FirstOrDefault(se => se.Name.Equals(sector.Name, StringComparison.OrdinalIgnoreCase));
                if (baselineSector == null)
                    continue;

                if (IsApproximatelyZero(sector.CurrentPercent) && !IsApproximatelyZero(baselineSector.CurrentPercent))
                    sector.CurrentPercent = baselineSector.CurrentPercent;

                if (IsApproximatelyZero(sector.FuturePercent) && !IsApproximatelyZero(baselineSector.FuturePercent))
                    sector.FuturePercent = baselineSector.FuturePercent;
            }

            UpdateResidualShares(scenario);
        }

        private void ApplyScenarioAdjustments()
        {
            if (_baselineScenario == null)
                return;

            foreach (var scenario in Scenarios)
            {
                if (ReferenceEquals(scenario, _baselineScenario))
                    continue;

                bool isAlternative1 = IsAlternativeForecast1(scenario);
                bool isAlternative2 = IsAlternativeForecast2(scenario);

                if (!isAlternative1 && !isAlternative2)
                {
                    FillMissingSectorPercentsFromBaseline(scenario);
                    continue;
                }

                scenario.BaseYear = _baselineScenario.BaseYear;
                scenario.BasePerCapitaDemand = _baselineScenario.BasePerCapitaDemand;
                scenario.BasePopulation = _baselineScenario.BasePopulation;

                double popGrowth = _baselineScenario.PopulationGrowthRate;
                double perCapita = _baselineScenario.PerCapitaDemandChangeRate;
                double improvements = _baselineScenario.SystemImprovementsPercent;
                double losses = _baselineScenario.SystemLossesPercent;

                double populationAdjustment = isAlternative1 ? Alternative1PopulationAdjustment : Alternative2PopulationAdjustment;
                double perCapitaAdjustment = isAlternative1 ? Alternative1PerCapitaAdjustment : Alternative2PerCapitaAdjustment;
                double improvementsAdjustment = isAlternative1 ? Alternative1ImprovementsAdjustment : Alternative2ImprovementsAdjustment;
                double lossesAdjustment = isAlternative1 ? Alternative1LossesAdjustment : Alternative2LossesAdjustment;

                scenario.PopulationGrowthRate = AdjustByPercent(popGrowth, populationAdjustment);
                scenario.PerCapitaDemandChangeRate = AdjustByPercent(perCapita, perCapitaAdjustment);
                scenario.SystemImprovementsPercent = ClampNonNegative(AdjustByPercent(improvements, improvementsAdjustment));
                scenario.SystemLossesPercent = ClampNonNegative(AdjustByPercent(losses, lossesAdjustment));

                FillMissingSectorPercentsFromBaseline(scenario);
            }

            if (SelectedScenario != null)
            {
                OnPropertyChanged(nameof(SystemImprovementsPercent));
                OnPropertyChanged(nameof(SystemLossesPercent));
                OnPropertyChanged(nameof(PopulationGrowthRate));
                OnPropertyChanged(nameof(PerCapitaDemandChangeRate));
            }
        }

        private static bool IsAlternativeForecast1(Scenario scenario) =>
            scenario.Name.Contains("Alternative Forecast 1", StringComparison.OrdinalIgnoreCase);

        private static bool IsAlternativeForecast2(Scenario scenario) =>
            scenario.Name.Contains("Alternative Forecast 2", StringComparison.OrdinalIgnoreCase);

        private static double AdjustByPercent(double value, double percent)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                return 0;

            double result = value * (1 + percent / 100.0);
            return Math.Round(result, 6);
        }

        private static double ClampNonNegative(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                return 0;

            return Math.Max(0, value);
        }

        private void LoadHistoricalFromWorkbook()
        {
            try
            {
                string file = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                    "Practical Forecast Exercise - Tables 6-28-18.xlsx");
                if (!File.Exists(file)) return;
                using var archive = ZipFile.OpenRead(file);
                var entry = archive.GetEntry("xl/worksheets/sheet3.xml");
                if (entry == null) return;
                using var stream = entry.Open();
                var doc = XDocument.Load(stream);
                XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
                var rows = doc.Root?.Element(ns + "sheetData")?.Elements(ns + "row");
                if (rows == null) return;
                foreach (var r in rows)
                {
                    var cells = r.Elements(ns + "c").ToList();
                    if (cells.Count < 3) continue;
                    var y = cells[0].Element(ns + "v")?.Value;
                    var d = cells[2].Element(ns + "v")?.Value;
                    if (int.TryParse(y, out int year) && double.TryParse(d, out double demand))
                        HistoricalData.Add(new DemandEntry { Year = year, Demand = demand });
                }
            }
            catch
            {
            }
        }

        private void Forecast()
        {
            try
            {
                foreach (var scenario in Scenarios)
                {
                    scenario.Results.Clear();
                    var currentPercents = scenario.Sectors.Select(s => s.CurrentPercent).ToArray();
                    var futurePercents = scenario.Sectors.Select(s => s.FuturePercent).ToArray();
                    for (int t = 0; t <= ForecastYears; t++)
                    {
                        int year = scenario.BaseYear + t;
                        double population = scenario.BasePopulation * Math.Pow(1 + scenario.PopulationGrowthRate / 100.0, t);
                        double perCapita = scenario.BasePerCapitaDemand * Math.Pow(1 + scenario.PerCapitaDemandChangeRate / 100.0, t);
                        double demand = population * perCapita;

                        double industrialDemand = 0, residentialDemand = 0, commercialDemand = 0, agriculturalDemand = 0;
                        for (int i = 0; i < scenario.Sectors.Count; i++)
                        {
                            double percent = ForecastYears <= 0
                                ? currentPercents[i]
                                : currentPercents[i] + (futurePercents[i] - currentPercents[i]) * t / (double)ForecastYears;
                            double sectorDemand = demand * percent / 100.0;
                            switch (scenario.Sectors[i].Name)
                            {
                                case "Industrial": industrialDemand = sectorDemand; break;
                                case "Residential": residentialDemand = sectorDemand; break;
                                case "Commercial": commercialDemand = sectorDemand; break;
                                case "Agricultural": agriculturalDemand = sectorDemand; break;
                            }
                        }

                        double adjusted = CalculateAdjustedDemand(
                            demand,
                            scenario.SystemImprovementsPercent,
                            scenario.SystemLossesPercent);
                        double growthRate = t == 0 ? 0 : (demand / scenario.Results[t - 1].Demand - 1) * 100.0;

                        scenario.Results.Add(new DemandEntry
                        {
                            Year = year,
                            Demand = demand,
                            ResidentialDemand = residentialDemand,
                            CommercialDemand = commercialDemand,
                            IndustrialDemand = industrialDemand,
                            AgriculturalDemand = agriculturalDemand,
                            AdjustedDemand = adjusted,
                            GrowthRate = growthRate
                        });
                    }

                    AttachResultHandlers(scenario);
                }

                Results = SelectedScenario?.Results ?? new ObservableCollection<DemandEntry>();
                if (SelectedScenario != null)
                {
                    Explanation =
                        "Population = BasePopulation × (1 + PopulationGrowthRate)^t, " +
                        "Per Capita = BasePerCapitaDemand × (1 + PerCapitaDemandChangeRate)^t, " +
                        "Total Demand = Population × Per Capita. " +
                        "Shares interpolated: " +
                        string.Join(", ", SelectedScenario.Sectors.Select(s => $"{s.Name} {s.CurrentPercent:F1}%→{s.FuturePercent:F1}%")) +
                        " Adjusted Demand = Total Demand ÷ (1 - Losses %) × (1 - Improvements %). " +
                        $"Scenario uses {SelectedScenario.SystemImprovementsPercent:F1}% improvements and {SelectedScenario.SystemLossesPercent:F1}% losses.";
                    if (SelectedScenario.SystemLossesPercent >= 100)
                    {
                        Explanation += " Adjusted demand is undefined when losses are 100% or greater.";
                    }
                }

                UpdateChartSeries();
            }
            catch
            {
                foreach (var scenario in Scenarios)
                {
                    scenario.Results.Clear();
                }
                Results = new ObservableCollection<DemandEntry>();
                Explanation = string.Empty;
                ChartSeries.Clear();
                LegendItems.Clear();
                ChartStatusMessage = "Unable to compute forecast with the provided inputs.";
            }
            finally
            {
                MarkClean();
            }
        }

        private void AttachResultHandlers(Scenario scenario)
        {
            foreach (var entry in scenario.Results)
            {
                entry.PropertyChanged -= Entry_PropertyChanged;
                entry.PropertyChanged += Entry_PropertyChanged;
            }
        }

        private void Entry_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            MarkDirty();
            if (e.PropertyName == nameof(DemandEntry.GrowthRate) && sender is DemandEntry entry)
            {
                bool recalculated = false;
                foreach (var scenario in Scenarios)
                {
                    int index = scenario.Results.IndexOf(entry);
                    if (index > 0)
                    {
                        RecalculateFromIndex(scenario, index);
                        if (scenario == SelectedScenario)
                            Results = scenario.Results;
                        recalculated = true;
                        break;
                    }
                }

                if (recalculated)
                    ValidateAllScenarioResults();
            }
        }

        private void RecalculateFromIndex(Scenario scenario, int index)
        {
            int lastHistYear = HistoricalData.Count > 0
                ? HistoricalData[^1].Year
                : (scenario.Results.Count > 0 ? scenario.Results[0].Year : 0);

            int forecastStartIndex = GetFirstForecastIndex(scenario, lastHistYear);
            bool hasForecast = forecastStartIndex >= 0 && forecastStartIndex < scenario.Results.Count;
            int forecastBaseYear = hasForecast ? scenario.Results[forecastStartIndex].Year : lastHistYear;
            int finalForecastYear = hasForecast ? scenario.Results[^1].Year : forecastBaseYear;
            int forecastYearRange = finalForecastYear - forecastBaseYear;
            bool singleForecastStep = hasForecast && forecastYearRange == 0;
            double forecastYearSpan = singleForecastStep ? 1 : Math.Max(1, forecastYearRange);

            for (int i = index; i < scenario.Results.Count; i++)
            {
                double baseDemand = scenario.Results[i - 1].Demand;
                double rate = scenario.Results[i].GrowthRate / 100.0;
                scenario.Results[i].Demand = baseDemand * (1 + rate);

                double t = 0;
                if (hasForecast && scenario.Results[i].Year > lastHistYear)
                {
                    if (singleForecastStep)
                    {
                        t = 1;
                    }
                    else
                    {
                        double progress = scenario.Results[i].Year - forecastBaseYear;
                        t = Math.Clamp(progress / forecastYearSpan, 0, 1);
                    }
                }

                double industrialDemand = 0, residentialDemand = 0, commercialDemand = 0, agriculturalDemand = 0;
                for (int s = 0; s < scenario.Sectors.Count; s++)
                {
                    double current = scenario.Sectors[s].CurrentPercent;
                    double future = scenario.Sectors[s].FuturePercent;
                    double percent = scenario.Results[i].Year <= lastHistYear
                        ? current
                        : current + (future - current) * t;
                    double sectorDemand = scenario.Results[i].Demand * percent / 100.0;
                    switch (scenario.Sectors[s].Name)
                    {
                        case "Industrial": industrialDemand = sectorDemand; break;
                        case "Residential": residentialDemand = sectorDemand; break;
                        case "Commercial": commercialDemand = sectorDemand; break;
                        case "Agricultural": agriculturalDemand = sectorDemand; break;
                    }
                }

                scenario.Results[i].IndustrialDemand = industrialDemand;
                scenario.Results[i].ResidentialDemand = residentialDemand;
                scenario.Results[i].CommercialDemand = commercialDemand;
                scenario.Results[i].AgriculturalDemand = agriculturalDemand;
                scenario.Results[i].AdjustedDemand = CalculateAdjustedDemand(
                    scenario.Results[i].Demand,
                    scenario.SystemImprovementsPercent,
                    scenario.SystemLossesPercent);
            }
        }

        private static int GetFirstForecastIndex(Scenario scenario, int lastHistoricalYear)
        {
            for (int i = 0; i < scenario.Results.Count; i++)
            {
                if (scenario.Results[i].Year > lastHistoricalYear)
                    return i;
            }
            return scenario.Results.Count;
        }

        private void ValidateAllScenarioResults()
        {
            foreach (var scenario in Scenarios)
            {
                bool scenarioUpdated = false;
                for (int i = 1; i < scenario.Results.Count; i++)
                {
                    var current = scenario.Results[i];
                    var previous = scenario.Results[i - 1];

                    double expectedDemand = previous.Demand * (1 + current.GrowthRate / 100.0);
                    if (!NearlyEqual(current.Demand, expectedDemand))
                    {
                        current.Demand = expectedDemand;
                        scenarioUpdated = true;
                    }

                    double expectedAdjusted = CalculateAdjustedDemand(
                        current.Demand,
                        scenario.SystemImprovementsPercent,
                        scenario.SystemLossesPercent);
                    if (double.IsFinite(expectedAdjusted))
                    {
                        if (!NearlyEqual(current.AdjustedDemand, expectedAdjusted))
                        {
                            current.AdjustedDemand = expectedAdjusted;
                            scenarioUpdated = true;
                        }
                    }
                    else if (!double.IsNaN(current.AdjustedDemand))
                    {
                        current.AdjustedDemand = double.NaN;
                        scenarioUpdated = true;
                    }
                }

                if (scenarioUpdated)
                {
                    if (scenario == SelectedScenario)
                        Results = scenario.Results;
                }
            }

            UpdateChartSeries();
        }

        private static bool NearlyEqual(double value1, double value2, double tolerance = 1e-6)
        {
            double scale = Math.Max(1.0, Math.Max(Math.Abs(value1), Math.Abs(value2)));
            return Math.Abs(value1 - value2) <= tolerance * scale;
        }

        private static bool IsApproximatelyZero(double value, double tolerance = 1e-6)
            => Math.Abs(value) <= tolerance;
        private static double CalculateAdjustedDemand(double demand, double improvementsPercent, double lossesPercent)
        {
            if (!double.IsFinite(demand))
                return double.NaN;

            double improvementsFactor = 1 - improvementsPercent / 100.0;
            double lossesFraction = lossesPercent / 100.0;
            if (!double.IsFinite(improvementsFactor) || !double.IsFinite(lossesFraction))
                return double.NaN;

            double denominator = 1 - lossesFraction;
            if (denominator <= 0 || !double.IsFinite(denominator))
                return double.NaN;

            return demand / denominator * improvementsFactor;
        }

        private async Task ExportAsync()
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Excel Workbook (*.xlsx)|*.xlsx",
                FileName = "water_demand.xlsx"
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    Forecast();
                    await Task.Run(() => _excelExportService.ExportWaterDemand(Scenarios, dlg.FileName));
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex);
                    MessageBox.Show($"Export failed: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void UpdateChartSeries()
        {
            ChartSeries.Clear();
            LegendItems.Clear();

            var validScenarios = Scenarios
                .Where(s => s.Results.Any(r => double.IsFinite(r.AdjustedDemand)))
                .ToList();

            if (validScenarios.Count == 0)
            {
                ChartStatusMessage = "Enter forecast inputs to visualize adjusted demand by scenario.";
                return;
            }

            foreach (var scenario in validScenarios)
            {
                var points = scenario.Results
                    .Where(r => double.IsFinite(r.AdjustedDemand))
                    .OrderBy(r => r.Year)
                    .Select(r => new ChartDataPoint
                    {
                        X = r.Year,
                        Y = r.AdjustedDemand
                    })
                    .ToList();

                if (points.Count == 0)
                {
                    continue;
                }

                ChartSeries.Add(new ChartSeries
                {
                    Name = scenario.Name,
                    Stroke = scenario.LineBrush,
                    Points = points
                });

                LegendItems.Add(new LegendItem
                {
                    Name = scenario.Name,
                    Color = scenario.LineBrush
                });
            }

            ChartStatusMessage = string.Empty;
        }
    }
}
