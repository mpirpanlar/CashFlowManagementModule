using System;
using System.Text;
using Sentez.Localization;
using Prism.Ioc;
using Sentez.Common.Report;
using Sentez.Common.SystemServices;
using System.Data;
using Sentez.Data.MetaData;
using Sentez.Data.Tools;
using DevExpress.Xpf.Grid;
using LiveCore.Desktop.UI.Controls;
using Sentez.Common.Commands;
using Sentez.Common.Utilities;
using System.Linq;
using Sentez.Data.Query;
using Sentez.Data.BusinessObjects;
using System.Windows;
using DevExpress.Xpf.Docking.Base;
using Sentez.FinanceModule.Reports;
using Sentez.Common;
using System.Windows.Controls;
using System.Windows.Threading;
using DevExpress.DataProcessing.InMemoryDataProcessor;
using DevExpress.DocumentServices.ServiceModel.DataContracts;
using static DevExpress.Mvvm.Native.Either;
using System.Windows.Forms;
using Sentez.Common.PresentationModels;
using Sentez.FinanceModule.Models;
using System.Windows.Shapes;
using Sentez.Core.ParameterClasses;

namespace Sentez.Finance.PresentationModels
{
    public class AgingReportResultsManagementPM : ReportPM
    {
        IBusinessObject CurrentAccountReceiptItemBo;
        bool FirstRange = false, SecondRange = false, ThirdRange = false, FourthRange = false, FifthRange = false, SixthRange = false, SeventhRange = false, EighthRange = false, NinthRange = false, TenthRange = false;
        bool IsIbanNo, IsForexCorrection, IsTaxNo, IsGsmPhone, IsAddressPhone, IsAddressFax, IsAddressInfo;

        private bool isChequeList;
        private ReportBase pPolicy;
        LiveLayoutPanel OperationPanel, AggrementPanel, RiskLimitPanel, CustomerActivityPanel, LiveLayoutPanelBouncedDocument, LiveLayoutPanelSellerNotes, LiveLayoutPanelIntelligenceNotes, LiveLayoutPanelRiskPointTable, LiveLayoutPanelOtherCompanyBalanceTable, ChildCurrentAccountPanel, CurrentAccountContactPanel;
        LiveDocumentPanel ChequeBondPanel;
        public LookupList Lists { get; set; }
        private LiveGridControl OperationGrid, AggrementGrid, RiskLimitGrid, CustomerActivityGrid, ChequeBondGrid, LiveGridControlBouncedDocument, LiveGridControlSellerNotes, LiveGridControlIntelligenceNotes, RiskPointTableGrid, OtherCompanyBalanceTableGrid, ChildCurrentAccountGrid, CurrentAccountContactGrid;

        public LiveDockLayoutManager liveDockLayoutManager { get; set; }
        private DataTable currentAccountAnalysisList;
        public DataTable CurrentAccountAnalysisList
        {
            get { return currentAccountAnalysisList; }
            set { currentAccountAnalysisList = value; this.OnPropertyChanged("CurrentAccountAnalysisList"); }
        }

        private DataTable riskLimitList;
        public DataTable RiskLimitList
        {
            get { return riskLimitList; }
            set { riskLimitList = value; this.OnPropertyChanged("RiskLimitList"); }
        }

        private DataTable chequeBonds;
        public DataTable ChequeBonds
        {
            get { return chequeBonds; }
            set { chequeBonds = value; this.OnPropertyChanged("ChequeBonds"); }
        }

        private DataTable childCurrentAccounts;
        public DataTable ChildCurrentAccounts
        {
            get { return childCurrentAccounts; }
            set { childCurrentAccounts = value; this.OnPropertyChanged("ChildCurrentAccounts"); }
        }

        DataTable currentAccountContacts;
        public DataTable CurrentAccountContacts
        {
            get { return currentAccountContacts; }
            set { currentAccountContacts = value; this.OnPropertyChanged("CurrentAccountContacts"); }
        }

        private DataTable aggrementReceiptTable;
        public DataTable AggrementReceiptTable
        {
            get { return aggrementReceiptTable; }
            set { aggrementReceiptTable = value; OnPropertyChanged("AggrementReceiptTable"); }
        }

        private DataTable _bouncedDocumentList;
        /// <summary>
        /// Karşılıksız evrak bilgileri
        /// </summary>
        public DataTable BouncedDocumentList
        {
            get { return _bouncedDocumentList; }
            set { _bouncedDocumentList = value; OnPropertyChanged("BouncedDocumentList"); }
        }

        private DataTable _sellerNotesList;
        /// <summary>
        /// Pazarlamacı notları
        /// </summary>
        public DataTable SellerNotesList
        {
            get { return _sellerNotesList; }
            set { _sellerNotesList = value; OnPropertyChanged("SellerNotesList"); }
        }

        private DataTable _intelligenceNotesList;
        /// <summary>
        /// İstihbarat notları
        /// </summary>
        public DataTable IntelligenceNotesList
        {
            get { return _intelligenceNotesList; }
            set { _intelligenceNotesList = value; OnPropertyChanged("IntelligenceNotesList"); }
        }

        private DataTable operationList;
        public DataTable OperationList
        {
            get { return operationList; }
            set { operationList = value; OnPropertyChanged("OperationList"); }
        }

        private DataTable customerActivityTable;
        public DataTable CustomerActivityTable
        {
            get { return customerActivityTable; }
            set { customerActivityTable = value; OnPropertyChanged("CustomerActivityTable"); }
        }

        private DataTable _riskPointTable;
        public DataTable RiskPointTable
        {
            get { return _riskPointTable; }
            set { _riskPointTable = value; OnPropertyChanged("RiskPointTable"); }
        }

        private DataTable _otherCompanyBalanceTable;
        public DataTable OtherCompanyBalanceTable
        {
            get { return _otherCompanyBalanceTable; }
            set { _otherCompanyBalanceTable = value; OnPropertyChanged("OtherCompanyBalanceTable"); }
        }

        Visibility _riskPointTableVisibility = Visibility.Collapsed;
        public Visibility RiskPointTableVisibility
        {
            get { return _riskPointTableVisibility; }
            set { _riskPointTableVisibility = value; OnPropertyChanged("RiskPointTableVisibility"); }
        }

        public AgingReportResultsManagementPM(IContainerExtension container_)
            : base(container_)
        {

        }

        public void Setup_PM()
        {
            Lists = ActiveSession.LookupList.GetChild(UtilityFunctions.GetConnection(ActiveSession.dbInfo.DBProvider, ActiveSession.dbInfo.ConnectionString));
            Lists.AddLookupList("RiskType", "Display", typeof(string), new object[] { SLanguage.GetString("Bakiye"), SLanguage.GetString("Bakiye + İrsaliye"), SLanguage.GetString("Bakiye + İrsaliye + Sipariş"), SLanguage.GetString("Bakiye + İrsaliye + Sipariş (Onay Bekleyenler Dahil)") }, "Value", typeof(byte), new object[] { (byte)1, (byte)2, (byte)3, (byte)4 });
            Lists.AddLookupList("RiskOverType", "Display", typeof(string), new object[] { SLanguage.GetString("Devam Edilecek"), SLanguage.GetString("Uyarı Verilecek"), SLanguage.GetString("İşleme İzin Verilmeyecek") }, "Value", typeof(short), new object[] { (short)1, (short)2, (short)3 });
            Lists.AddLookupList("AddressType", "Display", typeof(string), new object[] { SLanguage.GetString("Ev Adresi"), SLanguage.GetString("İş Adresi") }, "Value", typeof(byte), new object[] { (byte)1, (byte)2 });
            Lists.AddLookupList(new LookupListParam("ForexPrm", "Meta_ForexPrm", new[] { "RateCode", "RateName" }, "RecId", new[] { WhereField.GetIsDeletedRule("Meta_ForexPrm") }, EmptyLookupType.First) { FirstEmptyDisplayValue = SLanguage.GetString("Parametre") });
            Lists.AddLookupList("VisitPeriodTypeList", "Display", typeof(string), new object[] { SLanguage.GetString("Aralık Yok"), SLanguage.GetString("Gün"), SLanguage.GetString("Hafta"), SLanguage.GetString("Ay"), SLanguage.GetString("Yıl") }, "Value", typeof(byte), new object[] { (byte)0, (byte)1, (byte)2, (byte)3, (byte)4 });
            Lists.AddLookupList("CurrentAccountTransactionType", "Display", typeof(string), new object[] { SLanguage.GetString("Risk Limit"), SLanguage.GetString("Karşılıksız Evrak"), SLanguage.GetString("Kredi Bilgileri"), SLanguage.GetString("İşlem Tanımlama"), SLanguage.GetString("Pazarlamacı Notları"), SLanguage.GetString("Mutabakat") }, "Value", typeof(short), new object[] { (short)1, (short)2, (short)3, (short)4, (short)5, (short)6 }); // 1- de risk tarihçesi var
            //Lists.AddLookupList("DetailPaymentTypeList", "Display", typeof(string), new object[] { SLanguage.GetString("Nakit"), SLanguage.GetString("Havale"), SLanguage.GetString("Kredi Kartı") }, "Value", typeof(byte), new object[] { (byte)1, (byte)2, (byte)3 });

            Lists.AddLookupList("DocumentTypeList2",
                "Display", typeof(string), new object[] {
                SLanguage.GetString("Cari Hesap Fişi"),
                SLanguage.GetString("Banka Fişi"),
                SLanguage.GetString("Çek/Senet")},

                "Value", typeof(short), new object[] { (short)0, (short)1, (short)2 });

            RiskPointTableVisibility = Visibility.Collapsed;
            if (sysMng.HasApplication("RbKaresiModule"))
                RiskPointTableVisibility = Visibility.Visible;
        }

        public override void LoadCommands()
        {
            base.LoadCommands();
            CmdList.AddCmd(300, "RefreshList", SLanguage.GetString("Yenile"), OnRefreshPaymentCalculation, null);
            CmdList.AddCmd(301, "ChangeStatus", SLanguage.GetString("Durum Değiştir"), OnChangeStatus, null);
            CmdList.AddCmd(302, "OpenScreenCommand", SLanguage.GetString("Ekran Aç"), OnOpenScreenCommand, null);
            CmdList.AddCmd(303, "PrintForm", SLanguage.GetString("Form Basımı"), OnPrintForm, null);
            CmdList.AddCmd(304, "CurrentAccountServiceCmd", SLanguage.GetString("Cari Hesap Servisi"), OnCurrentAccountServiceCommand, null);

            CmdList.AddCmd(304, "SaveOperationCmd", SLanguage.GetString("Cari Hesap Servisi"), OnSaveOperationCmd, null);
            CmdList.AddCmd(304, "DeleteOperationCmd", SLanguage.GetString("Cari Hesap Servisi"), OnDeleteOperationCmd, null);
        }

        private void OnDeleteOperationCmd(ISysCommandParam param)
        {

        }

        private void OnSaveOperationCmd(ISysCommandParam param)
        {
            if (CurrentAccountReceiptItemBo?.CurrentRow != null)
            {
                byte currentAccountReceiptIntegration = CurrentAccountReceiptItemBo.ActiveSession.ParamService.GetParameterClass<GLParameters>().CurrentAccountReceiptIntegration;
                CurrentAccountReceiptItemBo.ActiveSession.ParamService.GetParameterClass<GLParameters>().CurrentAccountReceiptIntegration = 0;
                try
                {
                    PostResult postResult = CurrentAccountReceiptItemBo.PostData();
                    if (postResult == PostResult.Succeed)
                    {

                    }
                }
                finally
                {
                    CurrentAccountReceiptItemBo.ActiveSession.ParamService.GetParameterClass<GLParameters>().CurrentAccountReceiptIntegration = currentAccountReceiptIntegration;
                }
            }
        }

        public override void Init()
        {
            CurrentAccountReceiptItemBo = container.Resolve<CurrentAccountReceiptItemBO>();
            PmName = "AgingReportResultsManagementPM";
            pPolicy = _container.Resolve<IReport>("CurrentAccountDebitDistributionList") as ReportBase;
            if (pPolicy != null && pPolicy.PolicyParam == null)
                pPolicy.PolicyParam = new PolicyParams();
            pPolicy.PolicyParam.FieldName = "Aging";
            pPolicy.WorkMode = ReportWorkMode.WorkList;

            Get_WindowSetting();

            AddPolicy(pPolicy);
            ActivePolicy = pPolicy;
            pPolicy.Init();


            foreach (var fitem in pPolicy.statementList[0].filterList.Where(fitem => fitem.field1Name == "CurrentAccountCode" && fitem.filterTable1Name == "Erp_CurrentAccount"))
            {
                //string fVal = "1", lVal = "1";
                string fVal = "32002010024 01", lVal = "32002010024 01";
                try
                {
                    //fVal = SysMng.Instance.getSession().WindowSettings.GetValue(0, "AgingReportResultsManagementPM", "FilterOptions", "FCurrentAccountCode");
                    //if (string.IsNullOrEmpty(fVal))
                    //{
                    //    fVal = "1";
                    //}
                    fitem.valueList[0] = fVal;
                    //lVal = SysMng.Instance.getSession().WindowSettings.GetValue(0, "AgingReportResultsManagementPM", "FilterOptions", "LCurrentAccountCode");
                    //if (string.IsNullOrEmpty(lVal))
                    //{
                    //    lVal = "1";
                    //}
                    fitem.valueList[1] = lVal;
                }
                catch
                {
                    //fitem.valueList[0] = "1"; fitem.valueList[1] = "1";
                    fitem.valueList[0] = "32002010024 01"; fitem.valueList[1] = "32002010024 01";
                }
            }
            /*
            foreach (var fitem in pPolicy.statementList[0].filterList.Where(fitem => fitem.field1Name == "GroupCode" && fitem.filterTable1Name == "Erp_TradingGroup"))
            {
                string fVal = "", lVal = "";
                try
                {
                    fVal = SysMng.Instance.getSession().WindowSettings.GetValue(0, "AgingReportResultsManagementPM", "FilterOptions", "FTradingGroupGroupCode");
                    fitem.valueList[0] = fVal;
                    lVal = SysMng.Instance.getSession().WindowSettings.GetValue(0, "AgingReportResultsManagementPM", "FilterOptions", "LTradingGroupGroupCode");
                    fitem.valueList[1] = lVal;
                }
                catch { }
            }
            */

            base.Init();
            Setup_PM();
            dbGrid = FCtrl("dbGrid") as LiveGridControl;
            OperationGrid = FCtrl("OperationGrid") as LiveGridControl;
            AggrementGrid = FCtrl("AggrementGrid") as LiveGridControl;
            RiskLimitGrid = FCtrl("RiskLimitGrid") as LiveGridControl;
            LiveGridControlBouncedDocument = FCtrl("LiveGridControlBouncedDocument") as LiveGridControl;
            LiveGridControlSellerNotes = FCtrl("LiveGridControlSellerNotes") as LiveGridControl;
            LiveGridControlIntelligenceNotes = FCtrl("LiveGridControlIntelligenceNotes") as LiveGridControl;
            RiskPointTableGrid = FCtrl("RiskPointTableGrid") as LiveGridControl;
            OtherCompanyBalanceTableGrid = FCtrl("OtherCompanyBalanceTableGrid") as LiveGridControl;

            CustomerActivityGrid = FCtrl("CustomerActivityGrid") as LiveGridControl;
            ChequeBondGrid = FCtrl("ChequeBondGrid") as LiveGridControl;
            ChildCurrentAccountGrid = FCtrl("ChildCurrentAccountGrid") as LiveGridControl;
            CurrentAccountContactGrid = FCtrl("CurrentAccountContactGrid") as LiveGridControl;

            if (dbGrid != null)
            {
                dbGrid.CurrentItemChanged += dbGrid_CurrentItemChanged;
                dbGrid.ItemsSourceChanged += dbGrid_ItemsSourceChanged;
            }
            if (OperationGrid != null)
            {
                OperationGrid.CreatedNewRow += OperationGrid_CreatedNewRow;
            }
            OperationPanel = FCtrl("OperationPanel") as LiveLayoutPanel;
            AggrementPanel = FCtrl("AggrementPanel") as LiveLayoutPanel;
            RiskLimitPanel = FCtrl("RiskLimitPanel") as LiveLayoutPanel;
            CustomerActivityPanel = FCtrl("CustomerActivityPanel") as LiveLayoutPanel;
            LiveLayoutPanelBouncedDocument = FCtrl("LiveLayoutPanelBouncedDocument") as LiveLayoutPanel;
            LiveLayoutPanelSellerNotes = FCtrl("LiveLayoutPanelSellerNotes") as LiveLayoutPanel;
            LiveLayoutPanelIntelligenceNotes = FCtrl("LiveLayoutPanelIntelligenceNotes") as LiveLayoutPanel;
            LiveLayoutPanelRiskPointTable = FCtrl("LiveLayoutPanelRiskPointTable") as LiveLayoutPanel;
            LiveLayoutPanelOtherCompanyBalanceTable = FCtrl("LiveLayoutPanelOtherCompanyBalanceTable") as LiveLayoutPanel;

            ChequeBondPanel = FCtrl("ChequeBondPanel") as LiveDocumentPanel;
            ChildCurrentAccountPanel = FCtrl("ChildCurrentAccountPanel") as LiveLayoutPanel;
            CurrentAccountContactPanel = FCtrl("CurrentAccountContactPanel") as LiveLayoutPanel;

            if (OperationPanel != null) OperationPanel.IsVisibleChanged += LayoutPanel_IsVisibleChanged;
            if (AggrementPanel != null) AggrementPanel.IsVisibleChanged += LayoutPanel_IsVisibleChanged;
            if (RiskLimitPanel != null) RiskLimitPanel.IsVisibleChanged += LayoutPanel_IsVisibleChanged;
            if (CustomerActivityPanel != null) CustomerActivityPanel.IsVisibleChanged += LayoutPanel_IsVisibleChanged;
            if (LiveLayoutPanelBouncedDocument != null) LiveLayoutPanelBouncedDocument.IsVisibleChanged += LayoutPanel_IsVisibleChanged;
            if (LiveLayoutPanelSellerNotes != null) LiveLayoutPanelSellerNotes.IsVisibleChanged += LayoutPanel_IsVisibleChanged;
            if (LiveLayoutPanelIntelligenceNotes != null) LiveLayoutPanelIntelligenceNotes.IsVisibleChanged += LayoutPanel_IsVisibleChanged;
            if (ChequeBondPanel != null) ChequeBondPanel.IsVisibleChanged += LayoutPanel_IsVisibleChanged;
            if (ChildCurrentAccountPanel != null) ChildCurrentAccountPanel.IsVisibleChanged += LayoutPanel_IsVisibleChanged;
            if (CurrentAccountContactPanel != null) CurrentAccountContactPanel.IsVisibleChanged += LayoutPanel_IsVisibleChanged;

            if (LiveLayoutPanelRiskPointTable != null) LiveLayoutPanelRiskPointTable.IsVisibleChanged += LayoutPanel_IsVisibleChanged;
            if (LiveLayoutPanelOtherCompanyBalanceTable != null) LiveLayoutPanelOtherCompanyBalanceTable.IsVisibleChanged += LayoutPanel_IsVisibleChanged;

            liveDockLayoutManager = FCtrl("WorklistDockLayoutManager") as LiveDockLayoutManager;
            if (CurrentAccountReceiptItemBo != null)
            {
                CurrentAccountReceiptItemBo.TableNewRow += CurrentAccountReceiptItemBo_TableNewRow;
                (CurrentAccountReceiptItemBo as BusinessObjectBase).ValueFiller.AddRule("Erp_CurrentAccountReceiptItem", "ReceiptType", 56);
                (CurrentAccountReceiptItemBo as BusinessObjectBase).ValueFiller.AddRule("Erp_CurrentAccountReceiptItem", "DocumentType", 0);
                //(CurrentAccountReceiptItemBo as BusinessObjectBase).ValueFiller.AddRule("Erp_CurrentAccountReceiptItem", "CurrentAccountId", GetRefId("CurrentAccountId"));
            }
        }

        private void CurrentAccountReceiptItemBo_TableNewRow(object sender, DataTableNewRowEventArgs e)
        {
            if (e.Row != null)
            {
                e.Row["CurrentAccountId"] = GetRefId("CurrentAccountId");
            }
        }

        private void OperationGrid_CreatedNewRow(object sender, NewRowViewEventArgs e)
        {
            if (e.View is DataRowView)
            {
                e.View.Row["ReceiptType"] = 56;
                e.View.Row["CurrentAccountId"] = GetRefId("CurrentAccountId");
            }
        }

        private void Get_WindowSetting()
        {
            try
            {
                bool isIbanNo = Convert.ToBoolean(SysMng.Instance.getSession().WindowSettings.GetValue(0, "AgingReportResultsManagementPM", "FilterOptions", "IsIbanNo"));
                RepOps ops = pPolicy.FindOption("IsIbanNo");
                if (ops != null)
                {
                    ops.IsChecked = isIbanNo;
                }
            }
            catch { }

            try
            {
                bool isTaxNo = Convert.ToBoolean(SysMng.Instance.getSession().WindowSettings.GetValue(0, "AgingReportResultsManagementPM", "FilterOptions", "IsTaxNo"));
                RepOps ops = pPolicy.FindOption("IsTaxNo");
                if (ops != null)
                {
                    ops.IsChecked = isTaxNo;
                }
            }
            catch { }

            try
            {
                bool isForexCorrection = Convert.ToBoolean(SysMng.Instance.getSession().WindowSettings.GetValue(0, "AgingReportResultsManagementPM", "FilterOptions", "IsForexCorrection"));
                RepOps ops = pPolicy.FindOption("IsForexCorrection");
                if (ops != null)
                {
                    ops.IsChecked = isForexCorrection;
                }
            }
            catch { }

            try
            {
                string str = SysMng.Instance.getSession().WindowSettings.GetValue(0, "AgingReportResultsManagementPM", "FilterOptions", "ForexOptions");
                RepOps ops = pPolicy.FindOption("OptionsGroup3");
                if (ops != null)
                {
                    ops.selindex = Convert.ToInt32(str);
                    int ind = 0;
                    foreach (ObjPair child_ops in ops.Items)
                    {
                        if (ind == ops.selindex)
                        {
                            ops.selectedItem = child_ops;
                            break;
                        }
                        ind++;
                    }
                }
            }
            catch { }

            //try
            //{
            //    string str = SysMng.Instance.getSession().WindowSettings.GetValue(0, "AgingReportResultsManagementPM", "FilterOptions", "ReportDate");
            //    RepOps ops = pPolicy.FindOption("OptionsGroup2");
            //    if (ops != null)
            //    {
            //        ops.selectedItem = Convert.ToDateTime(str);
            //    }
            //}
            //catch { }

            try
            {
                bool IsGsmPhone = Convert.ToBoolean(SysMng.Instance.getSession().WindowSettings.GetValue(0, "AgingReportResultsManagementPM", "FilterOptions", "IsGsmPhone"));
                RepOps ops = pPolicy.FindOption("IsGsmPhone");
                if (ops != null)
                {
                    ops.IsChecked = IsGsmPhone;
                }
            }
            catch { }

            try
            {
                bool IsAddressPhone = Convert.ToBoolean(SysMng.Instance.getSession().WindowSettings.GetValue(0, "AgingReportResultsManagementPM", "FilterOptions", "IsAddressPhone"));
                RepOps ops = pPolicy.FindOption("IsAddressPhone");
                if (ops != null)
                {
                    ops.IsChecked = IsAddressPhone;
                }
            }
            catch { }

            try
            {
                bool IsAddressFax = Convert.ToBoolean(SysMng.Instance.getSession().WindowSettings.GetValue(0, "AgingReportResultsManagementPM", "FilterOptions", "IsAddressFax"));
                RepOps ops = pPolicy.FindOption("IsAddressFax");
                if (ops != null)
                {
                    ops.IsChecked = IsAddressFax;
                }
            }
            catch { }

            try
            {
                bool IsAddressInfo = Convert.ToBoolean(SysMng.Instance.getSession().WindowSettings.GetValue(0, "AgingReportResultsManagementPM", "FilterOptions", "IsAddressInfo"));
                RepOps ops = pPolicy.FindOption("IsAddressInfo");
                if (ops != null)
                {
                    ops.IsChecked = IsAddressInfo;
                }
            }
            catch { }

            try
            {
                FirstRange = Convert.ToBoolean(SysMng.Instance.getSession().WindowSettings.GetValue(0, "AgingReportResultsManagementPM", "FilterOptions", "FirstRange"));
                RepOps ops = pPolicy.FindOption("FirstRange");
                if (ops != null)
                {
                    ops.IsChecked = FirstRange;
                }
            }
            catch { }

            try
            {
                SecondRange = Convert.ToBoolean(SysMng.Instance.getSession().WindowSettings.GetValue(0, "AgingReportResultsManagementPM", "FilterOptions", "SecondRange"));
                RepOps ops = pPolicy.FindOption("SecondRange");
                if (ops != null)
                {
                    ops.IsChecked = SecondRange;
                }
            }
            catch { }

            try
            {
                ThirdRange = Convert.ToBoolean(SysMng.Instance.getSession().WindowSettings.GetValue(0, "AgingReportResultsManagementPM", "FilterOptions", "ThirdRange"));
                RepOps ops = pPolicy.FindOption("ThirdRange");
                if (ops != null)
                {
                    ops.IsChecked = ThirdRange;
                }
            }
            catch { }

            try
            {
                FourthRange = Convert.ToBoolean(SysMng.Instance.getSession().WindowSettings.GetValue(0, "AgingReportResultsManagementPM", "FilterOptions", "FourthRange"));
                RepOps ops = pPolicy.FindOption("FourthRange");
                if (ops != null)
                {
                    ops.IsChecked = FourthRange;
                }
            }
            catch { }

            try
            {
                FifthRange = Convert.ToBoolean(SysMng.Instance.getSession().WindowSettings.GetValue(0, "AgingReportResultsManagementPM", "FilterOptions", "FifthRange"));
                RepOps ops = pPolicy.FindOption("FifthRange");
                if (ops != null)
                {
                    ops.IsChecked = FifthRange;
                }
            }
            catch { }

            try
            {
                SixthRange = Convert.ToBoolean(SysMng.Instance.getSession().WindowSettings.GetValue(0, "AgingReportResultsManagementPM", "FilterOptions", "SixthRange"));
                RepOps ops = pPolicy.FindOption("SixthRange");
                if (ops != null)
                {
                    ops.IsChecked = SixthRange;
                }
            }
            catch { }

            try
            {
                SeventhRange = Convert.ToBoolean(SysMng.Instance.getSession().WindowSettings.GetValue(0, "AgingReportResultsManagementPM", "FilterOptions", "SeventhRange"));
                RepOps ops = pPolicy.FindOption("SeventhRange");
                if (ops != null)
                {
                    ops.IsChecked = SeventhRange;
                }
            }
            catch { }

            try
            {
                EighthRange = Convert.ToBoolean(SysMng.Instance.getSession().WindowSettings.GetValue(0, "AgingReportResultsManagementPM", "FilterOptions", "EighthRange"));
                RepOps ops = pPolicy.FindOption("EighthRange");
                if (ops != null)
                {
                    ops.IsChecked = EighthRange;
                }
            }
            catch { }

            try
            {
                NinthRange = Convert.ToBoolean(SysMng.Instance.getSession().WindowSettings.GetValue(0, "AgingReportResultsManagementPM", "FilterOptions", "NinthRange"));
                RepOps ops = pPolicy.FindOption("NinthRange");
                if (ops != null)
                {
                    ops.IsChecked = NinthRange;
                }
            }
            catch { }

            try
            {
                TenthRange = Convert.ToBoolean(SysMng.Instance.getSession().WindowSettings.GetValue(0, "AgingReportResultsManagementPM", "FilterOptions", "TenthRange"));
                RepOps ops = pPolicy.FindOption("TenthRange");
                if (ops != null)
                {
                    ops.IsChecked = TenthRange;
                }
            }
            catch { }
        }

        private void dbGrid_ItemsSourceChanged(object sender, ItemsSourceChangedEventArgs e)
        {
            if (e.NewItemsSource != null)
            {
                LiveGridControl gridControl = sender as LiveGridControl;
                TableView tableView = (gridControl.View as TableView);
                if (tableView != null)
                {
                    dbGrid.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        tableView.BestFitColumns();
                    }), DispatcherPriority.Render);
                }
                Init_ColumnVisible();
            }
        }

        private void LayoutPanel_IsVisibleChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
        {
            var sndr = sender as LiveLayoutPanel;
            if (sndr != null && e.NewValue != null && (bool)e.NewValue)
            {
                switch (sndr.Name)
                {
                    case "OperationPanel":
                        ShowOperationList();
                        break;
                    case "AggrementPanel":
                        ShowAggrementList();
                        break;
                    case "RiskLimitPanel":
                        ShowRiskLimitList();
                        break;
                    case "LiveLayoutPanelBouncedDocument":
                        ShowBouncedDocumentList();
                        break;
                    case "LiveLayoutPanelSellerNotes":
                        ShowSellerNotesList();
                        break;
                    case "LiveLayoutPanelIntelligenceNotes":
                        ShowIntelligenceNotesList();
                        break;
                    case "CustomerActivityPanel":
                        ShowCustomerActivityList();
                        break;
                    case "LiveLayoutPanelRiskPointTable":
                        ShowRiskPointList();
                        break;
                    case "LiveLayoutPanelOtherCompanyBalanceTable":
                        ShowOtherCompanyBalance();
                        break;
                }
            }
            else
            {
                var sndrd = sender as LiveDocumentPanel;
                if (sndrd != null && e.NewValue != null && (bool)e.NewValue)
                {
                    if (sndrd.Name == "ChequeBondPanel")
                    {
                        if (!isChequeList) ShowChequeList();
                        isChequeList = true;
                    }
                }
            }
        }
        bool firstRun = true;
        private void OnRefreshPaymentCalculation(ISysCommandParam obj)
        {
            firstRun = false;
            OnRun(obj);
        }

        public override void OnRun(ISysCommandParam obj)
        {
            if (!firstRun)
            {
                foreach (var fitem in pPolicy.statementList[0].filterList.Where(fitem => fitem.field1Name == "CurrentAccountCode" && fitem.filterTable1Name == "Erp_CurrentAccount"))
                {
                    try
                    {
                        SysMng.Instance.getSession().WindowSettings.SetValue(0, "AgingReportResultsManagementPM", "FilterOptions", "FCurrentAccountCode", fitem.valueList[0].ToString());
                        SysMng.Instance.getSession().WindowSettings.SetValue(0, "AgingReportResultsManagementPM", "FilterOptions", "LCurrentAccountCode", fitem.valueList[1].ToString());
                    }
                    catch { }
                }

                foreach (var fitem in pPolicy.statementList[0].filterList.Where(fitem => fitem.field1Name == "GroupCode" && fitem.filterTable1Name == "Erp_TradingGroup"))
                {
                    try
                    {
                        SysMng.Instance.getSession().WindowSettings.SetValue(0, "AgingReportResultsManagementPM", "FilterOptions", "FTradingGroupGroupCode", fitem.valueList[0].ToString());
                        SysMng.Instance.getSession().WindowSettings.SetValue(0, "AgingReportResultsManagementPM", "FilterOptions", "LTradingGroupGroupCode", fitem.valueList[1].ToString());
                    }
                    catch { }
                }
                pPolicy.Init();
                foreach (var fitem in pPolicy.statementList[0].filterList.Where(fitem => fitem.field1Name == "CurrentAccountCode" && fitem.filterTable1Name == "Erp_CurrentAccount"))
                {
                    string fVal = "", lVal = "";
                    try
                    {
                        fVal = SysMng.Instance.getSession().WindowSettings.GetValue(0, "AgingReportResultsManagementPM", "FilterOptions", "FCurrentAccountCode");
                        fitem.valueList[0] = fVal;


                        lVal = SysMng.Instance.getSession().WindowSettings.GetValue(0, "AgingReportResultsManagementPM", "FilterOptions", "LCurrentAccountCode");
                        fitem.valueList[1] = lVal;
                    }
                    catch
                    {
                        //fitem.valueList[0] = "1"; fitem.valueList[1] = "1";
                        fitem.valueList[0] = "32002010024 01"; fitem.valueList[1] = "32002010024 01";
                    }
                }

                foreach (var fitem in pPolicy.statementList[0].filterList.Where(fitem => fitem.field1Name == "GroupCode" && fitem.filterTable1Name == "Erp_TradingGroup"))
                {
                    string fVal = "", lVal = "";
                    try
                    {
                        fVal = SysMng.Instance.getSession().WindowSettings.GetValue(0, "AgingReportResultsManagementPM", "FilterOptions", "FTradingGroupGroupCode");
                        fitem.valueList[0] = fVal;
                        lVal = SysMng.Instance.getSession().WindowSettings.GetValue(0, "AgingReportResultsManagementPM", "FilterOptions", "LTradingGroupGroupCode");
                        fitem.valueList[1] = lVal;
                    }
                    catch { }
                }
            }
            base.OnRun(obj);
            PrepGridColumns();
            dbGrid.GenerateColumns();

            ShowOperationList();
            //ShowAggrementList();
            //ShowRiskLimitList();
            //ShowBouncedDocumentList();
            //ShowSellerNotesList();
            ShowIntelligenceNotesList();
            //ShowRiskPointList();
            //ShowOtherCompanyBalance();
            //isChequeList = false;
            ShowChildCurrentAccountList();
            ShowCurrentAccountContactList();
            //Init_ColumnVisible();
        }

        private void Init_ColumnVisible()
        {

            /*
            iTermInterestStatusOptions.IsIbanNo = IsOpsChecked("IsIbanNo");
            iTermInterestStatusOptions.IsForexCorrection = IsOpsChecked("IsForexCorrection");
            iTermInterestStatusOptions.IsTaxNo = IsOpsChecked("IsTaxNo");
            iTermInterestStatusOptions.IsGsmPhone = IsOpsChecked("IsGsmPhone");
            iTermInterestStatusOptions.IsAddressPhone = IsOpsChecked("IsAddressPhone");
            iTermInterestStatusOptions.IsAddressFax = IsOpsChecked("IsAddressFax");
            iTermInterestStatusOptions.IsAddressInfo = IsOpsChecked("IsAddressInfo");

             
             
            if (iTermInterestStatusOptions.IsIbanNo)
            {
                _statement1.AddCol(SLanguage.GetString("Iban No"), "temporary", SLanguage.GetString("Iban No"));
                _statement1.AddCol(SLanguage.GetString("Hesap Adı"), "temporary", SLanguage.GetString("Müşteri Hesap Adı"));
                _statement1.AddCol(SLanguage.GetString("Açıklama"), "temporary", SLanguage.GetString("Açıklama"));
            }
            if (iTermInterestStatusOptions.IsTaxNo)
            {
                _statement1.AddCol(SLanguage.GetString("Vergi Kimlik No"), "temporary", SLanguage.GetString("Vergi Kimlik No"));
                _statement1.AddCol(SLanguage.GetString("T.C. Kimlik No"), "temporary", SLanguage.GetString("T.C. Kimlik No"));
            }
             */

            #region Cep Telefonu
            try
            {
                RepOps ops = pPolicy.FindOption("IsGsmPhone");
                if (ops != null)
                {
                    if (!ops.IsChecked.Value)
                    {
                        foreach (GridColumn c in dbGrid.Columns)
                        {
                            if (c.Tag is ReceiptColumn && (c.Tag as ReceiptColumn).ColumnName == SLanguage.GetString("Cep Telefonu"))
                            {
                                c.Visible = false;
                            }
                        }
                    }
                }
            }
            catch { }
            #endregion

            #region Adres Telefon
            try
            {
                RepOps ops = pPolicy.FindOption("IsAddressPhone");
                if (ops != null)
                {
                    if (!ops.IsChecked.Value)
                    {
                        foreach (GridColumn c in dbGrid.Columns)
                        {
                            if (c.Tag is ReceiptColumn && (c.Tag as ReceiptColumn).ColumnName == SLanguage.GetString("Adres Telefon"))
                            {
                                c.Visible = false;
                            }
                        }
                    }
                }
            }
            catch { }
            #endregion

            #region Adres Faks
            try
            {
                RepOps ops = pPolicy.FindOption("IsAddressFax");
                if (ops != null)
                {
                    if (!ops.IsChecked.Value)
                    {
                        foreach (GridColumn c in dbGrid.Columns)
                        {
                            if (c.Tag is ReceiptColumn && (c.Tag as ReceiptColumn).ColumnName == SLanguage.GetString("Adres Faks"))
                            {
                                c.Visible = false;
                            }
                        }
                    }
                }
            }
            catch { }
            #endregion

            #region Adres Bilgisi
            try
            {
                RepOps ops = pPolicy.FindOption("IsAddressInfo");
                if (ops != null)
                {
                    if (!ops.IsChecked.Value)
                    {
                        foreach (GridColumn c in dbGrid.Columns)
                        {
                            if (c.Tag is ReceiptColumn && (c.Tag as ReceiptColumn).ColumnName == SLanguage.GetString("Adres Bilgisi"))
                            {
                                c.Visible = false;
                            }
                        }
                    }
                }
            }
            catch { }
            #endregion

            #region IbanNo Bilgisi
            try
            {
                RepOps ops = pPolicy.FindOption("IsIbanNo");
                if (ops != null)
                {
                    if (!ops.IsChecked.Value)
                    {
                        foreach (GridColumn c in dbGrid.Columns)
                        {
                            if (c.Tag is ReceiptColumn && (c.Tag as ReceiptColumn).ColumnName == SLanguage.GetString("Iban No"))
                            {
                                c.Visible = false;
                            }
                            else if (c.Tag is ReceiptColumn && (c.Tag as ReceiptColumn).ColumnName == SLanguage.GetString("Müşteri Hesap Adı"))
                            {
                                c.Visible = false;
                            }
                            else if (c.Tag is ReceiptColumn && (c.Tag as ReceiptColumn).ColumnName == SLanguage.GetString("Açıklama"))
                            {
                                c.Visible = false;
                            }
                        }
                    }
                }
            }
            catch { }
            #endregion

            #region IsTaxNo (Kimlik) Bilgisi
            try
            {
                RepOps ops = pPolicy.FindOption("IsTaxNo");
                if (ops != null)
                {
                    if (!ops.IsChecked.Value)
                    {
                        foreach (GridColumn c in dbGrid.Columns)
                        {
                            if (c.Tag is ReceiptColumn && (c.Tag as ReceiptColumn).ColumnName == SLanguage.GetString("Vergi Kimlik No"))
                            {
                                c.Visible = false;
                            }
                            else if (c.Tag is ReceiptColumn && (c.Tag as ReceiptColumn).ColumnName == SLanguage.GetString("T.C. Kimlik No"))
                            {
                                c.Visible = false;
                            }
                        }
                    }
                }
            }
            catch { }
            #endregion

            try
            {
                RepOps ops = pPolicy.FindOption("FirstRange");
                if (ops != null)
                {
                    FirstRange = ops.IsChecked.Value;
                }
            }
            catch { }

            try
            {
                RepOps ops = pPolicy.FindOption("SecondRange");
                if (ops != null)
                {
                    SecondRange = ops.IsChecked.Value;
                }
            }
            catch { }

            try
            {
                RepOps ops = pPolicy.FindOption("ThirdRange");
                if (ops != null)
                {
                    ThirdRange = ops.IsChecked.Value;
                }
            }
            catch { }

            try
            {
                RepOps ops = pPolicy.FindOption("FourthRange");
                if (ops != null)
                {
                    FourthRange = ops.IsChecked.Value;
                }
            }
            catch { }

            try
            {
                RepOps ops = pPolicy.FindOption("FifthRange");
                if (ops != null)
                {
                    FifthRange = ops.IsChecked.Value;
                }
            }
            catch { }

            try
            {
                RepOps ops = pPolicy.FindOption("SixthRange");
                if (ops != null)
                {
                    SixthRange = ops.IsChecked.Value;
                }
            }
            catch { }

            try
            {
                RepOps ops = pPolicy.FindOption("SeventhRange");
                if (ops != null)
                {
                    SeventhRange = ops.IsChecked.Value;
                }
            }
            catch { }

            try
            {
                RepOps ops = pPolicy.FindOption("EighthRange");
                if (ops != null)
                {
                    EighthRange = ops.IsChecked.Value;
                }
            }
            catch { }

            try
            {
                RepOps ops = pPolicy.FindOption("NinthRange");
                if (ops != null)
                {
                    NinthRange = ops.IsChecked.Value;
                }
            }
            catch { }

            try
            {
                RepOps ops = pPolicy.FindOption("TenthRange");
                if (ops != null)
                {
                    TenthRange = ops.IsChecked.Value;
                }
            }
            catch { }

            int inx = 1;
            foreach (GridColumn gridColumn in dbGrid.Columns)
            {
                if ((gridColumn.Tag as ReceiptColumn).ColumnName.Contains("-") || (gridColumn.Tag as ReceiptColumn).ColumnName.Contains("+"))
                {
                    switch (inx)
                    {
                        case 1:
                            gridColumn.Visible = FirstRange;
                            inx++;
                            break;
                        case 2:
                            gridColumn.Visible = SecondRange;
                            inx++;
                            break;
                        case 3:
                            gridColumn.Visible = ThirdRange;
                            inx++;
                            break;
                        case 4:
                            gridColumn.Visible = FourthRange;
                            inx++;
                            break;
                        case 5:
                            gridColumn.Visible = FifthRange;
                            inx++;
                            break;
                        case 6:
                            gridColumn.Visible = SixthRange;
                            inx++;
                            break;
                        case 7:
                            gridColumn.Visible = SeventhRange;
                            inx++;
                            break;
                        case 8:
                            gridColumn.Visible = EighthRange;
                            inx++;
                            break;
                        case 9:
                            gridColumn.Visible = NinthRange;
                            inx++;
                            break;
                        case 10:
                            gridColumn.Visible = TenthRange;
                            inx++;
                            break;
                        default:
                            gridColumn.Visible = false;
                            break;
                    }
                }
            }
        }

        public void OnOpenScreenCommand(ISysCommandParam obj)
        {
            if (obj?.Tag == "Card")
            {
                if (dbGrid.CurrentItem != null)
                {
                    int currAccId;
                    currAccId = Convert.ToInt32((dbGrid.CurrentItem as DataRowView).Row["CurrentAccountId"]);
                    if (currAccId > 0)
                    {
                        ActivePolicy.OpenCmd.MenuItemCommandParam.BoParamObj.ActiveRecordId = currAccId;
                    }
                }
                ActivePolicy.OpenCmd?.MenuItemCommand.Execute(ActivePolicy.OpenCmd.MenuItemCommandParam);
            }
            else if (obj?.Tag == "Extre")
            {
                foreach (MenuItemPM menuItem in from MenuItemPM menuItem in ActivePolicy.RootMenu.Children
                                                where menuItem.Children.Count > 0
                                                select menuItem
                                                )
                {
                    foreach (MenuItemPM menuItemSub in from MenuItemPM menuItemSub in menuItem.Children
                                                       where menuItemSub.MenuItemCommandName == "CurrentAccountExtreDetailCommand"
                                                       select menuItemSub)
                    {
                        menuItemSub.MenuItemCommand.Execute(menuItemSub.MenuItemCommandParam);
                        break;
                    }
                }
            }
            else if (obj?.Tag == "AgreementLetter")
            {
                foreach (MenuItemPM menuItem in from MenuItemPM menuItem in ActivePolicy.RootMenu.Children
                                                where menuItem.Children.Count > 0
                                                select menuItem
                                                )
                {
                    foreach (MenuItemPM menuItemSub in from MenuItemPM menuItemSub in menuItem.Children
                                                       where menuItemSub.Tag == "AgreementLetter"
                                                       select menuItemSub)
                    {
                        if (dbGrid.CurrentItem != null)
                        {
                            int currAccId;
                            currAccId = Convert.ToInt32((dbGrid.CurrentItem as DataRowView).Row["CurrentAccountId"]);
                            if (currAccId > 0)
                            {
                                if (menuItemSub.MenuItemCommandParam.BoParamObj == null)
                                {
                                    menuItemSub.MenuItemCommandParam.BoParamObj = new BoParam();
                                }
                                menuItemSub.MenuItemCommandParam.BoParamObj.ActiveRecordId = currAccId;
                            }
                        }

                        menuItemSub.MenuItemCommand.Execute(menuItemSub.MenuItemCommandParam);
                        break;
                    }
                }
            }
        }

        public void OnPrintForm(ISysCommandParam obj)
        {
            if (dbGrid?.CurrentItem == null) return;
            IBusinessObject businessObject = container.Resolve<IBusinessObject>("CurrentAccountBO");
            try
            {
                if (businessObject?.Get(Convert.ToInt64((dbGrid?.CurrentItem as DataRowView)?.Row["CurrentAccountId"])) > 0)
                {
                    SysCommandParam obj2 = new SysCommandParam();
                    obj2.logicalModuleID = (short)Common.ModuleBase.Modules.FinanceModule;
                    obj2.moduleID = (short)Common.ModuleBase.Modules.FinanceModule;
                    obj2.secID = (short)Common.Security.SecurityHelper.GetSecId("FinanceModule", "FinanceSecurityItems", "CurrentAccount");
                    obj2.subsecID = (short)Common.Security.SecurityHelper.GetSecId("FinanceModule", "CurrentAccountSubItems", "Form");
                    obj2.BoObj = businessObject;
                    obj2.BoName = "CurrentAccountForm";
                    obj2.BoParamObj = new BoParam { ActiveRecordId = Convert.ToInt32((dbGrid?.CurrentItem as DataRowView)?.Row["CurrentAccountId"]) };

                    sysMng.OnFormPrintCmd(obj2);
                }
            }
            finally
            {
                businessObject?.Dispose();
            }
        }

        public void OnCurrentAccountServiceCommand(ISysCommandParam obj)
        {
            if (pPolicy?.Data == null || pPolicy.Data.Tables == null || pPolicy.Data.Tables.Count <= 0)
                return;
            IBusinessObject businessObject = container.Resolve<IBusinessObject>("CurrentAccountBO");
            if (businessObject == null) return;
            try
            {
                if (obj?.Tag == "StopSale")
                {
                    string whereStr = $"[{SLanguage.GetString("Hft-8>")}]>=10000.00";
                    foreach (DataRow row in pPolicy.Data.Tables[0].Select(whereStr, "", DataViewRowState.CurrentRows))
                    {
                        long currAccId;
                        long.TryParse(row["RecId"].ToString(), out currAccId);
                        if (businessObject.Get(currAccId) > 0)
                        {
                            DataRow[] transactionRows = businessObject.Data.Tables["Erp_CurrentAccountTransaction"].Select($"TransactionType=4 and TransactionActionKind=3 and TransactionActionType=5 and IsNull(IsManualStopSale,0)=0", "TransactionDate desc", DataViewRowState.CurrentRows);
                            if (transactionRows?.Length == 0)
                            {
                                DataRow newRow = businessObject.Data.Tables["Erp_CurrentAccountTransaction"].NewRow();
                                businessObject.Data.Tables["Erp_CurrentAccountTransaction"].Rows.Add(newRow);
                                newRow.SetParentRow(businessObject.CurrentRow.Row);
                                newRow["TransactionType"] = 4;
                                newRow["TransactionActionKind"] = 3;
                                newRow["TransactionActionType"] = 5;
                                newRow["IsManualStopSale"] = 0;
                                newRow["CreatedBySystem"] = 1;
                                PostResult postResult = businessObject.PostData();
                                if (postResult == PostResult.Succeed)
                                {

                                }
                            }
                        }
                    }
                }
                else if (obj?.Tag == "StartSale")
                {
                    //string whereStr = $"[{SLanguage.GetString("Hft-8>")}]>=10000.00";
                    string whereStr = "";
                    foreach (DataRow row in pPolicy.Data.Tables[0].Select(whereStr, "", DataViewRowState.CurrentRows))
                    {
                        long currAccId;
                        long.TryParse(row["RecId"].ToString(), out currAccId);
                        decimal hft8;
                        decimal.TryParse(row[SLanguage.GetString("Hft-8>")].ToString(), out hft8);
                        if (DoubleUtil.CompareFloat(">=", 10000, hft8, ActiveSession.ParamService.GetParameterClass<Core.ParameterClasses.GeneralParameters>().AmountDec))
                        {
                            if (businessObject.Get(currAccId) > 0)
                            {
                                DataRow[] transactionRows = businessObject.Data.Tables["Erp_CurrentAccountTransaction"].Select($"TransactionType=4 and TransactionActionKind=3 and TransactionActionType=5 and IsNull(IsManualStopSale,0)=0", "TransactionDate desc", DataViewRowState.CurrentRows);
                                if (transactionRows?.Length > 0)
                                {
                                    transactionRows[0]["TransactionActionType"] = 1;
                                    PostResult postResult = businessObject.PostData();
                                    if (postResult == PostResult.Succeed)
                                    {

                                    }
                                }
                            }
                        }
                    }
                }
            }
            finally
            {
                businessObject?.Dispose();
            }
        }

        public void OnChangeStatus(ISysCommandParam obj)
        {
            if (obj == null || string.IsNullOrEmpty(obj.Tag) || string.IsNullOrEmpty(obj.itemCode))
                return;

            try
            {
                string idStr = string.Empty;
                foreach (var item in dbGrid?.SelectedItems)
                {
                    if (string.IsNullOrEmpty(idStr))
                        idStr = ((DataRowView)item).Row["RecId"].ToString();
                    else
                        idStr += "," + ((DataRowView)item).Row["RecId"].ToString();
                }
                UtilityFunctions.SqlCustomNonQuery(ActiveSession._dbInfo.Connection, null, $"update Erp_CurrentAccount set {obj.itemCode}={obj.Tag} where RecId in({idStr})");
            }
            finally
            {
                OnRun(null);
            }
        }

        public void ShowChequeList()
        {
            if (ChequeBondGrid == null) return;
            if (ChequeBondPanel == null || !ChequeBondPanel.IsVisible) return;

            if (ChequeBonds == null)
            {
                ChequeBonds = new DataTable();
                ChequeBonds.Columns.Add(new DataColumn("Cari Hesap Kodu") { DataType = UdtTypes.GetUdtSystemType(Schema.Tables["Erp_CurrentAccount"].Fields["CurrentAccountCode"].UdtType) });
                ChequeBonds.Columns.Add(new DataColumn("Cari Hesap Adı") { DataType = UdtTypes.GetUdtSystemType(Schema.Tables["Erp_CurrentAccount"].Fields["CurrentAccountName"].UdtType) });
                ChequeBonds.Columns.Add(new DataColumn("Borçlu") { DataType = UdtTypes.GetUdtSystemType(Schema.Tables["Erp_CurrentAccount"].Fields["CurrentAccountName"].UdtType) });
                ChequeBonds.Columns.Add(new DataColumn("Temiz Çek Adedi") { DataType = UdtTypes.GetUdtSystemType(UdtType.UdtAmount) });
                ChequeBonds.Columns.Add(new DataColumn("Temiz Çek Tutar") { DataType = UdtTypes.GetUdtSystemType(UdtType.UdtAmount) });
                ChequeBonds.Columns.Add(new DataColumn("Riskli Çek Adedi") { DataType = UdtTypes.GetUdtSystemType(UdtType.UdtAmount) });
                ChequeBonds.Columns.Add(new DataColumn("Riskli Çek Tutar") { DataType = UdtTypes.GetUdtSystemType(UdtType.UdtAmount) });
                ChequeBonds.Columns.Add(new DataColumn("Toplam Çek Adedi") { DataType = UdtTypes.GetUdtSystemType(UdtType.UdtAmount) });
                ChequeBonds.Columns.Add(new DataColumn("Toplam Çek Tutar") { DataType = UdtTypes.GetUdtSystemType(UdtType.UdtAmount) });
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("select ");
            sb.AppendLine($" c.CurrentAccountCode [{SLanguage.GetString("Cari Hesap Kodu")}], c.CurrentAccountName [{SLanguage.GetString("Cari Hesap Adı")}], ch.Debtor [{SLanguage.GetString("Borçlu")}]");
            sb.AppendLine($",sum(case when isnull(ch.IsRisk,0)=0 then 1.0 else 0.0 end) [{SLanguage.GetString("Temiz Çek Adedi")}]");
            sb.AppendLine($",sum(case when isnull(ch.IsRisk,0)=0 then ch.Amount else 0.0 end) [{SLanguage.GetString("Temiz Çek Tutar")}]");
            sb.AppendLine($",sum(case when isnull(ch.IsRisk,0)=1 then 1.0 else 0.0 end) [{SLanguage.GetString("Riskli Çek Adedi")}]");
            sb.AppendLine($",sum(case when isnull(ch.IsRisk,0)=1 then ch.Amount else 0.0 end) [{SLanguage.GetString("Riskli Çek Tutar")}]");
            sb.AppendLine($",sum(1) [{SLanguage.GetString("Toplam Çek Adedi")}]");
            sb.AppendLine($",sum(ch.Amount) [{SLanguage.GetString("Toplam Çek Tutar")}]");
            sb.AppendLine("from Erp_Cheque ch with (nolock) ");
            sb.AppendLine("left join Erp_CurrentAccount c with (nolock) on c.RecId=ch.CurrentAccountId  ");
            sb.AppendLine($"where ch.ChequeType in (1,2) and ch.Status in (1,2,3,4)");
            sb.AppendLine(" group by c.CurrentAccountCode,c.CurrentAccountName,ch.Debtor");
            sb.AppendLine(" order by c.CurrentAccountCode");

            ChequeBonds = UtilityFunctions.GetDataTableList(ActiveSession.dbInfo.DBProvider, ActiveSession.dbInfo.Connection, null, "ChequeBonds", sb.ToString());

        }

        public void ShowChildCurrentAccountList()
        {
            if (ChildCurrentAccountGrid == null) return;
            if (ChildCurrentAccountPanel == null || !ChildCurrentAccountPanel.IsVisible) return;
            if (dbGrid?.CurrentItem == null) return;

            //if (ChildCurrentAccounts == null)
            //{
            //    ChildCurrentAccounts = new DataTable();
            //    ChildCurrentAccounts.Columns.Add(new DataColumn("Cari Hesap Kodu") { DataType = UdtTypes.GetUdtSystemType(Schema.Tables["Erp_CurrentAccount"].Fields["CurrentAccountCode"].UdtType) });
            //    ChildCurrentAccounts.Columns.Add(new DataColumn("Cari Hesap Adı") { DataType = UdtTypes.GetUdtSystemType(Schema.Tables["Erp_CurrentAccount"].Fields["CurrentAccountName"].UdtType) });
            //}
            StringBuilder sb = new StringBuilder();


            sb.AppendLine($"SELECT");
            sb.AppendLine($"    ECA.RecId,");
            sb.AppendLine($"    ECA.CurrentAccountCode,");
            sb.AppendLine($"    ECA.CurrentAccountName,");
            sb.AppendLine($"    T.ForexId,");
            sb.AppendLine($"    COALESCE(MF.ForexCode, 'TL') AS ForexCode,");
            sb.AppendLine($"    T.DebitSum,");
            sb.AppendLine($"    T.CreditSum,");
            sb.AppendLine($"    CASE WHEN T.DebitSum > T.CreditSum");
            sb.AppendLine($"         THEN T.DebitSum - T.CreditSum");
            sb.AppendLine($"         ELSE T.CreditSum - T.DebitSum END AS Balance,");
            sb.AppendLine($"    CASE WHEN T.DebitSum > T.CreditSum THEN 'BB'");
            sb.AppendLine($"         WHEN T.CreditSum > T.DebitSum THEN 'AB'");
            sb.AppendLine($"         ELSE '' END AS BalanceType");
            sb.AppendLine($"FROM Erp_CurrentAccount ECA WITH (NOLOCK)");
            sb.AppendLine($"LEFT JOIN Erp_CurrentAccountGroup ECAG ON ECA.GroupId = ECAG.RecId");
            sb.AppendLine($"LEFT JOIN (");
            sb.AppendLine($"    SELECT");
            sb.AppendLine($"        ECAT.CurrentAccountId,");
            sb.AppendLine($"        ECAT.ForexId,");
            sb.AppendLine($"        SUM(ISNULL(ECAT.Debit01,0)+ISNULL(ECAT.Debit02,0)+ISNULL(ECAT.Debit03,0)+ISNULL(ECAT.Debit04,0)+");
            sb.AppendLine($"            ISNULL(ECAT.Debit05,0)+ISNULL(ECAT.Debit06,0)+ISNULL(ECAT.Debit07,0)+ISNULL(ECAT.Debit08,0)+");
            sb.AppendLine($"            ISNULL(ECAT.Debit09,0)+ISNULL(ECAT.Debit10,0)+ISNULL(ECAT.Debit11,0)+ISNULL(ECAT.Debit12,0)) AS DebitSum,");
            sb.AppendLine($"        SUM(ISNULL(ECAT.Credit01,0)+ISNULL(ECAT.Credit02,0)+ISNULL(ECAT.Credit03,0)+ISNULL(ECAT.Credit04,0)+");
            sb.AppendLine($"            ISNULL(ECAT.Credit05,0)+ISNULL(ECAT.Credit06,0)+ISNULL(ECAT.Credit07,0)+ISNULL(ECAT.Credit08,0)+");
            sb.AppendLine($"            ISNULL(ECAT.Credit09,0)+ISNULL(ECAT.Credit10,0)+ISNULL(ECAT.Credit11,0)+ISNULL(ECAT.Credit12,0)) AS CreditSum");
            sb.AppendLine($"    FROM Erp_CurrentAccountTotal ECAT WITH (NOLOCK)");
            sb.AppendLine($"    GROUP BY ECAT.CurrentAccountId, ECAT.ForexId");
            sb.AppendLine($") T ON T.CurrentAccountId = ECA.RecId");
            sb.AppendLine($"LEFT JOIN Meta_Forex MF ON MF.RecId = T.ForexId");
            sb.AppendLine($"WHERE (ECA.ParentId={(dbGrid.CurrentItem as DataRowView).Row["CurrentAccountId"]} OR ECAG.GroupCode='{(dbGrid.CurrentItem as DataRowView).Row[SLanguage.GetString("Grup Kodu")]}')");
            sb.AppendLine($"ORDER BY ECA.CurrentAccountCode, COALESCE(MF.ForexCode,'TL');");
            
            //stringBuilder.AppendLine("SELECT");
            //stringBuilder.AppendLine("ECA.RecId");
            //stringBuilder.AppendLine(", ECA.CurrentAccountCode");
            //stringBuilder.AppendLine(", ECA.CurrentAccountName");
            //stringBuilder.AppendLine(", (select sum(isnull(ECAT.Debit01, 0) + isnull(ECAT.Debit02, 0) + isnull(ECAT.Debit03, 0) + isnull(ECAT.Debit04, 0) + isnull(ECAT.Debit05, 0) + isnull(ECAT.Debit06, 0) + isnull(ECAT.Debit07, 0) + isnull(ECAT.Debit08, 0) + isnull(ECAT.Debit09, 0) + isnull(ECAT.Debit10, 0) + isnull(ECAT.Debit11, 0) + isnull(ECAT.Debit12, 0)) from Erp_CurrentAccountTotal ECAT WITH(NOLOCK) where ECAT.CurrentAccountId = ECA.RecId and ECAT.ForexId is null) Debit");
            //stringBuilder.AppendLine(", (select sum(isnull(ECAT.Credit01, 0) + isnull(ECAT.Credit02, 0) + isnull(ECAT.Credit03, 0) + isnull(ECAT.Credit04, 0) + isnull(ECAT.Credit05, 0) + isnull(ECAT.Credit06, 0) + isnull(ECAT.Credit07, 0) + isnull(ECAT.Credit08, 0) + isnull(ECAT.Credit09, 0) + isnull(ECAT.Credit10, 0) + isnull(ECAT.Credit11, 0) + isnull(ECAT.Credit12, 0)) from Erp_CurrentAccountTotal ECAT WITH(NOLOCK) where ECAT.CurrentAccountId = ECA.RecId and ECAT.ForexId is null) Credit");
            //stringBuilder.AppendLine(", (case when(select sum(isnull(ECAT.Debit01, 0) + isnull(ECAT.Debit02, 0) + isnull(ECAT.Debit03, 0) + isnull(ECAT.Debit04, 0) + isnull(ECAT.Debit05, 0) + isnull(ECAT.Debit06, 0) + isnull(ECAT.Debit07, 0) + isnull(ECAT.Debit08, 0) + isnull(ECAT.Debit09, 0) + isnull(ECAT.Debit10, 0) + isnull(ECAT.Debit11, 0) + isnull(ECAT.Debit12, 0)) from Erp_CurrentAccountTotal ECAT WITH(NOLOCK) where ECAT.CurrentAccountId = ECA.RecId and ECAT.ForexId is null) > (select sum(isnull(ECAT.Credit01, 0) + isnull(ECAT.Credit02, 0) + isnull(ECAT.Credit03, 0) + isnull(ECAT.Credit04, 0) + isnull(ECAT.Credit05, 0) + isnull(ECAT.Credit06, 0) + isnull(ECAT.Credit07, 0) + isnull(ECAT.Credit08, 0) + isnull(ECAT.Credit09, 0) + isnull(ECAT.Credit10, 0) + isnull(ECAT.Credit11, 0) + isnull(ECAT.Credit12, 0)) from Erp_CurrentAccountTotal ECAT WITH(NOLOCK) where ECAT.CurrentAccountId = ECA.RecId and ECAT.ForexId is null) then(select sum(isnull(ECAT.Debit01, 0) + isnull(ECAT.Debit02, 0) + isnull(ECAT.Debit03, 0) + isnull(ECAT.Debit04, 0) + isnull(ECAT.Debit05, 0) + isnull(ECAT.Debit06, 0) + isnull(ECAT.Debit07, 0) + isnull(ECAT.Debit08, 0) + isnull(ECAT.Debit09, 0) + isnull(ECAT.Debit10, 0) + isnull(ECAT.Debit11, 0) + isnull(ECAT.Debit12, 0)) from Erp_CurrentAccountTotal ECAT WITH(NOLOCK) where ECAT.CurrentAccountId = ECA.RecId and ECAT.ForexId is null) - (select sum(isnull(ECAT.Credit01, 0) + isnull(ECAT.Credit02, 0) + isnull(ECAT.Credit03, 0) + isnull(ECAT.Credit04, 0) + isnull(ECAT.Credit05, 0) + isnull(ECAT.Credit06, 0) + isnull(ECAT.Credit07, 0) + isnull(ECAT.Credit08, 0) + isnull(ECAT.Credit09, 0) + isnull(ECAT.Credit10, 0) + isnull(ECAT.Credit11, 0) + isnull(ECAT.Credit12, 0)) from Erp_CurrentAccountTotal ECAT WITH(NOLOCK) where ECAT.CurrentAccountId = ECA.RecId and ECAT.ForexId is null) else (select sum(isnull(ECAT.Credit01, 0) + isnull(ECAT.Credit02, 0) + isnull(ECAT.Credit03, 0) + isnull(ECAT.Credit04, 0) + isnull(ECAT.Credit05, 0) + isnull(ECAT.Credit06, 0) + isnull(ECAT.Credit07, 0) + isnull(ECAT.Credit08, 0) + isnull(ECAT.Credit09, 0) + isnull(ECAT.Credit10, 0) + isnull(ECAT.Credit11, 0) + isnull(ECAT.Credit12, 0)) from Erp_CurrentAccountTotal ECAT WITH(NOLOCK) where ECAT.CurrentAccountId = ECA.RecId and ECAT.ForexId is null) -(select sum(isnull(ECAT.Debit01, 0) + isnull(ECAT.Debit02, 0) + isnull(ECAT.Debit03, 0) + isnull(ECAT.Debit04, 0) + isnull(ECAT.Debit05, 0) + isnull(ECAT.Debit06, 0) + isnull(ECAT.Debit07, 0) + isnull(ECAT.Debit08, 0) + isnull(ECAT.Debit09, 0) + isnull(ECAT.Debit10, 0) + isnull(ECAT.Debit11, 0) + isnull(ECAT.Debit12, 0)) from Erp_CurrentAccountTotal ECAT WITH(NOLOCK) where ECAT.CurrentAccountId = ECA.RecId and ECAT.ForexId is null) end) Balance");
            //stringBuilder.AppendLine($", (case when(select sum(isnull(ECAT.Debit01, 0) + isnull(ECAT.Debit02, 0) + isnull(ECAT.Debit03, 0) + isnull(ECAT.Debit04, 0) + isnull(ECAT.Debit05, 0) + isnull(ECAT.Debit06, 0) + isnull(ECAT.Debit07, 0) + isnull(ECAT.Debit08, 0) + isnull(ECAT.Debit09, 0) + isnull(ECAT.Debit10, 0) + isnull(ECAT.Debit11, 0) + isnull(ECAT.Debit12, 0)) from Erp_CurrentAccountTotal ECAT WITH(NOLOCK) where ECAT.CurrentAccountId = ECA.RecId and ECAT.ForexId is null) > (select sum(isnull(ECAT.Credit01, 0) + isnull(ECAT.Credit02, 0) + isnull(ECAT.Credit03, 0) + isnull(ECAT.Credit04, 0) + isnull(ECAT.Credit05, 0) + isnull(ECAT.Credit06, 0) + isnull(ECAT.Credit07, 0) + isnull(ECAT.Credit08, 0) + isnull(ECAT.Credit09, 0) + isnull(ECAT.Credit10, 0) + isnull(ECAT.Credit11, 0) + isnull(ECAT.Credit12, 0)) from Erp_CurrentAccountTotal ECAT WITH(NOLOCK) where ECAT.CurrentAccountId = ECA.RecId and ECAT.ForexId is null) then '{SLanguage.GetString("BB")}' when(select sum(isnull(ECAT.Credit01, 0) + isnull(ECAT.Credit02, 0) + isnull(ECAT.Credit03, 0) + isnull(ECAT.Credit04, 0) + isnull(ECAT.Credit05, 0) + isnull(ECAT.Credit06, 0) + isnull(ECAT.Credit07, 0) + isnull(ECAT.Credit08, 0) + isnull(ECAT.Credit09, 0) + isnull(ECAT.Credit10, 0) + isnull(ECAT.Credit11, 0) + isnull(ECAT.Credit12, 0)) from Erp_CurrentAccountTotal ECAT WITH(NOLOCK) where ECAT.CurrentAccountId = ECA.RecId and ECAT.ForexId is null) > (select sum(isnull(ECAT.Debit01, 0) + isnull(ECAT.Debit02, 0) + isnull(ECAT.Debit03, 0) + isnull(ECAT.Debit04, 0) + isnull(ECAT.Debit05, 0) + isnull(ECAT.Debit06, 0) + isnull(ECAT.Debit07, 0) + isnull(ECAT.Debit08, 0) + isnull(ECAT.Debit09, 0) + isnull(ECAT.Debit10, 0) + isnull(ECAT.Debit11, 0) + isnull(ECAT.Debit12, 0)) from Erp_CurrentAccountTotal ECAT WITH(NOLOCK) where ECAT.CurrentAccountId = ECA.RecId and ECAT.ForexId is null) then '{SLanguage.GetString("AB")}' else '' end) BalanceType");
            //stringBuilder.AppendLine("FROM Erp_CurrentAccount ECA WITH (NOLOCK)");
            //stringBuilder.AppendLine("LEFT JOIN Erp_CurrentAccountGroup ECAG ON ECA.GroupId = ECAG.RecId");
            //sb.AppendLine($"WHERE (ECA.ParentId={(dbGrid.CurrentItem as DataRowView).Row["CurrentAccountId"]} OR ECAG.GroupCode='{(dbGrid.CurrentItem as DataRowView).Row[SLanguage.GetString("Grup Kodu")]}')");
            ChildCurrentAccounts = UtilityFunctions.GetDataTableList(ActiveSession.dbInfo.DBProvider, ActiveSession.dbInfo.Connection, null, "ChildCurrentAccounts", sb.ToString());
        }

        public void ShowCurrentAccountContactList()
        {
            if (CurrentAccountContactGrid == null) return;
            if (CurrentAccountContactPanel == null || !CurrentAccountContactPanel.IsVisible) return;
            if (dbGrid?.CurrentItem == null) return;
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine($"SELECT");
            stringBuilder.AppendLine($"ECAC.Title");
            stringBuilder.AppendLine($", ECAC.Name");
            stringBuilder.AppendLine($", ECAC.Surname");
            stringBuilder.AppendLine($", ECAC.Position");
            stringBuilder.AppendLine($", ECAC.Phone");
            stringBuilder.AppendLine($", ECAC.Fax");
            stringBuilder.AppendLine($", ECAC.EMailAddress");
            stringBuilder.AppendLine($"FROM Erp_CurrentAccountContact ECAC WITH(NOLOCK)");
            stringBuilder.AppendLine($"WHERE ECAC.CurrentAccountId={(dbGrid.CurrentItem as DataRowView).Row["CurrentAccountId"]} AND ISNULL(ECAC.EMailAddress,'')<>''");
            CurrentAccountContacts = UtilityFunctions.GetDataTableList(ActiveSession.dbInfo.DBProvider, ActiveSession.dbInfo.Connection, null, "CurrentAccountContacts", stringBuilder.ToString());
        }

        public void ShowIntelligenceNotesList()
        {
            if (LiveGridControlIntelligenceNotes == null) return;
            if (LiveLayoutPanelIntelligenceNotes == null || !LiveLayoutPanelIntelligenceNotes.IsVisible) return;
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("select ");
            sb.AppendLine("ct.*");
            sb.AppendLine("from Erp_CurrentAccountTransaction ct with (nolock) ");
            sb.AppendLine($"where ct.CurrentAccountId = {GetRefId("CurrentAccountId")} and ct.TransactionType = 7");
            sb.AppendLine(" order by ct.TransactionDate");
            IntelligenceNotesList = UtilityFunctions.GetDataTableList(ActiveSession.dbInfo.DBProvider, ActiveSession.dbInfo.Connection, null, "IntelligenceNotesList", sb.ToString());
        }

        public void ShowSellerNotesList()
        {
            if (LiveGridControlSellerNotes == null) return;
            if (LiveLayoutPanelSellerNotes == null || !LiveLayoutPanelSellerNotes.IsVisible) return;
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("select ");
            sb.AppendLine("ct.*");
            sb.AppendLine("from Erp_CurrentAccountTransaction ct with (nolock) ");
            sb.AppendLine($"where ct.CurrentAccountId = {GetRefId("CurrentAccountId")} and ct.TransactionType = 4");
            sb.AppendLine(" order by ct.TransactionDate");
            SellerNotesList = UtilityFunctions.GetDataTableList(ActiveSession.dbInfo.DBProvider, ActiveSession.dbInfo.Connection, null, "SellerNotesList", sb.ToString());
        }

        public void ShowBouncedDocumentList()
        {
            if (LiveGridControlBouncedDocument == null) return;
            if (LiveLayoutPanelBouncedDocument == null || !LiveLayoutPanelBouncedDocument.IsVisible) return;
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("select ");
            sb.AppendLine("ct.*");
            sb.AppendLine("from Erp_CurrentAccountTransaction ct with (nolock) ");
            sb.AppendLine($"where ct.CurrentAccountId = {GetRefId("RecId")} and ct.TransactionType = 2");
            sb.AppendLine(" order by ct.TransactionDate");
            BouncedDocumentList = UtilityFunctions.GetDataTableList(ActiveSession.dbInfo.DBProvider, ActiveSession.dbInfo.Connection, null, "BouncedDocumentList", sb.ToString());
        }

        public void ShowOperationList()
        {
            if (OperationGrid == null) return;
            if (OperationPanel == null || !OperationPanel.IsVisible) return;
            if (dbGrid?.CurrentItem == null) return;

            if (CurrentAccountReceiptItemBo == null)
                CurrentAccountReceiptItemBo = container.Resolve<CurrentAccountReceiptItemBO>();
            CurrentAccountReceiptItemBo.GetAll(new WhereField[]
            {
                new WhereField("Erp_CurrentAccountReceiptItem", "CurrentAccountId", GetRefId("CurrentAccountId"), WhereCondition.Equal),
                new WhereField("Erp_CurrentAccountReceiptItem", "ReceiptType", 56, WhereCondition.Equal)
            });

            //StringBuilder sb = new StringBuilder();
            //sb.AppendLine("select ");
            //sb.AppendLine("ct.*");
            //sb.AppendLine(", EE.EmployeeCode, EE.EmployeeName");
            //sb.AppendLine("from Erp_CurrentAccountReceiptItem ct with (nolock) ");
            //sb.AppendLine("LEFT JOIN Erp_Employee EE WITH (NOLOCK) ON ct.EmployeeId=EE.RecId");
            //sb.AppendLine($"where ct.CurrentAccountId = {GetRefId("CurrentAccountId")} and ct.TransactionType = 35 and CurrentAccountReceiptId is null");
            //sb.AppendLine("order by ct.ReceiptDate");
            //OperationList = UtilityFunctions.GetDataTableList(ActiveSession.dbInfo.DBProvider, ActiveSession.dbInfo.Connection, null, "OperationList", sb.ToString());

            if (OperationGrid != null)
                OperationGrid.ItemsSource = CurrentAccountReceiptItemBo.Data.Tables["Erp_CurrentAccountReceiptItem"].DefaultView;
        }

        public void ShowAggrementList()
        {
            if (OperationGrid == null) return;
            if (OperationPanel == null || !OperationPanel.IsVisible) return;

            if (AggrementReceiptTable == null)
            {
                AggrementReceiptTable = new DataTable();
                AggrementReceiptTable.Columns.Add(new DataColumn("RecId") { DataType = UdtTypes.GetUdtSystemType(UdtType.UdtRecId) });
                AggrementReceiptTable.Columns.Add(new DataColumn("TransactionDate") { DataType = UdtTypes.GetUdtSystemType(UdtType.UdtDate) });
                AggrementReceiptTable.Columns.Add(new DataColumn("TransactionAggrementType") { DataType = UdtTypes.GetUdtSystemType(UdtType.UdtInt8) });
                AggrementReceiptTable.Columns.Add(new DataColumn("Explanation") { DataType = UdtTypes.GetUdtSystemType(UdtType.UdtTextMax) });
            }
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("select ");
            sb.AppendLine("ct.RecId RecId,convert(varchar, ct.TransactionDate, 104) TransactionDate");
            sb.AppendLine(",ct.TransactionAggrementType TransactionAggrementType,ct.Explanation Explanation ");
            sb.AppendLine("from Erp_CurrentAccountTransaction ct with (nolock) ");
            sb.AppendLine($"where ct.CurrentAccountId = {GetRefId("RecId")} and ct.TransactionType = 6");
            sb.AppendLine(" order by ct.TransactionDate");

            AggrementReceiptTable = UtilityFunctions.GetDataTableList(ActiveSession.dbInfo.DBProvider, ActiveSession.dbInfo.Connection, null, "AggrementReceiptTable", sb.ToString());
        }

        public void ShowRiskLimitList()
        {
            if (RiskLimitGrid == null) return;
            if (RiskLimitPanel == null || !RiskLimitPanel.IsVisible) return;
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("select ");
            sb.AppendLine("ct.RecId RecId,ct.TransactionDate");
            sb.AppendLine(",ct.Explanation Explanation,ct.TransactionAmount,ct.RiskLimit");
            sb.AppendLine("from Erp_CurrentAccountTransaction ct with (nolock) ");
            sb.AppendLine($"where ct.CurrentAccountId = {GetRefId("RecId")} and ct.TransactionType = 1");
            sb.AppendLine(" order by ct.TransactionDate");

            RiskLimitList = UtilityFunctions.GetDataTableList(ActiveSession.dbInfo.DBProvider, ActiveSession.dbInfo.Connection, null, "RiskLimitList", sb.ToString());
        }

        public void ShowRiskPointList()
        {
            if (!sysMng.HasApplication("RbKaresiModule")) return;
            if (RiskPointTableGrid == null) return;
            if (LiveLayoutPanelRiskPointTable == null || LiveLayoutPanelRiskPointTable.Closed || LiveLayoutPanelRiskPointTable.IsHidden || (LiveLayoutPanelRiskPointTable.IsAutoHidden && LiveLayoutPanelRiskPointTable.AutoHideExpandState == AutoHideExpandState.Hidden)) return;
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("SELECT ECAAE.*,");
            sb.AppendLine("MCAAE.AnalysisElementCode,MCAAE.AnalysisElementName");
            sb.AppendLine("FROM Erp_CurrentAccountAnalysisElement ECAAE WITH (NOLOCK)");
            sb.AppendLine("LEFT JOIN Meta_CurrentAccountAnalysisElement MCAAE WITH (NOLOCK) ON ECAAE.MetaCurrentAccountAnalysisElementId = MCAAE.RecId");
            sb.AppendLine($"WHERE ECAAE.CurrentAccountId = {GetRefId("RecId")}");
            sb.AppendLine("ORDER BY MCAAE.AnalysisElementCode");
            RiskPointTable = UtilityFunctions.GetDataTableList(ActiveSession.dbInfo.DBProvider, ActiveSession.dbInfo.Connection, null, "RiskPointTable", sb.ToString());
        }

        public void ShowOtherCompanyBalance()
        {
            if (!sysMng.HasApplication("RbKaresiModule")) return;
            if (OtherCompanyBalanceTableGrid == null) return;
            if (dbGrid.CurrentItem == null) return;
            if (LiveLayoutPanelOtherCompanyBalanceTable == null || LiveLayoutPanelOtherCompanyBalanceTable.Closed || LiveLayoutPanelOtherCompanyBalanceTable.IsHidden || (LiveLayoutPanelOtherCompanyBalanceTable.IsAutoHidden && LiveLayoutPanelOtherCompanyBalanceTable.AutoHideExpandState == AutoHideExpandState.Hidden)) return;
            OtherCompanyBalanceTable?.Rows.Clear();

            object paramValue = ActiveSession.ParamService.GetParameterClass("RbKaresiTekstilParameters").GetValue("OperationCompanyCode");
            if (paramValue is string && !string.IsNullOrEmpty(paramValue.ToString()))
            {
                string taxNo = (dbGrid.CurrentItem as DataRowView).Row[SLanguage.GetString("Vergi Numarası")].ToString();
                if (!string.IsNullOrEmpty(taxNo))
                {
                    string debitStr = "(select sum(isnull(at.Debit01,0)+isnull(at.Debit02,0)+isnull(at.Debit03,0)+isnull(at.Debit04,0)+isnull(at.Debit05,0)+isnull(at.Debit06,0)+isnull(at.Debit07,0)+isnull(at.Debit08,0)+isnull(at.Debit09,0)+isnull(at.Debit10,0)+isnull(at.Debit11,0)+isnull(at.Debit12,0)) from Erp_CurrentAccountTotal at with (nolock) where at.CurrentAccountId=ECA.RecId and at.ForexId is null)";
                    string creditStr = "(select sum(isnull(at.Credit01,0)+isnull(at.Credit02,0)+isnull(at.Credit03,0)+isnull(at.Credit04,0)+isnull(at.Credit05,0)+isnull(at.Credit06,0)+isnull(at.Credit07,0)+isnull(at.Credit08,0)+isnull(at.Credit09,0)+isnull(at.Credit10,0)+isnull(at.Credit11,0)+isnull(at.Credit12,0)) from Erp_CurrentAccountTotal at with (nolock) where at.CurrentAccountId=ECA.RecId and at.ForexId is null)";

                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine("SELECT");
                    sb.AppendLine("ECA.CurrentAccountCode,ECA.CurrentAccountName");
                    sb.AppendLine($", {debitStr} Debit");
                    sb.AppendLine($", {creditStr} Credit");
                    sb.AppendLine($", (case when {debitStr} > {creditStr} then {debitStr} - {creditStr} else {creditStr} - {debitStr} end) Balance");
                    sb.AppendLine($", (case when {debitStr} > {creditStr} then '{SLanguage.GetString("BB")}' when {creditStr} > {debitStr} then '{SLanguage.GetString("AB")}' else '' end) BalanceType");
                    sb.AppendLine("FROM Erp_CurrentAccount ECA WITH (NOLOCK)");
                    sb.AppendLine($"WHERE ECA.CompanyId IN (SELECT EC.RecId FROM Erp_Company EC WITH (NOLOCK) WHERE EC.CompanyCode='{paramValue}') AND ECA.TaxNo='{taxNo}'");
                    sb.AppendLine("ORDER BY ECA.CurrentAccountCode");
                    OtherCompanyBalanceTable = UtilityFunctions.GetDataTableList(ActiveSession.dbInfo.DBProvider, ActiveSession.dbInfo.Connection, null, "RiskPointTable", sb.ToString());
                }
            }
        }

        string[] Params;
        public void ShowCustomerActivityList()
        {
            if (CustomerActivityGrid == null) return;
            if (CustomerActivityPanel == null || !CustomerActivityPanel.IsVisible) return;
            if (CustomerActivityGrid.ColumnDefinitions.Count < 1)
            {
                ReceiptColumnCollection columns = new ReceiptColumnCollection();
                columns.Add(new ReceiptColumn { ColumnName = "RecordTypeName", Caption = SLanguage.GetString("Eklenen Fiş"), Width = 70, DataType = typeof(string) });
                columns.Add(new ReceiptColumn { ColumnName = "DocumentNo", Caption = SLanguage.GetString("Belge No"), Width = 70, DataType = typeof(string) });
                columns.Add(new ReceiptColumn { ColumnName = "ResourceName", Caption = SLanguage.GetString("Yetkili Adı"), Width = 100, DataType = typeof(string) });
                columns.Add(new ReceiptColumn { ColumnName = "TranTypeName", Caption = SLanguage.GetString("Aktivite Tipi"), Width = 90, DataType = typeof(string) });
                columns.Add(new ReceiptColumn { ColumnName = "Explanation", Caption = SLanguage.GetString("Açıklama"), Width = 250, DataType = typeof(string) });
                columns.Add(new ReceiptColumn { ColumnName = "ResultExplanation", Caption = SLanguage.GetString("Sonuç Açıklaması"), Width = 100, DataType = typeof(string) });
                columns.Add(new ReceiptColumn { ColumnName = "PutOffExplanation", Caption = SLanguage.GetString("Erteleme Açıklaması"), Width = 70, DataType = typeof(string) });
                columns.Add(new ReceiptColumn { ColumnName = "CancellationExplanation", Caption = SLanguage.GetString("İptal Açıklaması"), Width = 70, DataType = typeof(string) });
                columns.Add(new ReceiptColumn { ColumnName = "DepartmentName", Caption = SLanguage.GetString("Departman"), Width = 90, DataType = typeof(string) });
                columns.Add(new ReceiptColumn { ColumnName = "SecondResourceName", Caption = SLanguage.GetString("Araç"), Width = 70, DataType = typeof(string) });
                columns.Add(new ReceiptColumn { ColumnName = "CustomerResource", Caption = SLanguage.GetString("Cari-Firma Yetkilisi"), Width = 100, DataType = typeof(string) });
                columns.Add(new ReceiptColumn { ColumnName = "FlowName", Caption = SLanguage.GetString("Akış Adı"), Width = 100, DataType = typeof(string) });
                columns.Add(new ReceiptColumn { ColumnName = "FlowStage", Caption = SLanguage.GetString("Akış Aşaması"), Width = 100, DataType = typeof(string) });
                columns.Add(new ReceiptColumn { ColumnName = "TranStatusName", Caption = SLanguage.GetString("Aktivite Durumu"), Width = 70, DataType = typeof(string) });
                columns.Add(new ReceiptColumn { ColumnName = "PriorityName", Caption = SLanguage.GetString("Öncelik"), Width = 70, DataType = typeof(string) });
                columns.Add(new ReceiptColumn { ColumnName = "TranSubTypeName", Caption = SLanguage.GetString("Aktivite Alt Tipi"), Width = 70, DataType = typeof(string) });
                columns.Add(new ReceiptColumn { ColumnName = "ProjectName", Caption = SLanguage.GetString("Proje Adı"), Width = 100, DataType = typeof(string) });
                columns.Add(new ReceiptColumn { ColumnName = "MarketingActivityName", Caption = SLanguage.GetString("Pazarlama Aktivite Adı"), Width = 120, DataType = typeof(string) });
                columns.Add(new ReceiptColumn { ColumnName = "SpecialCode", Caption = SLanguage.GetString("Özel Kod"), Width = 70, DataType = typeof(string) });
                columns.Add(new ReceiptColumn { ColumnName = "PlannedDate", Caption = SLanguage.GetString("Başlangıç Tarihi"), Width = 70, EditorType = EditorType.DateEditor, DataType = typeof(DateTime) });
                columns.Add(new ReceiptColumn { ColumnName = "PlannedTime", Caption = SLanguage.GetString("Başlangıç Saati"), Width = 50, EditorType = EditorType.TimeEditor, DataType = typeof(DateTime) });
                columns.Add(new ReceiptColumn { ColumnName = "ActualDate", Caption = SLanguage.GetString("Gerçekleşme Tarihi"), Width = 70, EditorType = EditorType.DateEditor, DataType = typeof(DateTime) });
                columns.Add(new ReceiptColumn { ColumnName = "ActualTime", Caption = SLanguage.GetString("Gerçekleşme Saati"), Width = 50, EditorType = EditorType.TimeEditor, DataType = typeof(DateTime) });
                columns.Add(new ReceiptColumn { ColumnName = "Price", Caption = SLanguage.GetString("Fiyat"), Width = 70, UsageType = FieldUsage.UnitPrice, DataType = typeof(string) });
                columns.Add(new ReceiptColumn { ColumnName = "ForexPrice", Caption = SLanguage.GetString("Döviz Fiyat"), Width = 70, UsageType = FieldUsage.ForexUnitPrice, DataType = typeof(decimal) });
                columns.Add(new ReceiptColumn { ColumnName = "ForexRate", Caption = SLanguage.GetString("Döviz Kuru"), Width = 70, UsageType = FieldUsage.ForexRate, DataType = typeof(decimal) });
                columns.Add(new ReceiptColumn { ColumnName = "ForexCode", Caption = SLanguage.GetString("Döviz"), Width = 40, DataType = typeof(string) });
                columns.Add(new ReceiptColumn { ColumnName = "SourceDocumentNo", Caption = SLanguage.GetString("Akışı Oluşuran Belge"), Width = 80, DataType = typeof(string) });
                columns.Add(new ReceiptColumn { ColumnName = "TicketNo", Caption = SLanguage.GetString("Akış No"), Width = 70, DataType = typeof(string) });
                columns.Add(new ReceiptColumn { ColumnName = "ReceiptNo", Caption = SLanguage.GetString("Fiş No"), Width = 70, DataType = typeof(string) });
                columns.Add(new ReceiptColumn { ColumnName = "ReceiptDate", Caption = SLanguage.GetString("Fiş Tarihi"), Width = 70, EditorType = EditorType.DateEditor, DataType = typeof(DateTime) });
                columns.Add(new ReceiptColumn { ColumnName = "ReceiptDocNo", Caption = SLanguage.GetString("Belge No"), Width = 80, DataType = typeof(string) });
                columns.Add(new ReceiptColumn { ColumnName = "ReceiptTotal", Caption = SLanguage.GetString("Fiş Toplamı"), Width = 70, UsageType = FieldUsage.Amount, DataType = typeof(decimal) });
                columns.Add(new ReceiptColumn { ColumnName = "ReceiptTypeName", Caption = SLanguage.GetString("Fiş Tipi"), Width = 150, DataType = typeof(string) });
                CustomerActivityGrid.ColumnDefinitions = columns;
            }

            var customerTransactionHistoryService = container.Resolve<ISystemService>("CustomerTransactionHistoryService") as SystemServiceBase;
            if (customerTransactionHistoryService == null) return;

            if (_pmParam.Tag != null)
                Params = _pmParam.Tag.ToString().Split(',');
            else
            {
                Params = new string[2];
                Params[0] = _pmParam.itemID.ToString();
                Params[1] = "";
            }

            if (Params[1] != "" && Params[0] == "")
            {
                string sqlStr = $"select CurrentAccountId from Crm_Lead where RecId ={Params[1]}";

                DataTable dt = UtilityFunctions.GetDataTableList(ActiveSession.dbInfo.DBProvider, ActiveSession.dbInfo.Connection, null, "Crm_Lead", sqlStr);
                if (dt != null && dt.Rows.Count > 0 && !dt.Rows[0].IsNull("CurrentAccountId"))
                {
                    Params[0] = dt.Rows[0]["CurrentAccountId"].ToString();
                }
            }
            else if (Params[0] != "" && Params[1] == "")
            {
                string sqlStr = $"select RecId from Crm_Lead where CurrentAccountId ={Params[0]}";

                DataTable dt = UtilityFunctions.GetDataTableList(ActiveSession.dbInfo.DBProvider, ActiveSession.dbInfo.Connection, null, "Crm_Lead", sqlStr);
                if (dt != null && dt.Rows.Count > 0 && !dt.Rows[0].IsNull("RecId"))
                {
                    Params[1] = dt.Rows[0]["RecId"].ToString();
                }
            }

            object hb = customerTransactionHistoryService.Execute(Params[0], Params[1], true);
            CustomerActivityTable = hb as DataTable;

            //if (Params[1] != "" && Params[0] == "") CustomerTypeHeader(1);
            //else if (Params[0] != "" && Params[1] == "") CustomerTypeHeader(0);
            //else if (Params[0] != "" && Params[1] != "") CustomerTypeHeader(2);
        }

        private void dbGrid_CurrentItemChanged(object sender, CurrentItemChangedEventArgs e)
        {
            if ((dbGrid?.View as TableView)?.DataControl.CurrentItem == null) return;
            DataRowView dRow = dbGrid.View.DataControl.CurrentItem as DataRowView;
            if (dRow?.Row?.Table.Columns == null) return;
            if (/*!dRow.Row.Table.Columns.Contains("RT") || */!dRow.Row.Table.Columns.Contains("CurrentAccountId")) return;
            ShowOperationList();
            //ShowAggrementList();
            //ShowRiskLimitList();
            //ShowBouncedDocumentList();
            //ShowSellerNotesList();
            ShowIntelligenceNotesList();
            //ShowRiskPointList();
            //ShowOtherCompanyBalance();
            //ShowCustomerActivityList();

            ShowChildCurrentAccountList();
            ShowCurrentAccountContactList();
        }

        bool isLoaded = false;
        public override void _view_Loaded(object sender, RoutedEventArgs e)
        {
            base._view_Loaded(sender, e);
            ISysCommand runCommand = CmdList["Run"];
            if (runCommand != null)
            {
                runCommand = CmdList["RefreshList"];
                LiveButton[] liveButtons = FrameworkTreeHelper.FindLogicalChilds<LiveButton>(ActiveViewControl);
                if (liveButtons.Length > 0)
                {
                    foreach (var liveButton in liveButtons.Where(liveButton => liveButton.Name == "RefreshButton"))
                    {
                        liveButton.Command = runCommand;
                    }
                }
            }
            if (!isLoaded)
            {
            }
            isLoaded = true;
        }

        public override void OnListCommand(ISysCommandParam obj)
        {
            base.OnListCommand(obj);
        }

        public override void Dispose()
        {
            if (disposed)
                return;

            Set_WindowSetting();

            if (OperationPanel != null) OperationPanel.IsVisibleChanged -= LayoutPanel_IsVisibleChanged;
            if (AggrementPanel != null) AggrementPanel.IsVisibleChanged -= LayoutPanel_IsVisibleChanged;
            if (RiskLimitPanel != null) RiskLimitPanel.IsVisibleChanged -= LayoutPanel_IsVisibleChanged;
            if (CustomerActivityPanel != null) CustomerActivityPanel.IsVisibleChanged -= LayoutPanel_IsVisibleChanged;
            if (LiveLayoutPanelBouncedDocument != null) LiveLayoutPanelBouncedDocument.IsVisibleChanged -= LayoutPanel_IsVisibleChanged;
            if (LiveLayoutPanelSellerNotes != null) LiveLayoutPanelSellerNotes.IsVisibleChanged -= LayoutPanel_IsVisibleChanged;
            if (LiveLayoutPanelIntelligenceNotes != null) LiveLayoutPanelIntelligenceNotes.IsVisibleChanged -= LayoutPanel_IsVisibleChanged;
            if (ChequeBondPanel != null) ChequeBondPanel.IsVisibleChanged -= LayoutPanel_IsVisibleChanged;
            if (LiveLayoutPanelRiskPointTable != null) LiveLayoutPanelRiskPointTable.IsVisibleChanged -= LayoutPanel_IsVisibleChanged;
            if (LiveLayoutPanelOtherCompanyBalanceTable != null) LiveLayoutPanelOtherCompanyBalanceTable.IsVisibleChanged -= LayoutPanel_IsVisibleChanged;
            if (ChildCurrentAccountPanel != null) ChildCurrentAccountPanel.IsVisibleChanged -= LayoutPanel_IsVisibleChanged;
            if (CurrentAccountContactPanel != null) CurrentAccountContactPanel.IsVisibleChanged -= LayoutPanel_IsVisibleChanged;

            OperationPanel = null;
            AggrementPanel = null;
            RiskLimitPanel = null;
            CustomerActivityPanel = null;
            LiveLayoutPanelBouncedDocument = null;
            LiveLayoutPanelSellerNotes = null;
            LiveLayoutPanelIntelligenceNotes = null;
            ChequeBondPanel = null;
            LiveLayoutPanelRiskPointTable = null;
            LiveLayoutPanelOtherCompanyBalanceTable = null;
            ChildCurrentAccountPanel = null;

            if (dbGrid != null)
            {
                dbGrid.CurrentItemChanged -= dbGrid_CurrentItemChanged;
                dbGrid.ItemsSourceChanged -= dbGrid_ItemsSourceChanged;
                dbGrid.ColumnDefinitions?.Dispose();
                dbGrid.Columns?.Clear();
                dbGrid.Dispose();
            }

            currentAccountAnalysisList?.Dispose();
            currentAccountAnalysisList = null;

            riskLimitList?.Dispose();
            riskLimitList = null;

            chequeBonds?.Dispose();
            chequeBonds = null;

            aggrementReceiptTable?.Dispose();
            aggrementReceiptTable = null;

            _bouncedDocumentList?.Dispose();
            _bouncedDocumentList = null;

            _sellerNotesList?.Dispose();
            _sellerNotesList = null;

            _intelligenceNotesList?.Dispose();
            _intelligenceNotesList = null;

            operationList?.Dispose();
            operationList = null;

            customerActivityTable?.Dispose();
            customerActivityTable = null;

            _riskPointTable?.Dispose();
            _riskPointTable = null;

            _otherCompanyBalanceTable?.Dispose();
            _otherCompanyBalanceTable = null;

            pPolicy?.Dispose();

            base.Dispose();
        }

        private void Set_WindowSetting()
        {
            foreach (var fitem in pPolicy.statementList[0].filterList.Where(fitem => fitem.field1Name == "CurrentAccountCode" && fitem.filterTable1Name == "Erp_CurrentAccount"))
            {
                try
                {
                    SysMng.Instance.getSession().WindowSettings.SetValue(0, "AgingReportResultsManagementPM", "FilterOptions", "FCurrentAccountCode", fitem.valueList[0].ToString());
                    SysMng.Instance.getSession().WindowSettings.SetValue(0, "AgingReportResultsManagementPM", "FilterOptions", "LCurrentAccountCode", fitem.valueList[1].ToString());
                }
                catch { }
            }

            foreach (var fitem in pPolicy.statementList[0].filterList.Where(fitem => fitem.field1Name == "GroupCode" && fitem.filterTable1Name == "Erp_TradingGroup"))
            {
                try
                {
                    SysMng.Instance.getSession().WindowSettings.SetValue(0, "AgingReportResultsManagementPM", "FilterOptions", "FTradingGroupGroupCode", fitem.valueList[0].ToString());
                    SysMng.Instance.getSession().WindowSettings.SetValue(0, "AgingReportResultsManagementPM", "FilterOptions", "LTradingGroupGroupCode", fitem.valueList[1].ToString());
                }
                catch { }
            }

            try
            {
                RepOps ops = pPolicy.FindOption("IsIbanNo");
                if (ops != null)
                {
                    SysMng.Instance.getSession().WindowSettings.SetValue(0, "AgingReportResultsManagementPM", "FilterOptions", "IsIbanNo", ops.IsChecked.ToString());
                }
            }
            catch { }

            try
            {
                RepOps ops = pPolicy.FindOption("IsTaxNo");
                if (ops != null)
                {
                    SysMng.Instance.getSession().WindowSettings.SetValue(0, "AgingReportResultsManagementPM", "FilterOptions", "IsTaxNo", ops.IsChecked.ToString());
                }
            }
            catch { }

            try
            {
                RepOps ops = pPolicy.FindOption("IsForexCorrection");
                if (ops != null)
                {
                    SysMng.Instance.getSession().WindowSettings.SetValue(0, "AgingReportResultsManagementPM", "FilterOptions", "IsForexCorrection", ops.IsChecked.ToString());
                }
            }
            catch { }

            try
            {
                RepOps ops = pPolicy.FindOption("OptionsGroup3");
                if (ops != null)
                {
                    SysMng.Instance.getSession().WindowSettings.SetValue(0, "AgingReportResultsManagementPM", "FilterOptions", "ForexOptions", ops.selindex.ToString());
                }
            }
            catch { }

            try
            {
                RepOps ops = pPolicy.FindOption("OptionsGroup2");
                if (ops != null)
                {
                    SysMng.Instance.getSession().WindowSettings.SetValue(0, "AgingReportResultsManagementPM", "FilterOptions", "ReportDate", ops.selectedItem.ToString());
                }
            }
            catch { }


            try
            {
                RepOps ops = pPolicy.FindOption("IsGsmPhone");
                if (ops != null)
                {
                    SysMng.Instance.getSession().WindowSettings.SetValue(0, "AgingReportResultsManagementPM", "FilterOptions", "IsGsmPhone", ops.IsChecked.ToString());
                }
            }
            catch { }

            try
            {
                RepOps ops = pPolicy.FindOption("IsAddressPhone");
                if (ops != null)
                {
                    SysMng.Instance.getSession().WindowSettings.SetValue(0, "AgingReportResultsManagementPM", "FilterOptions", "IsAddressPhone", ops.IsChecked.ToString());
                }
            }
            catch { }

            try
            {
                RepOps ops = pPolicy.FindOption("IsAddressFax");
                if (ops != null)
                {
                    SysMng.Instance.getSession().WindowSettings.SetValue(0, "AgingReportResultsManagementPM", "FilterOptions", "IsAddressFax", ops.IsChecked.ToString());
                }
            }
            catch { }

            try
            {
                RepOps ops = pPolicy.FindOption("IsAddressInfo");
                if (ops != null)
                {
                    SysMng.Instance.getSession().WindowSettings.SetValue(0, "AgingReportResultsManagementPM", "FilterOptions", "IsAddressInfo", ops.IsChecked.ToString());
                }
            }
            catch { }

            try
            {
                RepOps ops = pPolicy.FindOption("FirstRange");
                if (ops != null)
                {
                    SysMng.Instance.getSession().WindowSettings.SetValue(0, "AgingReportResultsManagementPM", "FilterOptions", "FirstRange", ops.IsChecked.ToString());
                }
            }
            catch { }

            try
            {
                RepOps ops = pPolicy.FindOption("SecondRange");
                if (ops != null)
                {
                    SysMng.Instance.getSession().WindowSettings.SetValue(0, "AgingReportResultsManagementPM", "FilterOptions", "SecondRange", ops.IsChecked.ToString());
                }
            }
            catch { }

            try
            {
                RepOps ops = pPolicy.FindOption("ThirdRange");
                if (ops != null)
                {
                    SysMng.Instance.getSession().WindowSettings.SetValue(0, "AgingReportResultsManagementPM", "FilterOptions", "ThirdRange", ops.IsChecked.ToString());
                }
            }
            catch { }

            try
            {
                RepOps ops = pPolicy.FindOption("FourthRange");
                if (ops != null)
                {
                    SysMng.Instance.getSession().WindowSettings.SetValue(0, "AgingReportResultsManagementPM", "FilterOptions", "FourthRange", ops.IsChecked.ToString());
                }
            }
            catch { }

            try
            {
                RepOps ops = pPolicy.FindOption("FifthRange");
                if (ops != null)
                {
                    SysMng.Instance.getSession().WindowSettings.SetValue(0, "AgingReportResultsManagementPM", "FilterOptions", "FifthRange", ops.IsChecked.ToString());
                }
            }
            catch { }

            try
            {
                RepOps ops = pPolicy.FindOption("SixthRange");
                if (ops != null)
                {
                    SysMng.Instance.getSession().WindowSettings.SetValue(0, "AgingReportResultsManagementPM", "FilterOptions", "SixthRange", ops.IsChecked.ToString());
                }
            }
            catch { }

            try
            {
                RepOps ops = pPolicy.FindOption("SeventhRange");
                if (ops != null)
                {
                    SysMng.Instance.getSession().WindowSettings.SetValue(0, "AgingReportResultsManagementPM", "FilterOptions", "SeventhRange", ops.IsChecked.ToString());
                }
            }
            catch { }

            try
            {
                RepOps ops = pPolicy.FindOption("EighthRange");
                if (ops != null)
                {
                    SysMng.Instance.getSession().WindowSettings.SetValue(0, "AgingReportResultsManagementPM", "FilterOptions", "EighthRange", ops.IsChecked.ToString());
                }
            }
            catch { }

            try
            {
                RepOps ops = pPolicy.FindOption("NinthRange");
                if (ops != null)
                {
                    SysMng.Instance.getSession().WindowSettings.SetValue(0, "AgingReportResultsManagementPM", "FilterOptions", "NinthRange", ops.IsChecked.ToString());
                }
            }
            catch { }

            try
            {
                RepOps ops = pPolicy.FindOption("TenthRange");
                if (ops != null)
                {
                    SysMng.Instance.getSession().WindowSettings.SetValue(0, "AgingReportResultsManagementPM", "FilterOptions", "TenthRange", ops.IsChecked.ToString());
                }
            }
            catch { }
        }
    }
}