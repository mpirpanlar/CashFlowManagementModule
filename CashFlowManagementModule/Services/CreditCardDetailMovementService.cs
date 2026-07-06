using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

using CashFlowManagementModule.BoExtensions;

using Sentez.Common.SystemServices;
using Sentez.Common.Utilities.ReceiptTypeDefinition;
using Sentez.Core.ParameterClasses;
using Sentez.Data.Tools;
using Sentez.Localization;

namespace CashFlowManagementModule.Services
{
    /// <summary>
    /// Seçili ekstre dönemine ait kredi kartı hareket detay grid verisini üretir.
    /// </summary>
    public static class CreditCardDetailMovementService
    {
        const string SourceTableAllocation = "Erp_BankAccountCreditCardPeriodAllocation";
        const string SourceTableCurrentAccountItem = "Erp_CurrentAccountReceiptItem";
        const string SourceTableReceiptPaymentItem = "Erp_ReceiptPaymentItem";
        const string SourceTableBankReceiptItem = "Erp_BankReceiptItem";
        const int FinanceSourceModule = 3;

        static readonly string MovementCategorySpending = SLanguage.GetString("Harcama");
        static readonly string MovementCategoryRefund = SLanguage.GetString("İade");
        static readonly string MovementCategoryPayment = SLanguage.GetString("Ödeme");

        public static DataTable BuildPeriodMovementTable(
            LiveSession session,
            long bankAccountId,
            long periodRecId)
        {
            var table = CreateMovementTableSchema();
            if (session?._dbInfo?.Connection == null || bankAccountId <= 0 || periodRecId <= 0)
                return table;

            CashFlowDbContext context = CashFlowDbContext.FromSession(session);
            int companyId = session.ActiveCompany.RecId ?? 0;
            int amountDec = session.ParamService?.GetParameterClass<GeneralParameters>()?.AmountDec ?? 2;

            PeriodMovementContext periodContext = BuildPeriodMovementContext(
                session, context, companyId, bankAccountId, periodRecId, amountDec);
            if (periodContext == null || periodContext.PeriodIndex < 0)
                return table;

            BankAccountHeaderInfo bankInfo = LoadBankAccountHeader(context, companyId, bankAccountId);
            if (bankInfo == null)
                return table;

            HashSet<long> allocatedCriIds = new HashSet<long>();
            AppendAllocationMovements(table, context, periodContext, bankInfo, amountDec, allocatedCriIds);
            AppendReceiptPaymentItemMovements(table, context, companyId, periodContext, bankInfo, amountDec, allocatedCriIds);
            AppendCurrentAccountMovements(table, context, companyId, periodContext, bankInfo, amountDec, allocatedCriIds);
            AppendBankReceiptSpendingMovements(table, context, companyId, periodContext, bankInfo, amountDec);
            AppendVirmanPaymentMovements(table, context, companyId, periodContext, bankInfo, amountDec);

            DataTable sorted = SortMovementTable(table);
            ApplyRunningBalance(sorted, periodContext.Snapshot, amountDec);
            return sorted;
        }

        static PeriodMovementContext BuildPeriodMovementContext(
            LiveSession session,
            CashFlowDbContext context,
            int companyId,
            long bankAccountId,
            long periodRecId,
            int amountDec)
        {
            IList<CreditCardPeriodInfo> periods = CreditCardStatementDataService.LoadActivePeriods(context, bankAccountId);
            if (periods == null || periods.Count == 0)
                return null;

            int periodIndex = -1;
            for (int i = 0; i < periods.Count; i++)
            {
                if (periods[i].RecId == periodRecId)
                {
                    periodIndex = i;
                    break;
                }
            }

            if (periodIndex < 0)
                return null;

            DataTable bankTable = LoadBankAccountTable(context, bankAccountId);
            DataTable periodTable = LoadPeriodTable(context, bankAccountId);
            if (periodTable == null || periodTable.Rows.Count == 0)
                return null;

            var data = new DataSet();
            data.Tables.Add(bankTable);
            data.Tables.Add(periodTable);

            CreditCardPaymentDueDaysSyncService.EnsureVirtualColumns(data);
            BankAccountCreditCardHelper.EnsureBankAccountDataColumns(data);
            CreditCardPeriodPaymentSummaryService.RefreshSummary(session, data, bankAccountId);
            CreditCardPaymentDueDaysSyncService.RecalculateAllPeriodRows(periodTable);

            IList<DataRow> activePeriodRows = GetActivePeriodRows(periodTable);
            DataRow periodRow = periodIndex < activePeriodRows.Count ? activePeriodRows[periodIndex] : null;
            CreditCardPeriodInfo period = periods[periodIndex];

            decimal? creditLimit = GetCreditLimit(bankTable.Rows.Count > 0 ? bankTable.Rows[0] : null);
            decimal totalLimit = GetPeriodTotalCreditLimit(periodRow, creditLimit, amountDec);

            return new PeriodMovementContext
            {
                PeriodIndex = periodIndex,
                PeriodRecId = periodRecId,
                Periods = periods,
                Period = period,
                Snapshot = new PeriodMovementSnapshot
                {
                    StatementDate = period.StatementDate,
                    PaymentDueDate = period.PaymentDueDate,
                    PaymentDueDays = GetPaymentDueDays(period.StatementDate, period.PaymentDueDate),
                    CardExpiryDate = GetCardExpiryDate(periodRow),
                    PeriodTotalCreditLimit = ToNullableAmount(totalLimit, amountDec)
                }
            };
        }

        static void AppendAllocationMovements(
            DataTable table,
            CashFlowDbContext context,
            PeriodMovementContext periodContext,
            BankAccountHeaderInfo bankInfo,
            int amountDec,
            HashSet<long> allocatedCriIds)
        {
            DataTable allocationTable = CashFlowDbAccess.GetDataTable(
                context,
                SourceTableAllocation,
                $@"select a.RecId,
                          a.Amount,
                          a.InstallmentNo,
                          a.InstallmentCount,
                          a.Explanation AllocationExplanation,
                          a.PaymentReferenceDate,
                          cri.RecId CurrentAccountReceiptItemId,
                          cri.ReceiptDate,
                          cri.Explanation ItemExplanation,
                          cr.ReceiptType,
                          cr.ReceiptNo
                   from Erp_BankAccountCreditCardPeriodAllocation a with (nolock)
                   inner join Erp_CurrentAccountReceiptItem cri with (nolock) on cri.RecId = a.CurrentAccountReceiptItemId
                   inner join Erp_CurrentAccountReceipt cr with (nolock) on cr.RecId = cri.CurrentAccountReceiptId
                   where a.CreditCardPeriodId = {periodContext.PeriodRecId}
                     and IsNull(a.IsDeleted, 0) = 0
                     and IsNull(cri.IsDeleted, 0) = 0
                     and IsNull(cr.IsDeleted, 0) = 0
                   order by a.PaymentReferenceDate, a.InstallmentNo, a.RecId");

            if (allocationTable == null)
                return;

            foreach (DataRow row in allocationTable.Rows)
            {
                short receiptType = Convert.ToInt16(row["ReceiptType"]);
                decimal amount = Convert.ToDecimal(row["Amount"]);
                if (amount == 0m)
                    continue;

                long criRecId = Convert.ToInt64(row["CurrentAccountReceiptItemId"]);
                allocatedCriIds.Add(criRecId);

                short installmentCount = row.IsNull("InstallmentCount")
                    ? (short)1
                    : Convert.ToInt16(row["InstallmentCount"]);
                if (installmentCount < 1)
                    installmentCount = 1;

                string explanation = row.IsNull("AllocationExplanation")
                    ? (row.IsNull("ItemExplanation") ? null : Convert.ToString(row["ItemExplanation"]))
                    : Convert.ToString(row["AllocationExplanation"]);

                DateTime receiptDate = row.IsNull("PaymentReferenceDate")
                    ? (row.IsNull("ReceiptDate") ? DateTime.Today : Convert.ToDateTime(row["ReceiptDate"]).Date)
                    : Convert.ToDateTime(row["PaymentReferenceDate"]).Date;

                AddMovementRow(
                    table,
                    periodContext,
                    bankInfo,
                    Convert.ToInt64(row["RecId"]),
                    SourceTableAllocation,
                    GetMovementCategory(receiptType),
                    GetReceiptTypeName(receiptType),
                    receiptDate,
                    explanation,
                    installmentCount,
                    amount,
                    amountDec);
            }
        }

        static void AppendReceiptPaymentItemMovements(
            DataTable table,
            CashFlowDbContext context,
            int companyId,
            PeriodMovementContext periodContext,
            BankAccountHeaderInfo bankInfo,
            int amountDec,
            HashSet<long> allocatedCriIds)
        {
            DataTable rpiTable = CashFlowDbAccess.GetDataTable(
                context,
                SourceTableReceiptPaymentItem,
                $@"select rpi.RecId,
                          rpi.TermDate,
                          rpi.Amount,
                          cri.RecId CurrentAccountReceiptItemId,
                          cri.ReceiptDate,
                          cri.Explanation,
                          IsNull(cri.InstallmentCount, 1) InstallmentCount,
                          cr.ReceiptType,
                          cr.ReceiptNo
                   from Erp_ReceiptPaymentItem rpi with (nolock)
                   inner join Erp_CurrentAccountReceiptItem cri with (nolock) on cri.RecId = rpi.SourceItemId
                   inner join Erp_CurrentAccountReceipt cr with (nolock) on cr.RecId = cri.CurrentAccountReceiptId
                   inner join Erp_BankAccount ba with (nolock) on ba.RecId = cri.BankAccountId
                   inner join Erp_Bank b with (nolock) on b.RecId = ba.BankId
                   where rpi.SourceModule = {FinanceSourceModule}
                     and b.CompanyId = {companyId}
                     and cri.BankAccountId = {bankInfo.BankAccountId}
                     and cr.ReceiptType in (50, 51, 52, 53)
                     and IsNull(rpi.IsDeleted, 0) = 0
                     and IsNull(cri.IsDeleted, 0) = 0
                     and IsNull(cr.IsDeleted, 0) = 0
                     and IsNull(ba.IsDeleted, 0) = 0
                     and IsNull(b.IsDeleted, 0) = 0
                   order by rpi.TermDate, rpi.RecId");

            if (rpiTable == null)
                return;

            foreach (DataRow row in rpiTable.Rows)
            {
                long criRecId = Convert.ToInt64(row["CurrentAccountReceiptItemId"]);
                if (allocatedCriIds.Contains(criRecId))
                    continue;

                if (row.IsNull("TermDate") || row.IsNull("Amount"))
                    continue;

                decimal amount = Convert.ToDecimal(row["Amount"]);
                if (amount == 0m)
                    continue;

                DateTime termDate = Convert.ToDateTime(row["TermDate"]).Date;
                if (ResolvePeriodIndex(periodContext.Periods, termDate) != periodContext.PeriodIndex)
                    continue;

                short receiptType = Convert.ToInt16(row["ReceiptType"]);
                short installmentCount = Convert.ToInt16(row["InstallmentCount"]);
                if (installmentCount < 1)
                    installmentCount = 1;

                DateTime receiptDate = row.IsNull("ReceiptDate")
                    ? termDate
                    : Convert.ToDateTime(row["ReceiptDate"]).Date;

                string explanation = row.IsNull("Explanation") ? null : Convert.ToString(row["Explanation"]);

                AddMovementRow(
                    table,
                    periodContext,
                    bankInfo,
                    Convert.ToInt64(row["RecId"]),
                    SourceTableReceiptPaymentItem,
                    GetMovementCategory(receiptType),
                    GetReceiptTypeName(receiptType),
                    receiptDate,
                    explanation,
                    installmentCount,
                    amount,
                    amountDec);
            }
        }

        static void AppendCurrentAccountMovements(
            DataTable table,
            CashFlowDbContext context,
            int companyId,
            PeriodMovementContext periodContext,
            BankAccountHeaderInfo bankInfo,
            int amountDec,
            HashSet<long> allocatedCriIds)
        {
            DataTable criTable = CashFlowDbAccess.GetDataTable(
                context,
                SourceTableCurrentAccountItem,
                $@"select cri.RecId,
                          cri.ReceiptDate,
                          cri.InstalmentStartDate,
                          cri.TermDate,
                          cri.Explanation,
                          IsNull(cri.InstallmentCount, 1) InstallmentCount,
                          cr.ReceiptDate HeaderReceiptDate,
                          cr.ReceiptType,
                          cr.ReceiptNo,
                          cri.Debit,
                          cri.Credit
                   from Erp_CurrentAccountReceiptItem cri with (nolock)
                   inner join Erp_CurrentAccountReceipt cr with (nolock) on cr.RecId = cri.CurrentAccountReceiptId
                   inner join Erp_BankAccount ba with (nolock) on ba.RecId = cri.BankAccountId
                   inner join Erp_Bank b with (nolock) on b.RecId = ba.BankId
                   where b.CompanyId = {companyId}
                     and cri.BankAccountId = {bankInfo.BankAccountId}
                     and cr.ReceiptType in (50, 51, 52, 53)
                     and IsNull(cri.IsDeleted, 0) = 0
                     and IsNull(cr.IsDeleted, 0) = 0
                     and IsNull(ba.IsDeleted, 0) = 0
                     and IsNull(b.IsDeleted, 0) = 0
                   order by cri.ReceiptDate, cri.RecId");

            if (criTable == null)
                return;

            HashSet<long> criWithRpi = LoadCurrentAccountRecIdsWithReceiptPaymentItems(
                context, companyId, bankInfo.BankAccountId);

            foreach (DataRow row in criTable.Rows)
            {
                long criRecId = Convert.ToInt64(row["RecId"]);
                if (allocatedCriIds.Contains(criRecId) || criWithRpi.Contains(criRecId))
                    continue;

                short receiptType = Convert.ToInt16(row["ReceiptType"]);
                decimal amount = GetItemAmount(
                    receiptType,
                    row.IsNull("Debit") ? 0m : Convert.ToDecimal(row["Debit"]),
                    row.IsNull("Credit") ? 0m : Convert.ToDecimal(row["Credit"]),
                    amountDec);
                if (amount == 0m)
                    continue;

                DateTime? headerReceiptDate = row.IsNull("HeaderReceiptDate")
                    ? (DateTime?)null
                    : Convert.ToDateTime(row["HeaderReceiptDate"]).Date;
                DateTime? movementDate = CurrentAccountReceiptCreditCardHelper.ResolveInstalmentStartDate(row, headerReceiptDate);
                if (!movementDate.HasValue)
                    continue;

                if (!MatchesPeriodByMovementDate(periodContext, movementDate.Value))
                    continue;

                short installmentCount = Convert.ToInt16(row["InstallmentCount"]);
                if (installmentCount < 1)
                    installmentCount = 1;

                DateTime receiptDate = row.IsNull("ReceiptDate")
                    ? movementDate.Value
                    : Convert.ToDateTime(row["ReceiptDate"]).Date;
                string explanation = row.IsNull("Explanation") ? null : Convert.ToString(row["Explanation"]);

                AddMovementRow(
                    table,
                    periodContext,
                    bankInfo,
                    criRecId,
                    SourceTableCurrentAccountItem,
                    GetMovementCategory(receiptType),
                    GetReceiptTypeName(receiptType),
                    receiptDate,
                    explanation,
                    installmentCount,
                    amount,
                    amountDec);
            }
        }

        static void AppendBankReceiptSpendingMovements(
            DataTable table,
            CashFlowDbContext context,
            int companyId,
            PeriodMovementContext periodContext,
            BankAccountHeaderInfo bankInfo,
            int amountDec)
        {
            DataTable briTable = CashFlowDbAccess.GetDataTable(
                context,
                SourceTableBankReceiptItem,
                $@"select bri.RecId,
                          bri.ReceiptDate,
                          bri.Explanation,
                          br.ReceiptType,
                          br.ReceiptNo,
                          IsNull(bri.Credit, 0) TotalAmount
                   from Erp_BankReceiptItem bri with (nolock)
                   inner join Erp_BankReceipt br with (nolock) on br.RecId = bri.BankReceiptId
                   where br.CompanyId = {companyId}
                     and bri.BankAccountId = {bankInfo.BankAccountId}
                     and br.ReceiptType = 1
                     and IsNull(bri.Credit, 0) <> 0
                     and IsNull(bri.IsDeleted, 0) = 0
                     and IsNull(br.IsDeleted, 0) = 0
                     and IsNull(br.IsCancelled, 0) = 0
                   order by bri.ReceiptDate, bri.RecId");

            if (briTable == null)
                return;

            foreach (DataRow row in briTable.Rows)
            {
                decimal amount = Convert.ToDecimal(row["TotalAmount"]);
                if (amount == 0m || row.IsNull("ReceiptDate"))
                    continue;

                DateTime receiptDate = Convert.ToDateTime(row["ReceiptDate"]).Date;
                if (!MatchesPeriodByMovementDate(periodContext, receiptDate))
                    continue;

                short receiptType = Convert.ToInt16(row["ReceiptType"]);
                string explanation = row.IsNull("Explanation") ? null : Convert.ToString(row["Explanation"]);

                AddMovementRow(
                    table,
                    periodContext,
                    bankInfo,
                    Convert.ToInt64(row["RecId"]),
                    SourceTableBankReceiptItem,
                    MovementCategorySpending,
                    GetBankReceiptTypeName(receiptType),
                    receiptDate,
                    explanation,
                    1,
                    amount,
                    amountDec);
            }
        }

        static void AppendVirmanPaymentMovements(
            DataTable table,
            CashFlowDbContext context,
            int companyId,
            PeriodMovementContext periodContext,
            BankAccountHeaderInfo bankInfo,
            int amountDec)
        {
            DataTable briTable = CashFlowDbAccess.GetDataTable(
                context,
                SourceTableBankReceiptItem,
                $@"select bri.RecId,
                          bri.ReceiptDate,
                          bri.Explanation,
                          br.ReceiptType,
                          br.ReceiptNo,
                          case when IsNull(bri.Debit, 0) <> 0 then IsNull(bri.Debit, 0)
                               when IsNull(bri.Credit, 0) <> 0 then IsNull(bri.Credit, 0)
                               else 0 end TotalAmount
                   from Erp_BankReceiptItem bri with (nolock)
                   inner join Erp_BankReceipt br with (nolock) on br.RecId = bri.BankReceiptId
                   where br.CompanyId = {companyId}
                     and bri.BankAccountId = {bankInfo.BankAccountId}
                     and br.ReceiptType = 2
                     and IsNull(bri.IsDeleted, 0) = 0
                     and IsNull(br.IsDeleted, 0) = 0
                     and IsNull(br.IsCancelled, 0) = 0
                   order by bri.ReceiptDate, bri.RecId");

            if (briTable == null)
                return;

            foreach (DataRow row in briTable.Rows)
            {
                decimal amount = Convert.ToDecimal(row["TotalAmount"]);
                if (amount == 0m || row.IsNull("ReceiptDate"))
                    continue;

                DateTime receiptDate = Convert.ToDateTime(row["ReceiptDate"]).Date;
                if (!MatchesPeriodByMovementDate(periodContext, receiptDate))
                    continue;

                short receiptType = Convert.ToInt16(row["ReceiptType"]);
                string explanation = row.IsNull("Explanation") ? null : Convert.ToString(row["Explanation"]);

                AddMovementRow(
                    table,
                    periodContext,
                    bankInfo,
                    Convert.ToInt64(row["RecId"]),
                    SourceTableBankReceiptItem,
                    MovementCategoryPayment,
                    GetBankReceiptTypeName(receiptType),
                    receiptDate,
                    explanation,
                    0,
                    amount,
                    amountDec);
            }
        }

        static void AddMovementRow(
            DataTable table,
            PeriodMovementContext periodContext,
            BankAccountHeaderInfo bankInfo,
            long sourceRecId,
            string sourceTable,
            string movementCategory,
            string receiptTypeName,
            DateTime receiptDate,
            string explanation,
            short installmentCount,
            decimal amount,
            int amountDec)
        {
            PeriodMovementSnapshot snapshot = periodContext.Snapshot;
            var row = table.NewRow();
            row["RecId"] = sourceRecId;
            row["SourceTable"] = sourceTable ?? string.Empty;
            row["SourceRecId"] = sourceRecId;
            row["BankAccountId"] = bankInfo.BankAccountId;
            row["BankCode"] = bankInfo.BankCode ?? string.Empty;
            row["BankName"] = bankInfo.BankName ?? string.Empty;
            row["AccountCode"] = bankInfo.AccountCode ?? string.Empty;
            row["AccountName"] = bankInfo.AccountName ?? string.Empty;
            row["MovementCategory"] = movementCategory ?? string.Empty;
            row["ReceiptTypeName"] = receiptTypeName ?? string.Empty;
            row["ReceiptDate"] = receiptDate.Date;
            row["Explanation"] = string.IsNullOrWhiteSpace(explanation) ? DBNull.Value : (object)explanation;
            row["InstallmentCount"] = installmentCount;
            row["Amount"] = RoundAmount(amount, amountDec);
            row["StatementDate"] = snapshot.StatementDate.Date;
            row[BankAccountCreditCardHelper.FieldPaymentDueDays] = snapshot.PaymentDueDays.HasValue
                ? (object)snapshot.PaymentDueDays.Value
                : DBNull.Value;
            row["PaymentDueDate"] = snapshot.PaymentDueDate.Date;
            row[BankAccountCreditCardHelper.FieldPeriodTotalCreditLimit] =
                snapshot.PeriodTotalCreditLimit ?? (object)DBNull.Value;
            row["CardExpiryDate"] = snapshot.CardExpiryDate.HasValue
                ? (object)snapshot.CardExpiryDate.Value.Date
                : DBNull.Value;
            table.Rows.Add(row);
        }

        static void ApplyRunningBalance(DataTable table, PeriodMovementSnapshot snapshot, int amountDec)
        {
            if (table == null || table.Rows.Count == 0)
                return;

            decimal totalLimit = snapshot.PeriodTotalCreditLimit ?? 0m;
            decimal runningSpending = 0m;
            decimal runningRefund = 0m;
            decimal runningPayment = 0m;

            foreach (DataRow row in table.Rows.Cast<DataRow>()
                         .OrderBy(r => r.IsNull("ReceiptDate") ? DateTime.MinValue : Convert.ToDateTime(r["ReceiptDate"]))
                         .ThenBy(r => r.IsNull("SourceTable") ? string.Empty : Convert.ToString(r["SourceTable"]))
                         .ThenBy(r => r.IsNull("SourceRecId") ? 0L : Convert.ToInt64(r["SourceRecId"])))
            {
                string category = row.IsNull("MovementCategory") ? string.Empty : Convert.ToString(row["MovementCategory"]);
                decimal amount = row.IsNull("Amount") ? 0m : Convert.ToDecimal(row["Amount"]);

                row[BankAccountCreditCardHelper.FieldPeriodSpendingTotal] = DBNull.Value;
                row[BankAccountCreditCardHelper.FieldPeriodRefundTotal] = DBNull.Value;
                row[BankAccountCreditCardHelper.FieldPeriodCardPaymentTotal] = DBNull.Value;

                if (category == MovementCategorySpending)
                {
                    row[BankAccountCreditCardHelper.FieldPeriodSpendingTotal] = amount == 0m ? DBNull.Value : (object)amount;
                    runningSpending += amount;
                }
                else if (category == MovementCategoryRefund)
                {
                    row[BankAccountCreditCardHelper.FieldPeriodRefundTotal] = amount == 0m ? DBNull.Value : (object)amount;
                    runningRefund += amount;
                    runningSpending = 0m;
                }
                else if (category == MovementCategoryPayment)
                {
                    row[BankAccountCreditCardHelper.FieldPeriodCardPaymentTotal] = amount == 0m ? DBNull.Value : (object)amount;
                    runningPayment += amount;
                    runningSpending = 0m;
                }

                decimal remainingLimit = CreditCardPeriodPaymentSummaryService.CalculatePeriodRemainingLimit(
                    totalLimit, runningSpending, runningRefund, runningPayment, amountDec);
                row[BankAccountCreditCardHelper.FieldPeriodRemainingCreditLimit] =
                    remainingLimit == 0m ? DBNull.Value : (object)remainingLimit;
            }
        }

        static bool MatchesPeriodByMovementDate(PeriodMovementContext periodContext, DateTime movementDate)
        {
            int periodIndex = CreditCardStatementDataService.FindPeriodIndexByStatementCycle(
                periodContext.Periods, movementDate.Date);
            if (periodIndex == periodContext.PeriodIndex)
                return true;

            return ResolvePeriodIndex(periodContext.Periods, movementDate.Date) == periodContext.PeriodIndex;
        }

        static int ResolvePeriodIndex(IList<CreditCardPeriodInfo> periods, DateTime termDate)
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

        static HashSet<long> LoadCurrentAccountRecIdsWithReceiptPaymentItems(
            CashFlowDbContext context,
            int companyId,
            long bankAccountId)
        {
            var recIds = new HashSet<long>();
            DataTable table = CashFlowDbAccess.GetDataTable(
                context,
                SourceTableReceiptPaymentItem,
                $@"select distinct cri.RecId
                   from Erp_ReceiptPaymentItem rpi with (nolock)
                   inner join Erp_CurrentAccountReceiptItem cri with (nolock) on cri.RecId = rpi.SourceItemId
                   inner join Erp_CurrentAccountReceipt cr with (nolock) on cr.RecId = cri.CurrentAccountReceiptId
                   inner join Erp_BankAccount ba with (nolock) on ba.RecId = cri.BankAccountId
                   inner join Erp_Bank b with (nolock) on b.RecId = ba.BankId
                   where rpi.SourceModule = {FinanceSourceModule}
                     and b.CompanyId = {companyId}
                     and cri.BankAccountId = {bankAccountId}
                     and cr.ReceiptType in (50, 51, 52, 53)
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

        static BankAccountHeaderInfo LoadBankAccountHeader(CashFlowDbContext context, int companyId, long bankAccountId)
        {
            DataTable table = CashFlowDbAccess.GetDataTable(
                context,
                "Erp_BankAccount",
                $@"select ba.RecId,
                          ba.AccountCode,
                          ba.AccountName,
                          b.BankCode,
                          b.BankName
                   from Erp_BankAccount ba with (nolock)
                   inner join Erp_Bank b with (nolock) on b.RecId = ba.BankId
                   where ba.RecId = {bankAccountId}
                     and b.CompanyId = {companyId}
                     and IsNull(ba.IsDeleted, 0) = 0
                     and IsNull(b.IsDeleted, 0) = 0");

            if (table == null || table.Rows.Count == 0)
                return null;

            DataRow row = table.Rows[0];
            return new BankAccountHeaderInfo
            {
                BankAccountId = Convert.ToInt64(row["RecId"]),
                BankCode = row.IsNull("BankCode") ? null : Convert.ToString(row["BankCode"]),
                BankName = row.IsNull("BankName") ? null : Convert.ToString(row["BankName"]),
                AccountCode = row.IsNull("AccountCode") ? null : Convert.ToString(row["AccountCode"]),
                AccountName = row.IsNull("AccountName") ? null : Convert.ToString(row["AccountName"])
            };
        }

        static DataTable CreateMovementTableSchema()
        {
            var table = new DataTable("CreditCardDetailMovement");
            table.Columns.Add("RecId", typeof(long));
            table.Columns.Add("SourceTable", typeof(string));
            table.Columns.Add("SourceRecId", typeof(long));
            table.Columns.Add("BankAccountId", typeof(long));
            table.Columns.Add("BankCode", typeof(string));
            table.Columns.Add("BankName", typeof(string));
            table.Columns.Add("AccountCode", typeof(string));
            table.Columns.Add("AccountName", typeof(string));
            table.Columns.Add("MovementCategory", typeof(string));
            table.Columns.Add("ReceiptTypeName", typeof(string));
            table.Columns.Add("ReceiptDate", typeof(DateTime));
            table.Columns.Add("Explanation", typeof(string));
            table.Columns.Add("InstallmentCount", typeof(short));
            table.Columns.Add("Amount", typeof(decimal));
            table.Columns.Add("StatementDate", typeof(DateTime));
            table.Columns.Add(BankAccountCreditCardHelper.FieldPaymentDueDays, typeof(short));
            table.Columns.Add("PaymentDueDate", typeof(DateTime));
            table.Columns.Add(BankAccountCreditCardHelper.FieldPeriodTotalCreditLimit, typeof(decimal));
            table.Columns.Add(BankAccountCreditCardHelper.FieldPeriodSpendingTotal, typeof(decimal));
            table.Columns.Add(BankAccountCreditCardHelper.FieldPeriodRefundTotal, typeof(decimal));
            table.Columns.Add(BankAccountCreditCardHelper.FieldPeriodCardPaymentTotal, typeof(decimal));
            table.Columns.Add(BankAccountCreditCardHelper.FieldPeriodRemainingCreditLimit, typeof(decimal));
            table.Columns.Add("CardExpiryDate", typeof(DateTime));
            return table;
        }

        static DataTable SortMovementTable(DataTable table)
        {
            if (table == null || table.Rows.Count == 0)
                return table;

            var sorted = table.AsEnumerable()
                .OrderBy(r => r.IsNull("ReceiptDate") ? DateTime.MinValue : Convert.ToDateTime(r["ReceiptDate"]))
                .ThenBy(r => r.IsNull("SourceTable") ? string.Empty : Convert.ToString(r["SourceTable"]))
                .ThenBy(r => r.IsNull("SourceRecId") ? 0L : Convert.ToInt64(r["SourceRecId"]))
                .CopyToDataTable();

            sorted.TableName = table.TableName;
            return sorted;
        }

        static string GetMovementCategory(short receiptType)
        {
            if (receiptType == 51 || receiptType == 52)
                return MovementCategorySpending;

            if (receiptType == 53 || receiptType == 50)
                return MovementCategoryRefund;

            return GetReceiptTypeName(receiptType);
        }

        static string GetReceiptTypeName(short receiptType)
        {
            ReceiptTypeDefinition definition = CurrentAccountReceiptType.GetCurrentAccountReceiptType(receiptType);
            return definition?.TypeName ?? receiptType.ToString();
        }

        static string GetBankReceiptTypeName(short receiptType)
        {
            ReceiptTypeDefinition definition = BankReceiptType.GetBankReceiptType(receiptType);
            return definition?.TypeName ?? receiptType.ToString();
        }

        static decimal GetItemAmount(short receiptType, decimal debit, decimal credit, int amountDec)
        {
            bool preferDebit = receiptType == 51 || receiptType == 52;
            decimal amount = preferDebit
                ? (debit != 0m ? debit : credit)
                : (credit != 0m ? credit : debit);
            return RoundAmount(amount, amountDec);
        }

        static DataTable LoadBankAccountTable(CashFlowDbContext context, long bankAccountId)
        {
            DataTable table = CashFlowDbAccess.GetDataTable(
                context,
                "Erp_BankAccount",
                $@"select RecId,
                          ChequeCreditLimit,
                          ForCreditCard,
                          {BankAccountCreditCardHelper.FieldIssueDate},
                          {BankAccountCreditCardHelper.FieldExpiryMonth},
                          {BankAccountCreditCardHelper.FieldExpiryYear}
                   from Erp_BankAccount with (nolock)
                   where RecId = {bankAccountId} and IsNull(IsDeleted, 0) = 0");
            if (table != null)
                table.TableName = "Erp_BankAccount";
            return table ?? new DataTable("Erp_BankAccount");
        }

        static DataRow LoadBankAccountRow(CashFlowDbContext context, long bankAccountId)
        {
            DataTable table = LoadBankAccountTable(context, bankAccountId);
            return table.Rows.Count == 0 ? null : table.Rows[0];
        }

        static DataTable LoadPeriodTable(CashFlowDbContext context, long bankAccountId)
        {
            DataTable table = CashFlowDbAccess.GetDataTable(
                context,
                BankAccountCreditCardHelper.PeriodTableName,
                $@"select RecId, CompanyId, BankAccountId, PeriodNo, PeriodYear, PeriodMonth,
                          StatementStartDate, StatementDate, PaymentDueDate, CardExpiryDate, IsDeleted
                   from Erp_BankAccountCreditCardPeriod with (nolock)
                   where BankAccountId = {bankAccountId} and IsNull(IsDeleted, 0) = 0
                   order by PaymentDueDate, PeriodNo");
            if (table != null)
                table.TableName = BankAccountCreditCardHelper.PeriodTableName;
            return table ?? new DataTable(BankAccountCreditCardHelper.PeriodTableName);
        }

        static IList<DataRow> GetActivePeriodRows(DataTable periodTable)
        {
            if (periodTable == null)
                return new List<DataRow>();

            return periodTable.Rows.Cast<DataRow>()
                .Where(r => r.RowState != DataRowState.Deleted &&
                            (r.IsNull("IsDeleted") || !Convert.ToBoolean(r["IsDeleted"])))
                .OrderBy(r => r.IsNull("PaymentDueDate") ? DateTime.MaxValue : Convert.ToDateTime(r["PaymentDueDate"]))
                .ThenBy(r => r.IsNull("PeriodNo") ? short.MaxValue : Convert.ToInt16(r["PeriodNo"]))
                .ToList();
        }

        static decimal? GetCreditLimit(DataRow bankAccountRow)
        {
            if (bankAccountRow == null || bankAccountRow.IsNull("ChequeCreditLimit"))
                return null;

            return Convert.ToDecimal(bankAccountRow["ChequeCreditLimit"]);
        }

        static decimal GetPeriodTotalCreditLimit(DataRow periodRow, decimal? creditLimit, int amountDec)
        {
            if (periodRow != null
                && periodRow.Table.Columns.Contains(BankAccountCreditCardHelper.FieldPeriodTotalCreditLimit)
                && !periodRow.IsNull(BankAccountCreditCardHelper.FieldPeriodTotalCreditLimit))
            {
                return Convert.ToDecimal(periodRow[BankAccountCreditCardHelper.FieldPeriodTotalCreditLimit]);
            }

            if (!creditLimit.HasValue || creditLimit.Value <= 0m)
                return 0m;

            return RoundAmount(creditLimit.Value, amountDec);
        }

        static short? GetPaymentDueDays(DateTime statementDate, DateTime paymentDueDate)
        {
            if (statementDate == DateTime.MinValue || paymentDueDate == DateTime.MinValue)
                return null;

            int days = (paymentDueDate.Date - statementDate.Date).Days;
            return days < 0 ? (short?)null : (short)days;
        }

        static DateTime? GetCardExpiryDate(DataRow periodRow)
        {
            if (periodRow == null || periodRow.IsNull("CardExpiryDate"))
                return null;

            return Convert.ToDateTime(periodRow["CardExpiryDate"]).Date;
        }

        static decimal? ToNullableAmount(decimal value, int amountDec)
        {
            decimal amount = RoundAmount(value, amountDec);
            return amount == 0m ? (decimal?)null : amount;
        }

        static decimal RoundAmount(decimal value, int amountDec)
        {
            return Math.Round(value, amountDec, MidpointRounding.AwayFromZero);
        }

        sealed class PeriodMovementContext
        {
            public int PeriodIndex { get; set; }
            public long PeriodRecId { get; set; }
            public IList<CreditCardPeriodInfo> Periods { get; set; }
            public CreditCardPeriodInfo Period { get; set; }
            public PeriodMovementSnapshot Snapshot { get; set; }
        }

        sealed class PeriodMovementSnapshot
        {
            public DateTime StatementDate { get; set; }
            public DateTime PaymentDueDate { get; set; }
            public short? PaymentDueDays { get; set; }
            public DateTime? CardExpiryDate { get; set; }
            public decimal? PeriodTotalCreditLimit { get; set; }
        }

        sealed class BankAccountHeaderInfo
        {
            public long BankAccountId { get; set; }
            public string BankCode { get; set; }
            public string BankName { get; set; }
            public string AccountCode { get; set; }
            public string AccountName { get; set; }
        }
    }
}
