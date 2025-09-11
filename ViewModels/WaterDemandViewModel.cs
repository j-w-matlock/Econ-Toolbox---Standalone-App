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
                foreach (var p in forecast.Data)
                    Results.Add(new DemandEntry { Year = p.Year, Demand = p.Demand });

                ChartPoints = CreatePointCollection(forecast.Data);
                Explanation = forecast.Explanation;
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

