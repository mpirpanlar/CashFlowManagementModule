using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;

using CashFlowManagementModule.BoExtensions;

using Sentez.Common.SystemServices;
using Sentez.Data.Tools;
using Sentez.Localization;

namespace CashFlowManagementModule.Services
{
    public static class PosStatementAnalysisService
    {
        public const string AccountListTableName = "PosStatementAccountList";
        public const string DailyTableName = "PosStatementDaily";
        public const string DeductionTableName = "PosStatementDeduction";
        public const string SettlementTableName = "PosStatementSettlement";
        public const string FutureReceivableTableName = "PosStatementFutureReceivable";
        public const string SummaryTableName = "PosStatementSummary";

        public static DataTable BuildAccountListTable(LiveSession session, DateTime referenceDate)
        {
            var table = CreateAccountListTable();
            if (session == null)
                return table;

            referenceDate = referenceDate.Date;
            foreach (PosMerchantAggregationResult result in PosMerchantMovementAggregationService.BuildPosAccountPeriodList(session, referenceDate))
            {
                DataRow row = table.NewRow();
                row["BankAccountId"] = result.BankAccountId;
                row["PeriodYear"] = result.PeriodYear;
                row["PeriodMonth"] = result.PeriodMonth;
                FillAccountDisplay(session, row, result.BankAccountId);
                FillSummaryColumns(row, result);
                table.Rows.Add(row);
            }

            return table;
        }

        public static PosMerchantAggregationResult BuildSelectedAggregation(
            LiveSession session,
            long bankAccountId,
            int periodYear,
            int periodMonth)
        {
            return PosMerchantMovementAggregationService.BuildForPeriod(session, bankAccountId, periodYear, periodMonth);
        }

        public static DataTable BuildDailyTable(PosMerchantAggregationResult aggregation)
        {
            var table = CreateDailyTable();
            if (aggregation?.DailyLines == null)
                return table;

            foreach (PosPeriodDailyLine line in aggregation.DailyLines)
            {
                DataRow row = table.NewRow();
                row["Day"] = line.Day;
                row["CollectionCount"] = line.CollectionCount;
                row["RefundCount"] = line.RefundCount;
                row["GrossAmount"] = line.GrossAmount;
                row["MerchantFeeAmount"] = line.MerchantFeeAmount;
                row["RewardExpenseAmount"] = line.RewardExpenseAmount;
                row["ServiceCommissionAmount"] = line.ServiceCommissionAmount;
                row["RefundAmount"] = line.RefundAmount;
                row["NetAmount"] = line.NetAmount;
                table.Rows.Add(row);
            }

            return table;
        }

        public static DataTable BuildDeductionTable(PosMerchantAggregationResult aggregation)
        {
            var table = CreateDeductionTable();
            if (aggregation?.PeriodDeductions == null)
                return table;

            foreach (PosDeductionBreakdownLine line in aggregation.PeriodDeductions)
            {
                DataRow row = table.NewRow();
                row["DeductionTypeCode"] = line.DeductionTypeCode;
                row["DeductionTypeName"] = line.DeductionTypeName;
                row["Amount"] = line.Amount;
                table.Rows.Add(row);
            }

            return table;
        }

        public static DataTable BuildSettlementTable(PosMerchantAggregationResult aggregation)
        {
            var table = CreateSettlementTable();
            if (aggregation?.SettlementLines == null)
                return table;

            foreach (PosPeriodSettlementLine line in aggregation.SettlementLines)
            {
                DataRow row = table.NewRow();
                row["SettlementDate"] = line.SettlementDate;
                row["SettlementKind"] = line.SettlementKind;
                row["SettlementKindName"] = ResolveSettlementKindName(line.SettlementKind);
                row["GrossAmount"] = line.GrossAmount;
                row["DeductionAmount"] = line.DeductionAmount;
                row["RefundAmount"] = line.RefundAmount;
                row["NetAmount"] = line.NetAmount;
                table.Rows.Add(row);
            }

            return table;
        }

        public static DataTable BuildFutureReceivableTable(PosMerchantAggregationResult aggregation)
        {
            var table = CreateFutureReceivableTable();
            if (aggregation?.FutureReceivableLines == null)
                return table;

            foreach (PosPeriodFutureReceivableLine line in aggregation.FutureReceivableLines)
            {
                DataRow row = table.NewRow();
                row["PeriodYear"] = line.PeriodYear;
                row["PeriodMonth"] = line.PeriodMonth;
                row["PeriodLabel"] = $"{line.PeriodMonth:00}/{line.PeriodYear}";
                row["GrossAmount"] = line.GrossAmount;
                row["NetAmount"] = line.NetAmount;
                table.Rows.Add(row);
            }

            return table;
        }

        public static DataTable BuildSummaryTable(PosMerchantAggregationResult aggregation)
        {
            var table = CreateSummaryTable();
            if (aggregation?.Summary == null)
                return table;

            PosPeriodSummaryKpi summary = aggregation.Summary;
            AddSummaryRow(table, SLanguage.GetString("Toplam Brüt"), summary.TotalGross);
            AddSummaryRow(table, SLanguage.GetString("Toplam Kesinti"), summary.TotalDeduction);
            AddSummaryRow(table, SLanguage.GetString("Toplam İade"), summary.TotalRefund);
            AddSummaryRow(table, SLanguage.GetString("Toplam Net"), summary.TotalNet);
            AddSummaryRow(table, SLanguage.GetString("Hesaba Geçiş Net (Seçili Ay)"), summary.CurrentMonthSettlementNet);
            AddSummaryRow(table, SLanguage.GetString("Sonraki Ay Alacağı Net"), summary.NextMonthReceivableNet);
            AddSummaryRow(table, SLanguage.GetString("Gelecek Aylar Alacağı Net"), summary.FutureReceivableNet);

            if (!string.IsNullOrWhiteSpace(summary.WarningMessage))
            {
                DataRow warningRow = table.NewRow();
                warningRow["MetricName"] = SLanguage.GetString("Uyarı");
                warningRow["MetricValue"] = DBNull.Value;
                warningRow["MetricText"] = summary.WarningMessage;
                table.Rows.Add(warningRow);
            }

            return table;
        }

        static void AddSummaryRow(DataTable table, string metricName, decimal metricValue)
        {
            DataRow row = table.NewRow();
            row["MetricName"] = metricName;
            row["MetricValue"] = metricValue;
            row["MetricText"] = DBNull.Value;
            table.Rows.Add(row);
        }

        static void FillAccountDisplay(LiveSession session, DataRow row, long bankAccountId)
        {
            if (session?._dbInfo?.Connection == null || bankAccountId <= 0)
                return;

            int companyId = session.ActiveCompany.RecId ?? 0;
            DataTable table = UtilityFunctions.GetDataTableList(
                session._dbInfo.DBProvider,
                session._dbInfo.Connection,
                null,
                "Erp_BankAccount",
                $@"select ba.AccountCode, ba.AccountName, b.BankCode, b.BankName
                   from Erp_BankAccount ba with (nolock)
                   inner join Erp_Bank b with (nolock) on b.RecId = ba.BankId
                   where b.CompanyId = {companyId}
                     and ba.RecId = {bankAccountId}");

            if (table == null || table.Rows.Count == 0)
                return;

            DataRow source = table.Rows[0];
            row["AccountCode"] = source.IsNull("AccountCode") ? string.Empty : source["AccountCode"];
            row["AccountName"] = source.IsNull("AccountName") ? string.Empty : source["AccountName"];
            row["BankCode"] = source.IsNull("BankCode") ? string.Empty : source["BankCode"];
            row["BankName"] = source.IsNull("BankName") ? string.Empty : source["BankName"];
        }

        static void FillSummaryColumns(DataRow row, PosMerchantAggregationResult result)
        {
            PosPeriodSummaryKpi summary = result.Summary;
            row["TotalGross"] = summary.TotalGross;
            row["TotalDeduction"] = summary.TotalDeduction;
            row["TotalRefund"] = summary.TotalRefund;
            row["TotalNet"] = summary.TotalNet;
            row["CurrentMonthSettlementNet"] = summary.CurrentMonthSettlementNet;
            row["NextMonthReceivableNet"] = summary.NextMonthReceivableNet;
            row["FutureReceivableNet"] = summary.FutureReceivableNet;
            row["WarningMessage"] = summary.WarningMessage ?? string.Empty;
        }

        static string ResolveSettlementKindName(byte settlementKind)
        {
            if (settlementKind == BankAccountPosHelper.SettlementKindCurrentMonth)
                return SLanguage.GetString("Hesaba Geçişler (Seçili Ay)");

            if (settlementKind == BankAccountPosHelper.SettlementKindNextMonth)
                return SLanguage.GetString("Sonraki Ay Alacağı");

            return string.Empty;
        }

        static DataTable CreateAccountListTable()
        {
            var table = new DataTable(AccountListTableName);
            table.Columns.Add("BankAccountId", typeof(long));
            table.Columns.Add("BankCode", typeof(string));
            table.Columns.Add("BankName", typeof(string));
            table.Columns.Add("AccountCode", typeof(string));
            table.Columns.Add("AccountName", typeof(string));
            table.Columns.Add("PeriodYear", typeof(short));
            table.Columns.Add("PeriodMonth", typeof(short));
            table.Columns.Add("TotalGross", typeof(decimal));
            table.Columns.Add("TotalDeduction", typeof(decimal));
            table.Columns.Add("TotalRefund", typeof(decimal));
            table.Columns.Add("TotalNet", typeof(decimal));
            table.Columns.Add("CurrentMonthSettlementNet", typeof(decimal));
            table.Columns.Add("NextMonthReceivableNet", typeof(decimal));
            table.Columns.Add("FutureReceivableNet", typeof(decimal));
            table.Columns.Add("WarningMessage", typeof(string));
            return table;
        }

        static DataTable CreateDailyTable()
        {
            var table = new DataTable(DailyTableName);
            table.Columns.Add("Day", typeof(DateTime));
            table.Columns.Add("CollectionCount", typeof(int));
            table.Columns.Add("RefundCount", typeof(int));
            table.Columns.Add("GrossAmount", typeof(decimal));
            table.Columns.Add("MerchantFeeAmount", typeof(decimal));
            table.Columns.Add("RewardExpenseAmount", typeof(decimal));
            table.Columns.Add("ServiceCommissionAmount", typeof(decimal));
            table.Columns.Add("RefundAmount", typeof(decimal));
            table.Columns.Add("NetAmount", typeof(decimal));
            return table;
        }

        static DataTable CreateDeductionTable()
        {
            var table = new DataTable(DeductionTableName);
            table.Columns.Add("DeductionTypeCode", typeof(string));
            table.Columns.Add("DeductionTypeName", typeof(string));
            table.Columns.Add("Amount", typeof(decimal));
            return table;
        }

        static DataTable CreateSettlementTable()
        {
            var table = new DataTable(SettlementTableName);
            table.Columns.Add("SettlementDate", typeof(DateTime));
            table.Columns.Add("SettlementKind", typeof(byte));
            table.Columns.Add("SettlementKindName", typeof(string));
            table.Columns.Add("GrossAmount", typeof(decimal));
            table.Columns.Add("DeductionAmount", typeof(decimal));
            table.Columns.Add("RefundAmount", typeof(decimal));
            table.Columns.Add("NetAmount", typeof(decimal));
            return table;
        }

        static DataTable CreateFutureReceivableTable()
        {
            var table = new DataTable(FutureReceivableTableName);
            table.Columns.Add("PeriodYear", typeof(short));
            table.Columns.Add("PeriodMonth", typeof(short));
            table.Columns.Add("PeriodLabel", typeof(string));
            table.Columns.Add("GrossAmount", typeof(decimal));
            table.Columns.Add("NetAmount", typeof(decimal));
            return table;
        }

        static DataTable CreateSummaryTable()
        {
            var table = new DataTable(SummaryTableName);
            table.Columns.Add("MetricName", typeof(string));
            table.Columns.Add("MetricValue", typeof(decimal));
            table.Columns.Add("MetricText", typeof(string));
            return table;
        }
    }
}
