using System;
using System.Collections.Generic;
using System.Linq;

namespace EconToolbox.Desktop.Models
{
    public class StageDamageRecord
    {
        public string StructureFid { get; init; } = string.Empty;
        public string DamageCategory { get; init; } = "Uncategorized";
        public string Description { get; init; } = string.Empty;
        public string ImpactArea { get; init; } = string.Empty;
        public string OccTypeName { get; init; } = string.Empty;

        public IReadOnlyList<StageDamageAepValue> AepDamages { get; init; } = Array.Empty<StageDamageAepValue>();

        public double FrequentPeakDamage
        {
            get
            {
                var peak = GetPeakAep();
                return peak.Value;
            }
        }

        public double FrequentSumDamage => AepDamages.Take(3).Sum(a => a.Value);

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
            var candidates = AepDamages.Take(3).ToList();
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
    }

    public class StageDamageCategorySummary
    {
        public string DamageCategory { get; init; } = string.Empty;
        public int StructureCount { get; init; }
        public IReadOnlyList<double> AepDamages { get; init; } = Array.Empty<double>();
        public double FrequentSumDamage { get; init; }
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

    public record StageDamageAepValue(string Label, double Value);
}
