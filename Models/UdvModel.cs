using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace EconToolbox.Desktop.Models
{
    public class PointValueRow : INotifyPropertyChanged
    {
        private int _points;
        private double _generalRecreation;
        private double _generalFishingHunting;
        private double _specializedFishingHunting;
        private double _specializedRecreation;

        public int Points
        {
            get => _points;
            set { _points = value; OnPropertyChanged(nameof(Points)); }
        }

        public double GeneralRecreation
        {
            get => _generalRecreation;
            set { _generalRecreation = value; OnPropertyChanged(nameof(GeneralRecreation)); }
        }

        public double GeneralFishingHunting
        {
            get => _generalFishingHunting;
            set { _generalFishingHunting = value; OnPropertyChanged(nameof(GeneralFishingHunting)); }
        }

        public double SpecializedFishingHunting
        {
            get => _specializedFishingHunting;
            set { _specializedFishingHunting = value; OnPropertyChanged(nameof(SpecializedFishingHunting)); }
        }

        public double SpecializedRecreation
        {
            get => _specializedRecreation;
            set { _specializedRecreation = value; OnPropertyChanged(nameof(SpecializedRecreation)); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public static class UdvModel
    {
        public static ObservableCollection<PointValueRow> CreateDefaultTable()
        {
            return new ObservableCollection<PointValueRow>
            {
                new PointValueRow { Points = 0, GeneralRecreation = 4.87, GeneralFishingHunting = 7.00, SpecializedFishingHunting = 34.09, SpecializedRecreation = 19.79 },
                new PointValueRow { Points = 10, GeneralRecreation = 5.78, GeneralFishingHunting = 7.91, SpecializedFishingHunting = 35.01, SpecializedRecreation = 21.00 },
                new PointValueRow { Points = 20, GeneralRecreation = 6.39, GeneralFishingHunting = 8.52, SpecializedFishingHunting = 35.62, SpecializedRecreation = 22.53 },
                new PointValueRow { Points = 30, GeneralRecreation = 7.31, GeneralFishingHunting = 9.44, SpecializedFishingHunting = 36.53, SpecializedRecreation = 24.35 },
                new PointValueRow { Points = 40, GeneralRecreation = 9.13, GeneralFishingHunting = 10.35, SpecializedFishingHunting = 37.44, SpecializedRecreation = 25.88 },
                new PointValueRow { Points = 50, GeneralRecreation = 10.35, GeneralFishingHunting = 11.26, SpecializedFishingHunting = 41.10, SpecializedRecreation = 29.22 },
                new PointValueRow { Points = 60, GeneralRecreation = 11.26, GeneralFishingHunting = 12.48, SpecializedFishingHunting = 44.75, SpecializedRecreation = 32.27 },
                new PointValueRow { Points = 70, GeneralRecreation = 11.87, GeneralFishingHunting = 13.09, SpecializedFishingHunting = 47.49, SpecializedRecreation = 38.97 },
                new PointValueRow { Points = 80, GeneralRecreation = 13.09, GeneralFishingHunting = 14.00, SpecializedFishingHunting = 51.14, SpecializedRecreation = 45.36 },
                new PointValueRow { Points = 90, GeneralRecreation = 14.00, GeneralFishingHunting = 14.31, SpecializedFishingHunting = 54.80, SpecializedRecreation = 51.75 },
                new PointValueRow { Points = 100, GeneralRecreation = 14.61, GeneralFishingHunting = 14.61, SpecializedFishingHunting = 57.84, SpecializedRecreation = 57.84 },
            };
        }

        public static double ComputeUnitDayValue(IEnumerable<PointValueRow> table, string recreationType, string activityType, double points)
        {
            string column = MapColumn(recreationType, activityType);
            var x = table.Select(r => (double)r.Points).ToArray();
            var y = table.Select(r => GetColumnValue(r, column)).ToArray();
            return Interpolate(x, y, points);
        }

        private static string MapColumn(string recreationType, string activityType)
        {
            return (recreationType, activityType) switch
            {
                ("General", "General Recreation") => "General Recreation",
                ("General", "Fishing and Hunting") => "General Fishing and Hunting",
                ("Specialized", "Fishing and Hunting") => "Specialized Fishing and Hunting",
                ("Specialized", "Other (e.g., Boating)") => "Specialized Recreation",
                _ => "General Recreation",
            };
        }

        private static double GetColumnValue(PointValueRow row, string column) => column switch
        {
            "General Recreation" => row.GeneralRecreation,
            "General Fishing and Hunting" => row.GeneralFishingHunting,
            "Specialized Fishing and Hunting" => row.SpecializedFishingHunting,
            "Specialized Recreation" => row.SpecializedRecreation,
            _ => row.GeneralRecreation,
        };

        private static double Interpolate(double[] x, double[] y, double point)
        {
            if (point <= x[0]) return y[0];
            if (point >= x[x.Length - 1]) return y[y.Length - 1];
            for (int i = 0; i < x.Length - 1; i++)
            {
                if (point >= x[i] && point <= x[i + 1])
                {
                    double t = (point - x[i]) / (x[i + 1] - x[i]);
                    return y[i] + t * (y[i + 1] - y[i]);
                }
            }
            return y[y.Length - 1];
        }

        public static double ComputeBenefit(double unitDayValue, double userDays, double visitation)
        {
            return unitDayValue * userDays * visitation;
        }
    }
}
