namespace EconToolbox.Desktop.Models
{
    public static class StorageCostModel
    {
        public static double Compute(double totalCost, double storagePrice, double storageReallocated, double totalUsableStorage)
        {
            if (totalUsableStorage <= 0)
                throw new System.ArgumentException("Total usable storage must be greater than zero");

            return (totalCost - storagePrice) * storageReallocated / totalUsableStorage;
        }
    }
}
