using System;

using System.Collections.Generic;

using System.Data;

using System.Globalization;



using CashFlowManagementModule.BoExtensions;



using Sentez.Common.SystemServices;



using Sentez.Localization;



namespace CashFlowManagementModule.Services

{

    public static class CreditCardStatementAllocationService

    {

        public static string AllocateAfterPayment(LiveSession session, CreditCardAllocationRequest request)

        {

            return AllocateAfterPayment(CashFlowDbContext.FromSession(session), session, request);

        }



        public static string AllocateAfterPayment(CashFlowDbContext context, LiveSession session, CreditCardAllocationRequest request)

        {

            if (!context.IsValid)

                return SLanguage.GetString("Veritabanı bağlantısı bulunamadı.");



            if (session == null)

                return SLanguage.GetString("Veritabanı bağlantısı bulunamadı.");



            if (request == null || request.BankAccountId <= 0)

                return SLanguage.GetString("Kredi kartı hesabı seçilmemiş.");



            if (request.InstallmentCount < 1)

                request.InstallmentCount = 1;



            if (!CreditCardStatementDataService.IsCreditCardBankAccount(context, request.BankAccountId))

                return null;



            if (!CreditCardStatementDataService.HasActivePeriods(context, request.BankAccountId))

                return SLanguage.GetString("Önce kredi kartı dönemlerini oluşturun.");



            IList<CreditCardPeriodInfo> periods = CreditCardStatementDataService.LoadActivePeriods(context, request.BankAccountId);

            int startIndex = CreditCardStatementDataService.FindStartPeriodIndex(periods, request.PaymentReferenceDate);

            if (startIndex < 0)

                return SLanguage.GetString("Ödeme tarihi için uygun ekstre dönemi bulunamadı.");



            if (startIndex + request.InstallmentCount > periods.Count)

                return SLanguage.GetString("Taksit sayısı için yeterli ekstre dönemi bulunmuyor.");



            SoftDeleteExistingAllocations(context, request);

            decimal[] amounts = CreditCardStatementDataService.SplitAmount(request.TotalAmount, request.InstallmentCount);

            decimal[] forexAmounts = request.TotalForexAmount.HasValue

                ? CreditCardStatementDataService.SplitAmount(request.TotalForexAmount.Value, request.InstallmentCount)

                : null;

            DateTime now = DateTime.Now;



            for (short installmentNo = 1; installmentNo <= request.InstallmentCount; installmentNo++)

            {

                CreditCardPeriodInfo period = periods[startIndex + installmentNo - 1];

                decimal amount = amounts[installmentNo - 1];

                decimal? forexAmount = forexAmounts != null ? forexAmounts[installmentNo - 1] : (decimal?)null;



                InsertAllocation(context, request, period, installmentNo, amount, forexAmount, now);

            }



            return null;

        }



        public static string AllocateFromBankReceiptItem(LiveSession session, DataRow bankReceiptItemRow, DataRow currentAccountReceiptItemRow)

        {

            return AllocateFromBankReceiptItem(CashFlowDbContext.FromSession(session), session, bankReceiptItemRow, currentAccountReceiptItemRow);

        }



        public static string AllocateFromBankReceiptItem(

            CashFlowDbContext context,

            LiveSession session,

            DataRow bankReceiptItemRow,

            DataRow currentAccountReceiptItemRow)

        {

            if (bankReceiptItemRow == null)

                return SLanguage.GetString("Ödeme planlama satırı bulunamadı.");



            DateTime paymentDate = DateTime.Today;

            if (bankReceiptItemRow.Table.Columns.Contains("UD_PaymentDate") && !bankReceiptItemRow.IsNull("UD_PaymentDate"))

                paymentDate = Convert.ToDateTime(bankReceiptItemRow["UD_PaymentDate"]).Date;



            decimal amount = 0m;
            decimal? forexAmount = null;
            if (bankReceiptItemRow.Table.Columns.Contains("Debit") && !bankReceiptItemRow.IsNull("Debit"))
            {
                decimal debitAmount = Convert.ToDecimal(bankReceiptItemRow["Debit"]);
                if (debitAmount != 0m)
                {
                    amount = debitAmount;
                    if (bankReceiptItemRow.Table.Columns.Contains("ForexDebit") && !bankReceiptItemRow.IsNull("ForexDebit"))
                        forexAmount = Convert.ToDecimal(bankReceiptItemRow["ForexDebit"]);
                }
            }

            if (amount == 0m && !bankReceiptItemRow.IsNull("Credit"))
            {
                amount = Convert.ToDecimal(bankReceiptItemRow["Credit"]);
                if (bankReceiptItemRow.Table.Columns.Contains("ForexCredit") && !bankReceiptItemRow.IsNull("ForexCredit"))
                    forexAmount = Convert.ToDecimal(bankReceiptItemRow["ForexCredit"]);
            }



            long? bankReceiptItemId = bankReceiptItemRow.IsNull("RecId") ? null : Convert.ToInt64(bankReceiptItemRow["RecId"]);

            long? currentAccountReceiptItemId = currentAccountReceiptItemRow != null && !currentAccountReceiptItemRow.IsNull("RecId")

                ? Convert.ToInt64(currentAccountReceiptItemRow["RecId"])

                : (long?)null;



            long bankAccountId = bankReceiptItemRow.IsNull("BankAccountId") ? 0L : Convert.ToInt64(bankReceiptItemRow["BankAccountId"]);

            string explanation = bankReceiptItemRow.IsNull("Explanation") ? null : bankReceiptItemRow["Explanation"].ToString();



            var request = new CreditCardAllocationRequest

            {

                CompanyId = session.ActiveCompany.RecId ?? 0,

                UserId = (int)(session.ActiveUser?.RecId ?? 0),

                BankAccountId = bankAccountId,

                BankReceiptItemId = bankReceiptItemId,

                CurrentAccountReceiptItemId = currentAccountReceiptItemId,

                PaymentReferenceDate = paymentDate,

                TotalAmount = amount,

                TotalForexAmount = forexAmount,

                InstallmentCount = BankReceiptCreditCardHelper.GetInstallmentCount(bankReceiptItemRow),

                Explanation = explanation

            };



            return AllocateAfterPayment(context, session, request);

        }



        public static void SoftDeleteByCurrentAccountReceiptItem(LiveSession session, long currentAccountReceiptItemId)

        {

            SoftDeleteByCurrentAccountReceiptItem(CashFlowDbContext.FromSession(session), session, currentAccountReceiptItemId);

        }



        public static void SoftDeleteByCurrentAccountReceiptItem(CashFlowDbContext context, LiveSession session, long currentAccountReceiptItemId)

        {

            if (!context.IsValid || currentAccountReceiptItemId <= 0 || session == null) return;



            int userId = (int)(session.ActiveUser?.RecId ?? 0);

            CashFlowDbAccess.ExecuteNonQuery(

                context,

                $@"update Erp_BankAccountCreditCardPeriodAllocation

                   set IsDeleted=1, DeletedAt=getdate(), DeletedBy={userId}, UpdatedAt=getdate(), UpdatedBy={userId}

                   where CurrentAccountReceiptItemId={currentAccountReceiptItemId} and IsNull(IsDeleted,0)=0");

        }



        public static void SoftDeleteByBankReceiptItem(LiveSession session, long bankReceiptItemId)

        {

            SoftDeleteByBankReceiptItem(CashFlowDbContext.FromSession(session), session, bankReceiptItemId);

        }



        public static void SoftDeleteByBankReceiptItem(CashFlowDbContext context, LiveSession session, long bankReceiptItemId)

        {

            if (!context.IsValid || bankReceiptItemId <= 0 || session == null) return;



            int userId = (int)(session.ActiveUser?.RecId ?? 0);

            CashFlowDbAccess.ExecuteNonQuery(

                context,

                $@"update Erp_BankAccountCreditCardPeriodAllocation

                   set IsDeleted=1, DeletedAt=getdate(), DeletedBy={userId}, UpdatedAt=getdate(), UpdatedBy={userId}

                   where BankReceiptItemId={bankReceiptItemId} and IsNull(IsDeleted,0)=0");

        }



        public static void SoftDeleteByCurrentAccountReceipt(LiveSession session, long currentAccountReceiptId)

        {

            SoftDeleteByCurrentAccountReceipt(CashFlowDbContext.FromSession(session), session, currentAccountReceiptId);

        }



        public static void SoftDeleteByCurrentAccountReceipt(CashFlowDbContext context, LiveSession session, long currentAccountReceiptId)

        {

            if (!context.IsValid || currentAccountReceiptId <= 0 || session == null) return;



            int userId = (int)(session.ActiveUser?.RecId ?? 0);

            CashFlowDbAccess.ExecuteNonQuery(

                context,

                $@"update a set a.IsDeleted=1, a.DeletedAt=getdate(), a.DeletedBy={userId}, a.UpdatedAt=getdate(), a.UpdatedBy={userId}

                   from Erp_BankAccountCreditCardPeriodAllocation a

                   inner join Erp_CurrentAccountReceiptItem i on i.RecId=a.CurrentAccountReceiptItemId

                   where i.CurrentAccountReceiptId={currentAccountReceiptId} and IsNull(a.IsDeleted,0)=0");

        }



        static void SoftDeleteExistingAllocations(CashFlowDbContext context, CreditCardAllocationRequest request)

        {

            int userId = request.UserId;

            if (request.CurrentAccountReceiptItemId.HasValue && request.CurrentAccountReceiptItemId.Value > 0)

            {

                CashFlowDbAccess.ExecuteNonQuery(

                    context,

                    $@"update Erp_BankAccountCreditCardPeriodAllocation

                       set IsDeleted=1, DeletedAt=getdate(), DeletedBy={userId}, UpdatedAt=getdate(), UpdatedBy={userId}

                       where CurrentAccountReceiptItemId={request.CurrentAccountReceiptItemId.Value} and IsNull(IsDeleted,0)=0");

            }



            if (request.BankReceiptItemId.HasValue && request.BankReceiptItemId.Value > 0)

            {

                CashFlowDbAccess.ExecuteNonQuery(

                    context,

                    $@"update Erp_BankAccountCreditCardPeriodAllocation

                       set IsDeleted=1, DeletedAt=getdate(), DeletedBy={userId}, UpdatedAt=getdate(), UpdatedBy={userId}

                       where BankReceiptItemId={request.BankReceiptItemId.Value} and IsNull(IsDeleted,0)=0");

            }

        }



        static void InsertAllocation(

            CashFlowDbContext context,

            CreditCardAllocationRequest request,

            CreditCardPeriodInfo period,

            short installmentNo,

            decimal amount,

            decimal? forexAmount,

            DateTime now)

        {

            string explanation = EscapeSql(request.Explanation);

            string forexSql = forexAmount.HasValue

                ? forexAmount.Value.ToString(CultureInfo.InvariantCulture)

                : "NULL";



            CashFlowDbAccess.ExecuteNonQuery(

                context,

                $@"insert into Erp_BankAccountCreditCardPeriodAllocation

                   (CompanyId, BankAccountId, CreditCardPeriodId, BankReceiptItemId, CurrentAccountReceiptItemId,

                    InstallmentNo, InstallmentCount, Amount, ForexAmount, PaymentReferenceDate, Explanation,

                    InsertedAt, InsertedBy, IsDeleted, UpdatedAt, UpdatedBy)

                   values

                   ({request.CompanyId}, {request.BankAccountId}, {period.RecId},

                    {(request.BankReceiptItemId.HasValue ? request.BankReceiptItemId.Value.ToString(CultureInfo.InvariantCulture) : "NULL")},

                    {(request.CurrentAccountReceiptItemId.HasValue ? request.CurrentAccountReceiptItemId.Value.ToString(CultureInfo.InvariantCulture) : "NULL")},

                    {installmentNo}, {request.InstallmentCount}, {amount.ToString(CultureInfo.InvariantCulture)}, {forexSql},

                    '{request.PaymentReferenceDate:yyyy-MM-dd}',

                    {(string.IsNullOrWhiteSpace(explanation) ? "NULL" : $"'{explanation}'")},

                    '{now:yyyy-MM-dd HH:mm:ss}', {request.UserId}, 0, '{now:yyyy-MM-dd HH:mm:ss}', {request.UserId})");

        }



        static string EscapeSql(string value)

        {

            return string.IsNullOrEmpty(value) ? value : value.Replace("'", "''");

        }

    }

}


