using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Sentez.Common.SqlBuilder;
using Sentez.Data.BusinessObjects;
using Reeb.SqlOM;
using Sentez.Common.Report;
using Sentez.Data.Tools;
using System.Xml;
using System.Windows.Controls;
using Sentez.Common.Commands;
using System.IO;
using Sentez.Localization;
using Sentez.Common.ModuleBase;
using Sentez.Common;
using Sentez.Common.SystemServices;
using Sentez.Common.PresentationModels;
using Prism.Ioc;

namespace Sentez.CRSModule.WorkList
{
    public class CrsBedTypeList : ReportBase
    {
        public CrsBedTypeList(IContainerExtension container)
            : base(container, ReportWorkMode.WorkList)            
        {
            Name = "Crs_BedTypeBedTypeCodeList";
            Title = SLanguage.GetString("Yatak Tipi Kartları Listesi");
            WorkMode = ReportWorkMode.WorkList;
        }

        public override void  Init()
        {
            InitBegin();

            Statement _statement1 = new Statement("Meta_BedType");
            _statement1.AddTable("Meta_BedType", "meta_bedtype");

            _statement1.SetBaseTable("meta_bedtype");

            _statement1.LoadAllFields();

            _statement1.AddCol("RecId", "meta_bedtype", "RecId", false);
            _statement1.AddColMandatory("BedTypeCode", "meta_bedtype", SLanguage.GetString("Yatak Tipi Kodu"));
            _statement1.AddColMandatory("BedTypeName", "meta_bedtype", SLanguage.GetString("Yatak Tipi Adı"));

           _statement1.AddMandatoryFilters(activeSession);

            _statement1.OrderBy("meta_bedtype", "BedTypeCode", OrderByDirection.Ascending);

            AddStatement(_statement1);

            InitEnd();
        }

 
        override public MenuItemPM GetCommands()
        {
            RootMenu = new MenuItemPM();
            SeparatorCmd = new MenuItemPM("-", "");

            BoParam boparam = new BoParam();
            boparam.ValRefs["ActiveRecordId"] = GetActiveRefId;
            boparam.LogicalModuleId = (short)Modules.CRSModule;

            BoParam boparam2 = new BoParam();
            boparam2.LogicalModuleId = (short)Modules.CRSModule;

            PmParam pmparam = new PmParam("CrsBedType", "BOCardContext");
            pmparam.Name = "CrsBedType";

            OpenCmd = new MenuItemPM("Değiştir", "CmdGeneralOpen");
            OpenCmd.MenuItemCommandParam = new SysCommandParam("CrsBedType", "CardPM", pmparam, "CrsBedTypeBO", boparam, "", "RecId");
            OpenCmd.MenuItemCommandParam.logicalModuleID = (short)Modules.CRSModule;
            OpenCmd.MenuItemCommandParam.moduleID = (short)Modules.CRMModule;
            //OpenCmd.MenuItemCommandParam.secID = (short)CRSSecurityItems.BedType;
            //OpenCmd.MenuItemCommandParam.subsecID = (short)CRSSecuritySubItems.None;
            OpenCmd.ShortcutKey = System.Windows.Input.Key.F4;
            OpenCmd.ShortcutKeyModifier = System.Windows.Input.ModifierKeys.Shift;
            RootMenu.Children.Add(OpenCmd);

            NewCmd = new MenuItemPM("Yeni", "CmdGeneralOpen");
            NewCmd.MenuItemCommandParam = new SysCommandParam("CrsBedType", "CardPM", pmparam, "CrsBedTypeBO", boparam2, "", "");
            NewCmd.MenuItemCommandParam.logicalModuleID = (short)Modules.CRSModule;
            NewCmd.MenuItemCommandParam.moduleID = (short)Modules.CRSModule;
            //NewCmd.MenuItemCommandParam.secID = (short)CRSSecurityItems.BedType;
            //NewCmd.MenuItemCommandParam.subsecID = (short)CRSSecuritySubItems.None;
            NewCmd.ShortcutKey = System.Windows.Input.Key.F4;
            RootMenu.Children.Add(NewCmd);

            MenuItemPM DeleteCmd = new MenuItemPM("Sil", "Delete");
            DeleteCmd.MenuItemCommandParam = new SysCommandParam("CrsBedType", "BedType,BOCardContext", "CrsBedTypeBO", "RecId");
            DeleteCmd.MenuItemCommandParam.logicalModuleID = (short)Modules.CRSModule;
            DeleteCmd.MenuItemCommandParam.moduleID = (short)Modules.CRSModule;
            //DeleteCmd.MenuItemCommandParam.secID = (short)CRSSecurityItems.BedType;
            //DeleteCmd.MenuItemCommandParam.subsecID = (short)CRSSecuritySubItems.None;
            DeleteCmd.ShortcutKey = System.Windows.Input.Key.F6;
            RootMenu.Children.Add(DeleteCmd);

            RootMenu.Children.Add(SeparatorCmd);

            MenuItemPM itmpm1 = new MenuItemPM("Kopyalama", "CopyToOtherCompanyCommand");
            itmpm1.MenuItemCommandParam = new SysCommandParam("", null, "CrsBedTypeBO", "RecId");
            itmpm1.ShortcutKey = System.Windows.Input.Key.F8;
            RootMenu.Children.Add(itmpm1);

            return RootMenu;
        }        

        public override object GetResultFieldValue(int row)
        {
            if (!Data.Tables[0].Columns.Contains(GetResultFieldName())) return null;   return Data.Tables[0].DefaultView[row][GetResultFieldName()];
        }
    }
}
