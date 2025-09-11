namespace EconToolbox.Desktop.ViewModels
{
    public class MainViewModel
    {
        public EadViewModel Ead { get; } = new();
        public UpdatedCostViewModel UpdatedCost { get; } = new();
        public InterestDuringConstructionViewModel Idc { get; } = new();
        public AnnualizerViewModel Annualizer { get; } = new();
        public UdvViewModel Udv { get; } = new();
        public WaterDemandViewModel WaterDemand { get; } = new();
    }
}
