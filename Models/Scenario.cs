using System;
using System.Collections.ObjectModel;
using System.Windows.Media;
using EconToolbox.Desktop.Themes;

namespace EconToolbox.Desktop.Models
{
    /// <summary>
    /// Represents a set of water demand forecast parameters and results.
    /// </summary>
    public class Scenario : ObservableObject
    {
        private string _name = string.Empty;
        public string Name
        {
            get => _name;
            set
            {
                if (_name == value)
                    return;
                _name = value;
                OnPropertyChanged();
            }
        }

        private string _description = string.Empty;
        public string Description
        {
            get => _description;
            set
            {
                if (_description == value)
                    return;
                _description = value;
                OnPropertyChanged();
            }
        }

        private int _baseYear;
        public int BaseYear
        {
            get => _baseYear;
            set
            {
                if (_baseYear == value)
                    return;
                _baseYear = value;
                OnPropertyChanged();
            }
        }

        private double _basePopulation;
        public double BasePopulation
        {
            get => _basePopulation;
            set
            {
                if (Math.Abs(_basePopulation - value) < 0.0001)
                    return;
                _basePopulation = value;
                OnPropertyChanged();
            }
        }

        private double _basePerCapitaDemand;
        public double BasePerCapitaDemand
        {
            get => _basePerCapitaDemand;
            set
            {
                if (Math.Abs(_basePerCapitaDemand - value) < 0.0001)
                    return;
                _basePerCapitaDemand = value;
                OnPropertyChanged();
            }
        }

        private double _populationGrowthRate;
        public double PopulationGrowthRate
        {
            get => _populationGrowthRate;
            set
            {
                if (Math.Abs(_populationGrowthRate - value) < 0.0001)
                    return;
                _populationGrowthRate = value;
                OnPropertyChanged();
            }
        }

        private double _perCapitaDemandChangeRate;
        public double PerCapitaDemandChangeRate
        {
            get => _perCapitaDemandChangeRate;
            set
            {
                if (Math.Abs(_perCapitaDemandChangeRate - value) < 0.0001)
                    return;
                _perCapitaDemandChangeRate = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Percentage shares for each demand sector. The last entry is treated
        /// as the residual category that is auto-calculated to total 100%.
        /// </summary>
        public ObservableCollection<SectorShare> Sectors { get; } = new()
        {
            new SectorShare { Name = "Industrial" },
            new SectorShare { Name = "Residential" },
            new SectorShare { Name = "Commercial" },
            new SectorShare { Name = "Agricultural", IsResidual = true }
        };
        private double _systemImprovementsPercent;
        public double SystemImprovementsPercent
        {
            get => _systemImprovementsPercent;
            set
            {
                if (Math.Abs(_systemImprovementsPercent - value) < 0.0001)
                    return;
                _systemImprovementsPercent = value;
                OnPropertyChanged();
            }
        }

        private double _systemLossesPercent;
        public double SystemLossesPercent
        {
            get => _systemLossesPercent;
            set
            {
                if (Math.Abs(_systemLossesPercent - value) < 0.0001)
                    return;
                _systemLossesPercent = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<DemandEntry> Results { get; } = new();

        private PointCollection _chartPoints = new();
        public PointCollection ChartPoints
        {
            get => _chartPoints;
            set { _chartPoints = value; OnPropertyChanged(); }
        }

        private Brush _lineBrush = ThemeResourceHelper.GetBrush("App.Chart.Series1", Brushes.Blue);
        public Brush LineBrush
        {
            get => _lineBrush;
            set
            {
                if (_lineBrush == value)
                    return;
                _lineBrush = value;
                OnPropertyChanged();
            }
        }
    }
}
