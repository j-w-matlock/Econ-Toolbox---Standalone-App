using System;

namespace EconToolbox.Desktop.Models
{
    public static class EadModel
    {
        public static double Compute(double[] probabilities, double[] damages)
        {
            if (probabilities == null || damages == null)
                throw new ArgumentNullException("Input arrays cannot be null");

            if (probabilities.Length != damages.Length || probabilities.Length < 2)
                throw new ArgumentException("Probability and damage counts must match and contain at least two points");

            double prevProb = probabilities[0];
            if (prevProb < 0.0 || prevProb > 1.0)
                throw new ArgumentException("Probabilities must be between 0 and 1");

            // Ensure probabilities are non-increasing and within [0,1]
            for (int i = 1; i < probabilities.Length; i++)
            {
                double p = probabilities[i];
                if (p > prevProb)
                    throw new ArgumentException("Probabilities must be in non-increasing order");
                if (p < 0.0 || p > 1.0)
                    throw new ArgumentException("Probabilities must be between 0 and 1");
                prevProb = p;
            }

            double sum = 0.0;
            for (int i = 0; i < probabilities.Length - 1; i++)
            {
                sum += 0.5 * (damages[i] + damages[i + 1]) * (probabilities[i] - probabilities[i + 1]);
            }
            return sum;
        }
    }
}
