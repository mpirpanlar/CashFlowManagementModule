using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;

using CashFlowManagementModule.BoExtensions;

using Sentez.Common.SystemServices;
using Sentez.Core.ParameterClasses;
using Sentez.Data.Tools;

namespace CashFlowManagementModule.Services
{
    public static class CreditCardPeriodPaymentSummaryService
    {
        const int FinanceSourceModule = 3;

        public static void RefreshSummary(LiveSession session, DataSet data, long bankAccountId)
        {
            if (session?._dbInfo?.Connection == null || data == null || bankAccountId <= 0)
                return;

            CreditCardPaymentDueDaysSyncService.EnsureVirtualColumns(data);

            if (!data.Tables.Contains(BankAccountCreditCardHelper.PeriodTableName))
                return;

            var periodTable = data.Tables[BankAccountCreditCardHelper.PeriodTableName];
            var periods = LoadPeriodsFromTable(periodTable);
            int companyId = session.ActiveCompany.RecId ?? 0;
            CashFlowDbContext context = CashFlowDbContext.FromSession(session);
            DataRow bankAccountRow = GetBankAccountRow(data);

            var spendingLines = LoadCurrentAccountReceiptLines(
                context, companyId, bankAccountId, 51, preferDebit: true, bankAccountRow);
            var refundLines = LoadCurrentAccountReceiptLines(
                context, companyId, bankAccountId, 53, preferDebit: false, bankAccountRow);
            var paymentLines = LoadVirmanPaymentLines(
                context, companyId, bankAccountId, bankAccountRow);

            var spendingTotals = BuildCurrentAccountPeriodTotals(
                context, companyId, bankAccountId, periods, 51, spendingLines, bankAccountRow);
            var refundTotals = BuildCurrentAccountPeriodTotals(
                context, companyId, bankAccountId, periods, 53, refundLines, bankAccountRow);
            var paymentTotals = BuildPeriodTotals(periods, paymentLines);

            int amountDec = session.ParamService?.GetParameterClass<GeneralParameters>()?.AmountDec ?? 2;
            var creditLimit = GetCreditLimit(data);
            ApplyPeriodAmountColumns(periodTable, spendingTotals, refundTotals, paymentTotals, amountDec);
            ApplyPeriodLimitColumns(periodTable, creditLimit, spendingTotals, refundTotals, paymentTotals, amountDec);

            ApplyHeaderLimitSummaryFromCurrentPeriod(data, periodTable, periods, amountDec);
        }

        public static decimal? TryGetPeriodSpendingTotal(
            LiveSession session,
            long bankAccountId,
            DateTime referenceDate,
            out CreditCardPeriodInfo matchedPeriod)
        {
            matchedPeriod = null;
            if (session?._dbInfo?.Connection == null || bankAccountId <= 0)
                return null;

            int companyId = session.ActiveCompany.RecId ?? 0;
            CashFlowDbContext context = CashFlowDbContext.FromSession(session);
            IList<CreditCardPeriodInfo> periods = CreditCardStatementDataService.LoadActivePeriods(context, bankAccountId);
            if (periods == null || periods.Count == 0)
                return null;

            int periodIndex = CreditCardStatementDataService.FindPeriodIndexByStatementCycle(periods, referenceDate);
            if (periodIndex < 0 || periodIndex >= periods.Count)
                return null;

            matchedPeriod = periods[periodIndex];
            DataRow bankAccountRow = LoadBankAccountRowForSummary(context, bankAccountId);
            var spendingLines = LoadCurrentAccountReceiptLines(
                context, companyId, bankAccountId, 51, preferDebit: true, bankAccountRow);
            var spendingTotals = BuildCurrentAccountPeriodTotals(
                context, companyId, bankAccountId, periods, 51, spendingLines, bankAccountRow);

            int amountDec = session.ParamService?.GetParameterClass<GeneralParameters>()?.AmountDec ?? 2;
            if (!spendingTotals.TryGetValue(periodIndex, out decimal total))
                return 0m;

            return RoundAmount(total, amountDec);
        }

        public static void ApplyHeaderLimitSummaryFromCurrentPeriod(
            DataSet data,
            DataTable periodTable,
            IList<CreditCardPeriodInfo> periods,
            int amountDec = 2)
        {
            if (data == null || periodTable == null || periods == null || periods.Count == 0)
            {
                ApplyLimitSummary(data, 0m, 0m);
                return;
            }

            int periodIndex = CreditCardStatementDataService.FindPeriodIndexByStatementCycle(periods, DateTime.Today);
            if (periodIndex < 0)
            {
                ApplyLimitSummary(data, 0m, 0m);
                return;
            }

            var activeRows = GetActivePeriodRows(periodTable);
            if (periodIndex >= activeRows.Count)
            {
                ApplyLimitSummary(data, 0m, 0m);
                return;
            }

            DataRow periodRow = activeRows[periodIndex];
            decimal spending = GetDecimalFromRow(periodRow, BankAccountCreditCardHelper.FieldPeriodSpendingTotal);
            decimal refunds = GetDecimalFromRow(periodRow, BankAccountCreditCardHelper.FieldPeriodRefundTotal);
            decimal payments = GetDecimalFromRow(periodRow, BankAccountCreditCardHelper.FieldPeriodCardPaymentTotal);
            decimal totalLimit = GetDecimalFromRow(periodRow, BankAccountCreditCardHelper.FieldPeriodTotalCreditLimit);

            if (totalLimit == 0m)
            {
                decimal? creditLimit = GetCreditLimit(data);
                if (creditLimit.HasValue)
                    totalLimit = creditLimit.Value;
            }

            decimal usedLimit = RoundAmount(spending - refunds - payments, amountDec);
            decimal remainingLimit = CalculatePeriodRemainingLimit(totalLimit, spending, refunds, payments, amountDec);

            ApplyLimitSummary(data, usedLimit, remainingLimit);
        }

        public static IList<CreditCardPeriodMovementLine> LoadCurrentAccountReceiptLines(
            CashFlowDbContext context,
            int companyId,
            long bankAccountId,
            short receiptType,
            bool preferDebit,
            DataRow bankAccountRow = null)
        {
            return LoadCurrentAccountReceiptLines(
                context.Provider,
                context.Connection,
                context.Transaction,
                companyId,
                bankAccountId,
                receiptType,
                preferDebit,
                bankAccountRow);
        }

        public static IList<CreditCardPeriodMovementLine> LoadCurrentAccountReceiptLines(
            ProviderType provider,
            DbConnection connection,
            DbTransaction transaction,
            int companyId,
            long bankAccountId,
            short receiptType,
            bool preferDebit,
            DataRow bankAccountRow = null)
        {
            var lines = new List<CreditCardPeriodMovementLine>();
            if (bankAccountId <= 0)
                return lines;

            string amountSql = preferDebit
                ? @"case when IsNull(cri.Debit, 0) <> 0 then IsNull(cri.Debit, 0)
                        when IsNull(cri.Credit, 0) <> 0 then IsNull(cri.Credit, 0)
                        else 0 end"
                : @"case when IsNull(cri.Credit, 0) <> 0 then IsNull(cri.Credit, 0)
                        when IsNull(cri.Debit, 0) <> 0 then IsNull(cri.Debit, 0)
                        else 0 end";

            string dateFilter = BuildReceiptDateFilter(bankAccountRow, "cri.ReceiptDate");

            DataTable table = CashFlowDbAccess.GetDataTable(
                CashFlowDbContext.From(connection, transaction, provider, keepConnectionOpen: true),
                "Erp_CurrentAccountReceiptItem",
                $@"select cri.RecId,
                          cri.ReceiptDate,
                          cri.TermDate,
                          cri.InstalmentStartDate,
                          cr.ReceiptDate HeaderReceiptDate,
                          IsNull(cri.InstallmentCount, 1) InstallmentCount,
                          {amountSql} TotalAmount
                   from Erp_CurrentAccountReceiptItem cri with (nolock)
                   inner join Erp_CurrentAccountReceipt cr with (nolock) on cr.RecId = cri.CurrentAccountReceiptId
                   inner join Erp_BankAccount ba with (nolock) on ba.RecId = cri.BankAccountId
                   inner join Erp_Bank b with (nolock) on b.RecId = ba.BankId
                   where b.CompanyId = {companyId}
                     and cri.BankAccountId = {bankAccountId}
                     and cr.ReceiptType = {receiptType}
                     and IsNull(cri.IsDeleted, 0) = 0
                     and IsNull(cr.IsDeleted, 0) = 0
                     and IsNull(ba.IsDeleted, 0) = 0
                     and IsNull(b.IsDeleted, 0) = 0
                     {dateFilter}
                   order by cri.ReceiptDate, cri.RecId");

            return MapCurrentAccountMovementLines(table);
        }

        public static IList<CreditCardPeriodMovementLine> LoadVirmanPaymentLines(
            CashFlowDbContext context,
            int companyId,
            long bankAccountId,
            DataRow bankAccountRow = null)
        {
            return LoadVirmanPaymentLines(
                context.Provider,
                context.Connection,
                context.Transaction,
                companyId,
                bankAccountId,
                bankAccountRow);
        }

        public static IList<CreditCardPeriodMovementLine> LoadVirmanPaymentLines(
            ProviderType provider,
            DbConnection connection,
            DbTransaction transaction,
            int companyId,
            long bankAccountId,
            DataRow bankAccountRow = null)
        {
            var lines = new List<CreditCardPeriodMovementLine>();
            if (bankAccountId <= 0)
                return lines;

            string dateFilter = BuildReceiptDateFilter(bankAccountRow, "bri.ReceiptDate");

            DataTable table = CashFlowDbAccess.GetDataTable(
                CashFlowDbContext.From(connection, transaction, provider, keepConnectionOpen: true),
                "Erp_BankReceiptItem",
                $@"select bri.RecId,
                          bri.ReceiptDate,
                          1 InstallmentCount,
                          case when IsNull(bri.Debit, 0) <> 0 then IsNull(bri.Debit, 0)
                               when IsNull(bri.Credit, 0) <> 0 then IsNull(bri.Credit, 0)
                               else 0 end TotalAmount
                   from Erp_BankReceiptItem bri with (nolock)
                   inner join Erp_BankReceipt br with (nolock) on br.RecId = bri.BankReceiptId
                   where br.CompanyId = {companyId}
                     and bri.BankAccountId = {bankAccountId}
                     and br.ReceiptType = 2
                     and IsNull(bri.IsDeleted, 0) = 0
                     and IsNull(br.IsDeleted, 0) = 0
                     and IsNull(br.IsCancelled, 0) = 0
                     {dateFilter}
                   order by bri.ReceiptDate, bri.RecId");

            return MapMovementLines(table);
        }

        static IList<CreditCardPeriodMovementLine> MapCurrentAccountMovementLines(DataTable table)
        {
            var lines = new List<CreditCardPeriodMovementLine>();
            if (table == null)
                return lines;

            foreach (DataRow row in table.Rows)
            {
                decimal amount = Convert.ToDecimal(row["TotalAmount"]);
                if (amount == 0m)
                    continue;

                short installmentCount = Convert.ToInt16(row["InstallmentCount"]);
                if (installmentCount < 1)
                    installmentCount = 1;

                DateTime? headerReceiptDate = null;
                if (table.Columns.Contains("HeaderReceiptDate") && !row.IsNull("HeaderReceiptDate"))
                    headerReceiptDate = Convert.ToDateTime(row["HeaderReceiptDate"]).Date;

                DateTime? referenceDate = CurrentAccountReceiptCreditCardHelper.ResolveInstalmentStartDate(row, headerReceiptDate);
                if (!referenceDate.HasValue)
                    continue;

                lines.Add(new CreditCardPeriodMovementLine
                {
                    RecId = Convert.ToInt64(row["RecId"]),
                    ReceiptDate = referenceDate.Value,
                    InstallmentCount = installmentCount,
                    TotalAmount = amount
                });
            }

            return lines;
        }

        static IList<CreditCardPeriodMovementLine> MapMovementLines(DataTable table)
        {
            var lines = new List<CreditCardPeriodMovementLine>();
            if (table == null)
                return lines;

            foreach (DataRow row in table.Rows)
            {
                decimal amount = Convert.ToDecimal(row["TotalAmount"]);
                if (amount == 0m)
                    continue;

                short installmentCount = Convert.ToInt16(row["InstallmentCount"]);
                if (installmentCount < 1)
                    installmentCount = 1;

                lines.Add(new CreditCardPeriodMovementLine
                {
                    RecId = Convert.ToInt64(row["RecId"]),
                    ReceiptDate = Convert.ToDateTime(row["ReceiptDate"]).Date,
                    InstallmentCount = installmentCount,
                    TotalAmount = amount
                });
            }

            return lines;
        }

        public static Dictionary<int, decimal> BuildPeriodTotals(
            IList<CreditCardPeriodInfo> periods,
            IList<CreditCardPeriodMovementLine> movementLines)
        {
            var totals = new Dictionary<int, decimal>();
            if (periods == null || periods.Count == 0 || movementLines == null)
                return totals;

            foreach (CreditCardPeriodMovementLine line in movementLines)
            {
                int startIndex = CreditCardStatementDataService.FindPeriodIndexByStatementCycle(periods, line.ReceiptDate);
                if (startIndex < 0)
                    continue;

                short installmentCount = line.InstallmentCount;
                if (startIndex + installmentCount > periods.Count)
                    installmentCount = (short)(periods.Count - startIndex);

                if (installmentCount < 1)
                    continue;

                decimal[] amounts = CreditCardStatementDataService.SplitAmount(line.TotalAmount, installmentCount);
                for (int i = 0; i < installmentCount; i++)
                {
                    int periodIndex = startIndex + i;
                    if (!totals.ContainsKey(periodIndex))
                        totals[periodIndex] = 0m;

                    totals[periodIndex] += amounts[i];
                }
            }

            return totals;
        }

        static Dictionary<int, decimal> BuildCurrentAccountPeriodTotals(
            CashFlowDbContext context,
            int companyId,
            long bankAccountId,
            IList<CreditCardPeriodInfo> periods,
            short receiptType,
            IList<CreditCardPeriodMovementLine> movementLines,
            DataRow bankAccountRow)
        {
            var totals = BuildPeriodTotalsFromAllocations(context, companyId, bankAccountId, periods, receiptType);

            HashSet<long> allocatedCriIds = LoadAllocatedCurrentAccountReceiptItemIds(
                context, companyId, bankAccountId, receiptType);
            HashSet<long> criWithRpi = LoadCurrentAccountRecIdsWithReceiptPaymentItems(
                context, companyId, bankAccountId, receiptType);

            var excludedFromMovement = new HashSet<long>(allocatedCriIds);
            foreach (long recId in criWithRpi)
                excludedFromMovement.Add(recId);

            MergePeriodTotals(totals, BuildPeriodTotals(periods, ExcludeMovementLines(movementLines, excludedFromMovement)));
            MergePeriodTotals(
                totals,
                BuildPeriodTotalsFromReceiptPaymentItems(
                    context, companyId, bankAccountId, periods, receiptType, bankAccountRow, allocatedCriIds));

            return totals;
        }

        static Dictionary<int, decimal> BuildPeriodTotalsFromAllocations(
            CashFlowDbContext context,
            int companyId,
            long bankAccountId,
            IList<CreditCardPeriodInfo> periods,
            short receiptType)
        {
            var totals = new Dictionary<int, decimal>();
            if (periods == null || periods.Count == 0 || bankAccountId <= 0)
                return totals;

            var periodIndexByRecId = new Dictionary<long, int>();
            for (int i = 0; i < periods.Count; i++)
            {
                if (periods[i].RecId > 0)
                    periodIndexByRecId[periods[i].RecId] = i;
            }

            DataTable table = CashFlowDbAccess.GetDataTable(
                context,
                BankReceiptCreditCardHelper.AllocationTableName,
                $@"select a.CreditCardPeriodId,
                          sum(a.Amount) TotalAmount
                   from Erp_BankAccountCreditCardPeriodAllocation a with (nolock)
                   inner join Erp_CurrentAccountReceiptItem cri with (nolock) on cri.RecId = a.CurrentAccountReceiptItemId
                   inner join Erp_CurrentAccountReceipt cr with (nolock) on cr.RecId = cri.CurrentAccountReceiptId
                   where a.CompanyId = {companyId}
                     and a.BankAccountId = {bankAccountId}
                     and cr.ReceiptType = {receiptType}
                     and IsNull(a.IsDeleted, 0) = 0
                     and IsNull(cri.IsDeleted, 0) = 0
                     and IsNull(cr.IsDeleted, 0) = 0
                   group by a.CreditCardPeriodId");

            if (table == null)
                return totals;

            foreach (DataRow row in table.Rows)
            {
                if (row.IsNull("CreditCardPeriodId") || row.IsNull("TotalAmount"))
                    continue;

                long periodRecId = Convert.ToInt64(row["CreditCardPeriodId"]);
                if (!periodIndexByRecId.TryGetValue(periodRecId, out int periodIndex))
                    continue;

                decimal amount = Convert.ToDecimal(row["TotalAmount"]);
                if (amount == 0m)
                    continue;

                if (!totals.ContainsKey(periodIndex))
                    totals[periodIndex] = 0m;

                totals[periodIndex] += amount;
            }

            return totals;
        }

        static HashSet<long> LoadAllocatedCurrentAccountReceiptItemIds(
            CashFlowDbContext context,
            int companyId,
            long bankAccountId,
            short receiptType)
        {
            var recIds = new HashSet<long>();
            if (bankAccountId <= 0)
                return recIds;

            DataTable table = CashFlowDbAccess.GetDataTable(
                context,
                BankReceiptCreditCardHelper.AllocationTableName,
                $@"select distinct a.CurrentAccountReceiptItemId
                   from Erp_BankAccountCreditCardPeriodAllocation a with (nolock)
                   inner join Erp_CurrentAccountReceiptItem cri with (nolock) on cri.RecId = a.CurrentAccountReceiptItemId
                   inner join Erp_CurrentAccountReceipt cr with (nolock) on cr.RecId = cri.CurrentAccountReceiptId
                   where a.CompanyId = {companyId}
                     and a.BankAccountId = {bankAccountId}
                     and cr.ReceiptType = {receiptType}
                     and IsNull(a.IsDeleted, 0) = 0
                     and IsNull(cri.IsDeleted, 0) = 0
                     and IsNull(cr.IsDeleted, 0) = 0");

            if (table == null)
                return recIds;

            foreach (DataRow row in table.Rows)
            {
                if (!row.IsNull("CurrentAccountReceiptItemId"))
                    recIds.Add(Convert.ToInt64(row["CurrentAccountReceiptItemId"]));
            }

            return recIds;
        }

        static Dictionary<int, decimal> BuildPeriodTotalsFromReceiptPaymentItems(
            CashFlowDbContext context,
            int companyId,
            long bankAccountId,
            IList<CreditCardPeriodInfo> periods,
            short receiptType,
            DataRow bankAccountRow,
            HashSet<long> excludedCriIds = null)
        {
            var totals = new Dictionary<int, decimal>();
            if (periods == null || periods.Count == 0 || bankAccountId <= 0)
                return totals;

            string excludedCriFilter = excludedCriIds != null && excludedCriIds.Count > 0
                ? $" and cri.RecId not in ({string.Join(",", excludedCriIds)})"
                : string.Empty;

            string dateFilter = BuildReceiptDateFilter(bankAccountRow, "rpi.TermDate");
            DataTable table = CashFlowDbAccess.GetDataTable(
                context,
                "Erp_ReceiptPaymentItem",
                $@"select rpi.TermDate,
                          rpi.Amount
                   from Erp_ReceiptPaymentItem rpi with (nolock)
                   inner join Erp_CurrentAccountReceiptItem cri with (nolock) on cri.RecId = rpi.SourceItemId
                   inner join Erp_CurrentAccountReceipt cr with (nolock) on cr.RecId = cri.CurrentAccountReceiptId
                   inner join Erp_BankAccount ba with (nolock) on ba.RecId = cri.BankAccountId
                   inner join Erp_Bank b with (nolock) on b.RecId = ba.BankId
                   where rpi.SourceModule = {FinanceSourceModule}
                     and b.CompanyId = {companyId}
                     and cri.BankAccountId = {bankAccountId}
                     and cr.ReceiptType = {receiptType}
                     and IsNull(rpi.IsDeleted, 0) = 0
                     and IsNull(cri.IsDeleted, 0) = 0
                     and IsNull(cr.IsDeleted, 0) = 0
                     and IsNull(ba.IsDeleted, 0) = 0
                     and IsNull(b.IsDeleted, 0) = 0
                     {excludedCriFilter}
                     {dateFilter}
                   order by rpi.TermDate, rpi.RecId");

            if (table == null)
                return totals;

            foreach (DataRow row in table.Rows)
            {
                if (row.IsNull("TermDate") || row.IsNull("Amount"))
                    continue;

                decimal amount = Convert.ToDecimal(row["Amount"]);
                if (amount == 0m)
                    continue;

                int periodIndex = ResolvePeriodIndexByTermDate(periods, Convert.ToDateTime(row["TermDate"]).Date);
                if (periodIndex < 0)
                    continue;

                if (!totals.ContainsKey(periodIndex))
                    totals[periodIndex] = 0m;

                totals[periodIndex] += amount;
            }

            return totals;
        }

        static HashSet<long> LoadCurrentAccountRecIdsWithReceiptPaymentItems(
            CashFlowDbContext context,
            int companyId,
            long bankAccountId,
            short receiptType)
        {
            var recIds = new HashSet<long>();
            if (bankAccountId <= 0)
                return recIds;

            DataTable table = CashFlowDbAccess.GetDataTable(
                context,
                "Erp_ReceiptPaymentItem",
                $@"select distinct cri.RecId
                   from Erp_ReceiptPaymentItem rpi with (nolock)
                   inner join Erp_CurrentAccountReceiptItem cri with (nolock) on cri.RecId = rpi.SourceItemId
                   inner join Erp_CurrentAccountReceipt cr with (nolock) on cr.RecId = cri.CurrentAccountReceiptId
                   inner join Erp_BankAccount ba with (nolock) on ba.RecId = cri.BankAccountId
                   inner join Erp_Bank b with (nolock) on b.RecId = ba.BankId
                   where rpi.SourceModule = {FinanceSourceModule}
                     and b.CompanyId = {companyId}
                     and cri.BankAccountId = {bankAccountId}
                     and cr.ReceiptType = {receiptType}
                     and IsNull(rpi.IsDeleted, 0) = 0
                     and IsNull(cri.IsDeleted, 0) = 0
                     and IsNull(cr.IsDeleted, 0) = 0");

            if (table == null)
                return recIds;

            foreach (DataRow row in table.Rows)
            {
                if (!row.IsNull("RecId"))
                    recIds.Add(Convert.ToInt64(row["RecId"]));
            }

            return recIds;
        }

        static IList<CreditCardPeriodMovementLine> ExcludeMovementLines(
            IList<CreditCardPeriodMovementLine> movementLines,
            HashSet<long> excludedRecIds)
        {
            if (movementLines == null || movementLines.Count == 0 || excludedRecIds == null || excludedRecIds.Count == 0)
                return movementLines;

            return movementLines.Where(line => !excludedRecIds.Contains(line.RecId)).ToList();
        }

        static void MergePeriodTotals(IDictionary<int, decimal> target, IDictionary<int, decimal> source)
        {
            if (target == null || source == null)
                return;

            foreach (KeyValuePair<int, decimal> item in source)
            {
                if (!target.ContainsKey(item.Key))
                    target[item.Key] = 0m;

                target[item.Key] += item.Value;
            }
        }

        static int ResolvePeriodIndexByTermDate(IList<CreditCardPeriodInfo> periods, DateTime termDate)
        {
            if (periods == null || periods.Count == 0)
                return -1;

            for (int i = 0; i < periods.Count; i++)
            {
                if (periods[i].PaymentDueDate.Date == termDate.Date)
                    return i;
            }

            return CreditCardStatementDataService.FindPeriodIndexByStatementCycle(periods, termDate);
        }

        public static void ApplyPeriodAmountColumns(
            DataTable periodTable,
            IDictionary<int, decimal> spendingTotals,
            IDictionary<int, decimal> refundTotals,
            IDictionary<int, decimal> paymentTotals,
            int amountDec = 2)
        {
            if (periodTable == null)
                return;

            var activeRows = GetActivePeriodRows(periodTable);
            for (int i = 0; i < activeRows.Count; i++)
            {
                SetPeriodAmount(activeRows[i], BankAccountCreditCardHelper.FieldPeriodSpendingTotal, spendingTotals, i, amountDec);
                SetPeriodAmount(activeRows[i], BankAccountCreditCardHelper.FieldPeriodRefundTotal, refundTotals, i, amountDec);
                SetPeriodAmount(activeRows[i], BankAccountCreditCardHelper.FieldPeriodCardPaymentTotal, paymentTotals, i, amountDec);
            }
        }

        public static void ApplyPeriodLimitColumns(
            DataTable periodTable,
            decimal? creditLimit,
            IDictionary<int, decimal> spendingTotals,
            IDictionary<int, decimal> refundTotals,
            IDictionary<int, decimal> paymentTotals,
            int amountDec = 2)
        {
            if (periodTable == null)
                return;

            var activeRows = GetActivePeriodRows(periodTable);
            for (int i = 0; i < activeRows.Count; i++)
            {
                EnsurePeriodTotalCreditLimit(activeRows[i], creditLimit, amountDec);

                decimal totalLimit = GetDecimalFromRow(activeRows[i], BankAccountCreditCardHelper.FieldPeriodTotalCreditLimit);
                decimal spending = GetPeriodTotal(spendingTotals, i, amountDec);
                decimal refunds = GetPeriodTotal(refundTotals, i, amountDec);
                decimal payments = GetPeriodTotal(paymentTotals, i, amountDec);
                decimal remainingLimit = CalculatePeriodRemainingLimit(totalLimit, spending, refunds, payments, amountDec);

                SetPeriodDecimalValue(activeRows[i], BankAccountCreditCardHelper.FieldPeriodRemainingCreditLimit, remainingLimit, amountDec);
            }
        }

        public static void RecalculatePeriodRemainingLimitRow(DataRow periodRow, int amountDec = 2)
        {
            if (periodRow == null || periodRow.RowState == DataRowState.Deleted)
                return;

            decimal totalLimit = GetDecimalFromRow(periodRow, BankAccountCreditCardHelper.FieldPeriodTotalCreditLimit);
            decimal spending = GetDecimalFromRow(periodRow, BankAccountCreditCardHelper.FieldPeriodSpendingTotal);
            decimal refunds = GetDecimalFromRow(periodRow, BankAccountCreditCardHelper.FieldPeriodRefundTotal);
            decimal payments = GetDecimalFromRow(periodRow, BankAccountCreditCardHelper.FieldPeriodCardPaymentTotal);
            decimal remainingLimit = CalculatePeriodRemainingLimit(totalLimit, spending, refunds, payments, amountDec);

            SetPeriodDecimalValue(periodRow, BankAccountCreditCardHelper.FieldPeriodRemainingCreditLimit, remainingLimit, amountDec);
        }

        public static decimal CalculatePeriodRemainingLimit(
            decimal totalLimit,
            decimal spending,
            decimal refunds,
            decimal payments,
            int amountDec = 2)
        {
            return RoundAmount((totalLimit - spending) + (refunds + payments), amountDec);
        }

        static void EnsurePeriodTotalCreditLimit(DataRow row, decimal? creditLimit, int amountDec)
        {
            if (!row.Table.Columns.Contains(BankAccountCreditCardHelper.FieldPeriodTotalCreditLimit))
                return;

            if (!row.IsNull(BankAccountCreditCardHelper.FieldPeriodTotalCreditLimit))
                return;

            if (!creditLimit.HasValue || creditLimit.Value <= 0m)
                return;

            SetPeriodDecimalValue(row, BankAccountCreditCardHelper.FieldPeriodTotalCreditLimit, creditLimit.Value, amountDec);
        }

        static decimal GetDecimalFromRow(DataRow row, string columnName)
        {
            if (row == null || !row.Table.Columns.Contains(columnName) || row.IsNull(columnName))
                return 0m;

            return Convert.ToDecimal(row[columnName]);
        }

        static decimal GetPeriodTotal(IDictionary<int, decimal> totals, int periodIndex, int amountDec)
        {
            if (totals == null || !totals.TryGetValue(periodIndex, out decimal periodTotal))
                return 0m;

            return RoundAmount(periodTotal, amountDec);
        }

        static void SetPeriodDecimalValue(DataRow row, string columnName, decimal? value, int amountDec)
        {
            if (!row.Table.Columns.Contains(columnName))
                return;

            if (!value.HasValue)
            {
                row[columnName] = DBNull.Value;
                return;
            }

            decimal amount = RoundAmount(value.Value, amountDec);
            row[columnName] = amount == 0m ? DBNull.Value : (object)amount;
        }

        static void SetPeriodAmount(
            DataRow row,
            string columnName,
            IDictionary<int, decimal> totals,
            int periodIndex,
            int amountDec)
        {
            if (!row.Table.Columns.Contains(columnName))
                return;

            decimal amount = 0m;
            if (totals != null && totals.TryGetValue(periodIndex, out decimal periodTotal))
                amount = RoundAmount(periodTotal, amountDec);

            row[columnName] = amount == 0m ? DBNull.Value : (object)amount;
        }

        static decimal RoundAmount(decimal value, int amountDec)
        {
            return Math.Round(value, amountDec, MidpointRounding.AwayFromZero);
        }

        static IList<DataRow> GetActivePeriodRows(DataTable periodTable)
        {
            return periodTable.Rows.Cast<DataRow>()
                .Where(r => r.RowState != DataRowState.Deleted &&
                            (r.IsNull("IsDeleted") || !Convert.ToBoolean(r["IsDeleted"])))
                .OrderBy(r => r.IsNull("PaymentDueDate") ? DateTime.MaxValue : Convert.ToDateTime(r["PaymentDueDate"]))
                .ThenBy(r => r.IsNull("PeriodNo") ? short.MaxValue : Convert.ToInt16(r["PeriodNo"]))
                .ToList();
        }

        public static IList<CreditCardPeriodInfo> LoadPeriodsFromTable(DataTable periodTable)
        {
            var periods = new List<CreditCardPeriodInfo>();
            if (periodTable == null)
                return periods;

            foreach (DataRow row in periodTable.Rows)
            {
                if (row.RowState == DataRowState.Deleted)
                    continue;
                if (!row.IsNull("IsDeleted") && Convert.ToBoolean(row["IsDeleted"]))
                    continue;
                if (row.IsNull("PaymentDueDate"))
                    continue;

                long recId = row.IsNull("RecId") ? 0L : Convert.ToInt64(row["RecId"]);
                short periodYear = row.IsNull("PeriodYear") ? (short)0 : Convert.ToInt16(row["PeriodYear"]);
                short periodMonth = row.IsNull("PeriodMonth") ? (short)0 : Convert.ToInt16(row["PeriodMonth"]);
                periods.Add(new CreditCardPeriodInfo
                {
                    RecId = recId,
                    PeriodNo = row.IsNull("PeriodNo") ? (short)0 : Convert.ToInt16(row["PeriodNo"]),
                    PeriodYear = periodYear,
                    PeriodMonth = periodMonth,
                    StatementStartDate = row.IsNull("StatementStartDate")
                        ? (periodYear > 0 && periodMonth > 0
                            ? new DateTime(periodYear, periodMonth, 1)
                            : DateTime.MinValue)
                        : Convert.ToDateTime(row["StatementStartDate"]).Date,
                    StatementDate = row.IsNull("StatementDate")
                        ? DateTime.MinValue
                        : Convert.ToDateTime(row["StatementDate"]).Date,
                    PaymentDueDate = Convert.ToDateTime(row["PaymentDueDate"]).Date
                });
            }

            return periods
                .OrderBy(p => p.PaymentDueDate)
                .ThenBy(p => p.PeriodNo)
                .ToList();
        }

        static DataRow GetBankAccountRow(DataSet data)
        {
            if (data?.Tables == null || !data.Tables.Contains("Erp_BankAccount"))
                return null;

            var table = data.Tables["Erp_BankAccount"];
            return table.Rows.Count == 0 ? null : table.Rows[0];
        }

        static DataRow LoadBankAccountRowForSummary(CashFlowDbContext context, long bankAccountId)
        {
            if (bankAccountId <= 0 || !context.IsValid)
                return null;

            DataTable table = CashFlowDbAccess.GetDataTable(
                context,
                "Erp_BankAccount",
                $@"select RecId,
                          ChequeCreditLimit,
                          {BankAccountCreditCardHelper.FieldIssueDate},
                          {BankAccountCreditCardHelper.FieldExpiryMonth},
                          {BankAccountCreditCardHelper.FieldExpiryYear}
                   from Erp_BankAccount with (nolock)
                   where RecId = {bankAccountId} and IsNull(IsDeleted, 0) = 0");
            if (table == null || table.Rows.Count == 0)
                return null;

            table.TableName = "Erp_BankAccount";
            return table.Rows[0];
        }

        static string BuildReceiptDateFilter(DataRow bankAccountRow, string dateColumn)
        {
            if (bankAccountRow == null)
                return string.Empty;

            var filters = new List<string>();
            var issueDate = BankAccountCreditCardHelper.GetIssueDate(bankAccountRow);
            if (issueDate.HasValue)
                filters.Add($"{dateColumn} >= '{issueDate.Value:yyyy-MM-dd}'");

            var expiryEnd = BankAccountCreditCardHelper.GetCardExpiryEndDate(bankAccountRow);
            if (expiryEnd.HasValue)
                filters.Add($"{dateColumn} <= '{expiryEnd.Value:yyyy-MM-dd}'");

            if (filters.Count == 0)
                return string.Empty;

            return " and " + string.Join(" and ", filters);
        }

        static decimal? GetCreditLimit(DataSet data)
        {
            if (data?.Tables == null || !data.Tables.Contains("Erp_BankAccount"))
                return null;

            var table = data.Tables["Erp_BankAccount"];
            if (table.Rows.Count == 0 || !table.Columns.Contains("ChequeCreditLimit"))
                return null;

            DataRow row = table.Rows[0];
            return row.IsNull("ChequeCreditLimit") ? (decimal?)null : Convert.ToDecimal(row["ChequeCreditLimit"]);
        }

        static void ApplyLimitSummary(DataSet data, decimal usedLimit, decimal remainingLimit)
        {
            if (data?.Tables == null || !data.Tables.Contains("Erp_BankAccount"))
                return;

            var table = data.Tables["Erp_BankAccount"];
            if (table.Rows.Count == 0)
                return;

            DataRow row = table.Rows[0];
            if (table.Columns.Contains(BankAccountCreditCardHelper.FieldUsedCreditLimit))
            {
                row[BankAccountCreditCardHelper.FieldUsedCreditLimit] =
                    usedLimit == 0m ? DBNull.Value : (object)usedLimit;
            }

            if (table.Columns.Contains(BankAccountCreditCardHelper.FieldRemainingCreditLimit))
            {
                row[BankAccountCreditCardHelper.FieldRemainingCreditLimit] =
                    remainingLimit == 0m ? DBNull.Value : (object)remainingLimit;
            }
        }
    }

    public sealed class CreditCardPeriodMovementLine
    {
        public long RecId { get; set; }
        public DateTime ReceiptDate { get; set; }
        public short InstallmentCount { get; set; }
        public decimal TotalAmount { get; set; }
    }
}
