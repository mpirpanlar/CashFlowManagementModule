using LiveCore.Desktop.SBase.MenuManager;
using Sentez.Common;
using Sentez.Common.Commands;
using Sentez.Common.ModuleBase;
using Sentez.Common.ResourceManager;
using Sentez.Common.SystemServices;
using Sentez.Data.MetaData.DatabaseControl;
using System;
using System.IO;
using System.Reflection;
using Sentez.Data.MetaData;
using Sentez.Data.Tools;
using Sentez.Localization;
using LiveCore.Desktop.Common;
using Prism.Ioc;
using Sentez.Common.SBase;
using Sentez.Core.ParameterClasses;
using Sentez.Parameters;
using Sentez.Common.PresentationModels;
using Microsoft.Office.Interop.Outlook;
using System.Windows;
using CashFlowManagementModule.Services;
using Sentez.FinanceModule.Reports;
using Sentez.FinanceModule.WorkList;
using Sentez.Common.Report;
using Sentez.Common.Utilities;
using Sentez.Data.BusinessObjects;
using Sentez.FinanceModule.Models;
using Sentez.Finance.PresentationModels;
using CashFlowManagementModule.BoExtensions;
using CashFlowManagementModule.Models;
using CashFlowManagementModule.PresentationModels;
using CashFlowManagementModule.WorkList;

namespace Sentez.CashFlowManagementModule
{
    public partial class CashFlowManagementModule : LiveModule
    {
        //Deneme değişiklik
        IContainerExtension _container;
        SysMng _sysMng;
        bool _bankReceiptApprovedChangeCommandRegistered;
        LiveSession ActiveSession
        {
            get
            {
                return SysMng.Instance.getSession();
            }
        }

        public Stream _MenuDefination = null;
        public override Stream MenuDefination
        {
            get
            {
                return _MenuDefination;
            }
        }

        public override short moduleID { get { return (short)Modules.ExternalModule16; } }

        public CashFlowManagementModule(IContainerExtension container)
        {
            _container = container;
            _sysMng = _container.Resolve<SysMng>();
            if (_sysMng != null)
            {
                _sysMng.AfterDesktopLogin += _sysMng_AfterDesktopLogin;
            }

            SLanguage.ActiveLanguageChanged += _sLanguage_ActiveLanguageChanged;
        }

        void _sLanguage_ActiveLanguageChanged(object sender, EventArgs e)
        {
            PaymentOrderTerminology.ApplyBankReceiptTypeDisplayNameAndRefreshMenus();
            CollectionOrderTerminology.ApplyBankReceiptTypeDisplayNameAndRefreshMenus();
            EnsureBankAccountSubTypeLookup();
        }

        public override void OnRegister(IContainerRegistry containerRegistry)
        {
            RegisterCoreDocuments();
            RegisterBO();
            RegisterViews();
            RegisterRes();
            RegisterRpr();
            RegisterPM();
            RegisterModuleCommands();
            RegisterServices();
            RegisterList();
            RegisterBoExtensions();
            RegisterBankReceiptBoHooks();
            RegisterBankReceiptPmHooks();
            RegisterBankAccountHooks();
            RegisterCurrentAccountHooks();
            RegisterCurrentAccountReceiptHooks();
            RegisterCardPmHooks();
            CashFlowManagementModuleSecurity.RegisterSecurityDefinitions();

            MenuManager.Instance.RegisterMenu("CashFlowManagementModule", "CashFlowManagementModuleMenu", moduleID, true);
            EnsureBankAccountSubTypeLookup();
        }

        public override void OnInitialize(IContainerProvider containerProvider)
        {
            _sysMng.AddApplication("CashFlowManagementModule");
            _container.Register<IPMBase, BankReceiptPmFactory>("BankReceiptPM");
            EnsureBankReceiptApprovedChangeCommandRegistered();
            CollectionOrderReceiptTypeRegistration.RegisterCollectionOrderReceiptType();
            PaymentOrderTerminology.ApplyBankReceiptTypeDisplayNameAndRefreshMenus();
            CollectionOrderTerminology.ApplyBankReceiptTypeDisplayNameAndRefreshMenus();
            EnsureGlobalFixedPaymentTypeLookup();
            EnsureGlobalPosDeductionTypeLookup();
            EnsureBankAccountSubTypeLookup();
        }

        public override void RegisterModuleCommands()
        {
        }

        private void _sysMng_AfterDesktopLogin(object sender, EventArgs e)
        {
            EnsureBankReceiptApprovedChangeCommandRegistered();
            CollectionOrderReceiptTypeRegistration.RegisterCollectionOrderReceiptType();
            PaymentOrderTerminology.ApplyBankReceiptTypeDisplayNameAndRefreshMenus();
            CollectionOrderTerminology.ApplyBankReceiptTypeDisplayNameAndRefreshMenus();
            EnsureGlobalFixedPaymentTypeLookup();
            EnsureGlobalPosDeductionTypeLookup();
            EnsureBankAccountSubTypeLookup();
        }

        void EnsureGlobalPosDeductionTypeLookup()
        {
            if (ActiveSession?.LookupList == null)
                return;

            MetaPosDeductionTypeHelper.EnsureLookupList(ActiveSession.LookupList);
            BankAccountPosHelper.EnsureStatementViewProfileLookup(ActiveSession.LookupList);
            BankAccountPosHelper.EnsureCalculationBaseLookup(ActiveSession.LookupList);
            PosCardClassificationHelper.EnsureLookups(ActiveSession.LookupList);
            PosCardClassificationHelper.EnsureCurrentAccountReceiptItemMetaDataFields();
            PosCardClassificationHelper.EnsureBankReceiptItemMetaDataFields();
        }

        void EnsureGlobalFixedPaymentTypeLookup()
        {
            if (ActiveSession?.LookupList == null) return;
            MetaFixedPaymentTypeHelper.EnsureLookupList(ActiveSession.LookupList);
        }

        void EnsureBankAccountSubTypeLookup()
        {
            BankAccountSubTypeHelper.EnsureLookupList(LookupList.Instance);
            BankAccountSubTypeHelper.EnsureLookupList(ActiveSession?.LookupList);
        }

        public void Initialize()
        {
        }

        private void RegisterBO()
        {
            ParameterFactory.StaticFactory.RegisterParameterClass(typeof(CrsParameters), (int)Modules.ExternalModule16);
            _container.Register<IBusinessObject, AgingReportResultsListBO>("AgingReportResultsListBO");
            _container.Register<IBusinessObject, MetaFixedPaymentTypeBO>("MetaFixedPaymentTypeBO");
            _container.Register<IBusinessObject, MetaPosDeductionTypeBO>("MetaPosDeductionTypeBO");
        }

        private void RegisterBoExtensions()
        {
            BusinessObjectBase.AddCustomExtension("BankReceiptBO", typeof(BankReceiptPaymentOrderWorkPeriodExtension));
            BusinessObjectBase.AddCustomExtension("BankReceiptBO", typeof(BankReceiptPaymentOrderControlExtension));
            BusinessObjectBase.AddCustomExtension("BankReceiptBO", typeof(BankReceiptCollectionOrderWorkPeriodExtension));
            BusinessObjectBase.AddCustomExtension("BankReceiptBO", typeof(BankReceiptCollectionOrderControlExtension));
            BusinessObjectBase.AddCustomExtension("BankAccountBO", typeof(BankAccountCreditCardSyncExtension));
        }

        private void RegisterServices()
        {
            _container.Register<ISystemService, CreatMetaDataFieldsService>("CreatMetaDataFieldsService");
        }

        private void RegisterRes()
        {
            ResMng.AddRes("CashFlowManagementModuleMenu", "CashFlowManagementModule;component/ModuleMenu.xml", ResSource.Resource, ResourceType.MenuXml, Modules.ExternalModule16, 0, 0);
        }

        private void RegisterList()
        {
            _container.Register<IReport, CurrentAccountDebitDistributionList>("CurrentAccountDebitDistributionList");
            _container.Register<IReport, MetaFixedPaymentTypeList>(MetaFixedPaymentTypeHelper.ListName);
            _container.Register<IReport, MetaPosDeductionTypeList>(MetaPosDeductionTypeHelper.ListName);
        }

        private void RegisterViews()
        {
            ResMng.AddRes("AgingReportResultsListW", "/CashFlowManagementModule;component/Views/AgingReportResultsListView.xaml", ResSource.Resource, ResourceType.View, Modules.ExternalModule16, 0, 0);
            ResMng.AddRes("AgingReportResultsListManagementW", "/CashFlowManagementModule;component/Views/AgingReportResultsListManagementView.xaml", ResSource.Resource, ResourceType.View, Modules.ExternalModule16, 0, 0);
            ResMng.AddRes("BankAccountCreditCardViewW", "/CashFlowManagementModule;component/Views/BankAccountCreditCardView.xaml", ResSource.Resource, ResourceType.View, Modules.ExternalModule16, 0, 0);
            ResMng.AddRes("CreditCardDetailAnalysisViewW", "/CashFlowManagementModule;component/Views/CreditCardDetailAnalysisView.xaml", ResSource.Resource, ResourceType.View, Modules.ExternalModule16, 0, 0);
            ResMng.AddRes("PaymentOrderAgingImportPreviewViewW", "/CashFlowManagementModule;component/Views/PaymentOrderAgingImportPreviewView.xaml", ResSource.Resource, ResourceType.View, Modules.ExternalModule16, 0, 0);
            ResMng.AddRes("CollectionOrderAgingImportPreviewViewW", "/CashFlowManagementModule;component/Views/CollectionOrderAgingImportPreviewView.xaml", ResSource.Resource, ResourceType.View, Modules.ExternalModule16, 0, 0);
            ResMng.AddRes("CurrentAccountFixedPaymentViewW", "/CashFlowManagementModule;component/Views/CurrentAccountFixedPaymentView.xaml", ResSource.Resource, ResourceType.View, Modules.ExternalModule16, 0, 0);
            ResMng.AddRes("MetaFixedPaymentType", "/CashFlowManagementModule;component/Views/MetaFixedPaymentType.xaml", ResSource.Resource, ResourceType.View, Modules.ExternalModule16, 0, 0);
            ResMng.AddRes("BankAccountPosViewW", "/CashFlowManagementModule;component/Views/BankAccountPosView.xaml", ResSource.Resource, ResourceType.View, Modules.ExternalModule16, 0, 0);
            ResMng.AddRes("MetaPosDeductionType", "/CashFlowManagementModule;component/Views/MetaPosDeductionType.xaml", ResSource.Resource, ResourceType.View, Modules.ExternalModule16, 0, 0);
            ResMng.AddRes("PosStatementAnalysisViewW", "/CashFlowManagementModule;component/Views/PosStatementAnalysisView.xaml", ResSource.Resource, ResourceType.View, Modules.ExternalModule16, 0, 0);
        }

        private void RegisterPM()
        {
            _container.Register<IPMBase, BankReceiptPmFactory>("BankReceiptPM");
            _container.Register<IPMBase, AgingReportResultsListPM>("AgingReportResultsListPM");
            _container.Register<IPMBase, AgingReportResultsManagementPM>("AgingReportResultsManagementPM");
            _container.Register<IPMBase, CreditCardDetailAnalysisPM>("CreditCardDetailAnalysisPM");
            _container.Register<IPMBase, PaymentOrderAgingImportPreviewPM>("PaymentOrderAgingImportPreviewPM");
            _container.Register<IPMBase, CollectionOrderAgingImportPreviewPM>("CollectionOrderAgingImportPreviewPM");
            _container.Register<IPMBase, PosStatementAnalysisPM>("PosStatementAnalysisPM");
        }

        private void RegisterRpr()
        {
            //_container.Register<IReport, SalesShipmentComparePolicy>("SalesShipmentComparePolicy");
            _container.Register<IReport, AgingReportResultsList>("Erp_AgingReportResultsReportNoList");
        }

        public void RegisterCoreDocuments()
        {
            Data.MetaData.Schema.ReadXml(Assembly.GetAssembly(typeof(CashFlowManagementModule)).GetManifestResourceStream("CashFlowManagementModule.CashFlowManagementModuleDataSchema.xml"));
            DbCreator.AddRegistration((int)Modules.ExternalModule16, CashFlowManagementModuleDbUpdateScript);
        }

        DbScripts CashFlowManagementModuleDbUpdateScript(DbCreator instance)
        {
            return DbScripts.LoadFromAssembly(Assembly.GetAssembly(typeof(CashFlowManagementModule)), "CashFlowManagementModule.CashFlowManagementModuleDbUpdateScripts.xml");
        }
    }
}
