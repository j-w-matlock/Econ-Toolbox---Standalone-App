using System;
using System.Linq;
using System.Collections.Generic;

namespace EconToolbox.Desktop.Models
{
    public static class EadModel
    {
        public static double Compute(double[] probabilities, double[] damages)
        {
            if (probabilities == null || damages == null)
                throw new ArgumentNullException("Input arrays cannot be null");

            if (probabilities.Length != damages.Length || probabilities.Length == 0)
                throw new ArgumentException("Probability and damage counts must match and contain at least one point");

            // Pair probabilities with damages and sort in descending order of probability
            var pairs = probabilities.Zip(damages, (p, d) => (p, d))
                                    .OrderByDescending(t => t.p)
                                    .ToList();

            // Validate probabilities and ensure they are within [0,1]
            foreach (var pair in pairs)
            {
                if (pair.p < 0.0 || pair.p > 1.0)
                    throw new ArgumentException("Probabilities must be between 0 and 1");
            }

            // Track the maximum damage to auto-fill the 0% probability row
            double maxDamage = pairs.Max(t => t.d);

            // Insert 100% probability with $0 damage if missing
            if (pairs[0].p < 1.0)
                pairs.Insert(0, (1.0, 0.0));

            // Append 0% probability with the highest damage if missing
            if (pairs[^1].p > 0.0)
                pairs.Add((0.0, maxDamage));

            // Compute EAD using the trapezoidal rule
            double sum = 0.0;
            for (int i = 0; i < pairs.Count - 1; i++)
            {
                sum += 0.5 * (pairs[i].d + pairs[i + 1].d) * (pairs[i].p - pairs[i + 1].p);
            }
            return sum;
        }

        public static double ComputeEquivalentAnnualDamage(double expectedAnnualDamage, double expectedFutureDamages, int analysisPeriodYears, double discountRate)
        {
            if (analysisPeriodYears <= 0)
                throw new ArgumentOutOfRangeException(nameof(analysisPeriodYears), "Analysis period must be greater than zero.");

            if (expectedFutureDamages < 0)
                throw new ArgumentOutOfRangeException(nameof(expectedFutureDamages), "Future damages must be zero or greater.");

            if (discountRate < 0)
                throw new ArgumentOutOfRangeException(nameof(discountRate), "Discount rate must be zero or greater.");

            double crf = CapitalRecoveryModel.Calculate(discountRate, analysisPeriodYears);
            return expectedAnnualDamage + (expectedFutureDamages * crf);
        }
    }
}
