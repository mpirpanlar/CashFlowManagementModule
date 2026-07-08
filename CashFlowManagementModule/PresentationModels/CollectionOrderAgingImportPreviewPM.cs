using System;
using System.Collections.Generic;
using System.Data;
using System.Windows;

using CashFlowManagementModule.Services;

using LiveCore.Desktop.UI.Controls;

using Prism.Ioc;

using Sentez.Common;
using Sentez.Common.Commands;
using Sentez.Common.PresentationModels;
using Sentez.Common.SystemServices;
using Sentez.Common.Utilities;
using Sentez.Data.BusinessObjects;
using Sentez.Data.MetaData;
using Sentez.Data.Tools;
using Sentez.Localization;

namespace CashFlowManagementModule.PresentationModels
{
    public class CollectionOrderAgingImportPreviewPM : PMDesktop
    {
        DataTable _agingData;
        string _amountColumnName;
        CollectionOrderAgingImportContext _context;
        BusinessObjectBase _businessObject;

        public DataTable AgingData
        {
            get => _agingData;
            set
            {
                _agingData = value;
                OnPropertyChanged(nameof(AgingData));
                OnPropertyChanged(nameof(AgingDataView));
            }
        }

        public DataView AgingDataView => _agingData?.DefaultView;

        public bool WasImported { get; private set; }

        LiveGridControl PreviewGrid => FCtrl<LiveGridControl>("gridAgingPreview");

        public CollectionOrderAgingImportPreviewPM(IContainerExtension container) : base(container)
        {
        }

        public void Initialize(
            CollectionOrderAgingImportContext context,
            BusinessObjectBase businessObject,
            LiveSession session)
        {
            _context = context;
            _businessObject = businessObject;
        }

        public override void LoadCommands()
        {
            base.LoadCommands();
            if (CmdList["CloseCommand"] == null)
            {
                CmdList.AddCmd(1, "CloseCommand", SLanguage.GetString("Kapat"), OnCloseCommand, CanCloseCommand);
            }

            CmdList.AddCmd(330, "ImportSelectedCollectionAgingCommand", SLanguage.GetString("Aktar"), OnImportSelectedAgingCommand, null);
        }

        public override void _view_Loaded(object sender, RoutedEventArgs e)
        {
            base._view_Loaded(sender, e);
            PmTitle = SLanguage.GetString("Alacak Yaşlandırma Önizleme");
            LoadAgingData();
        }

        void LoadAgingData()
        {
            if (_context == null)
            {
                SysMng.Instance.ActWndMng.ShowMsg(
                    SLanguage.GetString("Aktarım parametreleri bulunamadı."),
                    ConstantStr.Warning);
                return;
            }

            CurrentAccountAgingReportDataResult reportData = CurrentAccountCollectionAgingReportDataService.LoadAgingData(
                container,
                _context.ReportDate,
                _context.StartCurrentAccountCode,
                _context.EndCurrentAccountCode);

            if (!reportData.HasData)
            {
                SysMng.Instance.ActWndMng.ShowMsg(
                    string.IsNullOrWhiteSpace(reportData.ErrorMessage)
                        ? SLanguage.GetString("Alacak yaşlandırma verisi alınamadı.")
                        : reportData.ErrorMessage,
                    ConstantStr.Warning);
                return;
            }

            _amountColumnName = reportData.AmountColumnName;
            AgingData = reportData.Data;
            BuildPreviewColumns(AgingData);

            if (!string.IsNullOrWhiteSpace(reportData.ErrorMessage))
            {
                SysMng.Instance.ActWndMng.ShowMsg(reportData.ErrorMessage, ConstantStr.Warning);
            }
            else if (!string.IsNullOrWhiteSpace(reportData.InfoMessage))
            {
                SysMng.Instance.ActWndMng.ShowMsg(reportData.InfoMessage, ConstantStr.Information);
            }
        }

        void BuildPreviewColumns(DataTable table)
        {
            LiveGridControl grid = PreviewGrid;
            if (grid == null)
                return;

            if (grid.Columns.Count > 0)
                grid.Columns.Clear();

            var columns = new ReceiptColumnCollection();
            if (table != null)
            {
                foreach (DataColumn dataColumn in table.Columns)
                {
                    ReceiptColumn column = CreatePreviewColumn(dataColumn);
                    if (column != null)
                        columns.Add(column);
                }
            }

            grid.ColumnDefinitions = columns;
            grid.GenerateColumns();
        }

        static ReceiptColumn CreatePreviewColumn(DataColumn dataColumn)
        {
            if (dataColumn == null)
                return null;

            var column = new ReceiptColumn
            {
                ColumnName = dataColumn.ColumnName,
                Caption = dataColumn.ColumnName,
                IsReadOnly = true,
                IsVisible = true,
                EditorType = EditorType.ReadOnlyTextEditor,
                DataType = dataColumn.DataType,
                Width = 110
            };

            if (dataColumn.DataType == typeof(decimal)
                || dataColumn.DataType == typeof(double)
                || dataColumn.DataType == typeof(float))
            {
                column.UsageType = FieldUsage.Amount;
                column.UdtType = UdtType.UdtAmount;
                column.Width = 90;
            }
            else if (dataColumn.DataType == typeof(DateTime))
            {
                column.UsageType = FieldUsage.Date;
                column.UdtType = UdtType.UdtDate;
                column.EditorType = EditorType.ReadOnlyTextEditor;
                column.Width = 90;
            }
            else if (dataColumn.ColumnName.IndexOf("Adı", StringComparison.OrdinalIgnoreCase) >= 0
                     || dataColumn.ColumnName.IndexOf("Açıklama", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                column.Width = 200;
                column.UdtType = UdtType.UdtName;
            }
            else if (dataColumn.ColumnName.IndexOf("Kodu", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                column.Width = 100;
                column.UdtType = UdtType.UdtCode;
            }

            return column;
        }

        void OnImportSelectedAgingCommand(ISysCommandParam param)
        {
            if (string.IsNullOrWhiteSpace(_amountColumnName))
            {
                SysMng.Instance.ActWndMng.ShowMsg(
                    SLanguage.GetString("Alacak bakiye kolonu bulunamadı."),
                    ConstantStr.Warning);
                return;
            }

            LiveGridControl grid = PreviewGrid;
            if (grid?.SelectedItems == null || grid.SelectedItems.Count == 0)
            {
                SysMng.Instance.ActWndMng.ShowMsg(
                    SLanguage.GetString("Lütfen aktarılacak satırları seçiniz."),
                    ConstantStr.Warning);
                return;
            }

            var selectedRows = new List<DataRow>();
            foreach (object selectedItem in grid.SelectedItems)
            {
                if (selectedItem is DataRowView rowView && rowView.Row != null)
                    selectedRows.Add(rowView.Row);
            }

            if (selectedRows.Count == 0)
            {
                SysMng.Instance.ActWndMng.ShowMsg(
                    SLanguage.GetString("Lütfen aktarılacak satırları seçiniz."),
                    ConstantStr.Warning);
                return;
            }

            _context.RefreshDefaultBankAccount?.Invoke();

            CurrentAccountCollectionAgingImportResult importResult = CurrentAccountCollectionAgingImportService.ImportSelectedRows(
                _businessObject,
                _context.ReportDate,
                _context.DefaultBankAccountId,
                selectedRows,
                _amountColumnName,
                _context.DefaultBankAccountCode);

            if (!string.IsNullOrEmpty(importResult.Message))
            {
                SysMng.Instance.ActWndMng.ShowMsg(
                    importResult.Message,
                    importResult.AddedCount > 0 || importResult.UpdatedCount > 0 ? null : ConstantStr.Warning);
            }

            if (importResult.AddedCount > 0 || importResult.UpdatedCount > 0)
                WasImported = true;

            OnCloseCommand(param);
        }
    }
}
