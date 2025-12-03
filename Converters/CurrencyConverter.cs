using System.Globalization;

namespace EconToolbox.Desktop.Converters
{
    public class CurrencyConverter : NumberConverterBase
    {
        public CurrencyConverter()
        {
            FormatString = "C2";
            ParsingStyles = NumberStyles.Currency | NumberStyles.AllowThousands;
            TreatEmptyStringAsZero = true;
        }
    }
}
