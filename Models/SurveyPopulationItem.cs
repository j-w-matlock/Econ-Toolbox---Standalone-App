namespace EconToolbox.Desktop.Models
{
    public sealed class SurveyPopulationItem
    {
        public SurveyPopulationItem(int rowNumber, string identifier)
        {
            RowNumber = rowNumber;
            Identifier = identifier;
        }

        public int RowNumber { get; }

        public string Identifier { get; }
    }
}
