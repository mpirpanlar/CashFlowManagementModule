using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

using CashFlowManagementModule.BoExtensions;

using Sentez.Common.SystemServices;
using Sentez.Core.ParameterClasses;
using Sentez.Data.Tools;

namespace CashFlowManagementModule.Services
{
    /// <summary>
    /// Kredi kartı detay analizi ekranı için Erp_BankAccountCreditCardPeriod tabanlı grid verisini üretir.
    /// Bakiye hesapları Banka Hesap Kartı Kredi Kartı Detay sekmesiyle aynı mantıkta hesaplanır.
    /// </summary>
    public static class CreditCardDetailAnalysisService
    {
        /// <summary>
        /// Aktif şirkete ait tüm kredi kartı hesaplarının ekstre dönemlerini analiz tablosunda döndürür.
        /// </summary>
        /// <param name="session">Aktif oturum.</param>
        /// <returns>Banka ve dönem kolonlarını içeren DataTable.</returns>
        public static DataTable BuildAnalysisTable(LiveSession session)
        {
            var table = CreateAnalysisTableSchema();
            if (session?._dbInfo?.Connection == null)
                return table;

            CashFlowDbContext context = CashFlowDbContext.FromSession(session);
            int companyId = session.ActiveCompany.RecId ?? 0;
            int amountDec = session.ParamService?.GetParameterClass<GeneralParameters>()?.AmountDec ?? 2;

            IList<BankAccountInfo> bankAccounts = LoadCreditCardBankAccounts(context, companyId);
            if (bankAccounts.Count == 0)
                return table;

            foreach (BankAccountInfo bankAccount in bankAccounts)
                AppendBankAccountPeriods(table, session, context, bankAccount, amountDec);

            return SortAnalysisTable(table);
        }

        static void AppendBankAccountPeriods(
            DataTable table,
            LiveSession session,
            CashFlowDbContext context,
            BankAccountInfo bankAccount,
            int amountDec)
        {
            DataTable bankTable = LoadBankAccountTable(context, bankAccount.RecId);
            DataTable periodTable = LoadPeriodTable(context, bankAccount.RecId);
            if (periodTable == null || periodTable.Rows.Count == 0)
                return;

            var data = new DataSet();
            data.Tables.Add(bankTable);
            data.Tables.Add(periodTable);

            CreditCardPaymentDueDaysSyncService.EnsureVirtualColumns(data);
            BankAccountCreditCardHelper.EnsureBankAccountDataColumns(data);
            CreditCardPeriodPaymentSummaryService.RefreshSummary(session, data, bankAccount.RecId);
            CreditCardPaymentDueDaysSyncService.RecalculateAllPeriodRows(periodTable);

            foreach (DataRow periodRow in GetActivePeriodRows(periodTable))
                AppendPeriodAnalysisRow(table, periodRow, bankAccount, amountDec);
        }

        static void AppendPeriodAnalysisRow(
            DataTable table,
            DataRow periodRow,
            BankAccountInfo bankAccount,
            int amountDec)
        {
            var row = table.NewRow();
            row["RecId"] = periodRow.IsNull("RecId") ? 0L : Convert.ToInt64(periodRow["RecId"]);
            row["BankAccountId"] = bankAccount.RecId;
            row["BankCode"] = bankAccount.BankCode ?? string.Empty;
            row["BankName"] = bankAccount.BankName ?? string.Empty;
            row["AccountCode"] = bankAccount.AccountCode ?? string.Empty;
            row["AccountName"] = bankAccount.AccountName ?? string.Empty;
            row["PeriodNo"] = GetRowValue(periodRow, "PeriodNo");
            row["PeriodYear"] = GetRowValue(periodRow, "PeriodYear");
            row["PeriodMonth"] = GetRowValue(periodRow, "PeriodMonth");
            row["StatementStartDate"] = ToDateCellValue(GetRowDateOrNull(periodRow, "StatementStartDate"));
            row["StatementDate"] = ToDateCellValue(GetRowDateOrNull(periodRow, "StatementDate"));
            row[BankAccountCreditCardHelper.FieldPaymentDueDays] = GetRowValue(periodRow, BankAccountCreditCardHelper.FieldPaymentDueDays);
            row["PaymentDueDate"] = ToDateCellValue(GetRowDateOrNull(periodRow, "PaymentDueDate"));
            row[BankAccountCreditCardHelper.FieldPeriodTotalCreditLimit] =
                GetRowDecimalCellValue(periodRow, BankAccountCreditCardHelper.FieldPeriodTotalCreditLimit, amountDec);
            row[BankAccountCreditCardHelper.FieldPeriodSpendingTotal] =
                GetRowDecimalCellValue(periodRow, BankAccountCreditCardHelper.FieldPeriodSpendingTotal, amountDec);
            row[BankAccountCreditCardHelper.FieldPeriodRefundTotal] =
                GetRowDecimalCellValue(periodRow, BankAccountCreditCardHelper.FieldPeriodRefundTotal, amountDec);
            row[BankAccountCreditCardHelper.FieldPeriodCardPaymentTotal] =
                GetRowDecimalCellValue(periodRow, BankAccountCreditCardHelper.FieldPeriodCardPaymentTotal, amountDec);
            row[BankAccountCreditCardHelper.FieldPeriodRemainingCreditLimit] =
                GetRowDecimalCellValue(periodRow, BankAccountCreditCardHelper.FieldPeriodRemainingCreditLimit, amountDec);
            row["CardExpiryDate"] = ToDateCellValue(GetRowDateOrNull(periodRow, "CardExpiryDate"));
            table.Rows.Add(row);
        }

        static DataTable CreateAnalysisTableSchema()
        {
            var table = new DataTable("CreditCardDetailAnalysis");
            table.Columns.Add("RecId", typeof(long));
            table.Columns.Add("BankAccountId", typeof(long));
            table.Columns.Add("BankCode", typeof(string));
            table.Columns.Add("BankName", typeof(string));
            table.Columns.Add("AccountCode", typeof(string));
            table.Columns.Add("AccountName", typeof(string));
            table.Columns.Add("PeriodNo", typeof(short));
            table.Columns.Add("PeriodYear", typeof(short));
            table.Columns.Add("PeriodMonth", typeof(short));
            table.Columns.Add("StatementStartDate", typeof(DateTime));
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

        static DataTable SortAnalysisTable(DataTable table)
        {
            if (table == null || table.Rows.Count == 0)
                return table;

            var sorted = table.AsEnumerable()
                .OrderBy(r => r.IsNull("BankCode") ? string.Empty : Convert.ToString(r["BankCode"]))
                .ThenBy(r => r.IsNull("AccountCode") ? string.Empty : Convert.ToString(r["AccountCode"]))
                .ThenBy(r => r.IsNull("PaymentDueDate") ? DateTime.MinValue : Convert.ToDateTime(r["PaymentDueDate"]))
                .ThenBy(r => r.IsNull("PeriodNo") ? short.MaxValue : Convert.ToInt16(r["PeriodNo"]))
                .CopyToDataTable();

            sorted.TableName = table.TableName;
            return sorted;
        }

        static IList<BankAccountInfo> LoadCreditCardBankAccounts(CashFlowDbContext context, int companyId)
        {
            var accounts = new List<BankAccountInfo>();
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
                   where b.CompanyId = {companyId}
                     and IsNull(ba.ForCreditCard, 0) = 1
                     and IsNull(ba.IsDeleted, 0) = 0
                     and IsNull(b.IsDeleted, 0) = 0
                   order by b.BankCode, ba.AccountCode");

            if (table == null)
                return accounts;

            foreach (DataRow row in table.Rows)
            {
                accounts.Add(new BankAccountInfo
                {
                    RecId = Convert.ToInt64(row["RecId"]),
                    BankCode = row.IsNull("BankCode") ? null : Convert.ToString(row["BankCode"]),
                    BankName = row.IsNull("BankName") ? null : Convert.ToString(row["BankName"]),
                    AccountCode = row.IsNull("AccountCode") ? null : Convert.ToString(row["AccountCode"]),
                    AccountName = row.IsNull("AccountName") ? null : Convert.ToString(row["AccountName"])
                });
            }

            return accounts;
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
                            (r.IsNull("IsDeleted") || !Convert.ToBoolean(r["IsDeleted"])) &&
                            !r.IsNull("PaymentDueDate"))
                .OrderBy(r => Convert.ToDateTime(r["PaymentDueDate"]))
                .ThenBy(r => r.IsNull("PeriodNo") ? short.MaxValue : Convert.ToInt16(r["PeriodNo"]))
                .ToList();
        }

        static object GetRowValue(DataRow row, string columnName)
        {
            if (row == null || !row.Table.Columns.Contains(columnName) || row.IsNull(columnName))
                return DBNull.Value;

            return row[columnName];
        }

        static DateTime? GetRowDateOrNull(DataRow row, string columnName)
        {
            if (row == null || !row.Table.Columns.Contains(columnName) || row.IsNull(columnName))
                return null;

            return Convert.ToDateTime(row[columnName]).Date;
        }

        static object ToDateCellValue(DateTime? value)
        {
            return value.HasValue ? (object)value.Value.Date : DBNull.Value;
        }

        static object GetRowDecimalCellValue(DataRow row, string columnName, int amountDec)
        {
            if (row == null || !row.Table.Columns.Contains(columnName) || row.IsNull(columnName))
                return DBNull.Value;

            return RoundAmount(Convert.ToDecimal(row[columnName]), amountDec);
        }

        static decimal RoundAmount(decimal value, int amountDec)
        {
            return Math.Round(value, amountDec, MidpointRounding.AwayFromZero);
        }

        sealed class BankAccountInfo
        {
            public long RecId { get; set; }
            public string BankCode { get; set; }
            public string BankName { get; set; }
            public string AccountCode { get; set; }
            public string AccountName { get; set; }
        }
    }
}
