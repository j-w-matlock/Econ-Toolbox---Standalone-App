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
        private PointCollection _chartPoints = new();
        private string _explanation = string.Empty;
        private double _currentIndustrialPercent;
        private double _futureIndustrialPercent;
        private double _currentResidentialPercent;
        private double _futureResidentialPercent;
        private double _currentCommercialPercent;
        private double _futureCommercialPercent;
        private double _currentAgriculturalPercent;
        private double _futureAgriculturalPercent;
        private double _systemImprovementsPercent;
        private double _systemLossesPercent;
        private double _standardGrowthRate;
        private int _baseYear;
        private double _basePopulation;
        private double _basePerCapitaDemand;
        private double _populationGrowthRate;
        private double _perCapitaDemandChangeRate;

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

        public PointCollection ChartPoints
        {
            get => _chartPoints;
            set { _chartPoints = value; OnPropertyChanged(); }
        }

        public string Explanation
        {
            get => _explanation;
            set { _explanation = value; OnPropertyChanged(); }
        }

        public double CurrentIndustrialPercent
        {
            get => _currentIndustrialPercent;
            set { _currentIndustrialPercent = value; OnPropertyChanged(); }
        }

        public double FutureIndustrialPercent
        {
            get => _futureIndustrialPercent;
            set { _futureIndustrialPercent = value; OnPropertyChanged(); }
        }

        public double CurrentResidentialPercent
        {
            get => _currentResidentialPercent;
            set { _currentResidentialPercent = value; OnPropertyChanged(); }
        }

        public double FutureResidentialPercent
        {
            get => _futureResidentialPercent;
            set { _futureResidentialPercent = value; OnPropertyChanged(); }
        }

        public double CurrentCommercialPercent
        {
            get => _currentCommercialPercent;
            set { _currentCommercialPercent = value; OnPropertyChanged(); }
        }

        public double FutureCommercialPercent
        {
            get => _futureCommercialPercent;
            set { _futureCommercialPercent = value; OnPropertyChanged(); }
        }

        public double CurrentAgriculturalPercent
        {
            get => _currentAgriculturalPercent;
            set { _currentAgriculturalPercent = value; OnPropertyChanged(); }
        }

        public double FutureAgriculturalPercent
        {
            get => _futureAgriculturalPercent;
            set { _futureAgriculturalPercent = value; OnPropertyChanged(); }
        }

        public double SystemImprovementsPercent
        {
            get => _systemImprovementsPercent;
            set { _systemImprovementsPercent = value; OnPropertyChanged(); }
        }

        public double SystemLossesPercent
        {
            get => _systemLossesPercent;
            set { _systemLossesPercent = value; OnPropertyChanged(); }
        }

        public double StandardGrowthRate
        {
            get => _standardGrowthRate;
            set { _standardGrowthRate = value; OnPropertyChanged(); }
        }

        public int BaseYear
        {
            get => _baseYear;
            set { _baseYear = value; OnPropertyChanged(); }
        }

        public double BasePopulation
        {
            get => _basePopulation;
            set { _basePopulation = value; OnPropertyChanged(); }
        }

        public double BasePerCapitaDemand
        {
            get => _basePerCapitaDemand;
            set { _basePerCapitaDemand = value; OnPropertyChanged(); }
        }

        public double PopulationGrowthRate
        {
            get => _populationGrowthRate;
            set { _populationGrowthRate = value; OnPropertyChanged(); }
        }

        public double PerCapitaDemandChangeRate
        {
            get => _perCapitaDemandChangeRate;
            set { _perCapitaDemandChangeRate = value; OnPropertyChanged(); }
        }

        public ICommand ForecastCommand { get; }
        public ICommand ExportCommand { get; }
        public ICommand ComputeCommand { get; }

        public WaterDemandViewModel()
        {
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
                Results = new ObservableCollection<DemandEntry>();
                for (int t = 0; t <= ForecastYears; t++)
                {
                    int year = BaseYear + t;
                    double population = BasePopulation * Math.Pow(1 + PopulationGrowthRate / 100.0, t);
                    double perCapita = BasePerCapitaDemand * Math.Pow(1 + PerCapitaDemandChangeRate / 100.0, t);
                    double demand = population * perCapita;

                    double industrialPercent = ForecastYears <= 0
                        ? CurrentIndustrialPercent
                        : CurrentIndustrialPercent + (FutureIndustrialPercent - CurrentIndustrialPercent) * t / (double)ForecastYears;
                    double residentialPercent = ForecastYears <= 0
                        ? CurrentResidentialPercent
                        : CurrentResidentialPercent + (FutureResidentialPercent - CurrentResidentialPercent) * t / (double)ForecastYears;
                    double commercialPercent = ForecastYears <= 0
                        ? CurrentCommercialPercent
                        : CurrentCommercialPercent + (FutureCommercialPercent - CurrentCommercialPercent) * t / (double)ForecastYears;
                    double agriculturalPercent = ForecastYears <= 0
                        ? CurrentAgriculturalPercent
                        : CurrentAgriculturalPercent + (FutureAgriculturalPercent - CurrentAgriculturalPercent) * t / (double)ForecastYears;

                    double industrialDemand = demand * industrialPercent / 100.0;
                    double residentialDemand = demand * residentialPercent / 100.0;
                    double commercialDemand = demand * commercialPercent / 100.0;
                    double agriculturalDemand = demand * agriculturalPercent / 100.0;
                    double adjusted = demand * (1 + SystemLossesPercent / 100.0) * (1 - SystemImprovementsPercent / 100.0);
                    double growthRate = t == 0 ? 0 : (demand / Results[t - 1].Demand - 1) * 100.0;

                    Results.Add(new DemandEntry
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

                AttachResultHandlers();
                ChartPoints = CreatePointCollection(Results.Select(r => (r.Year, r.AdjustedDemand)).ToList());
                Explanation =
                    "Population = BasePopulation × (1 + PopulationGrowthRate)^t, " +
                    "Per Capita = BasePerCapitaDemand × (1 + PerCapitaDemandChangeRate)^t, " +
                    "Total Demand = Population × Per Capita. " +
                    $"Shares interpolated: Res {CurrentResidentialPercent:F1}%→{FutureResidentialPercent:F1}%, " +
                    $"Com {CurrentCommercialPercent:F1}%→{FutureCommercialPercent:F1}%, " +
                    $"Ind {CurrentIndustrialPercent:F1}%→{FutureIndustrialPercent:F1}%, " +
                    $"Ag {CurrentAgriculturalPercent:F1}%→{FutureAgriculturalPercent:F1}% " +
                    $"with {SystemImprovementsPercent:F1}% improvements and {SystemLossesPercent:F1}% losses.";
            }
            catch
            {
                Results = new ObservableCollection<DemandEntry>();
                ChartPoints = new PointCollection();
                Explanation = string.Empty;
            }
        }

        private void AttachResultHandlers()
        {
            foreach (var entry in Results)
            {
                entry.PropertyChanged -= Entry_PropertyChanged;
                entry.PropertyChanged += Entry_PropertyChanged;
            }
        }

        private void Entry_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DemandEntry.GrowthRate) && sender is DemandEntry entry)
            {
                int index = Results.IndexOf(entry);
                if (index <= 0) return;
                RecalculateFromIndex(index);
                ChartPoints = CreatePointCollection(Results.Select(r => (r.Year, r.AdjustedDemand)).ToList());
            }
        }

        private void RecalculateFromIndex(int index)
        {
            int forecastStartIndex = HistoricalData.Count;
            int lastHistYear = forecastStartIndex > 0 ? HistoricalData[^1].Year : 0;
            for (int i = index; i < Results.Count; i++)
            {
                double baseDemand = Results[i - 1].Demand;
                double rate = Results[i].GrowthRate / 100.0;
                Results[i].Demand = baseDemand * (1 + rate);

                double t = Results[i].Year <= lastHistYear
                    ? 0
                    : (ForecastYears <= 1 ? 1 : (i - forecastStartIndex) / (double)(ForecastYears - 1));

                double industrialPercent = Results[i].Year <= lastHistYear
                    ? CurrentIndustrialPercent
                    : CurrentIndustrialPercent + (FutureIndustrialPercent - CurrentIndustrialPercent) * t;
                double residentialPercent = Results[i].Year <= lastHistYear
                    ? CurrentResidentialPercent
                    : CurrentResidentialPercent + (FutureResidentialPercent - CurrentResidentialPercent) * t;
                double commercialPercent = Results[i].Year <= lastHistYear
                    ? CurrentCommercialPercent
                    : CurrentCommercialPercent + (FutureCommercialPercent - CurrentCommercialPercent) * t;
                double agriculturalPercent = Results[i].Year <= lastHistYear
                    ? CurrentAgriculturalPercent
                    : CurrentAgriculturalPercent + (FutureAgriculturalPercent - CurrentAgriculturalPercent) * t;

                Results[i].IndustrialDemand = Results[i].Demand * industrialPercent / 100.0;
                Results[i].ResidentialDemand = Results[i].Demand * residentialPercent / 100.0;
                Results[i].CommercialDemand = Results[i].Demand * commercialPercent / 100.0;
                Results[i].AgriculturalDemand = Results[i].Demand * agriculturalPercent / 100.0;
                Results[i].AdjustedDemand = Results[i].Demand * (1 + SystemLossesPercent / 100.0) * (1 - SystemImprovementsPercent / 100.0);
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
                Services.ExcelExporter.ExportWaterDemand(Results, dlg.FileName);
            }
        }
    }
}

