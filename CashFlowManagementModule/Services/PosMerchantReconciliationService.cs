using System;
using System.Data;

using CashFlowManagementModule.BoExtensions;

using Sentez.Common.SystemServices;
using Sentez.Core.ParameterClasses;
using Sentez.Data.Tools;
using Sentez.Localization;

namespace CashFlowManagementModule.Services
{
    public sealed class PosMerchantReconciliationResult
    {
        public decimal RawGrossTotal { get; set; }
        public decimal CalculatedGrossTotal { get; set; }
        public decimal CalculatedNetTotal { get; set; }
        public decimal RawRefundTotal { get; set; }
        public decimal CalculatedRefundTotal { get; set; }
        public decimal GrossDifference { get; set; }
        public decimal RefundDifference { get; set; }
        public bool IsBalanced { get; set; }
        public string Message { get; set; }
    }

    public static class PosMerchantReconciliationService
    {
        public static PosMerchantReconciliationResult ReconcilePeriod(
            LiveSession session,
            long bankAccountId,
            int periodYear,
            int periodMonth)
        {
            var result = new PosMerchantReconciliationResult();

            if (session?._dbInfo?.Connection == null || bankAccountId <= 0)
            {
                result.Message = SLanguage.GetString("Geçersiz hesap veya oturum bilgisi.");
                return result;
            }

            PosMerchantAggregationResult aggregation =
                PosMerchantMovementAggregationService.BuildForPeriod(session, bankAccountId, periodYear, periodMonth);

            result.CalculatedGrossTotal = aggregation.Summary.TotalGross;
            result.CalculatedNetTotal = aggregation.Summary.TotalNet;
            result.CalculatedRefundTotal = aggregation.Summary.TotalRefund;

            int amountDec = session.ParamService?.GetParameterClass<GeneralParameters>()?.AmountDec ?? 2;
            DateTime periodStart = new DateTime(periodYear, periodMonth, 1);
            DateTime periodEnd = periodStart.AddMonths(1).AddDays(-1);
            int companyId = session.ActiveCompany.RecId ?? 0;

            DataTable rawTable = UtilityFunctions.GetDataTableList(
                session._dbInfo.DBProvider,
                session._dbInfo.Connection,
                null,
                "PosMerchantReconciliation",
                $@"select cr.ReceiptType,
                          sum(case when cr.ReceiptType = {BankAccountPosHelper.CustomerCreditCardCollectionReceiptType}
                                   then isnull(cri.Credit,0) else 0 end) GrossTotal,
                          sum(case when cr.ReceiptType = {BankAccountPosHelper.CustomerCreditCardRefundReceiptType}
                                   then isnull(cri.Debit, case when isnull(cri.Credit,0)=0 then 0 else cri.Credit end) else 0 end) RefundTotal
                   from Erp_CurrentAccountReceiptItem cri with (nolock)
                   inner join Erp_CurrentAccountReceipt cr with (nolock) on cr.RecId = cri.CurrentAccountReceiptId
                   inner join Erp_BankAccount ba with (nolock) on ba.RecId = cri.BankAccountId
                   inner join Erp_Bank b with (nolock) on b.RecId = ba.BankId
                   where b.CompanyId = {companyId}
                     and cri.BankAccountId = {bankAccountId}
                     and cr.ReceiptType in ({BankAccountPosHelper.CustomerCreditCardCollectionReceiptType},{BankAccountPosHelper.CustomerCreditCardRefundReceiptType})
                     and isnull(cri.IsDeleted,0)=0
                     and isnull(cr.IsDeleted,0)=0
                     and cri.ReceiptDate >= '{periodStart:yyyy-MM-dd}'
                     and cri.ReceiptDate <= '{periodEnd:yyyy-MM-dd}'
                   group by cr.ReceiptType");

            if (rawTable != null)
            {
                foreach (DataRow row in rawTable.Rows)
                {
                    if (row.IsNull("ReceiptType"))
                        continue;

                    short receiptType = Convert.ToInt16(row["ReceiptType"]);
                    if (receiptType == BankAccountPosHelper.CustomerCreditCardCollectionReceiptType)
                        result.RawGrossTotal += Convert.ToDecimal(row["GrossTotal"]);
                    else if (receiptType == BankAccountPosHelper.CustomerCreditCardRefundReceiptType)
                        result.RawRefundTotal += Convert.ToDecimal(row["RefundTotal"]);
                }
            }

            result.RawGrossTotal = Math.Round(result.RawGrossTotal, amountDec, MidpointRounding.AwayFromZero);
            result.RawRefundTotal = Math.Round(result.RawRefundTotal, amountDec, MidpointRounding.AwayFromZero);
            result.GrossDifference = Math.Round(result.RawGrossTotal - result.CalculatedGrossTotal, amountDec, MidpointRounding.AwayFromZero);
            result.RefundDifference = Math.Round(result.RawRefundTotal - result.CalculatedRefundTotal, amountDec, MidpointRounding.AwayFromZero);
            result.IsBalanced = result.GrossDifference == 0m && result.RefundDifference == 0m;

            if (result.IsBalanced)
            {
                result.Message = SLanguage.GetString("Brüt ve iade tutarları fiş hareketleri ile uyumludur.");
            }
            else
            {
                result.Message = string.Format(
                    SLanguage.GetString("Mutabakat farkı: Brüt fark {0}, İade fark {1}."),
                    result.GrossDifference,
                    result.RefundDifference);
            }

            if (!aggregation.Summary.HasDeductionProfile)
            {
                result.Message += " " + SLanguage.GetString("Kesinti oran profili tanımlı değil.");
            }

            return result;
        }
    }
}
