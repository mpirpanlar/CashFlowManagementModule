using System;
using System.Data;
using System.Windows;

using CashFlowManagementModule.Services;

using DevExpress.Xpf.Grid;

using LiveCore.Desktop.UI.Controls;

using Prism.Ioc;

using Sentez.Common.Commands;
using Sentez.Common.PresentationModels;
using Sentez.Common.SystemServices;
using Sentez.Localization;

namespace CashFlowManagementModule.PresentationModels
{
    public class CreditCardDetailAnalysisPM : PMDesktop
    {
        DataTable _analysisData;
        DataTable _movementData;
        bool _analysisGridHooked;

        public DataTable AnalysisData
        {
            get => _analysisData;
            set
            {
                _analysisData = value;
                OnPropertyChanged(nameof(AnalysisData));
                OnPropertyChanged(nameof(AnalysisDataView));
            }
        }

        public DataView AnalysisDataView => _analysisData?.DefaultView;

        public DataTable MovementData
        {
            get => _movementData;
            set
            {
                _movementData = value;
                OnPropertyChanged(nameof(MovementData));
                OnPropertyChanged(nameof(MovementDataView));
            }
        }

        public DataView MovementDataView => _movementData?.DefaultView;

        LiveGridControl AnalysisGrid => FCtrl<LiveGridControl>("gridCreditCardAnalysis");
        LiveGridControl MovementGrid => FCtrl<LiveGridControl>("gridCreditCardMovement");
        LiveDockLayoutManager DockLayoutManager => FCtrl<LiveDockLayoutManager>("creditCardDockLayoutManager");

        public CreditCardDetailAnalysisPM(IContainerExtension container) : base(container)
        {
        }

        public override void LoadCommands()
        {
            base.LoadCommands();
            CmdList.AddCmd(300, "RefreshAnalysisCommand", SLanguage.GetString("Yenile"), OnRefreshAnalysisCommand, null);
        }

        void OnRefreshAnalysisCommand(ISysCommandParam param)
        {
            RefreshAnalysis();
        }

        public override void _view_Loaded(object sender, RoutedEventArgs e)
        {
            base._view_Loaded(sender, e);
            PmTitle = SLanguage.GetString("Kredi Kartı Detay Analizi");
            DockLayoutManager?.RestoreLayout();
            AnalysisGrid?.Restore_Layout();
            MovementGrid?.Restore_Layout();
            EnsureAnalysisGridSelectionHook();
            RefreshAnalysis();
        }

        public override void Dispose()
        {
            if (AnalysisGrid?.View?.DataControl != null)
                AnalysisGrid.View.DataControl.CurrentItemChanged -= AnalysisGrid_CurrentItemChanged;

            base.Dispose();
        }

        void EnsureAnalysisGridSelectionHook()
        {
            if (_analysisGridHooked || AnalysisGrid?.View?.DataControl == null)
                return;

            AnalysisGrid.View.DataControl.CurrentItemChanged += AnalysisGrid_CurrentItemChanged;
            _analysisGridHooked = true;
        }

        void AnalysisGrid_CurrentItemChanged(object sender, CurrentItemChangedEventArgs e)
        {
            LoadSelectedPeriodMovements();
        }

        void RefreshAnalysis()
        {
            LiveSession session = SysMng.getSession() as LiveSession;
            AnalysisData = CreditCardDetailAnalysisService.BuildAnalysisTable(session);
            AnalysisGrid?.RefreshData();
            EnsureAnalysisGridSelectionHook();
            LoadSelectedPeriodMovements();
        }

        void LoadSelectedPeriodMovements()
        {
            if (AnalysisGrid?.View?.DataControl?.CurrentItem is not DataRowView selectedRow)
            {
                MovementData = CreditCardDetailMovementService.BuildPeriodMovementTable(null, 0, 0);
                MovementGrid?.RefreshData();
                return;
            }

            if (selectedRow.Row.IsNull("BankAccountId") || selectedRow.Row.IsNull("RecId"))
            {
                MovementData = CreditCardDetailMovementService.BuildPeriodMovementTable(null, 0, 0);
                MovementGrid?.RefreshData();
                return;
            }

            long bankAccountId = Convert.ToInt64(selectedRow.Row["BankAccountId"]);
            long periodRecId = Convert.ToInt64(selectedRow.Row["RecId"]);
            LiveSession session = SysMng.getSession() as LiveSession;
            MovementData = CreditCardDetailMovementService.BuildPeriodMovementTable(session, bankAccountId, periodRecId);
            MovementGrid?.RefreshData();
        }
    }
}
