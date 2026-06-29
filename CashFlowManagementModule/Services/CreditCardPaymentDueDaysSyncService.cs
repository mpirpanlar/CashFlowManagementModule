using CashFlowManagementModule.BoExtensions;

using Sentez.Localization;

using System;
using System.Data;

namespace CashFlowManagementModule.Services
{
    public static class CreditCardPaymentDueDaysSyncService
    {
        const int MinPaymentDueDays = 1;

        static bool _isSyncing;

        public static bool IsSyncing => _isSyncing;

        public static void EnsureVirtualColumns(DataSet data)
        {
            if (data == null)
                return;

            BankAccountCreditCardHelper.EnsureBankAccountDataColumns(data);
            EnsurePaymentDueDaysColumn(data, "Erp_BankAccount");
            EnsurePaymentDueDaysColumn(data, BankAccountCreditCardHelper.PeriodTableName);
            EnsureAmountColumn(data, BankAccountCreditCardHelper.PeriodTableName, BankAccountCreditCardHelper.FieldPeriodSpendingTotal, SLanguage.GetString("Harcama Toplamı"));
            EnsureAmountColumn(data, BankAccountCreditCardHelper.PeriodTableName, BankAccountCreditCardHelper.FieldPeriodRefundTotal, SLanguage.GetString("İade Toplamı"));
            EnsureAmountColumn(data, BankAccountCreditCardHelper.PeriodTableName, BankAccountCreditCardHelper.FieldPeriodCardPaymentTotal, SLanguage.GetString("Ödeme Toplamı"));
            EnsureAmountColumn(data, BankAccountCreditCardHelper.PeriodTableName, BankAccountCreditCardHelper.FieldPeriodTotalCreditLimit, SLanguage.GetString("Toplam Limit"));
            EnsureAmountColumn(data, BankAccountCreditCardHelper.PeriodTableName, BankAccountCreditCardHelper.FieldPeriodRemainingCreditLimit, SLanguage.GetString("Kalan Limit"));
            EnsureAmountColumn(data, "Erp_BankAccount", BankAccountCreditCardHelper.FieldUsedCreditLimit, SLanguage.GetString("Kullanılan Limit"));
            EnsureAmountColumn(data, "Erp_BankAccount", BankAccountCreditCardHelper.FieldRemainingCreditLimit, SLanguage.GetString("Kalan Limit"));
        }

        static void EnsurePaymentDueDaysColumn(DataSet data, string tableName)
        {
            if (!data.Tables.Contains(tableName))
                return;

            var table = data.Tables[tableName];
            if (table.Columns.Contains(BankAccountCreditCardHelper.FieldPaymentDueDays))
                return;

            var column = new DataColumn(BankAccountCreditCardHelper.FieldPaymentDueDays, typeof(short))
            {
                Caption = SLanguage.GetString("Gün"),
                DefaultValue = DBNull.Value
            };
            table.Columns.Add(column);
        }

        static void EnsureAmountColumn(DataSet data, string tableName, string columnName, string caption)
        {
            if (!data.Tables.Contains(tableName))
                return;

            var table = data.Tables[tableName];
            if (table.Columns.Contains(columnName))
                return;

            var column = new DataColumn(columnName, typeof(decimal))
            {
                Caption = caption,
                DefaultValue = DBNull.Value
            };
            table.Columns.Add(column);
        }

        public static void RecalculateHeaderDays(DataRow bankAccountRow)
        {
            if (bankAccountRow == null)
                return;

            SetDaysFromDates(bankAccountRow,
                BankAccountCreditCardHelper.FieldStatementCutDate,
                BankAccountCreditCardHelper.FieldPaymentDueDate);
        }

        public static void RecalculatePeriodRowDays(DataRow periodRow)
        {
            if (periodRow == null || periodRow.RowState == DataRowState.Deleted)
                return;

            SetDaysFromDates(periodRow, "StatementDate", "PaymentDueDate");
        }

        public static void RecalculateAllPeriodRows(DataTable periodTable)
        {
            if (periodTable == null)
                return;

            foreach (DataRow row in periodTable.Rows)
            {
                if (row.RowState == DataRowState.Deleted)
                    continue;
                if (!row.IsNull("IsDeleted") && Convert.ToBoolean(row["IsDeleted"]))
                    continue;

                RecalculatePeriodRowDays(row);
            }
        }

        public static void SyncHeaderOnColumnChange(DataRow row, string columnName)
        {
            SyncOnColumnChange(row, columnName,
                BankAccountCreditCardHelper.FieldStatementCutDate,
                BankAccountCreditCardHelper.FieldPaymentDueDate);
        }

        public static void SyncPeriodOnColumnChange(DataRow row, string columnName)
        {
            SyncOnColumnChange(row, columnName, "StatementDate", "PaymentDueDate");
        }

        public static string ValidateHeaderColumnChange(DataRow row, string columnName, object proposedValue)
        {
            return ValidateColumnChange(row, columnName, proposedValue,
                BankAccountCreditCardHelper.FieldStatementCutDate,
                BankAccountCreditCardHelper.FieldPaymentDueDate);
        }

        public static string ValidatePeriodColumnChange(DataRow row, string columnName, object proposedValue)
        {
            return ValidateColumnChange(row, columnName, proposedValue, "StatementDate", "PaymentDueDate");
        }

        public static string ValidateColumnChange(DataRow row, string columnName, object proposedValue, string cutField, string dueField)
        {
            if (row == null || proposedValue == null || proposedValue == DBNull.Value)
                return null;

            if (columnName == cutField)
            {
                if (row.IsNull(dueField))
                    return null;

                var proposedCut = Convert.ToDateTime(proposedValue).Date;
                var dueDate = Convert.ToDateTime(row[dueField]).Date;
                if (proposedCut >= dueDate)
                    return SLanguage.GetString("Hesap kesim tarihi son ödeme tarihinden küçük olmalıdır.");

                return null;
            }

            if (columnName == dueField)
            {
                if (row.IsNull(cutField))
                    return null;

                var cutDate = Convert.ToDateTime(row[cutField]).Date;
                var proposedDue = Convert.ToDateTime(proposedValue).Date;
                if (cutDate >= proposedDue)
                    return SLanguage.GetString("Son ödeme tarihi hesap kesim tarihinden büyük olmalıdır.");

                return null;
            }

            if (columnName == BankAccountCreditCardHelper.FieldPaymentDueDays)
            {
                if (row.IsNull(cutField))
                    return null;

                var cutDate = Convert.ToDateTime(row[cutField]).Date;
                var days = Convert.ToInt32(proposedValue);
                if (days < MinPaymentDueDays)
                    return string.Format(SLanguage.GetString("Gün değeri en az {0} olmalıdır."), MinPaymentDueDays);

                var proposedDue = cutDate.AddDays(days);
                if (cutDate >= proposedDue)
                    return SLanguage.GetString("Son ödeme tarihi hesap kesim tarihinden büyük olmalıdır.");
            }

            return null;
        }

        static void SetDaysFromDates(DataRow row, string cutField, string dueField)
        {
            if (_isSyncing)
                return;

            try
            {
                _isSyncing = true;
                SetDaysFromDatesInternal(row, cutField, dueField);
            }
            finally
            {
                _isSyncing = false;
            }
        }

        static void SyncOnColumnChange(DataRow row, string columnName, string cutField, string dueField)
        {
            if (_isSyncing || row == null)
                return;

            if (columnName != BankAccountCreditCardHelper.FieldPaymentDueDays &&
                columnName != cutField &&
                columnName != dueField)
                return;

            try
            {
                _isSyncing = true;

                if (columnName == BankAccountCreditCardHelper.FieldPaymentDueDays)
                {
                    if (row.IsNull(cutField) || row.IsNull(BankAccountCreditCardHelper.FieldPaymentDueDays))
                        return;

                    var cutDate = Convert.ToDateTime(row[cutField]).Date;
                    var days = Convert.ToInt32(row[BankAccountCreditCardHelper.FieldPaymentDueDays]);
                    if (days < MinPaymentDueDays)
                        days = MinPaymentDueDays;

                    SetCellValue(row, dueField, cutDate.AddDays(days));
                    return;
                }

                if (columnName == cutField)
                {
                    if (row.IsNull(cutField))
                        return;

                    if (!row.IsNull(dueField))
                    {
                        SetDaysFromDatesInternal(row, cutField, dueField);
                    }
                    else if (!row.IsNull(BankAccountCreditCardHelper.FieldPaymentDueDays))
                    {
                        var cutDate = Convert.ToDateTime(row[cutField]).Date;
                        var days = Convert.ToInt32(row[BankAccountCreditCardHelper.FieldPaymentDueDays]);
                        if (days < MinPaymentDueDays)
                            days = MinPaymentDueDays;

                        SetCellValue(row, dueField, cutDate.AddDays(days));
                    }

                    return;
                }

                if (columnName == dueField)
                    SetDaysFromDatesInternal(row, cutField, dueField);
            }
            finally
            {
                _isSyncing = false;
            }
        }

        static void SetCellValue(DataRow row, string columnName, object value)
        {
            if (row == null || string.IsNullOrEmpty(columnName) || !row.Table.Columns.Contains(columnName))
                return;

            var currentValue = row[columnName];
            if (currentValue == DBNull.Value)
                currentValue = null;
            if (value == DBNull.Value)
                value = null;

            if (Equals(currentValue, value))
                return;

            if (!_isSyncing)
            {
                row.BeginEdit();
                row[columnName] = value ?? DBNull.Value;
                row.EndEdit();
                return;
            }

            row[columnName] = value ?? DBNull.Value;
        }

        static void SetDaysFromDatesInternal(DataRow row, string cutField, string dueField)
        {
            if (!row.Table.Columns.Contains(BankAccountCreditCardHelper.FieldPaymentDueDays))
                return;

            if (row.IsNull(cutField) || row.IsNull(dueField))
            {
                SetCellValue(row, BankAccountCreditCardHelper.FieldPaymentDueDays, DBNull.Value);
                return;
            }

            var cutDate = Convert.ToDateTime(row[cutField]).Date;
            var dueDate = Convert.ToDateTime(row[dueField]).Date;
            if (cutDate >= dueDate)
            {
                SetCellValue(row, BankAccountCreditCardHelper.FieldPaymentDueDays, DBNull.Value);
                return;
            }

            var days = (dueDate - cutDate).Days;
            if (days < MinPaymentDueDays)
                days = MinPaymentDueDays;

            SetCellValue(row, BankAccountCreditCardHelper.FieldPaymentDueDays, (short)days);
        }
    }
}
