using CashFlowManagementModule.BoExtensions;

using Sentez.Data.BusinessObjects;
using Sentez.Localization;

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace CashFlowManagementModule.Services
{
    public sealed class CreditCardStatementPeriod
    {
        public short PeriodNo { get; set; }
        public short PeriodYear { get; set; }
        public short PeriodMonth { get; set; }
        public DateTime StatementStartDate { get; set; }
        public DateTime StatementDate { get; set; }
        public DateTime PaymentDueDate { get; set; }
        public DateTime CardExpiryDate { get; set; }
    }

    public static class CreditCardStatementPeriodGeneratorService
    {
        public static DateTime SafeDate(int year, int month, int day)
        {
            var lastDay = DateTime.DaysInMonth(year, month);
            var actualDay = Math.Min(day, lastDay);
            return new DateTime(year, month, actualDay);
        }

        public static bool TryParseInputs(DataRow bankAccountRow, out short expiryMonth, out short expiryYear, out short statementCutDay, out short paymentDueDay, out string errorMessage)
        {
            expiryMonth = 0;
            expiryYear = 0;
            statementCutDay = 0;
            paymentDueDay = 0;
            errorMessage = null;

            if (bankAccountRow == null)
            {
                errorMessage = SLanguage.GetString("Banka hesap kartı bulunamadı.");
                return false;
            }

            if (!Convert.ToBoolean(bankAccountRow["ForCreditCard"]))
            {
                errorMessage = SLanguage.GetString("Bu işlem yalnızca kredi kartı hesapları için kullanılabilir.");
                return false;
            }

            if (bankAccountRow.IsNull(BankAccountCreditCardHelper.FieldExpiryMonth) ||
                bankAccountRow.IsNull(BankAccountCreditCardHelper.FieldExpiryYear) ||
                bankAccountRow.IsNull(BankAccountCreditCardHelper.FieldStatementCutDate) ||
                bankAccountRow.IsNull(BankAccountCreditCardHelper.FieldPaymentDueDate))
            {
                errorMessage = SLanguage.GetString("Son kullanma ay/yıl ve hesap kesim/son ödeme tarih bilgileri eksik.");
                return false;
            }

            expiryMonth = Convert.ToInt16(bankAccountRow[BankAccountCreditCardHelper.FieldExpiryMonth]);
            expiryYear = Convert.ToInt16(bankAccountRow[BankAccountCreditCardHelper.FieldExpiryYear]);
            statementCutDay = (short)Convert.ToDateTime(bankAccountRow[BankAccountCreditCardHelper.FieldStatementCutDate]).Day;
            paymentDueDay = (short)Convert.ToDateTime(bankAccountRow[BankAccountCreditCardHelper.FieldPaymentDueDate]).Day;

            if (expiryMonth < 1 || expiryMonth > 12)
            {
                errorMessage = SLanguage.GetString("Son kullanma ay değeri 1-12 arasında olmalıdır.");
                return false;
            }

            if (expiryYear < 2000 || expiryYear > 9999)
            {
                errorMessage = SLanguage.GetString("Son kullanma yıl değeri geçersiz.");
                return false;
            }

            if (statementCutDay < 1 || statementCutDay > 31 || paymentDueDay < 1 || paymentDueDay > 31)
            {
                errorMessage = SLanguage.GetString("Hesap kesim ve son ödeme gün değerleri 1-31 arasında olmalıdır.");
                return false;
            }

            var today = DateTime.Today;
            var expiryMonthStart = new DateTime(expiryYear, expiryMonth, 1);
            var currentMonthStart = new DateTime(today.Year, today.Month, 1);
            if (expiryMonthStart < currentMonthStart)
            {
                errorMessage = SLanguage.GetString("Son kullanma tarihi bugünün ay/yılından önce olamaz.");
                return false;
            }

            return true;
        }

        public static List<CreditCardStatementPeriod> GeneratePeriods(short expiryMonth, short expiryYear, short statementCutDay, short paymentDueDay, DateTime? referenceDate = null)
        {
            var today = (referenceDate ?? DateTime.Today).Date;
            var startCursor = new DateTime(today.Year, today.Month, 1);
            var endCursor = new DateTime(expiryYear, expiryMonth, 1);
            var cardExpiryDate = SafeDate(expiryYear, expiryMonth, DateTime.DaysInMonth(expiryYear, expiryMonth));

            var periods = new List<CreditCardStatementPeriod>();
            DateTime? previousStatementDate = null;
            short periodNo = 0;

            for (var cursor = startCursor; cursor <= endCursor; cursor = cursor.AddMonths(1))
            {
                periodNo++;
                var statementDate = SafeDate(cursor.Year, cursor.Month, statementCutDay);
                DateTime paymentDueDate;
                if (paymentDueDay >= statementCutDay)
                    paymentDueDate = SafeDate(cursor.Year, cursor.Month, paymentDueDay);
                else
                {
                    var nextMonth = cursor.AddMonths(1);
                    paymentDueDate = SafeDate(nextMonth.Year, nextMonth.Month, paymentDueDay);
                }

                var statementStartDate = previousStatementDate.HasValue
                    ? previousStatementDate.Value.AddDays(1)
                    : new DateTime(cursor.Year, cursor.Month, 1);

                periods.Add(new CreditCardStatementPeriod
                {
                    PeriodNo = periodNo,
                    PeriodYear = (short)cursor.Year,
                    PeriodMonth = (short)cursor.Month,
                    StatementStartDate = statementStartDate,
                    StatementDate = statementDate,
                    PaymentDueDate = paymentDueDate,
                    CardExpiryDate = cardExpiryDate
                });

                previousStatementDate = statementDate;
            }

            return periods;
        }

        public static bool HasActivePeriods(DataTable periodTable)
        {
            if (periodTable == null) return false;

            return periodTable.Rows.Cast<DataRow>().Any(row =>
                row.RowState != DataRowState.Deleted &&
                (row.IsNull("IsDeleted") || !Convert.ToBoolean(row["IsDeleted"])));
        }

        public static void SoftDeleteActivePeriods(DataTable periodTable, int deletedBy, DateTime deletedAt)
        {
            if (periodTable == null) return;

            foreach (DataRow row in periodTable.Rows)
            {
                if (row.RowState == DataRowState.Deleted) continue;
                if (row.IsNull("IsDeleted") || !Convert.ToBoolean(row["IsDeleted"]))
                {
                    row["IsDeleted"] = true;
                    row["DeletedAt"] = deletedAt;
                    row["DeletedBy"] = deletedBy;
                }
            }
        }

        public static string ApplyGeneratedPeriods(BusinessObjectBase bo, IList<CreditCardStatementPeriod> periods)
        {
            if (bo?.CurrentRow?.Row == null)
                return SLanguage.GetString("Banka hesap kartı bulunamadı.");

            if (!bo.Data.Tables.Contains(BankAccountCreditCardHelper.PeriodTableName))
                return SLanguage.GetString("Ekstre dönem tablosu yüklenemedi.");

            var bankAccountRow = bo.CurrentRow.Row;
            var periodTable = bo.Data.Tables[BankAccountCreditCardHelper.PeriodTableName];
            var bankAccountId = Convert.ToInt64(bankAccountRow["RecId"]);
            var companyId = bo.CompanyId;// bankAccountRow.IsNull("CompanyId") ? (object)DBNull.Value : bankAccountRow["CompanyId"];
            var userId = (int)(bo.ActiveSession?.ActiveUser?.RecId ?? 0);
            var now = DateTime.Now;

            foreach (var period in periods)
            {
                var row = periodTable.NewRow();
                row["CompanyId"] = companyId;
                row["BankAccountId"] = bankAccountId;
                row["PeriodNo"] = period.PeriodNo;
                row["PeriodYear"] = period.PeriodYear;
                row["PeriodMonth"] = period.PeriodMonth;
                row["StatementStartDate"] = period.StatementStartDate;
                row["StatementDate"] = period.StatementDate;
                row["PaymentDueDate"] = period.PaymentDueDate;
                row["CardExpiryDate"] = period.CardExpiryDate;
                row["InUse"] = true;
                row["IsDeleted"] = false;
                row["InsertedAt"] = now;
                row["InsertedBy"] = userId;
                periodTable.Rows.Add(row);
            }

            return null;
        }
    }
}
