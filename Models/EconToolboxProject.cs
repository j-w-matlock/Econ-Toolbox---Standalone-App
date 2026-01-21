namespace EconToolbox.Desktop.Models
{
    public class EconToolboxProject
    {
        public EadData? Ead { get; set; }
        public AgricultureDepthDamageData? AgricultureDepthDamage { get; set; }
        public UpdatedCostData? UpdatedCost { get; set; }
        public AnnualizerData? Annualizer { get; set; }
        public WaterDemandData? WaterDemand { get; set; }
        public UdvData? Udv { get; set; }
        public RecreationCapacityData? RecreationCapacity { get; set; }
        public GanttData? Gantt { get; set; }
        public StageDamageOrganizerData? StageDamageOrganizer { get; set; }
    }
}
