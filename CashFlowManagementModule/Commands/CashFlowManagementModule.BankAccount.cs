using CashFlowManagementModule.BoExtensions;
using CashFlowManagementModule.Services;

using LiveCore.Desktop.Common;
using LiveCore.Desktop.UI.Controls;

using Prism.Ioc;

using Sentez.Bank.PresentationModels;
using Sentez.Common;
using Sentez.Common.Commands;
using Sentez.Common.InformationMessages;
using Sentez.Common.PresentationModels;
using Sentez.Data.BusinessObjects;
using Sentez.Data.Tools;
using Sentez.Localization;

using System;
using System.Collections.Generic;
using System.Data;
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

        void RegisterBankAccountHooks()
        {
            BusinessObjectBase.AddCustomConstruction("BankAccountBO", BankAccountBo_CustomCons);
            BusinessObjectBase.AddCustomInit("BankAccountBO", BankAccountBo_Init);
            PMBase.AddCustomInit("BankAccountPM", BankAccountPm_Init);
            PMBase.AddCustomDispose("BankAccountPM", BankAccountPm_Dispose);
            PMBase.AddCustomViewLoaded("BankAccountPM", BankAccountPm_ViewLoaded);
        }

        void BankAccountBo_CustomCons(ref short itemId, ref string keyColumn, ref string typeField, ref string[] tables)
        {
            var tableList = new List<string>(tables);
            if (!tableList.Contains(BankAccountCreditCardHelper.PeriodTableName))
                tableList.Add(BankAccountCreditCardHelper.PeriodTableName);
            tables = tableList.ToArray();
        }

        void BankAccountBo_Init(BusinessObjectBase bo, BoParam parameter)
        {
            BankAccountCreditCardHelper.EnsureBankAccountMetaDataFields();

            bo.ValueFiller.AddRule(BankAccountCreditCardHelper.PeriodTableName, "InUse", 1);
            bo.ValueFiller.AddRule(BankAccountCreditCardHelper.PeriodTableName, "IsDeleted", 0);
            bo.ValueFiller.AddRule(BankAccountCreditCardHelper.PeriodTableName, "BankAccountId", "Erp_BankAccount", "RecId", BankAccountCreditCardHelper.BankAccountFkName);
            bo.ValueFiller.AddRule(BankAccountCreditCardHelper.PeriodTableName, "CompanyId", "Erp_BankAccount", "CompanyId", BankAccountCreditCardHelper.BankAccountFkName);
            bo.ValueFiller.AddRule(BankAccountCreditCardHelper.PeriodTableName, "IsDeleted", "Erp_BankAccount", "IsDeleted", BankAccountCreditCardHelper.BankAccountFkName);

            bo.Lookups.AddLookUp(BankAccountCreditCardHelper.PeriodTableName, "BankAccountId", true, "Erp_BankAccount", "AccountCode", "AccountCode", "AccountName", "AccountName");
        }

        void BankAccountPm_Init(PMBase pm, PmParam parameter)
        {
            _bankAccountPm = pm as BankAccountPM;
            if (_bankAccountPm == null)
                return;

            EnsureBankAccountCreditCardTab();
            UpdateBankAccountCreditCardTabVisibility();

            if (_bankAccountPm.ActiveBO != null)
                _bankAccountPm.ActiveBO.ColumnChanged += BankAccountPm_ActiveBO_ColumnChanged;

            if (!_bankAccountCreditCardCommandRegistered)
            {
                _bankAccountPm.CmdList.AddCmd(350, "GenerateCreditCardPeriodsCommand", SLanguage.GetString("Dönemleri Oluştur"), OnGenerateCreditCardPeriodsCommand, null);
                _bankAccountCreditCardCommandRegistered = true;
            }
        }

        void BankAccountPm_Dispose(PMBase pm, PmParam parameter)
        {
            if (_bankAccountPm?.ActiveBO != null)
                _bankAccountPm.ActiveBO.ColumnChanged -= BankAccountPm_ActiveBO_ColumnChanged;

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
            ApplyCreditCardPeriodGridFilter();
            UpdateBankAccountCreditCardTabVisibility();
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
                Header = SLanguage.GetString("Kredi Kartı Ekstre"),
                Visibility = Visibility.Collapsed
            };
            liveTabControl.Items.Add(_ltiBankAccountCreditCard);

            var pmDesktop = _bankAccountPm.container.Resolve<PMDesktop>();
            var creditCardView = pmDesktop.LoadXamlRes("BankAccountCreditCardViewW");
            if (creditCardView?._view is UserControl userControl)
            {
                userControl.DataContext = _bankAccountPm;
                _ltiBankAccountCreditCard.Content = userControl;
            }

            _bankAccountCreditCardTabInitialized = true;
        }

        void BankAccountPm_ActiveBO_ColumnChanged(object sender, DataColumnChangeEventArgs e)
        {
            if (e.Column?.ColumnName == "ForCreditCard")
                UpdateBankAccountCreditCardTabVisibility();
        }

        void UpdateBankAccountCreditCardTabVisibility()
        {
            if (_ltiBankAccountCreditCard == null || _bankAccountPm?.ActiveBO?.CurrentRow?.Row == null)
                return;

            var isCreditCard = ! _bankAccountPm.ActiveBO.CurrentRow.Row.IsNull("ForCreditCard") &&
                               Convert.ToBoolean(_bankAccountPm.ActiveBO.CurrentRow.Row["ForCreditCard"]);
            _ltiBankAccountCreditCard.Visibility = isCreditCard ? Visibility.Visible : Visibility.Collapsed;
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
            if (_bankAccountPm?.ActiveBO?.CurrentRow?.Row == null)
                return;

            var bankAccountRow = _bankAccountPm.ActiveBO.CurrentRow.Row;
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
                paymentDueDay);

            var applyError = CreditCardStatementPeriodGeneratorService.ApplyGeneratedPeriods(_bankAccountPm.ActiveBO as BusinessObjectBase, periods);
            if (!string.IsNullOrEmpty(applyError))
            {
                _sysMng.ActWndMng.ShowMsg(applyError, ConstantStr.Warning);
                return;
            }

            ApplyCreditCardPeriodGridFilter();
            _sysMng.ActWndMng.ShowMsg(
                string.Format(SLanguage.GetString("{0} adet ekstre dönemi oluşturuldu."), periods.Count),
                ConstantStr.Information);
        }
    }
}
