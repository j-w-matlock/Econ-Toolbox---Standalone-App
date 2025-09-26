using EconToolbox.Desktop.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace EconToolbox.Desktop.ViewModels
{
    public class UdvViewModel : BaseViewModel
    {
        private string _recreationType = "General";
        private string _activityType = "General Recreation";
        private double _points;
        private double _unitDayValue;
        private double _seasonDays = 120.0;
        private double _visitationInput;
        private string _visitationPeriod = "Per Day";
        private double _totalUserDays;
        private double _annualRecreationBenefit;
        private PointCollection _chartPoints = new();
        private double? _historicalMedianVisitation;
        private int _historicalObservationCount;
        private string _historicalDataError = string.Empty;

        public ObservableCollection<PointValueRow> Table { get; } = UdvModel.CreateDefaultTable();
        public ObservableCollection<HistoricalVisitationRow> HistoricalVisitationRows { get; } = new();

        public ObservableCollection<string> RecreationTypes { get; } = new(new[] { "General", "Specialized" });
        public ObservableCollection<string> ActivityTypes { get; } = new();
        public ObservableCollection<string> VisitationPeriods { get; } = new(new[] { "Per Day", "Per Month", "Per Year" });

        public string RecreationType
        {
            get => _recreationType;
            set
            {
                if (_recreationType != value)
                {
                    _recreationType = value;
                    OnPropertyChanged();
                    UpdateActivityTypes();
                    UpdateUnitDayValue();
                    UpdateChart();
                }
            }
        }

        public string ActivityType
        {
            get => _activityType;
            set
            {
                if (_activityType != value)
                {
                    _activityType = value;
                    OnPropertyChanged();
                    UpdateUnitDayValue();
                    UpdateChart();
                }
            }
        }

        public double Points
        {
            get => _points;
            set
            {
                _points = value;
                OnPropertyChanged();
                UpdateUnitDayValue();
            }
        }

        public double UnitDayValue
        {
            get => _unitDayValue;
            private set { _unitDayValue = value; OnPropertyChanged(); }
        }

        public double SeasonDays
        {
            get => _seasonDays;
            set
            {
                if (_seasonDays != value)
                {
                    _seasonDays = value;
                    OnPropertyChanged();
                    RecalculateUserDays();
                }
            }
        }

        public double VisitationInput
        {
            get => _visitationInput;
            set
            {
                if (_visitationInput != value)
                {
                    _visitationInput = value;
                    OnPropertyChanged();
                    RecalculateUserDays();
                }
            }
        }

        public string VisitationPeriod
        {
            get => _visitationPeriod;
            set
            {
                if (_visitationPeriod != value)
                {
                    _visitationPeriod = value;
                    OnPropertyChanged();
                    RecalculateUserDays();
                }
            }
        }

        public double TotalUserDays
        {
            get => _totalUserDays;
            private set
            {
                if (_totalUserDays != value)
                {
                    _totalUserDays = value;
                    OnPropertyChanged();
                }
            }
        }

        public double AnnualRecreationBenefit
        {
            get => _annualRecreationBenefit;
            private set
            {
                if (_annualRecreationBenefit != value)
                {
                    _annualRecreationBenefit = value;
                    OnPropertyChanged();
                }
            }
        }

        public PointCollection ChartPoints
        {
            get => _chartPoints;
            private set { _chartPoints = value; OnPropertyChanged(); }
        }

        public double? HistoricalMedianVisitation
        {
            get => _historicalMedianVisitation;
            private set
            {
                if (_historicalMedianVisitation != value)
                {
                    _historicalMedianVisitation = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(HasHistoricalMedian));
                }
            }
        }

        public bool HasHistoricalMedian => HistoricalMedianVisitation.HasValue;

        public int HistoricalObservationCount
        {
            get => _historicalObservationCount;
            private set
            {
                if (_historicalObservationCount != value)
                {
                    _historicalObservationCount = value;
                    OnPropertyChanged();
                }
            }
        }

        public string HistoricalDataError
        {
            get => _historicalDataError;
            private set
            {
                if (_historicalDataError != value)
                {
                    _historicalDataError = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(HasHistoricalDataError));
                }
            }
        }

        public bool HasHistoricalDataError => !string.IsNullOrWhiteSpace(HistoricalDataError);

        public ICommand ComputeCommand { get; }
        public ICommand ComputeMedianCommand { get; }

        public UdvViewModel()
        {
            ComputeCommand = new RelayCommand(Compute);
            ComputeMedianCommand = new RelayCommand(ComputeMedianFromHistoricalData);
            UpdateActivityTypes();
            UpdateUnitDayValue();
            UpdateChart();
            RecalculateUserDays();
            InitializeHistoricalRows();
            HistoricalVisitationRows.CollectionChanged += HistoricalVisitationRows_CollectionChanged;
            foreach (var row in HistoricalVisitationRows)
            {
                row.PropertyChanged += HistoricalRow_PropertyChanged;
            }
            foreach (var row in Table)
            {
                row.PropertyChanged += (_, __) =>
                {
                    UpdateUnitDayValue();
                    UpdateChart();
                };
            }
        }

        private void InitializeHistoricalRows()
        {
            for (int i = 0; i < 5; i++)
            {
                HistoricalVisitationRows.Add(new HistoricalVisitationRow());
            }
        }

        private void HistoricalVisitationRows_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (HistoricalVisitationRow row in e.NewItems)
                {
                    row.PropertyChanged += HistoricalRow_PropertyChanged;
                }
            }

            if (e.OldItems != null)
            {
                foreach (HistoricalVisitationRow row in e.OldItems)
                {
                    row.PropertyChanged -= HistoricalRow_PropertyChanged;
                }
            }
        }

        private void HistoricalRow_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            HistoricalDataError = string.Empty;
        }

        private void UpdateActivityTypes()
        {
            ActivityTypes.Clear();
            if (RecreationType == "General")
            {
                ActivityTypes.Add("General Recreation");
                ActivityTypes.Add("Fishing and Hunting");
            }
            else
            {
                ActivityTypes.Add("Fishing and Hunting");
                ActivityTypes.Add("Other (e.g., Boating)");
            }
            if (!ActivityTypes.Contains(ActivityType))
            {
                ActivityType = ActivityTypes.First();
            }
        }

        private void UpdateUnitDayValue()
        {
            UnitDayValue = UdvModel.ComputeUnitDayValue(Table, RecreationType, ActivityType, Points);
        }

        private void UpdateChart()
        {
            string column = (RecreationType, ActivityType) switch
            {
                ("General", "General Recreation") => "General Recreation",
                ("General", "Fishing and Hunting") => "General Fishing and Hunting",
                ("Specialized", "Fishing and Hunting") => "Specialized Fishing and Hunting",
                ("Specialized", "Other (e.g., Boating)") => "Specialized Recreation",
                _ => "General Recreation",
            };
            double maxX = Table.Max(r => r.Points);
            double maxY = Table.Max(r => column switch
            {
                "General Recreation" => r.GeneralRecreation,
                "General Fishing and Hunting" => r.GeneralFishingHunting,
                "Specialized Fishing and Hunting" => r.SpecializedFishingHunting,
                "Specialized Recreation" => r.SpecializedRecreation,
                _ => r.GeneralRecreation,
            });
            double width = 200.0;
            double height = 120.0;
            var pc = new PointCollection();
            foreach (var row in Table)
            {
                double val = column switch
                {
                    "General Recreation" => row.GeneralRecreation,
                    "General Fishing and Hunting" => row.GeneralFishingHunting,
                    "Specialized Fishing and Hunting" => row.SpecializedFishingHunting,
                    "Specialized Recreation" => row.SpecializedRecreation,
                    _ => row.GeneralRecreation,
                };
                double x = row.Points / maxX * width;
                double y = height - (val / maxY * height);
                pc.Add(new Point(x, y));
            }
            ChartPoints = pc;
        }

        private void RecalculateUserDays()
        {
            const double averageDaysPerMonth = 30.4375;
            const double daysPerYear = 365.0;
            double total = VisitationPeriod switch
            {
                "Per Day" => SeasonDays * VisitationInput,
                "Per Month" => (SeasonDays / averageDaysPerMonth) * VisitationInput,
                "Per Year" => (SeasonDays / daysPerYear) * VisitationInput,
                _ => SeasonDays * VisitationInput,
            };
            TotalUserDays = double.IsFinite(total) ? Math.Max(0.0, total) : 0.0;
        }

        private void Compute()
        {
            RecalculateUserDays();
            double benefit = UdvModel.ComputeBenefit(UnitDayValue, TotalUserDays);
            AnnualRecreationBenefit = double.IsFinite(benefit) ? benefit : 0.0;
        }

        private void ComputeMedianFromHistoricalData()
        {
            HistoricalDataError = string.Empty;
            var (values, invalidCount) = ParseHistoricalEntries(HistoricalVisitationRows);

            if (values.Count == 0)
            {
                HistoricalMedianVisitation = null;
                HistoricalObservationCount = 0;
                HistoricalDataError = "Enter at least one numeric visitation value.";
                return;
            }

            values.Sort();
            double median = values.Count % 2 == 0
                ? (values[values.Count / 2 - 1] + values[values.Count / 2]) / 2.0
                : values[values.Count / 2];

            HistoricalMedianVisitation = median;
            HistoricalObservationCount = values.Count;

            if (invalidCount > 0)
            {
                HistoricalDataError = invalidCount == 1
                    ? "One entry could not be parsed and was ignored."
                    : $"{invalidCount} entries could not be parsed and were ignored.";
            }

            VisitationInput = median;
        }

        private static (List<double> values, int invalidCount) ParseHistoricalEntries(IEnumerable<HistoricalVisitationRow> rows)
        {
            var values = new List<double>();
            int invalidCount = 0;
            foreach (var row in rows)
            {
                string entry = row.VisitationText;
                if (string.IsNullOrWhiteSpace(entry))
                {
                    continue;
                }

                if (double.TryParse(entry, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out double value) ||
                    double.TryParse(entry, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out value))
                {
                    if (double.IsFinite(value))
                    {
                        values.Add(value);
                    }
                }
                else
                {
                    invalidCount++;
                }
            }

            return (values, invalidCount);
        }
    }
}
