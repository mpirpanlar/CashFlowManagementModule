namespace Sentez.LodgingNetCoreManagementModule
{
    public enum MenuSubRoots : short
    {
        Descriptions = 1000, 
        Transactions, 
        Operations, 
        Reports, 
        Settings 
    }
    public enum LodgingNetCoreManagementModuleSecurityItems : short
    {
        None,
        VariantItemMark,
        InventoryMark,
        FaultTaskControl,
        MonthlyActualCost,
        OrderAllHistory
    }
    public enum LodgingNetCoreManagementModuleSecuritySubItems : short
    {
        None
    }
}
