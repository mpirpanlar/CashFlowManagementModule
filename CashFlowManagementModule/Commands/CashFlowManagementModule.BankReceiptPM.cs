using LiveCore.Desktop.SBase;
using LiveCore.Desktop.UI.Controls;

using CashFlowManagementModule.BoExtensions;
using CashFlowManagementModule.Services;

using Sentez.BankModule.PresentationModels;
using Sentez.Common;
using Sentez.Common.Commands;
using Sentez.Common.PresentationModels;
using Sentez.Common.Utilities;
using Sentez.Data.BusinessObjects;
using Sentez.Data.MetaData;
using Sentez.Localization;

using LiveCore.Desktop.Common;
using Sentez.Common.SystemServices;

using System;
using System.Collections;
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
        BankReceiptPM _paymentOrderBankReceiptPm;
        bool _paymentOrderHooksApplied;
        const string PaymentOrderLineApprovalToolbarName = "PaymentOrderLineApprovalToolbar";
        const string PaymentOrderCreditCardValidationLabelName = "PaymentOrderCreditCardValidationLabel";

        void RegisterBankReceiptPmHooks()
        {
            PMBase.AddCustomInit("BankReceiptPM", PaymentOrderBankReceiptPm_Init);
            PMBase.AddCustomDispose("BankReceiptPM", PaymentOrderBankReceiptPm_Dispose);
            PMBase.AddCustomViewLoaded("BankReceiptPM", PaymentOrderBankReceiptPm_ViewLoaded);
        }

        bool IsPaymentOrderPm(PMBase pm)
        {
            return BankReceiptPaymentOrderHelper.IsPaymentOrderContext(pm);
        }

        bool TryApplyPaymentOrderHooks(BankReceiptPM pm)
        {
            if (_paymentOrderHooksApplied || pm == null) return _paymentOrderHooksApplied;

            if (!BankReceiptPaymentOrderHelper.IsPaymentOrderContext(pm)) return false;

            EnsureBankReceiptApprovedChangeCommandRegistered();
            HookPaymentOrderCommands(pm);
            RefreshPaymentOrderApprovedChangeContextMenuCommand();
            _paymentOrderHooksApplied = true;
            return true;
        }

        void PaymentOrderBankReceiptPm_Init(PMBase pm, PmParam parameter)
        {
            _paymentOrderBankReceiptPm = pm as BankReceiptPM;
            if (_paymentOrderBankReceiptPm?.ActiveBO == null) return;

            _paymentOrderBankReceiptPm.ActiveBO.ColumnChanged += PaymentOrderBankReceiptPm_ActiveBO_ColumnChanged;
            _paymentOrderBankReceiptPm.ActiveBO.AfterGet += PaymentOrderBankReceiptPm_ActiveBO_AfterGet;
            _paymentOrderBankReceiptPm.ActiveBO.PropertyChanged += PaymentOrderBankReceiptPm_ActiveBO_PropertyChanged;

            TryApplyPaymentOrderHooks(_paymentOrderBankReceiptPm);
        }

        void PaymentOrderBankReceiptPm_Dispose(PMBase pm, PmParam parameter)
        {
            if (_paymentOrderBankReceiptPm?.ActiveBO != null)
            {
                _paymentOrderBankReceiptPm.ActiveBO.ColumnChanged -= PaymentOrderBankReceiptPm_ActiveBO_ColumnChanged;
                _paymentOrderBankReceiptPm.ActiveBO.AfterGet -= PaymentOrderBankReceiptPm_ActiveBO_AfterGet;
                _paymentOrderBankReceiptPm.ActiveBO.PropertyChanged -= PaymentOrderBankReceiptPm_ActiveBO_PropertyChanged;
            }

            _paymentOrderBankReceiptPm = null;
            _paymentOrderHooksApplied = false;
        }

        void PaymentOrderBankReceiptPm_ViewLoaded(object sender, RoutedEventArgs e)
        {
            if (_paymentOrderBankReceiptPm == null) return;

            if (!TryApplyPaymentOrderHooks(_paymentOrderBankReceiptPm)) return;

            _paymentOrderBankReceiptPm.ActiveViewControl?.Dispatcher.BeginInvoke(
                new Action(() =>
                {
                    AddPaymentOrderDetailColumns();
                    EnsurePaymentOrderLineApprovalToolbar();
                    ApplyPaymentOrderCardLockState();
                    ApplyPaymentOrderApprovalColumnAccess();
                    ApplyPaymentOrderApprovalContextMenuAccess();
                    RefreshPaymentOrderApprovedChangeContextMenuCommand();
                }),
                DispatcherPriority.Loaded);
        }

        void PaymentOrderBankReceiptPm_ActiveBO_AfterGet(object sender, EventArgs e)
        {
            if (_paymentOrderBankReceiptPm == null) return;

            if (!TryApplyPaymentOrderHooks(_paymentOrderBankReceiptPm)) return;

            _paymentOrderBankReceiptPm.ActiveViewControl?.Dispatcher.BeginInvoke(
                new Action(() =>
                {
                    AddPaymentOrderDetailColumns();
                    EnsurePaymentOrderLineApprovalToolbar();
                    ApplyPaymentOrderCardLockState();
                    ApplyPaymentOrderApprovalColumnAccess();
                    ApplyPaymentOrderApprovalContextMenuAccess();
                    RefreshPaymentOrderApprovedChangeContextMenuCommand();
                    RefreshPaymentOrderDetailGrid();
                }),
                DispatcherPriority.Background);
        }

        void PaymentOrderBankReceiptPm_ActiveBO_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (_paymentOrderBankReceiptPm == null || !IsPaymentOrderPm(_paymentOrderBankReceiptPm)) return;

            if (e.PropertyName == "IsNewRecord" || e.PropertyName == "CurrentRow")
                ApplyPaymentOrderCardLockState();
        }

        void PaymentOrderBankReceiptPm_ActiveBO_ColumnChanged(object sender, DataColumnChangeEventArgs e)
        {
            if (_paymentOrderBankReceiptPm == null) return;

            if (e.Row.Table.TableName == "Erp_BankReceiptItem" && e.Column.ColumnName == "IsApproved")
            {
                RefreshPaymentOrderDetailGrid();
            }
            else if (e.Row.Table.TableName == "Erp_BankReceipt" && e.Column.ColumnName == "IsApproved")
            {
                ApplyPaymentOrderCardLockState();
                RefreshPaymentOrderDetailGrid();
            }
            else if (e.Row.Table.TableName == "Erp_BankReceiptItem"
                     && (e.Column.ColumnName == "BankAccountId"
                         || e.Column.ColumnName == "UD_PaymentDate"
                         || e.Column.ColumnName == "Credit"
                         || e.Column.ColumnName == BankReceiptCreditCardHelper.FieldInstallmentCount))
            {
                NormalizeInstallmentCount(e.Row);
                RefreshPaymentOrderCreditCardValidationMessage(e.Row);
            }
        }

        void NormalizeInstallmentCount(DataRow itemRow)
        {
            if (itemRow == null) return;
            if (!itemRow.Table.Columns.Contains(BankReceiptCreditCardHelper.FieldInstallmentCount)) return;

            if (itemRow.IsNull(BankReceiptCreditCardHelper.FieldInstallmentCount)
                || Convert.ToInt16(itemRow[BankReceiptCreditCardHelper.FieldInstallmentCount]) < 1)
            {
                itemRow[BankReceiptCreditCardHelper.FieldInstallmentCount] = (short)1;
            }
        }

        void RefreshPaymentOrderCreditCardValidationMessage(DataRow itemRow)
        {
            if (_paymentOrderBankReceiptPm == null || itemRow == null) return;
            if (itemRow.IsNull("BankAccountId")) return;

            LiveSession session = SysMng.Instance.getSession() as LiveSession;
            if (session == null) return;

            DateTime? fallbackDate = null;
            if (_paymentOrderBankReceiptPm.ActiveBO?.CurrentRow?.Row != null
                && _paymentOrderBankReceiptPm.ActiveBO.CurrentRow.Row.Table.Columns.Contains("UD_PaymentDate")
                && !_paymentOrderBankReceiptPm.ActiveBO.CurrentRow.Row.IsNull("UD_PaymentDate"))
            {
                fallbackDate = Convert.ToDateTime(_paymentOrderBankReceiptPm.ActiveBO.CurrentRow.Row["UD_PaymentDate"]);
            }

            CreditCardPaymentLineInput line = CreditCardPaymentLineInput.FromBankReceiptItem(itemRow, fallbackDate);
            CreditCardPaymentValidationResult result = CreditCardPaymentWarningService.ValidateLinePreview(session, line);
            UpdatePaymentOrderCreditCardValidationLabel(result);
        }

        void UpdatePaymentOrderCreditCardValidationLabel(CreditCardPaymentValidationResult result)
        {
            if (_paymentOrderBankReceiptPm?.ActiveViewControl == null) return;

            TextBlock label = _paymentOrderBankReceiptPm.ActiveViewControl.FindName(PaymentOrderCreditCardValidationLabelName) as TextBlock;
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

        void AddPaymentOrderDetailColumns()
        {
            if (_paymentOrderBankReceiptPm?.BankReceiptColumnCollection == null) return;

            AddColumnIfMissing("IsApproved", SLanguage.GetString("Onay Durumu"), EditorType.ComboBox, FieldUsage.None, 90, "ApprovedList");
            AddColumnIfMissing(BankReceiptCreditCardHelper.FieldInstallmentCount, SLanguage.GetString("Taksit Sayısı"), EditorType.TextEditor, FieldUsage.None, 90);

            ReceiptColumnCollection columns = _paymentOrderBankReceiptPm.BankReceiptColumnCollection;
            _paymentOrderBankReceiptPm.BankReceiptColumnCollection = columns;

            ApplyPaymentOrderApprovalColumnAccess();
        }

        void AddColumnIfMissing(string columnName, string caption, EditorType editorType, FieldUsage usageType, int width, string comboLookup = null)
        {
            if (_paymentOrderBankReceiptPm.BankReceiptColumnCollection.Any(c => c.ColumnName == columnName))
                return;

            ReceiptColumn column = new ReceiptColumn()
            {
                ColumnName = columnName,
                Caption = caption,
                EditorType = editorType,
                Width = width,
                UsageType = usageType,
                IsVisible = true
            };

            if (!string.IsNullOrEmpty(comboLookup))
            {
                column.ComboLookup = comboLookup;
                column.ComboDisplayMember = "Display";
                column.ComboValueMember = "Value";
            }

            _paymentOrderBankReceiptPm.BankReceiptColumnCollection.Add(column);
        }

        void ApplyPaymentOrderApprovalColumnAccess()
        {
            if (_paymentOrderBankReceiptPm?.BankReceiptColumnCollection == null) return;

            ReceiptColumn approvalColumn = _paymentOrderBankReceiptPm.BankReceiptColumnCollection
                .FirstOrDefault(c => c.ColumnName == "IsApproved");
            if (approvalColumn == null) return;

            if (approvalColumn.IsReadOnly)
            {
                approvalColumn.IsReadOnly = false;
                ReceiptColumnCollection columns = _paymentOrderBankReceiptPm.BankReceiptColumnCollection;
                _paymentOrderBankReceiptPm.BankReceiptColumnCollection = columns;
            }
        }

        void ApplyPaymentOrderApprovalContextMenuAccess()
        {
            BankReceiptPaymentOrderApprovalHelper.RefreshPaymentOrderApprovalUi(_paymentOrderBankReceiptPm);
        }

        void RefreshPaymentOrderApprovedChangeContextMenuCommand()
        {
            if (_paymentOrderBankReceiptPm?.contextMenu == null || _paymentOrderBankReceiptPm.CmdList == null) return;

            ISysCommand approvedChangeCommand = _paymentOrderBankReceiptPm.CmdList["ApprovedChangeCommand"];
            if (approvedChangeCommand == null) return;

            foreach (object item in _paymentOrderBankReceiptPm.contextMenu.Items)
            {
                if (item is MenuItem menuItem && menuItem.Name == "ApprovedChangeCommand")
                    menuItem.Command = approvedChangeCommand;
            }
        }

        void ApplyPaymentOrderCardLockState()
        {
            if (_paymentOrderBankReceiptPm?.ActiveBO?.CurrentRow?.Row == null) return;

            bool isLocked = BankReceiptPaymentOrderHelper.ShouldLockPaymentOrder(_paymentOrderBankReceiptPm.ActiveBO);

            if (_paymentOrderBankReceiptPm is PMDesktop pmDesktop)
                pmDesktop.SetViewEnabled(!isLocked);
        }

        void RefreshPaymentOrderDetailGrid()
        {
            if (_paymentOrderBankReceiptPm == null) return;

            LiveGridControl gridDetail = _paymentOrderBankReceiptPm.FCtrl("gridDetail") as LiveGridControl;
            gridDetail?.RefreshData();
        }

        void EnsurePaymentOrderLineApprovalToolbar()
        {
            if (_paymentOrderBankReceiptPm == null || !IsPaymentOrderPm(_paymentOrderBankReceiptPm)) return;

            LiveGridControl gridDetail = _paymentOrderBankReceiptPm.FCtrl("gridDetail") as LiveGridControl;
            if (gridDetail?.Parent is not Grid parentGrid) return;

            if (parentGrid.FindName(PaymentOrderLineApprovalToolbarName) != null) return;

            parentGrid.RowDefinitions.Insert(0, new RowDefinition { Height = GridLength.Auto });

            foreach (UIElement child in parentGrid.Children)
            {
                int row = Grid.GetRow(child);
                Grid.SetRow(child, row + 1);
            }

            var toolbar = new StackPanel
            {
                Name = PaymentOrderLineApprovalToolbarName,
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 4)
            };

            var btnApprove = new LiveButton
            {
                Content = SLanguage.GetString("Seçili Satırları Onayla"),
                Margin = new Thickness(0, 0, 6, 0),
                Padding = new Thickness(8, 2, 8, 2)
            };
            btnApprove.Command = _paymentOrderBankReceiptPm.CmdList["PaymentOrderBulkLineApproveCommand"];

            var btnUnapprove = new LiveButton
            {
                Content = SLanguage.GetString("Seçili Satırları Onaysız Yap"),
                Padding = new Thickness(8, 2, 8, 2)
            };
            btnUnapprove.Command = _paymentOrderBankReceiptPm.CmdList["PaymentOrderBulkLineUnapproveCommand"];

            toolbar.Children.Add(btnApprove);
            toolbar.Children.Add(btnUnapprove);

            var validationLabel = new TextBlock
            {
                Name = PaymentOrderCreditCardValidationLabelName,
                Margin = new Thickness(12, 4, 0, 0),
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center
            };
            toolbar.Children.Add(validationLabel);

            Grid.SetRow(toolbar, 0);
            parentGrid.Children.Add(toolbar);
            parentGrid.RegisterName(PaymentOrderLineApprovalToolbarName, toolbar);
            parentGrid.RegisterName(PaymentOrderCreditCardValidationLabelName, validationLabel);
        }

        void HookPaymentOrderCommands(BankReceiptPM pm)
        {
            if (pm?.CmdList == null) return;

            ISysCommand existingCommand = pm.CmdList["ApprovedChangeCommand"];
            if (existingCommand != null)
                pm.CmdList.Remove(existingCommand);

            pm.CmdList.AddCmd(
                115,
                "ApprovedChangeCommand",
                SLanguage.GetString("Onay İşlemi"),
                PaymentOrderOnApprovedChangeCommand,
                PaymentOrderCanApprovedChangeCommand);

            if (pm.CmdList["PaymentOrderBulkLineApproveCommand"] == null)
            {
                pm.CmdList.AddCmd(
                    320,
                    "PaymentOrderBulkLineApproveCommand",
                    SLanguage.GetString("Seçili Satırları Onayla"),
                    PaymentOrderBulkLineApproveCommand,
                    null);
            }

            if (pm.CmdList["PaymentOrderBulkLineUnapproveCommand"] == null)
            {
                pm.CmdList.AddCmd(
                    321,
                    "PaymentOrderBulkLineUnapproveCommand",
                    SLanguage.GetString("Seçili Satırları Onaysız Yap"),
                    PaymentOrderBulkLineUnapproveCommand,
                    null);
            }
        }

        bool PaymentOrderCanApprovedChangeCommand(ISysCommandParam obj)
        {
            if (_paymentOrderBankReceiptPm == null || !IsPaymentOrderPm(_paymentOrderBankReceiptPm)) return false;
            return BankReceiptPaymentOrderApprovalHelper.CanToggleHeaderApproval(_paymentOrderBankReceiptPm);
        }

        void PaymentOrderOnApprovedChangeCommand(ISysCommandParam obj)
        {
            if (_paymentOrderBankReceiptPm == null || !IsPaymentOrderPm(_paymentOrderBankReceiptPm)) return;

            BankReceiptPaymentOrderApprovalHelper.ExecuteHeaderApprovalToggle(_paymentOrderBankReceiptPm, obj);
            RefreshPaymentOrderDetailGrid();
        }

        void PaymentOrderBulkLineApproveCommand(ISysCommandParam obj)
        {
            if (_paymentOrderBankReceiptPm == null || !IsPaymentOrderPm(_paymentOrderBankReceiptPm)) return;

            LiveGridControl gridDetail = _paymentOrderBankReceiptPm.FCtrl("gridDetail") as LiveGridControl;
            if (gridDetail?.SelectedItems == null || gridDetail.SelectedItems.Count == 0)
            {
                SysMng.Instance.ActWndMng.ShowMsg(
                    SLanguage.GetString("Lütfen işlem yapılacak satırları seçiniz."),
                    ConstantStr.Warning);
                return;
            }

            long? userId = SysMng.Instance.getSession()?.ActiveUser?.RecId;
            DateTime approvedAt = new DateHelper().GetToday();

            foreach (object selectedItem in gridDetail.SelectedItems)
            {
                if (selectedItem is not DataRowView rowView) continue;
                if (rowView.Row.RowState == DataRowState.Deleted) continue;

                DataRow itemRow = rowView.Row;
                itemRow["IsApproved"] = (byte)1;
                BankReceiptPaymentOrderHelper.SetLineApprovedMetadata(itemRow, true, userId, approvedAt);
            }

            RefreshPaymentOrderDetailGrid();
        }

        void PaymentOrderBulkLineUnapproveCommand(ISysCommandParam obj)
        {
            if (_paymentOrderBankReceiptPm == null || !IsPaymentOrderPm(_paymentOrderBankReceiptPm)) return;

            if (!BankReceiptPaymentOrderHelper.HasLineApprovedEditRight())
                throw new LiveCommandItemException(PaymentOrderTerminology.LineApprovalDeniedMessage);

            LiveGridControl gridDetail = _paymentOrderBankReceiptPm.FCtrl("gridDetail") as LiveGridControl;
            if (gridDetail?.SelectedItems == null || gridDetail.SelectedItems.Count == 0)
            {
                SysMng.Instance.ActWndMng.ShowMsg(
                    SLanguage.GetString("Lütfen işlem yapılacak satırları seçiniz."),
                    ConstantStr.Warning);
                return;
            }

            foreach (object selectedItem in gridDetail.SelectedItems)
            {
                if (selectedItem is not DataRowView rowView) continue;
                if (rowView.Row.RowState == DataRowState.Deleted) continue;

                DataRow itemRow = rowView.Row;
                if (BankReceiptPaymentOrderHelper.GetApprovedValue(itemRow) == 0
                    && BankReceiptPaymentOrderHelper.GetPersistedApprovedValue(itemRow) == 0)
                    continue;

                itemRow["IsApproved"] = (byte)0;
                BankReceiptPaymentOrderHelper.SetLineApprovedMetadata(itemRow, false, null, null);
            }

            RefreshPaymentOrderDetailGrid();
        }
    }
}
