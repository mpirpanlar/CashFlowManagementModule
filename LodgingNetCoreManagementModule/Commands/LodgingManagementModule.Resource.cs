using LiveCore.Desktop.Common;
using LiveCore.Desktop.UI.Controls;

using Prism.Ioc;


//using Microsoft.Office.Interop.Excel;

using Sentez.Common.Commands;
using Sentez.Common.ModuleBase;
using Sentez.Common.PresentationModels;
using Sentez.Common.Utilities;
using Sentez.CRSUIModule.PresentationModels;
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
        private void CrsResourcePm_ViewLoaded_EmployeeLodging(object sender, RoutedEventArgs e)
        {
            //throw new NotImplementedException();
        }

        private void CrsResourcePm_Dispose_EmployeeLodging(PMBase pm, PmParam parameter)
        {
            if (crsResourcePm.ActiveBO != null)
            {
                crsResourcePm.ActiveBO.AfterGet -= ActiveBO_AfterGet1;
                crsResourcePm.ActiveBO.ColumnChanged -= ActiveBO_ColumnChanged;
                crsResourcePm.ActiveBO.AfterSucceededPost -= ActiveBO_AfterSucceededPost1;
                crsResourcePm.ActiveBO.BeforePost -= ActiveBO_BeforePost1;
            }
        }

        private void CrsResourcePm_Init_EmployeeLodging(PMBase pm, PmParam parameter)
        {
            crsResourcePm = pm as CrsResourcePM;
            if (crsResourcePm == null)
            {
                return;
            }
            Lists = crsResourcePm.ActiveSession.LookupList.GetChild(UtilityFunctions.GetConnection(crsResourcePm.ActiveSession.dbInfo.DBProvider, crsResourcePm.ActiveSession.dbInfo.ConnectionString));
            LookupList.Instance.AddLookupList("LodgingBookingTypeList", "Display", typeof(string), new object[] { SLanguage.GetString("Kesin"), SLanguage.GetString("Rezerve") }, "Value", typeof(object), new object[] { (byte)0, (byte)1 });
            //LookupList.Instance.AddLookupList("LodgingAciTypeList", "Display", typeof(string), new object[] { SLanguage.GetString("Bay"), SLanguage.GetString("Bayan"), SLanguage.GetString("Aile") }, "Value", typeof(object), new object[] { (byte)0, (byte)1, (byte)2 });

            LiveDocumentGroup liveTabControl = crsResourcePm.FCtrl("GenelDocumentPanel") as LiveDocumentGroup;
            if (liveTabControl != null)
            {
                ltiEmployeeLodgingResource = new LiveDocumentPanel();
                ltiEmployeeLodgingResource.Caption = SLanguage.GetString("Konaklayanlar");
                liveTabControl.Items.Add(ltiEmployeeLodgingResource);

                PMDesktop pMDesktop = crsResourcePm.container.Resolve<PMDesktop>();
                var tseEmployeeLodgingView = pMDesktop.LoadXamlRes("LodgingDetailsResourceW");
                (tseEmployeeLodgingView._view as UserControl).DataContext = crsResourcePm;
                ltiEmployeeLodgingResource.Content = tseEmployeeLodgingView._view;

            }

            if (crsResourcePm.ActiveBO != null && crsResourcePm.ActiveBO.Data.Tables.Contains("Erp_EmployeeLodging"))
            {
                if (!crsResourcePm.ActiveBO.Data.Tables["Erp_EmployeeLodging"].Columns.Contains("BedTypeName"))
                {
                    DataColumn dc = new DataColumn();
                    dc.ColumnName = "BedTypeName";
                    dc.DataType = UdtTypes.GetUdtSystemType(Schema.Tables["Meta_BedType"].Fields["BedTypeName"].UdtType);
                    dc.DefaultValue = 0;
                    crsResourcePm.ActiveBO.Data.Tables["Erp_EmployeeLodging"].Columns.Add(dc);
                }
                if (!crsResourcePm.ActiveBO.Data.Tables["Erp_EmployeeLodging"].Columns.Contains("ParentResourceName"))
                {
                    DataColumn dc = new DataColumn();
                    dc.ColumnName = "ParentResourceName";
                    dc.DataType = UdtTypes.GetUdtSystemType(Schema.Tables["Erp_Resource"].Fields["Explanation"].UdtType);
                    dc.DefaultValue = 0;
                    crsResourcePm.ActiveBO.Data.Tables["Erp_EmployeeLodging"].Columns.Add(dc);
                }

                if (!crsResourcePm.ActiveBO.Data.Tables["Erp_EmployeeLodging"].Columns.Contains("AccommodationNames"))
                {
                    crsResourcePm.ActiveBO.Data.Tables["Erp_EmployeeLodging"].Columns.Add(new DataColumn() { ColumnName = "AccommodationNames", DataType = UdtTypes.GetUdtSystemType(UdtType.UdtTextLong) });
                }
                if (!crsResourcePm.ActiveBO.Data.Tables["Erp_EmployeeLodging"].Columns.Contains("RemainingCapacity"))
                {
                    crsResourcePm.ActiveBO.Data.Tables["Erp_EmployeeLodging"].Columns.Add(new DataColumn() { ColumnName = "RemainingCapacity", DataType = UdtTypes.GetUdtSystemType(UdtType.UdtQuantity) });
                }
                if (!crsResourcePm.ActiveBO.Data.Tables["Erp_EmployeeLodging"].Columns.Contains("AccommodationCapacity"))
                {
                    crsResourcePm.ActiveBO.Data.Tables["Erp_EmployeeLodging"].Columns.Add(new DataColumn() { ColumnName = "AccommodationCapacity", DataType = UdtTypes.GetUdtSystemType(UdtType.UdtQuantity) });
                }
                if (!crsResourcePm.ActiveBO.Data.Tables["Erp_EmployeeLodging"].Columns.Contains("EmployeeDepartmentCode"))
                {
                    crsResourcePm.ActiveBO.Data.Tables["Erp_EmployeeLodging"].Columns.Add(new DataColumn() { ColumnName = "EmployeeDepartmentCode", DataType = UdtTypes.GetUdtSystemType(UdtType.UdtCode) });
                }
                if (!crsResourcePm.ActiveBO.Data.Tables["Erp_EmployeeLodging"].Columns.Contains("EmployeeDepartmentName"))
                {
                    crsResourcePm.ActiveBO.Data.Tables["Erp_EmployeeLodging"].Columns.Add(new DataColumn() { ColumnName = "EmployeeDepartmentName", DataType = UdtTypes.GetUdtSystemType(UdtType.UdtName) });
                }
            }

            if (crsResourcePm.ActiveBO != null)
            {
                crsResourcePm.ActiveBO.AfterGet += ActiveBO_AfterGet1;
                crsResourcePm.ActiveBO.ColumnChanged += ActiveBO_ColumnChanged;
                crsResourcePm.ActiveBO.AfterSucceededPost += ActiveBO_AfterSucceededPost1;
                crsResourcePm.ActiveBO.BeforePost += ActiveBO_BeforePost1;
            }
        }

        private void ActiveBO_BeforePost1(object sender, System.ComponentModel.CancelEventArgs e)
        {
        }

        private void ActiveBO_AfterSucceededPost1(object sender, EventArgs e)
        {
            Init_LodgingValuesForResource();
        }

        private void ActiveBO_ColumnChanged(object sender, DataColumnChangeEventArgs e)
        {
            if (!crsResourcePm.ActiveBO.ExtensionsEnabled)
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
                        using (DataTable table = UtilityFunctions.GetDataTableList(crsResourcePm.ActiveBO.Provider, crsResourcePm.ActiveBO.Connection, crsResourcePm.ActiveBO.Transaction, "Meta_BedType", $"select BedTypeName from Meta_BedType with (nolock) where RecId={bedTypeId}"))
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
                        using (DataTable table = UtilityFunctions.GetDataTableList(crsResourcePm.ActiveBO.Provider, crsResourcePm.ActiveBO.Connection, crsResourcePm.ActiveBO.Transaction, "Erp_Resource", $"select Explanation from Erp_Resource with (nolock) where RecId={parentResourceId}"))
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
                    e.Row["ArrivalDate"] = crsResourcePm.ActiveBO.CurrentRow["StartDate"];
                    _suppressEvent = false;
                }
                catch
                {
                    _suppressEvent = false;
                }
            }
            else if (e.Column.ColumnName == "EmployeeDepartmentId")
            {
                try
                {
                    _suppressEvent = true;
                    int employeeDepartmentId;
                    int.TryParse(e.Row["EmployeeDepartmentId"].ToString(), out employeeDepartmentId);
                    if (employeeDepartmentId != 0)
                    {
                        using (DataTable table = UtilityFunctions.GetDataTableList(crsResourcePm.ActiveBO.Provider, crsResourcePm.ActiveBO.Connection, crsResourcePm.ActiveBO.Transaction, "Erp_Department", $"select DepartmentCode, DepartmentName from Erp_Department with (nolock) where RecId={employeeDepartmentId}"))
                        {
                            if (table?.Rows.Count > 0)
                            {
                                e.Row["EmployeeDepartmentCode"] = table.Rows[0]["DepartmentCode"];
                                e.Row["EmployeeDepartmentName"] = table.Rows[0]["DepartmentName"];
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
            else if (e.Column.ColumnName == "ResourceId" || e.Column.ColumnName == "Capacity")
            {
                try
                {
                    _suppressEvent = true;
                    long resourceId, employeeId;
                    long.TryParse(e.Row["ResourceId"].ToString(), out resourceId);
                    long.TryParse(e.Row["EmployeeId"].ToString(), out employeeId);
                    string accommodationNames = "";
                    using (DataTable table = UtilityFunctions.GetDataTableList(crsResourcePm.ActiveBO.Provider, crsResourcePm.ActiveBO.Connection, crsResourcePm.ActiveBO.Transaction, "Erp_Employee", $"SELECT EE.EmployeeName,EE.EmployeeSurname FROM Erp_Employee EE WITH (NOLOCK) WHERE EE.RecId IN (SELECT EEL.EmployeeId FROM Erp_EmployeeLodging EEL WITH (NOLOCK) WHERE EEL.ResourceId={resourceId} AND EEL.IsCheckedIn=1 AND EEL.IsCheckedOut=0) and EE.RecId<>{employeeId}"))
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

                    using (DataTable table = UtilityFunctions.GetDataTableList(crsResourcePm.ActiveBO.Provider, crsResourcePm.ActiveBO.Connection, crsResourcePm.ActiveBO.Transaction, "Erp_Employee", $"SELECT ISNULL(COUNT(*),0) FROM Erp_EmployeeLodging EEL WITH (NOLOCK) WHERE EEL.ResourceId={resourceId} AND EEL.IsCheckedIn=1 AND EEL.IsCheckedOut=0"))
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

        private void ActiveBO_AfterGet1(object sender, EventArgs e)
        {
            Init_LodgingValuesForResource();
        }

        private void CrsResourceBo_Init_EmployeeLodging(BusinessObjectBase bo, BoParam parameter)
        {
            bo.ValueFiller.AddRule("Erp_EmployeeLodging", "InUse", 1);
            bo.ValueFiller.AddRule("Erp_EmployeeLodging", "BookingType", (byte)0);
            //bo.ValueFiller.AddRule("Erp_EmployeeLodging", "AciType", (byte)0);
            bo.ValueFiller.AddRule("Erp_EmployeeLodging", "ArrivalDate", DateTime.Now.Date);
            bo.ValueFiller.AddRule("Erp_EmployeeLodging", "DepartureDate", new DateTime(DateTime.Now.Date.Year, 12, 31));

            bo.ValueFiller.AddRule("Erp_EmployeeLodging", "IsCheckedIn", 1);
            bo.ValueFiller.AddRule("Erp_EmployeeLodging", "IsCheckedOut", 0);

            bo.Lookups.AddLookUp("Erp_EmployeeLodging", "EmployeeId", true, "Erp_Employee", "EmployeeCode", "EmployeeCode", new[] { "EmployeeName", "EmployeeSurname", "DepartmentId" }, new[] { "EmployeeName", "EmployeeSurname", "EmployeeDepartmentId" });

            //bo.Lookups.AddLookUp("Erp_EmployeeLodging", "ResourceId", true, "Erp_Resource", "ResourceCode", "ResourceCode", "Explanation", "ResourceExplanation");
            bo.Lookups.AddLookUp("Erp_EmployeeLodging", "ResourceId", true, "Erp_Resource", "ResourceCode", "ResourceCode", new[] { "Explanation", "LocationBuilding", "LocationFloor", "LocationAisle", "ParentResourceId", "BedTypeId", "Capacity", "PaxType1", "PaxType2", "PaxType3", "PaxType99" }, new[] { "ResourceExplanation", "LocationBuilding", "LocationFloor", "LocationAisle", "ParentResourceId", "BedTypeId", "Capacity", "PaxType1", "PaxType2", "PaxType3", "PaxType99" });
        }

        private void CrsResourceBoCustomCons(ref short itemId, ref string keyColumn, ref string typeField, ref string[] Tables)
        {
            List<string> tableList = new List<string>();
            tableList.AddRange(Tables);

            tableList.Add("Erp_EmployeeLodging");
            Tables = tableList.ToArray();
        }

        private void Init_LodgingValuesForResource()
        {
            if (crsResourcePm != null)
            {
                if (crsResourcePm.ActiveBO?.CurrentRow != null && crsResourcePm.ActiveBO.Data.Tables.Contains("Erp_EmployeeLodging"))
                {
                    foreach (DataRow dataRow in crsResourcePm.ActiveBO.Data.Tables["Erp_EmployeeLodging"].Select("", "", DataViewRowState.CurrentRows))
                    {
                        long bedTypeId;
                        long.TryParse(dataRow["BedTypeId"].ToString(), out bedTypeId);
                        if (bedTypeId != 0)
                        {
                            using (DataTable table = UtilityFunctions.GetDataTableList(crsResourcePm.ActiveBO.Provider, crsResourcePm.ActiveBO.Connection, crsResourcePm.ActiveBO.Transaction, "Meta_BedType", $"select BedTypeName from Meta_BedType with (nolock) where RecId={bedTypeId}"))
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
                            using (DataTable table = UtilityFunctions.GetDataTableList(crsResourcePm.ActiveBO.Provider, crsResourcePm.ActiveBO.Connection, crsResourcePm.ActiveBO.Transaction, "Erp_Resource", $"select Explanation from Erp_Resource with (nolock) where RecId={parentResourceId}"))
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
                        using (DataTable table = UtilityFunctions.GetDataTableList(crsResourcePm.ActiveBO.Provider, crsResourcePm.ActiveBO.Connection, crsResourcePm.ActiveBO.Transaction, "Erp_Employee", $"SELECT EE.EmployeeName,EE.EmployeeSurname FROM Erp_Employee EE WITH (NOLOCK) WHERE EE.RecId IN (SELECT EEL.EmployeeId FROM Erp_EmployeeLodging EEL WITH (NOLOCK) WHERE EEL.ResourceId={resourceId} AND EEL.IsCheckedIn=1 AND EEL.IsCheckedOut=0) and EE.RecId<>{employeeId}"))
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

                        int departmentId;
                        int.TryParse(dataRow["EmployeeDepartmentId"].ToString(), out departmentId);
                        using (DataTable table = UtilityFunctions.GetDataTableList(crsResourcePm.ActiveBO.Provider, crsResourcePm.ActiveBO.Connection, crsResourcePm.ActiveBO.Transaction, "Erp_Employee", $"SELECT * FROM Erp_Department EE WITH (NOLOCK) WHERE EE.RecId={departmentId}"))
                        {
                            foreach (DataRow dataRow2 in table.Rows)
                            {
                                dataRow["EmployeeDepartmentCode"] = dataRow2["DepartmentCode"];
                                dataRow["EmployeeDepartmentName"] = dataRow2["DepartmentName"];
                            }
                        }

                        using (DataTable table = UtilityFunctions.GetDataTableList(crsResourcePm.ActiveBO.Provider, crsResourcePm.ActiveBO.Connection, crsResourcePm.ActiveBO.Transaction, "Erp_Employee", $"SELECT ISNULL(COUNT(*),0) FROM Erp_EmployeeLodging EEL WITH (NOLOCK) WHERE EEL.ResourceId={resourceId} AND EEL.IsCheckedIn=1 AND EEL.IsCheckedOut=0"))
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

                    LiveGridControl[] grids = FrameworkTreeHelper.FindLogicalChilds<LiveGridControl>((crsResourcePm as PMDesktop).ActiveViewControl);
                    if (grids != null)
                    {
                        foreach (LiveGridControl grd in grids.Where(b => b.Name == "gridEmployeeLog"))
                        {
                            using (DataTable table = UtilityFunctions.GetDataTableList(crsResourcePm.ActiveBO.Provider, crsResourcePm.ActiveBO.Connection, crsResourcePm.ActiveBO.Transaction, "Log_TransactionTurunc", $"SELECT LTT.* FROM Log_TransactionTurunc LTT WITH (NOLOCK) WHERE BOName = 'HRMEmployeeBO' AND BORecId = {crsResourcePm.ActiveBO.CurrentRow["RecId"]} ORDER BY OperationDate DESC, OperationTime DESC"))
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
    }
}
