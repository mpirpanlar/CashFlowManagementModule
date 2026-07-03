using System;
using System.Collections.Generic;
using System.Data;

using CashFlowManagementModule.BoExtensions;

namespace CashFlowManagementModule.Services
{
    public sealed class CreditCardPaymentLineInput
    {
        public long BankReceiptItemId { get; set; }
        public long BankAccountId { get; set; }
        public DateTime PaymentReferenceDate { get; set; }
        public decimal Amount { get; set; }
        public decimal? ForexAmount { get; set; }
        public short InstallmentCount { get; set; } = 1;
        public string BankAccountDisplayName { get; set; }

        public static CreditCardPaymentLineInput FromBankReceiptItem(DataRow row, DateTime? fallbackPaymentDate = null)
        {
            if (row == null) return null;

            DateTime paymentDate = fallbackPaymentDate ?? DateTime.Today;
            if (row.Table.Columns.Contains("UD_PaymentDate") && !row.IsNull("UD_PaymentDate"))
                paymentDate = Convert.ToDateTime(row["UD_PaymentDate"]).Date;

            decimal amount = 0m;
            if (!row.IsNull("Credit"))
                amount = Convert.ToDecimal(row["Credit"]);

            decimal? forexAmount = null;
            if (row.Table.Columns.Contains("ForexCredit") && !row.IsNull("ForexCredit"))
                forexAmount = Convert.ToDecimal(row["ForexCredit"]);

            long bankAccountId = row.IsNull("BankAccountId") ? 0L : Convert.ToInt64(row["BankAccountId"]);
            long bankReceiptItemId = row.IsNull("RecId") ? 0L : Convert.ToInt64(row["RecId"]);

            return new CreditCardPaymentLineInput
            {
                BankReceiptItemId = bankReceiptItemId,
                BankAccountId = bankAccountId,
                PaymentReferenceDate = paymentDate,
                Amount = amount,
                ForexAmount = forexAmount,
                InstallmentCount = BankReceiptCreditCardHelper.GetInstallmentCount(row)
            };
        }

        public static CreditCardPaymentLineInput FromCurrentAccountReceiptItem(DataRow itemRow, DataRow headerRow)
        {
            if (itemRow == null) return null;

            DateTime? paymentDate = CurrentAccountReceiptCreditCardHelper.ResolveInstalmentStartDate(itemRow, headerRow);
            if (!paymentDate.HasValue)
                paymentDate = DateTime.Today;

            decimal amount = 0m;
            if (!itemRow.IsNull("Debit"))
                amount = Convert.ToDecimal(itemRow["Debit"]);
            else if (!itemRow.IsNull("Credit"))
                amount = Convert.ToDecimal(itemRow["Credit"]);

            decimal? forexAmount = null;
            if (itemRow.Table.Columns.Contains("ForexDebit") && !itemRow.IsNull("ForexDebit"))
                forexAmount = Convert.ToDecimal(itemRow["ForexDebit"]);
            else if (itemRow.Table.Columns.Contains("ForexCredit") && !itemRow.IsNull("ForexCredit"))
                forexAmount = Convert.ToDecimal(itemRow["ForexCredit"]);

            long bankAccountId = itemRow.IsNull("BankAccountId") ? 0L : Convert.ToInt64(itemRow["BankAccountId"]);
            long currentAccountReceiptItemId = itemRow.IsNull("RecId") ? 0L : Convert.ToInt64(itemRow["RecId"]);

            short installmentCount = 1;
            if (itemRow.Table.Columns.Contains("InstallmentCount") && !itemRow.IsNull("InstallmentCount"))
            {
                installmentCount = Convert.ToInt16(itemRow["InstallmentCount"]);
                if (installmentCount < 1)
                    installmentCount = 1;
            }

            return new CreditCardPaymentLineInput
            {
                BankReceiptItemId = 0L,
                BankAccountId = bankAccountId,
                PaymentReferenceDate = paymentDate.Value,
                Amount = amount,
                ForexAmount = forexAmount,
                InstallmentCount = installmentCount
            };
        }
    }

    public sealed class CreditCardPaymentValidationResult
    {
        public bool IsBlocked { get; set; }
        public string BlockMessage { get; set; }
        public bool HasWarning { get; set; }
        public string WarningMessage { get; set; }

        public static CreditCardPaymentValidationResult Blocked(string message)
        {
            return new CreditCardPaymentValidationResult
            {
                IsBlocked = true,
                BlockMessage = message
            };
        }

        public static CreditCardPaymentValidationResult Warning(string message)
        {
            return new CreditCardPaymentValidationResult
            {
                HasWarning = true,
                WarningMessage = message
            };
        }

        public static CreditCardPaymentValidationResult Success()
        {
            return new CreditCardPaymentValidationResult();
        }
    }

    public sealed class CreditCardPeriodInfo
    {
        public long RecId { get; set; }
        public short PeriodNo { get; set; }
        public short PeriodYear { get; set; }
        public short PeriodMonth { get; set; }
        public DateTime StatementStartDate { get; set; }
        public DateTime StatementDate { get; set; }
        public DateTime PaymentDueDate { get; set; }
    }

    public sealed class CreditCardAllocationRequest
    {
        public int CompanyId { get; set; }
        public int UserId { get; set; }
        public long BankAccountId { get; set; }
        public long? BankReceiptItemId { get; set; }
        public long? CurrentAccountReceiptItemId { get; set; }
        public DateTime PaymentReferenceDate { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal? TotalForexAmount { get; set; }
        public short InstallmentCount { get; set; } = 1;
        public string Explanation { get; set; }
    }

    public sealed class CreditCardAllocationPreview
    {
        public long BankAccountId { get; set; }
        public string BankAccountDisplayName { get; set; }
        public DateTime FirstInstallmentDueDate { get; set; }
        public IList<CreditCardPeriodInfo> Periods { get; set; }
    }
}
