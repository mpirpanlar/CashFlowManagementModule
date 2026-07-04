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
        FixedPaymentImport = 4,
        CreditCardStatementSpendingImport = 5,
        CurrentAccountAgingImport = 6
    }

    public enum CashFlowManagementModuleSecuritySubItems : short
    {
        None = 0
    }
}
