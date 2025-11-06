using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using EconToolbox.Desktop.Models;

namespace EconToolbox.Desktop.ViewModels;

public class RecreationCapacityViewModel : BaseViewModel
{
    private double _campingCampsites = 80;
    public double CampingCampsites
    {
        get => _campingCampsites;
        set
        {
            double clamped = ClampNonNegative(value);
            if (Math.Abs(_campingCampsites - clamped) < 0.0001)
            {
                return;
            }

            _campingCampsites = clamped;
            OnPropertyChanged();
            Recalculate();
        }
    }

    private double _campingAverageGroupSize = 4.5;
    public double CampingAverageGroupSize
    {
        get => _campingAverageGroupSize;
        set
        {
            double clamped = ClampPositive(value);
            if (Math.Abs(_campingAverageGroupSize - clamped) < 0.0001)
            {
                return;
            }

            _campingAverageGroupSize = clamped;
            OnPropertyChanged();
            Recalculate();
        }
    }

    private double _campingDailyTurnover = 1.1;
    public double CampingDailyTurnover
    {
        get => _campingDailyTurnover;
        set
        {
            double clamped = ClampNonNegative(value);
            if (Math.Abs(_campingDailyTurnover - clamped) < 0.0001)
            {
                return;
            }

            _campingDailyTurnover = clamped;
            OnPropertyChanged();
            Recalculate();
        }
    }

    private double _campingSeasonLengthDays = 180;
    public double CampingSeasonLengthDays
    {
        get => _campingSeasonLengthDays;
        set
        {
            double clamped = ClampNonNegative(value);
            if (Math.Abs(_campingSeasonLengthDays - clamped) < 0.0001)
            {
                return;
            }

            _campingSeasonLengthDays = clamped;
            OnPropertyChanged();
            Recalculate();
        }
    }

    private double _fishingAccessibleShorelineFeet = 2400;
    public double FishingAccessibleShorelineFeet
    {
        get => _fishingAccessibleShorelineFeet;
        set
        {
            double clamped = ClampNonNegative(value);
            if (Math.Abs(_fishingAccessibleShorelineFeet - clamped) < 0.0001)
            {
                return;
            }

            _fishingAccessibleShorelineFeet = clamped;
            OnPropertyChanged();
            Recalculate();
        }
    }

    private double _fishingSpacingFeet = 50;
    public double FishingSpacingFeet
    {
        get => _fishingSpacingFeet;
        set
        {
            double clamped = ClampPositive(value);
            if (Math.Abs(_fishingSpacingFeet - clamped) < 0.0001)
            {
                return;
            }

            _fishingSpacingFeet = clamped;
            OnPropertyChanged();
            Recalculate();
        }
    }

    private double _fishingAverageGroupSize = 1.5;
    public double FishingAverageGroupSize
    {
        get => _fishingAverageGroupSize;
        set
        {
            double clamped = ClampPositive(value);
            if (Math.Abs(_fishingAverageGroupSize - clamped) < 0.0001)
            {
                return;
            }

            _fishingAverageGroupSize = clamped;
            OnPropertyChanged();
            Recalculate();
        }
    }

    private double _fishingDailyTurnover = 1.3;
    public double FishingDailyTurnover
    {
        get => _fishingDailyTurnover;
        set
        {
            double clamped = ClampNonNegative(value);
            if (Math.Abs(_fishingDailyTurnover - clamped) < 0.0001)
            {
                return;
            }

            _fishingDailyTurnover = clamped;
            OnPropertyChanged();
            Recalculate();
        }
    }

    private double _fishingSeasonLengthDays = 150;
    public double FishingSeasonLengthDays
    {
        get => _fishingSeasonLengthDays;
        set
        {
            double clamped = ClampNonNegative(value);
            if (Math.Abs(_fishingSeasonLengthDays - clamped) < 0.0001)
            {
                return;
            }

            _fishingSeasonLengthDays = clamped;
            OnPropertyChanged();
            Recalculate();
        }
    }

    private double _boatingWaterSurfaceAcres = 650;
    public double BoatingWaterSurfaceAcres
    {
        get => _boatingWaterSurfaceAcres;
        set
        {
            double clamped = ClampNonNegative(value);
            if (Math.Abs(_boatingWaterSurfaceAcres - clamped) < 0.0001)
            {
                return;
            }

            _boatingWaterSurfaceAcres = clamped;
            OnPropertyChanged();
            Recalculate();
        }
    }

    private double _boatingAcresPerVessel = 30;
    public double BoatingAcresPerVessel
    {
        get => _boatingAcresPerVessel;
        set
        {
            double clamped = ClampPositive(value);
            if (Math.Abs(_boatingAcresPerVessel - clamped) < 0.0001)
            {
                return;
            }

            _boatingAcresPerVessel = clamped;
            OnPropertyChanged();
            Recalculate();
        }
    }

    private double _boatingPersonsPerVessel = 3.0;
    public double BoatingPersonsPerVessel
    {
        get => _boatingPersonsPerVessel;
        set
        {
            double clamped = ClampPositive(value);
            if (Math.Abs(_boatingPersonsPerVessel - clamped) < 0.0001)
            {
                return;
            }

            _boatingPersonsPerVessel = clamped;
            OnPropertyChanged();
            Recalculate();
        }
    }

    private double _boatingDailyTurnover = 1.6;
    public double BoatingDailyTurnover
    {
        get => _boatingDailyTurnover;
        set
        {
            double clamped = ClampNonNegative(value);
            if (Math.Abs(_boatingDailyTurnover - clamped) < 0.0001)
            {
                return;
            }

            _boatingDailyTurnover = clamped;
            OnPropertyChanged();
            Recalculate();
        }
    }

    private double _boatingSeasonLengthDays = 200;
    public double BoatingSeasonLengthDays
    {
        get => _boatingSeasonLengthDays;
        set
        {
            double clamped = ClampNonNegative(value);
            if (Math.Abs(_boatingSeasonLengthDays - clamped) < 0.0001)
            {
                return;
            }

            _boatingSeasonLengthDays = clamped;
            OnPropertyChanged();
            Recalculate();
        }
    }

    private IReadOnlyList<RecreationCapacityActivity> _activitySummaries = Array.Empty<RecreationCapacityActivity>();
    public IReadOnlyList<RecreationCapacityActivity> ActivitySummaries
    {
        get => _activitySummaries;
        private set
        {
            _activitySummaries = value;
            OnPropertyChanged();
        }
    }

    private string _summary = string.Empty;
    public string Summary
    {
        get => _summary;
        private set
        {
            _summary = value;
            OnPropertyChanged();
        }
    }

    public string GuidanceNotes =>
        "Baseline multipliers align with the U.S. Army Corps of Engineers Recreation Facility and Customer Service Standards (EP 1130-2-550) " +
        "and the Planning Guidance Notebook design day concepts. Adjust the occupancy, spacing, and turnover factors when more detailed " +
        "site studies are available.";

    public double CampingPeopleAtOneTime => Math.Round(CampingCampsites * CampingAverageGroupSize, 2);
    public double CampingDailyCapacity => Math.Round(CampingPeopleAtOneTime * CampingDailyTurnover, 2);
    public double CampingSeasonCapacity => Math.Round(CampingDailyCapacity * CampingSeasonLengthDays, 2);

    public double FishingPeopleAtOneTime => Math.Round((FishingAccessibleShorelineFeet / Math.Max(FishingSpacingFeet, 0.1)) * FishingAverageGroupSize, 2);
    public double FishingDailyCapacity => Math.Round(FishingPeopleAtOneTime * FishingDailyTurnover, 2);
    public double FishingSeasonCapacity => Math.Round(FishingDailyCapacity * FishingSeasonLengthDays, 2);

    public double BoatingPeopleAtOneTime => Math.Round((BoatingWaterSurfaceAcres / Math.Max(BoatingAcresPerVessel, 0.1)) * BoatingPersonsPerVessel, 2);
    public double BoatingDailyCapacity => Math.Round(BoatingPeopleAtOneTime * BoatingDailyTurnover, 2);
    public double BoatingSeasonCapacity => Math.Round(BoatingDailyCapacity * BoatingSeasonLengthDays, 2);

    public double TotalPeopleAtOneTime => Math.Round(CampingPeopleAtOneTime + FishingPeopleAtOneTime + BoatingPeopleAtOneTime, 2);
    public double TotalDailyCapacity => Math.Round(CampingDailyCapacity + FishingDailyCapacity + BoatingDailyCapacity, 2);
    public double TotalSeasonCapacity => Math.Round(CampingSeasonCapacity + FishingSeasonCapacity + BoatingSeasonCapacity, 2);

    public IRelayCommand ComputeCommand { get; }

    public RecreationCapacityViewModel()
    {
        ComputeCommand = new RelayCommand(Recalculate);
        Recalculate();
    }

    private void Recalculate()
    {
        var activities = new List<RecreationCapacityActivity>
        {
            new(
                "Camping",
                "Developed campsites",
                CampingCampsites,
                "sites",
                CampingAverageGroupSize,
                CampingDailyTurnover,
                CampingSeasonLengthDays,
                CampingPeopleAtOneTime,
                CampingDailyCapacity,
                CampingSeasonCapacity,
                "5 persons per site design occupancy; turnover reflects Corps campground handbook guidance."),
            new(
                "Shoreline Fishing",
                "Accessible shoreline",
                FishingAccessibleShorelineFeet,
                "feet",
                FishingAverageGroupSize / Math.Max(FishingSpacingFeet, 0.1),
                FishingDailyTurnover,
                FishingSeasonLengthDays,
                FishingPeopleAtOneTime,
                FishingDailyCapacity,
                FishingSeasonCapacity,
                "One angling position per 50 feet of shoreline with typical 1.5 person parties."),
            new(
                "Boating",
                "Usable water surface",
                BoatingWaterSurfaceAcres,
                "acres",
                BoatingPersonsPerVessel / Math.Max(BoatingAcresPerVessel, 0.1),
                BoatingDailyTurnover,
                BoatingSeasonLengthDays,
                BoatingPeopleAtOneTime,
                BoatingDailyCapacity,
                BoatingSeasonCapacity,
                "Design day density of 30 surface acres per powered craft with 3 persons per vessel."),
        };

        ActivitySummaries = new ReadOnlyCollection<RecreationCapacityActivity>(activities);

        NotifyCapacityChanged();

        Summary = TotalDailyCapacity > 0
            ? $"Design day capacity totals {TotalDailyCapacity:N0} user days per day and {TotalSeasonCapacity:N0} seasonal user days."
            : "Enter facility inventory and policy multipliers to generate capacity outputs.";
    }

    private void NotifyCapacityChanged()
    {
        OnPropertyChanged(nameof(CampingPeopleAtOneTime));
        OnPropertyChanged(nameof(CampingDailyCapacity));
        OnPropertyChanged(nameof(CampingSeasonCapacity));
        OnPropertyChanged(nameof(FishingPeopleAtOneTime));
        OnPropertyChanged(nameof(FishingDailyCapacity));
        OnPropertyChanged(nameof(FishingSeasonCapacity));
        OnPropertyChanged(nameof(BoatingPeopleAtOneTime));
        OnPropertyChanged(nameof(BoatingDailyCapacity));
        OnPropertyChanged(nameof(BoatingSeasonCapacity));
        OnPropertyChanged(nameof(TotalPeopleAtOneTime));
        OnPropertyChanged(nameof(TotalDailyCapacity));
        OnPropertyChanged(nameof(TotalSeasonCapacity));
    }

    private static double ClampNonNegative(double value) => double.IsFinite(value) ? Math.Max(0, value) : 0;
    private static double ClampPositive(double value) => double.IsFinite(value) ? Math.Max(0.1, value) : 0.1;
}
