using System;
using System.Data;
using System.Windows;
using System.Windows.Media;

using CashFlowManagementModule.Services;

using DevExpress.Xpf.Core.ConditionalFormatting;
using DevExpress.Xpf.Grid;

using LiveCore.Desktop.Common;
using Sentez.Common.SystemServices;
using LiveCore.Desktop.UI.Controls;

using Prism.Ioc;

using Sentez.Common.Commands;
using Sentez.Common.PresentationModels;
using Sentez.Common.SBase;
using Sentez.Common.Utilities;
using Sentez.Data.MetaData;
using Sentez.Localization;

namespace CashFlowManagementModule.PresentationModels
{
    public class CreditCardDetailAnalysisPM : PMDesktop
    {
        DataTable _analysisData;
        short _selectedYear = (short)DateTime.Today.Year;
        string _summaryText;

        public DataTable AnalysisData
        {
            get => _analysisData;
            set { _analysisData = value; OnPropertyChanged(nameof(AnalysisData)); }
        }

        public short SelectedYear
        {
            get => _selectedYear;
            set { _selectedYear = value; OnPropertyChanged(nameof(SelectedYear)); }
        }

        public string SummaryText
        {
            get => _summaryText;
            set { _summaryText = value; OnPropertyChanged(nameof(SummaryText)); }
        }

        LiveGridControl AnalysisGrid => FCtrl<LiveGridControl>("gridCreditCardAnalysis");

        public CreditCardDetailAnalysisPM(IContainerExtension container) : base(container)
        {
        }

        public override void LoadCommands()
        {
            base.LoadCommands();
            CmdList.AddCmd(360, "RefreshCreditCardAnalysisCommand", SLanguage.GetString("Yenile"), OnRefreshCreditCardAnalysisCommand, null);
        }

        public override void _view_Loaded(object sender, RoutedEventArgs e)
        {
            base._view_Loaded(sender, e);
            PmTitle = SLanguage.GetString("Kredi Kartı Detay Analizi");
            ConfigureGridColumns();
            RefreshAnalysis();
        }

        void OnRefreshCreditCardAnalysisCommand(ISysCommandParam obj)
        {
            RefreshAnalysis();
        }

        void RefreshAnalysis()
        {
            LiveSession session = SysMng.Instance.getSession() as LiveSession;
            AnalysisData = CreditCardDetailAnalysisService.BuildAnalysisTable(session, SelectedYear);
            UpdateSummary(session);
            ApplyCardRowFormatting();
            AnalysisGrid?.RefreshData();
        }

        void ApplyCardRowFormatting()
        {
            if (AnalysisGrid?.View is not TableView tableView)
                return;

            tableView.UseEvenRowBackground = false;
            tableView.FormatConditions.Clear();

            Brush[] pastelBackgrounds =
            {
                new SolidColorBrush(Color.FromRgb(255, 228, 235)), // açık pembe
                new SolidColorBrush(Color.FromRgb(232, 245, 233)), // açık yeşil
                new SolidColorBrush(Color.FromRgb(255, 249, 230)), // açık sarı
                new SolidColorBrush(Color.FromRgb(227, 242, 253)), // açık mavi
                new SolidColorBrush(Color.FromRgb(243, 244, 246))  // açık gri
            };

            for (int i = 0; i < CreditCardDetailAnalysisService.CardColorGroupCount; i++)
            {
                tableView.FormatConditions.Add(new FormatCondition
                {
                    ApplyToRow = true,
                    Expression = $"[ColorGroup] = {i}",
                    Format = new Format { Background = pastelBackgrounds[i % pastelBackgrounds.Length] }
                });
            }

            tableView.FormatConditions.Add(new FormatCondition
            {
                ApplyToRow = true,
                Expression = "[BankAccountId] = 0",
                Format = new Format
                {
                    Background = new SolidColorBrush(Color.FromRgb(220, 220, 220)),
                    FontWeight = FontWeights.SemiBold
                }
            });
        }

        void UpdateSummary(LiveSession session)
        {
            if (AnalysisData == null || AnalysisData.Rows.Count == 0)
            {
                SummaryText = SLanguage.GetString("Kayıt bulunamadı.");
                return;
            }

            DataRow summaryRow = AnalysisData.Rows[AnalysisData.Rows.Count - 1];
            object totalValue = summaryRow["TotalAmount"];
            decimal totalDebt = totalValue == null || totalValue == DBNull.Value ? 0m : Convert.ToDecimal(totalValue);
            SummaryText = string.Format(
                SLanguage.GetString("{0} yılı toplam kredi kartı borcu: {1}"),
                SelectedYear,
                totalDebt.ToString("N2"));
        }

        void ConfigureGridColumns()
        {
            if (AnalysisGrid == null) return;

            AnalysisGrid.AutoPopulateColumns = false;
            AnalysisGrid.ColumnDefinitions.Clear();
            AnalysisGrid.ColumnDefinitions.Add(new ReceiptColumn
            {
                ColumnName = "CardName",
                Caption = SLanguage.GetString("Kart"),
                Width = 180,
                EditorType = EditorType.ReadOnlyTextEditor
            });
            AnalysisGrid.ColumnDefinitions.Add(new ReceiptColumn
            {
                ColumnName = "RowType",
                Caption = SLanguage.GetString("Satır"),
                Width = 100,
                EditorType = EditorType.ReadOnlyTextEditor
            });

            for (int month = 1; month <= 12; month++)
            {
                AnalysisGrid.ColumnDefinitions.Add(new ReceiptColumn
                {
                    ColumnName = $"M{month:00}",
                    Caption = CreditCardDetailAnalysisService.GetMonthCaption(month),
                    Width = 95,
                    EditorType = EditorType.ReadOnlyTextEditor
                });
            }

            AnalysisGrid.ColumnDefinitions.Add(new ReceiptColumn
            {
                ColumnName = "TotalAmount",
                Caption = SLanguage.GetString("Toplam Harcama"),
                Width = 120,
                UsageType = FieldUsage.Amount,
                EditorType = EditorType.ReadOnlyTextEditor
            });
        }
    }
}
