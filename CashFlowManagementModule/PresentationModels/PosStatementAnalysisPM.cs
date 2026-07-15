using System;
using System.Data;
using System.Windows;

using CashFlowManagementModule.Services;

using DevExpress.Xpf.Grid;

using LiveCore.Desktop.UI.Controls;

using Prism.Ioc;

using Sentez.Common.Commands;
using Sentez.Common.InformationMessages;
using Sentez.Common.PresentationModels;
using Sentez.Common.SystemServices;
using Sentez.Localization;

namespace CashFlowManagementModule.PresentationModels
{
    public class PosStatementAnalysisPM : PMDesktop
    {
        DateTime _referenceDate = DateTime.Today;
        DataTable _accountData;
        DataTable _dailyData;
        DataTable _deductionData;
        DataTable _settlementData;
        DataTable _futureReceivableData;
        DataTable _summaryData;
        string _reconciliationMessage;
        bool _accountGridHooked;

        public DateTime ReferenceDate
        {
            get => _referenceDate;
            set
            {
                _referenceDate = value.Date;
                OnPropertyChanged(nameof(ReferenceDate));
            }
        }

        public DataTable AccountData
        {
            get => _accountData;
            set
            {
                _accountData = value;
                OnPropertyChanged(nameof(AccountData));
                OnPropertyChanged(nameof(AccountDataView));
            }
        }

        public DataView AccountDataView => _accountData?.DefaultView;

        public DataTable DailyData
        {
            get => _dailyData;
            set
            {
                _dailyData = value;
                OnPropertyChanged(nameof(DailyData));
                OnPropertyChanged(nameof(DailyDataView));
            }
        }

        public DataView DailyDataView => _dailyData?.DefaultView;

        public DataTable DeductionData
        {
            get => _deductionData;
            set
            {
                _deductionData = value;
                OnPropertyChanged(nameof(DeductionData));
                OnPropertyChanged(nameof(DeductionDataView));
            }
        }

        public DataView DeductionDataView => _deductionData?.DefaultView;

        public DataTable SettlementData
        {
            get => _settlementData;
            set
            {
                _settlementData = value;
                OnPropertyChanged(nameof(SettlementData));
                OnPropertyChanged(nameof(SettlementDataView));
            }
        }

        public DataView SettlementDataView => _settlementData?.DefaultView;

        public DataTable FutureReceivableData
        {
            get => _futureReceivableData;
            set
            {
                _futureReceivableData = value;
                OnPropertyChanged(nameof(FutureReceivableData));
                OnPropertyChanged(nameof(FutureReceivableDataView));
            }
        }

        public DataView FutureReceivableDataView => _futureReceivableData?.DefaultView;

        public DataTable SummaryData
        {
            get => _summaryData;
            set
            {
                _summaryData = value;
                OnPropertyChanged(nameof(SummaryData));
                OnPropertyChanged(nameof(SummaryDataView));
            }
        }

        public DataView SummaryDataView => _summaryData?.DefaultView;

        public string ReconciliationMessage
        {
            get => _reconciliationMessage;
            set
            {
                _reconciliationMessage = value;
                OnPropertyChanged(nameof(ReconciliationMessage));
            }
        }

        LiveGridControl AccountGrid => FCtrl<LiveGridControl>("gridPosAccountList");
        LiveGridControl DailyGrid => FCtrl<LiveGridControl>("gridPosDaily");
        LiveGridControl DeductionGrid => FCtrl<LiveGridControl>("gridPosDeduction");
        LiveGridControl SettlementGrid => FCtrl<LiveGridControl>("gridPosSettlement");
        LiveGridControl FutureReceivableGrid => FCtrl<LiveGridControl>("gridPosFutureReceivable");
        LiveGridControl SummaryGrid => FCtrl<LiveGridControl>("gridPosSummary");
        LiveDockLayoutManager DockLayoutManager => FCtrl<LiveDockLayoutManager>("posStatementDockLayoutManager");

        public PosStatementAnalysisPM(IContainerExtension container) : base(container)
        {
        }

        public override void LoadCommands()
        {
            base.LoadCommands();
            CmdList.AddCmd(300, "RefreshPosStatementAnalysisCommand", SLanguage.GetString("Yenile"), OnRefreshCommand, null);
            CmdList.AddCmd(301, "RecalculatePosSnapshotCommand", SLanguage.GetString("Özet Kaydet"), OnRecalculateSnapshotCommand, null);
            CmdList.AddCmd(302, "BackfillPosSnapshotsCommand", SLanguage.GetString("Toplu Özet Kaydet"), OnBackfillSnapshotsCommand, null);
        }

        void OnRefreshCommand(ISysCommandParam param)
        {
            RefreshAnalysis();
        }

        void OnRecalculateSnapshotCommand(ISysCommandParam param)
        {
            LiveSession session = SysMng.getSession() as LiveSession;
            if (session == null)
                return;

            if (AccountGrid?.View?.DataControl?.CurrentItem is not DataRowView selectedRow)
            {
                SysMng.ActWndMng.ShowMsg(SLanguage.GetString("Lütfen bir Pos hesabı seçiniz."), ConstantStr.Warning);
                return;
            }

            if (selectedRow.Row.IsNull("BankAccountId") || selectedRow.Row.IsNull("PeriodYear") || selectedRow.Row.IsNull("PeriodMonth"))
                return;

            long bankAccountId = Convert.ToInt64(selectedRow.Row["BankAccountId"]);
            int periodYear = Convert.ToInt32(selectedRow.Row["PeriodYear"]);
            int periodMonth = Convert.ToInt32(selectedRow.Row["PeriodMonth"]);
            int userId = (int)(session.ActiveUser?.RecId ?? 0);
            PosSnapshotRefreshService.RefreshPeriodSnapshot(session, bankAccountId, periodYear, periodMonth, userId);
            SysMng.ActWndMng.ShowMsg(SLanguage.GetString("Pos ekstre özeti kaydedildi."), ConstantStr.Information);
        }

        void OnBackfillSnapshotsCommand(ISysCommandParam param)
        {
            LiveSession session = SysMng.getSession() as LiveSession;
            if (session == null)
                return;

            if (SysMng.ActWndMng.ShowMsgYesNo(
                    SLanguage.GetString("Referans tarihinden geriye 12 ay için tüm Pos hesaplarının ekstre özetleri yeniden hesaplanıp kaydedilecektir. Devam etmek istiyor musunuz?"),
                    ConstantStr.Warning) != Sentez.Common.InformationMessages.MessageBoxResult.Yes)
                return;

            DateTime endDate = ReferenceDate.Date;
            DateTime startDate = endDate.AddMonths(-11);
            int userId = (int)(session.ActiveUser?.RecId ?? 0);
            PosSnapshotBackfillResult backfillResult = PosSnapshotRefreshService.BackfillPeriodRange(
                session,
                startDate,
                endDate,
                userId);

            SysMng.ActWndMng.ShowMsg(backfillResult.Message, ConstantStr.Information);
        }

        public override void _view_Loaded(object sender, RoutedEventArgs e)
        {
            base._view_Loaded(sender, e);
            PmTitle = SLanguage.GetString("Pos Ekstre Analizi");
            DockLayoutManager?.RestoreLayout();
            AccountGrid?.Restore_Layout();
            DailyGrid?.Restore_Layout();
            DeductionGrid?.Restore_Layout();
            SettlementGrid?.Restore_Layout();
            FutureReceivableGrid?.Restore_Layout();
            SummaryGrid?.Restore_Layout();
            EnsureAccountGridSelectionHook();
            RefreshAnalysis();
        }

        public override void Dispose()
        {
            if (AccountGrid?.View?.DataControl != null)
                AccountGrid.View.DataControl.CurrentItemChanged -= AccountGrid_CurrentItemChanged;

            base.Dispose();
        }

        void EnsureAccountGridSelectionHook()
        {
            if (_accountGridHooked || AccountGrid?.View?.DataControl == null)
                return;

            AccountGrid.View.DataControl.CurrentItemChanged += AccountGrid_CurrentItemChanged;
            _accountGridHooked = true;
        }

        void AccountGrid_CurrentItemChanged(object sender, CurrentItemChangedEventArgs e)
        {
            LoadSelectedAccountDetails();
        }

        void RefreshAnalysis()
        {
            LiveSession session = SysMng.getSession() as LiveSession;
            AccountData = PosStatementAnalysisService.BuildAccountListTable(session, ReferenceDate);
            AccountGrid?.RefreshData();
            EnsureAccountGridSelectionHook();
            LoadSelectedAccountDetails();
        }

        void LoadSelectedAccountDetails()
        {
            if (AccountGrid?.View?.DataControl?.CurrentItem is not DataRowView selectedRow
                || selectedRow.Row.IsNull("BankAccountId")
                || selectedRow.Row.IsNull("PeriodYear")
                || selectedRow.Row.IsNull("PeriodMonth"))
            {
                DailyData = PosStatementAnalysisService.BuildDailyTable(null);
                DeductionData = PosStatementAnalysisService.BuildDeductionTable(null);
                SettlementData = PosStatementAnalysisService.BuildSettlementTable(null);
                FutureReceivableData = PosStatementAnalysisService.BuildFutureReceivableTable(null);
                SummaryData = PosStatementAnalysisService.BuildSummaryTable(null);
                ReconciliationMessage = string.Empty;
                RefreshDetailGrids();
                return;
            }

            LiveSession session = SysMng.getSession() as LiveSession;
            long bankAccountId = Convert.ToInt64(selectedRow.Row["BankAccountId"]);
            int periodYear = Convert.ToInt32(selectedRow.Row["PeriodYear"]);
            int periodMonth = Convert.ToInt32(selectedRow.Row["PeriodMonth"]);

            PosMerchantAggregationResult aggregation = PosStatementAnalysisService.BuildSelectedAggregation(
                session,
                bankAccountId,
                periodYear,
                periodMonth);

            DailyData = PosStatementAnalysisService.BuildDailyTable(aggregation);
            DeductionData = PosStatementAnalysisService.BuildDeductionTable(aggregation);
            SettlementData = PosStatementAnalysisService.BuildSettlementTable(aggregation);
            FutureReceivableData = PosStatementAnalysisService.BuildFutureReceivableTable(aggregation);
            SummaryData = PosStatementAnalysisService.BuildSummaryTable(aggregation);

            PosMerchantReconciliationResult reconciliation =
                PosMerchantReconciliationService.ReconcilePeriod(session, bankAccountId, periodYear, periodMonth);
            ReconciliationMessage = reconciliation.Message;

            RefreshDetailGrids();
        }

        void RefreshDetailGrids()
        {
            DailyGrid?.RefreshData();
            DeductionGrid?.RefreshData();
            SettlementGrid?.RefreshData();
            FutureReceivableGrid?.RefreshData();
            SummaryGrid?.RefreshData();
        }
    }
}
