using System;
using System.Collections.Generic;
using System.Linq;
using System.Data;
using Sentez.Common.SystemServices;
using Sentez.Data.BusinessObjects;
using System.Collections.ObjectModel;
using Sentez.Localization;
using Sentez.Common;
using Prism.Ioc;

namespace Sentez.CRSModule.Models
{
    [Serializable]
    public class ResourceAttributeModel : IDisposable
    {
        private IContainerExtension container { get; set; }

        private ISystemService getResourceAttributeSetItemTableService;

        private long resourceId;
        private string resourceKeyField = string.Empty;
        private DataTable serviceResultTable;
        private DataTable resourceAttributeTable;

        private ObservableCollection<object> itemsCollection;
        private ObservableCollection<object> selectedItemsCollection;
        private bool isLoading;

        private List<string> errorMesseages;

        private long? bookingItemPlanId;

        public ObservableCollection<object> ItemsCollection
        {
            get { return itemsCollection; }
            private set { itemsCollection = value; }
        }

        public ObservableCollection<object> SelectedItemsCollection
        {
            get { return selectedItemsCollection; }
            private set { selectedItemsCollection = value; }
        }

        private bool allowNew;
        public bool AllowNew
        {
            get { return allowNew; }
            set { allowNew = value; }
        }

        private bool allowRemove;
        public bool AllowRemove
        {
            get { return allowRemove; }
            set { allowRemove = value; }
        }

        public bool HasError { get { if (errorMesseages != null && errorMesseages.Count > 0) return true; return false; } }

        public ResourceAttributeModel(IContainerExtension _container)
        {
            this.container = _container;
            this.errorMesseages = new List<string>();
            this.itemsCollection = new ObservableCollection<object>();
            this.selectedItemsCollection = new ObservableCollection<object>();
            this.selectedItemsCollection.CollectionChanged += new System.Collections.Specialized.NotifyCollectionChangedEventHandler(selectedItemsCollection_CollectionChanged);
            getResourceAttributeSetItemTableService = container.Resolve<ISystemService>("GetResourceAttributeSetItemTableService");

            GetItemsCollection();
        }

        public void SetProperty(IBusinessObject bo)
        {
            if (bo == null)
                return;

            if (bo.Data.Tables.Contains("Erp_ResourceAttribute"))
            {
                resourceKeyField = "ResourceId";
                this.resourceAttributeTable = bo.Data.Tables["Erp_ResourceAttribute"];
                resourceId = Convert.ToInt64(bo.CurrentRow?.Row != null ? bo.CurrentRow.Row["RecId"] : bo.Data.Tables[bo.BaseTable].Rows[0]["RecId"]);
            }
            else if (bo.Data.Tables.Contains("Crs_BookingItemPlanResAttr"))
            {
                resourceKeyField = "BookingItemPlanId";
                this.resourceAttributeTable = bo.Data.Tables["Crs_BookingItemPlanResAttr"];
                if (bo.CurrentRow.Row.Table.Columns.Contains("IsIndividual") && Convert.ToByte(bo.CurrentRow.Row["IsIndividual"]) == 1 && bo.Data.Tables.Contains("Crs_BookingItemPlan") && bo.Data.Tables["Crs_BookingItemPlan"].Rows.Count > 0)
                    resourceId = Convert.ToInt64(bo.Data.Tables["Crs_BookingItemPlan"].Rows[0]["RecId"]);
            }
        }

        public void SetProperty(long _resourceId, DataTable _resourceAttributeTable)
        {
            this.resourceId = _resourceId;
            this.resourceAttributeTable = _resourceAttributeTable;
        }

        private void GetItemsCollection()
        {
            if (getResourceAttributeSetItemTableService == null)
            {
                errorMesseages.Add(SLanguage.GetString("Oda özellikleri detay tablosu servisine erişilemedi."));
                return;
            }

            serviceResultTable = getResourceAttributeSetItemTableService.Execute((int)ResourceTypeDefinition.ResourceType.Room) as DataTable;
        }

        private void MergeItems()
        {
            bool checkBoRows = resourceAttributeTable != null && resourceAttributeTable.Rows.Count > 0;

            if (serviceResultTable.Rows.Count > 0)
                isLoading = true;

            foreach (DataRow dr in serviceResultTable.Rows)
            {
                if (dr["ResAttSetItemId"] != DBNull.Value && dr["ResAttSetId"] != DBNull.Value)
                {
                    itemsCollection.Add(new ObjPair() { Display = dr["AttributeItemName"].ToString(), Value = Convert.ToInt64(dr["ResAttSetItemId"]) });

                    string query;
                    query = !bookingItemPlanId.HasValue ? string.Format("AttributeSetId = {0} and AttributeSetItemId = {1}", dr["ResAttSetId"], dr["ResAttSetItemId"]) : string.Format("BookingItemPlanId = {0} and AttributeSetId = {1} and AttributeSetItemId = {2}", bookingItemPlanId.Value, dr["ResAttSetId"], dr["ResAttSetItemId"]);

                    if (checkBoRows)
                        if (resourceAttributeTable.Select(query).Length > 0)
                            selectedItemsCollection.Add(itemsCollection[itemsCollection.Count - 1]);
                }
            }

            isLoading = false;
        }

        void selectedItemsCollection_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (isLoading)
                return;

            ObjPair item = null;

            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
            {
                if (AllowNew)
                    return;

                item = e.NewItems[0] as ObjPair;

                if (resourceAttributeTable != null)
                {
                    DataRow resAttItemRow = FindResourceAttributeSetItemRow(item);

                    DataRow resAttRow = resourceAttributeTable.NewRow();
                    resourceAttributeTable.Rows.Add(resAttRow);

                    if (bookingItemPlanId.HasValue)
                        resAttRow[resourceKeyField] = bookingItemPlanId.Value;
                    else
                        resAttRow[resourceKeyField] = resourceId;

                    resAttRow["AttributeSetId"] = resAttItemRow["ResAttSetId"];
                    resAttRow["AttributeSetItemId"] = resAttItemRow["ResAttSetItemId"];
                }
            }
            else if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Remove)
            {
                if (!AllowRemove)
                    return;

                item = e.OldItems[0] as ObjPair;

                DataRow resAttItemRow = FindResourceAttributeSetItemRow(item);

                DataRow resAttRow;

                if (bookingItemPlanId.HasValue)
                {
                    resAttRow = (from r in resourceAttributeTable.AsEnumerable()
                                 where r.RowState != DataRowState.Deleted
                                     &&
                                     Convert.ToInt64(r["BookingItemPlanId"]) == bookingItemPlanId.Value
                                     &&
                                     Convert.ToInt64(r["AttributeSetId"]) == Convert.ToInt64(resAttItemRow["ResAttSetId"])
                                     &&
                                     Convert.ToInt64(r["AttributeSetItemId"]) == Convert.ToInt64(resAttItemRow["ResAttSetItemId"])
                                 select r).FirstOrDefault();
                }
                else
                {
                    resAttRow = (from r in resourceAttributeTable.AsEnumerable()
                                 where r.RowState != DataRowState.Deleted
                                     &&
                                     Convert.ToInt64(r["AttributeSetId"]) == Convert.ToInt64(resAttItemRow["ResAttSetId"])
                                     &&
                                     Convert.ToInt64(r["AttributeSetItemId"]) == Convert.ToInt64(resAttItemRow["ResAttSetItemId"])
                                 select r).FirstOrDefault();
                }
                resAttRow?.Delete();
            }
        }

        DataRow FindResourceAttributeSetItemRow(ObjPair selObj)
        {
            if (selObj == null)
                return null;

            return (from r in serviceResultTable.AsEnumerable() where r.Field<long>("ResAttSetItemId") == (long)selObj.Value select r).FirstOrDefault();
        }

        public void LoadModel(long _bookingItemPlanId)
        {
            this.bookingItemPlanId = _bookingItemPlanId;
            LoadModel();
        }

        public void LoadModel()
        {
            Clear();
            MergeItems();
        }

        private void Clear()
        {
            isLoading = true;
            this.errorMesseages.Clear();
            this.itemsCollection.Clear();
            this.selectedItemsCollection.Clear();
            this.itemsCollection = null;
            this.selectedItemsCollection = null;
            this.itemsCollection = new ObservableCollection<object>();
            this.selectedItemsCollection = new ObservableCollection<object>();
            this.selectedItemsCollection.CollectionChanged += selectedItemsCollection_CollectionChanged;
            isLoading = false;
        }

        public bool IsDisposed = false;
        public void Dispose()
        {
            if (IsDisposed)
                return;

            if (getResourceAttributeSetItemTableService is IDisposable)
                (getResourceAttributeSetItemTableService as IDisposable).Dispose();

            getResourceAttributeSetItemTableService = null;

            serviceResultTable?.Dispose();
            resourceAttributeTable?.Dispose();

            // önce selectedlar gidiyor.
            if (selectedItemsCollection != null)
            {
                this.selectedItemsCollection.CollectionChanged -= selectedItemsCollection_CollectionChanged;
                selectedItemsCollection.Clear();
            }

            itemsCollection?.Clear();
            itemsCollection = null;
            selectedItemsCollection = null;

            errorMesseages?.Clear();
            errorMesseages = null;
        }
    }
}
