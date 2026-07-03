using CashFlowManagementModule.BoExtensions;

using Prism.Ioc;

using Sentez.BankModule.PresentationModels;
using Sentez.Common;
using Sentez.Common.Commands;
using Sentez.Common.PresentationModels;
using Sentez.Common.Report;
using Sentez.Common.SystemServices;
using Sentez.Data.Tools;

using LiveCore.Desktop.UI.Controls;

using System;
using System.Data;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CashFlowManagementModule.PresentationModels
{
    public class PaymentOrderBankReceiptPM : BankReceiptPM
    {
        const string DefaultBankAccountCodeFieldName = "TxtDefaultBankAccountCode";

        public string DefaultBankAccountCode
        {
            get => _defaultBankAccountCode;
            set
            {
                _defaultBankAccountCode = value;
                OnPropertyChanged(nameof(DefaultBankAccountCode));
            }
        }

        public long DefaultBankAccountId => _defaultBankAccountId;

        string _defaultBankAccountCode = string.Empty;
        long _defaultBankAccountId;

        public PaymentOrderBankReceiptPM(IContainerExtension container) : base(container)
        {
        }

        public override void OnListCommand(ISysCommandParam obj)
        {
            DependencyObject focusScope = FocusManager.GetFocusScope(ActiveViewControl);
            FrameworkElement focusedElement = FocusManager.GetFocusedElement(focusScope) as FrameworkElement;

            if (focusedElement is TextBox textBox
                && textBox.DataContext is LiveTextEdit textEdit
                && textEdit.Name == DefaultBankAccountCodeFieldName)
            {
                SysMng.Instance.ActWndMng.ShowReport(
                    "Erp_BankAccountAccountCodeList",
                    true,
                    DefaultBankAccountListValueHandler,
                    new DlgArgs("RecId"),
                    null,
                    new PolicyParams { FieldName = "DefaultBankAccountCode" },
                    "WorkListW",
                    ReportWorkMode.ChoseList);
                return;
            }

            base.OnListCommand(obj);
        }

        public void DefaultBankAccountListValueHandler(DlgArgs result)
        {
            if (result?.DlgReturnValue == null) return;
            ResolveDefaultBankAccount(Convert.ToInt64(result.DlgReturnValue), string.Empty);
        }

        public void OnDefaultBankAccountCodeKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F9)
            {
                SysMng.Instance.ActWndMng.ShowReport(
                    "Erp_BankAccountAccountCodeList",
                    true,
                    DefaultBankAccountListValueHandler,
                    new DlgArgs("RecId"),
                    null,
                    new PolicyParams { FieldName = "DefaultBankAccountCode" },
                    "WorkListW",
                    ReportWorkMode.ChoseList);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Tab || e.Key == Key.Enter)
            {
                if (sender is TextBox textBox && !string.IsNullOrWhiteSpace(textBox.Text))
                    ResolveDefaultBankAccount(0, textBox.Text.Trim());
                e.Handled = true;
            }
        }

        public void ResolveDefaultBankAccount(long recId, string accountCode)
        {
            LiveSession session = SysMng.Instance.getSession() as LiveSession;
            if (session?.ActiveCompany?.RecId == null) return;

            var sb = new StringBuilder();
            sb.AppendLine("select RecId, isnull(AccountCode,'') AccountCode, isnull(AccountName,'') AccountName");
            sb.AppendLine("from Erp_BankAccount with (nolock) where isnull(IsDeleted,0)=0");
            if (recId > 0)
                sb.AppendLine($"and RecId = {recId}");
            else
                sb.AppendLine($"and AccountCode = '{accountCode.Replace("'", "''")}' and CompanyId = {session.ActiveCompany.RecId.Value}");

            DataTable bankTable = UtilityFunctions.GetDataTableList(
                session._dbInfo.DBProvider,
                session._dbInfo.Connection,
                null,
                "Erp_BankAccount",
                sb.ToString());

            if (bankTable == null || bankTable.Rows.Count == 0)
            {
                _defaultBankAccountId = 0;
                DefaultBankAccountCode = accountCode;
                return;
            }

            DataRow bankRow = bankTable.Rows[0];
            _defaultBankAccountId = Convert.ToInt64(bankRow["RecId"]);
            DefaultBankAccountCode = bankRow["AccountCode"].ToString();
        }
    }
}
