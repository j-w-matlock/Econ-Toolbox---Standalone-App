using EconToolbox.Desktop.Models;
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
        private double _userDays;
        private double _visitation = 1.0;
        private string _result = string.Empty;
        private PointCollection _chartPoints = new();

        public ObservableCollection<PointValueRow> Table { get; } = UdvModel.CreateDefaultTable();

        public ObservableCollection<string> RecreationTypes { get; } = new(new[] { "General", "Specialized" });
        public ObservableCollection<string> ActivityTypes { get; } = new();

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

        public double UserDays
        {
            get => _userDays;
            set { _userDays = value; OnPropertyChanged(); }
        }

        public double Visitation
        {
            get => _visitation;
            set { _visitation = value; OnPropertyChanged(); }
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

        private void Compute()
        {
            double benefit = UdvModel.ComputeBenefit(UnitDayValue, UserDays, Visitation);
            Result = $"Annual Recreation Benefit: {benefit:F2}";
        }
    }
}
