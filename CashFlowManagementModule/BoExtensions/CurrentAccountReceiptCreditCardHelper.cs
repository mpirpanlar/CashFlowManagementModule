using System;
using System.Data;

namespace CashFlowManagementModule.BoExtensions
{
    public static class CurrentAccountReceiptCreditCardHelper
    {
        public const string FieldInstallmentCount = "InstallmentCount";
        public const string FieldInstalmentStartDate = "InstalmentStartDate";

        public static void EnsureCurrentAccountReceiptItemColumns(DataSet data)
        {
            if (data == null || !data.Tables.Contains("Erp_CurrentAccountReceiptItem"))
                return;

            DataTable table = data.Tables["Erp_CurrentAccountReceiptItem"];
            EnsureInt16Column(table, FieldInstallmentCount);
            EnsureDateColumn(table, FieldInstalmentStartDate);
        }

        static void EnsureInt16Column(DataTable table, string columnName)
        {
            if (table.Columns.Contains(columnName))
                return;

            table.Columns.Add(new DataColumn(columnName, typeof(short))
            {
                AllowDBNull = true,
                DefaultValue = DBNull.Value
            });
        }

        static void EnsureDateColumn(DataTable table, string columnName)
        {
            if (table.Columns.Contains(columnName))
                return;

            table.Columns.Add(new DataColumn(columnName, typeof(DateTime))
            {
                AllowDBNull = true,
                DefaultValue = DBNull.Value
            });
        }

        public static void SyncInstallmentField(DataRow row, string fieldName, object value)
        {
            if (row == null
                || row.Table == null
                || row.RowState == DataRowState.Deleted
                || row.RowState == DataRowState.Detached
                || string.IsNullOrWhiteSpace(fieldName)
                || !row.Table.Columns.Contains(fieldName))
                return;

            if (fieldName == FieldInstallmentCount)
            {
                if (TryParseInstallmentCount(value, out short installmentCount))
                    row[FieldInstallmentCount] = installmentCount;
                return;
            }

            if (fieldName == FieldInstalmentStartDate)
                row[FieldInstalmentStartDate] = value == null || value == DBNull.Value ? DBNull.Value : value;
        }

        public static void NormalizeInstallmentCount(DataRow row)
        {
            if (row == null
                || row.Table == null
                || row.RowState == DataRowState.Deleted
                || row.RowState == DataRowState.Detached
                || !row.Table.Columns.Contains(FieldInstallmentCount))
                return;

            if (row.IsNull(FieldInstallmentCount) || Convert.ToInt16(row[FieldInstallmentCount]) < 1)
                row[FieldInstallmentCount] = (short)1;
        }

        public static bool TryParseInstallmentCount(object value, out short installmentCount)
        {
            installmentCount = 0;
            if (value == null || value == DBNull.Value)
                return false;

            switch (value)
            {
                case short shortValue when shortValue >= 1:
                    installmentCount = shortValue;
                    return true;
                case int intValue when intValue >= 1 && intValue <= short.MaxValue:
                    installmentCount = (short)intValue;
                    return true;
                case byte byteValue when byteValue >= 1:
                    installmentCount = byteValue;
                    return true;
                default:
                    return short.TryParse(value.ToString(), out installmentCount) && installmentCount >= 1;
            }
        }

        /// <summary>
        /// Taksit başlangıç tarihini çözer. Elle girilen InstalmentStartDate korunur; boşsa
        /// header ReceiptDate → satır ReceiptDate → TermDate sırasıyla cascade uygulanır.
        /// </summary>
        public static DateTime? ResolveInstalmentStartDate(DataRow itemRow, DataRow headerRow)
        {
            DateTime? headerReceiptDate = TryGetRowDate(headerRow, "ReceiptDate");
            return ResolveInstalmentStartDate(itemRow, headerReceiptDate);
        }

        /// <summary>
        /// Taksit başlangıç tarihini çözer (header ReceiptDate ayrı parametre olarak).
        /// </summary>
        public static DateTime? ResolveInstalmentStartDate(DataRow itemRow, DateTime? headerReceiptDate)
        {
            if (itemRow == null)
                return null;

            DateTime? instalmentStartDate = TryGetRowDate(itemRow, FieldInstalmentStartDate);
            if (instalmentStartDate.HasValue)
                return instalmentStartDate;

            if (headerReceiptDate.HasValue)
                return headerReceiptDate;

            DateTime? itemReceiptDate = TryGetRowDate(itemRow, "ReceiptDate");
            if (itemReceiptDate.HasValue)
                return itemReceiptDate;

            return TryGetRowDate(itemRow, "TermDate");
        }

        /// <summary>
        /// InstalmentStartDate boşsa cascade ile doldurur; ardından TermDate alanını senkronlar.
        /// </summary>
        public static void EnsureInstalmentStartDate(DataRow itemRow, DataRow headerRow)
        {
            if (itemRow == null)
                return;

            EnsureDateColumn(itemRow.Table, FieldInstalmentStartDate);

            DateTime? resolvedDate = ResolveInstalmentStartDate(itemRow, headerRow);
            if (!resolvedDate.HasValue)
                return;

            if (itemRow.IsNull(FieldInstalmentStartDate))
                itemRow[FieldInstalmentStartDate] = resolvedDate.Value;

            if (itemRow.Table.Columns.Contains("TermDate"))
                itemRow["TermDate"] = resolvedDate.Value;
        }

        static DateTime? TryGetRowDate(DataRow row, string columnName)
        {
            if (row == null
                || row.Table == null
                || row.RowState == DataRowState.Deleted
                || row.RowState == DataRowState.Detached
                || string.IsNullOrWhiteSpace(columnName)
                || !row.Table.Columns.Contains(columnName)
                || row.IsNull(columnName))
                return null;

            return Convert.ToDateTime(row[columnName]).Date;
        }
    }
}
