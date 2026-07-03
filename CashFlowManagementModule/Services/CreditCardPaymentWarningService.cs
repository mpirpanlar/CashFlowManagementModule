using System;

using System.Collections.Generic;

using System.Data.Common;

using System.Linq;



using Sentez.Common.SystemServices;

using Sentez.Data.Tools;



using Sentez.Localization;



namespace CashFlowManagementModule.Services

{

    public static class CreditCardPaymentWarningService

    {

        public static CreditCardPaymentValidationResult ValidateBeforePayment(LiveSession session, IList<CreditCardPaymentLineInput> lines)

        {

            if (session?._dbInfo?.Connection == null)

                return CreditCardPaymentValidationResult.Blocked(SLanguage.GetString("Veritabanı bağlantısı bulunamadı."));



            CashFlowDbContext context = CashFlowDbContext.FromSession(session);

            return ValidateBeforePayment(context, session.ActiveCompany.RecId ?? 0, lines);

        }



        public static CreditCardPaymentValidationResult ValidateBeforePayment(

            ProviderType provider,

            DbConnection connection,

            DbTransaction transaction,

            int companyId,

            IList<CreditCardPaymentLineInput> lines)

        {

            return ValidateBeforePayment(

                CashFlowDbContext.From(connection, transaction, provider, keepConnectionOpen: true),

                companyId,

                lines);

        }



        public static CreditCardPaymentValidationResult ValidateBeforePayment(

            CashFlowDbContext context,

            int companyId,

            IList<CreditCardPaymentLineInput> lines)

        {

            if (!context.IsValid)

                return CreditCardPaymentValidationResult.Blocked(SLanguage.GetString("Veritabanı bağlantısı bulunamadı."));



            if (lines == null || lines.Count == 0)

                return CreditCardPaymentValidationResult.Success();



            using (CashFlowDbAccess.BeginScope(context))

            {

                var creditCardLines = lines.Where(l => l != null && l.BankAccountId > 0).ToList();

                if (creditCardLines.Count == 0)

                    return CreditCardPaymentValidationResult.Success();



                foreach (CreditCardPaymentLineInput line in creditCardLines)

                {

                    if (!CreditCardStatementDataService.IsCreditCardBankAccount(context, line.BankAccountId))

                        continue;



                    if (!CreditCardStatementDataService.HasActivePeriods(context, line.BankAccountId))

                    {

                        string accountName = CreditCardStatementDataService.GetBankAccountDisplayName(context, line.BankAccountId);

                        return CreditCardPaymentValidationResult.Blocked(

                            string.IsNullOrWhiteSpace(accountName)

                                ? SLanguage.GetString("Önce kredi kartı dönemlerini oluşturun.")

                                : string.Format(SLanguage.GetString("{0} kartı için önce kredi kartı dönemlerini oluşturun."), accountName));

                    }

                }



                var warnings = new List<string>();



                foreach (CreditCardPaymentLineInput line in creditCardLines)

                {

                    if (!CreditCardStatementDataService.IsCreditCardBankAccount(context, line.BankAccountId))

                        continue;



                    string nearCutWarning = BuildNearCutWarning(context, line);

                    if (!string.IsNullOrWhiteSpace(nearCutWarning))

                        warnings.Add(nearCutWarning);

                }



                string alternativeCardWarning = BuildAlternativeCardWarning(context, creditCardLines, companyId);

                if (!string.IsNullOrWhiteSpace(alternativeCardWarning))

                    warnings.Add(alternativeCardWarning);



                if (warnings.Count == 0)

                    return CreditCardPaymentValidationResult.Success();



                return CreditCardPaymentValidationResult.Warning(string.Join(System.Environment.NewLine + System.Environment.NewLine, warnings.Distinct()));

            }

        }



        public static CreditCardPaymentValidationResult ValidateLinePreview(LiveSession session, CreditCardPaymentLineInput line)

        {

            if (line == null || line.BankAccountId <= 0)

                return CreditCardPaymentValidationResult.Success();



            return ValidateBeforePayment(session, new List<CreditCardPaymentLineInput> { line });

        }



        public static CreditCardPaymentValidationResult ValidateLinePreview(

            ProviderType provider,

            DbConnection connection,

            DbTransaction transaction,

            int companyId,

            CreditCardPaymentLineInput line)

        {

            if (line == null || line.BankAccountId <= 0)

                return CreditCardPaymentValidationResult.Success();



            return ValidateBeforePayment(provider, connection, transaction, companyId, new List<CreditCardPaymentLineInput> { line });

        }



        static string BuildNearCutWarning(CashFlowDbContext context, CreditCardPaymentLineInput line)

        {

            var preview = CreditCardStatementDataService.BuildAllocationPreview(

                context,

                line.BankAccountId,

                line.PaymentReferenceDate,

                line.InstallmentCount);



            int startIndex = CreditCardStatementDataService.FindPeriodIndexByStatementCycle(preview.Periods, line.PaymentReferenceDate);

            if (startIndex < 0) return null;



            CreditCardPeriodInfo period = preview.Periods[startIndex];

            int daysToCut = (period.StatementDate - line.PaymentReferenceDate.Date).Days;

            if (daysToCut < 0 || daysToCut > CreditCardStatementDataService.StatementCutNearDaysThreshold)

                return null;



            string accountName = preview.BankAccountDisplayName;

            return string.Format(

                SLanguage.GetString("{0} kartının hesap kesim tarihi ({1}) ödeme tarihine çok yakın. İşlem bir sonraki ekstreye kayabilir."),

                accountName,

                CreditCardStatementDataService.FormatDate(period.StatementDate));

        }



        static string BuildAlternativeCardWarning(

            CashFlowDbContext context,

            IList<CreditCardPaymentLineInput> lines,

            int companyId)

        {

            CreditCardPaymentLineInput referenceLine = lines.FirstOrDefault(l =>

                l != null

                && l.BankAccountId > 0

                && CreditCardStatementDataService.IsCreditCardBankAccount(context, l.BankAccountId));

            if (referenceLine == null) return null;



            var currentPreview = CreditCardStatementDataService.BuildAllocationPreview(

                context,

                referenceLine.BankAccountId,

                referenceLine.PaymentReferenceDate,

                referenceLine.InstallmentCount);

            if (currentPreview.FirstInstallmentDueDate == DateTime.MinValue) return null;



            CreditCardAllocationPreview bestPreview = null;

            foreach (long bankAccountId in CreditCardStatementDataService.LoadCreditCardBankAccountIds(context, companyId))

            {

                if (bankAccountId == referenceLine.BankAccountId) continue;

                if (!CreditCardStatementDataService.HasActivePeriods(context, bankAccountId)) continue;



                var preview = CreditCardStatementDataService.BuildAllocationPreview(

                    context,

                    bankAccountId,

                    referenceLine.PaymentReferenceDate,

                    referenceLine.InstallmentCount);

                if (preview.FirstInstallmentDueDate == DateTime.MinValue) continue;



                if (bestPreview == null || preview.FirstInstallmentDueDate > bestPreview.FirstInstallmentDueDate)

                    bestPreview = preview;

            }



            if (bestPreview == null || bestPreview.FirstInstallmentDueDate <= currentPreview.FirstInstallmentDueDate)

                return null;



            return string.Format(

                SLanguage.GetString("{0} kartı ile ilk taksit {1} son ödemesine denk gelir ({2}: {3})."),

                bestPreview.BankAccountDisplayName,

                CreditCardStatementDataService.FormatDate(bestPreview.FirstInstallmentDueDate),

                currentPreview.BankAccountDisplayName,

                CreditCardStatementDataService.FormatDate(currentPreview.FirstInstallmentDueDate));

        }

    }

}


