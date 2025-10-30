using EconToolbox.Desktop.Models;

namespace EconToolbox.Desktop.Models
{
    public class UpdatedCostEntry : ObservableObject
    {
        private string _category = string.Empty;
        private double _jointUsePre1967;
        private double _pre1967EnrIndex;
        private double _transitionEnrIndex;
        private double _enrRatioPreToTransition;
        private double _jointUseTransition;
        private double _enr1967Index;
        private double _enrRatioTransitionTo1967;
        private double _cwccisBase = 100.0;
        private double _jointUse1967;
        private double _cwccisIndex;
        private double _cwccisUpdateFactor;
        private double _updatedJointCost;

        public string Category
        {
            get => _category;
            set { _category = value; OnPropertyChanged(); }
        }

        public double JointUsePre1967
        {
            get => _jointUsePre1967;
            set { _jointUsePre1967 = value; OnPropertyChanged(); }
        }

        public double Pre1967EnrIndex
        {
            get => _pre1967EnrIndex;
            set { _pre1967EnrIndex = value; OnPropertyChanged(); }
        }

        public double TransitionEnrIndex
        {
            get => _transitionEnrIndex;
            set { _transitionEnrIndex = value; OnPropertyChanged(); }
        }

        public double EnrRatioPreToTransition
        {
            get => _enrRatioPreToTransition;
            set { _enrRatioPreToTransition = value; OnPropertyChanged(); }
        }

        public double JointUseTransition
        {
            get => _jointUseTransition;
            set { _jointUseTransition = value; OnPropertyChanged(); }
        }

        public double Enr1967Index
        {
            get => _enr1967Index;
            set { _enr1967Index = value; OnPropertyChanged(); }
        }

        public double EnrRatioTransitionTo1967
        {
            get => _enrRatioTransitionTo1967;
            set { _enrRatioTransitionTo1967 = value; OnPropertyChanged(); }
        }

        public double CwccisBase
        {
            get => _cwccisBase;
            set { _cwccisBase = value; OnPropertyChanged(); }
        }

        public double JointUse1967
        {
            get => _jointUse1967;
            set { _jointUse1967 = value; OnPropertyChanged(); }
        }

        public double CwccisIndex
        {
            get => _cwccisIndex;
            set { _cwccisIndex = value; OnPropertyChanged(); }
        }

        public double CwccisUpdateFactor
        {
            get => _cwccisUpdateFactor;
            set { _cwccisUpdateFactor = value; OnPropertyChanged(); }
        }

        public double UpdatedJointCost
        {
            get => _updatedJointCost;
            set { _updatedJointCost = value; OnPropertyChanged(); }
        }
    }
}
