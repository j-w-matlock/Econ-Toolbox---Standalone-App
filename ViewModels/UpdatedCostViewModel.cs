using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using EconToolbox.Desktop.Models;

namespace EconToolbox.Desktop.ViewModels
{
    public class UpdatedCostViewModel : BaseViewModel
    {
        private double _totalStorage;
        private double _storageRecommendation;
        private double _percent;

        private double _jointOperationsCost;
        private double _jointMaintenanceCost;
        private double _totalJointOm;

        private ObservableCollection<UpdatedCostEntry> _updatedCostItems = new();
        private double _totalUpdatedCost;

        private int _preMidpointYear = 1939;
        private int _transitionMidpointYear = 1948;
        private int _preEnrYear = 1939;
        private int _transitionEnrYear = 1948;
        private int _enr1967Year = 1967;

        private double _rrrRate;
        private int _rrrPeriods = 30;
        private double _rrrCwcci = 1.0;
        private int _rrrBaseYear = DateTime.Now.Year;
        private ObservableCollection<RrrCostEntry> _rrrCostItems = new();
        private double _rrrTotalPv;
        private double _rrrUpdatedCost;
        private double _rrrAnnualized;

        private double _discountRate1;
        private int _analysisPeriod1 = 30;
        private double _discountRate2;
        private int _analysisPeriod2 = 50;
        private double _capital1;
        private double _total1;
        private double _capital2;
        private double _total2;
        private double _omScaled;
        private double _rrrScaled;
        private double _costRecommendation;

        public double TotalStorage
        {
            get => _totalStorage;
            set { _totalStorage = value; OnPropertyChanged(); }
        }

        public double StorageRecommendation
        {
            get => _storageRecommendation;
            set { _storageRecommendation = value; OnPropertyChanged(); }
        }

        public double Percent
        {
            get => _percent;
            private set { _percent = value; OnPropertyChanged(); }
        }

        public double JointOperationsCost
        {
            get => _jointOperationsCost;
            set { _jointOperationsCost = value; OnPropertyChanged(); }
        }

        public double JointMaintenanceCost
        {
            get => _jointMaintenanceCost;
            set { _jointMaintenanceCost = value; OnPropertyChanged(); }
        }

        public double TotalJointOm
        {
            get => _totalJointOm;
            private set { _totalJointOm = value; OnPropertyChanged(); }
        }

        public ObservableCollection<UpdatedCostEntry> UpdatedCostItems
        {
            get => _updatedCostItems;
            set { _updatedCostItems = value; OnPropertyChanged(); }
        }

        public double TotalUpdatedCost
        {
            get => _totalUpdatedCost;
            private set { _totalUpdatedCost = value; OnPropertyChanged(); }
        }

        public int PreMidpointYear
        {
            get => _preMidpointYear;
            set
            {
                if (_preMidpointYear != value)
                {
                    _preMidpointYear = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ActualJointUsePreLabel));
                    OnPropertyChanged(nameof(EnrRatioPreToTransitionLabel));
                }
            }
        }

        public int TransitionMidpointYear
        {
            get => _transitionMidpointYear;
            set
            {
                if (_transitionMidpointYear != value)
                {
                    _transitionMidpointYear = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ActualJointUseTransitionLabel));
                }
            }
        }

        public int PreEnrYear
        {
            get => _preEnrYear;
            set
            {
                if (_preEnrYear != value)
                {
                    _preEnrYear = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(PreEnrIndexLabel));
                    OnPropertyChanged(nameof(EnrRatioPreToTransitionLabel));
                }
            }
        }

        public int TransitionEnrYear
        {
            get => _transitionEnrYear;
            set
            {
                if (_transitionEnrYear != value)
                {
                    _transitionEnrYear = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(TransitionEnrIndexLabel));
                    OnPropertyChanged(nameof(EnrRatioPreToTransitionLabel));
                    OnPropertyChanged(nameof(EnrRatioTransitionTo1967Label));
                }
            }
        }

        public int Enr1967Year
        {
            get => _enr1967Year;
            set
            {
                if (_enr1967Year != value)
                {
                    _enr1967Year = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(Enr1967IndexLabel));
                    OnPropertyChanged(nameof(EnrRatioTransitionTo1967Label));
                    OnPropertyChanged(nameof(JointUse1967Label));
                    OnPropertyChanged(nameof(CwccisBaseLabel));
                }
            }
        }

        public string ActualJointUsePreLabel => $"Actual Joint Use (Mid-Point {PreMidpointYear})";
        public string ActualJointUseTransitionLabel => $"Actual Joint Use (Mid-Point {TransitionMidpointYear})";
        public string PreEnrIndexLabel => $"{PreEnrYear} ENR Index Value";
        public string TransitionEnrIndexLabel => $"{TransitionEnrYear} ENR Index Value";
        public string EnrRatioPreToTransitionLabel => $"ENR Ratio ({PreEnrYear} to {TransitionEnrYear})";
        public string Enr1967IndexLabel => $"{Enr1967Year} ENR Index Value";
        public string EnrRatioTransitionTo1967Label => $"ENR Ratio ({TransitionEnrYear} to {Enr1967Year})";
        public string CwccisBaseLabel => $"{Enr1967Year} CWCCIS Index Base 100";
        public string JointUse1967Label => $"Updated Joint-Use as of {Enr1967Year}";

        public double RrrRate
        {
            get => _rrrRate;
            set { _rrrRate = value; OnPropertyChanged(); }
        }

        public int RrrPeriods
        {
            get => _rrrPeriods;
            set { _rrrPeriods = value; OnPropertyChanged(); }
        }

        public double RrrCwcci
        {
            get => _rrrCwcci;
            set { _rrrCwcci = value; OnPropertyChanged(); }
        }

        public int RrrBaseYear
        {
            get => _rrrBaseYear;
            set { _rrrBaseYear = value; OnPropertyChanged(); }
        }

        public ObservableCollection<RrrCostEntry> RrrCostItems
        {
            get => _rrrCostItems;
            set { _rrrCostItems = value; OnPropertyChanged(); }
        }

        public double RrrTotalPv
        {
            get => _rrrTotalPv;
            private set { _rrrTotalPv = value; OnPropertyChanged(); }
        }

        public double RrrUpdatedCost
        {
            get => _rrrUpdatedCost;
            private set { _rrrUpdatedCost = value; OnPropertyChanged(); }
        }

        public double RrrAnnualized
        {
            get => _rrrAnnualized;
            private set { _rrrAnnualized = value; OnPropertyChanged(); }
        }

        public double DiscountRate1
        {
            get => _discountRate1;
            set { _discountRate1 = value; OnPropertyChanged(); }
        }

        public int AnalysisPeriod1
        {
            get => _analysisPeriod1;
            set { _analysisPeriod1 = value; OnPropertyChanged(); }
        }

        public double DiscountRate2
        {
            get => _discountRate2;
            set { _discountRate2 = value; OnPropertyChanged(); }
        }

        public int AnalysisPeriod2
        {
            get => _analysisPeriod2;
            set { _analysisPeriod2 = value; OnPropertyChanged(); }
        }

        public double Capital1
        {
            get => _capital1;
            private set { _capital1 = value; OnPropertyChanged(); }
        }

        public double Total1
        {
            get => _total1;
            private set { _total1 = value; OnPropertyChanged(); }
        }

        public double Capital2
        {
            get => _capital2;
            private set { _capital2 = value; OnPropertyChanged(); }
        }

        public double Total2
        {
            get => _total2;
            private set { _total2 = value; OnPropertyChanged(); }
        }

        public double OmScaled
        {
            get => _omScaled;
            private set { _omScaled = value; OnPropertyChanged(); }
        }

        public double RrrScaled
        {
            get => _rrrScaled;
            private set { _rrrScaled = value; OnPropertyChanged(); }
        }

        public double CostRecommendation
        {
            get => _costRecommendation;
            private set { _costRecommendation = value; OnPropertyChanged(); }
        }

        public ICommand ComputeStorageCommand { get; }
        public ICommand ComputeJointCommand { get; }
        public ICommand ComputeUpdatedStorageCommand { get; }
        public ICommand ComputeRrrCommand { get; }
        public ICommand ComputeTotalCommand { get; }
        public ICommand ResetUpdatedCostItemsCommand { get; }
        public ICommand ResetRrrItemsCommand { get; }
        public ICommand ComputeCommand { get; }

        public UpdatedCostViewModel()
        {
            ComputeStorageCommand = new RelayCommand(ComputeStorage);
            ComputeJointCommand = new RelayCommand(ComputeJoint);
            ComputeUpdatedStorageCommand = new RelayCommand(ComputeUpdatedStorage);
            ComputeRrrCommand = new RelayCommand(ComputeRrr);
            ComputeTotalCommand = new RelayCommand(ComputeTotal);
            ResetUpdatedCostItemsCommand = new RelayCommand(ResetUpdatedCostItems);
            ResetRrrItemsCommand = new RelayCommand(ResetRrrCostItems);
            ComputeCommand = new RelayCommand(() =>
            {
                ComputeStorage();
                ComputeJoint();
                ComputeUpdatedStorage();
                ComputeRrr();
                ComputeTotal();
            });

            UpdatedCostItems.Add(new UpdatedCostEntry { Category = "Lands and Damages" });
            UpdatedCostItems.Add(new UpdatedCostEntry { Category = "Relocations" });
            UpdatedCostItems.Add(new UpdatedCostEntry { Category = "Dam" });
            UpdatedCostItems.Add(new UpdatedCostEntry { Category = "Roads, Railroads, & Bridges" });
            UpdatedCostItems.Add(new UpdatedCostEntry { Category = "Channels & Canals" });
        }

        private void ComputeStorage()
        {
            Percent = TotalStorage > 0 ? StorageRecommendation / TotalStorage : 0.0;
        }

        private void ComputeJoint()
        {
            TotalJointOm = JointOperationsCost + JointMaintenanceCost;
        }

        private void ComputeUpdatedStorage()
        {
            foreach (var item in UpdatedCostItems)
            {
                double enrRatioPreToTransition = (item.Pre1967EnrIndex > 0 && item.TransitionEnrIndex > 0)
                    ? item.TransitionEnrIndex / item.Pre1967EnrIndex
                    : 0.0;
                item.EnrRatioPreToTransition = enrRatioPreToTransition;
                item.JointUseTransition = item.JointUsePre1967 * enrRatioPreToTransition;

                double enrRatioTransitionTo1967 = (item.TransitionEnrIndex > 0 && item.Enr1967Index > 0)
                    ? item.Enr1967Index / item.TransitionEnrIndex
                    : 0.0;
                item.EnrRatioTransitionTo1967 = enrRatioTransitionTo1967;
                item.JointUse1967 = item.JointUseTransition * enrRatioTransitionTo1967;

                double cwccisBase = item.CwccisBase <= 0 ? 100.0 : item.CwccisBase;
                item.CwccisBase = cwccisBase;
                double cwccisUpdateFactor = cwccisBase > 0 ? item.CwccisIndex / cwccisBase : 0.0;
                item.CwccisUpdateFactor = cwccisUpdateFactor;
                item.UpdatedJointCost = item.JointUse1967 * cwccisUpdateFactor;
            }
            TotalUpdatedCost = UpdatedCostItems.Sum(i => i.UpdatedJointCost);
        }

        private void ComputeRrr()
        {
            double rateDec = RrrRate / 100.0;
            foreach (var item in RrrCostItems)
            {
                item.PvFactor = 1.0 / Math.Pow(1.0 + rateDec, item.Year - RrrBaseYear);
                item.PresentValue = item.FutureCost * item.PvFactor;
            }
            RrrTotalPv = RrrCostItems.Sum(i => i.PresentValue);
            RrrUpdatedCost = RrrTotalPv * RrrCwcci;
            double crf = CapitalRecoveryModel.Calculate(rateDec, RrrPeriods);
            RrrAnnualized = RrrUpdatedCost * crf;
        }

        private void ComputeTotal()
        {
            OmScaled = TotalJointOm * Percent;
            RrrScaled = RrrAnnualized * Percent;
            CostRecommendation = TotalUpdatedCost * Percent;
            double crf1 = CapitalRecoveryModel.Calculate(DiscountRate1 / 100.0, AnalysisPeriod1);
            Capital1 = TotalUpdatedCost * Percent * crf1;
            Total1 = Capital1 + OmScaled + RrrScaled;
            double crf2 = CapitalRecoveryModel.Calculate(DiscountRate2 / 100.0, AnalysisPeriod2);
            Capital2 = TotalUpdatedCost * Percent * crf2;
            Total2 = Capital2 + OmScaled;
        }

        private void ResetUpdatedCostItems()
        {
            foreach (var item in UpdatedCostItems)
            {
                item.JointUsePre1967 = 0;
                item.Pre1967EnrIndex = 0;
                item.TransitionEnrIndex = 0;
                item.EnrRatioPreToTransition = 0;
                item.JointUseTransition = 0;
                item.Enr1967Index = 0;
                item.EnrRatioTransitionTo1967 = 0;
                item.CwccisBase = 100;
                item.JointUse1967 = 0;
                item.CwccisIndex = 0;
                item.CwccisUpdateFactor = 0;
                item.UpdatedJointCost = 0;
            }

            TotalUpdatedCost = 0;
            ComputeTotal();
        }

        private void ResetRrrCostItems()
        {
            RrrCostItems.Clear();
            RrrTotalPv = 0;
            RrrUpdatedCost = 0;
            RrrAnnualized = 0;
            ComputeTotal();
        }
    }
}
