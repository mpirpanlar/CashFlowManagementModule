using System.Windows;
using System.Windows.Controls;
using Sentez.Common.PresentationModels;
using Sentez.Common.SystemServices;
using Sentez.Common.Utilities;
using System.ComponentModel;
using Sentez.Localization;
using Sentez.Data.Tools;
using Sentez.CRSModule.Models;
using System.Data;
using System.Windows.Input;
using LiveCore.Desktop.UI.Controls;
using Sentez.Common.Commands;
using Sentez.CRSUIModule.Views;
using Prism.Ioc;

namespace Sentez.CRSUIModule.PresentationModels
{
    public class CrsResourcePM : PMDesktop
    {
        #region Properties

        public LookupList Lists { get; set; }
        LiveComboBoxEdit lcbeLocationBuilding, lcbeLocationFloor, lcbeLocationAisle;

        private DataTable _bedTypeTable;
        public DataTable BedTypeTable
        {
            get { return _bedTypeTable; }
            set { _bedTypeTable = value; OnPropertyChanged("BedTypeTable"); }
        }

        private DataTable _roomTypeTable;
        public DataTable RoomTypeTable
        {
            get { return _roomTypeTable; }
            set { _roomTypeTable = value; OnPropertyChanged("RoomTypeTable"); }
        }

        private DataTable _locationBuildingTable;
        public DataTable LocationBuildingTable
        {
            get { return _locationBuildingTable; }
            set { _locationBuildingTable = value; OnPropertyChanged("LocationBuildingTable"); }
        }

        private DataTable _locationFloorTable;
        public DataTable LocationFloorTable
        {
            get { return _locationFloorTable; }
            set { _locationFloorTable = value; OnPropertyChanged("LocationFloorTable"); }
        }

        private DataTable _locationAisleTable;
        public DataTable LocationAisleTable
        {
            get { return _locationAisleTable; }
            set { _locationAisleTable = value; OnPropertyChanged("LocationAisleTable"); }
        }
        #endregion

        #region UIComp
        LiveCore.Desktop.UI.Controls.LiveListBoxEdit _attributeSetItemList = null;
        #endregion

        ResourceAttributeModel _resourceAttributeModel;

        public CrsResourcePM(IContainerExtension container_)
            : base(container_)
        {
        }

        public override void Init()
        {
            base.Init();

            Lists = ActiveSession.LookupList.GetChild(UtilityFunctions.GetConnection(ActiveSession.dbInfo.DBProvider, ActiveSession.dbInfo.ConnectionString));
            Lists.AddLookupList("ResourceAvailableType", "Display", typeof(string), new object[] { SLanguage.GetString("Temiz"), SLanguage.GetString("Kirli") }, "Value", typeof(bool), new object[] { true, false });
            Lists.AddLookupList("LodgingBookingTypeList", "Display", typeof(string), new object[] { SLanguage.GetString("Kesin"), SLanguage.GetString("Rezerve"), SLanguage.GetString("Taşeron") }, "Value", typeof(object), new object[] { (byte)0, (byte)1, (byte)2 });
            ISystemService getRoomTypeService = container.Resolve<ISystemService>("GetRoomTypeService");
            if (getRoomTypeService != null)
                RoomTypeTable = getRoomTypeService.Execute(null) as DataTable;

            ISystemService getBedTypeService = container.Resolve<ISystemService>("GetBedTypeService");
            if (getBedTypeService != null)
                BedTypeTable = getBedTypeService.Execute(null) as DataTable;


            _attributeSetItemList = FCtrl("AttributeSetItemList") as LiveCore.Desktop.UI.Controls.LiveListBoxEdit;
            if (_attributeSetItemList != null)
            {
                _attributeSetItemList.SelectionMode = SelectionMode.Multiple;
                _attributeSetItemList.DisplayMember = "Display";
                _attributeSetItemList.EditValue = "Value";
                _attributeSetItemList.StyleSettings = new LiveCheckedListBoxStyleSettings();
                //_attributeSetItemList.Style = (Style)Application.Current.FindResource("FCheckBoxListStyle");
            }
            _resourceAttributeModel = container.Resolve<ResourceAttributeModel>();
            lcbeLocationBuilding = FCtrl("lcbeLocationBuilding") as LiveComboBoxEdit;
            if (lcbeLocationBuilding != null)
            {
                lcbeLocationBuilding.LostKeyboardFocus += LcbeLocationBuilding_LostKeyboardFocus;
            }
            lcbeLocationFloor = FCtrl("lcbeLocationFloor") as LiveComboBoxEdit;
            if (lcbeLocationFloor != null)
            {
                lcbeLocationFloor.LostKeyboardFocus += LcbeLocationFloor_LostKeyboardFocus;
            }
            lcbeLocationAisle = FCtrl("lcbeLocationAisle") as LiveComboBoxEdit;
            if (lcbeLocationAisle != null)
            {
                lcbeLocationAisle.LostKeyboardFocus += LcbeLocationAisle_LostKeyboardFocus;
            }
            if (ActiveBO != null)
            {
                LocationBuildingTable = UtilityFunctions.GetDataTableList(ActiveBO.Provider, ActiveBO.ActiveSession.dbInfo.GetNewConnection(), ActiveBO.Transaction, "Erp_Resource", $"SELECT ER.LocationBuilding FROM Erp_Resource ER WITH (NOLOCK) WHERE ER.CompanyId={ActiveSession.ActiveCompany.RecId} AND ER.ResourceType=5 GROUP BY ER.LocationBuilding");
                LocationFloorTable = UtilityFunctions.GetDataTableList(ActiveBO.Provider, ActiveBO.ActiveSession.dbInfo.GetNewConnection(), ActiveBO.Transaction, "Erp_Resource", $"SELECT ER.LocationFloor FROM Erp_Resource ER WITH (NOLOCK) WHERE ER.CompanyId={ActiveSession.ActiveCompany.RecId} AND ER.ResourceType=5 GROUP BY ER.LocationFloor");
                LocationAisleTable = UtilityFunctions.GetDataTableList(ActiveBO.Provider, ActiveBO.ActiveSession.dbInfo.GetNewConnection(), ActiveBO.Transaction, "Erp_Resource", $"SELECT ER.LocationAisle FROM Erp_Resource ER WITH (NOLOCK) WHERE ER.CompanyId={ActiveSession.ActiveCompany.RecId} AND ER.ResourceType=5 GROUP BY ER.LocationAisle");

                ActiveBO.PropertyChanged += ActiveBO_PropertyChanged;
                ActiveBO.BeforePost += ActiveBO_BeforePost;
                ActiveBO.AfterGet += ActiveBO_AfterGet;
                ActiveBO.AfterSucceededPost += ActiveBO_AfterSucceededPost;
                ActiveBO.ColumnChanged += ActiveBO_ColumnChanged;

                if (_pmParam.itemID > 0)
                {
                    ActiveBO.Get(_pmParam.itemID);
                }
                else
                {
                    if (!ActiveBO.IsNewRecord)
                    {
                        ActiveBO.NewRecord();
                    }
                }
            }
            InsertContextMenu(AddToMenu(new MenuItemPM("Separator_Produce", ""), null));
            InsertContextMenu(AddToMenu(new MenuItemPM("Oda Kartı Üretme", "ResourceProduceCommand") { ShortcutKey = Key.U, ShortcutKeyModifier = ModifierKeys.Control }, null));
        }

        private void LcbeLocationAisle_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (e.OldFocus is TextBox)
            {
                if (!string.IsNullOrEmpty((e.OriginalSource as TextBox).Text))
                {
                    if ((sender as LiveComboBoxEdit).ItemsSource is DataTable)
                    {
                        DataRow[] rows = ((sender as LiveComboBoxEdit).ItemsSource as DataTable).Select($"LocationAisle='{(e.OriginalSource as TextBox).Text}'", "", DataViewRowState.CurrentRows);
                        if (rows?.Length == 0)
                        {
                            DataRow newRow = ((sender as LiveComboBoxEdit).ItemsSource as DataTable).NewRow();
                            newRow["LocationAisle"] = (e.OriginalSource as TextBox).Text;
                            ((sender as LiveComboBoxEdit).ItemsSource as DataTable).Rows.Add(newRow);
                            ((sender as LiveComboBoxEdit).ItemsSource as DataTable).DefaultView.Sort = "LocationAisle";
                        }
                    }
                }
                if (ActiveBO?.CurrentRow != null)
                {
                    if (ActiveBO.CurrentRow["LocationAisle"].ToString() != (e.OriginalSource as TextBox).Text)
                        ActiveBO.CurrentRow["LocationAisle"] = (e.OriginalSource as TextBox).Text;
                }
            }
        }

        private void LcbeLocationFloor_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (e.OldFocus is TextBox)
            {
                if (!string.IsNullOrEmpty((e.OriginalSource as TextBox).Text))
                {
                    if ((sender as LiveComboBoxEdit).ItemsSource is DataTable)
                    {
                        DataRow[] rows = ((sender as LiveComboBoxEdit).ItemsSource as DataTable).Select($"LocationFloor='{(e.OriginalSource as TextBox).Text}'", "", DataViewRowState.CurrentRows);
                        if (rows?.Length == 0)
                        {
                            DataRow newRow = ((sender as LiveComboBoxEdit).ItemsSource as DataTable).NewRow();
                            newRow["LocationFloor"] = (e.OriginalSource as TextBox).Text;
                            ((sender as LiveComboBoxEdit).ItemsSource as DataTable).Rows.Add(newRow);
                            ((sender as LiveComboBoxEdit).ItemsSource as DataTable).DefaultView.Sort = "LocationFloor";
                        }
                    }
                }
                if (ActiveBO?.CurrentRow != null)
                {
                    if (ActiveBO.CurrentRow["LocationFloor"].ToString() != (e.OriginalSource as TextBox).Text)
                        ActiveBO.CurrentRow["LocationFloor"] = (e.OriginalSource as TextBox).Text;
                }
            }
        }

        private void LcbeLocationBuilding_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (e.OldFocus is TextBox)
            {
                if (!string.IsNullOrEmpty((e.OriginalSource as TextBox).Text))
                {
                    if ((sender as LiveComboBoxEdit).ItemsSource is DataTable)
                    {
                        DataRow[] rows = ((sender as LiveComboBoxEdit).ItemsSource as DataTable).Select($"LocationBuilding='{(e.OriginalSource as TextBox).Text}'", "", DataViewRowState.CurrentRows);
                        if (rows?.Length == 0)
                        {
                            DataRow newRow = ((sender as LiveComboBoxEdit).ItemsSource as DataTable).NewRow();
                            newRow["LocationBuilding"] = (e.OriginalSource as TextBox).Text;
                            ((sender as LiveComboBoxEdit).ItemsSource as DataTable).Rows.Add(newRow);
                            ((sender as LiveComboBoxEdit).ItemsSource as DataTable).DefaultView.Sort = "LocationBuilding";
                        }
                    }
                }
                if (ActiveBO?.CurrentRow != null)
                {
                    if (ActiveBO.CurrentRow["LocationBuilding"].ToString() != (e.OriginalSource as TextBox).Text)
                        ActiveBO.CurrentRow["LocationBuilding"] = (e.OriginalSource as TextBox).Text;
                }
            }
        }

        public override void LoadCommands()
        {
            base.LoadCommands();
            CmdList.AddCmd(305, "ResourceProduceCommand", SLanguage.GetString("Oda Kartı Üretme"), OnResourceProduceCommand, CanResourceProduceCommand);
        }
        public override void OnFirstCommand(ISysCommandParam obj)
        {
            if (_attributeSetItemList != null)
                _attributeSetItemList.EditValueChanged -= _attributeSetItemList_EditValueChanged;
            base.OnFirstCommand(obj);
        }
        public override void OnPreviousCommand(ISysCommandParam obj)
        {
            if (_attributeSetItemList != null)
                _attributeSetItemList.EditValueChanged -= _attributeSetItemList_EditValueChanged;
            base.OnPreviousCommand(obj);
        }
        public override void OnLastCommand(ISysCommandParam obj)
        {
            if (_attributeSetItemList != null)
                _attributeSetItemList.EditValueChanged -= _attributeSetItemList_EditValueChanged;
            base.OnLastCommand(obj);
        }
        public override void OnNextCommand(ISysCommandParam obj)
        {
            if (_attributeSetItemList != null)
                _attributeSetItemList.EditValueChanged -= _attributeSetItemList_EditValueChanged;
            base.OnNextCommand(obj);
        }
        public override void OnPostDataCommand(ISysCommandParam obj)
        {
            if (_attributeSetItemList != null)
                _attributeSetItemList.EditValueChanged -= _attributeSetItemList_EditValueChanged;
            base.OnPostDataCommand(obj);
        }
        private void ActiveBO_AfterSucceededPost(object sender, System.EventArgs e)
        {
            SetItemsValueOnUi();
        }

        private void ActiveBO_AfterGet(object sender, System.EventArgs e)
        {
            SetItemsValueOnUi();
        }

        private void SetItemsValueOnUi()
        {
            if (_attributeSetItemList != null && _resourceAttributeModel != null)
            {
                //_attributeSetItemList.EditValueChanged -= _attributeSetItemList_EditValueChanged;
                foreach (DataRow dr in ActiveBO.Data.Tables["Erp_ResourceAttribute"].Rows)
                {
                    long itemId;
                    long.TryParse(dr["AttributeSetItemId"].ToString(), out itemId);
                    foreach (var itm in _resourceAttributeModel.ItemsCollection)
                    {
                        long itmRecId;
                        long.TryParse(((Common.ObjPair)itm).Value.ToString(), out itmRecId);
                        if (itmRecId == itemId)
                        {
                            _attributeSetItemList.SelectedItems.Add(itm);
                            break;
                        }
                    }
                }
                _attributeSetItemList.EditValueChanged += _attributeSetItemList_EditValueChanged;
            }
        }

        private void _attributeSetItemList_EditValueChanged(object sender, DevExpress.Xpf.Editors.EditValueChangedEventArgs e)
        {
            if (!ActiveBO.HasDataChanges || !ActiveBO.IsNewRecord)
                ActiveBO.CurrentRow["RecId"] = ActiveBO.CurrentRow["RecId"];
        }

        private void ActiveBO_BeforePost(object sender, CancelEventArgs e)
        {
            if (!string.IsNullOrEmpty(ActiveBO.ErrorMessage) || ActiveBO.ErrorMessages.Count > 0) return;
            foreach (DataRow dr in ActiveBO.Data.Tables["Erp_ResourceAttribute"].Rows)
                dr.Delete();
            DataTable resourceAttributeSet = UtilityFunctions.GetDataTableList(ActiveBO.Provider, ActiveBO.Connection, ActiveBO.Transaction, "Erp_ResourceAttributeSet", string.Format("select * from Erp_ResourceAttributeSet with (nolock) where CompanyId={0} and ResourceType=5", ActiveSession.ActiveCompany.RecId));
            if (resourceAttributeSet != null && resourceAttributeSet.Rows.Count > 0)
            {
                foreach (var itm in _attributeSetItemList.SelectedItems)
                {
                    DataRow newRow = ActiveBO.Data.Tables["Erp_ResourceAttribute"].NewRow();
                    newRow.SetParentRow(ActiveBO.CurrentRow.Row);
                    ActiveBO.Data.Tables["Erp_ResourceAttribute"].Rows.Add(newRow);
                    newRow["AttributeSetId"] = resourceAttributeSet.Rows[0][0];
                    newRow["AttributeSetItemId"] = ((Common.ObjPair)itm).Value;
                }
            }
        }

        bool _viewLoaded;
        public override void _view_Loaded(object sender, RoutedEventArgs e)
        {
            if (_viewLoaded)
                return;

            base._view_Loaded(sender, e);

            if (_attributeSetItemList != null)
            {
                if (_resourceAttributeModel != null)
                {
                    _attributeSetItemList.ItemsSource = _resourceAttributeModel.ItemsCollection;
                }
            }
            _viewLoaded = true;
        }

        void ActiveBO_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "CurrentRow" && ActiveBO.CurrentRow != null)
            {
                //    _resourceAttributeModel.SetProperty(ActiveBO);
                //    _resourceAttributeModel.LoadModel();

                //    if (_attributeSetItemList != null)
                //    {
                //        _attributeSetItemList.ItemsSource = _resourceAttributeModel.ItemsCollection;
                //    }
            }
        }
        public void OnResourceProduceCommand(ISysCommandParam obj)
        {
            if (ActiveBO?.CurrentRow == null || ActiveBO.CurrentRow.Row.RowState == DataRowState.Detached || ActiveBO.CurrentRow.Row.RowState == DataRowState.Deleted || ActiveBO.CurrentRow.Row.IsNull("RecId")) return;
            try
            {
                if (ActiveBO.HasDataChanges || ActiveBO.IsNewRecord)
                {
                    ISysCommandParam obj1 = new SysCommandParam() { TagObj = false };
                    OnPostDataCommand(obj1);
                    if (!string.IsNullOrEmpty(ActiveBO.ErrorMessage) || ActiveBO.ErrorMessages.Count > 0)
                        return;
                }
                CrsResourceProduce cProduce = new CrsResourceProduce { ResourceBo = ActiveBO };
                SysMng.Instance.ActWndMng.ShowWnd(cProduce, true, SLanguage.GetString("Oda Kartı Üretme"), Common.InformationMessages.WindowStyle.ToolWindow);
            }
            finally
            {
            }
        }
        public bool CanResourceProduceCommand(ISysCommandParam arg)
        {
            return true;
        }
        public override void Dispose()
        {
            if (disposed)
                return;

            Lists?.Dispose();
            if (ActiveBO != null)
            {
                ActiveBO.PropertyChanged -= ActiveBO_PropertyChanged;
                ActiveBO.BeforePost -= ActiveBO_BeforePost;
                ActiveBO.AfterGet -= ActiveBO_AfterGet;
                ActiveBO.AfterSucceededPost -= ActiveBO_AfterSucceededPost;
                ActiveBO.ColumnChanged -= ActiveBO_ColumnChanged;
            }

            if (lcbeLocationBuilding != null)
            {
                lcbeLocationBuilding.LostKeyboardFocus -= LcbeLocationBuilding_LostKeyboardFocus;
            }
            if (lcbeLocationFloor != null)
            {
                lcbeLocationFloor.LostKeyboardFocus -= LcbeLocationFloor_LostKeyboardFocus;
            }
            if (lcbeLocationAisle != null)
            {
                lcbeLocationAisle.LostKeyboardFocus -= LcbeLocationAisle_LostKeyboardFocus;
            }

            base.Dispose();
        }

        bool _suppressEvents = false;
        private void ActiveBO_ColumnChanged(object sender, DataColumnChangeEventArgs e)
        {
            if (_suppressEvents || !ActiveBO.ExtensionsEnabled)
                return;
            try
            {
                if (e.Column.ColumnName == "PaxType1" || e.Column.ColumnName == "PaxType2" || e.Column.ColumnName == "PaxType3" || e.Column.ColumnName == "PaxType99")
                {
                    _suppressEvents = true;
                    decimal paxType1, paxType2, paxType3, paxType99;
                    decimal.TryParse(e.Row["PaxType1"].ToString(), out paxType1);
                    decimal.TryParse(e.Row["PaxType2"].ToString(), out paxType2);
                    decimal.TryParse(e.Row["PaxType3"].ToString(), out paxType3);
                    decimal.TryParse(e.Row["PaxType99"].ToString(), out paxType99);
                    paxType1 = paxType1 * 2;
                    paxType3 = paxType3 * 2;
                    e.Row["Capacity"] = (paxType1 + paxType2 + paxType3) - paxType99;
                    _suppressEvents = false;
                }
            }
            catch (System.Exception)
            {
                _suppressEvents = false;
            }
        }
    }
}
