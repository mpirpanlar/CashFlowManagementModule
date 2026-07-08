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
    public static class CurrentAccountCollectionAgingReportDataService
    {
        static readonly string CreditBalanceColumnName = SLanguage.GetString("Alacak Bakiye");

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

            var report = container.Resolve<IReport>("CurrentAccountPaymentCollectingRpr") as ReportBase;
            if (report == null)
            {
                result.ErrorMessage = SLanguage.GetString("Alacak yaşlandırma raporu yüklenemedi.");
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
                    result.ErrorMessage = SLanguage.GetString("Alacak yaşlandırma raporu için tablo oluşturulamadı.");
                    return result;
                }

                LiveSession session = SysMng.Instance.getSession() as LiveSession;
                MergeFilteredCurrentAccounts(
                    reportTable,
                    session,
                    startCurrentAccountCode,
                    endCurrentAccountCode);

                result.Data = reportTable;
                result.AmountColumnName = ResolveCollectionAmountColumnName(reportTable);

                if (string.IsNullOrWhiteSpace(result.AmountColumnName))
                {
                    result.ErrorMessage = SLanguage.GetString("Alacak bakiye kolonu bulunamadı.");
                }
                else if (!result.HasRows)
                {
                    result.InfoMessage = SLanguage.GetString(
                        "Alacak yaşlandırma raporunda kayıt bulunamadı; filtrelenen cari hesaplar listelendi.");
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

        static void ApplyReportOptions(ReportBase report, DateTime reportDate)
        {
            SetOptionChecked(report, "IsDetail", false);
            SetCreditBalanceOption(report);
            SetReportDate(report, reportDate);
        }

        static void SetCreditBalanceOption(ReportBase report)
        {
            RepOps option = report?.FindOption("OptionsGroup2");
            if (option?.Items == null)
                return;

            int index = 0;
            foreach (ObjPair item in option.Items)
            {
                if (string.Equals(item.Value?.ToString(), "IsCreditBalance", StringComparison.OrdinalIgnoreCase))
                {
                    option.selindex = index;
                    option.selectedItem = item;
                    return;
                }

                index++;
            }
        }

        static void SetReportDate(ReportBase report, DateTime reportDate)
        {
            RepOps option = report?.FindOption("OptionsGroup8");
            if (option != null)
                option.selectedItem = reportDate;
        }

        static void SetOptionChecked(ReportBase report, string optionName, bool isChecked)
        {
            RepOps option = report?.FindOption(optionName);
            if (option != null)
                option.IsChecked = isChecked;
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

        public static string ResolveCollectionAmountColumnName(DataTable table)
        {
            if (table == null)
                return null;

            if (table.Columns.Contains(CreditBalanceColumnName))
                return CreditBalanceColumnName;

            string lastPaymentAmount = SLanguage.GetString("Son Ödeme Tutarı");
            if (table.Columns.Contains(lastPaymentAmount))
                return lastPaymentAmount;

            string balance = SLanguage.GetString("Güncel Bakiye");
            if (table.Columns.Contains(balance))
                return balance;

            return null;
        }
    }
}
