using System;
using System.Collections.Generic;
using System.Windows.Input;

namespace EconToolbox.Desktop.ViewModels;

public class TrafficDelayAnalysisViewModel : DiagnosticViewModelBase, IComputeModule
{
    private const double HoursPerWorkYear = 2080d;

    private double _durationOfFloodingDays = 21;
    private double _trafficCountForFloodingPeriod = 1180;
    private double _originalRouteMiles = 29.9;
    private double _alternativeRouteMiles = 44.6;
    private double _totalPassengers = 1.5;
    private double _operatingCostPerMile = 0.33;
    private double _medianHouseholdIncome = 66250;

    public ICommand ComputeCommand { get; }

    public TrafficDelayAnalysisViewModel()
    {
        ComputeCommand = new RelayCommand(Recalculate);
        Recalculate();
    }

    public double DurationOfFloodingDays
    {
        get => _durationOfFloodingDays;
        set
        {
            if (SetNumericField(ref _durationOfFloodingDays, value))
            {
                Recalculate();
            }
        }
    }

    public double TrafficCountForFloodingPeriod
    {
        get => _trafficCountForFloodingPeriod;
        set
        {
            if (SetNumericField(ref _trafficCountForFloodingPeriod, value))
            {
                Recalculate();
            }
        }
    }

    public double OriginalRouteMiles
    {
        get => _originalRouteMiles;
        set
        {
            if (SetNumericField(ref _originalRouteMiles, value))
            {
                Recalculate();
            }
        }
    }

    public double AlternativeRouteMiles
    {
        get => _alternativeRouteMiles;
        set
        {
            if (SetNumericField(ref _alternativeRouteMiles, value))
            {
                Recalculate();
            }
        }
    }

    public double TotalPassengers
    {
        get => _totalPassengers;
        set
        {
            if (SetNumericField(ref _totalPassengers, value))
            {
                Recalculate();
            }
        }
    }

    public double OperatingCostPerMile
    {
        get => _operatingCostPerMile;
        set
        {
            if (SetNumericField(ref _operatingCostPerMile, value))
            {
                Recalculate();
            }
        }
    }

    public double MedianHouseholdIncome
    {
        get => _medianHouseholdIncome;
        set
        {
            if (SetNumericField(ref _medianHouseholdIncome, value))
            {
                Recalculate();
            }
        }
    }

    public double HourlyIncome { get; private set; }
    public double AdditionalMilesPerVehicle { get; private set; }
    public double TotalAdditionalMileage { get; private set; }
    public double AdditionalOperatingCostsPerDay { get; private set; }
    public double TotalAdditionalDetourCosts { get; private set; }

    public double LowTimeSavingsWorkTripsValue { get; private set; }
    public double LowTimeSavingsSocialRecreationValue { get; private set; }
    public double LowTimeSavingsOtherValue { get; private set; }

    public double MediumTimeSavingsWorkTripsValue { get; private set; }
    public double MediumTimeSavingsSocialRecreationValue { get; private set; }
    public double MediumTimeSavingsOtherValue { get; private set; }

    public double HighTimeSavingsWorkTripsValue { get; private set; }
    public double HighTimeSavingsSocialRecreationValue { get; private set; }
    public double HighTimeSavingsOtherValue { get; private set; }

    public double DelayImpactsPerDay { get; private set; }
    public double TotalDelayImpacts { get; private set; }
    public double TotalTrafficImpacts { get; private set; }

    protected override IEnumerable<DiagnosticItem> BuildDiagnostics()
    {
        if (DurationOfFloodingDays < 0)
        {
            yield return new DiagnosticItem(DiagnosticLevel.Warning, "Duration is negative", "Duration of flooding should be zero or greater days.");
        }

        if (TrafficCountForFloodingPeriod < 0)
        {
            yield return new DiagnosticItem(DiagnosticLevel.Warning, "Traffic count is negative", "Traffic count for the flooding period should be zero or greater.");
        }

        if (AlternativeRouteMiles < OriginalRouteMiles)
        {
            yield return new DiagnosticItem(DiagnosticLevel.Info, "Alternative route is shorter", "The alternative route is shorter than the original route, resulting in negative detour mileage.");
        }

        yield return new DiagnosticItem(
            DiagnosticLevel.Info,
            "Traffic delay impacts updated",
            $"Total traffic impacts are currently {TotalTrafficImpacts:C0} for the selected scenario.");
    }

    private void Recalculate()
    {
        AdditionalMilesPerVehicle = AlternativeRouteMiles - OriginalRouteMiles;
        TotalAdditionalMileage = TrafficCountForFloodingPeriod * AdditionalMilesPerVehicle;
        AdditionalOperatingCostsPerDay = TotalAdditionalMileage * OperatingCostPerMile;
        TotalAdditionalDetourCosts = AdditionalOperatingCostsPerDay * DurationOfFloodingDays;

        HourlyIncome = MedianHouseholdIncome / HoursPerWorkYear;

        LowTimeSavingsWorkTripsValue = HourlyIncome * 0.064;
        LowTimeSavingsSocialRecreationValue = HourlyIncome * 0.013;
        LowTimeSavingsOtherValue = LowTimeSavingsSocialRecreationValue * 0.001;

        MediumTimeSavingsWorkTripsValue = HourlyIncome * 0.322;
        MediumTimeSavingsSocialRecreationValue = HourlyIncome * 0.231;
        MediumTimeSavingsOtherValue = HourlyIncome * 0.145;

        HighTimeSavingsWorkTripsValue = HourlyIncome * 0.538;
        HighTimeSavingsSocialRecreationValue = HourlyIncome * 0.6;
        HighTimeSavingsOtherValue = HourlyIncome * 0.645;

        DelayImpactsPerDay = HighTimeSavingsWorkTripsValue * TrafficCountForFloodingPeriod * TotalPassengers;
        TotalDelayImpacts = DelayImpactsPerDay * DurationOfFloodingDays;
        TotalTrafficImpacts = TotalDelayImpacts + TotalAdditionalDetourCosts;

        NotifyCalculatedProperties();
        RefreshDiagnostics();
    }

    private void NotifyCalculatedProperties()
    {
        OnPropertyChanged(nameof(HourlyIncome));
        OnPropertyChanged(nameof(AdditionalMilesPerVehicle));
        OnPropertyChanged(nameof(TotalAdditionalMileage));
        OnPropertyChanged(nameof(AdditionalOperatingCostsPerDay));
        OnPropertyChanged(nameof(TotalAdditionalDetourCosts));
        OnPropertyChanged(nameof(LowTimeSavingsWorkTripsValue));
        OnPropertyChanged(nameof(LowTimeSavingsSocialRecreationValue));
        OnPropertyChanged(nameof(LowTimeSavingsOtherValue));
        OnPropertyChanged(nameof(MediumTimeSavingsWorkTripsValue));
        OnPropertyChanged(nameof(MediumTimeSavingsSocialRecreationValue));
        OnPropertyChanged(nameof(MediumTimeSavingsOtherValue));
        OnPropertyChanged(nameof(HighTimeSavingsWorkTripsValue));
        OnPropertyChanged(nameof(HighTimeSavingsSocialRecreationValue));
        OnPropertyChanged(nameof(HighTimeSavingsOtherValue));
        OnPropertyChanged(nameof(DelayImpactsPerDay));
        OnPropertyChanged(nameof(TotalDelayImpacts));
        OnPropertyChanged(nameof(TotalTrafficImpacts));
    }

    private bool SetNumericField(ref double field, double value)
    {
        if (Math.Abs(field - value) < 0.0001)
        {
            return false;
        }

        field = value;
        OnPropertyChanged();
        return true;
    }
}
