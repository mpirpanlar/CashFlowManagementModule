using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;

using ClosedXML.Excel;

using CashFlowManagementModule.BoExtensions;

using Sentez.Common.SystemServices;
using Sentez.Data.BusinessObjects;
using Sentez.Data.Tools;
using Sentez.Localization;

namespace CashFlowManagementModule.Services
{
    public sealed class PaymentOrderInstructionExportResult
    {
        public bool Succeeded { get; set; }
        public string Message { get; set; }
        public string FilePath { get; set; }
    }

    public static class PaymentOrderInstructionExportService
    {
        const string TemplateResourceName = "CashFlowManagementModule.Resources.TalimatSablon.xlsx";
        const int DateRow = 9;
        const int DateColumn = 5;
        const int BankNameRow = 10;
        const int BranchNameRow = 11;
        const int SourceIbanRow = 14;
        const int DetailStartRow = 25;
        const int MinDetailRows = 7;
        const int FixedSumEndRow = 32;
        const int FixedTotalRow = 33;
        const int DetailColumnCount = 5;

        public static PaymentOrderInstructionExportResult Export(
            BusinessObjectBase businessObject,
            LiveSession session,
            long defaultBankAccountId,
            IReadOnlyList<DataRow> selectedRows,
            string targetFilePath)
        {
            var result = new PaymentOrderInstructionExportResult();

            if (businessObject?.CurrentRow?.Row == null || businessObject.Data?.Tables == null)
            {
                result.Message = SLanguage.GetString("Fiş bilgisi bulunamadı.");
                return result;
            }

            if (defaultBankAccountId <= 0)
            {
                result.Message = SLanguage.GetString("Lütfen ön değer banka hesabını seçiniz.");
                return result;
            }

            if (string.IsNullOrWhiteSpace(targetFilePath))
            {
                result.Message = SLanguage.GetString("Kayıt dosyası seçilmedi.");
                return result;
            }

            List<DataRow> exportRows = NormalizeExportRows(selectedRows);
            if (exportRows.Count == 0)
            {
                result.Message = SLanguage.GetString("Talimat oluşturmak için en az bir geçerli satır seçilmelidir.");
                return result;
            }

            DataRow bankRow = BankAccountDefaultResolver.LoadBankAccountInstructionRow(session, defaultBankAccountId);
            if (bankRow == null)
            {
                result.Message = SLanguage.GetString("Ön değer banka hesabı bilgileri okunamadı.");
                return result;
            }

            if (!BankAccountDefaultResolver.TryResolveSourceIbanFromExportRows(session, exportRows, out string sourceIbanNo))
            {
                result.Message = SLanguage.GetString("Seçili satırlarda banka hesabı bulunamadı.");
                return result;
            }

            Dictionary<long, CurrentAccountInstructionInfo> currentAccountInfo =
                LoadCurrentAccountInstructionInfo(session, exportRows);

            string tempTemplatePath = ExtractTemplateToTempFile();
            try
            {
                using (var workbook = new XLWorkbook(tempTemplatePath))
                {
                    IXLWorksheet worksheet = workbook.Worksheet(1);

                    WriteHeaderFields(worksheet, bankRow, sourceIbanNo);

                    int rowCount = exportRows.Count;
                    if (rowCount > MinDetailRows)
                        worksheet.Row(MinDetailRows + DetailStartRow - 1).InsertRowsBelow(rowCount - MinDetailRows);

                    int lastDetailRow = rowCount <= MinDetailRows
                        ? FixedSumEndRow
                        : DetailStartRow + rowCount - 1;
                    int totalRow = rowCount <= MinDetailRows
                        ? FixedTotalRow
                        : FixedTotalRow + (rowCount - MinDetailRows);

                    ClearDetailArea(worksheet, DetailStartRow, totalRow);

                    int excelRow = DetailStartRow;
                    foreach (DataRow itemRow in exportRows)
                    {
                        decimal amount = PlanningAmountSide.GetAmountFromRow(
                            itemRow,
                            BankReceiptPaymentOrderHelper.ReceiptType);

                        worksheet.Cell(excelRow, 1).Value = GetCurrentAccountName(itemRow);
                        worksheet.Cell(excelRow, 2).Value = GetTaxOrIdNo(itemRow, currentAccountInfo);
                        worksheet.Cell(excelRow, 3).Value = GetRecipientIban(itemRow, currentAccountInfo);
                        worksheet.Cell(excelRow, 4).Value = amount;
                        worksheet.Cell(excelRow, 5).Value = itemRow.IsNull("Explanation")
                            ? string.Empty
                            : Convert.ToString(itemRow["Explanation"]);

                        excelRow++;
                    }

                    worksheet.Cell(totalRow, 3).Value = SLanguage.GetString("toplam");
                    worksheet.Cell(totalRow, 4).FormulaA1 = $"SUM(D{DetailStartRow}:D{lastDetailRow})";

                    workbook.SaveAs(targetFilePath);
                }

                result.Succeeded = true;
                result.FilePath = targetFilePath;
                result.Message = SLanguage.GetString("Talimat dosyası oluşturuldu.");
                return result;
            }
            catch (Exception ex)
            {
                result.Message = $"{SLanguage.GetString("Talimat dosyası oluşturulamadı.")} {ex.Message}";
                return result;
            }
            finally
            {
                TryDeleteFile(tempTemplatePath);
            }
        }

        static void WriteHeaderFields(IXLWorksheet worksheet, DataRow bankRow, string sourceIbanNo)
        {
            worksheet.Cell(DateRow, DateColumn).Value = DateTime.Today;
            worksheet.Cell(BankNameRow, 1).Value = Convert.ToString(bankRow["BankName"]) ?? string.Empty;
            worksheet.Cell(BranchNameRow, 1).Value = Convert.ToString(bankRow["BranchName"]) ?? string.Empty;
            worksheet.Cell(SourceIbanRow, 1).Value = sourceIbanNo ?? string.Empty;
        }

        public static List<DataRow> NormalizeExportRows(IEnumerable<DataRow> selectedRows)
        {
            if (selectedRows == null)
                return new List<DataRow>();

            return selectedRows
                .Where(row => row != null
                    && row.RowState != DataRowState.Deleted
                    && row.RowState != DataRowState.Detached
                    && PlanningAmountSide.GetAmountFromRow(row, BankReceiptPaymentOrderHelper.ReceiptType) > 0m)
                .OrderBy(row => row.IsNull("ItemOrderNo") ? int.MaxValue : Convert.ToInt32(row["ItemOrderNo"]))
                .ToList();
        }

        static string GetCurrentAccountName(DataRow itemRow)
        {
            if (itemRow.Table.Columns.Contains("CurrentAccountName") && !itemRow.IsNull("CurrentAccountName"))
                return Convert.ToString(itemRow["CurrentAccountName"]) ?? string.Empty;

            return string.Empty;
        }

        static string GetTaxOrIdNo(DataRow itemRow, Dictionary<long, CurrentAccountInstructionInfo> currentAccountInfo)
        {
            if (!TryGetCurrentAccountId(itemRow, out long currentAccountId))
                return string.Empty;

            return currentAccountInfo.TryGetValue(currentAccountId, out CurrentAccountInstructionInfo info)
                ? info.TaxOrIdNo ?? string.Empty
                : string.Empty;
        }

        static string GetRecipientIban(DataRow itemRow, Dictionary<long, CurrentAccountInstructionInfo> currentAccountInfo)
        {
            if (itemRow.Table.Columns.Contains("CurrentAccountBankIbanNo") && !itemRow.IsNull("CurrentAccountBankIbanNo"))
            {
                string itemIban = Convert.ToString(itemRow["CurrentAccountBankIbanNo"]);
                if (!string.IsNullOrWhiteSpace(itemIban))
                    return itemIban.Trim();
            }

            if (!TryGetCurrentAccountId(itemRow, out long currentAccountId))
                return string.Empty;

            return currentAccountInfo.TryGetValue(currentAccountId, out CurrentAccountInstructionInfo info)
                ? info.DefaultIban ?? string.Empty
                : string.Empty;
        }

        static bool TryGetCurrentAccountId(DataRow itemRow, out long currentAccountId)
        {
            currentAccountId = 0;
            if (!itemRow.Table.Columns.Contains("CurrentAccountId") || itemRow.IsNull("CurrentAccountId"))
                return false;

            currentAccountId = Convert.ToInt64(itemRow["CurrentAccountId"]);
            return currentAccountId > 0;
        }

        static Dictionary<long, CurrentAccountInstructionInfo> LoadCurrentAccountInstructionInfo(
            LiveSession session,
            IEnumerable<DataRow> exportRows)
        {
            var result = new Dictionary<long, CurrentAccountInstructionInfo>();
            if (session?.ActiveCompany?.RecId == null)
                return result;

            var currentAccountIds = exportRows
                .Select(TryGetCurrentAccountIdFromRow)
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            if (currentAccountIds.Count == 0)
                return result;

            string idList = string.Join(",", currentAccountIds);
            DataTable table = UtilityFunctions.GetDataTableList(
                session._dbInfo.DBProvider,
                session._dbInfo.Connection,
                null,
                "Erp_CurrentAccount",
                $@"select ca.RecId,
                          case
                              when isnull(ca.CurrentAccountKind,0)=1 then isnull(ca.TaxNo,'')
                              when ca.CurrentAccountKind in (2,3) then isnull(ca.IdNo,'')
                              else isnull(ca.TaxNo,'')
                          end TaxOrIdNo,
                          isnull((
                              select top 1 cab.IbanNo
                              from Erp_CurrentAccountBank cab with (nolock)
                              where cab.CurrentAccountId = ca.RecId
                                and isnull(cab.IsDefault,0)=1
                                and isnull(cab.InUse,0)=1
                                and isnull(cab.IsDeleted,0)=0
                          ), '') DefaultIban
                   from Erp_CurrentAccount ca with (nolock)
                   where ca.RecId in ({idList})");

            if (table == null)
                return result;

            foreach (DataRow row in table.Rows)
            {
                if (row.IsNull("RecId"))
                    continue;

                long recId = Convert.ToInt64(row["RecId"]);
                result[recId] = new CurrentAccountInstructionInfo
                {
                    TaxOrIdNo = Convert.ToString(row["TaxOrIdNo"])?.Trim(),
                    DefaultIban = Convert.ToString(row["DefaultIban"])?.Trim()
                };
            }

            return result;
        }

        static long TryGetCurrentAccountIdFromRow(DataRow itemRow)
        {
            TryGetCurrentAccountId(itemRow, out long currentAccountId);
            return currentAccountId;
        }

        static void ClearDetailArea(IXLWorksheet worksheet, int startRow, int endRow)
        {
            if (endRow < startRow)
                return;

            worksheet.Range(startRow, 1, endRow, DetailColumnCount).Clear(XLClearOptions.Contents);
        }

        static string ExtractTemplateToTempFile()
        {
            Assembly assembly = typeof(PaymentOrderInstructionExportService).Assembly;
            using Stream resourceStream = assembly.GetManifestResourceStream(TemplateResourceName)
                ?? throw new InvalidOperationException($"Embedded resource not found: {TemplateResourceName}");

            string tempPath = Path.Combine(Path.GetTempPath(), $"TalimatSablon_{Guid.NewGuid():N}.xlsx");
            using (FileStream fileStream = File.Create(tempPath))
                resourceStream.CopyTo(fileStream);

            return tempPath;
        }

        static void TryDeleteFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return;

            try
            {
                File.Delete(path);
            }
            catch
            {
                // ignored
            }
        }

        public static string BuildSuggestedFileName(DataRow headerRow)
        {
            string receiptNo = headerRow?.Table?.Columns.Contains("ReceiptNo") == true && !headerRow.IsNull("ReceiptNo")
                ? Convert.ToString(headerRow["ReceiptNo"])?.Trim()
                : string.Empty;

            DateTime receiptDate = headerRow?.Table?.Columns.Contains("ReceiptDate") == true && !headerRow.IsNull("ReceiptDate")
                ? Convert.ToDateTime(headerRow["ReceiptDate"]).Date
                : DateTime.Today;

            string safeReceiptNo = string.IsNullOrWhiteSpace(receiptNo)
                ? "Talimat"
                : receiptNo.Replace('\\', '_').Replace('/', '_').Replace(':', '_');

            return $"Talimat_{safeReceiptNo}_{receiptDate:yyyyMMdd}.xlsx";
        }

        sealed class CurrentAccountInstructionInfo
        {
            public string TaxOrIdNo { get; set; }
            public string DefaultIban { get; set; }
        }
    }
}
