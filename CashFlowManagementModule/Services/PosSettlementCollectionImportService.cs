using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;

using CashFlowManagementModule.BoExtensions;

using Sentez.Common.SystemServices;
using Sentez.Data.BusinessObjects;
using Sentez.Localization;

namespace CashFlowManagementModule.Services
{
    public sealed class PosSettlementCollectionImportResult
    {
        public int AddedCount { get; set; }
        public int UpdatedCount { get; set; }
        public int SkippedApprovedCount { get; set; }
        public int SkippedZeroAmountCount { get; set; }
        public int SkippedNoSettlementCount { get; set; }
        public string Message { get; set; }
    }

    public static class PosSettlementCollectionImportService
    {
        public static PosSettlementCollectionImportResult Import(
            BusinessObjectBase businessObject,
            LiveSession session,
            DateTime receiptDate)
        {
            var result = new PosSettlementCollectionImportResult();

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

            receiptDate = receiptDate.Date;
            int periodYear = receiptDate.Year;
            int periodMonth = receiptDate.Month;
            IList<long> bankAccountIds = PosMerchantDataService.LoadPosBankAccountIds(session);

            if (bankAccountIds == null || bankAccountIds.Count == 0)
            {
                result.Message = SLanguage.GetString("Aktarılacak Pos hesabı bulunamadı.");
                return result;
            }

            foreach (long bankAccountId in bankAccountIds)
            {
                PosMerchantAggregationResult aggregation = PosMerchantMovementAggregationService.BuildForPeriod(
                    session,
                    bankAccountId,
                    periodYear,
                    periodMonth);

                IList<PosPeriodSettlementLine> settlementLines = aggregation.SettlementLines?
                    .Where(line => line.SettlementKind == BankAccountPosHelper.SettlementKindCurrentMonth && line.NetAmount > 0m)
                    .ToList();

                if (settlementLines == null || settlementLines.Count == 0)
                {
                    result.SkippedNoSettlementCount++;
                    continue;
                }

                foreach (PosPeriodSettlementLine settlementLine in settlementLines)
                {
                    if (settlementLine.NetAmount <= 0m)
                    {
                        result.SkippedZeroAmountCount++;
                        continue;
                    }

                    string explanation = BuildExplanation(bankAccountId, settlementLine, periodYear, periodMonth);

                    if (TryFindItemRow(itemTable, bankAccountId, settlementLine.SettlementDate, out DataRow existingRow, out bool isApproved))
                    {
                        if (isApproved)
                        {
                            result.SkippedApprovedCount++;
                            continue;
                        }

                        ApplyLineValues(existingRow, settlementLine.NetAmount, settlementLine.SettlementDate, explanation);
                        result.UpdatedCount++;
                        continue;
                    }

                    DataRow itemRow = itemTable.NewRow();
                    itemRow.SetParentRow(headerRow);
                    itemTable.Rows.Add(itemRow);
                    itemRow["BankAccountId"] = bankAccountId;
                    itemRow["IsApproved"] = (byte)0;
                    ApplyLineValues(itemRow, settlementLine.NetAmount, settlementLine.SettlementDate, explanation);
                    result.AddedCount++;
                }
            }

            result.Message = BuildResultMessage(result);
            return result;
        }

        static void ApplyLineValues(DataRow itemRow, decimal amount, DateTime settlementDate, string explanation)
        {
            PlanningAmountSide.ApplyAmountToRow(itemRow, BankReceiptCollectionOrderHelper.ReceiptType, amount, null);

            if (ItemRowHasColumn(itemRow, CollectionOrderUdFields.PaymentDate))
                itemRow[CollectionOrderUdFields.PaymentDate] = settlementDate;

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

        static string BuildExplanation(long bankAccountId, PosPeriodSettlementLine settlementLine, int periodYear, int periodMonth)
        {
            return string.Format(
                SLanguage.GetString("Pos Hesaba Geçiş - Hesap {0} - {1}/{2} - {3}"),
                bankAccountId,
                periodMonth.ToString("00", CultureInfo.InvariantCulture),
                periodYear.ToString(CultureInfo.InvariantCulture),
                settlementLine.SettlementDate.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture));
        }

        static bool TryFindItemRow(
            DataTable itemTable,
            long bankAccountId,
            DateTime settlementDate,
            out DataRow itemRow,
            out bool isApproved)
        {
            itemRow = null;
            isApproved = false;

            foreach (DataRow row in itemTable.Rows)
            {
                if (row.RowState == DataRowState.Deleted) continue;
                if (row.IsNull("BankAccountId")) continue;
                if (Convert.ToInt64(row["BankAccountId"]) != bankAccountId) continue;

                if (ItemRowHasColumn(row, CollectionOrderUdFields.PaymentDate) && !row.IsNull(CollectionOrderUdFields.PaymentDate))
                {
                    DateTime paymentDate = Convert.ToDateTime(row[CollectionOrderUdFields.PaymentDate]).Date;
                    if (paymentDate != settlementDate.Date)
                        continue;
                }

                itemRow = row;
                isApproved = BankReceiptCollectionOrderHelper.GetApprovedValue(row) == 1;
                return true;
            }

            return false;
        }

        static string BuildResultMessage(PosSettlementCollectionImportResult result)
        {
            var message = new StringBuilder();
            message.AppendFormat(
                SLanguage.GetString("{0} satır eklendi, {1} satır güncellendi."),
                result.AddedCount,
                result.UpdatedCount);

            if (result.SkippedApprovedCount > 0)
                message.Append(' ').AppendFormat(SLanguage.GetString("{0} onaylı satır atlandı."), result.SkippedApprovedCount);

            if (result.SkippedZeroAmountCount > 0)
                message.Append(' ').AppendFormat(SLanguage.GetString("{0} sıfır tutarlı satır atlandı."), result.SkippedZeroAmountCount);

            if (result.SkippedNoSettlementCount > 0)
                message.Append(' ').AppendFormat(SLanguage.GetString("{0} hesapta hesaba geçiş bulunamadı."), result.SkippedNoSettlementCount);

            return message.ToString().Trim();
        }
    }
}
