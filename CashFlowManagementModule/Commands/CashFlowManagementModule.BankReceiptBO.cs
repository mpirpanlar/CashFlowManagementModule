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
            BusinessObjectBase.AddCustomInit("BankReceiptBO", BankReceiptBo_FixedPaymentInit);
        }

        void BankReceiptBo_FixedPaymentInit(BusinessObjectBase bo, BoParam parameter)
        {
            if (bo?.Lookups == null || !BankReceiptFixedPaymentHelper.IsBankReceiptItemFieldAvailable())
                return;

            bo.Lookups.AddLookUp(
                "Erp_BankReceiptItem",
                BankReceiptFixedPaymentHelper.FieldFixedPaymentTypeId,
                true,
                MetaFixedPaymentTypeHelper.TableName,
                MetaFixedPaymentTypeHelper.KeyField,
                MetaFixedPaymentTypeHelper.KeyField,
                "FixedPaymentTypeName",
                "FixedPaymentTypeName");
        }

        void BankReceiptBo_CreditCardInit(BusinessObjectBase bo, BoParam parameter)
        {
            BankReceiptCreditCardHelper.EnsureBankReceiptItemMetaDataFields();
            bo.ValueFiller.AddRule("Erp_BankReceiptItem", BankReceiptCreditCardHelper.FieldInstallmentCount, (short)1);
        }

        void PaymentOrderBankReceiptBo_Init(BusinessObjectBase bo, BoParam parameter)
        {
            if (parameter?.Type == BankReceiptPaymentOrderHelper.ReceiptType)
                BankReceiptPaymentOrderHelper.DisableItemIsApprovedFkSync(bo);

            if (parameter?.Type == BankReceiptCollectionOrderHelper.ReceiptType)
                BankReceiptCollectionOrderHelper.DisableItemIsApprovedFkSync(bo);

            if (parameter?.Type == BankReceiptPaymentOrderHelper.ReceiptType
                || parameter?.Type == BankReceiptCollectionOrderHelper.ReceiptType)
            {
                BankReceiptItemAuditHelper.EnsureAuditLookups(bo);
                BankReceiptItemAccessCodeHelper.DisableItemAccessCodeConditionalKeyField(bo);
            }
        }
    }
}
