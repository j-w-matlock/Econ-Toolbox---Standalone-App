using System;
using System.Collections.Generic;
using System.Linq;

namespace EconToolbox.Desktop.Models
{
    public class StageDamageRecord
    {
        public string StructureFid { get; set; } = string.Empty;
        public string DamageCategory { get; set; } = "Uncategorized";
        public string Description { get; set; } = string.Empty;
        public string ImpactArea { get; set; } = string.Empty;
        public string OccTypeName { get; set; } = string.Empty;
        public string SummaryName { get; set; } = string.Empty;
        public string SourceKey { get; set; } = string.Empty;

        public List<StageDamageAepValue> AepDamages { get; set; } = new();
        public List<StageDamageAepValue> DepthAboveFirstFloorByAep { get; set; } = new();

        public double FrequentPeakDamage
        {
            get
            {
                var peak = GetPeakAep();
                return peak.Value;
            }
        }

        public double FrequentPeakDepthAboveFirstFloor
        {
            get
            {
                var peak = GetPeakAep();
                return GetDepthAboveFirstFloor(peak.Label);
            }
        }

        public double FrequentSumDamage => AepDamages.Sum(a => a.Value);

        public string FrequentPeakAepLabel
        {
            get
            {
                var peak = GetPeakAep();
                return peak.Label;
            }
        }

        public double FrequentPeakAepDamage
        {
            get
            {
                var peak = GetPeakAep();
                return peak.Value;
            }
        }

        private StageDamageAepValue GetPeakAep()
        {
            var candidates = AepDamages.ToList();
            if (candidates.Count == 0)
            {
                return new StageDamageAepValue("N/A", 0);
            }

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

        private double GetDepthAboveFirstFloor(string aepLabel)
        {
            if (DepthAboveFirstFloorByAep.Count == 0 || string.IsNullOrWhiteSpace(aepLabel))
            {
                return 0d;
            }

            var match = DepthAboveFirstFloorByAep
                .FirstOrDefault(value => value.Label.Equals(aepLabel, StringComparison.OrdinalIgnoreCase));

            return match?.Value ?? 0d;
        }
    }

    public class StageDamageCategorySummary
    {
        public string SummaryName { get; init; } = string.Empty;
        public string DamageCategory { get; init; } = string.Empty;
        public int StructureCount { get; init; }
        public IReadOnlyList<double> AepDamages { get; init; } = Array.Empty<double>();
        public double FrequentSumDamage { get; init; }
    }

    public class StageDamageHighlight
    {
        public string SummaryName { get; init; } = string.Empty;
        public string StructureFid { get; init; } = string.Empty;
        public string DamageCategory { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public string ImpactArea { get; init; } = string.Empty;
        public string HighestAepLabel { get; init; } = string.Empty;
        public double DepthAboveFirstFloorAtHighestAep { get; init; }
        public double HighestStructureDamage { get; init; }
    }

    public record StageDamageAepValue(string Label, double Value);
}
