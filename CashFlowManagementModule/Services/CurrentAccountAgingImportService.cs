using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Text;

using CashFlowManagementModule.BoExtensions;

using Prism.Ioc;

using Sentez.Common;
using Sentez.Common.Commands;
using Sentez.Common.SystemServices;
using Sentez.Data.BusinessObjects;
using Sentez.Data.Tools;
using Sentez.Localization;

namespace CashFlowManagementModule.Services
{
    public sealed class CurrentAccountAgingImportResult
    {
        public int AddedCount { get; set; }
        public int UpdatedCount { get; set; }
        public int SkippedApprovedCount { get; set; }
        public int SkippedZeroAmountCount { get; set; }
        public int SkippedNoAccountCount { get; set; }
        public string Message { get; set; }
    }

    public static class CurrentAccountAgingImportService
    {
        public static CurrentAccountAgingImportResult Import(
            BusinessObjectBase businessObject,
            LiveSession session,
            IContainerExtension container,
            DateTime reportDate,
            long defaultBankAccountId)
        {
            CurrentAccountAgingReportDataResult reportData = CurrentAccountAgingReportDataService.LoadAgingData(
                container,
                reportDate);
            if (!reportData.IsSuccess)
            {
                return new CurrentAccountAgingImportResult
                {
                    Message = string.IsNullOrWhiteSpace(reportData.ErrorMessage)
                        ? SLanguage.GetString("Yaşlandırma verisi alınamadı.")
                        : reportData.ErrorMessage
                };
            }

            var rows = new List<DataRow>();
            foreach (DataRow row in reportData.Data.Rows)
            {
                if (row.RowState != DataRowState.Deleted)
                    rows.Add(row);
            }

            return ImportSelectedRows(
                businessObject,
                reportDate,
                defaultBankAccountId,
                rows,
                reportData.AmountColumnName);
        }

        public static CurrentAccountAgingImportResult ImportSelectedRows(
            BusinessObjectBase businessObject,
            DateTime reportDate,
            long defaultBankAccountId,
            IEnumerable<DataRow> selectedRows,
            string amountColumnName,
            string defaultBankAccountCode = null)
        {
            var result = new CurrentAccountAgingImportResult();

            if (businessObject?.Data == null || businessObject.CurrentRow?.Row == null)
            {
                result.Message = SLanguage.GetString("Fiş bilgisi bulunamadı.");
                return result;
            }

            if (reportDate == DateTime.MinValue)
            {
                result.Message = SLanguage.GetString("Rapor tarihi geçersiz.");
                return result;
            }

            if (string.IsNullOrWhiteSpace(amountColumnName))
            {
                result.Message = SLanguage.GetString("<= 0 yaşlandırma kolonu bulunamadı.");
                return result;
            }

            DataTable itemTable = businessObject.Data.Tables["Erp_BankReceiptItem"];
            if (itemTable == null)
            {
                result.Message = SLanguage.GetString("Fiş detay tablosu bulunamadı.");
                return result;
            }

            if (selectedRows == null)
            {
                result.Message = SLanguage.GetString("Aktarılacak satır bulunamadı.");
                return result;
            }

            LiveSession session = SysMng.Instance.getSession() as LiveSession;
            long bankAccountId = ResolveDefaultBankAccountId(
                session,
                defaultBankAccountId,
                defaultBankAccountCode);

            reportDate = reportDate.Date;
            DateTime paymentDate = new DateTime(
                reportDate.Year,
                reportDate.Month,
                DateTime.DaysInMonth(reportDate.Year, reportDate.Month));
            DataRow headerRow = businessObject.CurrentRow.Row;
            bool needsDefaultBankAccount = false;

            foreach (DataRow reportRow in selectedRows)
            {
                if (reportRow == null || reportRow.RowState == DataRowState.Deleted)
                    continue;

                if (!TryGetCurrentAccountId(reportRow, out long currentAccountId))
                {
                    result.SkippedNoAccountCount++;
                    continue;
                }

                decimal amount = GetDecimal(reportRow, amountColumnName);
                if (amount <= 0m)
                {
                    result.SkippedZeroAmountCount++;
                    continue;
                }

                string accountName = GetCurrentAccountDisplayName(reportRow);
                string explanation = BuildExplanation(accountName, reportDate);

                if (TryFindItemRow(itemTable, currentAccountId, out DataRow existingRow, out bool isApproved))
                {
                    if (isApproved)
                    {
                        result.SkippedApprovedCount++;
                        continue;
                    }

                    ApplyLineValues(existingRow, amount, paymentDate, explanation);
                    result.UpdatedCount++;
                    continue;
                }

                if (bankAccountId <= 0)
                {
                    needsDefaultBankAccount = true;
                    continue;
                }

                DataRow itemRow = itemTable.NewRow();
                itemRow.SetParentRow(headerRow);
                itemTable.Rows.Add(itemRow);
                itemRow["CurrentAccountId"] = currentAccountId;
                itemRow["BankAccountId"] = bankAccountId;
                itemRow["Credit"] = amount;
                itemRow["IsApproved"] = (byte)0;
                ApplyLineValues(itemRow, amount, paymentDate, explanation);
                result.AddedCount++;
            }

            result.Message = BuildResultMessage(result, needsDefaultBankAccount);
            return result;
        }

        static void ApplyLineValues(DataRow itemRow, decimal amount, DateTime paymentDate, string explanation)
        {
            itemRow["Credit"] = amount;

            if (ItemRowHasColumn(itemRow, PaymentOrderUdFields.PaymentDate))
                itemRow[PaymentOrderUdFields.PaymentDate] = paymentDate;

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

        static bool TryGetCurrentAccountId(DataRow row, out long currentAccountId)
        {
            currentAccountId = 0;
            if (row?.Table == null)
                return false;

            if (row.Table.Columns.Contains("CurrentAccountId") && !row.IsNull("CurrentAccountId"))
            {
                currentAccountId = Convert.ToInt64(row["CurrentAccountId"]);
                return currentAccountId > 0;
            }

            if (row.Table.Columns.Contains("RecId") && !row.IsNull("RecId"))
            {
                currentAccountId = Convert.ToInt64(row["RecId"]);
                return currentAccountId > 0;
            }

            return false;
        }

        static string GetCurrentAccountDisplayName(DataRow row)
        {
            string accountName = GetString(row, SLanguage.GetString("Cari Hesap Adı"));
            if (!string.IsNullOrWhiteSpace(accountName))
                return accountName;

            return GetString(row, SLanguage.GetString("Cari Hesap Kodu"));
        }

        static string GetString(DataRow row, string columnName)
        {
            if (row?.Table == null || !row.Table.Columns.Contains(columnName) || row.IsNull(columnName))
                return string.Empty;

            return Convert.ToString(row[columnName]) ?? string.Empty;
        }

        static decimal GetDecimal(DataRow row, string columnName)
        {
            if (row?.Table == null || !row.Table.Columns.Contains(columnName) || row.IsNull(columnName))
                return 0m;

            return Convert.ToDecimal(row[columnName], CultureInfo.InvariantCulture);
        }

        static string BuildExplanation(string accountName, DateTime reportDate)
        {
            return string.Format(
                SLanguage.GetString("{0} - {1}/{2} Yaşlandırma"),
                accountName,
                reportDate.Month.ToString("00", CultureInfo.InvariantCulture),
                reportDate.Year.ToString(CultureInfo.InvariantCulture));
        }

        static bool TryFindItemRow(DataTable itemTable, long currentAccountId, out DataRow itemRow, out bool isApproved)
        {
            itemRow = null;
            isApproved = false;

            foreach (DataRow row in itemTable.Rows)
            {
                if (row.RowState == DataRowState.Deleted) continue;
                if (row.IsNull("CurrentAccountId")) continue;
                if (Convert.ToInt64(row["CurrentAccountId"]) != currentAccountId) continue;

                itemRow = row;
                isApproved = BankReceiptPaymentOrderHelper.GetApprovedValue(row) == 1;
                return true;
            }

            return false;
        }

        static string BuildResultMessage(CurrentAccountAgingImportResult result, bool needsDefaultBankAccount)
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
                        SLanguage.GetString("{0} cari için yaşlandırma tutarı bulunamadı."),
                        result.SkippedZeroAmountCount));
            }

            if (result.SkippedNoAccountCount > 0)
            {
                AppendMessageLine(
                    message,
                    string.Format(
                        SLanguage.GetString("{0} satırda cari hesap bilgisi bulunamadı."),
                        result.SkippedNoAccountCount));
            }

            if (needsDefaultBankAccount)
            {
                AppendMessageLine(
                    message,
                    SLanguage.GetString("Yeni satır eklemek için ön değer banka hesabını seçiniz."));
            }

            return message.ToString().Trim();
        }

        public static long ResolveDefaultBankAccountId(
            LiveSession session,
            long defaultBankAccountId,
            string defaultBankAccountCode)
        {
            return BankAccountDefaultResolver.ResolveBankAccountId(
                session,
                defaultBankAccountId,
                defaultBankAccountCode);
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
