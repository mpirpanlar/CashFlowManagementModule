using CashFlowManagementModule.BoExtensions;

using Sentez.Data.BusinessObjects;

namespace Sentez.CashFlowManagementModule
{
    public partial class CashFlowManagementModule
    {
        void RegisterBankReceiptBoHooks()
        {
            BusinessObjectBase.AddCustomInit("BankReceiptBO", PaymentOrderBankReceiptBo_Init);
        }

        void PaymentOrderBankReceiptBo_Init(BusinessObjectBase bo, BoParam parameter)
        {
            if (parameter?.Type != BankReceiptPaymentOrderHelper.ReceiptType) return;

            BankReceiptPaymentOrderHelper.DisableItemIsApprovedFkSync(bo);
        }
    }
}
