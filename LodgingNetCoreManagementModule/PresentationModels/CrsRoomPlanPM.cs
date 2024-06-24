using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DevExpress.Xpf.Scheduler;
using DevExpress.XtraScheduler;
using Prism.Ioc;

using Sentez.Common.Commands;
using Sentez.Common.ModuleBase;
using Sentez.Common.PresentationModels;
using Sentez.Common.Report;
using Sentez.Common.SystemServices;
using Sentez.Common.Utilities;
using Sentez.Core.ParameterClasses;
using Sentez.CRSModule;
using Sentez.CRSModule.Services;
using Sentez.CRSUIModule.Views;
using Sentez.Data.BusinessObjects;
using Sentez.Data.Query;
using Sentez.Data.Tools;
using Sentez.Localization;

namespace Sentez.CRSUIModule.PresentationModels
{
    public class CrsRoomPlanPM : PMDesktop
    {
        public LookupList Lists { get; set; }
        DevExpress.Xpf.Scheduler.SchedulerControl schControl;
        DataTable schTable;
        CrsParameters crsParameters;
        private ReportBase pPolicy;

        string _endOfDateString;
        public string EndOfDateString
        {
            get { return _endOfDateString; }
            set { _endOfDateString = value; OnPropertyChanged("EndOfDateString"); }
        }

        private DateTime _currentDate;
        public DateTime CurrentDate
        {
            get
            {
                return _currentDate;
            }
            set
            {
                DateTime eofDate = (DateTime)ActiveSession.ServiceReferences["GetAgileGlobalTodayDateHelperService"].Execute(null);
                try
                {
                    EndOfDateString = string.Format(SLanguage.GetString("Aktif Günsonu Tarihi :{0}"), eofDate.ToLongDateString());
                }
                catch { EndOfDateString = SLanguage.GetString("Gün sonu servisine eriþilemedi!"); }
                if (value != eofDate)
                    EndOfDateString += "     " + string.Format(SLanguage.GetString("RoomRack Tarihi :{0}"), value.ToLongDateString());

                _currentDate = value; OnPropertyChanged("CurrentDate");
            }
        }

        public CrsRoomPlanPM(IContainerExtension container_)
            : base(container_)
        {
            crsParameters = ActiveSession.ParamService.GetParameterClass<CrsParameters>();
        }

        public override void Init()
        {
            base.Init();
            Lists = ActiveSession.LookupList.GetChild(UtilityFunctions.GetConnection(ActiveSession.dbInfo.DBProvider, ActiveSession.dbInfo.ConnectionString));
            SetRoomRackDay();
            schControl = FCtrl("Sch") as DevExpress.Xpf.Scheduler.SchedulerControl;
            if (schControl != null)
            {
                schControl.Start = CurrentDate;
                schControl.EditAppointmentFormShowing += schControl_EditAppointmentFormShowing;
                schControl.InplaceEditorShowing += schControl_InplaceEditorShowing;
                schControl.DeleteRecurrentAppointmentFormShowing += schControl_DeleteRecurrentAppointmentFormShowing;
                schControl.InitNewAppointment += schControl_InitNewAppointment;

                schControl.OptionsCustomization.AllowAppointmentDelete = UsedAppointmentType.None;
                schControl.OptionsCustomization.AllowInplaceEditor = UsedAppointmentType.None;
                schControl.PopupMenuShowing += schControl_PopupMenuShowing;
                schControl.ActiveView.ResourcesPerPage = 10;// crsParameters.ResourcesPerPage;
                FillSchedule();
            }
        }

        public override void LoadCommands()
        {
            base.LoadCommands();
            CmdList.AddCmd(100, "RefreshCommand", SLanguage.GetString("Yenile"), OnRefreshCommand, null);
            CmdList.AddCmd(100, "PreviousPeriodCmd", SLanguage.GetString("Önceki Dönem"), OnPreviousPeriodCommand, null);
            CmdList.AddCmd(100, "PreviousDayCmd", SLanguage.GetString("Önceki Gün"), OnPreviousDayCommand, null);
            CmdList.AddCmd(100, "NextDayCmd", SLanguage.GetString("Sonraki Gün"), OnNextDayCommand, null);
            CmdList.AddCmd(100, "NextPeriodCmd", SLanguage.GetString("Sonraki Dönem"), OnNextPeriodCommand, null);
            CmdList.AddCmd(100, "GotoToDayCmd", SLanguage.GetString("Bugün"), OnGotoToDayCommand, null);
            CmdList.AddCmd(303, "GoToDayCommand", SLanguage.GetString("Tarihe Git"), OnGoToDayCommand, CanGoToDayCommand);
            CmdList.AddCmd(303, "RoomPlanOperatinCmd", SLanguage.GetString("Ýþlemler"), OnRoomPlanOperatinCommand, null);
        }

        void SetRoomRackDay()
        {
            if (ActiveSession.ServiceReferences.ContainsKey("GetAgileGlobalTodayDateHelperService"))
            {
                CurrentDate = GetRoomRackDay();
            }
        }

        DateTime GetRoomRackDay()
        {
            DateTime dt = new DateTime();

            if (ActiveSession.ServiceReferences.ContainsKey("GetAgileGlobalTodayDateHelperService"))
            {
                var getAgileGlobalTodayDateHelperService = ActiveSession.ServiceReferences["GetAgileGlobalTodayDateHelperService"] as GetAgileGlobalTodayDateHelperService;
                if (getAgileGlobalTodayDateHelperService != null) dt = getAgileGlobalTodayDateHelperService.GetToday();
            }

            return dt;
        }

        public void OnPreviousPeriodCommand(ISysCommandParam obj)
        {
            if (schControl == null) return;
            schControl.Start = schControl.Start.AddDays(-10);
            //FillSchedule();
        }

        public void OnPreviousDayCommand(ISysCommandParam obj)
        {
            if (schControl == null) return;
            schControl.Start = schControl.Start.AddDays(-1);
            //FillSchedule();
        }

        public void OnNextPeriodCommand(ISysCommandParam obj)
        {
            if (schControl == null) return;
            schControl.Start = schControl.Start.AddDays(10);
            //FillSchedule();
        }

        public void OnNextDayCommand(ISysCommandParam obj)
        {
            if (schControl == null) return;
            schControl.Start = schControl.Start.AddDays(1);
            //FillSchedule();
        }

        public void OnGotoToDayCommand(ISysCommandParam obj)
        {
            if (schControl == null) return;
            schControl.Start = CurrentDate;
            //FillSchedule();
        }

        public void OnRefreshCommand(ISysCommandParam obj)
        {
            if (schControl == null) return;
            schControl.Start = CurrentDate;
            FillSchedule();
        }

        void schControl_EditAppointmentFormShowing(object sender, EditAppointmentFormEventArgs e)
        {
            long bookingItemId;
            long.TryParse(e.Appointment.Id?.ToString(), out bookingItemId);
            InitBookingOrCheckIn(bookingItemId, e.Appointment.Start);
            e.Cancel = true;
        }

        private void InitBookingOrCheckIn(long bookingItemId, DateTime startDate, ISysCommandParam obj = null)
        {
            try
            {
                if (bookingItemId == 0L)
                {
                    string itemCodeStr = obj?.itemCode;
                    BoParam boparam2 = new BoParam
                    {
                        ValRefs = { ["Type"] = GetActiveType },
                        LogicalModuleId = (short)Modules.CRSModule
                    };

                    PmParam pmparam = new PmParam("CrsBookingPM", "BOCardContext");
                    pmparam.TagStr = "RoomPlan";
                    pmparam.Tag2 = startDate;
                    pmparam.Tag3 = schControl.SelectedInterval.Duration.Days;
                    Dictionary<string, object> keyValues = new Dictionary<string, object>();
                    if (string.IsNullOrEmpty(itemCodeStr))
                    {
                        if (schControl.SelectedInterval.Start == CurrentDate)
                            itemCodeStr = "NewCheckIn";
                        else if (schControl.SelectedInterval.Start > CurrentDate)
                            itemCodeStr = "NewReservation";
                    }
                    if (itemCodeStr == "NewCheckIn" || itemCodeStr == "NewReservation")
                    {
                        using (DataTable table = UtilityFunctions.GetDataTableList(ActiveSession.dbInfo.DBProvider, ActiveSession.dbInfo.GetNewConnection(), null, "Erp_Resource", $"select ParentResourceId from Erp_Resource with (nolock) where RecId={schControl.SelectedResource.Id}"))
                        {
                            if (table?.Rows.Count > 0)
                            {
                                if (itemCodeStr == "NewCheckIn")
                                    pmparam.Name = "WalkIn-CheckIn";
                                keyValues.Add("ResourceId", schControl.SelectedResource.Id);
                                keyValues.Add("ParentResourceId", Convert.ToInt32(table.Rows[0][0]));
                            }
                        }
                    }
                    pmparam.Tag = keyValues;

                    SysCommandParam obj1 = new SysCommandParam("CrsBooking", "CrsBookingPM", pmparam, "CrsBookingBO", boparam2, "", "")
                    {
                        logicalModuleID = (short)Modules.CRSModule,
                        moduleID = (short)Modules.CRSModule,
                        secID = 0,// (short)CRSSecurityItems.Booking,
                        subsecID = 0,//(short)CRSSecuritySubItems.None,
                        isModal = true,
                        Tag2Obj = keyValues,
                        Tag = "RoomPlan"
                    };
                    ActiveSession.sysMng.SysCmdList["BookingEntranceCommand"].Execute(obj1);
                    keyValues.Clear();
                    keyValues = null;
                }
                else
                {
                    BoParam boparam = new BoParam();
                    boparam.ValRefs["Type"] = GetActiveType;
                    boparam.ActiveRecordId = Convert.ToInt32(bookingItemId);
                    boparam.LogicalModuleId = (short)Modules.CRSModule;

                    PmParam pmparam = new PmParam("CrsBooking", "BOCardContext");
                    pmparam.Name = "CrsBooking";

                    pPolicy = _container.Resolve<IReport>("Crs_BookingBookingNoList") as ReportBase;

                    SysCommandParam obj1 = new SysCommandParam("CrsBooking", "CrsBookingPM", pmparam, "CrsBookingBO", boparam, "", "RecId")
                    {
                        logicalModuleID = (short)Modules.CRSModule,
                        moduleID = (short)Modules.CRSModule,
                        secID = 0,//(short)CRSSecurityItems.Booking,
                        subsecID =0,// (short)CRSSecuritySubItems.None,
                        isModal = true,
                        TagObj = pPolicy,
                        Tag = "RoomPlan",
                        Tag2Obj = bookingItemId
                    };
                    ActiveSession.sysMng.SysCmdList["BookingEntranceCommand"].Execute(obj1);
                }
            }
            catch
            {

            }
            finally
            {
                OnRefreshCommand(null);
            }
        }

        void schControl_InplaceEditorShowing(object sender, DevExpress.Xpf.Scheduler.InplaceEditorEventArgs e)
        {
            //
        }
        void schControl_DeleteRecurrentAppointmentFormShowing(object sender, DevExpress.Xpf.Scheduler.DeleteRecurrentAppointmentFormEventArgs e)
        {
            e.Cancel = true;
        }

        void schControl_InitNewAppointment(object sender, AppointmentEventArgs e)
        {
            //
        }

        void schControl_PopupMenuShowing(object sender, SchedulerMenuEventArgs e)
        {
            e.Handled = true;
            //if (e.Menu.Name == SchedulerMenuItemName.DefaultMenu)
            //{
            //    e.Customizations.Add(new RemoveBarItemAndLinkAction() { ItemName = SchedulerMenuItemName.NewAllDayEvent });
            //}

            //if (e.Menu.Name == SchedulerMenuItemName.AppointmentMenu)
            //{
            //    e.Customizations.Add(new RemoveBarItemAndLinkAction() { ItemName = SchedulerMenuItemName.DeleteAppointment });
            //}
        }

        public override void _view_Loaded(object sender, RoutedEventArgs e)
        {
            base._view_Loaded(sender, e);
            //if (ActiveView?._view != null)
            //{
            //    var userControl = ActiveView._view as UserControl;
            //    FocusManager.SetIsFocusScope(userControl, true);
            //    var window = userControl?.Parent as Window;
            //    if (window != null)
            //    {
            //        userControl.Height = window.ActualHeight;
            //        userControl.Width = window.ActualWidth;
            //    }
            //}
        }


        void FillSchedule()
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine("select CrsBI.RecId CrsBookingItemId");
            sb.AppendLine(", eca.RecId CurrentAccountId");
            sb.AppendLine(", eca.CurrentAccountCode");
            sb.AppendLine(", eca.CurrentAccountName");
            sb.AppendLine(", eca.ForegroundColor");
            sb.AppendLine(", eca.BackgroundColor");
            sb.AppendLine(", CASE WHEN (SELECT CrsG.GuestName + ' ' + CrsG.GuestLastname FROM Crs_Guest CrsG WITH (NOLOCK) WHERE CrsG.RecId IN((SELECT top 1 CrsBIG.GuestId FROM Crs_BookingItemGuest CrsBIG WITH (NOLOCK) WHERE CrsBIG.BookingItemId=CrsBI.RecId))) IS NOT NULL THEN (SELECT CrsG.GuestName + ' ' + CrsG.GuestLastname FROM Crs_Guest CrsG WITH (NOLOCK) WHERE CrsG.RecId IN((SELECT top 1 CrsBIG.GuestId FROM Crs_BookingItemGuest CrsBIG WITH (NOLOCK) WHERE CrsBIG.BookingItemId=CrsBI.RecId))) ELSE (SELECT top 1 CrsBIG.GuestName + ' ' + CrsBIG.GuestLastname FROM Crs_BookingItemGuest CrsBIG WITH (NOLOCK) WHERE CrsBIG.BookingItemId=CrsBI.RecId) END Subject");
            sb.AppendLine(", 'Açýklama' Description");
            sb.AppendLine(", CrsBI.ArrivalDate");
            sb.AppendLine(", CrsBI.DepartureDate");
            sb.AppendLine(", CrsBI.ArrivalTime");
            sb.AppendLine(", CrsBI.DepartureTime");
            sb.AppendLine(",DATEADD(MINUTE,datepart(MINUTE,CrsBI.ArrivalTime), DATEADD(HOUR,datepart(HOUR,CrsBI.ArrivalTime),CrsBI.ArrivalDate)) StartDate");
            sb.AppendLine(",DATEADD(MINUTE,datepart(MINUTE,CrsBI.DepartureTime), DATEADD(HOUR,datepart(HOUR,CrsBI.DepartureTime),CrsBI.DepartureDate)) EndDate");
            sb.AppendLine(", CASE");
            sb.AppendFormat("WHEN ISNULL(CrsBI.IsCheckedIn, 0) = 1 AND ISNULL(CrsBI.IsCheckedOut,0)= 0 THEN '{0}'", SLanguage.GetString("Dolu")).AppendLine();
            sb.AppendFormat("WHEN ISNULL(CrsBI.IsCheckedIn,0)= 1 AND ISNULL(CrsBI.IsCheckedOut,0)= 1 THEN '{0}'", SLanguage.GetString("Check-Out")).AppendLine();
            sb.AppendFormat("WHEN ISNULL(CrsBI.IsCheckedIn,0)= 0 AND ISNULL(CrsBI.IsCheckedOut,0)= 0 THEN '{0}'", SLanguage.GetString("Rezerve")).AppendLine();
            sb.AppendLine("ELSE '' END Status");
            sb.AppendLine(", (SELECT ErpR.ResourceCode FROM Erp_Resource ErpR WITH(NOLOCK) WHERE ErpR.RecId IN((SELECT CrsBIP.ResourceId FROM Crs_BookingItemPlan CrsBIP WITH (NOLOCK) WHERE CrsBIP.BookingItemId= CrsBI.RecId AND CrsBIP.PlannedDate IS NULL AND CrsBIP.PlanType= 0))) Resource");
            sb.AppendLine(", (SELECT CrsBIP.ResourceId FROM Crs_BookingItemPlan CrsBIP WITH(NOLOCK) WHERE CrsBIP.BookingItemId = CrsBI.RecId AND CrsBIP.PlannedDate IS NULL AND CrsBIP.PlanType = 0) ResourceId");
            sb.AppendLine("from Crs_BookingItem CrsBI with(nolock)");
            sb.AppendLine("LEFT JOIN Erp_CurrentAccount eca WITH(NOLOCK) ON CrsBI.AgentId = eca.RecId");

            if (ActiveSession?.ActiveCompany != null)
            {
                sb.AppendFormat("where ISNULL(CrsBI.IsDeleted,0)=0 AND ISNULL(CrsBI.IsCancelled,0)=0 AND CrsBI.CompanyId={0}", ActiveSession.ActiveCompany.RecId.Value).AppendLine();
                sb.AppendLine(" And (SELECT CrsBIP.ResourceId FROM Crs_BookingItemPlan CrsBIP WITH(NOLOCK) WHERE CrsBIP.BookingItemId = CrsBI.RecId AND CrsBIP.PlannedDate IS NULL AND CrsBIP.PlanType = 0) IS NOT NULL");
                schTable = UtilityFunctions.GetDataTableList(ActiveSession.dbInfo.DBProvider, ActiveSession.dbInfo.Connection, null, "Table", sb.ToString());

                schControl.Storage.AppointmentStorage.DataSource = schTable.DefaultView;
                schControl.Storage.ResourceStorage.DataSource = UtilityFunctions.GetDataTableList(ActiveSession.dbInfo.DBProvider, ActiveSession.dbInfo.Connection, null, "Erp_Resource", string.Format("select RecId, case when isnull(Explanation,'') = '' then ResourceCode else Explanation end Resource from Erp_Resource with (nolock) where CompanyId={1} and ResourceType={2}", SLanguage.GetString("Oda Belirsiz"), ActiveSession.ActiveCompany.RecId.Value.ToString(), (short)ResourceTypeDefinition.ResourceType.Room));
            }


            schControl.Storage.AppointmentStorage.Labels.Clear();
            System.Drawing.Color color; color = System.Drawing.Color.FromArgb(crsParameters.OccupiedReady);
            schControl.Storage.AppointmentStorage.Labels.Add(new DevExpress.Xpf.Scheduler.AppointmentLabel() { Id = SLanguage.GetString("Dolu"), DisplayName = SLanguage.GetString("Dolu"), MenuCaption = SLanguage.GetString("Dolu"), Color = System.Windows.Media.Color.FromRgb(color.R, color.G, color.B) });
            color = System.Drawing.Color.FromArgb(crsParameters.CheckOutRoom);
            schControl.Storage.AppointmentStorage.Labels.Add(new DevExpress.Xpf.Scheduler.AppointmentLabel() { Id = SLanguage.GetString("Çýkýþ"), DisplayName = SLanguage.GetString("Çýkýþ"), MenuCaption = SLanguage.GetString("Çýkýþ"), Color = System.Windows.Media.Color.FromRgb(color.R, color.G, color.B) });
            color = System.Drawing.Color.FromArgb(crsParameters.ReservedRoom);
            schControl.Storage.AppointmentStorage.Labels.Add(new DevExpress.Xpf.Scheduler.AppointmentLabel() { Id = SLanguage.GetString("Rezerve"), DisplayName = SLanguage.GetString("Rezerve"), MenuCaption = SLanguage.GetString("Rezerve"), Color = System.Windows.Media.Color.FromRgb(color.R, color.G, color.B) });
            color = System.Drawing.Color.FromArgb(crsParameters.CheckOutRoom);
            schControl.Storage.AppointmentStorage.Labels.Add(new DevExpress.Xpf.Scheduler.AppointmentLabel() { Id = SLanguage.GetString("Check-Out"), DisplayName = SLanguage.GetString("Check-Out"), MenuCaption = SLanguage.GetString("Check-Out"), Color = System.Windows.Media.Color.FromRgb(color.R, color.G, color.B) });

            if (crsParameters.UseAgentColors == 1)
            {
                List<long> agentIdList = new List<long>();
                foreach (DataRow row in schTable.Select("CurrentAccountId is not null", "", DataViewRowState.CurrentRows))
                {
                    long agentId;
                    long.TryParse(row["CurrentAccountId"].ToString(), out agentId);
                    if (agentIdList.Contains(agentId))
                        continue;
                    agentIdList.Add(agentId);
                    int agentBackgroundColor;
                    int.TryParse(row["BackgroundColor"].ToString(), out agentBackgroundColor);
                    if (agentBackgroundColor != 0)
                    {
                        color = System.Drawing.Color.FromArgb(agentBackgroundColor);
                        schControl.Storage.AppointmentStorage.Labels.Add(new DevExpress.Xpf.Scheduler.AppointmentLabel() { Id = row["CurrentAccountCode"].ToString(), DisplayName = row["CurrentAccountName"].ToString(), MenuCaption = row["CurrentAccountName"].ToString(), Color = System.Windows.Media.Color.FromRgb(color.R, color.G, color.B) });
                    }
                }
                agentIdList.Clear();
            }

            if (schTable != null && schTable.Rows.Count > 0 && schControl.Storage.AppointmentStorage.Count > 0)
            {
                int count = schControl.Storage.AppointmentStorage.Count;
                for (int i = 0; i < count; i++)
                {
                    schControl.Storage.AppointmentStorage[i].LabelKey = schTable.Rows[i]["Status"].ToString();
                    schControl.Storage.AppointmentStorage[i].Subject += string.Format("\n{0} :{1}\n{2} :{3}", SLanguage.GetString("Geliþ"), schControl.Storage.AppointmentStorage[i].Start.ToString("dd.MM.yyyy hh:mm"), SLanguage.GetString("Ayrýlýþ"), schControl.Storage.AppointmentStorage[i].End.ToString("dd.MM.yyyy hh:mm"));
                    DataRow[] bookingItemRows = schTable.Select($"CrsBookingItemId={schControl.Storage.AppointmentStorage[i].Id}");
                    if (bookingItemRows?.Length > 0)
                    {
                        if (!bookingItemRows[0].IsNull("CurrentAccountId"))
                        {
                            schControl.Storage.AppointmentStorage[i].Subject += string.Format("\n{0} :{1}", SLanguage.GetString("Acente"), bookingItemRows[0]["CurrentAccountName"]);
                            if (crsParameters.UseAgentColors == 1)
                                schControl.Storage.AppointmentStorage[i].LabelKey = bookingItemRows[0]["CurrentAccountCode"].ToString();
                        }
                    }
                    //DateTime startDate, endDate;
                    //DateTime.TryParse(schTable.Rows[i]["StartDate"].ToString(), out startDate);
                    //DateTime.TryParse(schTable.Rows[i]["EndDate"].ToString(), out endDate);
                    //schControl.Storage.AppointmentStorage[i].Start = startDate;
                    //schControl.Storage.AppointmentStorage[i].End = endDate;
                    //Appointment appo = schControl.Storage.AppointmentStorage[i];
                    //if (schTable.Rows[i]["Status"].ToString() == SLanguage.GetString("Dolu")) appo.LabelKey = schControl.Storage.AppointmentStorage.Labels[0];
                    //else if (schTable.Rows[i]["Status"].ToString() == SLanguage.GetString("Rezerve")) appo.LabelKey = 6;
                    //else if (schTable.Rows[i]["Status"].ToString() == SLanguage.GetString("Çýkýþ")) appo.LabelKey = 7;
                    //else if (schTable.Rows[i]["Status"].ToString() == SLanguage.GetString("Baþladý")) appo.LabelKey = 8;
                    //else if (schTable.Rows[i]["Status"].ToString() == SLanguage.GetString("Durduruldu")) appo.LabelKey = 9;
                    //else if (schTable.Rows[i]["Status"].ToString() == SLanguage.GetString("Ýptal Edildi")) appo.LabelKey = 10;
                }
            }
        }

        public object GetActiveType0(string propname, object propval)
        {
            return "0";
        }

        public object GetActiveType(string propname, object propval)
        {
            return "1";
        }

        public override void OnCloseCommand(ISysCommandParam obj)
        {
            base.OnCloseCommand(obj);
        }

        public void OnGoToDayCommand(ISysCommandParam obj)
        {
            if (schControl == null) return;
            CrsRoomRackGotoDate cRoomRackGotoDate = new CrsRoomRackGotoDate(container);
            SysMng.Instance.ActWndMng.ShowWnd(cRoomRackGotoDate, true, SLanguage.GetString("Tarihe Git"), Common.InformationMessages.WindowStyle.ToolWindow);
            CurrentDate = cRoomRackGotoDate.RoomRackDate;
            if (cRoomRackGotoDate.IsDateOk)
            {
                schControl.Start = CurrentDate;
                FillSchedule();
            }
        }

        public bool CanGoToDayCommand(ISysCommandParam arg)
        {
            return true;
        }

        public void OnRoomPlanOperatinCommand(ISysCommandParam obj)
        {
            if (schControl == null || obj == null) return;
            if (obj.itemCode == "NewCheckIn")
            {
                if (schControl.SelectedInterval.Start != CurrentDate)
                {
                    sysMng.ActWndMng.ShowMsg(SLanguage.GetString("Sadece aktif gün sonu tarihi için Check-In iþlemi yapabilirsiniz!"), ConstantStr.Warning, Common.InformationMessages.MessageBoxButton.OK, Common.InformationMessages.MessageBoxImage.Warning);
                    return;
                }
                InitBookingOrCheckIn(0, CurrentDate, obj);
            }
            else if (obj.itemCode == "NewReservation")
            {
                if (schControl.SelectedInterval.Start < CurrentDate)
                {
                    sysMng.ActWndMng.ShowMsg(SLanguage.GetString("Günsonu tarihinden öncesine Rezervasyon iþlemi yapamazsýnýz!"), ConstantStr.Warning, Common.InformationMessages.MessageBoxButton.OK, Common.InformationMessages.MessageBoxImage.Warning);
                    return;
                }
                InitBookingOrCheckIn(0, schControl.SelectedInterval.Start, obj);
            }
            else if (obj.itemCode == "MainFolio")
            {
                if (schControl.SelectedAppointments.Count > 0)
                {
                    long bookingItemId;
                    long.TryParse(schControl.SelectedAppointments[0].Id.ToString(), out bookingItemId);
                    if (bookingItemId == 0)
                    {
                        sysMng.ActWndMng.ShowMsg(SLanguage.GetString("Lütfen Check-In veya Rezervasyon kaydý seçiniz!"), ConstantStr.Warning, Common.InformationMessages.MessageBoxButton.OK, Common.InformationMessages.MessageBoxImage.Warning);
                        return;
                    }
                    else
                    {
                        IBusinessObject crsBookingItemBo = container.Resolve<IBusinessObject>("CrsBookingItemBO");
                        try
                        {
                            if (crsBookingItemBo?.Get(bookingItemId) > 0)
                            {
                                if (crsBookingItemBo.Data.Tables["Crs_BookingItem"].Rows.Count > 0)
                                {
                                    int accountId;
                                    int.TryParse(crsBookingItemBo.Data.Tables["Crs_BookingItem"].Rows[0]["AccountId"].ToString(), out accountId);
                                    if (accountId > 0)
                                    {
                                        PmParam pmparam = new PmParam() { itemID = accountId, TagStr = "AllAccountsExtreCard", Name = "CrsAccountExtreScreen", Tag3 = null };
                                        obj = new SysCommandParam { PmParamObj = pmparam };
                                        SysMng.GetCmd("AccountInfoCommand").Execute(obj);
                                    }
                                    else if (accountId == 0)
                                    {
                                        sysMng.ActWndMng.ShowMsg(SLanguage.GetString("Check-In veya Rezervasyon kaydýna ait folyo tanýmlanmamýþ!"), ConstantStr.Warning, Common.InformationMessages.MessageBoxButton.OK, Common.InformationMessages.MessageBoxImage.Warning);
                                        return;
                                    }
                                }
                            }
                        }
                        finally
                        {
                            crsBookingItemBo?.Dispose();
                        }
                    }
                }
                else
                {
                    sysMng.ActWndMng.ShowMsg(SLanguage.GetString("Lütfen Check-In veya Rezervasyon kaydý seçiniz!"), ConstantStr.Warning, Common.InformationMessages.MessageBoxButton.OK, Common.InformationMessages.MessageBoxImage.Warning);
                    return;
                }
            }
            #region ExtraFolio
            else if (obj.itemCode == "ExtraFolio")
            {
                if (schControl.SelectedAppointments.Count > 0)
                {
                    long bookingId;
                    long.TryParse(schControl.SelectedAppointments[0].Id.ToString(), out bookingId);
                    if (bookingId == 0)
                    {
                        sysMng.ActWndMng.ShowMsg(SLanguage.GetString("Lütfen Check-In veya Rezervasyon kaydý seçiniz!"), ConstantStr.Warning, Common.InformationMessages.MessageBoxButton.OK, Common.InformationMessages.MessageBoxImage.Warning);
                        return;
                    }
                    else
                    {
                        IBusinessObject crsBookingBo = container.Resolve<IBusinessObject>("CrsBookingBO");
                        try
                        {
                            if (crsBookingBo?.Get(bookingId) > 0)
                            {
                                if (crsBookingBo.Data.Tables["Crs_BookingItemGuest"].Rows.Count > 0)
                                {
                                    int accountId;
                                    int.TryParse(crsBookingBo.Data.Tables["Crs_BookingItemGuest"].Rows[0]["AccountId"].ToString(), out accountId);
                                    if (accountId > 0)
                                    {
                                        PmParam pmparam = new PmParam() { itemID = accountId, TagStr = "MainAccountExtreCard", Name = "CrsAccountExtreScreen", Tag3 = null };
                                        obj = new SysCommandParam { PmParamObj = pmparam };
                                        SysMng.GetCmd("AccountInfoCommand").Execute(obj);
                                    }
                                    else if (accountId == 0)
                                    {
                                        sysMng.ActWndMng.ShowMsg(SLanguage.GetString("Check-In veya Rezervasyon kaydýna ait ekstra folyo tanýmlanmamýþ!"), ConstantStr.Warning, Common.InformationMessages.MessageBoxButton.OK, Common.InformationMessages.MessageBoxImage.Warning);
                                        return;
                                    }
                                }
                            }
                        }
                        finally
                        {
                            crsBookingBo?.Dispose();
                        }
                    }
                }
                else
                {
                    sysMng.ActWndMng.ShowMsg(SLanguage.GetString("Lütfen Check-In veya Rezervasyon kaydý seçiniz!"), ConstantStr.Warning, Common.InformationMessages.MessageBoxButton.OK, Common.InformationMessages.MessageBoxImage.Warning);
                    return;
                }
            }
            #endregion
        }

        public override void Dispose()
        {
            if (disposed)
                return;
            Lists?.Dispose();

            if (schControl != null)
            {
                schControl.Start = CurrentDate;
                schControl.EditAppointmentFormShowing -= schControl_EditAppointmentFormShowing;
                schControl.InplaceEditorShowing -= schControl_InplaceEditorShowing;
                schControl.DeleteRecurrentAppointmentFormShowing -= schControl_DeleteRecurrentAppointmentFormShowing;
                schControl.InitNewAppointment -= schControl_InitNewAppointment;
                schControl.PopupMenuShowing -= schControl_PopupMenuShowing;
            }

            base.Dispose();
        }
    }
}

