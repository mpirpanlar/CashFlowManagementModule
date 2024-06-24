using Sentez.Common.SqlBuilder;
using Sentez.Data.BusinessObjects;
using Reeb.SqlOM;
using Sentez.Common.Report;
using Sentez.Common.Commands;
using Sentez.Localization;
using Sentez.Common.ModuleBase;
using Sentez.Common.SystemServices;
using Sentez.Common.PresentationModels;
using Prism.Ioc;

namespace Sentez.CRSModule.WorkList
{
    public class CrsResourceTypeList : ReportBase
    {
        public CrsResourceTypeList(IContainerExtension container)
            : base(container, ReportWorkMode.WorkList)            
        {
            Name = "Crs_ResourceResourceTypeCodeList";
            Title = SLanguage.GetString("Oda Tipi Kartları Listesi");
            WorkMode = ReportWorkMode.WorkList;
        }

        public override void  Init()
        {
            InitBegin();

            Statement statement1 = new Statement("Crs_Resource");
            statement1.AddTable("Erp_Resource", "crs_resource");
            statement1.SetBaseTable("crs_resource");

            statement1.LoadAllFields();

            statement1.AddCol("RecId", "crs_resource", "RecId", false);
            statement1.AddColMandatory("ResourceCode", "crs_resource", SLanguage.GetString("Kodu"));
            statement1.AddColMandatory("Explanation", "crs_resource", SLanguage.GetString("Adı"));

             statement1.AddWhere(WhereTermType.Compare, SqlDataType.Number, CompareOperator.Equal, "crs_resource", "ResourceType").valueList[0] = (int)ResourceTypeDefinition.ResourceType.RoomType;
            
            statement1.AddMandatoryFilters(activeSession);

            statement1.OrderBy("crs_resource", "ResourceCode", OrderByDirection.Ascending);

            AddStatement(statement1);

            InitEnd();
        }

 
        override public MenuItemPM GetCommands()
        {
            RootMenu = new MenuItemPM();
            SeparatorCmd = new MenuItemPM("-", "");

            BoParam boparam = new BoParam();
            boparam.ValRefs["ActiveRecordId"] = GetActiveRefId;
            boparam.Type = (int)ResourceTypeDefinition.ResourceType.RoomType;
            boparam.LogicalModuleId = (short)Modules.CRMModule;

            BoParam boparam2 = new BoParam();
            boparam2.Type = (int)ResourceTypeDefinition.ResourceType.RoomType;
            boparam2.LogicalModuleId = (short)Modules.CRMModule;

            PmParam pmparam = new PmParam("CrsResourceType", "BOCardContext");
            pmparam.Name = "CrsResourceType";

            OpenCmd = new MenuItemPM("Değiştir", "CmdGeneralOpen");
            OpenCmd.MenuItemCommandParam = new SysCommandParam("CrsResourceType", "CrsResourceAttributeSetPM", pmparam, "CrsResourceBO", boparam, "", "RecId");
            OpenCmd.MenuItemCommandParam.logicalModuleID = (short)Modules.ExternalModule15;
            OpenCmd.MenuItemCommandParam.moduleID = (short)Modules.ExternalModule15;
            //OpenCmd.MenuItemCommandParam.secID = (short)CRSSecurityItems.Resource;
            //OpenCmd.MenuItemCommandParam.subsecID = (short)CRSSecuritySubItems.None;
            OpenCmd.ShortcutKey = System.Windows.Input.Key.F4;
            OpenCmd.ShortcutKeyModifier = System.Windows.Input.ModifierKeys.Shift;
            RootMenu.Children.Add(OpenCmd);

            NewCmd = new MenuItemPM("Yeni", "CmdGeneralOpen");
            NewCmd.MenuItemCommandParam = new SysCommandParam("CrsResourceType", "CrsResourceAttributeSetPM", pmparam, "CrsResourceBO", boparam2, "", "RecId");
            NewCmd.MenuItemCommandParam.logicalModuleID = (short)Modules.ExternalModule15;
            NewCmd.MenuItemCommandParam.moduleID = (short)Modules.ExternalModule15;
            //NewCmd.MenuItemCommandParam.secID = (short)CRSSecurityItems.ResourceType;
            //NewCmd.MenuItemCommandParam.subsecID = (short)CRSSecuritySubItems.None;
            NewCmd.ShortcutKey = System.Windows.Input.Key.F4;
            RootMenu.Children.Add(NewCmd);

            MenuItemPM DeleteCmd = new MenuItemPM("Sil", "Delete");
            DeleteCmd.MenuItemCommandParam = new SysCommandParam("CrsResourceType", "CrsResourceAttributeSetPM", pmparam, "CrsResourceBO", boparam, "", "RecId");
            DeleteCmd.MenuItemCommandParam.logicalModuleID = (short)Modules.ExternalModule15;
            DeleteCmd.MenuItemCommandParam.moduleID = (short)Modules.ExternalModule15;
            //DeleteCmd.MenuItemCommandParam.secID = (short)CRSSecurityItems.ResourceType;
            //DeleteCmd.MenuItemCommandParam.subsecID = (short)CRSSecuritySubItems.None;
            DeleteCmd.ShortcutKey = System.Windows.Input.Key.F6;
            RootMenu.Children.Add(DeleteCmd);

            RootMenu.Children.Add(SeparatorCmd);

            MenuItemPM itmpm1 = new MenuItemPM("Kopyalama", "CopyToOtherCompanyCommand");
            itmpm1.MenuItemCommandParam = new SysCommandParam("", null, "CrsResourceTypeBO", "RecId");
            itmpm1.ShortcutKey = System.Windows.Input.Key.F8;
            RootMenu.Children.Add(itmpm1);

            RootMenu.Children.Add(SeparatorCmd); 
            
            MenuItemPM ListCmd = new MenuItemPM("Liste", "ListCommand");
            ListCmd.ShortcutKey = System.Windows.Input.Key.F9;
            RootMenu.Children.Add(ListCmd);

            return RootMenu;
        }        
        public override object GetResultFieldValue(int row)
        {
            if (!Data.Tables[0].Columns.Contains(GetResultFieldName())) return null;   return Data.Tables[0].DefaultView[row][GetResultFieldName()];
        }
    }
}
