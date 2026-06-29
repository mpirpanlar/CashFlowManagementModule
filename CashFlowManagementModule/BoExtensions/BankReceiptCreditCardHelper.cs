using CashFlowManagementModule.Services;

using Sentez.Data.MetaData;
using Sentez.Data.Tools;
using Sentez.Localization;

namespace CashFlowManagementModule.BoExtensions
{
    public static class BankReceiptCreditCardHelper
    {
        public const string AllocationTableName = "Erp_BankAccountCreditCardPeriodAllocation";
        public const string FieldInstallmentCount = "UD_InstallmentCount";

        public static void EnsureBankReceiptItemMetaDataFields()
        {
            if (!Schema.Tables["Erp_BankReceiptItem"].Fields.Contains(FieldInstallmentCount))
                CreatMetaDataFieldsService.CreatMetaDataFields(
                    "Erp_BankReceiptItem",
                    FieldInstallmentCount,
                    SLanguage.GetString("Taksit Sayısı"),
                    (byte)UdtType.UdtInt16,
                    (byte)FieldUsage.None,
                    (byte)EditorType.TextEditor,
                    (byte)ValueInputMethod.FreeType,
                    0);
        }

        public static short GetInstallmentCount(System.Data.DataRow bankReceiptItemRow)
        {
            if (bankReceiptItemRow == null)
                return 1;

            if (bankReceiptItemRow.Table.Columns.Contains(FieldInstallmentCount)
                && !bankReceiptItemRow.IsNull(FieldInstallmentCount))
            {
                short value = System.Convert.ToInt16(bankReceiptItemRow[FieldInstallmentCount]);
                return value >= 1 ? value : (short)1;
            }

            return 1;
        }
    }
}
