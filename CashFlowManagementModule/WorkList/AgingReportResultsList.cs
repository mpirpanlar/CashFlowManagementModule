using Sentez.Common.SqlBuilder;
using Reeb.SqlOM;
using Sentez.Common.Report;
using Sentez.Common.Commands;
using Prism.Ioc;
using Sentez.Localization;
using Sentez.Common.ModuleBase;
using Sentez.Common.PresentationModels;
using Sentez.Data.BusinessObjects;

namespace Sentez.FinanceModule.WorkList
{
    public class AgingReportResultsList : ReportBase
    {
        public override bool CacheResults => true;

        public AgingReportResultsList(IContainerExtension container)
            : base(container, ReportWorkMode.WorkList)            
        {
            Name = "Erp_AgingReportResultsReportNoList";
            Title = SLanguage.GetString("Cari Hesap Yaşlandırma Raporları Listesi");
            WorkMode = ReportWorkMode.WorkList;
        }

        public override void  Init()
        {
            InitBegin();
 	
            Statement _statement1 = new Statement("Erp_AgingReportResults");
            _statement1.AddTable("Erp_AgingReportResults", "erp_agingreportresults");
            _statement1.SetBaseTable("erp_agingreportresults");
            _statement1.LoadAllFields();

            _statement1.AddCol("RecId", "erp_agingreportresults", "RecId", false);
            _statement1.AddColMandatory("ReportNo", "erp_agingreportresults", SLanguage.GetString("Rapor No"));
            _statement1.AddColMandatory("ReportDate", "erp_agingreportresults", SLanguage.GetString("Rapor Tarihi"));
            _statement1.AddColMandatory("Explanation", "erp_agingreportresults", SLanguage.GetString("Rapor Açıklaması"));

            _statement1.AddMandatoryFilters(activeSession);

            _statement1.OrderBy("erp_agingreportresults", "ReportNo", OrderByDirection.Ascending);

            AddStatement(_statement1);

            InitEnd();
        }

        public override MenuItemPM GetCommands()
        {
            RootMenu = new MenuItemPM();
            SeparatorCmd = new MenuItemPM("-", "");
            BoParam boparam = new BoParam
            {
                Type = 0,
                ValRefs = { ["ActiveRecordId"] = GetActiveRefId },
                LogicalModuleId = (short)Modules.FinanceModule
            };
            if (PolicyParam != null && PolicyParam.FieldName == "DealerCode") boparam.Tag = "Dealer";
            BoParam boparam2 = new BoParam
            {
                Type = 0,
                LogicalModuleId = (short)Modules.FinanceModule
            };
            if (PolicyParam != null && PolicyParam.FieldName != null && PolicyParam.FieldName == "DealerCode") boparam2.Tag = "Dealer";
            PmParam pmparam = new PmParam("AgingReportResultsListPM", "BOCardContext");

            OpenCmd = new MenuItemPM("Değiştir", "CmdGeneralOpen")
            {
                MenuItemCommandParam = new SysCommandParam("AgingReportResultsListW", "AgingReportResultsListPM", pmparam, "AgingReportResultsListBO", boparam, "", "RecId")
                {
                    logicalModuleID = boparam.LogicalModuleId,
                    moduleID = (short)Modules.FinanceModule,
                    secID = (short)FinanceSecurityItems.CurrentAccount,
                    subsecID = (short)CurrentAccountSubItems.None
                },
                ShortcutKey = System.Windows.Input.Key.F4,
                ShortcutKeyModifier = System.Windows.Input.ModifierKeys.Shift
            };
            RootMenu.Children.Add(OpenCmd);

            NewCmd = new MenuItemPM("Yeni", "CurrentAccountCard")
            {
                MenuItemCommandParam = new SysCommandParam("AgingReportResultsListW", "AgingReportResultsListPM", pmparam, "AgingReportResultsListBO", boparam2, "", "")
                {
                    logicalModuleID = boparam2.LogicalModuleId,
                    moduleID = (short)Modules.FinanceModule,
                    secID = (short)FinanceSecurityItems.CurrentAccount,
                    subsecID = (short)CurrentAccountSubItems.None
                },
                ShortcutKey = System.Windows.Input.Key.F4
            };
            RootMenu.Children.Add(NewCmd);

            MenuItemPM DeleteCmd = new MenuItemPM("Sil", "Delete")
            {
                MenuItemCommandParam = new SysCommandParam("AgingReportResultsListW", "AgingReportResultsListPM", pmparam, "AgingReportResultsListBO", boparam, "", "RecId")
                {
                    logicalModuleID = boparam.LogicalModuleId,
                    moduleID = (short)Modules.FinanceModule,
                    secID = (short)FinanceSecurityItems.CurrentAccount,
                    subsecID = (short)CurrentAccountSubItems.None
                },
                ShortcutKey = System.Windows.Input.Key.F6
            };
            RootMenu.Children.Add(DeleteCmd);
            RootMenu.Children.Add(SeparatorCmd);

            MenuItemPM itmpm1 = new MenuItemPM("Kopyalama", "CopyToOtherCompanyCommand")
            {
                MenuItemCommandParam = new SysCommandParam("", null, "CurrentAccountGroupBO", "RecId"),
                ShortcutKey = System.Windows.Input.Key.F8
            };
            RootMenu.Children.Add(itmpm1);
            /*
            MenuItemPM itmpm2 = new MenuItemPM("Kod Değiştirme", "CardChangeCommand");
            itmpm2.MenuItemCommandParam = new SysCommandParam("", null, "CurrentAccountGroupBO", "RecId");
            itmpm2.ShortcutKey = System.Windows.Input.Key.F8;
            itmpm2.ShortcutKeyModifier = System.Windows.Input.ModifierKeys.Shift;
            RootMenu.Children.Add(itmpm2);
            */
            RootMenu.Children.Add(SeparatorCmd);

            MenuItemPM ListCmd = new MenuItemPM("Liste", "ListCommand") {ShortcutKey = System.Windows.Input.Key.F9};
            RootMenu.Children.Add(ListCmd);

            return RootMenu;
        }

        public override object GetResultFieldValue(int row)
        {
            if (!Data.Tables[0].Columns.Contains(GetResultFieldName())) return null;   return Data.Tables[0].DefaultView[row][GetResultFieldName()];
        }
    }
}
