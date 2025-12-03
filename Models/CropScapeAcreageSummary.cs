using System;
using System.Collections.Generic;
using System.Linq;
using EconToolbox.Desktop.ViewModels;

namespace EconToolbox.Desktop.Models
{
    public class CropScapeAcreageSummary : BaseViewModel
    {
        private double _percentOfTotal;

        public CropScapeAcreageSummary(int code, string name, long pixelCount, double acres, double percentOfTotal)
        {
            if (pixelCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(pixelCount), "Pixel count cannot be negative.");
            }

            if (!double.IsFinite(acres) || acres < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(acres), "Acreage must be a finite, non-negative value.");
            }

            Code = code;
            Name = name;
            PixelCount = pixelCount;
            Acres = acres;
            PercentOfTotal = percentOfTotal;
        }

        public int Code { get; }

        public string Name { get; }

        public long PixelCount { get; }

        public double Acres { get; }

        public double PercentOfTotal
        {
            get => _percentOfTotal;
            private set
            {
                if (Math.Abs(_percentOfTotal - value) < 1e-6)
                {
                    return;
                }

                _percentOfTotal = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PercentDisplay));
            }
        }

        public string AcresDisplay => Acres.ToString("N1");

        public string PercentDisplay => PercentOfTotal.ToString("P1");

        public void UpdateShare(double totalAcres)
        {
            double updated = totalAcres > 0 ? Acres / totalAcres : 0;
            PercentOfTotal = updated;
        }

        public static IReadOnlyList<CropScapeAcreageSummary> FromAreas(
            IEnumerable<CropScapeClassArea> areas,
            out double totalAcres)
        {
            if (areas == null)
            {
                throw new ArgumentNullException(nameof(areas));
            }

            var areaList = areas.ToList();
            totalAcres = areaList.Sum(area => area.Acres);
            double total = totalAcres;

            return areaList
                .Select(area => new CropScapeAcreageSummary(
                    area.Code,
                    area.Name,
                    area.PixelCount,
                    area.Acres,
                    total > 0 ? area.Acres / total : 0))
                .ToList();
        }
    }
}
