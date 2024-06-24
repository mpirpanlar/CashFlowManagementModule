using LiveCore.Desktop.Common;
using LiveCore.Desktop.UI.Controls;


using Sentez.Common.ModuleBase;
using Sentez.Common.PresentationModels;
using Sentez.Common.Utilities;
using Sentez.Data.BusinessObjects;
using Sentez.Data.MetaData;
using Sentez.Data.Tools;
using Sentez.InventoryModule.PresentationModels;
using Sentez.Localization;
using Sentez.QuotationModule.PresentationModels;

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
        private void QuotationReceiptBo_Init_InventoryUnitItemSizeSetDetails(BusinessObjectBase bo, BoParam parameter)
        {
            bo.Lookups.AddLookUp("Erp_QuotationReceiptItem", "InventoryUnitItemSizeSetDetailsId", true, "Erp_InventoryUnitItemSizeSetDetails", "SizeDetailCode", "InventoryUnitItemSizeSetDetails_SizeDetailCode"
            , new string[] { 
                "SizeDetailName", 
                //"UnitItemId", 
                "UnitFactor", 
                "UnitDivisor", 
                "UnitWidth", 
                "UnitLength", 
                "UnitHeight" 
            }
            , new string[] { 
                "InventoryUnitItemSizeSetDetails_SizeDetailName", 
                //"InventoryUnitItemSizeSetDetails_UnitItemId", 
                "InventoryUnitItemSizeSetDetails_UnitFactor", 
                "InventoryUnitItemSizeSetDetails_UnitDivisor", 
                "InventoryUnitItemSizeSetDetails_UnitWidth", 
                "InventoryUnitItemSizeSetDetails_UnitLength", 
                "InventoryUnitItemSizeSetDetails_UnitHeight" 
            });
        }

        private void QuotationReceiptPm_Init_InventoryUnitItemSizeSetDetails(PMBase pm, PmParam parameter)
        {
            quotationReceiptPm = pm as QuotationReceiptPM;
            if (quotationReceiptPm == null)
            {
                return;
            }
            Lists_QuotationReceiptPM = quotationReceiptPm.ActiveSession.LookupList.GetChild(UtilityFunctions.GetConnection(quotationReceiptPm.ActiveSession.dbInfo.DBProvider, quotationReceiptPm.ActiveSession.dbInfo.ConnectionString));
            if (quotationReceiptPm.ActiveBO != null)
            {
                quotationReceiptPm.ActiveBO.ColumnChanged += ActiveBO_ColumnChanged_QuotationReceiptPm;
            }
        }

        private void QuotationReceiptPm_ViewLoaded_InventoryUnitItemSizeSetDetails(object sender, RoutedEventArgs e)
        {
            if (quotationReceiptPm?.ReceiptColumnCollection != null)
            {
                if (!quotationReceiptPm.ReceiptColumnCollection.Contains("InventoryUnitItemSizeSetDetails_SizeDetailCode"))
                    quotationReceiptPm.ReceiptColumnCollection.Add(new ReceiptColumn() { ColumnName = "InventoryUnitItemSizeSetDetails_SizeDetailCode", Caption = "Ölçü Kodu", EditorType = EditorType.ListSelector, Width = 100, LookUpTable = "Erp_InventoryUnitItemSizeSetDetails", LookUpField = "SizeDetailCode", IsVisible = false });
                if (!quotationReceiptPm.ReceiptColumnCollection.Contains("InventoryUnitItemSizeSetDetails_SizeDetailName"))
                    quotationReceiptPm.ReceiptColumnCollection.Add(new ReceiptColumn() { ColumnName = "InventoryUnitItemSizeSetDetails_SizeDetailName", Caption = "Ölçü Adı", EditorType = EditorType.ReadOnlyTextEditor, Width = 120, LookUpTable = "Erp_InventoryUnitItemSizeSetDetails", LookUpField = "SizeDetailName", IsVisible = false });
            }
        }

        private void ActiveBO_ColumnChanged_QuotationReceiptPm(object sender, DataColumnChangeEventArgs e)
        {
            //throw new NotImplementedException();
        }

        private void QuotationReceiptPm_Dispose_InventoryUnitItemSizeSetDetails(PMBase pm, PmParam parameter)
        {
            quotationReceiptPm = pm as QuotationReceiptPM;
            if (quotationReceiptPm == null)
            {
                return;
            }
            if (quotationReceiptPm.ActiveBO != null)
            {
                quotationReceiptPm.ActiveBO.ColumnChanged -= ActiveBO_ColumnChanged_QuotationReceiptPm;
            }
        }
    }
}
