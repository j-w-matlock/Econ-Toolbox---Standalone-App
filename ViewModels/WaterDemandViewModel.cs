using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using System.Windows.Media;
using EconToolbox.Desktop.Models;

namespace EconToolbox.Desktop.ViewModels
{
    /// <summary>
    /// View model allowing users to enter historical demand data and
    /// project future demand using either linear regression or growth
    /// rate methods. Results can be displayed in a simple chart.
    /// </summary>
    public class WaterDemandViewModel : BaseViewModel
    {
        private string _historicalData = string.Empty;
        private int _forecastYears = 5;
        private bool _useGrowthRate;
        private ObservableCollection<DemandPoint> _results = new();
        private PointCollection _chartPoints = new();

        public string HistoricalData
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

        public ObservableCollection<DemandPoint> Results
        {
            get => _results;
            set { _results = value; OnPropertyChanged(); }
        }

        public PointCollection ChartPoints
        {
            get => _chartPoints;
            set { _chartPoints = value; OnPropertyChanged(); }
        }

        public ICommand ForecastCommand { get; }

        public WaterDemandViewModel()
        {
            ForecastCommand = new RelayCommand(Forecast);
        }

        private void Forecast()
        {
            try
            {
                var hist = ParseHistorical(HistoricalData);
                List<(int Year, double Demand)> forecast = UseGrowthRate
                    ? WaterDemandModel.GrowthRateForecast(hist, ForecastYears)
                    : WaterDemandModel.LinearRegressionForecast(hist, ForecastYears);

                Results = new ObservableCollection<DemandPoint>();
                foreach (var p in forecast)
                    Results.Add(new DemandPoint { Year = p.Year, Demand = p.Demand });

                ChartPoints = CreatePointCollection(forecast);
            }
            catch
            {
                Results = new ObservableCollection<DemandPoint>();
                ChartPoints = new PointCollection();
            }
        }

        private static List<(int Year, double Demand)> ParseHistorical(string text)
        {
            List<(int Year, double Demand)> list = new();
            if (string.IsNullOrWhiteSpace(text))
                return list;

            var items = text.Split(',', StringSplitOptions.RemoveEmptyEntries);
            foreach (var item in items)
            {
                var parts = item.Split(':', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2 &&
                    int.TryParse(parts[0].Trim(), out int year) &&
                    double.TryParse(parts[1].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out double demand))
                {
                    list.Add((year, demand));
                }
            }
            list.Sort((a, b) => a.Year.CompareTo(b.Year));
            return list;
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

        public class DemandPoint
        {
            public int Year { get; set; }
            public double Demand { get; set; }
        }
    }
}

