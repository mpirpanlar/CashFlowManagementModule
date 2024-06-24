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
using LodgingNetCoreManagementModule.Services;
using Sentez.Data.MetaData;
using Sentez.Data.Tools;
using Sentez.Localization;
using LiveCore.Desktop.Common;
using Prism.Ioc;
using Sentez.Common.SBase;
using Sentez.LodgingNetCoreManagementModule.Models;
using Sentez.Data.BusinessObjects;
using Sentez.CRSModule.Services;
using Sentez.Core.ParameterClasses;
using Sentez.CRSModule.Models;
using Sentez.Parameters;
using LodgingManagementModule.BoExtensions;
using Sentez.Common.PresentationModels;
using Sentez.CRSUIModule.PresentationModels;
using Sentez.CRSModule.WorkList;
using Sentez.Common.Report;
using System.Windows.Input;
//using DevExpress.XtraScheduler.Outlook.Interop;
using Microsoft.Office.Interop.Outlook;
using System.Windows;

namespace Sentez.LodgingNetCoreManagementModule
{
    public partial class LodgingNetCoreManagementModule : LiveModule
    {
        //Deneme değişiklik
        IContainerExtension _container;
        SysMng _sysMng;
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

        public override short moduleID { get { return (short)Modules.ExternalModule15; } }

        public LodgingNetCoreManagementModule(IContainerExtension container)
        {
            _container = container;
            _sysMng = _container.Resolve<SysMng>();
            if (_sysMng != null)
            {
                _sysMng.AfterDesktopLogin += _sysMng_AfterDesktopLogin;
            }
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
            LodgingNetCoreManagementModuleSecurity.RegisterSecurityDefinitions();

            MenuManager.Instance.RegisterMenu("LodgingNetCoreManagementModule", "LodgingNetCoreManagementModuleMenu", moduleID, true);
        }

        public override void OnInitialize(IContainerProvider containerProvider)
        {
            _sysMng.AddApplication("LodgingNetCoreManagementModule");
        }

        public override void RegisterModuleCommands()
        {
            RegisterModuleCommands_CurrentAccount();
        }

        private void _sysMng_AfterDesktopLogin(object sender, EventArgs e)
        {
            if (!_sysMng.getSession().ServiceReferences.ContainsKey("GetAgileGlobalTodayDateHelperService"))
                _sysMng.getSession().ServiceReferences.Add("GetAgileGlobalTodayDateHelperService", _container.Resolve<GetAgileGlobalTodayDateHelperService>());
        }

        public void Initialize()
        {
        }

        private void RegisterBO()
        {
            _container.Register<IBusinessObject, CrsResourceBO>("CrsResourceBO");
            _container.Register<IBusinessObject, CrsBedTypeBO>("CrsBedTypeBO");
            _container.Register<IBusinessObject, LogTransactionTuruncBO>("LogTransactionTuruncBO");
            ParameterFactory.StaticFactory.RegisterParameterClass(typeof(CrsParameters), (int)Modules.ExternalModule15);
        }

        private void RegisterServices()
        {
            _container.Register<ISystemService, CreatMetaDataFieldsService>("CreatMetaDataFieldsService");

            _container.Register<ISystemService, GetBedTypeService>("GetBedTypeService");
            _container.Register<ISystemService, GetRoomTypeService>("GetRoomTypeService");
            _container.Register<ISystemService, GetResourceAttributeSetItemTableService>("GetResourceAttributeSetItemTableService");
            _container.Register<ResourceAttributeModel, ResourceAttributeModel>("ResourceAttributeModel");
            _container.Register<ISystemService, GetResourceAttributeSetItemTableService>("GetResourceAttributeSetItemTableService");

            BusinessObjectBase.AddCustomExtension("HRMEmployeeBO", typeof(LogTransactionTuruncExtension));
            BusinessObjectBase.AddCustomConstruction("HRMEmployeeBO", HrmEmployeeBoCustomCons);
            BusinessObjectBase.AddCustomInit("HRMEmployeeBO", HrmEmployeeBo_Init_EmployeeLodging);

            PMBase.AddCustomInit("HRMEmployeePM", HrmEmployeePm_Init_EmployeeLodging);
            PMBase.AddCustomDispose("HRMEmployeePM", HrmEmployeePm_Dispose_EmployeeLodging);
            PMBase.AddCustomViewLoaded("HRMEmployeePM", HrmEmployeePm_ViewLoaded_EmployeeLodging);
            PMBase.AddCustomCommandExecutes("HRMEmployeePM", HrmEmployeePm_OnListCommand);

            BusinessObjectBase.AddCustomConstruction("CrsResourceBO", CrsResourceBoCustomCons);
            BusinessObjectBase.AddCustomInit("CrsResourceBO", CrsResourceBo_Init_EmployeeLodging);
            PMBase.AddCustomInit("CrsResource", CrsResourcePm_Init_EmployeeLodging);
            PMBase.AddCustomDispose("CrsResource", CrsResourcePm_Dispose_EmployeeLodging);
            PMBase.AddCustomViewLoaded("CrsResource", CrsResourcePm_ViewLoaded_EmployeeLodging);

            PMBase.AddCustomInit("CurrentAccountPM", CurrentAccountPm_Init);
            PMBase.AddCustomDispose("CurrentAccountPM", CurrentAccountPm_Dispose);
            PMBase.AddCustomViewLoaded("CurrentAccountPM", CurrentAccountPm_ViewLoaded);
            PMBase.AddCustomCommandExecutes("CurrentAccountPM", CurrentAccountPm_OnListCommand);
        }

        private void RegisterRes()
        {
            ResMng.AddRes("LodgingNetCoreManagementModuleMenu", "LodgingNetCoreManagementModule;component/ModuleMenu.xml", ResSource.Resource, ResourceType.MenuXml, Modules.ExternalModule15, 0, 0);
        }

        private void RegisterList()
        {
            //_container.Register<IReport, UnitItemSizeSetDetailsList>("Erp_UnitItemSizeSetDetailsSizeDetailCodeList");
            _container.Register<IReport, CrsResourceList>("Crs_ResourceResourceCodeList");
            _container.Register<IReport, CrsBedTypeList>("Crs_BedTypeBedTypeCodeList");
            _container.Register<IReport, CrsResourceTypeList>("Crs_ResourceResourceTypeCodeList");
        }

        private void RegisterViews()
        {
            ResMng.AddRes("CrsRoomPlanW", "/LodgingNetCoreManagementModule;component/Views/CrsRoomPlan.xaml", ResSource.Resource, ResourceType.View, Modules.ExternalModule15, 0, 0);
            ResMng.AddRes("LodgingDetailsW", "/LodgingNetCoreManagementModule;component/Views/LodgingDetails.xaml", ResSource.Resource, ResourceType.View, Modules.ExternalModule15, 0, 0);
            ResMng.AddRes("LodgingDetailsResourceW", "/LodgingNetCoreManagementModule;component/Views/LodgingDetailsResource.xaml", ResSource.Resource, ResourceType.View, Modules.ExternalModule15, 0, 0);
            ResMng.AddRes("LogDetailsW", "/LodgingNetCoreManagementModule;component/Views/LogDetails.xaml", ResSource.Resource, ResourceType.View, Modules.ExternalModule15, 0, 0);

            ResMng.AddRes("CrsResource", "/LodgingNetCoreManagementModule;component/Views/CrsResource.xaml", ResSource.Resource, ResourceType.View, Modules.ExternalModule15, 0, 0);
            ResMng.AddRes("CrsResourceType", "/LodgingNetCoreManagementModule;component/Views/CrsResourceType.xaml", ResSource.Resource, ResourceType.View, Modules.ExternalModule15, 0, 0);
            ResMng.AddRes("CrsBedType", "/LodgingNetCoreManagementModule;component/Views/CrsBedType.xaml", ResSource.Resource, ResourceType.View, Modules.ExternalModule15, 0, 0);
        }

        private void RegisterPM()
        {
            _container.Register<IPMBase, CrsResourcePM>("CrsResourcePM");
            _container.Register<IPMBase, CrsRoomPlanPM>("CrsRoomPlanPM");
            _container.Register<IPMBase, CrsResourceAttributeSetPM>("CrsResourceAttributeSetPM");
        }

        private void RegisterRpr()
        {
            //_container.Register<IReport, SalesShipmentComparePolicy>("SalesShipmentComparePolicy");
        }

        public void RegisterCoreDocuments()
        {
            Data.MetaData.Schema.ReadXml(Assembly.GetAssembly(typeof(LodgingNetCoreManagementModule)).GetManifestResourceStream("LodgingNetCoreManagementModule.LodgingNetCoreManagementModuleDataSchema.xml"));
            DbCreator.AddRegistration(3014, LodgingNetCoreManagementModuleDbUpdateScript);
        }

        DbScripts LodgingNetCoreManagementModuleDbUpdateScript(DbCreator instance)
        {
            return DbScripts.LoadFromAssembly(Assembly.GetAssembly(typeof(LodgingNetCoreManagementModule)), "LodgingNetCoreManagementModule.LodgingNetCoreManagementModuleDbUpdateScripts.xml");
        }
    }
}
