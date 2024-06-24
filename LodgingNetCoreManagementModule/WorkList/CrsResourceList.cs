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
using System.Data;
using Sentez.Data.Query;
using LiveCore.Desktop.UI.Controls;
using Sentez.Data.MetaData;
using DevExpress.Xpf.Core;
using System.Windows.Controls.Primitives;
using System.Windows.Forms;
using Sentez.HRMModule.PresentationModels;
using Prism.Ioc;

namespace Sentez.CRSModule.WorkList
{
    public class CrsResourceList : ReportBase
    {
        ISystemService service { get; set; }
        LiveGridControl liveGrid;
        DateTime eofDate;
        public CrsResourceList(IContainerExtension container)
            : base(container, ReportWorkMode.WorkList)
        {
            Name = "Crs_ResourceResourceCodeList";
            Title = SLanguage.GetString("Lojman Kartları Listesi");
            WorkMode = ReportWorkMode.WorkList;
        }

        public override void Init()
        {
            InitBegin();

            if (PolicyParam.FieldName == "TotalRoom")
            {
                Title = SLanguage.GetString("Toplam Oda Listesi");
            }
            Statement _statement1 = new Statement("Crs_Resource");
            _statement1.AddTable("Erp_Resource", "crs_resource");
            _statement1.AddTable("Meta_BedType", "meta_bedtype");
            _statement1.AddTable("Erp_Employee", "erp_employee");
            _statement1.AddTable("Erp_Resource", "crs_parent_resource");
            _statement1.AddTable("Erp_ResourceOutOfUse", "crs_resourceoutofuse");
            _statement1.AddTable("Erp_EmployeeLodging", "erp_employeelodging");
            _statement1.AddTable("Erp_Department", "erp_department");
            _statement1.AddTable("Hrm_Position", "hrm_position");
            _statement1.AddTable("Meta_HrmProfession", "meta_hrmprofession");



            _statement1.SetBaseTable("crs_resource");

            _statement1.LoadAllFields();

            _statement1.AddCol("RecId", "crs_resource", "RecId", false);
            _statement1.AddColMandatory("ResourceCode", "crs_resource", SLanguage.GetString("Lojman Kodu"));
            _statement1.AddColMandatory("Explanation", "crs_resource", SLanguage.GetString("Açıklama"));
            _statement1.AddColMandatory("EmployeeCode", "erp_employee", SLanguage.GetString("Personel Kodu"), false);
            _statement1.AddColMandatory("EmployeeName", "erp_employee", SLanguage.GetString("Personel Adı"));
            _statement1.AddColMandatory("EmployeeSurname", "erp_employee", SLanguage.GetString("Personel Soyadı"));
            _statement1.AddColMandatory("DepartmentCode", "erp_department", SLanguage.GetString("Departman Kodu"), false);
            _statement1.AddColMandatory("DepartmentName", "erp_department", SLanguage.GetString("Departman Adı"));
            _statement1.AddColMandatory("PositionCode", "hrm_position", SLanguage.GetString("Pozisyon Kodu"), false);
            _statement1.AddColMandatory("PositionName", "hrm_position", SLanguage.GetString("Pozisyon Adı"));
            _statement1.AddColMandatory("ProfessionCode", "meta_hrmprofession", SLanguage.GetString("Meslek Kodu"), false);
            _statement1.AddColMandatory("ProfessionName", "meta_hrmprofession", SLanguage.GetString("Meslek Adı"));
            _statement1.AddColMandatory("ArrivalDate", "erp_employeelodging", SLanguage.GetString("Geliş Tarihi"));
            _statement1.AddColMandatory("DepartureDate", "erp_employeelodging", SLanguage.GetString("Ayrılış Tarihi"));
            _statement1.AddColMandatory("Explanation", "erp_employeelodging", SLanguage.GetString("Konaklama Açıklaması"));
            _statement1.AddColMandatory("ResourceCode", "crs_parent_resource", SLanguage.GetString("Oda Tipi Kodu"), false);
            _statement1.AddColMandatory("Explanation", "crs_parent_resource", SLanguage.GetString("Oda Tipi"));
            _statement1.AddCol("BedTypeCode", "meta_bedtype", SLanguage.GetString("Yatak Tipi Kodu"), false);
            _statement1.AddCol("BedTypeName", "meta_bedtype", SLanguage.GetString("Yatak Tipi"));
            _statement1.AddCol("LocationBuilding", "crs_resource", SLanguage.GetString("Bina"));
            _statement1.AddCol("LocationFloor", "crs_resource", SLanguage.GetString("Kat"));
            _statement1.AddCol("LocationAisle", "crs_resource", SLanguage.GetString("Koridor"), false);
            _statement1.AddCol("Capacity", "crs_resource", SLanguage.GetString("Toplam Kişi"), null, SqlAggregationFunction.None, true, true, FieldUsage.VariantQuantity);
            _statement1.AddColCalc("0.0", SLanguage.GetString("Konaklayan Kişi"), SqlDataType.Number, FieldUsage.VariantQuantity, null);
            _statement1.AddColCalc("0.0", SLanguage.GetString("Kalabilecek Kişi"), SqlDataType.Number, FieldUsage.VariantQuantity, null);

            _statement1.JoinTables("crs_resource", "crs_resourceoutofuse", "RecId", "ResourceId", JoinType.Left);
            _statement1.JoinTables("crs_resource", "meta_bedtype", "BedTypeId", "RecId", JoinType.Left);
            _statement1.JoinTables("crs_resource", "crs_parent_resource", "ParentResourceId", "RecId", JoinType.Left);
            _statement1.JoinTables("crs_resource", "erp_employeelodging", "RecId", "ResourceId", JoinType.Left);
            _statement1.JoinTables("erp_employeelodging", "erp_employee", "EmployeeId", "RecId", JoinType.Left);
            _statement1.JoinTables("erp_employee", "erp_department", "DepartmentId", "RecId", JoinType.Left);
            _statement1.JoinTables("erp_employee", "hrm_position", "PositionId", "RecId", JoinType.Left);
            _statement1.JoinTables("erp_employee", "meta_hrmprofession", "SsiProfessionId", "RecId", JoinType.Left);


            _statement1.AddWhere(WhereTermType.Compare, SqlDataType.Number, CompareOperator.Equal, "crs_resource", "ResourceType").valueList[0] = (int)ResourceTypeDefinition.ResourceType.Room;
            _statement1.AddWhere(WhereTermType.Compare, SqlDataType.Number, CompareOperator.Equal, "erp_employeelodging", "IsCheckedOut").valueList[0] = 0;

            if (PolicyParam?.Tag is DataRowView && (PolicyParam.Tag as DataRowView).Row.Table.Columns.Contains("WorkplaceId") && !(PolicyParam.Tag as DataRowView).Row.IsNull("WorkplaceId"))
            {
                int workplaceId;
                int.TryParse((PolicyParam.Tag as DataRowView).Row["WorkplaceId"].ToString(), out workplaceId);
                FilterItem filterItem = _statement1.AddWhereGroup("WorkplaceId", WhereClauseRelationship.Or);
                filterItem.AddToGroup(WhereTermType.Compare, SqlDataType.Number, CompareOperator.Equal, "crs_resource", "WorkplaceId", workplaceId);
                filterItem.AddToGroup(WhereTermType.Compare, SqlDataType.Null, CompareOperator.Equal, "crs_resource", "WorkplaceId", "NULL");
                _statement1.AddWhere(filterItem);
            }

            DateTime eofDate = DateTime.Now.Date;
            string sqlFormattedDate = eofDate.ToString("yyyy-MM-dd");
            _statement1.AddMandatoryFilters(activeSession);

            _statement1.OrderBy("crs_resource", "ResourceCode", OrderByDirection.Ascending);

            AddStatement(_statement1);

            InitEnd();
        }

        private StringBuilder GetSqlStr(DateTime arrivalDate, DateTime departureDate)
        {
            eofDate = (DateTime)activeSession.ServiceReferences["GetAgileGlobalTodayDateHelperService"].Execute(null);
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine($"crs_resource.RecId");
            stringBuilder.AppendLine($"NOT IN");
            stringBuilder.AppendLine($"(");
            stringBuilder.AppendLine($"SELECT");
            stringBuilder.AppendLine($"[crs_resource].[RecId]");
            stringBuilder.AppendLine($"FROM");
            stringBuilder.AppendLine($"[Erp_Resource][crs_resource] WITH(NOLOCK)");
            stringBuilder.AppendLine($"LEFT JOIN[Erp_ResourceOutOfUse][crs_resourceoutofuse] WITH(NOLOCK)");
            stringBuilder.AppendLine($"ON([crs_resource].[RecId] = [crs_resourceoutofuse].[ResourceId])");
            stringBuilder.AppendLine($"LEFT JOIN[Meta_BedType] [meta_bedtype] WITH(NOLOCK)");
            stringBuilder.AppendLine($"ON([crs_resource].[BedTypeId] = [meta_bedtype].[RecId])");
            stringBuilder.AppendLine($"LEFT JOIN[Erp_Employee] [erp_employee] WITH(NOLOCK)");
            stringBuilder.AppendLine($"ON([crs_resource].[EmployeeId] = [erp_employee].[RecId])");
            stringBuilder.AppendLine($"LEFT JOIN[Erp_Resource] [crs_parent_resource] WITH(NOLOCK)");
            stringBuilder.AppendLine($"ON([crs_resource].[ParentResourceId] = [crs_parent_resource].[RecId])");
            stringBuilder.AppendLine($"WHERE(5 = ISNULL([crs_resource].[ResourceType], 0)");
            stringBuilder.AppendLine($"AND ");
            stringBuilder.AppendLine($"(");
            stringBuilder.AppendLine($"crs_resource.RecId IN (SELECT cbip.ResourceId FROM Crs_BookingItemPlan cbip WITH (NoLock) WHERE PlannedDate Between {ObjectQueryBuilder.GetWhereDateValue(arrivalDate)} And {ObjectQueryBuilder.GetWhereDateValue(departureDate)} And  PlanType = 0 AND cbip.BookingItemId IN (SELECT RecId FROM Crs_BookingItem cbi WITH (NoLock) WHERE ISNULL(IsCancelled,0)= 0 AND ISNULL(IsDeleted,0)= 0 AND ISNULL(cbi.IsCheckedOut,0)= 0 And BookingType NOT IN(4,6) AND ISNULL(cbi.IsDeleted,0)=0 And CompanyId = {activeSession.ActiveCompany.RecId} AND ArrivalDate <> {ObjectQueryBuilder.GetWhereDateValue(departureDate)} ))");
            stringBuilder.AppendLine($"Or");
            stringBuilder.AppendLine($"crs_resource.RecId IN (SELECT cbip.ResourceId FROM Crs_BookingItemPlan cbip WITH (NoLock) WHERE PlannedDate Between {ObjectQueryBuilder.GetWhereDateValue(arrivalDate)} And {ObjectQueryBuilder.GetWhereDateValue(departureDate)} And  PlanType = 0 AND cbip.BookingItemId IN (SELECT RecId FROM Crs_BookingItem cbi WITH (NoLock) WHERE ISNULL(IsCancelled,0)= 0 AND ISNULL(IsDeleted,0)= 0 AND ISNULL(cbi.IsCheckedOut,0)= 0 AND ISNULL(cbi.IsCheckedIn,0)= 1  And BookingType NOT IN(4,6) AND ISNULL(cbi.IsDeleted,0)=0 And CompanyId = {activeSession.ActiveCompany.RecId} AND ArrivalDate <> {ObjectQueryBuilder.GetWhereDateValue(departureDate)} ))");
            stringBuilder.AppendLine($")");
            stringBuilder.AppendLine($"");
            stringBuilder.AppendLine($"AND {activeSession.ActiveCompany.RecId} = ISNULL([crs_resource].[CompanyId], 0)");
            stringBuilder.AppendLine($"AND 0 = ISNULL([crs_resource].[IsDeleted], 0)");
            stringBuilder.AppendLine($"AND 0 = ISNULL([meta_bedtype].[IsDeleted], 0)");
            stringBuilder.AppendLine($"AND 0 = ISNULL([erp_employee].[IsDeleted], 0)");
            stringBuilder.AppendLine($"AND 0 = ISNULL([crs_parent_resource].[IsDeleted], 0)");
            stringBuilder.AppendLine($"AND 0 = ISNULL([crs_resourceoutofuse].[IsDeleted], 0)");
            stringBuilder.AppendLine($")");
            stringBuilder.AppendLine($")");
            if (PolicyParam.Tag3 is string && PolicyParam.Tag3.ToString() == "ForInSql")
            {
                if (PolicyParam.Tag2 is DataRowView)
                    stringBuilder.AppendLine($"AND  crs_parent_resource.RecId={(PolicyParam.Tag2 as DataRowView).Row["ResourceTypeId"]}");
                else if (PolicyParam.Tag2 is long)
                    stringBuilder.AppendLine($"AND  crs_parent_resource.RecId={(long)PolicyParam.Tag2}");
            }
            return stringBuilder;
        }

        override public MenuItemPM GetCommands()
        {
            RootMenu = new MenuItemPM();
            SeparatorCmd = new MenuItemPM("-", "");

            BoParam boparam = new BoParam();
            boparam.ValRefs["ActiveRecordId"] = GetActiveRefId;
            boparam.Type = (int)ResourceTypeDefinition.ResourceType.Room;
            boparam.LogicalModuleId = (short)Modules.ExternalModule15;

            BoParam boparam2 = new BoParam();
            boparam2.Type = (int)ResourceTypeDefinition.ResourceType.Room;
            boparam2.LogicalModuleId = (short)Modules.ExternalModule15;

            PmParam pmparam = new PmParam("CrsResource", "BOCardContext");
            pmparam.Name = "CrsResource";

            OpenCmd = new MenuItemPM("Değiştir", "CmdGeneralOpen");
            OpenCmd.MenuItemCommandParam = new SysCommandParam("CrsResource", "CrsResourcePM", pmparam, "CrsResourceBO", boparam, "", "RecId");
            OpenCmd.MenuItemCommandParam.logicalModuleID = (short)Modules.ExternalModule15;
            OpenCmd.MenuItemCommandParam.moduleID = (short)Modules.ExternalModule15;
            //OpenCmd.MenuItemCommandParam.secID = (short)CRSSecurityItems.Resource;
            //OpenCmd.MenuItemCommandParam.subsecID = (short)CRSSecuritySubItems.None;
            OpenCmd.ShortcutKey = System.Windows.Input.Key.F4;
            OpenCmd.ShortcutKeyModifier = System.Windows.Input.ModifierKeys.Shift;
            RootMenu.Children.Add(OpenCmd);

            NewCmd = new MenuItemPM("Yeni", "CmdGeneralOpen");
            NewCmd.MenuItemCommandParam = new SysCommandParam("CrsResource", "CrsResourcePM", pmparam, "CrsResourceBO", boparam2, "", "RecId");
            NewCmd.MenuItemCommandParam.logicalModuleID = (short)Modules.ExternalModule15;
            NewCmd.MenuItemCommandParam.moduleID = (short)Modules.ExternalModule15;
            //NewCmd.MenuItemCommandParam.secID = (short)CRSSecurityItems.Resource;
            //NewCmd.MenuItemCommandParam.subsecID = (short)CRSSecuritySubItems.None;
            NewCmd.ShortcutKey = System.Windows.Input.Key.F4;
            RootMenu.Children.Add(NewCmd);

            MenuItemPM DeleteCmd = new MenuItemPM("Sil", "Delete");
            DeleteCmd.MenuItemCommandParam = new SysCommandParam("CrsResource", "Resource,BOCardContext", "CrsResourceBO", "RecId");
            DeleteCmd.MenuItemCommandParam.logicalModuleID = (short)Modules.ExternalModule15;
            DeleteCmd.MenuItemCommandParam.moduleID = (short)Modules.ExternalModule15;
            //DeleteCmd.MenuItemCommandParam.secID = (short)CRSSecurityItems.Resource;
            //DeleteCmd.MenuItemCommandParam.subsecID = (short)CRSSecuritySubItems.None;
            DeleteCmd.ShortcutKey = System.Windows.Input.Key.F6;
            RootMenu.Children.Add(DeleteCmd);

            RootMenu.Children.Add(SeparatorCmd);

            MenuItemPM itmpm1 = new MenuItemPM("Kopyalama", "CopyToOtherCompanyCommand");
            itmpm1.MenuItemCommandParam = new SysCommandParam("", null, "CrsResourceBO", "RecId");
            itmpm1.ShortcutKey = System.Windows.Input.Key.F8;
            RootMenu.Children.Add(itmpm1);

            RootMenu.Children.Add(SeparatorCmd);

            MenuItemPM ListCmd = new MenuItemPM("Liste", "ListCommand");
            ListCmd.ShortcutKey = System.Windows.Input.Key.F9;
            RootMenu.Children.Add(ListCmd);

            if (GetActiveRefId != null)
            {
                object value = this.GetActiveRefId.Target;
                if (value != null)
                {
                    if (value is PMDesktop)
                    {
                        ((value as PMDesktop).ActiveView._view as System.Windows.Controls.UserControl).Loaded += CrsResourceList_Loaded;
                    }
                }
            }
            return RootMenu;
        }

        public override object GetResultFieldValue(int row)
        {
            if (!Data.Tables[0].Columns.Contains(GetResultFieldName())) return null; return Data.Tables[0].DefaultView[row][GetResultFieldName()];
        }

        private void CrsResourceList_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            liveGrid = ((sender as System.Windows.Controls.UserControl).DataContext as PMDesktop).FCtrl("dbGrid") as LiveGridControl;
            if (liveGrid != null)
            {
                if (liveGrid.ItemsSource is DataView)
                {
                    liveGrid.EndingRestoreLayout += LiveGrid_EndingRestoreLayout;
                }
            }
        }

        private void LiveGrid_EndingRestoreLayout(object sender, EventArgs e)
        {
            if (PolicyParam != null && PolicyParam.Tag2 is DataRowView)
            {
                DataRow[] itemPlanRows = (PolicyParam.Tag2 as DataRowView).Row.Table.Select("PlanType=0 and PlannedDate is null", "", DataViewRowState.CurrentRows);
                if (itemPlanRows?.Length > 0)
                {
                    if (!string.IsNullOrEmpty(itemPlanRows[0]["ResourceTypeCode"].ToString()))
                        (sender as LiveGridControl).FilterString = $"StartsWith([Oda Tipi Kodu], '{itemPlanRows[0]["ResourceTypeCode"]}')";
                }
            }
            else if (PolicyParam != null && PolicyParam.Tag is DataRowView)
            {
                (sender as LiveGridControl).FilterString = $"StartsWith([Oda Tipi Kodu], '{(PolicyParam.Tag as DataRowView).Row[SLanguage.GetString("Oda Tipi")]}')";
            }
            //else if (PolicyParam != null && PolicyParam.Tag is RoomModel && (PolicyParam.Tag as RoomModel)?.SelectedRoom?.RoomSpecifications.Count > 0)
            //{
            //    (sender as LiveGridControl).FilterString = $"StartsWith([Oda Tipi Kodu], '{(PolicyParam.Tag as RoomModel).SelectedRoom.RoomSpecifications[0].ResourceTypeCode}')";
            //}
        }

        public override void AfterCreateDataset()
        {
            base.AfterCreateDataset();
            if (Data.Tables[0]?.Rows.Count == 0) return;
            var groupedData = Data.Tables[0].AsEnumerable()
                .GroupBy(row => row.Field<string>(SLanguage.GetString("Lojman Kodu")))
                .Select(g => new
                {
                    Category = g.Key,
                    Count = g.Count()
                });

            foreach (var item in groupedData)
            {
                DataRow[] resourceRows = Data.Tables[0].Select($"[{SLanguage.GetString("Lojman Kodu")}]='{item.Category}'");
                foreach (var row in resourceRows)
                {
                    decimal totalPerson;
                    decimal.TryParse(row[$"{SLanguage.GetString("Toplam Kişi")}"].ToString(), out totalPerson);
                    row[$"{SLanguage.GetString("Konaklayan Kişi")}"] = item.Count;
                    row[$"{SLanguage.GetString("Kalabilecek Kişi")}"] = totalPerson - item.Count;
                }
            }
        }

        public override void Dispose()
        {
            if (disposed)
                return;
            if (GetActiveRefId != null)
            {
                object value = this.GetActiveRefId.Target;
                if (value != null)
                {
                    if (value is PMDesktop)
                    {
                        ((value as PMDesktop).ActiveView._view as System.Windows.Controls.UserControl).Loaded -= CrsResourceList_Loaded;
                    }
                }
            }
            if (liveGrid != null)
                liveGrid.EndingRestoreLayout -= LiveGrid_EndingRestoreLayout;
            base.Dispose();
        }
    }
}
