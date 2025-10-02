using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.IO;
using System.IO.Compression;
using System.Xml.Linq;
using System.Windows.Input;
using System.Windows.Media;
using System.ComponentModel;
using System.Collections.Specialized;
using EconToolbox.Desktop.Models;
using EconToolbox.Desktop.Services;

namespace EconToolbox.Desktop.ViewModels
{
    /// <summary>
    /// View model allowing users to enter historical demand data and
    /// project future demand using either linear regression or growth
    /// rate methods. Results can be displayed in a simple chart.
    /// </summary>
    public class WaterDemandViewModel : BaseViewModel
    {
        private ObservableCollection<DemandEntry> _historicalData = new();
        private int _forecastYears = 5;
        private ObservableCollection<DemandEntry> _results = new();
        private string _explanation = string.Empty;
        public ObservableCollection<Scenario> Scenarios { get; } = new();
        private Scenario? _selectedScenario;
        private Scenario? _baselineScenario;

        private const double ScenarioVariationPercentValue = 20.0;

        public double ScenarioVariationPercent => ScenarioVariationPercentValue;

        public string PositiveVariationDescription => $"+{ScenarioVariationPercentValue:0.#}% (automatic)";

        public string NegativeVariationDescription => $"-{ScenarioVariationPercentValue:0.#}% (automatic)";

        public ObservableCollection<DemandEntry> HistoricalData
        {
            get => _historicalData;
            set { _historicalData = value; OnPropertyChanged(); }
        }

        public int ForecastYears
        {
            get => _forecastYears;
            set { _forecastYears = value; OnPropertyChanged(); }
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

        public ICommand ForecastCommand { get; }
        public ICommand ExportCommand { get; }
        public ICommand ComputeCommand { get; }

        public WaterDemandViewModel()
        {
            Scenarios.Add(new Scenario
            {
                Name = "Baseline",
                LineBrush = Brushes.Blue,
                Description = "Most likely projection based on expected population and demand changes"
            });
            Scenarios.Add(new Scenario
            {
                Name = "Optimistic",
                LineBrush = Brushes.Green,
                Description = "Higher growth assumptions resulting in greater future demand"
            });
            Scenarios.Add(new Scenario
            {
                Name = "Pessimistic",
                LineBrush = Brushes.Red,
                Description = "Lower growth assumptions producing reduced future demand"
            });

            foreach (var s in Scenarios)
            {
                InitializeScenario(s);
                s.PropertyChanged += Scenario_PropertyChanged;
            }

            _baselineScenario = Scenarios.FirstOrDefault(s => string.Equals(s.Name, "Baseline", StringComparison.OrdinalIgnoreCase));

            HistoricalData.CollectionChanged += HistoricalData_CollectionChanged;

            SelectedScenario = Scenarios.FirstOrDefault();

            ApplyScenarioAdjustments();

            ForecastCommand = new RelayCommand(Forecast);
            ExportCommand = new RelayCommand(Export);
            ComputeCommand = ForecastCommand;
        }

        private void HistoricalData_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (DemandEntry d in e.NewItems)
                    d.PropertyChanged += HistoricalEntryChanged;
            }
            AutoPopulateBaseline();
        }

        private void HistoricalEntryChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DemandEntry.Demand) || e.PropertyName == nameof(DemandEntry.Year))
                AutoPopulateBaseline();
        }

        private void AutoPopulateBaseline()
        {
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
            foreach (var sector in scenario.Sectors.Where(se => !se.IsResidual))
                sector.PropertyChanged += (_, __) => UpdateResidualShares(scenario);
            UpdateResidualShares(scenario);
        }

        private void Scenario_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is not Scenario scenario)
                return;

            if (!ReferenceEquals(scenario, _baselineScenario))
                return;

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
        }

        private void UpdateResidualShares(Scenario scenario)
        {
            var residual = scenario.Sectors.FirstOrDefault(se => se.IsResidual);
            if (residual == null) return;
            residual.CurrentPercent = Math.Max(0, 100 - scenario.Sectors.Where(se => !se.IsResidual).Sum(se => se.CurrentPercent));
            residual.FuturePercent = Math.Max(0, 100 - scenario.Sectors.Where(se => !se.IsResidual).Sum(se => se.FuturePercent));
        }

        private void ApplyScenarioAdjustments()
        {
            if (_baselineScenario == null)
                return;

            foreach (var scenario in Scenarios)
            {
                if (ReferenceEquals(scenario, _baselineScenario))
                    continue;

                bool isOptimistic = IsOptimisticScenario(scenario);
                bool isPessimistic = IsPessimisticScenario(scenario);

                if (!isOptimistic && !isPessimistic)
                    continue;

                scenario.BaseYear = _baselineScenario.BaseYear;
                scenario.BasePerCapitaDemand = _baselineScenario.BasePerCapitaDemand;
                scenario.BasePopulation = _baselineScenario.BasePopulation;

                double popGrowth = _baselineScenario.PopulationGrowthRate;
                double perCapita = _baselineScenario.PerCapitaDemandChangeRate;
                double improvements = _baselineScenario.SystemImprovementsPercent;
                double losses = _baselineScenario.SystemLossesPercent;

                double variationPercent = ScenarioVariationPercent;

                if (isOptimistic)
                {
                    scenario.PopulationGrowthRate = AdjustByPercent(popGrowth, variationPercent);
                    scenario.PerCapitaDemandChangeRate = AdjustByPercent(perCapita, variationPercent);
                    scenario.SystemImprovementsPercent = ClampNonNegative(AdjustByPercent(improvements, -variationPercent));
                    scenario.SystemLossesPercent = ClampNonNegative(AdjustByPercent(losses, variationPercent));
                }
                else if (isPessimistic)
                {
                    scenario.PopulationGrowthRate = AdjustByPercent(popGrowth, -variationPercent);
                    scenario.PerCapitaDemandChangeRate = AdjustByPercent(perCapita, -variationPercent);
                    scenario.SystemImprovementsPercent = ClampNonNegative(AdjustByPercent(improvements, variationPercent));
                    scenario.SystemLossesPercent = ClampNonNegative(AdjustByPercent(losses, -variationPercent));
                }
            }

            if (SelectedScenario != null)
            {
                OnPropertyChanged(nameof(SystemImprovementsPercent));
                OnPropertyChanged(nameof(SystemLossesPercent));
                OnPropertyChanged(nameof(PopulationGrowthRate));
                OnPropertyChanged(nameof(PerCapitaDemandChangeRate));
            }
        }

        private static bool IsOptimisticScenario(Scenario scenario) =>
            scenario.Name.Contains("Optimistic", StringComparison.OrdinalIgnoreCase);

        private static bool IsPessimisticScenario(Scenario scenario) =>
            scenario.Name.Contains("Pessimistic", StringComparison.OrdinalIgnoreCase);

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
                    scenario.ChartPoints = CreatePointCollection(scenario.Results.Select(r => (r.Year, r.AdjustedDemand)).ToList());
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
            }
            catch
            {
                foreach (var scenario in Scenarios)
                {
                    scenario.Results.Clear();
                    scenario.ChartPoints = new PointCollection();
                }
                Results = new ObservableCollection<DemandEntry>();
                Explanation = string.Empty;
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
            if (e.PropertyName == nameof(DemandEntry.GrowthRate) && sender is DemandEntry entry)
            {
                bool recalculated = false;
                foreach (var scenario in Scenarios)
                {
                    int index = scenario.Results.IndexOf(entry);
                    if (index > 0)
                    {
                        RecalculateFromIndex(scenario, index);
                        scenario.ChartPoints = CreatePointCollection(scenario.Results.Select(r => (r.Year, r.AdjustedDemand)).ToList());
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
                    scenario.ChartPoints = CreatePointCollection(scenario.Results.Select(r => (r.Year, r.AdjustedDemand)).ToList());
                    if (scenario == SelectedScenario)
                        Results = scenario.Results;
                }
            }
        }

        private static bool NearlyEqual(double value1, double value2, double tolerance = 1e-6)
        {
            double scale = Math.Max(1.0, Math.Max(Math.Abs(value1), Math.Abs(value2)));
            return Math.Abs(value1 - value2) <= tolerance * scale;
        }
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

        private static PointCollection CreatePointCollection(List<(int Year, double Demand)> data)
        {
            PointCollection points = new();
            var finiteData = data.Where(d => double.IsFinite(d.Demand)).ToList();
            if (finiteData.Count == 0) return points;

            double minYear = finiteData[0].Year;
            double maxYear = finiteData[^1].Year;
            double minDemand = double.MaxValue;
            double maxDemand = double.MinValue;
            foreach (var d in finiteData)
            {
                if (d.Demand < minDemand) minDemand = d.Demand;
                if (d.Demand > maxDemand) maxDemand = d.Demand;
            }

            const double width = 300; // Canvas width used in XAML
            const double height = 168; // Canvas height used in XAML
            double yearRange = maxYear - minYear;
            if (yearRange == 0) yearRange = 1;
            double demandRange = maxDemand - minDemand;
            if (demandRange == 0) demandRange = 1;

            foreach (var d in finiteData)
            {
                double x = (d.Year - minYear) / yearRange * width;
                double y = height - (d.Demand - minDemand) / demandRange * height;
                points.Add(new System.Windows.Point(x, y));
            }

            return points;
        }

        private void Export()
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Excel Workbook (*.xlsx)|*.xlsx",
                FileName = "water_demand.xlsx"
            };
            if (dlg.ShowDialog() == true)
            {
                Services.ExcelExporter.ExportWaterDemand(Scenarios, dlg.FileName);
            }
        }
    }
}

