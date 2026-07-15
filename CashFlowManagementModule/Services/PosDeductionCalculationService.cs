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
    public static class PosDeductionCalculationService
    {
        public static IList<PosDeductionProfileLine> LoadActiveProfiles(LiveSession session, long bankAccountId, DateTime referenceDate)
        {
            var result = new List<PosDeductionProfileLine>();
            if (session?._dbInfo?.Connection == null || bankAccountId <= 0)
                return result;

            int companyId = session.ActiveCompany.RecId ?? 0;
            referenceDate = referenceDate.Date;
            string sql = $@"
select p.RecId,
       p.DeductionTypeId,
       isnull(t.PosDeductionTypeCode,'') PosDeductionTypeCode,
       isnull(t.PosDeductionTypeName,'') PosDeductionTypeName,
       isnull(p.RatePercent,0) RatePercent,
       isnull(p.FixedAmount,0) FixedAmount,
       isnull(p.CalculationBase,1) CalculationBase,
       p.CardCategory,
       p.InstallmentCount,
       p.BlockDays
from Erp_BankAccountPosDeductionProfile p with (nolock)
inner join Erp_BankAccount ba with (nolock) on ba.RecId = p.BankAccountId
inner join Erp_Bank b with (nolock) on b.RecId = ba.BankId
inner join Meta_PosDeductionType t with (nolock) on t.RecId = p.DeductionTypeId
where p.BankAccountId = {bankAccountId}
  and b.CompanyId = {companyId}
  and isnull(ba.IsDeleted,0)=0
  and isnull(b.IsDeleted,0)=0
  and isnull(p.IsDeleted,0)=0
  and isnull(p.InUse,1)=1
  and p.DeductionTypeId is not null
  and isnull(t.InUse,1)=1
  and isnull(t.IsDeleted,0)=0
  and (p.ValidFrom is null or cast(p.ValidFrom as date) <= '{referenceDate:yyyy-MM-dd}')
  and (p.ValidTo is null or cast(p.ValidTo as date) >= '{referenceDate:yyyy-MM-dd}')
order by t.PosDeductionTypeCode, p.RecId";

            DataTable table = UtilityFunctions.GetDataTableList(
                session._dbInfo.DBProvider,
                session._dbInfo.Connection,
                null,
                BankAccountPosHelper.DeductionProfileTableName,
                sql);

            if (table == null)
                return result;

            foreach (DataRow row in table.Rows)
            {
                result.Add(new PosDeductionProfileLine
                {
                    RecId = Convert.ToInt64(row["RecId"]),
                    DeductionTypeId = Convert.ToInt64(row["DeductionTypeId"]),
                    DeductionTypeCode = Convert.ToString(row["PosDeductionTypeCode"])?.Trim(),
                    DeductionTypeName = Convert.ToString(row["PosDeductionTypeName"])?.Trim(),
                    RatePercent = Convert.ToDecimal(row["RatePercent"]),
                    FixedAmount = Convert.ToDecimal(row["FixedAmount"]),
                    CalculationBase = Convert.ToByte(row["CalculationBase"]),
                    CardCategory = ReadNullableShort(row, "CardCategory"),
                    InstallmentCount = ReadNullableShort(row, "InstallmentCount"),
                    BlockDays = ReadNullableShort(row, "BlockDays")
                });
            }

            return result;
        }

        /// <summary>
        /// Kart kategorisi + taksit için en spesifik profil satırlarını kesinti türü bazında seçer.
        /// NULL kategori/taksit satırları genel (geriye uyumlu) kural olarak kullanılır.
        /// </summary>
        public static IList<PosDeductionProfileLine> FilterProfilesForMovement(
            IList<PosDeductionProfileLine> profiles,
            short? cardCategory,
            short installmentCount)
        {
            if (profiles == null || profiles.Count == 0)
                return new List<PosDeductionProfileLine>();

            short normalizedInstallment = installmentCount < 1 ? (short)1 : installmentCount;

            return profiles
                .Where(p => MatchesCategory(p, cardCategory) && MatchesInstallment(p, normalizedInstallment))
                .GroupBy(p => p.DeductionTypeId)
                .Select(g => g.OrderByDescending(SpecificityScore).ThenBy(p => p.RecId).First())
                .OrderBy(p => p.DeductionTypeCode)
                .ThenBy(p => p.RecId)
                .ToList();
        }

        static bool MatchesCategory(PosDeductionProfileLine profile, short? cardCategory)
        {
            if (!profile.CardCategory.HasValue || profile.CardCategory.Value <= 0)
                return true;

            // Harekette kategori yoksa yalnızca genel (kategori boş) satırlar uygulanır.
            if (!cardCategory.HasValue || cardCategory.Value <= 0)
                return false;

            return profile.CardCategory.Value == cardCategory.Value;
        }

        static bool MatchesInstallment(PosDeductionProfileLine profile, short installmentCount)
        {
            if (!profile.InstallmentCount.HasValue || profile.InstallmentCount.Value <= 0)
                return true;

            short profileInstallment = profile.InstallmentCount.Value <= 1 ? (short)1 : profile.InstallmentCount.Value;
            short movementInstallment = installmentCount <= 1 ? (short)1 : installmentCount;
            return profileInstallment == movementInstallment;
        }

        static int SpecificityScore(PosDeductionProfileLine profile)
        {
            int score = 0;
            if (profile.CardCategory.HasValue && profile.CardCategory.Value > 0)
                score += 2;
            if (profile.InstallmentCount.HasValue && profile.InstallmentCount.Value > 0)
                score += 1;
            return score;
        }

        static short? ReadNullableShort(DataRow row, string columnName)
        {
            if (row == null || !row.Table.Columns.Contains(columnName) || row.IsNull(columnName))
                return null;

            short value = Convert.ToInt16(row[columnName]);
            return value;
        }

        public static decimal CalculateTotalDeduction(
            decimal grossAmount,
            IList<PosDeductionProfileLine> profiles,
            int amountDec)
        {
            if (grossAmount <= 0m || profiles == null || profiles.Count == 0)
                return 0m;

            decimal total = 0m;
            foreach (PosDeductionBreakdownLine line in BuildBreakdown(grossAmount, profiles, amountDec))
                total += line.Amount;

            return RoundAmount(total, amountDec);
        }

        public static IList<PosDeductionBreakdownLine> BuildBreakdown(
            decimal grossAmount,
            IList<PosDeductionProfileLine> profiles,
            int amountDec)
        {
            var lines = new List<PosDeductionBreakdownLine>();
            if (grossAmount <= 0m || profiles == null)
                return lines;

            decimal runningDeduction = 0m;
            foreach (PosDeductionProfileLine profile in profiles)
            {
                decimal baseAmount = ResolveCalculationBase(grossAmount, runningDeduction, profile.CalculationBase);
                if (baseAmount <= 0m)
                    continue;

                decimal amount = CalculateProfileAmountOnBase(baseAmount, profile, amountDec);
                if (amount == 0m)
                    continue;

                runningDeduction += amount;
                lines.Add(new PosDeductionBreakdownLine
                {
                    DeductionTypeId = profile.DeductionTypeId,
                    DeductionTypeCode = profile.DeductionTypeCode,
                    DeductionTypeName = profile.DeductionTypeName,
                    Amount = amount
                });
            }

            return lines;
        }

        static decimal ResolveCalculationBase(decimal grossAmount, decimal priorDeductionTotal, byte calculationBase)
        {
            if (calculationBase == BankAccountPosHelper.CalculationBaseNet)
                return Math.Max(0m, grossAmount - priorDeductionTotal);

            return grossAmount;
        }

        static decimal CalculateProfileAmountOnBase(decimal baseAmount, PosDeductionProfileLine profile, int amountDec)
        {
            if (profile == null || baseAmount <= 0m)
                return 0m;

            decimal amount = profile.FixedAmount;
            if (profile.RatePercent > 0m)
                amount += baseAmount * profile.RatePercent / 100m;

            return RoundAmount(amount, amountDec);
        }

        static decimal RoundAmount(decimal value, int amountDec)
        {
            return Math.Round(value, amountDec, MidpointRounding.AwayFromZero);
        }

        public static void MergeDeductionTotals(
            IDictionary<long, PosDeductionBreakdownLine> target,
            IEnumerable<PosDeductionBreakdownLine> source)
        {
            if (target == null || source == null)
                return;

            foreach (PosDeductionBreakdownLine line in source)
            {
                if (line == null || line.DeductionTypeId <= 0)
                    continue;

                if (!target.TryGetValue(line.DeductionTypeId, out PosDeductionBreakdownLine existing))
                {
                    target[line.DeductionTypeId] = new PosDeductionBreakdownLine
                    {
                        DeductionTypeId = line.DeductionTypeId,
                        DeductionTypeCode = line.DeductionTypeCode,
                        DeductionTypeName = line.DeductionTypeName,
                        Amount = line.Amount
                    };
                    continue;
                }

                existing.Amount += line.Amount;
            }
        }

        public static PosPeriodDailyLine MapDeductionColumns(
            PosPeriodDailyLine daily,
            IList<PosDeductionBreakdownLine> breakdown)
        {
            if (daily == null)
                return daily;

            daily.MerchantFeeAmount = SumByCode(breakdown, MetaPosDeductionTypeHelper.CodeMerchantFee);
            daily.RewardExpenseAmount = SumByCode(breakdown, MetaPosDeductionTypeHelper.CodeRewardExpense);
            daily.ServiceCommissionAmount = SumByCode(breakdown, MetaPosDeductionTypeHelper.CodeServiceCommission);
            daily.Deductions = breakdown ?? new List<PosDeductionBreakdownLine>();
            return daily;
        }

        static decimal SumByCode(IEnumerable<PosDeductionBreakdownLine> breakdown, string code)
        {
            if (breakdown == null || string.IsNullOrWhiteSpace(code))
                return 0m;

            return breakdown
                .Where(line => string.Equals(line.DeductionTypeCode, code, StringComparison.OrdinalIgnoreCase))
                .Sum(line => line.Amount);
        }
    }
}
