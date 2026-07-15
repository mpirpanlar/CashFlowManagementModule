using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

using CashFlowManagementModule.BoExtensions;

using Sentez.Common.SystemServices;
using Sentez.Data.BusinessObjects;
using Sentez.Data.Tools;
using Sentez.Localization;

namespace CashFlowManagementModule.Services
{
    public sealed class PosSnapshotBackfillResult
    {
        public int ProcessedCount { get; set; }
        public int SkippedCount { get; set; }
        public string Message { get; set; }
    }

    public readonly struct PosSnapshotPeriodKey : IEquatable<PosSnapshotPeriodKey>
    {
        public PosSnapshotPeriodKey(long bankAccountId, int periodYear, int periodMonth)
        {
            BankAccountId = bankAccountId;
            PeriodYear = periodYear;
            PeriodMonth = periodMonth;
        }

        public long BankAccountId { get; }
        public int PeriodYear { get; }
        public int PeriodMonth { get; }

        public bool Equals(PosSnapshotPeriodKey other)
        {
            return BankAccountId == other.BankAccountId
                && PeriodYear == other.PeriodYear
                && PeriodMonth == other.PeriodMonth;
        }

        public override bool Equals(object obj)
        {
            return obj is PosSnapshotPeriodKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = BankAccountId.GetHashCode();
                hash = (hash * 397) ^ PeriodYear;
                hash = (hash * 397) ^ PeriodMonth;
                return hash;
            }
        }
    }

    /// <summary>
    /// Pos ekstre snapshot yenileme ve toplu backfill işlemleri.
    /// </summary>
    public static class PosSnapshotRefreshService
    {
        const int FinanceSourceModule = 3;

        public static void RefreshPeriodSnapshot(
            LiveSession session,
            long bankAccountId,
            int periodYear,
            int periodMonth,
            int userId)
        {
            if (session?._dbInfo?.Connection == null || bankAccountId <= 0)
                return;

            if (!PosMerchantDataService.IsPosBankAccount(session, bankAccountId))
                return;

            PosMerchantAggregationResult aggregation = PosMerchantMovementAggregationService.BuildForPeriod(
                session,
                bankAccountId,
                periodYear,
                periodMonth);

            PosSnapshotPersistenceService.RefreshSnapshot(session, aggregation, userId);
        }

        public static void RefreshAffectedPeriods(BusinessObjectBase businessObject, LiveSession session, int userId)
        {
            if (session?._dbInfo?.Connection == null || businessObject?.Data == null)
                return;

            RefreshPeriods(session, CollectAffectedPeriods(businessObject, session), userId);
        }

        public static void RefreshPeriods(
            LiveSession session,
            IEnumerable<PosSnapshotPeriodKey> periods,
            int userId)
        {
            if (session?._dbInfo?.Connection == null || periods == null)
                return;

            foreach (PosSnapshotPeriodKey periodKey in periods)
            {
                RefreshPeriodSnapshot(
                    session,
                    periodKey.BankAccountId,
                    periodKey.PeriodYear,
                    periodKey.PeriodMonth,
                    userId);
            }
        }

        public static IReadOnlyCollection<PosSnapshotPeriodKey> CollectAffectedPeriods(
            BusinessObjectBase businessObject,
            LiveSession session)
        {
            var periods = new HashSet<PosSnapshotPeriodKey>();
            if (businessObject?.Data == null || session?._dbInfo?.Connection == null)
                return periods.ToList();

            if (!businessObject.Data.Tables.Contains("Erp_CurrentAccountReceiptItem"))
                return periods.ToList();

            Dictionary<long, List<DataRow>> paymentItemsByCri = LoadPaymentItemsFromBo(businessObject);

            foreach (DataRow itemRow in businessObject.Data.Tables["Erp_CurrentAccountReceiptItem"].Rows.Cast<DataRow>())
            {
                if (!TryGetRowField(itemRow, "BankAccountId", out object bankAccountIdValue))
                    continue;

                long bankAccountId = Convert.ToInt64(bankAccountIdValue);
                if (!PosMerchantDataService.IsPosBankAccount(session, bankAccountId))
                    continue;

                if (TryGetRowField(itemRow, "ReceiptDate", out object receiptDateValue))
                {
                    DateTime receiptDate = Convert.ToDateTime(receiptDateValue).Date;
                    periods.Add(new PosSnapshotPeriodKey(bankAccountId, receiptDate.Year, receiptDate.Month));
                }

                long criId = TryGetRowField(itemRow, "RecId", out object criIdValue)
                    ? Convert.ToInt64(criIdValue)
                    : 0L;

                if (criId > 0
                    && paymentItemsByCri.TryGetValue(criId, out List<DataRow> paymentRows)
                    && paymentRows.Count > 0)
                {
                    foreach (DataRow paymentRow in paymentRows)
                    {
                        if (!TryGetRowField(paymentRow, "TermDate", out object termDateValue))
                            continue;

                        DateTime settlementDate = Convert.ToDateTime(termDateValue).Date;
                        periods.Add(new PosSnapshotPeriodKey(bankAccountId, settlementDate.Year, settlementDate.Month));
                    }

                    continue;
                }

                if (TryGetRowField(itemRow, "TermDate", out object itemTermDateValue))
                {
                    DateTime settlementDate = Convert.ToDateTime(itemTermDateValue).Date;
                    periods.Add(new PosSnapshotPeriodKey(bankAccountId, settlementDate.Year, settlementDate.Month));
                }
                else if (criId > 0)
                {
                    foreach (DateTime settlementDate in LoadSettlementDatesFromDb(session, criId))
                    {
                        periods.Add(new PosSnapshotPeriodKey(bankAccountId, settlementDate.Year, settlementDate.Month));
                    }
                }
            }

            return periods.ToList();
        }

        public static PosSnapshotBackfillResult BackfillPeriodRange(
            LiveSession session,
            DateTime startDate,
            DateTime endDate,
            int userId,
            IEnumerable<long> bankAccountIds = null)
        {
            var result = new PosSnapshotBackfillResult();
            if (session?._dbInfo?.Connection == null)
            {
                result.Message = SLanguage.GetString("Oturum bilgisi bulunamadı.");
                return result;
            }

            startDate = new DateTime(startDate.Year, startDate.Month, 1);
            endDate = new DateTime(endDate.Year, endDate.Month, 1);
            if (endDate < startDate)
            {
                result.Message = SLanguage.GetString("Bitiş dönemi başlangıç döneminden küçük olamaz.");
                return result;
            }

            IList<long> accountIds = bankAccountIds?.Distinct().ToList()
                ?? PosMerchantDataService.LoadPosBankAccountIds(session);

            if (accountIds == null || accountIds.Count == 0)
            {
                result.Message = SLanguage.GetString("Pos hesabı bulunamadı.");
                return result;
            }

            for (DateTime cursor = startDate; cursor <= endDate; cursor = cursor.AddMonths(1))
            {
                foreach (long bankAccountId in accountIds)
                {
                    if (!PosMerchantDataService.IsPosBankAccount(session, bankAccountId))
                    {
                        result.SkippedCount++;
                        continue;
                    }

                    RefreshPeriodSnapshot(session, bankAccountId, cursor.Year, cursor.Month, userId);
                    result.ProcessedCount++;
                }
            }

            result.Message = string.Format(
                SLanguage.GetString("{0} adet dönem özeti yenilendi."),
                result.ProcessedCount);
            return result;
        }

        static Dictionary<long, List<DataRow>> LoadPaymentItemsFromBo(BusinessObjectBase businessObject)
        {
            var map = new Dictionary<long, List<DataRow>>();
            if (!businessObject.Data.Tables.Contains("Erp_ReceiptPaymentItem"))
                return map;

            foreach (DataRow row in businessObject.Data.Tables["Erp_ReceiptPaymentItem"].Rows.Cast<DataRow>())
            {
                if (!TryGetRowField(row, "SourceItemId", out object sourceItemIdValue)
                    || !TryGetRowField(row, "TermDate", out _))
                    continue;

                long criId = Convert.ToInt64(sourceItemIdValue);
                if (!map.TryGetValue(criId, out List<DataRow> list))
                {
                    list = new List<DataRow>();
                    map[criId] = list;
                }

                list.Add(row);
            }

            return map;
        }

        static bool TryGetRowField(DataRow row, string columnName, out object value)
        {
            value = null;
            if (row == null || row.Table == null || string.IsNullOrEmpty(columnName))
                return false;

            if (!row.Table.Columns.Contains(columnName))
                return false;

            if (row.RowState == DataRowState.Detached && !row.HasVersion(DataRowVersion.Original))
                return false;

            try
            {
                DataRowVersion version = DataRowVersion.Current;
                if (row.RowState == DataRowState.Deleted)
                    version = DataRowVersion.Original;
                else if (!row.HasVersion(DataRowVersion.Current) && row.HasVersion(DataRowVersion.Original))
                    version = DataRowVersion.Original;

                if (!row.HasVersion(version))
                    return false;

                DataColumn column = row.Table.Columns[columnName];
                if (row.IsNull(column, version))
                    return false;

                value = row[column, version];
                return true;
            }
            catch (RowNotInTableException)
            {
                return false;
            }
            catch (DeletedRowInaccessibleException)
            {
                return false;
            }
        }

        static IEnumerable<DateTime> LoadSettlementDatesFromDb(LiveSession session, long criId)
        {
            DataTable table = UtilityFunctions.GetDataTableList(
                session._dbInfo.DBProvider,
                session._dbInfo.Connection,
                null,
                "Erp_ReceiptPaymentItem",
                $@"select rpi.TermDate
                   from Erp_ReceiptPaymentItem rpi with (nolock)
                   where rpi.SourceModule = {FinanceSourceModule}
                     and isnull(rpi.IsDeleted,0)=0
                     and rpi.SourceItemId = {criId}
                     and rpi.TermDate is not null");

            if (table == null)
                yield break;

            foreach (DataRow row in table.Rows)
            {
                if (row.IsNull("TermDate"))
                    continue;

                yield return Convert.ToDateTime(row["TermDate"]).Date;
            }
        }
    }
}
