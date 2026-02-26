namespace EconToolbox.Desktop.Models
{
    public sealed class AttributeStatistic
    {
        public AttributeStatistic(string metric, string value, string? valueTooltip = null)
        {
            Metric = metric;
            Value = value;
            ValueTooltip = valueTooltip;
        }

        public string Metric { get; }

        public string Value { get; }

        public string? ValueTooltip { get; }
    }
}
