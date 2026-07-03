using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Linq;

using Sentez.Common.SystemServices;

using Sentez.Data.Tools;
using Sentez.Localization;

namespace CashFlowManagementModule.Services
{
    public static class CreditCardDetailAnalysisService
    {
        static readonly string[] MonthCaptions =
        {
            "OCAK", "ŞUBAT", "MART", "NİSAN", "MAYIS", "HAZİRAN",
            "TEMMUZ", "AĞUSTOS", "EYLÜL", "EKİM", "KASIM", "ARALIK"
        };

        public static DataTable BuildAnalysisTable(LiveSession session, short year, long? bankAccountId = null)
        {
            var table = CreateAnalysisTableSchema();
            if (session?._dbInfo?.Connection == null) return table;

            CashFlowDbContext context = CashFlowDbContext.FromSession(session);
            int companyId = session.ActiveCompany.RecId ?? 0;
            IList<long> bankAccountIds = bankAccountId.HasValue && bankAccountId.Value > 0
                ? new List<long> { bankAccountId.Value }
                : CreditCardStatementDataService.LoadCreditCardBankAccountIds(context, companyId);

            var allocationTotals = LoadAllocationTotals(context, companyId, year, bankAccountId);
            decimal[] grandMonthTotals = new decimal[12];
            decimal grandTotal = 0m;
            int cardIndex = 0;

            foreach (long accountId in bankAccountIds)
            {
                int colorGroup = cardIndex % CardColorGroupCount;
                cardIndex++;

                string displayName = CreditCardStatementDataService.GetBankAccountDisplayName(context, accountId);
                IList<CreditCardPeriodInfo> periods = CreditCardStatementDataService
                    .LoadActivePeriods(context, accountId)
                    .Where(p => p.PeriodYear == year)
                    .OrderBy(p => p.PeriodMonth)
                    .ToList();

                AddDateRow(table, accountId, displayName, colorGroup, SLanguage.GetString("Hs.Kesim"), periods, p => p.StatementDate);
                AddDateRow(table, accountId, displayName, colorGroup, SLanguage.GetString("Son Ödeme"), periods, p => p.PaymentDueDate);

                var amountRow = table.NewRow();
                amountRow["BankAccountId"] = accountId;
                amountRow["CardName"] = displayName;
                amountRow["RowType"] = SLanguage.GetString("Tutar");
                amountRow["ColorGroup"] = colorGroup;
                decimal cardTotal = 0m;

                for (int month = 1; month <= 12; month++)
                {
                    string columnName = GetMonthColumnName(month);
                    decimal monthAmount = allocationTotals
                        .Where(x => x.BankAccountId == accountId && x.PeriodMonth == month)
                        .Sum(x => x.Amount);
                    amountRow[columnName] = monthAmount == 0m ? DBNull.Value : (object)monthAmount;
                    cardTotal += monthAmount;
                    grandMonthTotals[month - 1] += monthAmount;
                }

                amountRow["TotalAmount"] = cardTotal == 0m ? DBNull.Value : (object)cardTotal;
                table.Rows.Add(amountRow);
                grandTotal += cardTotal;
            }

            AddSummaryRow(table, SLanguage.GetString("GENEL TOPLAM"), grandMonthTotals, grandTotal);
            return table;
        }

        public static IList<string> GetMonthColumnNames()
        {
            return Enumerable.Range(1, 12).Select(GetMonthColumnName).ToList();
        }

        public static string GetMonthCaption(int month)
        {
            return month >= 1 && month <= 12 ? MonthCaptions[month - 1] : month.ToString(CultureInfo.InvariantCulture);
        }

        public const int CardColorGroupCount = 5;

        static DataTable CreateAnalysisTableSchema()
        {
            var table = new DataTable("CreditCardDetailAnalysis");
            table.Columns.Add("BankAccountId", typeof(long));
            table.Columns.Add("CardName", typeof(string));
            table.Columns.Add("RowType", typeof(string));
            table.Columns.Add("ColorGroup", typeof(int));
            for (int month = 1; month <= 12; month++)
                table.Columns.Add(GetMonthColumnName(month), typeof(object));
            table.Columns.Add("TotalAmount", typeof(object));
            return table;
        }

        static string GetMonthColumnName(int month)
        {
            return $"M{month:00}";
        }

        static void AddDateRow(
            DataTable table,
            long bankAccountId,
            string displayName,
            int colorGroup,
            string rowType,
            IList<CreditCardPeriodInfo> periods,
            Func<CreditCardPeriodInfo, DateTime> dateSelector)
        {
            var row = table.NewRow();
            row["BankAccountId"] = bankAccountId;
            row["CardName"] = displayName;
            row["RowType"] = rowType;
            row["ColorGroup"] = colorGroup;

            for (int month = 1; month <= 12; month++)
            {
                CreditCardPeriodInfo period = periods.FirstOrDefault(p => p.PeriodMonth == month);
                row[GetMonthColumnName(month)] = period == null
                    ? DBNull.Value
                    : (object)CreditCardStatementDataService.FormatDate(dateSelector(period));
            }

            row["TotalAmount"] = DBNull.Value;
            table.Rows.Add(row);
        }

        static void AddSummaryRow(DataTable table, string label, decimal[] monthTotals, decimal grandTotal)
        {
            var row = table.NewRow();
            row["BankAccountId"] = 0L;
            row["CardName"] = label;
            row["RowType"] = string.Empty;
            row["ColorGroup"] = -1;

            for (int month = 1; month <= 12; month++)
            {
                decimal value = monthTotals[month - 1];
                row[GetMonthColumnName(month)] = value == 0m ? DBNull.Value : (object)value;
            }

            row["TotalAmount"] = grandTotal == 0m ? DBNull.Value : (object)grandTotal;
            table.Rows.Add(row);
        }

        static IList<AllocationMonthTotal> LoadAllocationTotals(
            CashFlowDbContext context,
            int companyId,
            short year,
            long? bankAccountId)
        {
            var totals = new List<AllocationMonthTotal>();
            string accountFilter = bankAccountId.HasValue && bankAccountId.Value > 0
                ? $" and a.BankAccountId={bankAccountId.Value}"
                : string.Empty;

            DataTable table = CashFlowDbAccess.GetDataTable(
                context,
                "Erp_BankAccountCreditCardPeriodAllocation",
                $@"select a.BankAccountId, p.PeriodMonth, sum(a.Amount) Amount
                   from Erp_BankAccountCreditCardPeriodAllocation a with (nolock)
                   inner join Erp_BankAccountCreditCardPeriod p with (nolock) on p.RecId=a.CreditCardPeriodId
                   where a.CompanyId={companyId} and p.PeriodYear={year} and IsNull(a.IsDeleted,0)=0 and IsNull(p.IsDeleted,0)=0
                   {accountFilter}
                   group by a.BankAccountId, p.PeriodMonth");
            if (table == null) return totals;

            foreach (DataRow row in table.Rows)
            {
                totals.Add(new AllocationMonthTotal
                {
                    BankAccountId = Convert.ToInt64(row["BankAccountId"]),
                    PeriodMonth = Convert.ToInt16(row["PeriodMonth"]),
                    Amount = Convert.ToDecimal(row["Amount"])
                });
            }

            return totals;
        }

        sealed class AllocationMonthTotal
        {
            public long BankAccountId { get; set; }
            public short PeriodMonth { get; set; }
            public decimal Amount { get; set; }
        }
    }
}
