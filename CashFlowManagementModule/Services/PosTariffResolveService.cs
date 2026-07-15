using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

using CashFlowManagementModule.BoExtensions;

using Sentez.Common.SystemServices;

namespace CashFlowManagementModule.Services
{
    public sealed class PosTariffResolution
    {
        public bool Found { get; set; }
        public decimal PrimaryRatePercent { get; set; }
        public short? BlockDays { get; set; }
        public IList<PosDeductionProfileLine> MatchedProfiles { get; set; } = new List<PosDeductionProfileLine>();
        public IList<PosDeductionBreakdownLine> Breakdown { get; set; } = new List<PosDeductionBreakdownLine>();
        public decimal TotalDeduction { get; set; }
    }

    /// <summary>
    /// Üye iş yeri hesabı kesinti tarifesini kart kategorisi + taksit ile çözümler.
    /// </summary>
    public static class PosTariffResolveService
    {
        public static PosTariffResolution Resolve(
            LiveSession session,
            long bankAccountId,
            short? cardCategory,
            short installmentCount,
            DateTime asOfDate,
            decimal? grossAmount = null,
            int amountDec = 2)
        {
            var resolution = new PosTariffResolution();
            if (session?._dbInfo?.Connection == null || bankAccountId <= 0)
                return resolution;

            IList<PosDeductionProfileLine> allProfiles =
                PosDeductionCalculationService.LoadActiveProfiles(session, bankAccountId, asOfDate);
            if (allProfiles == null || allProfiles.Count == 0)
                return resolution;

            IList<PosDeductionProfileLine> matched =
                PosDeductionCalculationService.FilterProfilesForMovement(allProfiles, cardCategory, installmentCount);
            if (matched == null || matched.Count == 0)
                return resolution;

            resolution.Found = true;
            resolution.MatchedProfiles = matched;
            resolution.PrimaryRatePercent = ResolvePrimaryRatePercent(matched);
            resolution.BlockDays = ResolveBlockDays(matched);

            if (grossAmount.HasValue && grossAmount.Value > 0m)
            {
                resolution.Breakdown = PosDeductionCalculationService.BuildBreakdown(grossAmount.Value, matched, amountDec);
                resolution.TotalDeduction = PosDeductionCalculationService.CalculateTotalDeduction(grossAmount.Value, matched, amountDec);
            }

            return resolution;
        }

        public static PosTariffResolution ResolveForReceiptItem(
            LiveSession session,
            DataRow itemRow,
            DateTime asOfDate,
            decimal? grossAmount = null,
            int amountDec = 2)
        {
            if (itemRow == null || itemRow.Table == null)
                return new PosTariffResolution();

            if (!itemRow.Table.Columns.Contains("BankAccountId") || itemRow.IsNull("BankAccountId"))
                return new PosTariffResolution();

            long bankAccountId = Convert.ToInt64(itemRow["BankAccountId"]);
            short? cardCategory = PosCardClassificationHelper.TryGetShort(itemRow, PosCardClassificationHelper.FieldCardCategory);
            short installmentCount = 1;
            if (itemRow.Table.Columns.Contains("InstallmentCount") && !itemRow.IsNull("InstallmentCount"))
            {
                short value = Convert.ToInt16(itemRow["InstallmentCount"]);
                installmentCount = value >= 1 ? value : (short)1;
            }

            return Resolve(session, bankAccountId, cardCategory, installmentCount, asOfDate, grossAmount, amountDec);
        }

        static decimal ResolvePrimaryRatePercent(IList<PosDeductionProfileLine> matched)
        {
            PosDeductionProfileLine merchantFee = matched.FirstOrDefault(p =>
                string.Equals(p.DeductionTypeCode, MetaPosDeductionTypeHelper.CodeMerchantFee, StringComparison.OrdinalIgnoreCase));
            if (merchantFee != null)
                return merchantFee.RatePercent;

            return matched.OrderByDescending(p => p.RatePercent).FirstOrDefault()?.RatePercent ?? 0m;
        }

        static short? ResolveBlockDays(IList<PosDeductionProfileLine> matched)
        {
            PosDeductionProfileLine withBlock = matched.FirstOrDefault(p =>
                p.BlockDays.HasValue
                && p.BlockDays.Value > 0
                && string.Equals(p.DeductionTypeCode, MetaPosDeductionTypeHelper.CodeMerchantFee, StringComparison.OrdinalIgnoreCase));

            if (withBlock != null)
                return withBlock.BlockDays;

            return matched.FirstOrDefault(p => p.BlockDays.HasValue && p.BlockDays.Value > 0)?.BlockDays;
        }
    }
}
