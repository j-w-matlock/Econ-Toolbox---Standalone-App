using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using System.Windows.Media;
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
        private double _systemImprovementsPercent;
        private double _systemLossesPercent;

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

        public ICommand ForecastCommand { get; }
        public ICommand ExportCommand { get; }
        public ICommand ComputeCommand { get; }

        public WaterDemandViewModel()
        {
            ForecastCommand = new RelayCommand(Forecast);
            ExportCommand = new RelayCommand(Export);
            ComputeCommand = ForecastCommand;
        }

        private void Forecast()
        {
            try
            {
                var hist = HistoricalData
                    .Select(h => (h.Year, h.Demand))
                    .ToList();
                var forecast = UseGrowthRate
                    ? WaterDemandModel.GrowthRateForecast(hist, ForecastYears)
                    : WaterDemandModel.LinearRegressionForecast(hist, ForecastYears);

                Results = new ObservableCollection<DemandEntry>();
                int lastHistYear = hist.Count > 0 ? hist[^1].Year : 0;
                int forecastCount = forecast.Data.Count - hist.Count;
                int idx = 0;
                foreach (var p in forecast.Data)
                {
                    double industrialPercent;
                    if (p.Year <= lastHistYear)
                    {
                        industrialPercent = CurrentIndustrialPercent;
                    }
                    else
                    {
                        double t = forecastCount <= 1 ? 1 : (double)idx / (forecastCount - 1);
                        industrialPercent = CurrentIndustrialPercent + (FutureIndustrialPercent - CurrentIndustrialPercent) * t;
                        idx++;
                    }
                    double industrialDemand = p.Demand * industrialPercent / 100.0;
                    double adjusted = p.Demand * (1 + SystemLossesPercent / 100.0) * (1 - SystemImprovementsPercent / 100.0);
                    Results.Add(new DemandEntry { Year = p.Year, Demand = p.Demand, IndustrialDemand = industrialDemand, AdjustedDemand = adjusted });
                }

                ChartPoints = CreatePointCollection(Results.Select(r => (r.Year, r.AdjustedDemand)).ToList());
                Explanation = forecast.Explanation + $" Industrial share interpolated from {CurrentIndustrialPercent:F1}% to {FutureIndustrialPercent:F1}% with {SystemImprovementsPercent:F1}% improvements and {SystemLossesPercent:F1}% losses.";
            }
            catch
            {
                Results = new ObservableCollection<DemandEntry>();
                ChartPoints = new PointCollection();
                Explanation = string.Empty;
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

