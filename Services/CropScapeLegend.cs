using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml.Linq;
using BitMiracle.LibTiff.Classic;

namespace EconToolbox.Desktop.Services
{
    public static class CropScapeLegend
    {
        private static readonly IReadOnlyDictionary<int, string> FallbackNames = new Dictionary<int, string>
        {
            [0] = "Background / No Data",
            [1] = "Corn",
            [2] = "Cotton",
            [3] = "Rice",
            [4] = "Sorghum",
            [5] = "Soybeans",
            [6] = "Sunflower",
            [7] = "Peanuts",
            [8] = "Tobacco",
            [9] = "Sweet corn",
            [10] = "Pop or ornamental corn",
            [11] = "Mint",
            [12] = "Barley",
            [13] = "Other small grains",
            [14] = "Double crop: winter wheat / soybeans",
            [15] = "Double crop: winter wheat / corn",
            [16] = "Double crop: barley / soybeans",
            [17] = "Double crop: winter wheat / sorghum",
            [18] = "Double crop: winter wheat / cotton",
            [19] = "Double crop: winter wheat / sunflower",
            [20] = "Double crop: winter wheat / other grain",
            [23] = "Spring wheat",
            [24] = "Winter wheat",
            [25] = "Other small grains",
            [26] = "Dbl crop: winter wheat / cotton",
            [27] = "Dbl crop: oats / corn",
            [28] = "Dbl crop: barley / corn",
            [29] = "Dbl crop: oats / soybeans",
            [30] = "Dbl crop: barley / soybeans",
            [36] = "Alfalfa",
            [37] = "Other hay / non-alfalfa",
            [39] = "Sugarbeets",
            [40] = "Dry beans",
            [41] = "Potatoes",
            [42] = "Other crops",
            [43] = "Sugarcane",
            [44] = "Sweet potatoes",
            [45] = "Miscellaneous vegetables & fruits",
            [46] = "Watermelons",
            [47] = "Onions",
            [48] = "Cucumbers",
            [49] = "Chick peas",
            [50] = "Lentils",
            [51] = "Peas",
            [52] = "Tomatoes",
            [53] = "Caneberries",
            [54] = "Hops",
            [55] = "Herbs",
            [56] = "Clover / wildflowers",
            [57] = "Sod / grass seed",
            [58] = "Switchgrass",
            [59] = "Fallow / idle cropland",
            [60] = "Pasture / grass",
            [61] = "Forest",
            [111] = "Open water",
            [112] = "Perennial ice / snow",
            [121] = "Developed / open space",
            [122] = "Developed / low intensity",
            [123] = "Developed / medium intensity",
            [124] = "Developed / high intensity",
            [131] = "Barren",
            [141] = "Deciduous forest",
            [142] = "Evergreen forest",
            [143] = "Mixed forest",
            [152] = "Shrubland",
            [176] = "Grassland",
            [190] = "Woody wetlands",
            [195] = "Herbaceous wetlands",
        };

        public static IReadOnlyDictionary<int, string> ReadMetadataNames(Tiff tiff)
        {
            try
            {
                FieldValue[]? fieldValues = tiff.GetField((TiffTag)42112);
                if (fieldValues == null || fieldValues.Length == 0)
                {
                    return new Dictionary<int, string>();
                }

                string? metadata = fieldValues[0].ToString();
                if (string.IsNullOrWhiteSpace(metadata))
                {
                    return new Dictionary<int, string>();
                }

                return ParseMetadata(metadata);
            }
            catch
            {
                return new Dictionary<int, string>();
            }
        }

        public static string Lookup(int code, IReadOnlyDictionary<int, string> metadataNames)
        {
            if (metadataNames != null && metadataNames.TryGetValue(code, out string? metadataName) && !string.IsNullOrWhiteSpace(metadataName))
            {
                return metadataName;
            }

            if (FallbackNames.TryGetValue(code, out string? fallback))
            {
                return fallback;
            }

            return $"Class {code}";
        }

        private static IReadOnlyDictionary<int, string> ParseMetadata(string metadata)
        {
            var results = new Dictionary<int, string>();

            if (metadata.IndexOf('<') >= 0)
            {
                TryParseXml(metadata, results);
            }
            else
            {
                TryParseDelimited(metadata, results);
            }

            return results;
        }

        private static void TryParseXml(string metadata, IDictionary<int, string> results)
        {
            try
            {
                var document = XDocument.Parse(metadata);
                foreach (var item in document.Descendants("Item"))
                {
                    string? name = item.Attribute("name")?.Value;
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        continue;
                    }

                    if (string.Equals(name, "Class_Names", StringComparison.OrdinalIgnoreCase))
                    {
                        string[] entries = item.Value.Split(new[] { '|', ',' }, StringSplitOptions.RemoveEmptyEntries);
                        for (int index = 0; index < entries.Length; index++)
                        {
                            string entry = entries[index].Trim();
                            if (entry.Length > 0)
                            {
                                results[index] = entry;
                            }
                        }
                        continue;
                    }

                    if (TryParseCode(name, out int code))
                    {
                        string value = item.Value.Trim();
                        if (value.Length > 0)
                        {
                            results[code] = value;
                        }
                    }
                }
            }
            catch
            {
                // Ignore parsing issues and fall back.
            }
        }

        private static void TryParseDelimited(string metadata, IDictionary<int, string> results)
        {
            string[] tokens = metadata.Split(new[] { '\0', '|', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string token in tokens)
            {
                string[] parts = token.Split('=', 2);
                if (parts.Length != 2)
                {
                    continue;
                }

                string key = parts[0].Trim();
                string value = parts[1].Trim();

                if (TryParseCode(key, out int code) && value.Length > 0)
                {
                    results[code] = value;
                }
            }
        }

        private static bool TryParseCode(string key, out int code)
        {
            string[] prefixes =
            {
                "Class_",
                "CLASS_",
                "ClassName_",
                "CLASSNAME_",
                "Class_Name_",
                "CLASS_NAME_"
            };

            foreach (string prefix in prefixes)
            {
                if (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    string suffix = key.Substring(prefix.Length);
                    if (int.TryParse(suffix, NumberStyles.Integer, CultureInfo.InvariantCulture, out code))
                    {
                        return true;
                    }
                }
            }

            code = default;
            return false;
        }
    }
}
