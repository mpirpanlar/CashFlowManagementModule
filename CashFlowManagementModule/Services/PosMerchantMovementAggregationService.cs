using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

using CashFlowManagementModule.BoExtensions;

using Sentez.Common.SystemServices;
using Sentez.Core.ParameterClasses;
using Sentez.Data.Tools;
using Sentez.Localization;

namespace CashFlowManagementModule.Services
{
    public static class PosMerchantMovementAggregationService
    {
        const int FinanceSourceModule = 3;

        public static PosMerchantAggregationResult BuildForPeriod(
            LiveSession session,
            long bankAccountId,
            int periodYear,
            int periodMonth)
        {
            var result = new PosMerchantAggregationResult
            {
                BankAccountId = bankAccountId,
                PeriodYear = periodYear,
                PeriodMonth = periodMonth
            };

            if (session?._dbInfo?.Connection == null || bankAccountId <= 0)
                return result;

            if (!PosMerchantDataService.IsPosBankAccount(session, bankAccountId))
            {
                result.Summary.WarningMessage = SLanguage.GetString("Seçilen hesap Üye İş Yeri Hesabı değildir.");
                return result;
            }

            int amountDec = session.ParamService?.GetParameterClass<GeneralParameters>()?.AmountDec ?? 2;
            DateTime periodStart = new DateTime(periodYear, periodMonth, 1);
            DateTime periodEnd = periodStart.AddMonths(1).AddDays(-1);
            DateTime nextMonthStart = periodStart.AddMonths(1);
            DateTime nextMonthEnd = nextMonthStart.AddMonths(1).AddDays(-1);
            DateTime loadStart = periodStart.AddMonths(-1);
            DateTime loadEnd = periodEnd.AddMonths(13);

            IList<PosDeductionProfileLine> profiles =
                PosDeductionCalculationService.LoadActiveProfiles(session, bankAccountId, periodEnd);
            result.Summary.HasDeductionProfile = profiles.Count > 0;

            IList<PosMerchantMovementLine> movements = LoadSettlementMovements(
                session,
                bankAccountId,
                loadStart,
                loadEnd,
                profiles,
                amountDec);
            result.MovementLines = movements;

            BuildDailyLines(result, movements, periodStart, periodEnd, profiles, amountDec);
            BuildSettlementLines(result, movements, periodStart, periodEnd, nextMonthStart, nextMonthEnd);
            BuildFutureReceivableLines(result, movements, nextMonthEnd);
            BuildSummary(result);

            if (!result.Summary.HasDeductionProfile)
            {
                result.Summary.WarningMessage = SLanguage.GetString(
                    "Kesinti oran profili tanımlı değil. Brüt ve iade tutarları gösterilir; net ve kesinti hesaplanamaz.");
            }

            return result;
        }

        public static IList<PosMerchantAggregationResult> BuildPosAccountPeriodList(
            LiveSession session,
            DateTime referenceDate)
        {
            var results = new List<PosMerchantAggregationResult>();
            if (session?._dbInfo?.Connection == null)
                return results;

            referenceDate = referenceDate.Date;
            foreach (long bankAccountId in PosMerchantDataService.LoadPosBankAccountIds(session))
            {
                results.Add(BuildForPeriod(
                    session,
                    bankAccountId,
                    referenceDate.Year,
                    referenceDate.Month));
            }

            return results;
        }

        static void BuildDailyLines(
            PosMerchantAggregationResult result,
            IList<PosMerchantMovementLine> movements,
            DateTime periodStart,
            DateTime periodEnd,
            IList<PosDeductionProfileLine> profiles,
            int amountDec)
        {
            var dailyMap = new Dictionary<DateTime, PosPeriodDailyLine>();
            var periodDeductionTotals = new Dictionary<long, PosDeductionBreakdownLine>();

            foreach (PosMerchantMovementLine movement in movements)
            {
                if (movement.ReceiptType != BankAccountPosHelper.CustomerCreditCardCollectionReceiptType
                    && movement.ReceiptType != BankAccountPosHelper.CustomerCreditCardRefundReceiptType)
                {
                    continue;
                }

                DateTime day = movement.ReceiptDate.Date;
                if (day < periodStart || day > periodEnd)
                    continue;

                if (!dailyMap.TryGetValue(day, out PosPeriodDailyLine daily))
                {
                    daily = new PosPeriodDailyLine { Day = day };
                    dailyMap[day] = daily;
                }

                if (movement.ReceiptType == BankAccountPosHelper.CustomerCreditCardCollectionReceiptType)
                {
                    daily.CollectionCount++;
                    daily.GrossAmount += movement.GrossAmount;
                    IList<PosDeductionProfileLine> matchedProfiles =
                        PosDeductionCalculationService.FilterProfilesForMovement(
                            profiles,
                            movement.CardCategory,
                            movement.InstallmentCount);
                    IList<PosDeductionBreakdownLine> breakdown =
                        PosDeductionCalculationService.BuildBreakdown(movement.GrossAmount, matchedProfiles, amountDec);
                    PosDeductionCalculationService.MergeDeductionTotals(periodDeductionTotals, breakdown);
                    daily = PosDeductionCalculationService.MapDeductionColumns(daily, MergeBreakdownLists(daily.Deductions, breakdown));
                    daily.NetAmount += movement.NetAmount;
                }
                else
                {
                    daily.RefundCount++;
                    daily.RefundAmount += movement.RefundAmount;
                    daily.NetAmount -= movement.RefundAmount;
                }
            }

            result.DailyLines = dailyMap.Values.OrderBy(line => line.Day).ToList();
            result.PeriodDeductions = periodDeductionTotals.Values
                .OrderBy(line => line.DeductionTypeCode)
                .ToList();
        }

        static IList<PosDeductionBreakdownLine> MergeBreakdownLists(
            IEnumerable<PosDeductionBreakdownLine> left,
            IEnumerable<PosDeductionBreakdownLine> right)
        {
            var map = new Dictionary<long, PosDeductionBreakdownLine>();
            PosDeductionCalculationService.MergeDeductionTotals(map, left);
            PosDeductionCalculationService.MergeDeductionTotals(map, right);
            return map.Values.ToList();
        }

        static void BuildSettlementLines(
            PosMerchantAggregationResult result,
            IList<PosMerchantMovementLine> movements,
            DateTime periodStart,
            DateTime periodEnd,
            DateTime nextMonthStart,
            DateTime nextMonthEnd)
        {
            var settlementMap = new Dictionary<string, PosPeriodSettlementLine>();

            foreach (PosMerchantMovementLine movement in movements)
            {
                DateTime settlementDate = movement.SettlementDate.Date;
                byte kind = ResolveSettlementKind(settlementDate, periodStart, periodEnd, nextMonthStart, nextMonthEnd);
                if (kind == 0)
                    continue;

                string key = $"{kind}:{settlementDate:yyyyMMdd}";
                if (!settlementMap.TryGetValue(key, out PosPeriodSettlementLine line))
                {
                    line = new PosPeriodSettlementLine
                    {
                        SettlementDate = settlementDate,
                        SettlementKind = kind
                    };
                    settlementMap[key] = line;
                }

                if (movement.ReceiptType == BankAccountPosHelper.CustomerCreditCardCollectionReceiptType)
                {
                    line.GrossAmount += movement.GrossAmount;
                    line.DeductionAmount += movement.DeductionAmount;
                    line.NetAmount += movement.NetAmount;
                }
                else if (movement.ReceiptType == BankAccountPosHelper.CustomerCreditCardRefundReceiptType)
                {
                    line.RefundAmount += movement.RefundAmount;
                    line.NetAmount -= movement.RefundAmount;
                }
            }

            result.SettlementLines = settlementMap.Values
                .OrderBy(line => line.SettlementKind)
                .ThenBy(line => line.SettlementDate)
                .ToList();
        }

        static byte ResolveSettlementKind(
            DateTime settlementDate,
            DateTime periodStart,
            DateTime periodEnd,
            DateTime nextMonthStart,
            DateTime nextMonthEnd)
        {
            if (settlementDate >= periodStart && settlementDate <= periodEnd)
                return BankAccountPosHelper.SettlementKindCurrentMonth;

            if (settlementDate >= nextMonthStart && settlementDate <= nextMonthEnd)
                return BankAccountPosHelper.SettlementKindNextMonth;

            return 0;
        }

        static void BuildFutureReceivableLines(
            PosMerchantAggregationResult result,
            IList<PosMerchantMovementLine> movements,
            DateTime nextMonthEnd)
        {
            var map = new Dictionary<string, PosPeriodFutureReceivableLine>();

            foreach (PosMerchantMovementLine movement in movements)
            {
                if (movement.ReceiptType != BankAccountPosHelper.CustomerCreditCardCollectionReceiptType)
                    continue;

                DateTime settlementDate = movement.SettlementDate.Date;
                if (settlementDate <= nextMonthEnd)
                    continue;

                string key = $"{settlementDate.Year:0000}-{settlementDate.Month:00}";
                if (!map.TryGetValue(key, out PosPeriodFutureReceivableLine line))
                {
                    line = new PosPeriodFutureReceivableLine
                    {
                        PeriodYear = settlementDate.Year,
                        PeriodMonth = settlementDate.Month
                    };
                    map[key] = line;
                }

                line.GrossAmount += movement.GrossAmount;
                line.NetAmount += movement.NetAmount;
            }

            result.FutureReceivableLines = map.Values
                .OrderBy(line => line.PeriodYear)
                .ThenBy(line => line.PeriodMonth)
                .ToList();
        }

        static void BuildSummary(PosMerchantAggregationResult result)
        {
            PosPeriodSummaryKpi summary = result.Summary;
            summary.TotalGross = result.DailyLines.Sum(line => line.GrossAmount);
            summary.TotalRefund = result.DailyLines.Sum(line => line.RefundAmount);
            summary.TotalDeduction = result.PeriodDeductions.Sum(line => line.Amount);
            summary.TotalNet = result.DailyLines.Sum(line => line.NetAmount);
            summary.CurrentMonthSettlementNet = result.SettlementLines
                .Where(line => line.SettlementKind == BankAccountPosHelper.SettlementKindCurrentMonth)
                .Sum(line => line.NetAmount);
            summary.NextMonthReceivableNet = result.SettlementLines
                .Where(line => line.SettlementKind == BankAccountPosHelper.SettlementKindNextMonth)
                .Sum(line => line.NetAmount);
            summary.FutureReceivableNet = result.FutureReceivableLines.Sum(line => line.NetAmount);
        }

        static IList<PosMerchantMovementLine> LoadSettlementMovements(
            LiveSession session,
            long bankAccountId,
            DateTime loadStart,
            DateTime loadEnd,
            IList<PosDeductionProfileLine> profiles,
            int amountDec)
        {
            var lines = new List<PosMerchantMovementLine>();
            int companyId = session.ActiveCompany.RecId ?? 0;

            DataTable itemTable = UtilityFunctions.GetDataTableList(
                session._dbInfo.DBProvider,
                session._dbInfo.Connection,
                null,
                "PosMerchantCurrentAccountItems",
                $@"select cri.RecId,
                          cri.ReceiptDate,
                          cri.TermDate,
                          cri.InstalmentStartDate,
                          isnull(cri.InstallmentCount,1) InstallmentCount,
                          cri.UD_PosCardCategory,
                          cri.UD_PosCardSource,
                          cri.Debit,
                          cri.Credit,
                          cr.ReceiptType,
                          cr.ReceiptNo,
                          cri.Explanation
                   from Erp_CurrentAccountReceiptItem cri with (nolock)
                   inner join Erp_CurrentAccountReceipt cr with (nolock) on cr.RecId = cri.CurrentAccountReceiptId
                   inner join Erp_BankAccount ba with (nolock) on ba.RecId = cri.BankAccountId
                   inner join Erp_Bank b with (nolock) on b.RecId = ba.BankId
                   where b.CompanyId = {companyId}
                     and cri.BankAccountId = {bankAccountId}
                     and cr.ReceiptType in ({BankAccountPosHelper.CustomerCreditCardCollectionReceiptType},{BankAccountPosHelper.CustomerCreditCardRefundReceiptType})
                     and isnull(cri.IsDeleted,0)=0
                     and isnull(cr.IsDeleted,0)=0
                     and isnull(ba.IsDeleted,0)=0
                     and isnull(b.IsDeleted,0)=0
                     and cri.ReceiptDate >= '{loadStart:yyyy-MM-dd}'
                     and cri.ReceiptDate <= '{loadEnd:yyyy-MM-dd}'
                   order by cri.ReceiptDate, cri.RecId");

            if (itemTable == null || itemTable.Rows.Count == 0)
                return lines;

            var criIds = itemTable.Rows.Cast<DataRow>()
                .Select(row => Convert.ToInt64(row["RecId"]))
                .Distinct()
                .ToList();

            Dictionary<long, List<DataRow>> paymentItemsByCri = LoadReceiptPaymentItems(session, companyId, criIds);

            foreach (DataRow itemRow in itemTable.Rows)
            {
                long criId = Convert.ToInt64(itemRow["RecId"]);
                short receiptType = Convert.ToInt16(itemRow["ReceiptType"]);
                DateTime receiptDate = Convert.ToDateTime(itemRow["ReceiptDate"]).Date;
                decimal itemAmount = GetItemAmount(receiptType, itemRow, amountDec);
                if (itemAmount <= 0m)
                    continue;

                if (paymentItemsByCri.TryGetValue(criId, out List<DataRow> paymentRows) && paymentRows.Count > 0)
                {
                    decimal paymentTotal = paymentRows.Sum(row => Convert.ToDecimal(row["Amount"]));
                    foreach (DataRow paymentRow in paymentRows)
                    {
                        decimal portion = Convert.ToDecimal(paymentRow["Amount"]);
                        if (portion <= 0m)
                            continue;

                        decimal ratio = paymentTotal > 0m ? portion / paymentTotal : 1m;
                        AppendMovementLine(
                            lines,
                            itemRow,
                            receiptType,
                            receiptDate,
                            Convert.ToDateTime(paymentRow["TermDate"]).Date,
                            ScaleAmount(itemAmount, ratio, amountDec),
                            profiles,
                            amountDec,
                            Convert.ToInt16(itemRow["InstallmentCount"]),
                            Convert.ToInt16(paymentRow["InstallmentNo"]),
                            ReadNullableShort(itemRow, "UD_PosCardCategory"),
                            ReadNullableShort(itemRow, "UD_PosCardSource"));
                    }

                    continue;
                }

                DateTime settlementDate = itemRow.IsNull("TermDate")
                    ? receiptDate
                    : Convert.ToDateTime(itemRow["TermDate"]).Date;

                AppendMovementLine(
                    lines,
                    itemRow,
                    receiptType,
                    receiptDate,
                    settlementDate,
                    itemAmount,
                    profiles,
                    amountDec,
                    Convert.ToInt16(itemRow["InstallmentCount"]),
                    1,
                    ReadNullableShort(itemRow, "UD_PosCardCategory"),
                    ReadNullableShort(itemRow, "UD_PosCardSource"));
            }

            return lines;
        }

        static short? ReadNullableShort(DataRow row, string columnName)
        {
            if (row == null || !row.Table.Columns.Contains(columnName) || row.IsNull(columnName))
                return null;

            short value = Convert.ToInt16(row[columnName]);
            return value > 0 ? value : null;
        }

        static Dictionary<long, List<DataRow>> LoadReceiptPaymentItems(
            LiveSession session,
            int companyId,
            IList<long> criIds)
        {
            var map = new Dictionary<long, List<DataRow>>();
            if (criIds == null || criIds.Count == 0)
                return map;

            string idList = string.Join(",", criIds);
            DataTable table = UtilityFunctions.GetDataTableList(
                session._dbInfo.DBProvider,
                session._dbInfo.Connection,
                null,
                "Erp_ReceiptPaymentItem",
                $@"select rpi.SourceItemId,
                          rpi.TermDate,
                          rpi.Amount,
                          isnull(rpi.InstallmentNo,1) InstallmentNo
                   from Erp_ReceiptPaymentItem rpi with (nolock)
                   where rpi.SourceModule = {FinanceSourceModule}
                     and isnull(rpi.IsDeleted,0)=0
                     and rpi.SourceItemId in ({idList})
                   order by rpi.SourceItemId, rpi.TermDate, rpi.InstallmentNo");

            if (table == null)
                return map;

            foreach (DataRow row in table.Rows)
            {
                if (row.IsNull("SourceItemId") || row.IsNull("TermDate") || row.IsNull("Amount"))
                    continue;

                long criId = Convert.ToInt64(row["SourceItemId"]);
                if (!map.TryGetValue(criId, out List<DataRow> list))
                {
                    list = new List<DataRow>();
                    map[criId] = list;
                }

                list.Add(row);
            }

            return map;
        }

        static void AppendMovementLine(
            IList<PosMerchantMovementLine> lines,
            DataRow itemRow,
            short receiptType,
            DateTime receiptDate,
            DateTime settlementDate,
            decimal amount,
            IList<PosDeductionProfileLine> profiles,
            int amountDec,
            short installmentCount,
            short installmentNo,
            short? cardCategory,
            short? cardSource)
        {
            decimal gross = 0m;
            decimal refund = 0m;
            decimal deduction = 0m;
            decimal net = 0m;

            IList<PosDeductionProfileLine> matchedProfiles =
                PosDeductionCalculationService.FilterProfilesForMovement(profiles, cardCategory, installmentCount);

            if (receiptType == BankAccountPosHelper.CustomerCreditCardCollectionReceiptType)
            {
                gross = amount;
                deduction = PosDeductionCalculationService.CalculateTotalDeduction(gross, matchedProfiles, amountDec);
                net = gross - deduction;
            }
            else
            {
                refund = amount;
            }

            lines.Add(new PosMerchantMovementLine
            {
                CurrentAccountReceiptItemId = Convert.ToInt64(itemRow["RecId"]),
                ReceiptType = receiptType,
                ReceiptDate = receiptDate,
                SettlementDate = settlementDate,
                GrossAmount = gross,
                RefundAmount = refund,
                DeductionAmount = deduction,
                NetAmount = net,
                InstallmentCount = installmentCount,
                InstallmentNo = installmentNo,
                CardCategory = cardCategory,
                CardSource = cardSource,
                ReceiptNo = itemRow.IsNull("ReceiptNo") ? string.Empty : Convert.ToString(itemRow["ReceiptNo"]),
                Explanation = itemRow.IsNull("Explanation") ? string.Empty : Convert.ToString(itemRow["Explanation"])
            });
        }

        static decimal GetItemAmount(short receiptType, DataRow itemRow, int amountDec)
        {
            decimal debit = itemRow.IsNull("Debit") ? 0m : Convert.ToDecimal(itemRow["Debit"]);
            decimal credit = itemRow.IsNull("Credit") ? 0m : Convert.ToDecimal(itemRow["Credit"]);

            decimal amount = receiptType == BankAccountPosHelper.CustomerCreditCardRefundReceiptType
                ? (debit != 0m ? debit : credit)
                : (credit != 0m ? credit : debit);

            return Math.Round(amount, amountDec, MidpointRounding.AwayFromZero);
        }

        static decimal ScaleAmount(decimal amount, decimal ratio, int amountDec)
        {
            return Math.Round(amount * ratio, amountDec, MidpointRounding.AwayFromZero);
        }
    }
}
