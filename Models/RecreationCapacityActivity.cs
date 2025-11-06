namespace EconToolbox.Desktop.Models;

public record RecreationCapacityActivity(
    string Activity,
    string ResourceDescription,
    double ResourceQuantity,
    string ResourceUnits,
    double PeoplePerUnit,
    double DailyTurnover,
    double SeasonDays,
    double PeopleAtOneTime,
    double DailyCapacity,
    double SeasonalCapacity,
    string GuidanceNote);
