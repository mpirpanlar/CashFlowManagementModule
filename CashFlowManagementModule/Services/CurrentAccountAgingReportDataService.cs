using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

using Prism.Ioc;

using Sentez.Common;
using Sentez.Common.Report;
using Sentez.Common.SystemServices;
using Sentez.FinanceModule.Reports;
using Sentez.Localization;

namespace CashFlowManagementModule.Services
{
    public sealed class CurrentAccountAgingReportDataResult
    {
        public DataTable Data { get; set; }
        public string AmountColumnName { get; set; }
        public string ErrorMessage { get; set; }

        public bool IsSuccess => Data != null && !string.IsNullOrWhiteSpace(AmountColumnName);
    }

    public static class CurrentAccountAgingReportDataService
    {
        static readonly string[] RangeOptionNames =
        {
            "FirstRange", "SecondRange", "ThirdRange", "FourthRange", "FifthRange",
            "SixthRange", "SeventhRange", "EighthRange", "NinthRange", "TenthRange"
        };

        public static CurrentAccountAgingReportDataResult LoadAgingData(
            IContainerExtension container,
            DateTime receiptDate)
        {
            var result = new CurrentAccountAgingReportDataResult();

            if (container == null)
            {
                result.ErrorMessage = SLanguage.GetString("Modül bağlantısı bulunamadı.");
                return result;
            }

            if (receiptDate == DateTime.MinValue)
            {
                result.ErrorMessage = SLanguage.GetString("Fiş tarihi geçersiz.");
                return result;
            }

            var report = container.Resolve<IReport>("CurrentAccountDebitDistributionList") as ReportBase;
            if (report == null)
            {
                result.ErrorMessage = SLanguage.GetString("Yaşlandırma raporu yüklenemedi.");
                return result;
            }

            try
            {
                report.PolicyParam = new PolicyParams { FieldName = "AgingPaymentOrderImport" };
                ApplyReportOptions(report, receiptDate.Date);
                report.Init();
                report.CreateDataset();

                if (report.Data?.Tables == null || report.Data.Tables.Count == 0 || report.Data.Tables[0].Rows.Count == 0)
                {
                    result.ErrorMessage = SLanguage.GetString("Yaşlandırma raporu için kayıt bulunamadı.");
                    return result;
                }

                string amountColumnName = ResolveAgingAmountColumnName(receiptDate, report.Data.Tables[0]);
                if (string.IsNullOrWhiteSpace(amountColumnName))
                {
                    result.ErrorMessage = SLanguage.GetString("Fiş ayına karşılık gelen yaşlandırma kolonu bulunamadı.");
                    return result;
                }

                result.Data = report.Data.Tables[0];
                result.AmountColumnName = amountColumnName;
                return result;
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
                return result;
            }
        }

        static void ApplyReportOptions(ReportBase report, DateTime receiptDate)
        {
            SetOptionChecked(report, "IsForexCorrection", true);
            SetForexOnlyTl(report);
            SetReportDate(report, receiptDate);

            foreach (string optionName in RangeOptionNames)
                SetOptionChecked(report, optionName, true);
        }

        static void SetOptionChecked(ReportBase report, string optionName, bool isChecked)
        {
            RepOps option = report?.FindOption(optionName);
            if (option != null)
                option.IsChecked = isChecked;
        }

        static void SetReportDate(ReportBase report, DateTime receiptDate)
        {
            RepOps option = report?.FindOption("OptionsGroup2");
            if (option != null)
                option.selectedItem = receiptDate;
        }

        static void SetForexOnlyTl(ReportBase report)
        {
            RepOps option = report?.FindOption("OptionsGroup3");
            if (option?.Items == null)
                return;

            int index = 0;
            foreach (ObjPair item in option.Items)
            {
                if (string.Equals(item.Value?.ToString(), "IsOnlyTL", StringComparison.OrdinalIgnoreCase))
                {
                    option.selindex = index;
                    option.selectedItem = item;
                    return;
                }

                index++;
            }
        }

        public static string ResolveAgingAmountColumnName(DateTime receiptDate, DataTable table)
        {
            if (table == null)
                return null;

            IList<string> amountColumns = GetAmountColumnNames();
            if (amountColumns.Count == 0)
                amountColumns = GetAmountColumnsFromTable(table);

            if (amountColumns.Count == 0)
                return null;

            int bucketIndex = Math.Min(Math.Max(receiptDate.Month - 1, 0), amountColumns.Count - 1);
            string columnName = amountColumns[bucketIndex];
            return table.Columns.Contains(columnName) ? columnName : null;
        }

        static IList<string> GetAmountColumnNames()
        {
            var columns = new List<string>();
            if (CurrentAccountDebitDistributionList.colonList == null)
                return columns;

            foreach (KeyValuePair<string, string> item in CurrentAccountDebitDistributionList.colonList)
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
