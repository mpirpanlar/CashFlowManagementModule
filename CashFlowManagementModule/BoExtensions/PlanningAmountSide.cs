using System;
using System.Data;

namespace CashFlowManagementModule.BoExtensions
{
    public sealed class DownstreamAmountMapping
    {
        public string AmountColumn { get; set; }
        public string ForexAmountColumn { get; set; }
        public decimal Amount { get; set; }
        public decimal? ForexAmount { get; set; }
    }

    public static class PlanningAmountSide
    {
        static bool IsUsableDataRow(DataRow row)
        {
            return row != null
                && row.Table != null
                && row.RowState != DataRowState.Deleted
                && row.RowState != DataRowState.Detached;
        }

        public static string GetPlanningAmountColumn(short planningType)
        {
            return planningType == BankReceiptCollectionOrderHelper.ReceiptType ? "Debit" : "Credit";
        }

        public static string GetPlanningForexAmountColumn(short planningType)
        {
            return planningType == BankReceiptCollectionOrderHelper.ReceiptType ? "ForexDebit" : "ForexCredit";
        }

        public static void ApplyAmountToRow(DataRow row, short planningType, decimal amount, decimal? forexAmount)
        {
            if (!IsUsableDataRow(row)) return;

            string amountColumn = GetPlanningAmountColumn(planningType);
            string forexColumn = GetPlanningForexAmountColumn(planningType);
            string oppositeAmountColumn = planningType == BankReceiptCollectionOrderHelper.ReceiptType ? "Credit" : "Debit";
            string oppositeForexColumn = planningType == BankReceiptCollectionOrderHelper.ReceiptType ? "ForexCredit" : "ForexDebit";

            if (row.Table.Columns.Contains(amountColumn))
                row[amountColumn] = amount;

            if (row.Table.Columns.Contains(oppositeAmountColumn))
                row[oppositeAmountColumn] = DBNull.Value;

            if (forexAmount.HasValue)
            {
                if (row.Table.Columns.Contains(forexColumn))
                    row[forexColumn] = forexAmount.Value;

                if (row.Table.Columns.Contains(oppositeForexColumn))
                    row[oppositeForexColumn] = DBNull.Value;
            }
            else
            {
                if (row.Table.Columns.Contains(forexColumn))
                    row[forexColumn] = DBNull.Value;

                if (row.Table.Columns.Contains(oppositeForexColumn))
                    row[oppositeForexColumn] = DBNull.Value;
            }
        }

        public static decimal GetAmountFromRow(DataRow row, short planningType)
        {
            if (!IsUsableDataRow(row)) return 0m;

            string column = GetPlanningAmountColumn(planningType);
            if (!row.Table.Columns.Contains(column) || row.IsNull(column))
                return 0m;

            return Convert.ToDecimal(row[column]);
        }

        public static DownstreamAmountMapping GetAmountForDownstream(DataRow planningRow, short planningType, short targetReceiptType)
        {
            if (!IsUsableDataRow(planningRow))
                return new DownstreamAmountMapping();

            decimal amount = GetAmountFromRow(planningRow, planningType);
            decimal? forexAmount = GetForexAmountFromRow(planningRow, planningType);
            string amountColumn = ResolveDownstreamAmountColumn(planningType, targetReceiptType);

            return new DownstreamAmountMapping
            {
                AmountColumn = amountColumn,
                ForexAmountColumn = amountColumn == "Debit" ? "ForexDebit" : "ForexCredit",
                Amount = amount,
                ForexAmount = forexAmount
            };
        }

        static decimal? GetForexAmountFromRow(DataRow row, short planningType)
        {
            if (!IsUsableDataRow(row)) return null;

            string column = GetPlanningForexAmountColumn(planningType);
            if (!row.Table.Columns.Contains(column) || row.IsNull(column))
                return null;

            return Convert.ToDecimal(row[column]);
        }

        static string ResolveDownstreamAmountColumn(short planningType, short targetReceiptType)
        {
            switch (targetReceiptType)
            {
                case 50:
                    return "Credit";
                case 51:
                    return "Debit";
                case 3:
                    return "Debit";
                case 4:
                    return "Credit";
                case 1:
                    return "Debit";
                case 2:
                    return planningType == BankReceiptCollectionOrderHelper.ReceiptType ? "Credit" : "Credit";
                default:
                    return GetPlanningAmountColumn(planningType);
            }
        }
    }
}
