namespace EconToolbox.Desktop.Models
{
    public sealed class AttributeStatistic
    {
        public AttributeStatistic(string metric, string value)
        {
            Metric = metric;
            Value = value;
        }

        public string Metric { get; }

        public string Value { get; }
    }
}
