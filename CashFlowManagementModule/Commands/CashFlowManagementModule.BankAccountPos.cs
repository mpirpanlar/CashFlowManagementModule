using CashFlowManagementModule.BoExtensions;
using CashFlowManagementModule.Services;

using LiveCore.Desktop.Common;
using LiveCore.Desktop.UI.Controls;

using Prism.Ioc;

using Sentez.Bank.PresentationModels;
using Sentez.Common.Commands;
using Sentez.Common.PresentationModels;
using Sentez.Common.SystemServices;
using Sentez.Data.BusinessObjects;
using Sentez.Localization;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Sentez.CashFlowManagementModule
{
    public partial class CashFlowManagementModule
    {
        BankAccountPM _bankAccountPosPmContext;
        LiveTabItem _ltiBankAccountPos;
        bool _bankAccountPosTabInitialized;

        void RegisterBankAccountPosHooks()
        {
            PMBase.AddCustomInit("BankAccountPM", BankAccountPosPm_Init);
            PMBase.AddCustomDispose("BankAccountPM", BankAccountPosPm_Dispose);
            PMBase.AddCustomViewLoaded("BankAccountPM", BankAccountPosPm_ViewLoaded);
        }

        void ExtendBankAccountBoForPos(ref List<string> tableList)
        {
            if (!tableList.Contains(BankAccountPosHelper.DeductionProfileTableName))
                tableList.Add(BankAccountPosHelper.DeductionProfileTableName);
        }

        void ConfigureBankAccountBoForPos(BusinessObjectBase bo)
        {
            BankAccountPosHelper.EnsureBankAccountMetaDataFields();
            BankAccountPosHelper.EnsureStatementViewProfileLookup(_bankAccountPm?.Lists ?? ActiveSession?.LookupList);
            PosCardClassificationHelper.EnsureLookups(_bankAccountPm?.Lists ?? ActiveSession?.LookupList);

            if (bo?.Data != null)
                BankAccountPosHelper.EnsureBankAccountDataColumns(bo.Data);

            bo.ValueFiller.AddRule(BankAccountPosHelper.DeductionProfileTableName, "InUse", 1);
            bo.ValueFiller.AddRule(BankAccountPosHelper.DeductionProfileTableName, "IsDeleted", 0);
            bo.ValueFiller.AddRule(BankAccountPosHelper.DeductionProfileTableName, "CalculationBase", BankAccountPosHelper.CalculationBaseGross);
            bo.ValueFiller.AddRule(BankAccountPosHelper.DeductionProfileTableName, "BankAccountId", "Erp_BankAccount", "RecId", BankAccountPosHelper.BankAccountFkName);
            bo.ValueFiller.AddRule(BankAccountPosHelper.DeductionProfileTableName, "CompanyId", "Erp_Bank", "CompanyId", "FK_Erp_BankAccount_Erp_Bank");
            bo.ValueFiller.AddRule(BankAccountPosHelper.DeductionProfileTableName, "IsDeleted", "Erp_BankAccount", "IsDeleted", BankAccountPosHelper.BankAccountFkName);

            bo.Lookups.AddLookUp(BankAccountPosHelper.DeductionProfileTableName, "DeductionTypeId", true, MetaPosDeductionTypeHelper.TableName, "PosDeductionTypeCode", "PosDeductionTypeCode", "PosDeductionTypeName", "PosDeductionTypeName");
            bo.Lookups.AddLookUp(BankAccountPosHelper.DeductionProfileTableName, "BankAccountId", true, "Erp_BankAccount", "AccountCode", "AccountCode", "AccountName", "AccountName");

            bo.BeforePost -= BankAccountBo_BeforePost_PosDeductionProfile;
            bo.BeforePost += BankAccountBo_BeforePost_PosDeductionProfile;
        }

        void BankAccountBo_BeforePost_PosDeductionProfile(object sender, CancelEventArgs e)
        {
            if (sender is not BusinessObjectBase bo || bo.Data == null)
                return;

            if (!bo.Data.Tables.Contains(BankAccountPosHelper.DeductionProfileTableName)
                || !bo.Data.Tables.Contains("Erp_BankAccount"))
                return;

            if (!DataRowSafety.TryGetCurrentRow(bo, out DataRow bankAccountRow) || bankAccountRow.IsNull("RecId"))
                return;

            long bankAccountId = Convert.ToInt64(bankAccountRow["RecId"]);
            int companyId = (bo.ActiveSession as LiveSession)?.ActiveCompany?.RecId ?? 0;
            DataTable profileTable = bo.Data.Tables[BankAccountPosHelper.DeductionProfileTableName];

            foreach (DataRow profileRow in profileTable.Rows.Cast<DataRow>().Where(DataRowSafety.IsUsable))
            {
                if (!profileRow.IsNull("IsDeleted") && Convert.ToBoolean(profileRow["IsDeleted"]))
                    continue;

                if (profileRow.IsNull("BankAccountId"))
                    profileRow["BankAccountId"] = bankAccountId;

                if (companyId > 0 && profileRow.IsNull("CompanyId"))
                    profileRow["CompanyId"] = companyId;

                if (profileRow.IsNull("InUse"))
                    profileRow["InUse"] = true;

                if (profileRow.IsNull("IsDeleted"))
                    profileRow["IsDeleted"] = false;

                if (profileRow.IsNull("CalculationBase"))
                    profileRow["CalculationBase"] = BankAccountPosHelper.CalculationBaseGross;
            }
        }

        void BankAccountPosPm_Init(PMBase pm, PmParam parameter)
        {
            _bankAccountPosPmContext = pm as BankAccountPM;
            if (_bankAccountPosPmContext == null)
                return;

            EnsurePosDeductionProfileLookups();
            EnsureBankAccountPosTab();
            UpdateBankAccountPosTabVisibility();
            EnsureBankAccountPosVisibilityHook();
        }

        void BankAccountPosPm_Dispose(PMBase pm, PmParam parameter)
        {
            _bankAccountPosPmContext = null;
            _ltiBankAccountPos = null;
            _bankAccountPosTabInitialized = false;
        }

        void BankAccountPosPm_ViewLoaded(object sender, RoutedEventArgs e)
        {
            if (_bankAccountPosPmContext == null)
                _bankAccountPosPmContext = sender as BankAccountPM;
            if (_bankAccountPosPmContext == null)
                return;

            EnsureBankAccountPosTab();
            EnsureBankAccountPosVisibilityHook();
            EnsurePosDeductionProfileLookups();
            ApplyPosDeductionProfileGridFilter();
            UpdateBankAccountPosTabVisibility();
        }

        void EnsurePosDeductionProfileLookups()
        {
            BankAccountPM pm = _bankAccountPosPmContext ?? _bankAccountPm;
            if (pm?.Lists == null)
                return;

            var session = SysMng.Instance.getSession();
            if (session?.LookupList != null)
            {
                BankAccountPosHelper.EnsureStatementViewProfileLookup(session.LookupList);
                BankAccountPosHelper.EnsureCalculationBaseLookup(session.LookupList);
                MetaPosDeductionTypeHelper.RefreshLookupList(session.LookupList);
            }

            BankAccountPosHelper.EnsureStatementViewProfileLookup(pm.Lists);
            BankAccountPosHelper.EnsureCalculationBaseLookup(pm.Lists);
            MetaPosDeductionTypeHelper.RefreshLookupList(pm.Lists);
            SyncPosDeductionProfileGridLookups();
        }

        void SyncPosDeductionProfileGridLookups()
        {
            BankAccountPM pm = _bankAccountPosPmContext ?? _bankAccountPm;
            if (_ltiBankAccountPos?.Content is not UserControl view || pm?.Lists == null)
                return;

            if (view.FindName("gridPosDeductionProfile") is LiveGridControl grid)
                grid.Lookups = pm.Lists;
        }

        void EnsureBankAccountPosTab()
        {
            BankAccountPM pm = _bankAccountPosPmContext ?? _bankAccountPm;
            if (_bankAccountPosTabInitialized || pm == null)
                return;

            var liveTabControl = pm.FCtrl("GenelTab") as LiveTabControl;
            if (liveTabControl == null)
                return;

            _ltiBankAccountPos = new LiveTabItem
            {
                Header = SLanguage.GetString("Üye İş Yeri Ekstre Bilgileri"),
                Visibility = Visibility.Collapsed
            };
            liveTabControl.Items.Add(_ltiBankAccountPos);

            var pmDesktop = pm.container.Resolve<PMDesktop>();
            var posView = pmDesktop.LoadXamlRes("BankAccountPosViewW");
            if (posView?._view is UserControl userControl)
            {
                if (pm.ActiveBO?.Data != null)
                    BankAccountPosHelper.EnsureBankAccountDataColumns(pm.ActiveBO.Data);

                userControl.DataContext = pm;
                _ltiBankAccountPos.Content = userControl;
            }

            _bankAccountPosTabInitialized = true;
        }

        void EnsureBankAccountPosVisibilityHook()
        {
            BankAccountPM pm = _bankAccountPosPmContext ?? _bankAccountPm;
            var bo = pm?.ActiveBO;
            if (bo == null)
                return;

            bo.ColumnChanged -= BankAccountPm_PosTabVisibility_ColumnChanged;
            bo.ColumnChanged += BankAccountPm_PosTabVisibility_ColumnChanged;
        }

        void BankAccountPm_PosTabVisibility_ColumnChanged(object sender, DataColumnChangeEventArgs e)
        {
            if (e.Column?.ColumnName == "AccountSubType")
                UpdateBankAccountPosTabVisibility();
        }

        void UpdateBankAccountPosTabVisibility()
        {
            BankAccountPM pm = _bankAccountPosPmContext ?? _bankAccountPm;
            if (_ltiBankAccountPos == null || !DataRowSafety.TryGetCurrentRow(pm?.ActiveBO, out DataRow headerRow))
                return;

            bool showTab = BankAccountSubTypeHelper.ShouldShowPosDetailTab(headerRow);
            _ltiBankAccountPos.Visibility = showTab ? Visibility.Visible : Visibility.Collapsed;
        }

        void ApplyPosDeductionProfileGridFilter()
        {
            BankAccountPM pm = _bankAccountPosPmContext ?? _bankAccountPm;
            if (pm?.ActiveBO?.Data?.Tables == null ||
                !pm.ActiveBO.Data.Tables.Contains(BankAccountPosHelper.DeductionProfileTableName))
                return;

            pm.ActiveBO.Data.Tables[BankAccountPosHelper.DeductionProfileTableName].DefaultView.RowFilter =
                "IsDeleted = 0 OR IsDeleted IS NULL";
        }
    }
}
