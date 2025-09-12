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
        private bool _useGrowthRate;
        private ObservableCollection<DemandEntry> _results = new();
        private string _explanation = string.Empty;
        public ObservableCollection<Scenario> Scenarios { get; } = new();
        private Scenario? _selectedScenario;

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

        public bool UseGrowthRate
        {
            get => _useGrowthRate;
            set { _useGrowthRate = value; OnPropertyChanged(); }
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
                OnPropertyChanged(nameof(CurrentIndustrialPercent));
                OnPropertyChanged(nameof(FutureIndustrialPercent));
                OnPropertyChanged(nameof(CurrentResidentialPercent));
                OnPropertyChanged(nameof(FutureResidentialPercent));
                OnPropertyChanged(nameof(CurrentCommercialPercent));
                OnPropertyChanged(nameof(FutureCommercialPercent));
                OnPropertyChanged(nameof(CurrentAgriculturalPercent));
                OnPropertyChanged(nameof(FutureAgriculturalPercent));
                OnPropertyChanged(nameof(SystemImprovementsPercent));
                OnPropertyChanged(nameof(SystemLossesPercent));
                OnPropertyChanged(nameof(StandardGrowthRate));
                OnPropertyChanged(nameof(BaseYear));
                OnPropertyChanged(nameof(BasePopulation));
                OnPropertyChanged(nameof(BasePerCapitaDemand));
                OnPropertyChanged(nameof(PopulationGrowthRate));
                OnPropertyChanged(nameof(PerCapitaDemandChangeRate));
            }
        }

        public double CurrentIndustrialPercent
        {
            get => SelectedScenario?.CurrentIndustrialPercent ?? 0;
            set { if (SelectedScenario != null) { SelectedScenario.CurrentIndustrialPercent = value; OnPropertyChanged(); } }
        }

        public double FutureIndustrialPercent
        {
            get => SelectedScenario?.FutureIndustrialPercent ?? 0;
            set { if (SelectedScenario != null) { SelectedScenario.FutureIndustrialPercent = value; OnPropertyChanged(); } }
        }

        public double CurrentResidentialPercent
        {
            get => SelectedScenario?.CurrentResidentialPercent ?? 0;
            set { if (SelectedScenario != null) { SelectedScenario.CurrentResidentialPercent = value; OnPropertyChanged(); } }
        }

        public double FutureResidentialPercent
        {
            get => SelectedScenario?.FutureResidentialPercent ?? 0;
            set { if (SelectedScenario != null) { SelectedScenario.FutureResidentialPercent = value; OnPropertyChanged(); } }
        }

        public double CurrentCommercialPercent
        {
            get => SelectedScenario?.CurrentCommercialPercent ?? 0;
            set { if (SelectedScenario != null) { SelectedScenario.CurrentCommercialPercent = value; OnPropertyChanged(); } }
        }

        public double FutureCommercialPercent
        {
            get => SelectedScenario?.FutureCommercialPercent ?? 0;
            set { if (SelectedScenario != null) { SelectedScenario.FutureCommercialPercent = value; OnPropertyChanged(); } }
        }

        public double CurrentAgriculturalPercent
        {
            get => SelectedScenario?.CurrentAgriculturalPercent ?? 0;
            set { if (SelectedScenario != null) { SelectedScenario.CurrentAgriculturalPercent = value; OnPropertyChanged(); } }
        }

        public double FutureAgriculturalPercent
        {
            get => SelectedScenario?.FutureAgriculturalPercent ?? 0;
            set { if (SelectedScenario != null) { SelectedScenario.FutureAgriculturalPercent = value; OnPropertyChanged(); } }
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

        public double StandardGrowthRate
        {
            get => SelectedScenario?.StandardGrowthRate ?? 0;
            set { if (SelectedScenario != null) { SelectedScenario.StandardGrowthRate = value; OnPropertyChanged(); } }
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
            SelectedScenario = Scenarios.FirstOrDefault();

            ForecastCommand = new RelayCommand(Forecast);
            ExportCommand = new RelayCommand(Export);
            ComputeCommand = ForecastCommand;
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
                    for (int t = 0; t <= ForecastYears; t++)
                    {
                        int year = scenario.BaseYear + t;
                        double population = scenario.BasePopulation * Math.Pow(1 + scenario.PopulationGrowthRate / 100.0, t);
                        double perCapita = scenario.BasePerCapitaDemand * Math.Pow(1 + scenario.PerCapitaDemandChangeRate / 100.0, t);
                        double demand = population * perCapita;

                        double industrialPercent = ForecastYears <= 0
                            ? scenario.CurrentIndustrialPercent
                            : scenario.CurrentIndustrialPercent + (scenario.FutureIndustrialPercent - scenario.CurrentIndustrialPercent) * t / (double)ForecastYears;
                        double residentialPercent = ForecastYears <= 0
                            ? scenario.CurrentResidentialPercent
                            : scenario.CurrentResidentialPercent + (scenario.FutureResidentialPercent - scenario.CurrentResidentialPercent) * t / (double)ForecastYears;
                        double commercialPercent = ForecastYears <= 0
                            ? scenario.CurrentCommercialPercent
                            : scenario.CurrentCommercialPercent + (scenario.FutureCommercialPercent - scenario.CurrentCommercialPercent) * t / (double)ForecastYears;
                        double agriculturalPercent = ForecastYears <= 0
                            ? scenario.CurrentAgriculturalPercent
                            : scenario.CurrentAgriculturalPercent + (scenario.FutureAgriculturalPercent - scenario.CurrentAgriculturalPercent) * t / (double)ForecastYears;

                        double industrialDemand = demand * industrialPercent / 100.0;
                        double residentialDemand = demand * residentialPercent / 100.0;
                        double commercialDemand = demand * commercialPercent / 100.0;
                        double agriculturalDemand = demand * agriculturalPercent / 100.0;
                        double adjusted = demand * (1 + scenario.SystemLossesPercent / 100.0) * (1 - scenario.SystemImprovementsPercent / 100.0);
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
                        $"Shares interpolated: Res {SelectedScenario.CurrentResidentialPercent:F1}%→{SelectedScenario.FutureResidentialPercent:F1}%, " +
                        $"Com {SelectedScenario.CurrentCommercialPercent:F1}%→{SelectedScenario.FutureCommercialPercent:F1}%, " +
                        $"Ind {SelectedScenario.CurrentIndustrialPercent:F1}%→{SelectedScenario.FutureIndustrialPercent:F1}%, " +
                        $"Ag {SelectedScenario.CurrentAgriculturalPercent:F1}%→{SelectedScenario.FutureAgriculturalPercent:F1}% " +
                        $"with {SelectedScenario.SystemImprovementsPercent:F1}% improvements and {SelectedScenario.SystemLossesPercent:F1}% losses.";
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
                foreach (var scenario in Scenarios)
                {
                    int index = scenario.Results.IndexOf(entry);
                    if (index > 0)
                    {
                        RecalculateFromIndex(scenario, index);
                        scenario.ChartPoints = CreatePointCollection(scenario.Results.Select(r => (r.Year, r.AdjustedDemand)).ToList());
                        if (scenario == SelectedScenario)
                            Results = scenario.Results;
                        break;
                    }
                }
            }
        }

        private void RecalculateFromIndex(Scenario scenario, int index)
        {
            int forecastStartIndex = HistoricalData.Count;
            int lastHistYear = forecastStartIndex > 0 ? HistoricalData[^1].Year : 0;
            for (int i = index; i < scenario.Results.Count; i++)
            {
                double baseDemand = scenario.Results[i - 1].Demand;
                double rate = scenario.Results[i].GrowthRate / 100.0;
                scenario.Results[i].Demand = baseDemand * (1 + rate);

                double t = scenario.Results[i].Year <= lastHistYear
                    ? 0
                    : (ForecastYears <= 1 ? 1 : (i - forecastStartIndex) / (double)(ForecastYears - 1));

                double industrialPercent = scenario.Results[i].Year <= lastHistYear
                    ? scenario.CurrentIndustrialPercent
                    : scenario.CurrentIndustrialPercent + (scenario.FutureIndustrialPercent - scenario.CurrentIndustrialPercent) * t;
                double residentialPercent = scenario.Results[i].Year <= lastHistYear
                    ? scenario.CurrentResidentialPercent
                    : scenario.CurrentResidentialPercent + (scenario.FutureResidentialPercent - scenario.CurrentResidentialPercent) * t;
                double commercialPercent = scenario.Results[i].Year <= lastHistYear
                    ? scenario.CurrentCommercialPercent
                    : scenario.CurrentCommercialPercent + (scenario.FutureCommercialPercent - scenario.CurrentCommercialPercent) * t;
                double agriculturalPercent = scenario.Results[i].Year <= lastHistYear
                    ? scenario.CurrentAgriculturalPercent
                    : scenario.CurrentAgriculturalPercent + (scenario.FutureAgriculturalPercent - scenario.CurrentAgriculturalPercent) * t;

                scenario.Results[i].IndustrialDemand = scenario.Results[i].Demand * industrialPercent / 100.0;
                scenario.Results[i].ResidentialDemand = scenario.Results[i].Demand * residentialPercent / 100.0;
                scenario.Results[i].CommercialDemand = scenario.Results[i].Demand * commercialPercent / 100.0;
                scenario.Results[i].AgriculturalDemand = scenario.Results[i].Demand * agriculturalPercent / 100.0;
                scenario.Results[i].AdjustedDemand = scenario.Results[i].Demand * (1 + scenario.SystemLossesPercent / 100.0) * (1 - scenario.SystemImprovementsPercent / 100.0);
            }
        }
        private static PointCollection CreatePointCollection(List<(int Year, double Demand)> data)
        {
            PointCollection points = new();
            if (data.Count == 0) return points;

            double minYear = data[0].Year;
            double maxYear = data[^1].Year;
            double minDemand = double.MaxValue;
            double maxDemand = double.MinValue;
            foreach (var d in data)
            {
                if (d.Demand < minDemand) minDemand = d.Demand;
                if (d.Demand > maxDemand) maxDemand = d.Demand;
            }

            double width = 300; // Canvas width used in XAML
            double height = 150; // Canvas height used in XAML
            double yearRange = maxYear - minYear;
            if (yearRange == 0) yearRange = 1;
            double demandRange = maxDemand - minDemand;
            if (demandRange == 0) demandRange = 1;

            foreach (var d in data)
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

