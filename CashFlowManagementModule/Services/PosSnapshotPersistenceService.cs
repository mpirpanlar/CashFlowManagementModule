using System;
using System.Collections.Generic;
using System.Data;

using CashFlowManagementModule.BoExtensions;

using Sentez.Common.SystemServices;
using Sentez.Data.Tools;

namespace CashFlowManagementModule.Services
{
    public static class PosSnapshotPersistenceService
    {
        public static long RefreshSnapshot(LiveSession session, PosMerchantAggregationResult aggregation, int userId)
        {
            if (session?._dbInfo?.Connection == null || aggregation == null || aggregation.BankAccountId <= 0)
                return 0;

            int companyId = session.ActiveCompany.RecId ?? 0;
            DateTime now = DateTime.Now;
            long summaryId = UpsertSummary(session, aggregation, companyId, userId, now);
            if (summaryId <= 0)
                return 0;

            SoftDeleteChildRows(session, BankAccountPosHelper.PeriodDailyTableName, summaryId, userId, now);
            SoftDeleteChildRows(session, BankAccountPosHelper.PeriodSettlementTableName, summaryId, userId, now);
            SoftDeleteChildRows(session, BankAccountPosHelper.PeriodFutureReceivableTableName, summaryId, userId, now);

            InsertDailyRows(session, summaryId, aggregation.DailyLines);
            InsertSettlementRows(session, summaryId, aggregation.SettlementLines);
            InsertFutureReceivableRows(session, summaryId, aggregation.FutureReceivableLines);

            return summaryId;
        }

        static long UpsertSummary(
            LiveSession session,
            PosMerchantAggregationResult aggregation,
            int companyId,
            int userId,
            DateTime now)
        {
            PosPeriodSummaryKpi summary = aggregation.Summary;
            string warning = string.IsNullOrWhiteSpace(summary.WarningMessage)
                ? "NULL"
                : $"N'{EscapeSql(summary.WarningMessage)}'";

            string sql = $@"
if exists (
    select 1 from Erp_BankAccountPosPeriodSummary with (nolock)
    where CompanyId = {companyId}
      and BankAccountId = {aggregation.BankAccountId}
      and PeriodYear = {aggregation.PeriodYear}
      and PeriodMonth = {aggregation.PeriodMonth}
      and isnull(IsDeleted,0)=0)
begin
    update Erp_BankAccountPosPeriodSummary
       set TotalGross = {ToSqlDecimal(summary.TotalGross)},
           TotalDeduction = {ToSqlDecimal(summary.TotalDeduction)},
           TotalRefund = {ToSqlDecimal(summary.TotalRefund)},
           TotalNet = {ToSqlDecimal(summary.TotalNet)},
           CurrentMonthSettlementNet = {ToSqlDecimal(summary.CurrentMonthSettlementNet)},
           NextMonthReceivableNet = {ToSqlDecimal(summary.NextMonthReceivableNet)},
           FutureReceivableNet = {ToSqlDecimal(summary.FutureReceivableNet)},
           HasDeductionProfile = {(summary.HasDeductionProfile ? 1 : 0)},
           WarningMessage = {warning},
           CalculatedAt = '{now:yyyy-MM-dd HH:mm:ss}',
           UpdatedAt = '{now:yyyy-MM-dd HH:mm:ss}',
           UpdatedBy = {userId}
     where CompanyId = {companyId}
       and BankAccountId = {aggregation.BankAccountId}
       and PeriodYear = {aggregation.PeriodYear}
       and PeriodMonth = {aggregation.PeriodMonth}
       and isnull(IsDeleted,0)=0
end
else
begin
    insert into Erp_BankAccountPosPeriodSummary
    (CompanyId,BankAccountId,PeriodYear,PeriodMonth,TotalGross,TotalDeduction,TotalRefund,TotalNet,
     CurrentMonthSettlementNet,NextMonthReceivableNet,FutureReceivableNet,HasDeductionProfile,WarningMessage,
     CalculatedAt,InUse,InsertedAt,InsertedBy,IsDeleted)
    values
    ({companyId},{aggregation.BankAccountId},{aggregation.PeriodYear},{aggregation.PeriodMonth},
     {ToSqlDecimal(summary.TotalGross)},{ToSqlDecimal(summary.TotalDeduction)},{ToSqlDecimal(summary.TotalRefund)},{ToSqlDecimal(summary.TotalNet)},
     {ToSqlDecimal(summary.CurrentMonthSettlementNet)},{ToSqlDecimal(summary.NextMonthReceivableNet)},{ToSqlDecimal(summary.FutureReceivableNet)},
     {(summary.HasDeductionProfile ? 1 : 0)},{warning},'{now:yyyy-MM-dd HH:mm:ss}',1,'{now:yyyy-MM-dd HH:mm:ss}',{userId},0)
end

select top 1 RecId
from Erp_BankAccountPosPeriodSummary with (nolock)
where CompanyId = {companyId}
  and BankAccountId = {aggregation.BankAccountId}
  and PeriodYear = {aggregation.PeriodYear}
  and PeriodMonth = {aggregation.PeriodMonth}
  and isnull(IsDeleted,0)=0";

            DataTable table = UtilityFunctions.GetDataTableList(
                session._dbInfo.DBProvider,
                session._dbInfo.Connection,
                null,
                BankAccountPosHelper.PeriodSummaryTableName,
                sql);

            if (table == null || table.Rows.Count == 0 || table.Rows[0].IsNull("RecId"))
                return 0;

            return Convert.ToInt64(table.Rows[0]["RecId"]);
        }

        static void SoftDeleteChildRows(LiveSession session, string tableName, long summaryId, int userId, DateTime now)
        {
            CashFlowDbContext context = CashFlowDbContext.FromSession(session);
            string sql = $@"
update {tableName}
   set IsDeleted = 1
 where SummaryId = {summaryId}
   and isnull(IsDeleted,0)=0";

            CashFlowDbAccess.ExecuteNonQuery(context, sql);
        }

        static void InsertDailyRows(LiveSession session, long summaryId, IEnumerable<PosPeriodDailyLine> lines)
        {
            if (lines == null)
                return;

            CashFlowDbContext context = CashFlowDbContext.FromSession(session);
            foreach (PosPeriodDailyLine line in lines)
            {
                string sql = $@"
insert into Erp_BankAccountPosPeriodDaily
(SummaryId,Day,CollectionCount,RefundCount,GrossAmount,MerchantFeeAmount,RewardExpenseAmount,ServiceCommissionAmount,RefundAmount,NetAmount,IsDeleted)
values
({summaryId},'{line.Day:yyyy-MM-dd}',{line.CollectionCount},{line.RefundCount},
 {ToSqlDecimal(line.GrossAmount)},{ToSqlDecimal(line.MerchantFeeAmount)},{ToSqlDecimal(line.RewardExpenseAmount)},
 {ToSqlDecimal(line.ServiceCommissionAmount)},{ToSqlDecimal(line.RefundAmount)},{ToSqlDecimal(line.NetAmount)},0)";

                CashFlowDbAccess.ExecuteNonQuery(context, sql);
            }
        }

        static void InsertSettlementRows(LiveSession session, long summaryId, IEnumerable<PosPeriodSettlementLine> lines)
        {
            if (lines == null)
                return;

            CashFlowDbContext context = CashFlowDbContext.FromSession(session);
            foreach (PosPeriodSettlementLine line in lines)
            {
                string sql = $@"
insert into Erp_BankAccountPosPeriodSettlement
(SummaryId,SettlementDate,SettlementKind,GrossAmount,DeductionAmount,RefundAmount,NetAmount,IsDeleted)
values
({summaryId},'{line.SettlementDate:yyyy-MM-dd}',{line.SettlementKind},
 {ToSqlDecimal(line.GrossAmount)},{ToSqlDecimal(line.DeductionAmount)},{ToSqlDecimal(line.RefundAmount)},{ToSqlDecimal(line.NetAmount)},0)";

                CashFlowDbAccess.ExecuteNonQuery(context, sql);
            }
        }

        static void InsertFutureReceivableRows(LiveSession session, long summaryId, IEnumerable<PosPeriodFutureReceivableLine> lines)
        {
            if (lines == null)
                return;

            CashFlowDbContext context = CashFlowDbContext.FromSession(session);
            foreach (PosPeriodFutureReceivableLine line in lines)
            {
                string sql = $@"
insert into Erp_BankAccountPosPeriodFutureReceivable
(SummaryId,PeriodYear,PeriodMonth,GrossAmount,NetAmount,IsDeleted)
values
({summaryId},{line.PeriodYear},{line.PeriodMonth},{ToSqlDecimal(line.GrossAmount)},{ToSqlDecimal(line.NetAmount)},0)";

                CashFlowDbAccess.ExecuteNonQuery(context, sql);
            }
        }

        static string ToSqlDecimal(decimal value)
        {
            return value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        static string EscapeSql(string value)
        {
            return value?.Replace("'", "''") ?? string.Empty;
        }
    }
}
