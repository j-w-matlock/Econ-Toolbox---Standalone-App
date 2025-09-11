using System;
using System.Collections.Generic;

namespace EconToolbox.Desktop.Models
{
    public static class AnnualizerModel
    {
        public record Result(double Idc, double TotalInvestment, double Crf, double AnnualCost, double Bcr);

        public static Result Compute(
            double firstCost,
            double rate,
            double annualOm,
            double annualBenefits,
            IEnumerable<(double cost, int year)>? futureCosts = null,
            int periods = 1,
            int constructionMonths = 12)
        {
            double idc = InterestDuringConstructionModel.Compute(firstCost, rate, constructionMonths);

            double pvFuture = 0.0;
            int maxYear = 0;
            if (futureCosts != null)
            {
                foreach (var (cost, year) in futureCosts)
                {
                    double pvFactor = 1.0 / Math.Pow(1.0 + rate, year);
                    pvFuture += cost * pvFactor;
                    if (year > maxYear) maxYear = year;
                }
            }

            double totalInvestment = firstCost + idc + pvFuture;
            int finalPeriods = Math.Max(periods, Math.Max(1, maxYear));
            double crf = CapitalRecoveryModel.Calculate(rate, finalPeriods);
            double annualConstruction = totalInvestment * crf;
            double annualCost = annualConstruction + annualOm;
            double bcr = annualCost == 0 ? double.NaN : annualBenefits / annualCost;

            return new Result(idc, totalInvestment, crf, annualCost, bcr);
        }
    }
}

