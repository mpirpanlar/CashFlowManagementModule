using CashFlowManagementModule.BoExtensions;

using LiveCore.Desktop.Common;
using LiveCore.Desktop.UI.Controls;

using Prism.Ioc;

using Sentez.Common;
using Sentez.Common.Commands;
using Sentez.Common.PresentationModels;
using Sentez.Data.BusinessObjects;
using Sentez.Finance.PresentationModels;
using Sentez.Localization;

using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace Sentez.CashFlowManagementModule
{
    public partial class CashFlowManagementModule
    {
        CurrentAccountPM _currentAccountPm;
        LiveDocumentPanel _ldpFixedPayment;
        bool _currentAccountFixedPaymentTabInitialized;

        void RegisterCurrentAccountHooks()
        {
            BusinessObjectBase.AddCustomConstruction("CurrentAccountBO", CurrentAccountBo_CustomCons);
            BusinessObjectBase.AddCustomInit("CurrentAccountBO", CurrentAccountBo_Init);
            PMBase.AddCustomInit("CurrentAccountPM", CurrentAccountPm_Init);
            PMBase.AddCustomDispose("CurrentAccountPM", CurrentAccountPm_Dispose);
            PMBase.AddCustomViewLoaded("CurrentAccountPM", CurrentAccountPm_ViewLoaded);
        }

        void CurrentAccountBo_CustomCons(ref short itemId, ref string keyColumn, ref string typeField, ref string[] tables)
        {
            var tableList = new List<string>(tables);
            if (!tableList.Contains(CurrentAccountFixedPaymentHelper.ScheduleTableName))
                tableList.Add(CurrentAccountFixedPaymentHelper.ScheduleTableName);
            tables = tableList.ToArray();
        }

        void CurrentAccountBo_Init(BusinessObjectBase bo, BoParam parameter)
        {
            CurrentAccountFixedPaymentHelper.EnsureCurrentAccountMetaDataFields();
            CurrentAccountFixedPaymentHelper.EnsureFixedPaymentTypeLookups(bo);

            bo.ValueFiller.AddRule(CurrentAccountFixedPaymentHelper.ScheduleTableName, "InUse", 1);
            bo.ValueFiller.AddRule(CurrentAccountFixedPaymentHelper.ScheduleTableName, "IsDeleted", 0);
            bo.ValueFiller.AddRule(
                CurrentAccountFixedPaymentHelper.ScheduleTableName,
                "CurrentAccountId",
                "Erp_CurrentAccount",
                "RecId",
                CurrentAccountFixedPaymentHelper.CurrentAccountFkName);
            bo.ValueFiller.AddRule(
                CurrentAccountFixedPaymentHelper.ScheduleTableName,
                "CompanyId",
                "Erp_CurrentAccount",
                "CompanyId",
                CurrentAccountFixedPaymentHelper.CurrentAccountFkName);
            bo.ValueFiller.AddRule(
                CurrentAccountFixedPaymentHelper.ScheduleTableName,
                "IsDeleted",
                "Erp_CurrentAccount",
                "IsDeleted",
                CurrentAccountFixedPaymentHelper.CurrentAccountFkName);

            bo.Lookups.AddLookUp(
                CurrentAccountFixedPaymentHelper.ScheduleTableName,
                "CurrentAccountId",
                true,
                "Erp_CurrentAccount",
                "CurrentAccountCode",
                "CurrentAccountCode",
                "CurrentAccountName",
                "CurrentAccountName");
        }

        void CurrentAccountPm_Init(PMBase pm, PmParam parameter)
        {
            _currentAccountPm = pm as CurrentAccountPM;
            if (_currentAccountPm == null) return;

            EnsureCurrentAccountFixedPaymentTab();
        }

        void EnsureCurrentAccountFixedPaymentLookups()
        {
            if (_currentAccountPm?.Lists == null) return;

            var session = SysMng.Instance.getSession();
            if (session?.LookupList != null)
                MetaFixedPaymentTypeHelper.RefreshLookupList(session.LookupList);

            MetaFixedPaymentTypeHelper.RefreshLookupList(_currentAccountPm.Lists);
            SyncFixedPaymentGridLookups();
        }

        void SyncFixedPaymentGridLookups()
        {
            if (_ldpFixedPayment?.Content is not UserControl view || _currentAccountPm?.Lists == null)
                return;

            if (view.FindName("gridFixedPaymentSchedule") is LiveGridControl grid)
                grid.Lookups = _currentAccountPm.Lists;
        }

        void CurrentAccountPm_Dispose(PMBase pm, PmParam parameter)
        {
            _currentAccountPm = null;
            _ldpFixedPayment = null;
            _currentAccountFixedPaymentTabInitialized = false;
        }

        void CurrentAccountPm_ViewLoaded(object sender, RoutedEventArgs e)
        {
            if (_currentAccountPm == null)
                _currentAccountPm = sender as CurrentAccountPM;
            if (_currentAccountPm == null) return;

            EnsureCurrentAccountFixedPaymentLookups();
            EnsureCurrentAccountFixedPaymentTab();
            EnsureCurrentAccountFixedPaymentLookups();
        }

        void EnsureCurrentAccountFixedPaymentTab()
        {
            if (_currentAccountFixedPaymentTabInitialized || _currentAccountPm == null)
                return;

            var documentGroup = _currentAccountPm.FCtrl("GenelDocumentPanel") as LiveDocumentGroup;
            if (documentGroup == null)
                return;

            _ldpFixedPayment = new LiveDocumentPanel
            {
                Name = "LdpFixedPayment",
                Caption = SLanguage.GetString("Tekrar Eden Ödeme Tanımları")
            };
            documentGroup.Items.Add(_ldpFixedPayment);

            var pmDesktop = _currentAccountPm.container.Resolve<PMDesktop>();
            var fixedPaymentView = pmDesktop.LoadXamlRes("CurrentAccountFixedPaymentViewW");
            if (fixedPaymentView?._view is UserControl userControl)
            {
                userControl.DataContext = _currentAccountPm;
                _ldpFixedPayment.Content = userControl;
                SyncFixedPaymentGridLookups();
            }

            _currentAccountFixedPaymentTabInitialized = true;
        }
    }
}
