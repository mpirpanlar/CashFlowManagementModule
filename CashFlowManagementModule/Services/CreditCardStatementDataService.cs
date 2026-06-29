using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Linq;

using Sentez.Data.Tools;

namespace CashFlowManagementModule.Services
{
    public static class CreditCardStatementDataService
    {
        public const int StatementCutNearDaysThreshold = 3;

        public static bool IsCreditCardBankAccount(DbConnection connection, DbTransaction transaction, long bankAccountId)
        {
            if (bankAccountId <= 0) return false;

            object value = UtilityFunctions.SqlCustomScalarQuery(
                connection,
                transaction,
                $"select IsNull(ForCreditCard,0) from Erp_BankAccount with (nolock) where RecId={bankAccountId}");
            return value != null && Convert.ToBoolean(value);
        }

        public static bool HasActivePeriods(DbConnection connection, DbTransaction transaction, long bankAccountId)
        {
            if (bankAccountId <= 0) return false;

            object value = UtilityFunctions.SqlCustomScalarQuery(
                connection,
                transaction,
                $@"select count(1) from Erp_BankAccountCreditCardPeriod with (nolock)
                   where BankAccountId={bankAccountId} and IsNull(IsDeleted,0)=0");
            return value != null && Convert.ToInt32(value) > 0;
        }

        public static string GetBankAccountDisplayName(
            ProviderType provider,
            DbConnection connection,
            DbTransaction transaction,
            long bankAccountId)
        {
            if (bankAccountId <= 0) return string.Empty;

            DataTable table = UtilityFunctions.GetDataTableList(
                provider,
                connection,
                transaction,
                "Erp_BankAccount",
                $@"select AccountCode, AccountName
                   from Erp_BankAccount with (nolock)
                   where RecId={bankAccountId}");
            if (table == null || table.Rows.Count == 0)
                return bankAccountId.ToString(CultureInfo.InvariantCulture);

            DataRow row = table.Rows[0];
            string code = row.IsNull("AccountCode") ? string.Empty : row["AccountCode"].ToString();
            string name = row.IsNull("AccountName") ? string.Empty : row["AccountName"].ToString();
            return string.IsNullOrWhiteSpace(name) ? code : $"{code} {name}".Trim();
        }

        public static IList<CreditCardPeriodInfo> LoadActivePeriods(
            ProviderType provider,
            DbConnection connection,
            DbTransaction transaction,
            long bankAccountId)
        {
            var periods = new List<CreditCardPeriodInfo>();
            if (bankAccountId <= 0) return periods;

            DataTable table = UtilityFunctions.GetDataTableList(
                provider,
                connection,
                transaction,
                "Erp_BankAccountCreditCardPeriod",
                $@"select RecId, PeriodNo, PeriodYear, PeriodMonth, StatementStartDate, StatementDate, PaymentDueDate
                   from Erp_BankAccountCreditCardPeriod with (nolock)
                   where BankAccountId={bankAccountId} and IsNull(IsDeleted,0)=0
                   order by PaymentDueDate, PeriodNo");
            if (table == null) return periods;

            foreach (DataRow row in table.Rows)
            {
                short periodYear = Convert.ToInt16(row["PeriodYear"]);
                short periodMonth = Convert.ToInt16(row["PeriodMonth"]);
                periods.Add(new CreditCardPeriodInfo
                {
                    RecId = Convert.ToInt64(row["RecId"]),
                    PeriodNo = Convert.ToInt16(row["PeriodNo"]),
                    PeriodYear = periodYear,
                    PeriodMonth = periodMonth,
                    StatementStartDate = row.IsNull("StatementStartDate")
                        ? new DateTime(periodYear, periodMonth, 1)
                        : Convert.ToDateTime(row["StatementStartDate"]).Date,
                    StatementDate = Convert.ToDateTime(row["StatementDate"]).Date,
                    PaymentDueDate = Convert.ToDateTime(row["PaymentDueDate"]).Date
                });
            }

            return periods;
        }

        public static IList<long> LoadCreditCardBankAccountIds(
            ProviderType provider,
            DbConnection connection,
            DbTransaction transaction,
            int companyId)
        {
            var ids = new List<long>();
            DataTable table = UtilityFunctions.GetDataTableList(
                provider,
                connection,
                transaction,
                "Erp_BankAccount",
                $@"select ba.RecId
                   from Erp_BankAccount ba with (nolock)
                   inner join Erp_Bank b with (nolock) on b.RecId = ba.BankId
                   where b.CompanyId = {companyId}
                     and IsNull(ba.ForCreditCard, 0) = 1
                     and IsNull(ba.IsDeleted, 0) = 0
                     and IsNull(b.IsDeleted, 0) = 0
                   order by ba.AccountCode");
            if (table == null) return ids;

            foreach (DataRow row in table.Rows)
                ids.Add(Convert.ToInt64(row["RecId"]));

            return ids;
        }

        public static int FindStartPeriodIndex(IList<CreditCardPeriodInfo> periods, DateTime paymentReferenceDate)
        {
            if (periods == null || periods.Count == 0) return -1;

            for (int i = 0; i < periods.Count; i++)
            {
                if (periods[i].PaymentDueDate.Date >= paymentReferenceDate.Date)
                    return i;
            }

            return -1;
        }

        public static int FindPeriodIndexByStatementCycle(IList<CreditCardPeriodInfo> periods, DateTime movementDate)
        {
            if (periods == null || periods.Count == 0)
                return -1;

            var date = movementDate.Date;

            for (int i = 0; i < periods.Count; i++)
            {
                if (periods[i].StatementStartDate.Date <= date && date <= periods[i].StatementDate.Date)
                    return i;
            }

            if (date < periods[0].StatementStartDate.Date)
                return -1;

            if (date > periods[periods.Count - 1].StatementDate.Date)
                return periods.Count - 1;

            return -1;
        }

        public static CreditCardAllocationPreview BuildAllocationPreview(
            ProviderType provider,
            DbConnection connection,
            DbTransaction transaction,
            long bankAccountId,
            DateTime paymentReferenceDate,
            short installmentCount)
        {
            var periods = LoadActivePeriods(provider, connection, transaction, bankAccountId);
            int startIndex = FindStartPeriodIndex(periods, paymentReferenceDate);
            DateTime firstDueDate = startIndex >= 0 ? periods[startIndex].PaymentDueDate : DateTime.MinValue;

            return new CreditCardAllocationPreview
            {
                BankAccountId = bankAccountId,
                BankAccountDisplayName = GetBankAccountDisplayName(provider, connection, transaction, bankAccountId),
                FirstInstallmentDueDate = firstDueDate,
                Periods = periods
            };
        }

        public static decimal[] SplitAmount(decimal totalAmount, short installmentCount)
        {
            if (installmentCount < 1) installmentCount = 1;

            var amounts = new decimal[installmentCount];
            decimal baseAmount = Math.Round(totalAmount / installmentCount, 2, MidpointRounding.AwayFromZero);
            decimal allocated = 0m;

            for (int i = 0; i < installmentCount - 1; i++)
            {
                amounts[i] = baseAmount;
                allocated += baseAmount;
            }

            amounts[installmentCount - 1] = totalAmount - allocated;
            return amounts;
        }

        public static string FormatDate(DateTime date)
        {
            return date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
        }
    }
}
