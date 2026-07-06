using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;

using Prism.Ioc;

using Sentez.Common;
using Sentez.Common.Commands;
using Sentez.Common.Report;
using Sentez.Common.SqlBuilder;
using Sentez.Common.SystemServices;
using Sentez.Data.Tools;
using Sentez.FinanceModule.Reports;
using Sentez.Localization;

namespace CashFlowManagementModule.Services
{
    public sealed class CurrentAccountAgingReportDataResult
    {
        public DataTable Data { get; set; }
        public string AmountColumnName { get; set; }
        public string ErrorMessage { get; set; }
        public string InfoMessage { get; set; }
        public bool HasRows => Data?.Rows.Count > 0;

        public bool HasData => Data != null;

        public bool IsSuccess => HasData && !string.IsNullOrWhiteSpace(AmountColumnName);
    }

    public static class CurrentAccountAgingReportDataService
    {
        public static CurrentAccountAgingReportDataResult LoadAgingData(
            IContainerExtension container,
            DateTime reportDate)
        {
            return LoadAgingData(container, reportDate, null, null);
        }

        public static CurrentAccountAgingReportDataResult LoadAgingData(
            IContainerExtension container,
            DateTime reportDate,
            string startCurrentAccountCode,
            string endCurrentAccountCode)
        {
            var result = new CurrentAccountAgingReportDataResult();

            if (container == null)
            {
                result.ErrorMessage = SLanguage.GetString("Modül bağlantısı bulunamadı.");
                return result;
            }

            if (reportDate == DateTime.MinValue)
            {
                result.ErrorMessage = SLanguage.GetString("Rapor tarihi geçersiz.");
                return result;
            }

            var report = container.Resolve<IReport>("CurrentAccountDebitDistributionRpr") as ReportBase;
            if (report == null)
            {
                result.ErrorMessage = SLanguage.GetString("Yaşlandırma raporu yüklenemedi.");
                return result;
            }

            try
            {
                string whereStr = BuildCurrentAccountWhereClause(startCurrentAccountCode, endCurrentAccountCode);
                report.PolicyParam = new PolicyParams
                {
                    FieldName = "Aging",
                    WhereStr = whereStr
                };
                report.WorkMode = ReportWorkMode.Report;

                report.Init();
                ApplyReportOptions(report, reportDate.Date);
                ApplyCurrentAccountCodeFilter(report, startCurrentAccountCode, endCurrentAccountCode);

                report.PolicyParam = new PolicyParams
                {
                    FieldName = "Aging",
                    WhereStr = whereStr
                };
                report.Init();
                ApplyReportOptions(report, reportDate.Date);
                ApplyCurrentAccountCodeFilter(report, startCurrentAccountCode, endCurrentAccountCode);
                report.CreateDataset();

                DataTable reportTable = GetReportTable(report);
                if (reportTable == null)
                {
                    result.ErrorMessage = SLanguage.GetString("Yaşlandırma raporu için tablo oluşturulamadı.");
                    return result;
                }

                LiveSession session = SysMng.Instance.getSession() as LiveSession;
                MergeFilteredCurrentAccounts(
                    reportTable,
                    session,
                    startCurrentAccountCode,
                    endCurrentAccountCode);

                result.Data = reportTable;
                result.AmountColumnName = ResolveAgingAmountColumnName(reportTable);

                if (string.IsNullOrWhiteSpace(result.AmountColumnName))
                {
                    result.ErrorMessage = SLanguage.GetString(
                        "<= 0 yaşlandırma kolonu bulunamadı.");
                }
                else if (!result.HasRows)
                {
                    result.InfoMessage = SLanguage.GetString(
                        "Yaşlandırma raporunda kayıt bulunamadı; filtrelenen cari hesaplar listelendi.");
                }

                return result;
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
                return result;
            }
        }

        static DataTable GetReportTable(ReportBase report)
        {
            if (report?.Data?.Tables == null || report.Data.Tables.Count == 0)
                return null;

            return report.Data.Tables[0];
        }

        static string BuildCurrentAccountWhereClause(string startCurrentAccountCode, string endCurrentAccountCode)
        {
            string startCode = startCurrentAccountCode?.Trim();
            string endCode = endCurrentAccountCode?.Trim();
            if (string.IsNullOrWhiteSpace(startCode) && string.IsNullOrWhiteSpace(endCode))
                return null;

            var where = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(startCode))
                where.Append($"C.CurrentAccountCode >= '{EscapeSql(startCode)}'");

            if (!string.IsNullOrWhiteSpace(endCode))
            {
                if (where.Length > 0)
                    where.Append(" and ");

                where.Append($"C.CurrentAccountCode <= '{EscapeSql(endCode)}'");
            }

            return where.ToString();
        }

        static string EscapeSql(string value)
        {
            return (value ?? string.Empty).Replace("'", "''");
        }

        static void MergeFilteredCurrentAccounts(
            DataTable reportTable,
            LiveSession session,
            string startCurrentAccountCode,
            string endCurrentAccountCode)
        {
            if (reportTable == null || session?.ActiveCompany?.RecId == null)
                return;

            string startCode = startCurrentAccountCode?.Trim();
            string endCode = endCurrentAccountCode?.Trim();
            if (string.IsNullOrWhiteSpace(startCode) && string.IsNullOrWhiteSpace(endCode))
                return;

            DataTable accountsTable = LoadAccountsInRange(session, startCode, endCode);
            if (accountsTable == null || accountsTable.Rows.Count == 0)
                return;

            string codeColumnName = SLanguage.GetString("Cari Hesap Kodu");
            string nameColumnName = SLanguage.GetString("Cari Hesap Adı");
            EnsureColumn(reportTable, "CurrentAccountId", typeof(long));
            EnsureColumn(reportTable, "RecId", typeof(long));
            EnsureColumn(reportTable, codeColumnName, typeof(string));
            EnsureColumn(reportTable, nameColumnName, typeof(string));

            foreach (DataRow accountRow in accountsTable.Rows)
            {
                if (accountRow.RowState == DataRowState.Deleted)
                    continue;

                long currentAccountId = Convert.ToInt64(accountRow["RecId"]);
                string accountCode = Convert.ToString(accountRow["CurrentAccountCode"]) ?? string.Empty;
                if (TryFindReportRow(reportTable, currentAccountId, accountCode, codeColumnName, out DataRow existingRow))
                {
                    FillMissingIdentityColumns(existingRow, currentAccountId, accountCode,
                        Convert.ToString(accountRow["CurrentAccountName"]) ?? string.Empty,
                        codeColumnName, nameColumnName);
                    continue;
                }

                DataRow newRow = reportTable.NewRow();
                if (reportTable.Columns.Contains("CurrentAccountId"))
                    newRow["CurrentAccountId"] = currentAccountId;
                if (reportTable.Columns.Contains("RecId"))
                    newRow["RecId"] = currentAccountId;
                if (reportTable.Columns.Contains(codeColumnName))
                    newRow[codeColumnName] = accountCode;
                if (reportTable.Columns.Contains(nameColumnName))
                    newRow[nameColumnName] = accountRow["CurrentAccountName"];

                reportTable.Rows.Add(newRow);
            }
        }

        static DataTable LoadAccountsInRange(LiveSession session, string startCode, string endCode)
        {
            var sql = new StringBuilder();
            sql.AppendLine("select RecId, isnull(CurrentAccountCode,'') CurrentAccountCode, isnull(CurrentAccountName,'') CurrentAccountName");
            sql.AppendLine("from Erp_CurrentAccount with (nolock)");
            sql.AppendLine("where isnull(IsDeleted,0)=0");
            sql.AppendLine($"and CompanyId = {session.ActiveCompany.RecId.Value}");

            if (!string.IsNullOrWhiteSpace(startCode))
                sql.AppendLine($"and CurrentAccountCode >= '{EscapeSql(startCode)}'");
            if (!string.IsNullOrWhiteSpace(endCode))
                sql.AppendLine($"and CurrentAccountCode <= '{EscapeSql(endCode)}'");

            sql.AppendLine("order by CurrentAccountCode");

            return UtilityFunctions.GetDataTableList(
                session._dbInfo.DBProvider,
                session._dbInfo.Connection,
                null,
                "Erp_CurrentAccount",
                sql.ToString());
        }

        static bool TryFindReportRow(
            DataTable reportTable,
            long currentAccountId,
            string accountCode,
            string codeColumnName,
            out DataRow itemRow)
        {
            itemRow = null;
            foreach (DataRow row in reportTable.Rows)
            {
                if (row.RowState == DataRowState.Deleted)
                    continue;

                if (reportTable.Columns.Contains("CurrentAccountId")
                    && !row.IsNull("CurrentAccountId")
                    && Convert.ToInt64(row["CurrentAccountId"]) == currentAccountId)
                {
                    itemRow = row;
                    return true;
                }

                if (reportTable.Columns.Contains("RecId")
                    && !row.IsNull("RecId")
                    && Convert.ToInt64(row["RecId"]) == currentAccountId)
                {
                    itemRow = row;
                    return true;
                }

                if (reportTable.Columns.Contains(codeColumnName)
                    && !row.IsNull(codeColumnName)
                    && string.Equals(
                        Convert.ToString(row[codeColumnName]),
                        accountCode,
                        StringComparison.OrdinalIgnoreCase))
                {
                    itemRow = row;
                    return true;
                }
            }

            return false;
        }

        static void FillMissingIdentityColumns(
            DataRow row,
            long currentAccountId,
            string accountCode,
            string accountName,
            string codeColumnName,
            string nameColumnName)
        {
            if (row.Table.Columns.Contains("CurrentAccountId") && row.IsNull("CurrentAccountId"))
                row["CurrentAccountId"] = currentAccountId;
            if (row.Table.Columns.Contains("RecId") && row.IsNull("RecId"))
                row["RecId"] = currentAccountId;
            if (row.Table.Columns.Contains(codeColumnName) && row.IsNull(codeColumnName))
                row[codeColumnName] = accountCode;
            if (row.Table.Columns.Contains(nameColumnName) && row.IsNull(nameColumnName))
                row[nameColumnName] = accountName;
        }

        static void EnsureColumn(DataTable table, string columnName, Type dataType)
        {
            if (table == null || table.Columns.Contains(columnName))
                return;

            table.Columns.Add(columnName, dataType);
        }

        static void ApplyReportOptions(ReportBase report, DateTime reportDate)
        {
            SetOptionChecked(report, "IsForexCorrection", true);
            SetOptionChecked(report, "IsGsmPhone", true);
            SetForexCurrentBalance(report);
            SetReportDate(report, reportDate);
        }

        static void ApplyCurrentAccountCodeFilter(
            ReportBase report,
            string startCurrentAccountCode,
            string endCurrentAccountCode)
        {
            if (report?.statementList == null || report.statementList.Count == 0)
                return;

            Statement statement = report.statementList[0];
            if (statement?.filterList == null)
                return;

            string startCode = startCurrentAccountCode?.Trim();
            string endCode = endCurrentAccountCode?.Trim();
            if (string.IsNullOrWhiteSpace(startCode) && string.IsNullOrWhiteSpace(endCode))
                return;

            FilterItem filterItem = statement.filterList
                .FirstOrDefault(f => f.field1Name == "CurrentAccountCode"
                                     && (f.filterTable1Name == "Erp_CurrentAccount"
                                         || f.filterTable1Alias == "C"));
            if (filterItem?.valueList == null || filterItem.valueList.Count < 2)
                return;

            if (!string.IsNullOrWhiteSpace(startCode))
                filterItem.valueList[0] = startCode;
            if (!string.IsNullOrWhiteSpace(endCode))
                filterItem.valueList[1] = endCode;
        }

        static void SetOptionChecked(ReportBase report, string optionName, bool isChecked)
        {
            RepOps option = report?.FindOption(optionName);
            if (option != null)
                option.IsChecked = isChecked;
        }

        static void SetReportDate(ReportBase report, DateTime reportDate)
        {
            RepOps option = report?.FindOption("OptionsGroup2");
            if (option != null)
                option.selectedItem = reportDate;
        }

        static void SetForexCurrentBalance(ReportBase report)
        {
            RepOps option = report?.FindOption("OptionsGroup3");
            if (option?.Items == null)
                return;

            int index = 0;
            foreach (ObjPair item in option.Items)
            {
                if (string.Equals(item.Value?.ToString(), "IsCurrentBalance", StringComparison.OrdinalIgnoreCase))
                {
                    option.selindex = index;
                    option.selectedItem = item;
                    return;
                }

                index++;
            }
        }

        public static string ResolveAgingAmountColumnName(DataTable table)
        {
            if (table == null)
                return null;

            string zeroBucketColumn = FindZeroBucketColumnName(table);
            if (!string.IsNullOrWhiteSpace(zeroBucketColumn))
                return zeroBucketColumn;

            IList<string> amountColumns = GetAmountColumnNames();
            if (amountColumns.Count == 0)
                amountColumns = GetAmountColumnsFromTable(table);

            if (amountColumns.Count == 0)
                return null;

            string firstBucketColumn = amountColumns[0];
            return table.Columns.Contains(firstBucketColumn) ? firstBucketColumn : null;
        }

        static string FindZeroBucketColumnName(DataTable table)
        {
            if (table == null)
                return null;

            foreach (DataColumn column in table.Columns)
            {
                if (IsZeroBucketColumnName(column.ColumnName))
                    return column.ColumnName;
            }

            IList<string> amountColumns = GetAmountColumnNames();
            foreach (string columnName in amountColumns)
            {
                if (IsZeroBucketColumnName(columnName) && table.Columns.Contains(columnName))
                    return columnName;
            }

            return null;
        }

        static bool IsZeroBucketColumnName(string columnName)
        {
            if (string.IsNullOrWhiteSpace(columnName))
                return false;

            string normalized = columnName.Trim();
            return normalized.StartsWith("<=", StringComparison.Ordinal)
                   && normalized.IndexOf("0", StringComparison.Ordinal) >= 0;
        }

        static IList<string> GetAmountColumnNames()
        {
            var columns = new List<string>();
            if (CurrentAccountDebitDistributionRpr.colonList == null)
                return columns;

            foreach (KeyValuePair<string, string> item in CurrentAccountDebitDistributionRpr.colonList)
            {
                if (!string.IsNullOrWhiteSpace(item.Value))
                    columns.Add(item.Value);
            }

            return columns;
        }

        static IList<string> GetAmountColumnsFromTable(DataTable table)
        {
            var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "CurrentAccountId",
                "RecId",
                SLanguage.GetString("Cari Hesap Kodu"),
                SLanguage.GetString("Cari Hesap Adı"),
                SLanguage.GetString("Cari Hesap"),
                SLanguage.GetString("Ticari Ünvan"),
                SLanguage.GetString("Özel Kod"),
                SLanguage.GetString("Erişim Kodu"),
                SLanguage.GetString("Grup Kodu"),
                SLanguage.GetString("Grup Adı"),
                SLanguage.GetString("Cep Telefonu"),
                SLanguage.GetString("Adres Telefon"),
                SLanguage.GetString("Adres Faks"),
                SLanguage.GetString("Adres Bilgisi"),
                SLanguage.GetString("Iban No"),
                SLanguage.GetString("Müşteri Hesap Adı"),
                SLanguage.GetString("Açıklama"),
                SLanguage.GetString("Vergi Kimlik No"),
                SLanguage.GetString("T.C. Kimlik No"),
                SLanguage.GetString("Vade Günü"),
                SLanguage.GetString("Vade Faiz Oranı"),
                SLanguage.GetString("Bakiye"),
                SLanguage.GetString("Güncel Bakiye"),
                SLanguage.GetString("BT"),
                SLanguage.GetString("Döviz"),
                SLanguage.GetString("Cari Döviz")
            };

            return table.Columns.Cast<DataColumn>()
                .Select(c => c.ColumnName)
                .Where(name => !excluded.Contains(name))
                .ToList();
        }
    }
}
