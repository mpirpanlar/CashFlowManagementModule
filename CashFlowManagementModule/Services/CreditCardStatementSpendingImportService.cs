using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Text;

using CashFlowManagementModule.BoExtensions;

using Sentez.Common.SystemServices;
using Sentez.Data.BusinessObjects;
using Sentez.Localization;

namespace CashFlowManagementModule.Services
{
    public sealed class CreditCardStatementSpendingImportResult
    {
        public int AddedCount { get; set; }
        public int UpdatedCount { get; set; }
        public int SkippedApprovedCount { get; set; }
        public int SkippedNoPeriodCount { get; set; }
        public int SkippedZeroAmountCount { get; set; }
        public string Message { get; set; }
    }

    public static class CreditCardStatementSpendingImportService
    {
        public static CreditCardStatementSpendingImportResult Import(
            BusinessObjectBase businessObject,
            LiveSession session,
            DateTime receiptDate)
        {
            var result = new CreditCardStatementSpendingImportResult();

            if (businessObject?.Data == null || businessObject.CurrentRow?.Row == null)
            {
                result.Message = SLanguage.GetString("Fiş bilgisi bulunamadı.");
                return result;
            }

            if (session?.ActiveCompany?.RecId == null)
            {
                result.Message = SLanguage.GetString("Şirket bilgisi bulunamadı.");
                return result;
            }

            if (receiptDate == DateTime.MinValue)
            {
                result.Message = SLanguage.GetString("Fiş tarihi geçersiz.");
                return result;
            }

            DataRow headerRow = businessObject.CurrentRow.Row;
            DataTable itemTable = businessObject.Data.Tables["Erp_BankReceiptItem"];
            if (itemTable == null)
            {
                result.Message = SLanguage.GetString("Fiş detay tablosu bulunamadı.");
                return result;
            }

            int companyId = session.ActiveCompany.RecId.Value;
            CashFlowDbContext context = CashFlowDbContext.FromSession(session);
            IList<long> bankAccountIds = CreditCardStatementDataService.LoadCreditCardBankAccountIds(context, companyId);
            if (bankAccountIds == null || bankAccountIds.Count == 0)
            {
                result.Message = SLanguage.GetString("Aktarılacak kredi kartı hesabı bulunamadı.");
                return result;
            }

            var noPeriodAccounts = new List<string>();
            receiptDate = receiptDate.Date;

            foreach (long bankAccountId in bankAccountIds)
            {
                if (!CreditCardStatementDataService.HasActivePeriods(context, bankAccountId))
                {
                    result.SkippedNoPeriodCount++;
                    noPeriodAccounts.Add(CreditCardStatementDataService.GetBankAccountDisplayName(context, bankAccountId));
                    continue;
                }

                decimal? spendingTotal = CreditCardPeriodPaymentSummaryService.TryGetPeriodSpendingTotal(
                    session,
                    bankAccountId,
                    receiptDate,
                    out CreditCardPeriodInfo matchedPeriod);

                if (!spendingTotal.HasValue || matchedPeriod == null)
                {
                    result.SkippedNoPeriodCount++;
                    noPeriodAccounts.Add(CreditCardStatementDataService.GetBankAccountDisplayName(context, bankAccountId));
                    continue;
                }

                if (spendingTotal.Value <= 0m)
                {
                    result.SkippedZeroAmountCount++;
                    continue;
                }

                string accountName = CreditCardStatementDataService.GetBankAccountDisplayName(context, bankAccountId);
                string explanation = BuildExplanation(accountName, matchedPeriod);

                if (TryFindItemRow(itemTable, bankAccountId, out DataRow existingRow, out bool isApproved))
                {
                    if (isApproved)
                    {
                        result.SkippedApprovedCount++;
                        continue;
                    }

                    ApplyLineValues(existingRow, spendingTotal.Value, matchedPeriod.PaymentDueDate, explanation);
                    result.UpdatedCount++;
                    continue;
                }

                DataRow itemRow = itemTable.NewRow();
                itemRow.SetParentRow(headerRow);
                itemTable.Rows.Add(itemRow);
                itemRow["BankAccountId"] = bankAccountId;
                itemRow["Credit"] = spendingTotal.Value;
                itemRow["IsApproved"] = (byte)0;
                ApplyLineValues(itemRow, spendingTotal.Value, matchedPeriod.PaymentDueDate, explanation);
                result.AddedCount++;
            }

            result.Message = BuildResultMessage(result, noPeriodAccounts);
            return result;
        }

        static void ApplyLineValues(DataRow itemRow, decimal amount, DateTime paymentDueDate, string explanation)
        {
            itemRow["Credit"] = amount;

            if (ItemRowHasColumn(itemRow, PaymentOrderUdFields.PaymentDate))
                itemRow[PaymentOrderUdFields.PaymentDate] = paymentDueDate;

            if (ItemRowHasColumn(itemRow, BankReceiptCreditCardHelper.FieldInstallmentCount)
                && (itemRow.IsNull(BankReceiptCreditCardHelper.FieldInstallmentCount)
                    || Convert.ToInt16(itemRow[BankReceiptCreditCardHelper.FieldInstallmentCount]) < 1))
            {
                itemRow[BankReceiptCreditCardHelper.FieldInstallmentCount] = (short)1;
            }

            if (ItemRowHasColumn(itemRow, "Explanation"))
                itemRow["Explanation"] = explanation;
        }

        static bool ItemRowHasColumn(DataRow itemRow, string columnName)
        {
            return itemRow?.Table != null && itemRow.Table.Columns.Contains(columnName);
        }

        static string BuildExplanation(string accountName, CreditCardPeriodInfo period)
        {
            string monthText = period.PeriodMonth.ToString("00", CultureInfo.InvariantCulture);
            string yearText = period.PeriodYear.ToString(CultureInfo.InvariantCulture);
            string statementDate = period.StatementDate.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);

            return string.Format(
                SLanguage.GetString("{0} - {1}/{2} Ekstre Harcama (Kesim: {3})"),
                accountName,
                monthText,
                yearText,
                statementDate);
        }

        static bool TryFindItemRow(DataTable itemTable, long bankAccountId, out DataRow itemRow, out bool isApproved)
        {
            itemRow = null;
            isApproved = false;

            foreach (DataRow row in itemTable.Rows)
            {
                if (row.RowState == DataRowState.Deleted) continue;
                if (row.IsNull("BankAccountId")) continue;
                if (Convert.ToInt64(row["BankAccountId"]) != bankAccountId) continue;

                itemRow = row;
                isApproved = BankReceiptPaymentOrderHelper.GetApprovedValue(row) == 1;
                return true;
            }

            return false;
        }

        static string BuildResultMessage(CreditCardStatementSpendingImportResult result, IList<string> noPeriodAccounts)
        {
            var message = new StringBuilder();
            AppendMessageLine(
                message,
                string.Format(
                    SLanguage.GetString("{0} satır eklendi, {1} satır güncellendi."),
                    result.AddedCount,
                    result.UpdatedCount));

            if (result.SkippedApprovedCount > 0)
            {
                AppendMessageLine(
                    message,
                    string.Format(
                        SLanguage.GetString("{0} onaylı satır atlandı."),
                        result.SkippedApprovedCount));
            }

            if (result.SkippedZeroAmountCount > 0)
            {
                AppendMessageLine(
                    message,
                    string.Format(
                        SLanguage.GetString("{0} kart için harcama bulunamadı."),
                        result.SkippedZeroAmountCount));
            }

            if (result.SkippedNoPeriodCount > 0)
            {
                AppendMessageLine(
                    message,
                    string.Format(
                        SLanguage.GetString("{0} kart için uygun ekstre dönemi bulunamadı."),
                        result.SkippedNoPeriodCount));

                if (noPeriodAccounts != null && noPeriodAccounts.Count > 0)
                {
                    foreach (string accountName in noPeriodAccounts)
                        AppendMessageLine(message, accountName);
                }
            }

            return message.ToString().Trim();
        }

        static void AppendMessageLine(StringBuilder message, string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return;

            if (message.Length > 0)
                message.Append(Environment.NewLine);

            message.Append(line);
        }
    }
}
