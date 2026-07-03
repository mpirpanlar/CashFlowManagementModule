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
        None = 0,
        PaymentOrderLineApproval = 1,
        PaymentOrderHeaderApproval = 2,
        FixedPaymentType = 3,
        FixedPaymentImport = 4
    }

    public enum CashFlowManagementModuleSecuritySubItems : short
    {
        None = 0
    }
}
