using CashFlowManagementModule.BoExtensions;
using CashFlowManagementModule.Services;

using DevExpress.Xpf.Grid;

using LiveCore.Desktop.Common;
using LiveCore.Desktop.UI.Controls;

using Prism.Ioc;

using Sentez.Bank.PresentationModels;
using Sentez.Common;
using Sentez.Common.Commands;
using Sentez.Common.InformationMessages;
using Sentez.Common.PresentationModels;
using Sentez.Common.SystemServices;
using Sentez.Data.BusinessObjects;
using Sentez.Data.Tools;
using Sentez.Localization;

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Sentez.CashFlowManagementModule
{
    public partial class CashFlowManagementModule
    {
        BankAccountPM _bankAccountPm;
        LiveTabItem _ltiBankAccountCreditCard;
        bool _bankAccountCreditCardTabInitialized;
        bool _bankAccountCreditCardCommandRegistered;
        bool _bankAccountCreditCardGridSyncInitialized;
        bool _suppressCreditCardPaymentDueSync;

        void RegisterBankAccountHooks()
        {
            BusinessObjectBase.AddCustomConstruction("BankAccountBO", BankAccountBo_CustomCons);
            BusinessObjectBase.AddCustomInit("BankAccountBO", BankAccountBo_Init);
            PMBase.AddCustomInit("BankAccountPM", BankAccountPm_Init);
            PMBase.AddCustomDispose("BankAccountPM", BankAccountPm_Dispose);
            PMBase.AddCustomViewLoaded("BankAccountPM", BankAccountPm_ViewLoaded);
            RegisterBankAccountPosHooks();
        }

        void BankAccountBo_CustomCons(ref short itemId, ref string keyColumn, ref string typeField, ref string[] tables)
        {
            var tableList = new List<string>(tables);
            if (!tableList.Contains(BankAccountCreditCardHelper.PeriodTableName))
                tableList.Add(BankAccountCreditCardHelper.PeriodTableName);
            ExtendBankAccountBoForPos(ref tableList);
            tables = tableList.ToArray();
        }

        void BankAccountBo_Init(BusinessObjectBase bo, BoParam parameter)
        {
            BankAccountCreditCardHelper.EnsureBankAccountMetaDataFields();
            ConfigureBankAccountBoForPos(bo);

            if (bo?.Data != null)
                CreditCardPaymentDueDaysSyncService.EnsureVirtualColumns(bo.Data);

            bo.ValueFiller.AddRule(BankAccountCreditCardHelper.PeriodTableName, "InUse", 1);
            bo.ValueFiller.AddRule(BankAccountCreditCardHelper.PeriodTableName, "IsDeleted", 0);
            bo.ValueFiller.AddRule(BankAccountCreditCardHelper.PeriodTableName, "BankAccountId", "Erp_BankAccount", "RecId", BankAccountCreditCardHelper.BankAccountFkName);
            bo.ValueFiller.AddRule(BankAccountCreditCardHelper.PeriodTableName, "CompanyId", "Erp_Bank", "CompanyId", "FK_Erp_BankAccount_Erp_Bank");
            bo.ValueFiller.AddRule(BankAccountCreditCardHelper.PeriodTableName, "IsDeleted", "Erp_BankAccount", "IsDeleted", BankAccountCreditCardHelper.BankAccountFkName);

            bo.Lookups.AddLookUp(BankAccountCreditCardHelper.PeriodTableName, "BankAccountId", true, "Erp_BankAccount", "AccountCode", "AccountCode", "AccountName", "AccountName");
        }

        void BankAccountPm_Init(PMBase pm, PmParam parameter)
        {
            _bankAccountPm = pm as BankAccountPM;
            if (_bankAccountPm == null)
                return;

            BankAccountSubTypeHelper.EnsureLookupList(_bankAccountPm.Lists);

            EnsureBankAccountCreditCardTab();
            UpdateBankAccountCreditCardTabVisibility();
            EnsureBankAccountCreditCardVisibilityHook();

            if (!_bankAccountCreditCardCommandRegistered)
            {
                _bankAccountPm.CmdList.AddCmd(350, "GenerateCreditCardPeriodsCommand", SLanguage.GetString("Dönemleri Oluştur"), OnGenerateCreditCardPeriodsCommand, null);
                _bankAccountCreditCardCommandRegistered = true;
            }
        }

        void BankAccountPm_Dispose(PMBase pm, PmParam parameter)
        {
            DetachCreditCardPeriodGridSync();

            _bankAccountPm = null;
            _ltiBankAccountCreditCard = null;
            _bankAccountCreditCardTabInitialized = false;
            _bankAccountCreditCardCommandRegistered = false;
        }

        void BankAccountPm_ViewLoaded(object sender, RoutedEventArgs e)
        {
            if (_bankAccountPm == null)
                _bankAccountPm = sender as BankAccountPM;
            if (_bankAccountPm == null)
                return;

            EnsureBankAccountCreditCardTab();
            EnsureBankAccountCreditCardVisibilityHook();
            ApplyCreditCardPeriodGridFilter();
            UpdateBankAccountCreditCardTabVisibility();
            EnsureCreditCardPeriodGridSync();
            RecalculateCreditCardPaymentDueDays();
            RefreshCreditCardPeriodPaymentSummary();
        }

        void EnsureBankAccountCreditCardTab()
        {
            if (_bankAccountCreditCardTabInitialized || _bankAccountPm == null)
                return;

            var liveTabControl = _bankAccountPm.FCtrl("GenelTab") as LiveTabControl;
            if (liveTabControl == null)
                return;

            _ltiBankAccountCreditCard = new LiveTabItem
            {
                Header = SLanguage.GetString("Kredi Kartı Detay Bilgileri"),
                Visibility = Visibility.Collapsed
            };
            liveTabControl.Items.Add(_ltiBankAccountCreditCard);

            var pmDesktop = _bankAccountPm.container.Resolve<PMDesktop>();
            var creditCardView = pmDesktop.LoadXamlRes("BankAccountCreditCardViewW");
            if (creditCardView?._view is UserControl userControl)
            {
                if (_bankAccountPm.ActiveBO?.Data != null)
                    CreditCardPaymentDueDaysSyncService.EnsureVirtualColumns(_bankAccountPm.ActiveBO.Data);

                userControl.DataContext = _bankAccountPm;
                userControl.IsVisibleChanged += BankAccountCreditCardView_IsVisibleChanged;
                _ltiBankAccountCreditCard.Content = userControl;
            }

            _bankAccountCreditCardTabInitialized = true;
        }

        void BankAccountCreditCardView_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is UIElement element && element.IsVisible)
                RefreshCreditCardPeriodPaymentSummary();
        }

        void EnsureCreditCardPeriodGridSync()
        {
            if (_bankAccountCreditCardGridSyncInitialized || _ltiBankAccountCreditCard?.Content is not UserControl userControl)
                return;

            if (userControl.FindName("gridCreditCardPeriods") is not LiveGridControl grid ||
                grid.View is not ReceiptView receiptView)
                return;

            receiptView.CellValueChanged -= CreditCardPeriodGrid_CellValueChanged;
            receiptView.CellValueChanged += CreditCardPeriodGrid_CellValueChanged;
            _bankAccountCreditCardGridSyncInitialized = true;
        }

        void DetachCreditCardPeriodGridSync()
        {
            if (!_bankAccountCreditCardGridSyncInitialized || _ltiBankAccountCreditCard?.Content is not UserControl userControl)
                return;

            if (userControl.FindName("gridCreditCardPeriods") is LiveGridControl grid &&
                grid.View is ReceiptView receiptView)
            {
                receiptView.CellValueChanged -= CreditCardPeriodGrid_CellValueChanged;
            }

            _bankAccountCreditCardGridSyncInitialized = false;
        }

        void CreditCardPeriodGrid_CellValueChanged(object sender, CellValueChangedEventArgs e)
        {
            if (e.Column?.FieldName == null || CreditCardPaymentDueDaysSyncService.IsSyncing)
                return;

            if (!BankAccountCreditCardSyncExtension.IsPeriodPaymentDueSyncColumn(e.Column.FieldName))
                return;

            if (e.Row is not DataRowView rowView || rowView.Row == null)
                return;

            try
            {
                _suppressCreditCardPaymentDueSync = true;
                CreditCardPaymentDueDaysSyncService.SyncPeriodOnColumnChange(rowView.Row, e.Column.FieldName);
            }
            finally
            {
                _suppressCreditCardPaymentDueSync = false;
            }
        }

        void EnsureBankAccountCreditCardVisibilityHook()
        {
            var bo = _bankAccountPm?.ActiveBO;
            if (bo == null)
                return;

            bo.ColumnChanged -= BankAccountPm_CreditCardTabVisibility_ColumnChanged;
            bo.ColumnChanged += BankAccountPm_CreditCardTabVisibility_ColumnChanged;
        }

        void BankAccountPm_CreditCardTabVisibility_ColumnChanged(object sender, DataColumnChangeEventArgs e)
        {
            if (e.Column?.ColumnName == "ForCreditCard" || e.Column?.ColumnName == "AccountSubType")
                UpdateBankAccountCreditCardTabVisibility();
        }

        void RecalculateCreditCardPaymentDueDays()
        {
            if (_bankAccountPm?.ActiveBO is not BusinessObjectBase bo)
                return;

            var extension = bo.Extensions?.Values.OfType<BankAccountCreditCardSyncExtension>().FirstOrDefault();
            if (extension != null)
            {
                extension.RecalculatePaymentDueDays();
                return;
            }

            if (bo.Data == null)
                return;

            CreditCardPaymentDueDaysSyncService.EnsureVirtualColumns(bo.Data);

            try
            {
                _suppressCreditCardPaymentDueSync = true;

                if (DataRowSafety.TryGetCurrentRow(bo, out DataRow headerRow))
                    CreditCardPaymentDueDaysSyncService.RecalculateHeaderDays(headerRow);

                if (bo.Data.Tables.Contains(BankAccountCreditCardHelper.PeriodTableName))
                    CreditCardPaymentDueDaysSyncService.RecalculateAllPeriodRows(
                        bo.Data.Tables[BankAccountCreditCardHelper.PeriodTableName]);
            }
            finally
            {
                _suppressCreditCardPaymentDueSync = false;
            }
        }

        void UpdateBankAccountCreditCardTabVisibility()
        {
            if (_ltiBankAccountCreditCard == null
                || !DataRowSafety.TryGetCurrentRow(_bankAccountPm?.ActiveBO, out DataRow headerRow))
                return;

            var showTab = BankAccountSubTypeHelper.ShouldShowCreditCardDetailTab(headerRow);
            _ltiBankAccountCreditCard.Visibility = showTab ? Visibility.Visible : Visibility.Collapsed;

            if (showTab)
                RefreshCreditCardPeriodPaymentSummary();
        }

        void ApplyCreditCardPeriodGridFilter()
        {
            if (_bankAccountPm?.ActiveBO?.Data?.Tables == null ||
                !_bankAccountPm.ActiveBO.Data.Tables.Contains(BankAccountCreditCardHelper.PeriodTableName))
                return;

            _bankAccountPm.ActiveBO.Data.Tables[BankAccountCreditCardHelper.PeriodTableName].DefaultView.RowFilter =
                "IsDeleted = 0 OR IsDeleted IS NULL";
        }

        void OnGenerateCreditCardPeriodsCommand(ISysCommandParam param)
        {
            if (!DataRowSafety.TryGetCurrentRow(_bankAccountPm?.ActiveBO, out DataRow bankAccountRow))
                return;
            if (!CreditCardStatementPeriodGeneratorService.TryParseInputs(
                    bankAccountRow,
                    out var expiryMonth,
                    out var expiryYear,
                    out var statementCutDay,
                    out var paymentDueDay,
                    out var errorMessage))
            {
                _sysMng.ActWndMng.ShowMsg(errorMessage, ConstantStr.Warning);
                return;
            }

            if (!_bankAccountPm.ActiveBO.Data.Tables.Contains(BankAccountCreditCardHelper.PeriodTableName))
            {
                _sysMng.ActWndMng.ShowMsg(SLanguage.GetString("Ekstre dönem tablosu yüklenemedi."), ConstantStr.Warning);
                return;
            }

            var periodTable = _bankAccountPm.ActiveBO.Data.Tables[BankAccountCreditCardHelper.PeriodTableName];
            if (CreditCardStatementPeriodGeneratorService.HasActivePeriods(periodTable))
            {
                if (_sysMng.ActWndMng.ShowMsgYesNo(
                    SLanguage.GetString("Bu karta ait mevcut ekstre dönem kayıtları silinip yeniden oluşturulacaktır. Devam etmek istiyor musunuz?"),
                    ConstantStr.Warning) != Sentez.Common.InformationMessages.MessageBoxResult.Yes)
                    return;

                var userId = (int)(_bankAccountPm.ActiveSession?.ActiveUser?.RecId ?? 0);
                CreditCardStatementPeriodGeneratorService.SoftDeleteActivePeriods(periodTable, userId, DateTime.Now);
            }

            var periods = CreditCardStatementPeriodGeneratorService.GeneratePeriods(
                expiryMonth,
                expiryYear,
                statementCutDay,
                paymentDueDay,
                BankAccountCreditCardHelper.GetIssueDate(bankAccountRow));

            var applyError = CreditCardStatementPeriodGeneratorService.ApplyGeneratedPeriods(_bankAccountPm.ActiveBO as BusinessObjectBase, periods);
            if (!string.IsNullOrEmpty(applyError))
            {
                _sysMng.ActWndMng.ShowMsg(applyError, ConstantStr.Warning);
                return;
            }

            try
            {
                _suppressCreditCardPaymentDueSync = true;
                CreditCardPaymentDueDaysSyncService.RecalculateAllPeriodRows(periodTable);
            }
            finally
            {
                _suppressCreditCardPaymentDueSync = false;
            }

            ApplyCreditCardPeriodGridFilter();
            RefreshCreditCardPeriodPaymentSummary();
            _sysMng.ActWndMng.ShowMsg(
                string.Format(SLanguage.GetString("{0} adet ekstre dönemi oluşturuldu."), periods.Count),
                ConstantStr.Information);
        }

        void RefreshCreditCardPeriodPaymentSummary()
        {
            if (_bankAccountPm?.ActiveBO is not BusinessObjectBase bo)
                return;

            var extension = bo.Extensions?.Values.OfType<BankAccountCreditCardSyncExtension>().FirstOrDefault();
            if (extension != null)
            {
                extension.RefreshPeriodPaymentSummary();
                return;
            }

            if (!DataRowSafety.TryGetCurrentRow(bo, out DataRow bankAccountRow))
                return;

            if (!BankAccountSubTypeHelper.ShouldShowCreditCardDetailTab(bankAccountRow))
                return;

            if (bankAccountRow.IsNull("RecId"))
                return;

            if (_bankAccountPm.ActiveSession is not LiveSession session)
                return;

            long bankAccountId = Convert.ToInt64(bankAccountRow["RecId"]);
            CreditCardPeriodPaymentSummaryService.RefreshSummary(session, bo.Data, bankAccountId);
            ApplyCreditCardPeriodGridFilter();
        }
    }
}
