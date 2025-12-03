using System;

namespace EconToolbox.Desktop.Models
{
    /// <summary>
    /// Represents a single CropScape class and its measured acreage.
    /// </summary>
    public sealed record CropScapeClassArea
    {
        public CropScapeClassArea(int code, string name, long pixelCount, double acres)
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
        }

        public int Code { get; }

        public string Name { get; }

        public long PixelCount { get; }

        public double Acres { get; }
    }
}
