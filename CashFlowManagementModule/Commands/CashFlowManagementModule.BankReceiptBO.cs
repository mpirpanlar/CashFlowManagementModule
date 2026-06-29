using CashFlowManagementModule.BoExtensions;

using Sentez.Data.BusinessObjects;

namespace Sentez.CashFlowManagementModule
{
    public partial class CashFlowManagementModule
    {
        void RegisterBankReceiptBoHooks()
        {
            BusinessObjectBase.AddCustomInit("BankReceiptBO", PaymentOrderBankReceiptBo_Init);
            BusinessObjectBase.AddCustomInit("BankReceiptBO", BankReceiptBo_CreditCardInit);
        }

        void BankReceiptBo_CreditCardInit(BusinessObjectBase bo, BoParam parameter)
        {
            BankReceiptCreditCardHelper.EnsureBankReceiptItemMetaDataFields();
            bo.ValueFiller.AddRule("Erp_BankReceiptItem", BankReceiptCreditCardHelper.FieldInstallmentCount, (short)1);
        }

        void PaymentOrderBankReceiptBo_Init(BusinessObjectBase bo, BoParam parameter)
        {
            if (parameter?.Type != BankReceiptPaymentOrderHelper.ReceiptType) return;

            BankReceiptPaymentOrderHelper.DisableItemIsApprovedFkSync(bo);
        }
    }
}
