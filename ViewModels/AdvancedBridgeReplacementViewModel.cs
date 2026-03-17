using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using EconToolbox.Desktop.Models;

namespace EconToolbox.Desktop.ViewModels;

public sealed class AdvancedBridgeReplacementViewModel : DiagnosticViewModelBase, IComputeModule
{
    private double _costOfNewBridge = 29_405_000d;
    private double _lifeOfNewBridgeYears = 50d;
    private double _remainingLifeOfExistingBridgeYears = 18d;
    private double _discountRate = 0.0275d;
    private double _annualOmAndRehabExistingBridge = 459_000d;
    private double _annualOmNewBridge = 25_000d;

    public AdvancedBridgeReplacementViewModel()
    {
        ComputeCommand = new RelayCommand(Recalculate);
        Recalculate();
    }

    public ICommand ComputeCommand { get; }

    public double CostOfNewBridge
    {
        get => _costOfNewBridge;
        set => SetAndRecalculate(ref _costOfNewBridge, Math.Max(0d, value));
    }

    public double LifeOfNewBridgeYears
    {
        get => _lifeOfNewBridgeYears;
        set => SetAndRecalculate(ref _lifeOfNewBridgeYears, Math.Max(0d, value));
    }

    public double RemainingLifeOfExistingBridgeYears
    {
        get => _remainingLifeOfExistingBridgeYears;
        set => SetAndRecalculate(ref _remainingLifeOfExistingBridgeYears, Math.Max(0d, value));
    }

    public double DiscountRate
    {
        get => _discountRate;
        set => SetAndRecalculate(ref _discountRate, Math.Max(0d, value));
    }

    public double AnnualOmAndRehabExistingBridge
    {
        get => _annualOmAndRehabExistingBridge;
        set => SetAndRecalculate(ref _annualOmAndRehabExistingBridge, Math.Max(0d, value));
    }

    public double AnnualOmNewBridge
    {
        get => _annualOmNewBridge;
        set => SetAndRecalculate(ref _annualOmNewBridge, Math.Max(0d, value));
    }

    public double CapitalRecoveryFactor { get; private set; }
    public double AnnualCostOfNewBridge { get; private set; }
    public double ExtensionPeriodYears { get; private set; }
    public double PwAnnuityFactorForExtensionPeriod { get; private set; }
    public double PwAtYearROfAvoidedReplacementStream { get; private set; }
    public double PwSinglePaymentFactorToYear1 { get; private set; }
    public double PwOfAvoidedFutureReplacementCost { get; private set; }
    public double AnnualOmRehabSavings { get; private set; }
    public double PwAnnuityFactorForRemainingLife { get; private set; }
    public double PwOfOmRehabSavings { get; private set; }
    public double TotalPresentWorthCredit { get; private set; }
    public double AverageAnnualAdvancedBridgeReplacementBenefit { get; private set; }

    public string AvoidedReplacementFormula =>
        $"A_b × P/A(i,E) × P/F(i,R) = {AnnualCostOfNewBridge:C0} × {PwAnnuityFactorForExtensionPeriod:N4} × {PwSinglePaymentFactorToYear1:N4} = {PwOfAvoidedFutureReplacementCost:C0}";

    public string OmSavingsFormula =>
        $"ΔA_om × P/A(i,R) = {AnnualOmRehabSavings:C0} × {PwAnnuityFactorForRemainingLife:N4} = {PwOfOmRehabSavings:C0}";

    public string FinalBenefitFormula =>
        $"(PW_repl + PW_om) × CRF = ({PwOfAvoidedFutureReplacementCost:C0} + {PwOfOmRehabSavings:C0}) × {CapitalRecoveryFactor:N6} = {AverageAnnualAdvancedBridgeReplacementBenefit:C0}/yr";

    private void SetAndRecalculate(ref double field, double value, [CallerMemberName] string? propertyName = null)
    {
        if (Math.Abs(field - value) < 0.0000001d)
        {
            return;
        }

        field = value;
        OnPropertyChanged(propertyName);
        Recalculate();
    }

    private static double CapitalRecovery(double rate, double years)
    {
        if (years <= 0)
        {
            return 0;
        }

        if (Math.Abs(rate) < 0.000000001d)
        {
            return 1d / years;
        }

        var growth = Math.Pow(1d + rate, years);
        return rate * growth / (growth - 1d);
    }

    private static double PresentWorthAnnuityFactor(double rate, double years)
    {
        if (years <= 0)
        {
            return 0;
        }

        if (Math.Abs(rate) < 0.000000001d)
        {
            return years;
        }

        return (1d - Math.Pow(1d + rate, -years)) / rate;
    }

    private static double PresentWorthSinglePaymentFactor(double rate, double years)
    {
        if (years <= 0)
        {
            return 1d;
        }

        return Math.Pow(1d + rate, -years);
    }

    private void Recalculate()
    {
        CapitalRecoveryFactor = CapitalRecovery(DiscountRate, LifeOfNewBridgeYears);
        AnnualCostOfNewBridge = CostOfNewBridge * CapitalRecoveryFactor;

        ExtensionPeriodYears = Math.Max(0d, LifeOfNewBridgeYears - RemainingLifeOfExistingBridgeYears);
        PwAnnuityFactorForExtensionPeriod = PresentWorthAnnuityFactor(DiscountRate, ExtensionPeriodYears);
        PwAtYearROfAvoidedReplacementStream = AnnualCostOfNewBridge * PwAnnuityFactorForExtensionPeriod;

        PwSinglePaymentFactorToYear1 = PresentWorthSinglePaymentFactor(DiscountRate, RemainingLifeOfExistingBridgeYears);
        PwOfAvoidedFutureReplacementCost = PwAtYearROfAvoidedReplacementStream * PwSinglePaymentFactorToYear1;

        AnnualOmRehabSavings = AnnualOmAndRehabExistingBridge - AnnualOmNewBridge;
        PwAnnuityFactorForRemainingLife = PresentWorthAnnuityFactor(DiscountRate, RemainingLifeOfExistingBridgeYears);
        PwOfOmRehabSavings = AnnualOmRehabSavings * PwAnnuityFactorForRemainingLife;

        TotalPresentWorthCredit = PwOfAvoidedFutureReplacementCost + PwOfOmRehabSavings;
        AverageAnnualAdvancedBridgeReplacementBenefit = TotalPresentWorthCredit * CapitalRecoveryFactor;

        OnPropertyChanged(nameof(CapitalRecoveryFactor));
        OnPropertyChanged(nameof(AnnualCostOfNewBridge));
        OnPropertyChanged(nameof(ExtensionPeriodYears));
        OnPropertyChanged(nameof(PwAnnuityFactorForExtensionPeriod));
        OnPropertyChanged(nameof(PwAtYearROfAvoidedReplacementStream));
        OnPropertyChanged(nameof(PwSinglePaymentFactorToYear1));
        OnPropertyChanged(nameof(PwOfAvoidedFutureReplacementCost));
        OnPropertyChanged(nameof(AnnualOmRehabSavings));
        OnPropertyChanged(nameof(PwAnnuityFactorForRemainingLife));
        OnPropertyChanged(nameof(PwOfOmRehabSavings));
        OnPropertyChanged(nameof(TotalPresentWorthCredit));
        OnPropertyChanged(nameof(AverageAnnualAdvancedBridgeReplacementBenefit));
        OnPropertyChanged(nameof(AvoidedReplacementFormula));
        OnPropertyChanged(nameof(OmSavingsFormula));
        OnPropertyChanged(nameof(FinalBenefitFormula));

        RefreshDiagnostics();
    }

    protected override IEnumerable<DiagnosticItem> BuildDiagnostics()
    {
        if (LifeOfNewBridgeYears <= 0)
        {
            yield return new DiagnosticItem("Life of new bridge must be greater than zero", "Increase n to compute CRF.");
        }

        if (RemainingLifeOfExistingBridgeYears > LifeOfNewBridgeYears)
        {
            yield return new DiagnosticItem("Remaining life exceeds new bridge life", "Extension period is clamped to zero because n - R is negative.");
        }

        if (AnnualOmRehabSavings < 0)
        {
            yield return new DiagnosticItem("New bridge O&M exceeds existing O&M + rehab", "PW_om is negative because ΔA_om is below zero.");
        }

        if (LifeOfNewBridgeYears > 0 && RemainingLifeOfExistingBridgeYears <= LifeOfNewBridgeYears && AnnualOmRehabSavings >= 0)
        {
            yield return new DiagnosticItem(
                "Advanced bridge replacement inputs are valid",
                $"Equivalent annual benefit is {AverageAnnualAdvancedBridgeReplacementBenefit:C0}/year using USACE-based present worth conversions.");
        }
    }

    public override object CaptureState()
    {
        return new AdvancedBridgeReplacementData
        {
            CostOfNewBridge = CostOfNewBridge,
            LifeOfNewBridgeYears = LifeOfNewBridgeYears,
            RemainingLifeOfExistingBridgeYears = RemainingLifeOfExistingBridgeYears,
            DiscountRate = DiscountRate,
            AnnualOmAndRehabExistingBridge = AnnualOmAndRehabExistingBridge,
            AnnualOmNewBridge = AnnualOmNewBridge
        };
    }

    public override void RestoreState(object state)
    {
        if (state is not AdvancedBridgeReplacementData data)
        {
            return;
        }

        _costOfNewBridge = Math.Max(0d, data.CostOfNewBridge);
        _lifeOfNewBridgeYears = Math.Max(0d, data.LifeOfNewBridgeYears);
        _remainingLifeOfExistingBridgeYears = Math.Max(0d, data.RemainingLifeOfExistingBridgeYears);
        _discountRate = Math.Max(0d, data.DiscountRate);
        _annualOmAndRehabExistingBridge = Math.Max(0d, data.AnnualOmAndRehabExistingBridge);
        _annualOmNewBridge = Math.Max(0d, data.AnnualOmNewBridge);

        OnPropertyChanged(nameof(CostOfNewBridge));
        OnPropertyChanged(nameof(LifeOfNewBridgeYears));
        OnPropertyChanged(nameof(RemainingLifeOfExistingBridgeYears));
        OnPropertyChanged(nameof(DiscountRate));
        OnPropertyChanged(nameof(AnnualOmAndRehabExistingBridge));
        OnPropertyChanged(nameof(AnnualOmNewBridge));

        Recalculate();
    }
}
