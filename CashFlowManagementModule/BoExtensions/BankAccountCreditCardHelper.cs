using CashFlowManagementModule.Services;

using Sentez.Data.MetaData;
using Sentez.Data.Tools;
using Sentez.Localization;

namespace CashFlowManagementModule.BoExtensions
{
    public static class BankAccountCreditCardHelper
    {
        public const string PeriodTableName = "Erp_BankAccountCreditCardPeriod";
        public const string BankAccountFkName = "FK_Erp_BankAccountCreditCardPeriod_Erp_BankAccount";

        public const string FieldExpiryMonth = "UD_CreditCardExpiryMonth";
        public const string FieldExpiryYear = "UD_CreditCardExpiryYear";
        public const string FieldStatementCutDate = "UD_StatementCutDate";
        public const string FieldPaymentDueDate = "UD_PaymentDueDate";

        public static void EnsureBankAccountMetaDataFields()
        {
            if (!Schema.Tables["Erp_BankAccount"].Fields.Contains(FieldExpiryMonth))
                CreatMetaDataFieldsService.CreatMetaDataFields("Erp_BankAccount", FieldExpiryMonth, SLanguage.GetString("Son Kullanma Ay"), (byte)UdtType.UdtInt16, (byte)FieldUsage.None, (byte)EditorType.TextEditor, (byte)ValueInputMethod.FreeType, 0);

            if (!Schema.Tables["Erp_BankAccount"].Fields.Contains(FieldExpiryYear))
                CreatMetaDataFieldsService.CreatMetaDataFields("Erp_BankAccount", FieldExpiryYear, SLanguage.GetString("Son Kullanma Yıl"), (byte)UdtType.UdtInt16, (byte)FieldUsage.None, (byte)EditorType.TextEditor, (byte)ValueInputMethod.FreeType, 0);

            if (!Schema.Tables["Erp_BankAccount"].Fields.Contains(FieldStatementCutDate))
                CreatMetaDataFieldsService.CreatMetaDataFields("Erp_BankAccount", FieldStatementCutDate, SLanguage.GetString("Hesap Kesim Tarihi"), (byte)UdtType.UdtDate, (byte)FieldUsage.Date, (byte)EditorType.DateEditor, (byte)ValueInputMethod.FreeType, 0);

            if (!Schema.Tables["Erp_BankAccount"].Fields.Contains(FieldPaymentDueDate))
                CreatMetaDataFieldsService.CreatMetaDataFields("Erp_BankAccount", FieldPaymentDueDate, SLanguage.GetString("Son Ödeme Tarihi"), (byte)UdtType.UdtDate, (byte)FieldUsage.Date, (byte)EditorType.DateEditor, (byte)ValueInputMethod.FreeType, 0);
        }
    }
}
