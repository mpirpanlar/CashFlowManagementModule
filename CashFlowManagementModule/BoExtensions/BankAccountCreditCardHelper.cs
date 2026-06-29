using CashFlowManagementModule.Services;

using Sentez.Data.MetaData;
using Sentez.Data.Tools;
using Sentez.Localization;

using System;
using System.Data;

namespace CashFlowManagementModule.BoExtensions
{
    public static class BankAccountCreditCardHelper
    {
        public const string PeriodTableName = "Erp_BankAccountCreditCardPeriod";
        public const string BankAccountFkName = "FK_Erp_BankAccountCreditCardPeriod_Erp_BankAccount";

        public const string FieldIssueDate = "UD_CreditCardIssueDate";
        public const string FieldExpiryMonth = "UD_CreditCardExpiryMonth";
        public const string FieldExpiryYear = "UD_CreditCardExpiryYear";
        public const string FieldStatementCutDate = "UD_StatementCutDate";
        public const string FieldPaymentDueDate = "UD_PaymentDueDate";
        public const string FieldPaymentDueDays = "PaymentDueDays";
        public const string FieldPeriodSpendingTotal = "PeriodSpendingTotal";
        public const string FieldPeriodRefundTotal = "PeriodRefundTotal";
        public const string FieldPeriodCardPaymentTotal = "PeriodCardPaymentTotal";
        public const string FieldPeriodTotalCreditLimit = "PeriodTotalCreditLimit";
        public const string FieldPeriodRemainingCreditLimit = "PeriodRemainingCreditLimit";
        public const string FieldUsedCreditLimit = "UsedCreditLimit";
        public const string FieldRemainingCreditLimit = "RemainingCreditLimit";
        public const string DateDisplayMask = "dd.MM.yyyy dddd";

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

            if (!Schema.Tables["Erp_BankAccount"].Fields.Contains(FieldIssueDate))
                CreatMetaDataFieldsService.CreatMetaDataFields("Erp_BankAccount", FieldIssueDate, SLanguage.GetString("Kart Veriliş Tarihi"), (byte)UdtType.UdtDate, (byte)FieldUsage.Date, (byte)EditorType.DateEditor, (byte)ValueInputMethod.FreeType, 0);
        }

        public static void EnsureBankAccountDataColumns(DataSet data)
        {
            if (data == null || !data.Tables.Contains("Erp_BankAccount"))
                return;

            EnsureDateColumn(data.Tables["Erp_BankAccount"], FieldIssueDate, SLanguage.GetString("Kart Veriliş Tarihi"));
        }

        static void EnsureDateColumn(DataTable table, string columnName, string caption)
        {
            if (table.Columns.Contains(columnName))
                return;

            table.Columns.Add(new DataColumn(columnName, typeof(DateTime))
            {
                Caption = caption,
                DefaultValue = DBNull.Value,
                AllowDBNull = true
            });
        }

        public static DateTime? GetIssueDate(DataRow bankAccountRow)
        {
            if (bankAccountRow == null || !bankAccountRow.Table.Columns.Contains(FieldIssueDate))
                return null;

            if (bankAccountRow.IsNull(FieldIssueDate))
                return null;

            return Convert.ToDateTime(bankAccountRow[FieldIssueDate]).Date;
        }

        public static DateTime? GetCardExpiryEndDate(DataRow bankAccountRow)
        {
            if (bankAccountRow == null ||
                !bankAccountRow.Table.Columns.Contains(FieldExpiryMonth) ||
                !bankAccountRow.Table.Columns.Contains(FieldExpiryYear) ||
                bankAccountRow.IsNull(FieldExpiryMonth) ||
                bankAccountRow.IsNull(FieldExpiryYear))
                return null;

            short month = Convert.ToInt16(bankAccountRow[FieldExpiryMonth]);
            short year = Convert.ToInt16(bankAccountRow[FieldExpiryYear]);
            return CreditCardStatementPeriodGeneratorService.SafeDate(
                year, month, DateTime.DaysInMonth(year, month));
        }
    }
}
