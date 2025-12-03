using System.Globalization;
using System.Windows.Data;
using EconToolbox.Desktop.Converters;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EconToolbox.Desktop.Tests;

[TestClass]
public class ConverterTests
{
    [TestMethod]
    public void CurrencyConverter_FormatsNullAndNumbers()
    {
        var converter = new CurrencyConverter();
        var culture = CultureInfo.GetCultureInfo("en-US");

        var nullResult = converter.Convert(null, typeof(string), null, culture);
        var formatted = converter.Convert(1234.5, typeof(string), null, culture);

        Assert.AreEqual(string.Empty, nullResult);
        Assert.AreEqual("$1,234.50", formatted);
    }

    [TestMethod]
    public void CurrencyConverter_ConvertBack_HandlesEmptyStringAndDecimalTarget()
    {
        var converter = new CurrencyConverter();
        var culture = CultureInfo.GetCultureInfo("en-US");

        var emptyResult = converter.ConvertBack(string.Empty, typeof(decimal), null, culture);
        var parsed = converter.ConvertBack("$2,500.40", typeof(decimal), null, culture);

        Assert.AreEqual(0m, emptyResult);
        Assert.AreEqual(2500.40m, parsed);
    }

    [TestMethod]
    public void CurrencyConverter_Convert_SupportsVeryLargeValues()
    {
        var converter = new CurrencyConverter();
        var culture = CultureInfo.InvariantCulture;

        var result = converter.Convert(double.MaxValue, typeof(string), null, culture);

        Assert.IsInstanceOfType(result, typeof(string));
    }

    [TestMethod]
    public void SafeNumberConverter_RoundsToIntegers()
    {
        var converter = new SafeNumberConverter();
        var culture = CultureInfo.InvariantCulture;

        var rounded = converter.ConvertBack("3.6", typeof(int), null, culture);
        var unchanged = converter.ConvertBack("7", typeof(double), null, culture);

        Assert.AreEqual(4, rounded);
        Assert.AreEqual(7d, unchanged);
    }

    [TestMethod]
    public void SafeNumberConverter_RespectsCultureSeparators()
    {
        var converter = new SafeNumberConverter();
        var culture = CultureInfo.GetCultureInfo("fr-FR");

        var parsed = converter.ConvertBack("1,5", typeof(double), null, culture);
        var rejected = converter.ConvertBack("not-a-number", typeof(double), null, culture);

        Assert.AreEqual(1.5d, parsed);
        Assert.AreEqual(Binding.DoNothing, rejected);
    }

    [TestMethod]
    public void NullOrEmptyToValueConverter_UsesFallbackForEmpty()
    {
        var converter = new NullOrEmptyToValueConverter();
        var culture = CultureInfo.InvariantCulture;

        var empty = converter.Convert("   ", typeof(string), "fallback", culture);
        var value = converter.Convert("data", typeof(string), "fallback", culture);

        Assert.AreEqual("fallback", empty);
        Assert.AreEqual("data", value);
    }
}
