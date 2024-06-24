using LiveCore.Desktop.Common;
using LiveCore.Desktop.UI.Controls;

using Prism.Ioc;

using Sentez.Common;
using Sentez.Common.Commands;
using Sentez.Common.ModuleBase;
using Sentez.Common.PresentationModels;
using Sentez.Common.Report;
using Sentez.Common.Utilities;
using Sentez.Data.BusinessObjects;
using Sentez.Data.MetaData;
using Sentez.Data.Tools;
using Sentez.HRMModule.PresentationModels;
using Sentez.InventoryModule.PresentationModels;
using Sentez.Localization;

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Sentez.LodgingNetCoreManagementModule
{
    public partial class LodgingNetCoreManagementModule : LiveModule
    {
        private void HrmEmployeeBoCustomCons(ref short itemId, ref string keyColumn, ref string typeField, ref string[] Tables)
        {
            List<string> tableList = new List<string>();
            tableList.AddRange(Tables);

            tableList.Add("Erp_EmployeeLodging");
            Tables = tableList.ToArray();
        }

        private void HrmEmployeeBo_Init_EmployeeLodging(BusinessObjectBase bo, BoParam parameter)
        {
            bo.ValueFiller.AddRule("Erp_EmployeeLodging", "InUse", 1);
            bo.ValueFiller.AddRule("Erp_EmployeeLodging", "BookingType", (byte)0);
            //bo.ValueFiller.AddRule("Erp_EmployeeLodging", "AciType", (byte)0);
            bo.ValueFiller.AddRule("Erp_EmployeeLodging", "ArrivalDate", DateTime.Now.Date);
            bo.ValueFiller.AddRule("Erp_EmployeeLodging", "DepartureDate", new DateTime(DateTime.Now.Date.Year, 12, 31));

            bo.ValueFiller.AddRule("Erp_EmployeeLodging", "IsCheckedIn", 1);
            bo.ValueFiller.AddRule("Erp_EmployeeLodging", "IsCheckedOut", 0);

            bo.Lookups.AddLookUp("Erp_EmployeeLodging", "EmployeeId", true, "Erp_Employee", "EmployeeCode", "EmployeeCode", "EmployeeName", "EmployeeName");

            //bo.Lookups.AddLookUp("Erp_EmployeeLodging", "ResourceId", true, "Erp_Resource", "ResourceCode", "ResourceCode", "Explanation", "ResourceExplanation");
            bo.Lookups.AddLookUp("Erp_EmployeeLodging", "ResourceId", true, "Erp_Resource", "ResourceCode", "ResourceCode", new[] { "Explanation", "LocationBuilding", "LocationFloor", "LocationAisle", "ParentResourceId", "BedTypeId", "Capacity", "PaxType1", "PaxType2", "PaxType3", "PaxType99" }, new[] { "ResourceExplanation", "LocationBuilding", "LocationFloor", "LocationAisle", "ParentResourceId", "BedTypeId", "Capacity", "PaxType1", "PaxType2", "PaxType3", "PaxType99" });
        }

        private void HrmEmployeePm_Init_EmployeeLodging(PMBase pm, PmParam parameter)
        {
            hrmEmployeePm = pm as HRMEmployeePM;
            if (hrmEmployeePm == null)
            {
                return;
            }
            Lists = hrmEmployeePm.ActiveSession.LookupList.GetChild(UtilityFunctions.GetConnection(hrmEmployeePm.ActiveSession.dbInfo.DBProvider, hrmEmployeePm.ActiveSession.dbInfo.ConnectionString));
            LookupList.Instance.AddLookupList("LodgingBookingTypeList", "Display", typeof(string), new object[] { SLanguage.GetString("Kesin"), SLanguage.GetString("Rezerve") }, "Value", typeof(object), new object[] { (byte)0, (byte)1 });
            //LookupList.Instance.AddLookupList("LodgingAciTypeList", "Display", typeof(string), new object[] { SLanguage.GetString("Bay"), SLanguage.GetString("Bayan"), SLanguage.GetString("Aile") }, "Value", typeof(object), new object[] { (byte)0, (byte)1, (byte)2 });

            LiveTabControl liveTabControl = hrmEmployeePm.FCtrl("GenelTab") as LiveTabControl;
            if (liveTabControl != null)
            {
                ltiEmployeeLodging = new LiveTabItem();
                ltiEmployeeLodging.Header = SLanguage.GetString("Lojman");
                liveTabControl.Items.Add(ltiEmployeeLodging);

                PMDesktop pMDesktop = hrmEmployeePm.container.Resolve<PMDesktop>();
                var tseEmployeeLodgingView = pMDesktop.LoadXamlRes("LodgingDetailsW");
                (tseEmployeeLodgingView._view as UserControl).DataContext = hrmEmployeePm;
                ltiEmployeeLodging.Content = tseEmployeeLodgingView._view;

                ltiEmployeeLog = new LiveTabItem();
                ltiEmployeeLog.Header = SLanguage.GetString("Tarihçe");
                liveTabControl.Items.Add(ltiEmployeeLog);

                var tseEmployeeLogView = pMDesktop.LoadXamlRes("LogDetailsW");
                (tseEmployeeLogView._view as UserControl).DataContext = hrmEmployeePm;
                ltiEmployeeLog.Content = tseEmployeeLogView._view;
            }

            if (hrmEmployeePm.ActiveBO != null && hrmEmployeePm.ActiveBO.Data.Tables.Contains("Erp_EmployeeLodging"))
            {
                if (!hrmEmployeePm.ActiveBO.Data.Tables["Erp_EmployeeLodging"].Columns.Contains("BedTypeName"))
                {
                    DataColumn dc = new DataColumn();
                    dc.ColumnName = "BedTypeName";
                    dc.DataType = UdtTypes.GetUdtSystemType(Schema.Tables["Meta_BedType"].Fields["BedTypeName"].UdtType);
                    dc.DefaultValue = 0;
                    hrmEmployeePm.ActiveBO.Data.Tables["Erp_EmployeeLodging"].Columns.Add(dc);
                }
                if (!hrmEmployeePm.ActiveBO.Data.Tables["Erp_EmployeeLodging"].Columns.Contains("ParentResourceName"))
                {
                    DataColumn dc = new DataColumn();
                    dc.ColumnName = "ParentResourceName";
                    dc.DataType = UdtTypes.GetUdtSystemType(Schema.Tables["Erp_Resource"].Fields["Explanation"].UdtType);
                    dc.DefaultValue = 0;
                    hrmEmployeePm.ActiveBO.Data.Tables["Erp_EmployeeLodging"].Columns.Add(dc);
                }

                if (!hrmEmployeePm.ActiveBO.Data.Tables["Erp_EmployeeLodging"].Columns.Contains("AccommodationNames"))
                {
                    hrmEmployeePm.ActiveBO.Data.Tables["Erp_EmployeeLodging"].Columns.Add(new DataColumn() { ColumnName = "AccommodationNames", DataType = UdtTypes.GetUdtSystemType(UdtType.UdtTextLong) });
                }
                if (!hrmEmployeePm.ActiveBO.Data.Tables["Erp_EmployeeLodging"].Columns.Contains("RemainingCapacity"))
                {
                    hrmEmployeePm.ActiveBO.Data.Tables["Erp_EmployeeLodging"].Columns.Add(new DataColumn() { ColumnName = "RemainingCapacity", DataType = UdtTypes.GetUdtSystemType(UdtType.UdtQuantity) });
                }
                if (!hrmEmployeePm.ActiveBO.Data.Tables["Erp_EmployeeLodging"].Columns.Contains("AccommodationCapacity"))
                {
                    hrmEmployeePm.ActiveBO.Data.Tables["Erp_EmployeeLodging"].Columns.Add(new DataColumn() { ColumnName = "AccommodationCapacity", DataType = UdtTypes.GetUdtSystemType(UdtType.UdtQuantity) });
                }
            }

            if (hrmEmployeePm.ActiveBO != null)
            {
                hrmEmployeePm.ActiveBO.AfterGet += ActiveBO_AfterGet;
                hrmEmployeePm.ActiveBO.ColumnChanged += ActiveBO_ColumnChanged1;
                hrmEmployeePm.ActiveBO.AfterSucceededPost += ActiveBO_AfterSucceededPost;
                hrmEmployeePm.ActiveBO.BeforePost += ActiveBO_BeforePost;
            }

            //hrmEmployeePm.CmdList.AddCmd(317, "ListCommand", SLanguage.GetString("Excelden Yükle"), OnHrmEmployeePmListCommand, null);

            //foreach (SysCommand sysCommand in hrmEmployeePm.CmdList)
            //{
            //    if (sysCommand.Name == "ListCommand")
            //    {
            //        sysCommand.Name = "OldListCommand";
            //    }
            //}
        }

        private void ActiveBO_BeforePost(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (e.Cancel)
            { return; }
            DateTime endDate;
            foreach (DataRow row in hrmEmployeePm.ActiveBO.Data.Tables["Erp_EmployeeLodging"].Select("", "", DataViewRowState.CurrentRows))
            {
                DateTime.TryParse(hrmEmployeePm.ActiveBO.CurrentRow["EndDate"].ToString(), out endDate);
                if (endDate.Date > new DateTime(1900, 1, 1))
                {
                    row["IsCheckedOut"] = 1;
                    row["DepartureDate"] = endDate;
                }
                else
                {
                    row["IsCheckedOut"] = 0;
                    row["DepartureDate"] = DBNull.Value;
                }
            }
        }

        private void ActiveBO_AfterSucceededPost(object sender, EventArgs e)
        {
            Init_LodgingValues();
        }

        private void ActiveBO_ColumnChanged1(object sender, DataColumnChangeEventArgs e)
        {
            if (!hrmEmployeePm.ActiveBO.ExtensionsEnabled)
                return;
            if (_suppressEvent)
                return;
            if (e.Column.ColumnName == "BedTypeId")
            {
                try
                {
                    _suppressEvent = true;
                    long bedTypeId;
                    long.TryParse(e.Row["BedTypeId"].ToString(), out bedTypeId);
                    if (bedTypeId != 0)
                    {
                        using (DataTable table = UtilityFunctions.GetDataTableList(hrmEmployeePm.ActiveBO.Provider, hrmEmployeePm.ActiveBO.Connection, hrmEmployeePm.ActiveBO.Transaction, "Meta_BedType", $"select BedTypeName from Meta_BedType with (nolock) where RecId={bedTypeId}"))
                        {
                            if (table?.Rows.Count > 0)
                            {
                                e.Row["BedTypeName"] = table.Rows[0]["BedTypeName"];
                            }
                        }
                    }
                    _suppressEvent = false;
                }
                catch
                {
                    _suppressEvent = false;
                }
            }
            else if (e.Column.ColumnName == "ParentResourceId")
            {
                try
                {
                    _suppressEvent = true;
                    long parentResourceId;
                    long.TryParse(e.Row["ParentResourceId"].ToString(), out parentResourceId);
                    if (parentResourceId != 0)
                    {
                        using (DataTable table = UtilityFunctions.GetDataTableList(hrmEmployeePm.ActiveBO.Provider, hrmEmployeePm.ActiveBO.Connection, hrmEmployeePm.ActiveBO.Transaction, "Erp_Resource", $"select Explanation from Erp_Resource with (nolock) where RecId={parentResourceId}"))
                        {
                            if (table?.Rows.Count > 0)
                            {
                                e.Row["ParentResourceName"] = table.Rows[0]["Explanation"];
                            }
                        }
                    }
                    _suppressEvent = false;
                }
                catch
                {
                    _suppressEvent = false;
                }
            }
            else if (e.Column.ColumnName == "EmployeeId")
            {
                try
                {
                    _suppressEvent = true;
                    e.Row["ArrivalDate"] = hrmEmployeePm.ActiveBO.CurrentRow["StartDate"];
                    _suppressEvent = false;
                }
                catch
                {
                    _suppressEvent = false;
                }
            }
            else if (e.Column.ColumnName == "ResourceId" || e.Column.ColumnName == "Capacity")
            {
                try
                {
                    _suppressEvent = true;
                    long resourceId, employeeId;
                    long.TryParse(e.Row["ResourceId"].ToString(), out resourceId);
                    long.TryParse(e.Row["EmployeeId"].ToString(), out employeeId);
                    string accommodationNames = "";
                    using (DataTable table = UtilityFunctions.GetDataTableList(hrmEmployeePm.ActiveBO.Provider, hrmEmployeePm.ActiveBO.Connection, hrmEmployeePm.ActiveBO.Transaction, "Erp_Employee", $"SELECT EE.EmployeeName,EE.EmployeeSurname FROM Erp_Employee EE WITH (NOLOCK) WHERE EE.RecId IN (SELECT EEL.EmployeeId FROM Erp_EmployeeLodging EEL WITH (NOLOCK) WHERE EEL.ResourceId={resourceId} AND EEL.IsCheckedIn=1 AND EEL.IsCheckedOut=0) and EE.RecId<>{employeeId}"))
                    {
                        foreach (DataRow dataRow2 in table.Rows)
                        {
                            if (string.IsNullOrEmpty(accommodationNames))
                            {
                                accommodationNames = $"{dataRow2["EmployeeName"]} {dataRow2["EmployeeSurname"]}";
                            }
                            else
                            {
                                accommodationNames += $", {dataRow2["EmployeeName"]} {dataRow2["EmployeeSurname"]}";
                            }
                        }
                    }
                    if (!string.IsNullOrEmpty(accommodationNames))
                        e.Row["AccommodationNames"] = accommodationNames;

                    using (DataTable table = UtilityFunctions.GetDataTableList(hrmEmployeePm.ActiveBO.Provider, hrmEmployeePm.ActiveBO.Connection, hrmEmployeePm.ActiveBO.Transaction, "Erp_Employee", $"SELECT ISNULL(COUNT(*),0) FROM Erp_EmployeeLodging EEL WITH (NOLOCK) WHERE EEL.ResourceId={resourceId} AND EEL.IsCheckedIn=1 AND EEL.IsCheckedOut=0"))
                    {
                        if (table?.Rows.Count > 0)
                        {
                            decimal cap, qty;
                            decimal.TryParse(e.Row["Capacity"].ToString(), out cap);
                            decimal.TryParse(table.Rows[0][0].ToString(), out qty);
                            e.Row["AccommodationCapacity"] = qty;
                            e.Row["RemainingCapacity"] = cap - qty;
                        }
                    }
                    _suppressEvent = false;
                }
                catch
                {
                    _suppressEvent = false;
                }
            }
        }

        private void ActiveBO_AfterGet(object sender, EventArgs e)
        {
            Init_LodgingValues();
        }

        private void Init_LodgingValues()
        {
            if (hrmEmployeePm != null)
            {
                if (hrmEmployeePm.ActiveBO?.CurrentRow != null && hrmEmployeePm.ActiveBO.Data.Tables.Contains("Erp_EmployeeLodging"))
                {
                    foreach (DataRow dataRow in hrmEmployeePm.ActiveBO.Data.Tables["Erp_EmployeeLodging"].Select("", "", DataViewRowState.CurrentRows))
                    {
                        long bedTypeId;
                        long.TryParse(dataRow["BedTypeId"].ToString(), out bedTypeId);
                        if (bedTypeId != 0)
                        {
                            using (DataTable table = UtilityFunctions.GetDataTableList(hrmEmployeePm.ActiveBO.Provider, hrmEmployeePm.ActiveBO.Connection, hrmEmployeePm.ActiveBO.Transaction, "Meta_BedType", $"select BedTypeName from Meta_BedType with (nolock) where RecId={bedTypeId}"))
                            {
                                if (table?.Rows.Count > 0)
                                {
                                    dataRow["BedTypeName"] = table.Rows[0]["BedTypeName"];
                                }
                            }
                        }

                        long parentResourceId;
                        long.TryParse(dataRow["ParentResourceId"].ToString(), out parentResourceId);
                        if (bedTypeId != 0)
                        {
                            using (DataTable table = UtilityFunctions.GetDataTableList(hrmEmployeePm.ActiveBO.Provider, hrmEmployeePm.ActiveBO.Connection, hrmEmployeePm.ActiveBO.Transaction, "Erp_Resource", $"select Explanation from Erp_Resource with (nolock) where RecId={parentResourceId}"))
                            {
                                if (table?.Rows.Count > 0)
                                {
                                    dataRow["ParentResourceName"] = table.Rows[0]["Explanation"];
                                }
                            }
                        }

                        long resourceId, employeeId;
                        long.TryParse(dataRow["ResourceId"].ToString(), out resourceId);
                        long.TryParse(dataRow["EmployeeId"].ToString(), out employeeId);
                        string accommodationNames = "";
                        using (DataTable table = UtilityFunctions.GetDataTableList(hrmEmployeePm.ActiveBO.Provider, hrmEmployeePm.ActiveBO.Connection, hrmEmployeePm.ActiveBO.Transaction, "Erp_Employee", $"SELECT EE.EmployeeName,EE.EmployeeSurname FROM Erp_Employee EE WITH (NOLOCK) WHERE EE.RecId IN (SELECT EEL.EmployeeId FROM Erp_EmployeeLodging EEL WITH (NOLOCK) WHERE EEL.ResourceId={resourceId} AND EEL.IsCheckedIn=1 AND EEL.IsCheckedOut=0) and EE.RecId<>{employeeId}"))
                        {
                            foreach (DataRow dataRow2 in table.Rows)
                            {
                                if (string.IsNullOrEmpty(accommodationNames))
                                {
                                    accommodationNames = $"{dataRow2["EmployeeName"]} {dataRow2["EmployeeSurname"]}";
                                }
                                else
                                {
                                    accommodationNames += $", {dataRow2["EmployeeName"]} {dataRow2["EmployeeSurname"]}";
                                }
                            }
                        }
                        if (!string.IsNullOrEmpty(accommodationNames))
                            dataRow["AccommodationNames"] = accommodationNames;

                        using (DataTable table = UtilityFunctions.GetDataTableList(hrmEmployeePm.ActiveBO.Provider, hrmEmployeePm.ActiveBO.Connection, hrmEmployeePm.ActiveBO.Transaction, "Erp_Employee", $"SELECT ISNULL(COUNT(*),0) FROM Erp_EmployeeLodging EEL WITH (NOLOCK) WHERE EEL.ResourceId={resourceId} AND EEL.IsCheckedIn=1 AND EEL.IsCheckedOut=0"))
                        {
                            if (table?.Rows.Count > 0)
                            {
                                decimal cap, qty;
                                decimal.TryParse(dataRow["Capacity"].ToString(), out cap);
                                decimal.TryParse(table.Rows[0][0].ToString(), out qty);
                                dataRow["AccommodationCapacity"] = qty;
                                dataRow["RemainingCapacity"] = cap - qty;
                            }
                        }
                    }

                    LiveGridControl[] grids = FrameworkTreeHelper.FindLogicalChilds<LiveGridControl>((hrmEmployeePm as PMDesktop).ActiveViewControl);
                    if (grids != null)
                    {
                        foreach (LiveGridControl grd in grids.Where(b => b.Name == "gridEmployeeLog"))
                        {
                            using (DataTable table = UtilityFunctions.GetDataTableList(hrmEmployeePm.ActiveBO.Provider, hrmEmployeePm.ActiveBO.Connection, hrmEmployeePm.ActiveBO.Transaction, "Log_TransactionTurunc", $"SELECT LTT.* FROM Log_TransactionTurunc LTT WITH (NOLOCK) WHERE BOName = 'HRMEmployeeBO' AND BORecId = {hrmEmployeePm.ActiveBO.CurrentRow["RecId"]} ORDER BY OperationDate DESC, OperationTime DESC"))
                            {
                                if (table?.Rows.Count > 0)
                                {
                                    grd.ItemsSource = table.DefaultView;
                                }
                            }
                        }
                    }
                }
            }
        }

        private void HrmEmployeePm_Dispose_EmployeeLodging(PMBase pm, PmParam parameter)
        {
            if (hrmEmployeePm != null)
            {
                if (hrmEmployeePm.ActiveBO != null)
                {
                    hrmEmployeePm.ActiveBO.AfterGet -= ActiveBO_AfterGet;
                    hrmEmployeePm.ActiveBO.ColumnChanged -= ActiveBO_ColumnChanged1;
                    hrmEmployeePm.ActiveBO.AfterSucceededPost -= ActiveBO_AfterSucceededPost;
                }
            }
        }

        private void HrmEmployeePm_ViewLoaded_EmployeeLodging(object sender, RoutedEventArgs e)
        {
            /*
            foreach (var menuItem in hrmEmployeePm.contextMenu.Items)
            {
                if (!(menuItem is System.Windows.Controls.MenuItem))
                    continue;

                if (((menuItem as System.Windows.Controls.MenuItem).Command as SysCommand)?.Name == "ListCommand")
                {
                    (menuItem as System.Windows.Controls.MenuItem).Command = hrmEmployeePm.CmdList["HrmEmployeeOnListCommand"];
                    break;
                }
            }
            */
        }

        private bool HrmEmployeePm_OnListCommand(PMBase pm, PmParam parameter, ISysCommandParam commandParam)
        {
            var focusScope = FocusManager.GetFocusScope((pm as PMDesktop).ActiveViewControl);
            var element = FocusManager.GetFocusedElement(focusScope) as FrameworkElement;
            LiveGridControl grid = FrameworkTreeHelper.FindParent<LiveGridControl>(element);
            if (grid != null && grid.Name == "gridEmployeeLodging")
            {
                //LiveGridControl[] grids = FrameworkTreeHelper.FindLogicalChilds<LiveGridControl>((pm as PMDesktop).ActiveViewControl);
                //if (grids != null)
                //{
                //    foreach (LiveGridControl grd in grids.Where(b => b.Name == "gridEmployeeLodging"))
                //    {
                //        grd.Lookups = Lists;
                //    }
                //}
                if (grid.CurrentColumn.Tag is ReceiptColumn && (grid.CurrentColumn.Tag as ReceiptColumn).ColumnName == "ResourceCode")
                {
                    SysMng.Instance.ActWndMng.ShowReport("Crs_ResourceResourceCodeList", true, ResourceListValueHandler, new DlgArgs("RecId") { ForWhichScreen = grid }, null, new PolicyParams(), "WorkListW", ReportWorkMode.ChoseList);
                    return true;
                }
                else
                    return false;
            }
            else
                return false;
        }

        private void ResourceListValueHandler(DlgArgs result)
        {
            if (result.ForWhichScreen is LiveGridControl)
            {
                if ((result.ForWhichScreen as LiveGridControl).CurrentItem is DataRowView)
                {
                    ((result.ForWhichScreen as LiveGridControl).CurrentItem as DataRowView).Row["ResourceId"] = result.DlgReturnValue;
                    ((result.ForWhichScreen as LiveGridControl).View as ReceiptView).MoveNextCell();
                }
            }
        }
    }
}
