using System;

namespace EconToolbox.Desktop.Models
{
    public class StageDamageRecord
    {
        public string StructureFid { get; init; } = string.Empty;
        public string DamageCategory { get; init; } = "Uncategorized";
        public string Description { get; init; } = string.Empty;
        public string ImpactArea { get; init; } = string.Empty;
        public string OccTypeName { get; init; } = string.Empty;
        public double StructureDamage0493 { get; init; }
        public double StructureDamage0224 { get; init; }
        public double StructureDamage0034 { get; init; }
        public double StructureDamage0011 { get; init; }
        public double StructureDamage0003 { get; init; }

        public double FrequentPeakDamage => Math.Max(StructureDamage0493, Math.Max(StructureDamage0224, StructureDamage0034));

        public double FrequentSumDamage => StructureDamage0493 + StructureDamage0224 + StructureDamage0034;

        public string FrequentPeakAepLabel
        {
            get
            {
                var (label, _) = GetPeakAep();
                return label;
            }
        }

        public double FrequentPeakAepDamage
        {
            get
            {
                var (_, value) = GetPeakAep();
                return value;
            }
        }

        private (string Label, double Value) GetPeakAep()
        {
            var candidates = new[]
            {
                (Label: "0.493 AEP", Value: StructureDamage0493),
                (Label: "0.224 AEP", Value: StructureDamage0224),
                (Label: "0.034 AEP", Value: StructureDamage0034)
            };

            var best = candidates[0];
            foreach (var candidate in candidates)
            {
                if (candidate.Value > best.Value)
                {
                    best = candidate;
                }
            }

            return best;
        }
    }

    public class StageDamageCategorySummary
    {
        public string DamageCategory { get; init; } = string.Empty;
        public int StructureCount { get; init; }
        public double FrequentSumDamage { get; init; }
        public double StructureDamage0493 { get; init; }
        public double StructureDamage0224 { get; init; }
        public double StructureDamage0034 { get; init; }
        public double StructureDamage0011 { get; init; }
        public double StructureDamage0003 { get; init; }
        public double PeakStructureDamage { get; init; }
    }

    public class StageDamageHighlight
    {
        public string StructureFid { get; init; } = string.Empty;
        public string DamageCategory { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public string ImpactArea { get; init; } = string.Empty;
        public string HighestAepLabel { get; init; } = string.Empty;
        public double HighestStructureDamage { get; init; }
    }
}
