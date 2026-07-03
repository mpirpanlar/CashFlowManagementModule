using Sentez.Data.MetaData;

namespace CashFlowManagementModule.BoExtensions
{
    public static class BankReceiptFixedPaymentHelper
    {
        public const string FieldFixedPaymentTypeId = "FixedPaymentTypeId";
        public const string FixedPaymentTypeFkName = "FK_Erp_BankReceiptItem_Meta_FixedPaymentType";

        public static bool IsBankReceiptItemFieldAvailable()
        {
            return Schema.Tables.Contains("Erp_BankReceiptItem")
                && Schema.Tables["Erp_BankReceiptItem"].Fields.Contains(FieldFixedPaymentTypeId);
        }
    }
}