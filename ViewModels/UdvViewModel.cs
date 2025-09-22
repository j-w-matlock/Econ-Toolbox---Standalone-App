using EconToolbox.Desktop.Models;
using System;
using System.Collections.ObjectModel;
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
        private string _visitationPeriod = "Daily";
        private double _totalUserDays;
        private string _result = string.Empty;
        private PointCollection _chartPoints = new();

        public ObservableCollection<PointValueRow> Table { get; } = UdvModel.CreateDefaultTable();

        public ObservableCollection<string> RecreationTypes { get; } = new(new[] { "General", "Specialized" });
        public ObservableCollection<string> ActivityTypes { get; } = new();
        public ObservableCollection<string> VisitationPeriods { get; } = new(new[] { "Daily", "Monthly", "Total Season" });

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
                    OnPropertyChanged(nameof(VisitationUnitLabel));
                    RecalculateUserDays();
                }
            }
        }

        public string VisitationUnitLabel => VisitationPeriod switch
        {
            "Daily" => "per day",
            "Monthly" => "per month",
            "Total Season" => "total",
            _ => "per day",
        };

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

        public string Result
        {
            get => _result;
            set { _result = value; OnPropertyChanged(); }
        }

        public PointCollection ChartPoints
        {
            get => _chartPoints;
            private set { _chartPoints = value; OnPropertyChanged(); }
        }

        public ICommand ComputeCommand { get; }

        public UdvViewModel()
        {
            ComputeCommand = new RelayCommand(Compute);
            UpdateActivityTypes();
            UpdateUnitDayValue();
            UpdateChart();
            RecalculateUserDays();
            foreach (var row in Table)
            {
                row.PropertyChanged += (_, __) =>
                {
                    UpdateUnitDayValue();
                    UpdateChart();
                };
            }
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
            double total = VisitationPeriod switch
            {
                "Daily" => SeasonDays * VisitationInput,
                "Monthly" => (SeasonDays / averageDaysPerMonth) * VisitationInput,
                "Total Season" => VisitationInput,
                _ => SeasonDays * VisitationInput,
            };
            TotalUserDays = double.IsFinite(total) ? Math.Max(0.0, total) : 0.0;
        }

        private void Compute()
        {
            RecalculateUserDays();
            double benefit = UdvModel.ComputeBenefit(UnitDayValue, TotalUserDays);
            Result = $"Season Recreation Benefit: {benefit:F2}";
        }
    }
}
