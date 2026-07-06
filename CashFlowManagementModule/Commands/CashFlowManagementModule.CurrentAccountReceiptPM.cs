using CashFlowManagementModule.BoExtensions;
using CashFlowManagementModule.Services;

using DevExpress.Xpf.Grid;

using LiveCore.Desktop.UI.Controls;

using Sentez.Common;
using Sentez.Common.Commands;
using Sentez.Common.InformationMessages;
using Sentez.Common.PresentationModels;
using Sentez.Common.SystemServices;
using Sentez.Common.Utilities;
using Sentez.Data.BusinessObjects;
using Sentez.Data.MetaData;
using Sentez.Finance.PresentationModels;
using Sentez.FinanceModule.Models;
using Sentez.Localization;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Sentez.CashFlowManagementModule
{
    public partial class CashFlowManagementModule
    {
        CurrentAccountReceiptPM _currentAccountReceiptPm;
        bool _creditCardInstallmentGridHooked;
        const string CreditCardValidationLabelName = "CreditCardValidationLabel";
        const string CreditCardValidationToolbarName = "CreditCardValidationToolbar";

        void RegisterCurrentAccountReceiptPmHooks()
        {
            PMBase.AddCustomInit("CurrentAccountReceiptPM", CurrentAccountReceiptPm_Init);
            PMBase.AddCustomDispose("CurrentAccountReceiptPM", CurrentAccountReceiptPm_Dispose);
            PMBase.AddCustomViewLoaded("CurrentAccountReceiptPM", CurrentAccountReceiptPm_ViewLoaded);
        }

        bool IsOwnCreditCardReceiptContext()
        {
            DataRow headerRow = _currentAccountReceiptPm?.ActiveBO?.CurrentRow?.Row;
            if (headerRow == null || headerRow.IsNull("ReceiptType")) return false;
            short receiptType = Convert.ToInt16(headerRow["ReceiptType"]);
            return receiptType == 51 || receiptType == 50;
        }

        void CurrentAccountReceiptPm_Init(PMBase pm, PmParam parameter)
        {
            _currentAccountReceiptPm = pm as CurrentAccountReceiptPM;
            if (_currentAccountReceiptPm?.ActiveBO == null) return;

            _currentAccountReceiptPm.ActiveBO.ColumnChanged += CurrentAccountReceiptPm_ActiveBO_ColumnChanged;
            _currentAccountReceiptPm.ActiveBO.BeforePost += CurrentAccountReceiptPm_ActiveBO_BeforePost;
            _currentAccountReceiptPm.ActiveBO.AfterGet += CurrentAccountReceiptPm_ActiveBO_AfterGet;
        }

        void CurrentAccountReceiptPm_ActiveBO_AfterGet(object sender, EventArgs e)
        {
            _currentAccountReceiptPm?.ActiveViewControl?.Dispatcher.BeginInvoke(
                new Action(() =>
                {
                    EnsureCreditCardInstallmentColumns();
                    EnsureCreditCardValidationToolbar();
                }),
                DispatcherPriority.Background);
        }

        void CurrentAccountReceiptPm_Dispose(PMBase pm, PmParam parameter)
        {
            if (_currentAccountReceiptPm?.ActiveBO != null)
            {
                _currentAccountReceiptPm.ActiveBO.ColumnChanged -= CurrentAccountReceiptPm_ActiveBO_ColumnChanged;
                _currentAccountReceiptPm.ActiveBO.BeforePost -= CurrentAccountReceiptPm_ActiveBO_BeforePost;
                _currentAccountReceiptPm.ActiveBO.AfterGet -= CurrentAccountReceiptPm_ActiveBO_AfterGet;
            }

            UnhookCreditCardInstallmentGridSync();
            _currentAccountReceiptPm = null;
        }

        void CurrentAccountReceiptPm_ViewLoaded(object sender, RoutedEventArgs e)
        {
            if (_currentAccountReceiptPm == null)
                _currentAccountReceiptPm = sender as CurrentAccountReceiptPM;
            if (_currentAccountReceiptPm == null) return;

            _currentAccountReceiptPm.ActiveViewControl?.Dispatcher.BeginInvoke(
                new Action(() =>
                {
                    EnsureCreditCardInstallmentColumns();
                    EnsureCreditCardValidationToolbar();
                }),
                DispatcherPriority.Loaded);
        }

        void CurrentAccountReceiptPm_ActiveBO_ColumnChanged(object sender, DataColumnChangeEventArgs e)
        {
            if (_currentAccountReceiptPm == null) return;

            if (e.Row.Table.TableName == "Erp_CurrentAccountReceipt" && e.Column.ColumnName == "ReceiptType")
            {
                UpdateCreditCardValidationLabel(null);
                _currentAccountReceiptPm?.ActiveViewControl?.Dispatcher.BeginInvoke(
                    new Action(EnsureCreditCardInstallmentColumns),
                    DispatcherPriority.Background);
                return;
            }

            if (!IsOwnCreditCardReceiptContext()) return;

            if (e.Row.Table.TableName == "Erp_CurrentAccountReceiptItem"
                && (e.Column.ColumnName == "BankAccountId"
                    || e.Column.ColumnName == "ReceiptDate"
                    || e.Column.ColumnName == CurrentAccountReceiptCreditCardHelper.FieldInstalmentStartDate
                    || e.Column.ColumnName == CurrentAccountReceiptCreditCardHelper.FieldInstallmentCount
                    || e.Column.ColumnName == "Debit"
                    || e.Column.ColumnName == "Credit"))
            {
                RefreshCurrentAccountCreditCardValidationMessage(e.Row);
            }
        }

        void CurrentAccountReceiptPm_ActiveBO_BeforePost(object sender, CancelEventArgs e)
        {
            if (e.Cancel || _currentAccountReceiptPm == null) return;

            CommitCurrentAccountGridEdits();
            SyncCreditCardInstallmentFieldsFromGrid();
            NormalizeAllCreditCardInstallmentCounts();
            if (!IsOwnCreditCardReceiptContext()) return;

            IList<CreditCardPaymentLineInput> lines = CollectCurrentAccountCreditCardLines();
            if (lines.Count == 0) return;

            BusinessObjectBase businessObject = _currentAccountReceiptPm.ActiveBO as BusinessObjectBase;
            if (businessObject?.Connection == null) return;

            LiveSession session = SysMng.Instance.getSession() as LiveSession;
            int companyId = session?.ActiveCompany?.RecId ?? 0;

            CreditCardPaymentValidationResult result = CreditCardPaymentWarningService.ValidateBeforePayment(
                businessObject.Provider,
                businessObject.Connection,
                businessObject.Transaction,
                companyId,
                lines);
            if (result.IsBlocked)
            {
                _currentAccountReceiptPm.ActiveBO.ErrorMessage = result.BlockMessage;
                e.Cancel = true;
                return;
            }

            if (result.HasWarning
                && _sysMng.ActWndMng.ShowMsg(
                    result.WarningMessage + System.Environment.NewLine + System.Environment.NewLine + SLanguage.GetString("Devam etmek istiyor musunuz?"),
                    ConstantStr.Warning,
                    Sentez.Common.InformationMessages.MessageBoxButton.YesNo,
                    Sentez.Common.InformationMessages.MessageBoxImage.Warning) == Sentez.Common.InformationMessages.MessageBoxResult.No)
            {
                e.Cancel = true;
            }
        }

        void NormalizeAllCreditCardInstallmentCounts()
        {
            if (_currentAccountReceiptPm?.ActiveBO?.Data == null || !IsOwnCreditCardReceiptContext()) return;

            DataTable itemTable = _currentAccountReceiptPm.ActiveBO.Data.Tables["Erp_CurrentAccountReceiptItem"];
            if (itemTable == null) return;

            foreach (DataRow itemRow in itemTable.Rows.Cast<DataRow>().Where(r => r.RowState != DataRowState.Deleted))
                CurrentAccountReceiptCreditCardHelper.NormalizeInstallmentCount(itemRow);
        }

        void SyncCreditCardInstallmentFieldsFromGrid()
        {
            LiveGridControl gridDetail = _currentAccountReceiptPm?.FCtrl("gridDetail") as LiveGridControl;
            if (gridDetail?.View == null || gridDetail.ItemsSource is not DataView dataView)
                return;

            for (int listIndex = 0; listIndex < dataView.Count; listIndex++)
            {
                DataRowView rowView = dataView[listIndex];
                if (rowView?.Row == null || rowView.Row.RowState == DataRowState.Deleted)
                    continue;

                SyncCreditCardInstallmentFieldFromGrid(gridDetail, listIndex, rowView, CurrentAccountReceiptCreditCardHelper.FieldInstallmentCount);
                SyncCreditCardInstallmentFieldFromGrid(gridDetail, listIndex, rowView, CurrentAccountReceiptCreditCardHelper.FieldInstalmentStartDate);
            }
        }

        static void SyncCreditCardInstallmentFieldFromGrid(LiveGridControl gridDetail, int listIndex, DataRowView rowView, string fieldName)
        {
            int rowHandle = gridDetail.GetRowHandleByListIndex(listIndex);
            if (!gridDetail.IsValidRowHandle(rowHandle)) return;

            object cellValue = gridDetail.GetCellValue(rowHandle, fieldName);
            CurrentAccountReceiptCreditCardHelper.SyncInstallmentField(rowView.Row, fieldName, cellValue);
        }

        void RefreshCurrentAccountCreditCardValidationMessage(DataRow itemRow)
        {
            if (_currentAccountReceiptPm == null || itemRow == null || !IsOwnCreditCardReceiptContext()) return;
            if (itemRow.IsNull("BankAccountId")) return;

            BusinessObjectBase businessObject = _currentAccountReceiptPm.ActiveBO as BusinessObjectBase;
            if (businessObject?.Connection == null) return;

            LiveSession session = SysMng.Instance.getSession() as LiveSession;
            int companyId = session?.ActiveCompany?.RecId ?? 0;

            DataRow headerRow = businessObject.CurrentRow?.Row;
            CreditCardPaymentLineInput line = CreditCardPaymentLineInput.FromCurrentAccountReceiptItem(itemRow, headerRow);
            CreditCardPaymentValidationResult result = CreditCardPaymentWarningService.ValidateLinePreview(
                businessObject.Provider,
                businessObject.Connection,
                businessObject.Transaction,
                companyId,
                line);
            UpdateCreditCardValidationLabel(result);
        }

        IList<CreditCardPaymentLineInput> CollectCurrentAccountCreditCardLines()
        {
            var lines = new List<CreditCardPaymentLineInput>();
            if (_currentAccountReceiptPm?.ActiveBO?.Data == null) return lines;

            DataRow headerRow = _currentAccountReceiptPm.ActiveBO.CurrentRow?.Row;
            DataTable itemTable = _currentAccountReceiptPm.ActiveBO.Data.Tables["Erp_CurrentAccountReceiptItem"];
            if (itemTable == null) return lines;

            foreach (DataRow itemRow in itemTable.Rows.Cast<DataRow>()
                         .Where(r => r.RowState != DataRowState.Deleted && !r.IsNull("BankAccountId")))
            {
                CreditCardPaymentLineInput line = CreditCardPaymentLineInput.FromCurrentAccountReceiptItem(itemRow, headerRow);
                if (line != null && line.BankAccountId > 0)
                    lines.Add(line);
            }

            return lines;
        }

        void UpdateCreditCardValidationLabel(CreditCardPaymentValidationResult result)
        {
            if (_currentAccountReceiptPm?.ActiveViewControl == null) return;

            TextBlock label = _currentAccountReceiptPm.ActiveViewControl.FindName(CreditCardValidationLabelName) as TextBlock;
            if (label == null) return;

            if (result == null || (!result.IsBlocked && !result.HasWarning))
            {
                label.Text = string.Empty;
                label.Foreground = System.Windows.Media.Brushes.Black;
                return;
            }

            label.Text = result.IsBlocked ? result.BlockMessage : result.WarningMessage;
            label.Foreground = result.IsBlocked
                ? System.Windows.Media.Brushes.DarkRed
                : System.Windows.Media.Brushes.DarkOrange;
        }

        void EnsureCreditCardValidationToolbar()
        {
            if (_currentAccountReceiptPm == null) return;

            LiveGridControl gridDetail = _currentAccountReceiptPm.FCtrl("gridDetail") as LiveGridControl;
            if (gridDetail?.Parent is not Grid parentGrid) return;
            if (parentGrid.FindName(CreditCardValidationToolbarName) != null) return;

            parentGrid.RowDefinitions.Insert(0, new System.Windows.Controls.RowDefinition { Height = GridLength.Auto });

            foreach (UIElement child in parentGrid.Children)
            {
                int row = Grid.GetRow(child);
                Grid.SetRow(child, row + 1);
            }

            var toolbar = new StackPanel
            {
                Name = CreditCardValidationToolbarName,
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 4)
            };

            var validationLabel = new TextBlock
            {
                Name = CreditCardValidationLabelName,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center
            };
            toolbar.Children.Add(validationLabel);

            Grid.SetRow(toolbar, 0);
            parentGrid.Children.Add(toolbar);
            parentGrid.RegisterName(CreditCardValidationToolbarName, toolbar);
            parentGrid.RegisterName(CreditCardValidationLabelName, validationLabel);
        }

        void CommitCurrentAccountGridEdits()
        {
            LiveGridControl gridDetail = _currentAccountReceiptPm?.FCtrl("gridDetail") as LiveGridControl;
            gridDetail?.View?.CloseEditor();
            gridDetail?.ValidateRows();
        }

        void EnsureCreditCardInstallmentColumns()
        {
            if (_currentAccountReceiptPm == null || !IsOwnCreditCardReceiptContext()) return;

            CurrentAccountReceiptCreditCardHelper.EnsureCurrentAccountReceiptItemColumns(_currentAccountReceiptPm.ActiveBO?.Data);

            var columns = _currentAccountReceiptPm.CurrentAccountReceiptColumnCollection;
            if (columns == null) return;

            EnsureCurrentAccountColumnVisible(columns, CurrentAccountReceiptCreditCardHelper.FieldInstallmentCount, SLanguage.GetString("Taksit Sayısı"), EditorType.TextEditor, 80);
            EnsureCurrentAccountColumnVisible(columns, CurrentAccountReceiptCreditCardHelper.FieldInstalmentStartDate, SLanguage.GetString("Taksit Başlangıç Tarihi"), EditorType.DateEditor, 110);

            HookCreditCardInstallmentGridSync();
        }

        void HookCreditCardInstallmentGridSync()
        {
            if (_creditCardInstallmentGridHooked || _currentAccountReceiptPm == null) return;

            LiveGridControl gridDetail = _currentAccountReceiptPm.FCtrl("gridDetail") as LiveGridControl;
            if (gridDetail?.View is not TableView tableView) return;

            tableView.CellValueChanged += CreditCardInstallmentGrid_CellValueChanged;
            _creditCardInstallmentGridHooked = true;
        }

        void UnhookCreditCardInstallmentGridSync()
        {
            if (!_creditCardInstallmentGridHooked || _currentAccountReceiptPm == null) return;

            LiveGridControl gridDetail = _currentAccountReceiptPm.FCtrl("gridDetail") as LiveGridControl;
            if (gridDetail?.View is TableView tableView)
                tableView.CellValueChanged -= CreditCardInstallmentGrid_CellValueChanged;

            _creditCardInstallmentGridHooked = false;
        }

        void CreditCardInstallmentGrid_CellValueChanged(object sender, CellValueChangedEventArgs e)
        {
            if (!IsOwnCreditCardReceiptContext()) return;
            if (e.Column?.FieldName != CurrentAccountReceiptCreditCardHelper.FieldInstallmentCount
                && e.Column?.FieldName != CurrentAccountReceiptCreditCardHelper.FieldInstalmentStartDate)
                return;

            if (e.Row is not DataRowView rowView || rowView.Row == null)
                return;

            CurrentAccountReceiptCreditCardHelper.SyncInstallmentField(rowView.Row, e.Column.FieldName, e.Value);
        }

        static void EnsureCurrentAccountColumnVisible(
            IList<ReceiptColumn> columns,
            string columnName,
            string caption,
            EditorType editorType,
            int width)
        {
            ReceiptColumn column = columns.FirstOrDefault(c => c.ColumnName == columnName);
            if (column == null)
            {
                columns.Add(new ReceiptColumn
                {
                    ColumnName = columnName,
                    Caption = caption,
                    EditorType = editorType,
                    Width = width,
                    IsVisible = true
                });
                return;
            }

            column.IsVisible = true;
            if (string.IsNullOrWhiteSpace(column.Caption))
                column.Caption = caption;
        }
    }
}
