using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;

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

            if (lines == null || lines.Count == 0)
                return CreditCardPaymentValidationResult.Success();

            DbConnection connection = session._dbInfo.Connection;
            ProviderType provider = session._dbInfo.DBProvider;
            var creditCardLines = lines.Where(l => l != null && l.BankAccountId > 0).ToList();
            if (creditCardLines.Count == 0)
                return CreditCardPaymentValidationResult.Success();

            foreach (CreditCardPaymentLineInput line in creditCardLines)
            {
                if (!CreditCardStatementDataService.IsCreditCardBankAccount(connection, null, line.BankAccountId))
                    continue;

                if (!CreditCardStatementDataService.HasActivePeriods(connection, null, line.BankAccountId))
                {
                    string accountName = CreditCardStatementDataService.GetBankAccountDisplayName(provider, connection, null, line.BankAccountId);
                    return CreditCardPaymentValidationResult.Blocked(
                        string.IsNullOrWhiteSpace(accountName)
                            ? SLanguage.GetString("Önce kredi kartı dönemlerini oluşturun.")
                            : string.Format(SLanguage.GetString("{0} kartı için önce kredi kartı dönemlerini oluşturun."), accountName));
                }
            }

            var warnings = new List<string>();

            foreach (CreditCardPaymentLineInput line in creditCardLines)
            {
                if (!CreditCardStatementDataService.IsCreditCardBankAccount(connection, null, line.BankAccountId))
                    continue;

                string nearCutWarning = BuildNearCutWarning(provider, connection, line);
                if (!string.IsNullOrWhiteSpace(nearCutWarning))
                    warnings.Add(nearCutWarning);
            }

            string alternativeCardWarning = BuildAlternativeCardWarning(provider, connection, creditCardLines, session.ActiveCompany.RecId ?? 0);
            if (!string.IsNullOrWhiteSpace(alternativeCardWarning))
                warnings.Add(alternativeCardWarning);

            if (warnings.Count == 0)
                return CreditCardPaymentValidationResult.Success();

            return CreditCardPaymentValidationResult.Warning(string.Join(Environment.NewLine + Environment.NewLine, warnings.Distinct()));
        }

        public static CreditCardPaymentValidationResult ValidateLinePreview(LiveSession session, CreditCardPaymentLineInput line)
        {
            if (line == null || line.BankAccountId <= 0)
                return CreditCardPaymentValidationResult.Success();

            return ValidateBeforePayment(session, new List<CreditCardPaymentLineInput> { line });
        }

        static string BuildNearCutWarning(ProviderType provider, DbConnection connection, CreditCardPaymentLineInput line)
        {
            var preview = CreditCardStatementDataService.BuildAllocationPreview(
                provider,
                connection,
                null,
                line.BankAccountId,
                line.PaymentReferenceDate,
                line.InstallmentCount);

            int startIndex = CreditCardStatementDataService.FindStartPeriodIndex(preview.Periods, line.PaymentReferenceDate);
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

        static string BuildAlternativeCardWarning(ProviderType provider, DbConnection connection, IList<CreditCardPaymentLineInput> lines, int companyId)
        {
            CreditCardPaymentLineInput referenceLine = lines.FirstOrDefault(l =>
                l != null
                && l.BankAccountId > 0
                && CreditCardStatementDataService.IsCreditCardBankAccount(connection, null, l.BankAccountId));
            if (referenceLine == null) return null;

            var currentPreview = CreditCardStatementDataService.BuildAllocationPreview(
                provider,
                connection,
                null,
                referenceLine.BankAccountId,
                referenceLine.PaymentReferenceDate,
                referenceLine.InstallmentCount);
            if (currentPreview.FirstInstallmentDueDate == DateTime.MinValue) return null;

            CreditCardAllocationPreview bestPreview = null;
            foreach (long bankAccountId in CreditCardStatementDataService.LoadCreditCardBankAccountIds(provider, connection, null, companyId))
            {
                if (bankAccountId == referenceLine.BankAccountId) continue;
                if (!CreditCardStatementDataService.HasActivePeriods(connection, null, bankAccountId)) continue;

                var preview = CreditCardStatementDataService.BuildAllocationPreview(
                    provider,
                    connection,
                    null,
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
