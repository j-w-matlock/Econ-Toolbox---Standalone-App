using CommunityToolkit.Mvvm.ComponentModel;

namespace EconToolbox.Desktop.Models
{
    public partial class ProjectTask : ObservableObject
    {
        [ObservableProperty]
        private string _taskName = string.Empty;

        [ObservableProperty]
        private int _urgency;

        [ObservableProperty]
        private int _importance;

        [ObservableProperty]
        private int _complexity;

        private double _priorityScore;

        public double PriorityScore
        {
            get => _priorityScore;
            private set => SetProperty(ref _priorityScore, value);
        }

        public void SetPriorityScore(double score)
        {
            PriorityScore = score;
        }
    }
}
