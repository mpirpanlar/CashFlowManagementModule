using System;
using System.Collections.Generic;

namespace CashFlowManagementModule.Services
{
    public sealed class PosDeductionProfileLine
    {
        public long RecId { get; set; }
        public long DeductionTypeId { get; set; }
        public string DeductionTypeCode { get; set; }
        public string DeductionTypeName { get; set; }
        public decimal RatePercent { get; set; }
        public decimal FixedAmount { get; set; }
        public byte CalculationBase { get; set; }
        public short? CardCategory { get; set; }
        public short? InstallmentCount { get; set; }
        public short? BlockDays { get; set; }
    }

    public sealed class PosDeductionBreakdownLine
    {
        public long DeductionTypeId { get; set; }
        public string DeductionTypeCode { get; set; }
        public string DeductionTypeName { get; set; }
        public decimal Amount { get; set; }
    }

    public sealed class PosMerchantMovementLine
    {
        public long CurrentAccountReceiptItemId { get; set; }
        public short ReceiptType { get; set; }
        public DateTime ReceiptDate { get; set; }
        public DateTime SettlementDate { get; set; }
        public decimal GrossAmount { get; set; }
        public decimal RefundAmount { get; set; }
        public decimal DeductionAmount { get; set; }
        public decimal NetAmount { get; set; }
        public short InstallmentCount { get; set; }
        public short InstallmentNo { get; set; }
        public short? CardCategory { get; set; }
        public short? CardSource { get; set; }
        public string ReceiptNo { get; set; }
        public string Explanation { get; set; }
    }

    public sealed class PosPeriodDailyLine
    {
        public DateTime Day { get; set; }
        public int CollectionCount { get; set; }
        public int RefundCount { get; set; }
        public decimal GrossAmount { get; set; }
        public decimal MerchantFeeAmount { get; set; }
        public decimal RewardExpenseAmount { get; set; }
        public decimal ServiceCommissionAmount { get; set; }
        public decimal RefundAmount { get; set; }
        public decimal NetAmount { get; set; }
        public IList<PosDeductionBreakdownLine> Deductions { get; set; } = new List<PosDeductionBreakdownLine>();
    }

    public sealed class PosPeriodSettlementLine
    {
        public DateTime SettlementDate { get; set; }
        public byte SettlementKind { get; set; }
        public decimal GrossAmount { get; set; }
        public decimal DeductionAmount { get; set; }
        public decimal RefundAmount { get; set; }
        public decimal NetAmount { get; set; }
    }

    public sealed class PosPeriodFutureReceivableLine
    {
        public int PeriodYear { get; set; }
        public int PeriodMonth { get; set; }
        public decimal GrossAmount { get; set; }
        public decimal NetAmount { get; set; }
    }

    public sealed class PosPeriodSummaryKpi
    {
        public decimal TotalGross { get; set; }
        public decimal TotalDeduction { get; set; }
        public decimal TotalRefund { get; set; }
        public decimal TotalNet { get; set; }
        public decimal CurrentMonthSettlementNet { get; set; }
        public decimal NextMonthReceivableNet { get; set; }
        public decimal FutureReceivableNet { get; set; }
        public bool HasDeductionProfile { get; set; }
        public string WarningMessage { get; set; }
    }

    public sealed class PosMerchantAggregationResult
    {
        public long BankAccountId { get; set; }
        public int PeriodYear { get; set; }
        public int PeriodMonth { get; set; }
        public PosPeriodSummaryKpi Summary { get; set; } = new PosPeriodSummaryKpi();
        public IList<PosPeriodDailyLine> DailyLines { get; set; } = new List<PosPeriodDailyLine>();
        public IList<PosPeriodSettlementLine> SettlementLines { get; set; } = new List<PosPeriodSettlementLine>();
        public IList<PosPeriodFutureReceivableLine> FutureReceivableLines { get; set; } = new List<PosPeriodFutureReceivableLine>();
        public IList<PosDeductionBreakdownLine> PeriodDeductions { get; set; } = new List<PosDeductionBreakdownLine>();
        public IList<PosMerchantMovementLine> MovementLines { get; set; } = new List<PosMerchantMovementLine>();
    }
}
