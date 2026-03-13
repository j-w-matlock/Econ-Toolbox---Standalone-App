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
    private double _aepThatCausesDelay;
    private double _discountRatePercent;
    private double _analysisPeriodYears;

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

    public double AepThatCausesDelay
    {
        get => _aepThatCausesDelay;
        set
        {
            if (SetNumericField(ref _aepThatCausesDelay, value))
            {
                Recalculate();
            }
        }
    }

    public double DiscountRatePercent
    {
        get => _discountRatePercent;
        set
        {
            if (SetNumericField(ref _discountRatePercent, value))
            {
                Recalculate();
            }
        }
    }

    public double AnalysisPeriodYears
    {
        get => _analysisPeriodYears;
        set
        {
            if (SetNumericField(ref _analysisPeriodYears, value))
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
    public double AnnualizedTrafficImpacts { get; private set; }
    public double DiscountedTrafficImpactsOverAnalysisPeriod { get; private set; }

    public string AdditionalMilesPerVehicleFormula =>
        $"{AlternativeRouteMiles:N2} - {OriginalRouteMiles:N2} = {AdditionalMilesPerVehicle:N2} mi/vehicle";

    public string TotalAdditionalMileageFormula =>
        $"{TrafficCountForFloodingPeriod:N2} × {AdditionalMilesPerVehicle:N2} = {TotalAdditionalMileage:N2} mi";

    public string AdditionalOperatingCostsPerDayFormula =>
        $"{TotalAdditionalMileage:N2} × {OperatingCostPerMile:C2}/mi = {AdditionalOperatingCostsPerDay:C2}/day";

    public string TotalAdditionalDetourCostsFormula =>
        $"{AdditionalOperatingCostsPerDay:C2} × {DurationOfFloodingDays:N2} days = {TotalAdditionalDetourCosts:C2}";

    public string HourlyIncomeFormula =>
        $"{MedianHouseholdIncome:C2} ÷ {HoursPerWorkYear:N0} hrs = {HourlyIncome:C2}/hr";

    public string LowTimeSavingsWorkTripsValueFormula =>
        $"{HourlyIncome:C2} × 0.064 = {LowTimeSavingsWorkTripsValue:C2}";

    public string LowTimeSavingsSocialRecreationValueFormula =>
        $"{HourlyIncome:C2} × 0.013 = {LowTimeSavingsSocialRecreationValue:C2}";

    public string LowTimeSavingsOtherValueFormula =>
        $"{LowTimeSavingsSocialRecreationValue:C2} × 0.001 = {LowTimeSavingsOtherValue:C4}";

    public string MediumTimeSavingsWorkTripsValueFormula =>
        $"{HourlyIncome:C2} × 0.322 = {MediumTimeSavingsWorkTripsValue:C2}";

    public string MediumTimeSavingsSocialRecreationValueFormula =>
        $"{HourlyIncome:C2} × 0.231 = {MediumTimeSavingsSocialRecreationValue:C2}";

    public string MediumTimeSavingsOtherValueFormula =>
        $"{HourlyIncome:C2} × 0.145 = {MediumTimeSavingsOtherValue:C2}";

    public string HighTimeSavingsWorkTripsValueFormula =>
        $"{HourlyIncome:C2} × 0.538 = {HighTimeSavingsWorkTripsValue:C2}";

    public string HighTimeSavingsSocialRecreationValueFormula =>
        $"{HourlyIncome:C2} × 0.600 = {HighTimeSavingsSocialRecreationValue:C2}";

    public string HighTimeSavingsOtherValueFormula =>
        $"{HourlyIncome:C2} × 0.645 = {HighTimeSavingsOtherValue:C2}";

    public string DelayImpactsPerDayFormula =>
        $"{HighTimeSavingsWorkTripsValue:C2} × {TrafficCountForFloodingPeriod:N2} × {TotalPassengers:N2} = {DelayImpactsPerDay:C2}/day";

    public string TotalDelayImpactsFormula =>
        $"{DelayImpactsPerDay:C2} × {DurationOfFloodingDays:N2} days = {TotalDelayImpacts:C2}";

    public string TotalTrafficImpactsFormula =>
        $"{TotalDelayImpacts:C2} + {TotalAdditionalDetourCosts:C2} = {TotalTrafficImpacts:C2}";

    public string AnnualizedTrafficImpactsFormula =>
        $"{TotalTrafficImpacts:C2} × max(0, {AepThatCausesDelay:N4}) = {AnnualizedTrafficImpacts:C2}";

    public string DiscountedTrafficImpactsOverAnalysisPeriodFormula
    {
        get
        {
            var rate = Math.Max(0, DiscountRatePercent) / 100d;
            var years = Math.Max(0, AnalysisPeriodYears);

            if (AnnualizedTrafficImpacts <= 0 || years <= 0)
            {
                return $"No discounted series applied because annualized impacts ({AnnualizedTrafficImpacts:C2}) or years ({years:N2}) are zero.";
            }

            if (Math.Abs(rate) < 0.000001)
            {
                return $"{AnnualizedTrafficImpacts:C2} × {years:N2} years = {DiscountedTrafficImpactsOverAnalysisPeriod:C2}";
            }

            return $"{AnnualizedTrafficImpacts:C2} × ((1 - (1 + {rate:P4})^-{years:N2}) ÷ {rate:P4}) = {DiscountedTrafficImpactsOverAnalysisPeriod:C2}";
        }
    }

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

        if (AepThatCausesDelay < 0)
        {
            yield return new DiagnosticItem(DiagnosticLevel.Warning, "AEP is negative", "AEP that causes delay should be zero or greater.");
        }

        if (DiscountRatePercent < 0)
        {
            yield return new DiagnosticItem(DiagnosticLevel.Warning, "Discount rate is negative", "Discount rate should be zero or greater when annualizing impacts.");
        }

        if (AnalysisPeriodYears < 0)
        {
            yield return new DiagnosticItem(DiagnosticLevel.Warning, "Analysis period is negative", "Analysis period should be zero or greater years.");
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

        AnnualizedTrafficImpacts = TotalTrafficImpacts * Math.Max(0, AepThatCausesDelay);
        DiscountedTrafficImpactsOverAnalysisPeriod = CalculateDiscountedAnnualSeries(
            AnnualizedTrafficImpacts,
            Math.Max(0, DiscountRatePercent) / 100d,
            Math.Max(0, AnalysisPeriodYears));

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
        OnPropertyChanged(nameof(AnnualizedTrafficImpacts));
        OnPropertyChanged(nameof(DiscountedTrafficImpactsOverAnalysisPeriod));
        OnPropertyChanged(nameof(AdditionalMilesPerVehicleFormula));
        OnPropertyChanged(nameof(TotalAdditionalMileageFormula));
        OnPropertyChanged(nameof(AdditionalOperatingCostsPerDayFormula));
        OnPropertyChanged(nameof(TotalAdditionalDetourCostsFormula));
        OnPropertyChanged(nameof(HourlyIncomeFormula));
        OnPropertyChanged(nameof(LowTimeSavingsWorkTripsValueFormula));
        OnPropertyChanged(nameof(LowTimeSavingsSocialRecreationValueFormula));
        OnPropertyChanged(nameof(LowTimeSavingsOtherValueFormula));
        OnPropertyChanged(nameof(MediumTimeSavingsWorkTripsValueFormula));
        OnPropertyChanged(nameof(MediumTimeSavingsSocialRecreationValueFormula));
        OnPropertyChanged(nameof(MediumTimeSavingsOtherValueFormula));
        OnPropertyChanged(nameof(HighTimeSavingsWorkTripsValueFormula));
        OnPropertyChanged(nameof(HighTimeSavingsSocialRecreationValueFormula));
        OnPropertyChanged(nameof(HighTimeSavingsOtherValueFormula));
        OnPropertyChanged(nameof(DelayImpactsPerDayFormula));
        OnPropertyChanged(nameof(TotalDelayImpactsFormula));
        OnPropertyChanged(nameof(TotalTrafficImpactsFormula));
        OnPropertyChanged(nameof(AnnualizedTrafficImpactsFormula));
        OnPropertyChanged(nameof(DiscountedTrafficImpactsOverAnalysisPeriodFormula));
    }

    private static double CalculateDiscountedAnnualSeries(double annualValue, double discountRate, double years)
    {
        if (annualValue <= 0 || years <= 0)
        {
            return 0;
        }

        if (Math.Abs(discountRate) < 0.000001)
        {
            return annualValue * years;
        }

        return annualValue * ((1 - Math.Pow(1 + discountRate, -years)) / discountRate);
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
