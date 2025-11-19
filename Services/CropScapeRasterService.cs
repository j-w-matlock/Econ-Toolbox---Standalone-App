using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BitMiracle.LibTiff.Classic;

namespace EconToolbox.Desktop.Services
{
    public sealed class CropScapeRasterService
    {
        private const double DefaultPixelSizeMeters = 30.0;
        private const double SquareMetersPerAcre = 4046.8564224;
        private const int GeoKeyDirectoryTag = 34735;
        private const int GeoDoubleParamsTag = 34736;
        private const ushort ProjLinearUnitsGeoKey = 2052;
        private const ushort ProjLinearUnitSizeGeoKey = 2053;

        public IReadOnlyList<CropScapeClassArea> ReadClassAreas(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("A file path is required.", nameof(path));
            }

            if (!File.Exists(path))
            {
                throw new FileNotFoundException("The CropScape raster file could not be found.", path);
            }

            using var tiff = Tiff.Open(path, "r");
            if (tiff == null)
            {
                throw new InvalidOperationException("Unable to open the CropScape raster. The file may be corrupt or in an unsupported format.");
            }

            int width = tiff.GetField(TiffTag.IMAGEWIDTH)?[0].ToInt() ?? throw new InvalidOperationException("The raster does not specify an image width.");
            int height = tiff.GetField(TiffTag.IMAGELENGTH)?[0].ToInt() ?? throw new InvalidOperationException("The raster does not specify an image height.");

            int bitsPerSample = tiff.GetField(TiffTag.BITSPERSAMPLE)?[0].ToInt() ?? 8;
            int samplesPerPixel = tiff.GetField(TiffTag.SAMPLESPERPIXEL)?[0].ToInt() ?? 1;

            if (bitsPerSample != 8)
            {
                throw new NotSupportedException($"Expected an 8-bit CropScape raster but encountered {bitsPerSample}-bit data.");
            }

            if (samplesPerPixel != 1)
            {
                throw new NotSupportedException($"Expected a single-band CropScape raster but encountered {samplesPerPixel} bands.");
            }

            (double pixelWidthMeters, double pixelHeightMeters) = ReadPixelSizeInMeters(tiff);

            double acresPerPixel = (pixelWidthMeters * pixelHeightMeters) / SquareMetersPerAcre;

            IReadOnlyDictionary<int, string> metadataNames = CropScapeLegend.ReadMetadataNames(tiff);

            var counts = new Dictionary<int, long>();
            int scanlineSize = tiff.ScanlineSize();
            byte[] buffer = new byte[scanlineSize];

            for (int row = 0; row < height; row++)
            {
                tiff.ReadScanline(buffer, row);
                for (int column = 0; column < width; column++)
                {
                    int index = column;
                    int value = buffer[index];

                    if (counts.TryGetValue(value, out long existing))
                    {
                        counts[value] = existing + 1;
                    }
                    else
                    {
                        counts[value] = 1;
                    }
                }
            }

            var results = counts
                .Select(kvp =>
                {
                    int code = kvp.Key;
                    long pixelCount = kvp.Value;
                    double acres = pixelCount * acresPerPixel;
                    string name = CropScapeLegend.Lookup(code, metadataNames);
                    return new CropScapeClassArea(code, name, pixelCount, acres);
                })
                .OrderByDescending(area => area.Acres)
                .ToList();

            return results;
        }
        private static (double WidthMeters, double HeightMeters) ReadPixelSizeInMeters(Tiff tiff)
        {
            const int ModelPixelScaleTag = 33550;

            double pixelWidth = DefaultPixelSizeMeters;
            double pixelHeight = DefaultPixelSizeMeters;

            FieldValue[]? scaleField = tiff.GetField((TiffTag)ModelPixelScaleTag);
            if (scaleField != null)
            {
                foreach (FieldValue value in scaleField)
                {
                    double[]? scaleValues = value.ToDoubleArray();
                    if (scaleValues != null && scaleValues.Length >= 2 && scaleValues[0] > 0 && scaleValues[1] > 0)
                    {
                        pixelWidth = scaleValues[0];
                        pixelHeight = scaleValues[1];
                        break;
                    }
                }
            }

            double unitsToMeters = ResolveLinearUnitToMeters(tiff);
            return (pixelWidth * unitsToMeters, pixelHeight * unitsToMeters);
        }

        private static double ResolveLinearUnitToMeters(Tiff tiff)
        {
            (short[]? directory, double[]? doubleParams) = ReadGeoKeyMetadata(tiff);
            if (directory == null || directory.Length < 4)
            {
                return 1.0;
            }

            int keyCount = directory[3];
            int offset = 4;

            double? linearUnitSize = null;
            int? linearUnitCode = null;

            for (int i = 0; i < keyCount && offset + 3 < directory.Length; i++, offset += 4)
            {
                ushort keyId = unchecked((ushort)directory[offset]);
                ushort tiffTagLocation = unchecked((ushort)directory[offset + 1]);
                ushort count = unchecked((ushort)directory[offset + 2]);
                ushort valueOffset = unchecked((ushort)directory[offset + 3]);

                if (keyId == ProjLinearUnitSizeGeoKey && count == 1 && tiffTagLocation == GeoDoubleParamsTag && doubleParams != null)
                {
                    if (valueOffset >= 0 && valueOffset < doubleParams.Length)
                    {
                        double value = doubleParams[valueOffset];
                        if (value > 0)
                        {
                            linearUnitSize = value;
                        }
                    }
                }
                else if (keyId == ProjLinearUnitsGeoKey && count == 1 && tiffTagLocation == 0)
                {
                    linearUnitCode = valueOffset;
                }
            }

            if (linearUnitSize.HasValue)
            {
                return linearUnitSize.Value;
            }

            if (linearUnitCode.HasValue)
            {
                return LookupLinearUnitScale(linearUnitCode.Value);
            }

            return 1.0;
        }

        private static (short[]? KeyDirectory, double[]? DoubleParams) ReadGeoKeyMetadata(Tiff tiff)
        {
            short[]? directory = null;
            FieldValue[]? directoryField = tiff.GetField((TiffTag)GeoKeyDirectoryTag);
            if (directoryField != null)
            {
                foreach (FieldValue value in directoryField)
                {
                    short[]? candidate = value.ToShortArray();
                    if (candidate != null && candidate.Length >= 4)
                    {
                        directory = candidate;
                        break;
                    }
                }
            }

            double[]? doubleParams = null;
            FieldValue[]? doubleField = tiff.GetField((TiffTag)GeoDoubleParamsTag);
            if (doubleField != null)
            {
                foreach (FieldValue value in doubleField)
                {
                    double[]? candidate = value.ToDoubleArray();
                    if (candidate != null && candidate.Length > 0)
                    {
                        doubleParams = candidate;
                        break;
                    }
                }
            }

            return (directory, doubleParams);
        }

        private static double LookupLinearUnitScale(int unitCode)
        {
            return unitCode switch
            {
                9001 => 1.0, // meter
                9002 => 0.3048, // foot
                9003 => 0.9144, // yard
                9005 => 0.201168, // chain
                9036 => 0.3048006096012192, // US survey foot
                9037 => 0.0254, // inch
                _ => 1.0,
            };
        }
    }

    public record CropScapeClassArea(int Code, string Name, long PixelCount, double Acres);
}
