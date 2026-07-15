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
        CurrentAccountAgingImport = 6,
        CollectionOrderLineApproval = 7,
        CollectionOrderHeaderApproval = 8,
        FixedCollectionImport = 9,
        CurrentAccountCollectionAgingImport = 10,
        PosDeductionType = 11,
        PosStatementAnalysis = 12,
        PosSettlementCollectionImport = 13
    }

    public enum CashFlowManagementModuleSecuritySubItems : short
    {
        None = 0
    }
}
