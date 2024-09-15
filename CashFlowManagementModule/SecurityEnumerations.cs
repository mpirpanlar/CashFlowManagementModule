namespace Sentez.CashFlowManagementModule
{
    public enum MenuSubRoots : short
    {
        Descriptions = 1000, 
        Transactions, 
        Operations, 
        Reports, 
        Settings 
    }
    public enum CashFlowManagementModuleSecurityItems : short
    {
        None,
        VariantItemMark,
        InventoryMark,
        FaultTaskControl,
        MonthlyActualCost,
        OrderAllHistory
    }
    public enum CashFlowManagementModuleSecuritySubItems : short
    {
        None
    }
}
