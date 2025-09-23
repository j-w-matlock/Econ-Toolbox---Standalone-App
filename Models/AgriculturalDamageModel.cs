using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace EconToolbox.Desktop.Models
{
    public record FloodRegionProfile(
        string Name,
        string Description,
        int FloodSeasonStartDay,
        int FloodSeasonPeakDay,
        int FloodSeasonEndDay,
        int GrowingSeasonShiftDays,
        double AnnualFloodProbability)
    {
        public override string ToString() => Name;
    }

    public record CropGrowthStage(
        string Name,
        int StartDayOffset,
        int EndDayOffset,
        double Vulnerability,
        double FloodToleranceDays);

    public record DamageCurvePoint(double DepthFeet, double DamagePercent);

    public record ResolvedGrowthStage(
        string Name,
        int StartDay,
        int EndDay,
        double Vulnerability,
        double FloodToleranceDays);

    public record ResolvedCropProfile(
        CropDamageProfile Profile,
        int PlantingStartDay,
        int PlantingEndDay,
        int HarvestEndDay,
        IReadOnlyList<ResolvedGrowthStage> Stages);

    public class CropDamageProfile
    {
        private readonly List<DamageCurvePoint> _damageCurve;

        public CropDamageProfile(
            string cropName,
            string occupancyType,
            string description,
            double valuePerAcre,
            int plantingWindowStartDay,
            int plantingWindowEndDay,
            int harvestEndDay,
            IEnumerable<CropGrowthStage> stages,
            IEnumerable<DamageCurvePoint> damageCurve)
        {
            CropName = cropName;
            OccupancyType = occupancyType;
            Description = description;
            ValuePerAcre = valuePerAcre;
            PlantingWindowStartDay = plantingWindowStartDay;
            PlantingWindowEndDay = plantingWindowEndDay;
            HarvestEndDay = harvestEndDay;
            Stages = stages.ToList();
            _damageCurve = damageCurve.OrderBy(p => p.DepthFeet).ToList();
        }

        public string CropName { get; }
        public string OccupancyType { get; }
        public string Description { get; }
        public double ValuePerAcre { get; }
        public int PlantingWindowStartDay { get; }
        public int PlantingWindowEndDay { get; }
        public int HarvestEndDay { get; }
        public IReadOnlyList<CropGrowthStage> Stages { get; }
        public IReadOnlyList<DamageCurvePoint> DamageCurve => _damageCurve;

        public double GetBaseDamage(double depth)
        {
            if (_damageCurve.Count == 0)
            {
                return 0;
            }

            if (depth <= _damageCurve[0].DepthFeet)
            {
                return _damageCurve[0].DamagePercent;
            }

            if (depth >= _damageCurve[^1].DepthFeet)
            {
                return _damageCurve[^1].DamagePercent;
            }

            for (int i = 0; i < _damageCurve.Count - 1; i++)
            {
                var current = _damageCurve[i];
                var next = _damageCurve[i + 1];
                if (depth >= current.DepthFeet && depth <= next.DepthFeet)
                {
                    double span = next.DepthFeet - current.DepthFeet;
                    if (Math.Abs(span) < 0.0001)
                    {
                        return current.DamagePercent;
                    }
                    double ratio = (depth - current.DepthFeet) / span;
                    return current.DamagePercent + ratio * (next.DamagePercent - current.DamagePercent);
                }
            }

            return _damageCurve[^1].DamagePercent;
        }

        public ResolvedCropProfile ResolveForRegion(int growingSeasonShiftDays)
        {
            int Adjust(int day) => Math.Clamp(day, 1, 365);

            int plantingStart = Adjust(PlantingWindowStartDay + growingSeasonShiftDays);
            int plantingEnd = Adjust(PlantingWindowEndDay + growingSeasonShiftDays);
            int harvestEnd = Adjust(HarvestEndDay + growingSeasonShiftDays);

            var resolvedStages = new List<ResolvedGrowthStage>();
            foreach (var stage in Stages)
            {
                int stageStart = Adjust(plantingStart + stage.StartDayOffset);
                int stageEnd = Adjust(plantingStart + stage.EndDayOffset);
                stageEnd = Math.Min(stageEnd, harvestEnd);
                resolvedStages.Add(new ResolvedGrowthStage(stage.Name, stageStart, stageEnd, stage.Vulnerability, stage.FloodToleranceDays));
            }

            return new ResolvedCropProfile(this, plantingStart, plantingEnd, harvestEnd, resolvedStages);
        }

        public override string ToString() => CropName;
    }

    public static class AgriculturalDamageLibrary
    {
        public static IReadOnlyList<FloodRegionProfile> Regions { get; }
        public static IReadOnlyList<CropDamageProfile> Crops { get; }
        public static IReadOnlyDictionary<int, double> DurationMultipliers { get; }
        public static IReadOnlyList<double> StandardDepths { get; } = new ReadOnlyCollection<double>(new List<double> { 0.5, 1, 2, 3, 4, 5 });
        public static IReadOnlyList<int> StandardDurations { get; } = new ReadOnlyCollection<int>(new List<int> { 1, 3, 7, 14 });

        static AgriculturalDamageLibrary()
        {
            Regions = new ReadOnlyCollection<FloodRegionProfile>(new List<FloodRegionProfile>
            {
                new("Upper Midwest", "Represents snowmelt-driven flooding across the Missouri and Upper Mississippi basins.", 75, 120, 220, 0, 0.28),
                new("Lower Mississippi & Gulf", "Captures early spring river flooding and tropical rainfall influences across the lower Mississippi Valley.", 45, 95, 190, -18, 0.30),
                new("Northern Plains", "Reflects later planting and prolonged runoff from plains snowmelt in the Dakotas and Montana.", 90, 135, 230, 12, 0.25),
                new("Atlantic & Northeast", "Accounts for nor'easter rainfall and spring breakup flooding typical of the northeastern U.S.", 80, 125, 205, 6, 0.22),
                new("Pacific & Interior West", "Combines snowmelt and convective flooding drivers for interior western agricultural valleys.", 70, 115, 200, -4, 0.18)
            });

            DurationMultipliers = new ReadOnlyDictionary<int, double>(new Dictionary<int, double>
            {
                { 1, 0.35 },
                { 3, 0.65 },
                { 7, 1.0 },
                { 14, 1.18 }
            });

            Crops = new ReadOnlyCollection<CropDamageProfile>(new List<CropDamageProfile>
            {
                new(
                    "Corn (Grain)",
                    "Agricultural Field - Corn (NASS 600)",
                    "Assumes full-season field corn grown for grain with modern tillage and drainage practices.",
                    950,
                    110,
                    140,
                    280,
                    new[]
                    {
                        new CropGrowthStage("Stand establishment", 0, 25, 0.35, 2.5),
                        new CropGrowthStage("Vegetative growth", 25, 70, 0.6, 3.0),
                        new CropGrowthStage("Silking & tassel", 70, 110, 1.0, 1.5),
                        new CropGrowthStage("Maturity & dry down", 110, 150, 0.45, 2.0)
                    },
                    new[]
                    {
                        new DamageCurvePoint(0.5, 5),
                        new DamageCurvePoint(1, 15),
                        new DamageCurvePoint(2, 35),
                        new DamageCurvePoint(3, 55),
                        new DamageCurvePoint(4, 75),
                        new DamageCurvePoint(5, 90)
                    }),
                new(
                    "Soybeans",
                    "Agricultural Field - Soybeans (NASS 660)",
                    "Standard maturity group III/IV soybeans drilled on 30-inch rows with conventional irrigation.",
                    750,
                    125,
                    160,
                    285,
                    new[]
                    {
                        new CropGrowthStage("Emergence & stand", 0, 25, 0.3, 2.0),
                        new CropGrowthStage("Vegetative nodes", 25, 65, 0.55, 3.0),
                        new CropGrowthStage("Flowering & pod set", 65, 105, 0.9, 1.8),
                        new CropGrowthStage("Seed fill", 105, 140, 0.6, 2.2)
                    },
                    new[]
                    {
                        new DamageCurvePoint(0.5, 4),
                        new DamageCurvePoint(1, 12),
                        new DamageCurvePoint(2, 32),
                        new DamageCurvePoint(3, 50),
                        new DamageCurvePoint(4, 68),
                        new DamageCurvePoint(5, 85)
                    }),
                new(
                    "Spring Wheat",
                    "Agricultural Field - Wheat (NASS 411)",
                    "Represents hard red spring wheat planted after frost risk with standard fertility.",
                    600,
                    105,
                    130,
                    250,
                    new[]
                    {
                        new CropGrowthStage("Tillering", 0, 20, 0.4, 2.5),
                        new CropGrowthStage("Stem elongation", 20, 60, 0.6, 3.0),
                        new CropGrowthStage("Heading & bloom", 60, 95, 0.85, 1.7),
                        new CropGrowthStage("Grain fill", 95, 130, 0.5, 2.0)
                    },
                    new[]
                    {
                        new DamageCurvePoint(0.5, 3),
                        new DamageCurvePoint(1, 10),
                        new DamageCurvePoint(2, 28),
                        new DamageCurvePoint(3, 45),
                        new DamageCurvePoint(4, 63),
                        new DamageCurvePoint(5, 80)
                    }),
                new(
                    "Cotton",
                    "Agricultural Field - Cotton (NASS 390)",
                    "Long-season upland cotton on row spacing common to the lower Mississippi Delta.",
                    1200,
                    110,
                    150,
                    300,
                    new[]
                    {
                        new CropGrowthStage("Seeding & stand", 0, 25, 0.25, 3.2),
                        new CropGrowthStage("Vegetative", 25, 70, 0.6, 4.0),
                        new CropGrowthStage("Bloom & boll set", 70, 120, 0.95, 2.0),
                        new CropGrowthStage("Boll fill & open", 120, 170, 0.5, 2.5)
                    },
                    new[]
                    {
                        new DamageCurvePoint(0.5, 6),
                        new DamageCurvePoint(1, 18),
                        new DamageCurvePoint(2, 40),
                        new DamageCurvePoint(3, 60),
                        new DamageCurvePoint(4, 78),
                        new DamageCurvePoint(5, 92)
                    }),
                new(
                    "Rice",
                    "Agricultural Field - Rice (NASS 310)",
                    "Mechanized paddy rice with managed flooding across the Gulf Coast and lower Mississippi.",
                    1400,
                    95,
                    140,
                    270,
                    new[]
                    {
                        new CropGrowthStage("Permanent flood establishment", 0, 30, 0.4, 4.5),
                        new CropGrowthStage("Tillering", 30, 80, 0.7, 5.0),
                        new CropGrowthStage("Panicle initiation & boot", 80, 120, 1.0, 3.0),
                        new CropGrowthStage("Grain fill & maturation", 120, 160, 0.6, 3.0)
                    },
                    new[]
                    {
                        new DamageCurvePoint(0.5, 8),
                        new DamageCurvePoint(1, 20),
                        new DamageCurvePoint(2, 45),
                        new DamageCurvePoint(3, 65),
                        new DamageCurvePoint(4, 82),
                        new DamageCurvePoint(5, 95)
                    }),
                new(
                    "Grain Sorghum",
                    "Agricultural Field - Sorghum (NASS 650)",
                    "Medium maturity grain sorghum managed under dryland or supplemental irrigation.",
                    700,
                    120,
                    150,
                    260,
                    new[]
                    {
                        new CropGrowthStage("Stand establishment", 0, 25, 0.3, 3.0),
                        new CropGrowthStage("Vegetative", 25, 70, 0.55, 3.2),
                        new CropGrowthStage("Boot & bloom", 70, 110, 0.9, 1.8),
                        new CropGrowthStage("Grain fill", 110, 150, 0.5, 2.3)
                    },
                    new[]
                    {
                        new DamageCurvePoint(0.5, 4),
                        new DamageCurvePoint(1, 13),
                        new DamageCurvePoint(2, 30),
                        new DamageCurvePoint(3, 48),
                        new DamageCurvePoint(4, 65),
                        new DamageCurvePoint(5, 82)
                    })
            });
        }

        public static double GetDurationMultiplier(int durationDays)
        {
            if (DurationMultipliers.TryGetValue(durationDays, out var value))
            {
                return value;
            }

            // Linear interpolation for non-standard durations.
            var ordered = DurationMultipliers.OrderBy(kv => kv.Key).ToList();
            if (durationDays <= ordered[0].Key)
            {
                return ordered[0].Value;
            }
            if (durationDays >= ordered[^1].Key)
            {
                return ordered[^1].Value;
            }

            for (int i = 0; i < ordered.Count - 1; i++)
            {
                var current = ordered[i];
                var next = ordered[i + 1];
                if (durationDays >= current.Key && durationDays <= next.Key)
                {
                    double span = next.Key - current.Key;
                    if (span == 0)
                    {
                        return current.Value;
                    }
                    double ratio = (durationDays - current.Key) / span;
                    return current.Value + ratio * (next.Value - current.Value);
                }
            }

            return ordered[^1].Value;
        }
    }
}
