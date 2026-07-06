using CashFlowManagementModule.BoExtensions;

using Prism.Ioc;

using Sentez.BankModule.PresentationModels;
using Sentez.Common;
using Sentez.Common.Commands;
using Sentez.Common.PresentationModels;
using Sentez.Common.Report;
using Sentez.Common.SystemServices;
using Sentez.Data.Tools;

using CashFlowManagementModule.Services;

using LiveCore.Desktop.UI.Controls;

using System;
using System.Data;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace CashFlowManagementModule.PresentationModels
{
    public class CollectionOrderBankReceiptPM : BankReceiptPM
    {
        const string CollectionOrderDefaultBankAccountCodeFieldName = "TxtCollectionOrderDefaultBankAccountCode";
        const string CollectionOrderAgingStartCurrentAccountCodeFieldName = "TxtCollectionOrderAgingStartCurrentAccountCode";
        const string CollectionOrderAgingEndCurrentAccountCodeFieldName = "TxtCollectionOrderAgingEndCurrentAccountCode";
        const string CollectionOrderAgingImportParamsPanelName = "CollectionOrderAgingImportParamsPanel";
        const string CollectionOrderAgingListFieldStart = "CollectionOrderAgingStartCurrentAccountCode";
        const string CollectionOrderAgingListFieldEnd = "CollectionOrderAgingEndCurrentAccountCode";
        const string CollectionOrderAgingImportSettingsPmName = "CollectionOrderBankReceiptPM";
        const string CollectionOrderAgingImportSettingsGroup = "AgingImportParams";

        public string DefaultBankAccountCode
        {
            get => _defaultBankAccountCode;
            set
            {
                string newValue = value ?? string.Empty;
                if (_defaultBankAccountCode == newValue) return;

                _defaultBankAccountCode = newValue;
                OnPropertyChanged(nameof(DefaultBankAccountCode));
                SaveAgingImportWindowSetting("DefaultBankAccountCode", newValue);
            }
        }

        public long DefaultBankAccountId => _defaultBankAccountId;

        public DateTime AgingReportDate
        {
            get => _agingReportDate;
            set
            {
                _agingReportDate = value;
                OnPropertyChanged(nameof(AgingReportDate));
            }
        }

        public string AgingStartCurrentAccountCode
        {
            get => _agingStartCurrentAccountCode;
            set
            {
                string newValue = value ?? string.Empty;
                if (_agingStartCurrentAccountCode == newValue) return;

                _agingStartCurrentAccountCode = newValue;
                OnPropertyChanged(nameof(AgingStartCurrentAccountCode));
                SaveAgingImportWindowSetting("AgingStartCurrentAccountCode", newValue);
            }
        }

        public string AgingEndCurrentAccountCode
        {
            get => _agingEndCurrentAccountCode;
            set
            {
                string newValue = value ?? string.Empty;
                if (_agingEndCurrentAccountCode == newValue) return;

                _agingEndCurrentAccountCode = newValue;
                OnPropertyChanged(nameof(AgingEndCurrentAccountCode));
                SaveAgingImportWindowSetting("AgingEndCurrentAccountCode", newValue);
            }
        }

        public bool ImportAgingDirectlyToReceipt
        {
            get => _importAgingDirectlyToReceipt;
            set
            {
                if (_importAgingDirectlyToReceipt == value) return;

                _importAgingDirectlyToReceipt = value;
                OnPropertyChanged(nameof(ImportAgingDirectlyToReceipt));
                SaveAgingImportWindowSetting("ImportAgingDirectlyToReceipt", value ? "1" : "0");
            }
        }

        string _defaultBankAccountCode = string.Empty;
        long _defaultBankAccountId;
        DateTime _agingReportDate = DateTime.Today;
        string _agingStartCurrentAccountCode = string.Empty;
        string _agingEndCurrentAccountCode = string.Empty;
        bool _importAgingDirectlyToReceipt;
        string _activeAgingListField;
        bool _agingImportSettingsLoaded;
        bool _loadingAgingImportSettings;

        public CollectionOrderBankReceiptPM(IContainerExtension container) : base(container)
        {
        }

        public void LoadAgingImportWindowSettings()
        {
            if (_agingImportSettingsLoaded) return;
            _agingImportSettingsLoaded = true;

            LiveSession session = SysMng.Instance.getSession() as LiveSession;
            if (session?.WindowSettings == null) return;

            _loadingAgingImportSettings = true;
            try
            {
                string startCode = session.WindowSettings.GetValue(
                    0, CollectionOrderAgingImportSettingsPmName, CollectionOrderAgingImportSettingsGroup, "AgingStartCurrentAccountCode");
                string endCode = session.WindowSettings.GetValue(
                    0, CollectionOrderAgingImportSettingsPmName, CollectionOrderAgingImportSettingsGroup, "AgingEndCurrentAccountCode");
                string bankCode = session.WindowSettings.GetValue(
                    0, CollectionOrderAgingImportSettingsPmName, CollectionOrderAgingImportSettingsGroup, "DefaultBankAccountCode");
                string bankIdValue = session.WindowSettings.GetValue(
                    0, CollectionOrderAgingImportSettingsPmName, CollectionOrderAgingImportSettingsGroup, "DefaultBankAccountId");
                string directImport = session.WindowSettings.GetValue(
                    0, CollectionOrderAgingImportSettingsPmName, CollectionOrderAgingImportSettingsGroup, "ImportAgingDirectlyToReceipt");

                if (!string.IsNullOrWhiteSpace(startCode))
                {
                    _agingStartCurrentAccountCode = startCode.Trim();
                    OnPropertyChanged(nameof(AgingStartCurrentAccountCode));
                }

                if (!string.IsNullOrWhiteSpace(endCode))
                {
                    _agingEndCurrentAccountCode = endCode.Trim();
                    OnPropertyChanged(nameof(AgingEndCurrentAccountCode));
                }

                if (long.TryParse(bankIdValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out long savedBankId)
                    && savedBankId > 0)
                {
                    ResolveDefaultBankAccount(savedBankId, string.Empty);
                }

                if (_defaultBankAccountId <= 0 && !string.IsNullOrWhiteSpace(bankCode))
                {
                    _defaultBankAccountCode = bankCode.Trim();
                    OnPropertyChanged(nameof(DefaultBankAccountCode));
                    ResolveDefaultBankAccount(0, bankCode.Trim());
                }

                if (bool.TryParse(directImport, out bool importDirectly))
                {
                    _importAgingDirectlyToReceipt = importDirectly;
                    OnPropertyChanged(nameof(ImportAgingDirectlyToReceipt));
                }
                else if (directImport == "1")
                {
                    _importAgingDirectlyToReceipt = true;
                    OnPropertyChanged(nameof(ImportAgingDirectlyToReceipt));
                }
            }
            finally
            {
                _loadingAgingImportSettings = false;
                if (_defaultBankAccountId > 0)
                {
                    session.WindowSettings.SetValue(
                        0,
                        CollectionOrderAgingImportSettingsPmName,
                        CollectionOrderAgingImportSettingsGroup,
                        "DefaultBankAccountId",
                        _defaultBankAccountId.ToString(CultureInfo.InvariantCulture));
                }
            }
        }

        public void RefreshDefaultBankAccountForImport()
        {
            EnsureDefaultBankAccountResolved();
        }

        public bool EnsureDefaultBankAccountResolved()
        {
            SyncDefaultBankAccountCodeFromView();

            if (_defaultBankAccountId > 0)
                return true;

            if (string.IsNullOrWhiteSpace(_defaultBankAccountCode))
                return false;

            ResolveDefaultBankAccount(0, _defaultBankAccountCode.Trim());
            return _defaultBankAccountId > 0;
        }

        void SyncDefaultBankAccountCodeFromView()
        {
            LiveLookUpEdit lookup = FindDefaultBankAccountLookup();
            if (lookup == null || string.IsNullOrWhiteSpace(lookup.Text))
                return;

            string code = BankAccountDefaultResolver.NormalizeDisplayCode(lookup.Text);
            if (BankAccountDefaultResolver.AreEquivalentCodes(_defaultBankAccountCode, code))
                return;

            _defaultBankAccountId = 0;
            _defaultBankAccountCode = code;
            OnPropertyChanged(nameof(DefaultBankAccountCode));
        }

        LiveLookUpEdit FindDefaultBankAccountLookup()
        {
            if (ActiveViewControl?.FindName(CollectionOrderDefaultBankAccountCodeFieldName) is LiveLookUpEdit lookup)
                return lookup;

            var gridDetail = FCtrl("gridDetail") as FrameworkElement;
            if (gridDetail?.Parent is FrameworkElement parent
                && parent.FindName(CollectionOrderAgingImportParamsPanelName) is FrameworkElement panel
                && panel.FindName(CollectionOrderDefaultBankAccountCodeFieldName) is LiveLookUpEdit panelLookup)
            {
                return panelLookup;
            }

            return null;
        }

        void SaveAgingImportWindowSetting(string key, string value)
        {
            if (_loadingAgingImportSettings) return;

            LiveSession session = SysMng.Instance.getSession() as LiveSession;
            session?.WindowSettings?.SetValue(
                0,
                CollectionOrderAgingImportSettingsPmName,
                CollectionOrderAgingImportSettingsGroup,
                key,
                value ?? string.Empty);
        }

        public void SyncAgingReportDateFromReceipt()
        {
            if (ActiveBO?.CurrentRow?.Row == null) return;
            if (!ActiveBO.CurrentRow.Row.Table.Columns.Contains("ReceiptDate")
                || ActiveBO.CurrentRow.Row.IsNull("ReceiptDate"))
            {
                AgingReportDate = DateTime.Today;
                return;
            }

            AgingReportDate = Convert.ToDateTime(ActiveBO.CurrentRow.Row["ReceiptDate"]).Date;
        }

        public override void OnListCommand(ISysCommandParam obj)
        {
            DependencyObject focusScope = FocusManager.GetFocusScope(ActiveViewControl);
            FrameworkElement focusedElement = FocusManager.GetFocusedElement(focusScope) as FrameworkElement;
            LiveLookUpEdit lookup = FindParentLiveLookUpEdit(focusedElement);

            if (lookup != null)
            {
                if (lookup.Name == CollectionOrderDefaultBankAccountCodeFieldName)
                {
                    ShowDefaultBankAccountList();
                    return;
                }

                if (lookup.Name == CollectionOrderAgingStartCurrentAccountCodeFieldName)
                {
                    ShowAgingCurrentAccountList(CollectionOrderAgingListFieldStart);
                    return;
                }

                if (lookup.Name == CollectionOrderAgingEndCurrentAccountCodeFieldName)
                {
                    ShowAgingCurrentAccountList(CollectionOrderAgingListFieldEnd);
                    return;
                }
            }

            base.OnListCommand(obj);
        }

        void ShowDefaultBankAccountList()
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
        }

        void ShowAgingCurrentAccountList(string fieldName)
        {
            _activeAgingListField = fieldName;
            SysMng.Instance.ActWndMng.ShowReport(
                "Erp_CurrentAccountCurrentAccountCodeList",
                true,
                AgingCurrentAccountListValueHandler,
                new DlgArgs("RecId"),
                null,
                new PolicyParams { FieldName = fieldName },
                "WorkListW",
                ReportWorkMode.ChoseList);
        }

        public void DefaultBankAccountListValueHandler(DlgArgs result)
        {
            if (result?.DlgReturnValue == null) return;
            ResolveDefaultBankAccount(Convert.ToInt64(result.DlgReturnValue), string.Empty);
        }

        public void AgingCurrentAccountListValueHandler(DlgArgs result)
        {
            if (result?.DlgReturnValue == null) return;
            ResolveAgingCurrentAccountCode(Convert.ToInt64(result.DlgReturnValue), string.Empty);
        }

        public void OnDefaultBankAccountCodeKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F9)
            {
                ShowDefaultBankAccountList();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Enter)
            {
                string accountCode = GetLookupEditorText(sender);
                if (!string.IsNullOrWhiteSpace(accountCode))
                    ResolveDefaultBankAccount(0, accountCode.Trim());

                MoveFocusToNextControl(sender);
                e.Handled = true;
            }
        }

        public void OnAgingStartCurrentAccountCodeKeyDown(object sender, KeyEventArgs e)
        {
            HandleAgingCurrentAccountCodeKeyDown(sender, e, CollectionOrderAgingListFieldStart, code => AgingStartCurrentAccountCode = code);
        }

        public void OnAgingEndCurrentAccountCodeKeyDown(object sender, KeyEventArgs e)
        {
            HandleAgingCurrentAccountCodeKeyDown(sender, e, CollectionOrderAgingListFieldEnd, code => AgingEndCurrentAccountCode = code);
        }

        void HandleAgingCurrentAccountCodeKeyDown(object sender, KeyEventArgs e, string listField, Action<string> setCode)
        {
            if (e.Key == Key.F9)
            {
                ShowAgingCurrentAccountList(listField);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Enter)
            {
                string accountCode = GetLookupEditorText(sender);
                if (!string.IsNullOrWhiteSpace(accountCode))
                    setCode(accountCode.Trim());

                MoveFocusToNextControl(sender);
                e.Handled = true;
            }
        }

        static string GetLookupEditorText(object sender)
        {
            if (sender is LiveLookUpEdit lookup)
                return lookup.Text;

            if (sender is TextBox textBox)
                return textBox.Text;

            if (sender is DependencyObject dependencyObject)
            {
                LiveLookUpEdit parentLookup = FindParentLiveLookUpEdit(dependencyObject);
                if (parentLookup != null)
                    return parentLookup.Text;
            }

            return string.Empty;
        }

        static void MoveFocusToNextControl(object sender)
        {
            if (sender is UIElement element)
                element.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
        }

        static LiveLookUpEdit FindParentLiveLookUpEdit(DependencyObject element)
        {
            while (element != null)
            {
                if (element is LiveLookUpEdit lookup)
                    return lookup;

                element = VisualTreeHelper.GetParent(element);
            }

            return null;
        }

        public void ResolveDefaultBankAccount(long recId, string accountCode)
        {
            LiveSession session = SysMng.Instance.getSession() as LiveSession;
            if (session?.ActiveCompany?.RecId == null) return;

            long resolvedId = BankAccountDefaultResolver.ResolveBankAccountId(session, recId, accountCode);
            if (resolvedId <= 0)
            {
                _defaultBankAccountId = 0;
                if (!string.IsNullOrWhiteSpace(accountCode))
                {
                    _defaultBankAccountCode = accountCode.Trim();
                    OnPropertyChanged(nameof(DefaultBankAccountCode));
                }

                return;
            }

            DataRow bankRow = BankAccountDefaultResolver.LoadBankAccountRow(session, resolvedId);
            if (bankRow == null)
            {
                _defaultBankAccountId = 0;
                return;
            }

            ApplyResolvedBankAccount(bankRow);
        }

        void ApplyResolvedBankAccount(DataRow bankRow)
        {
            _defaultBankAccountId = Convert.ToInt64(bankRow["RecId"]);
            _defaultBankAccountCode = Convert.ToString(bankRow["AccountCode"]) ?? string.Empty;
            OnPropertyChanged(nameof(DefaultBankAccountCode));
            SaveAgingImportWindowSetting(
                "DefaultBankAccountId",
                _defaultBankAccountId.ToString(CultureInfo.InvariantCulture));
            SaveAgingImportWindowSetting("DefaultBankAccountCode", _defaultBankAccountCode);
        }

        void ResolveAgingCurrentAccountCode(long recId, string accountCode)
        {
            LiveSession session = SysMng.Instance.getSession() as LiveSession;
            if (session?.ActiveCompany?.RecId == null) return;

            var sb = new StringBuilder();
            sb.AppendLine("select RecId, isnull(CurrentAccountCode,'') CurrentAccountCode");
            sb.AppendLine("from Erp_CurrentAccount with (nolock) where isnull(IsDeleted,0)=0");
            if (recId > 0)
                sb.AppendLine($"and RecId = {recId}");
            else
                sb.AppendLine($"and CurrentAccountCode = '{accountCode.Replace("'", "''")}'");

            DataTable accountTable = UtilityFunctions.GetDataTableList(
                session._dbInfo.DBProvider,
                session._dbInfo.Connection,
                null,
                "Erp_CurrentAccount",
                sb.ToString());

            string resolvedCode = accountCode;
            if (accountTable != null && accountTable.Rows.Count > 0)
                resolvedCode = accountTable.Rows[0]["CurrentAccountCode"].ToString();

            if (_activeAgingListField == CollectionOrderAgingListFieldEnd)
                AgingEndCurrentAccountCode = resolvedCode;
            else
                AgingStartCurrentAccountCode = resolvedCode;
        }
    }
}
