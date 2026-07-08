using LiveCore.Desktop.SBase;
using LiveCore.Desktop.UI.Controls;

using CashFlowManagementModule.BoExtensions;
using CashFlowManagementModule.PresentationModels;
using CashFlowManagementModule.Services;

using Sentez.BankModule.PresentationModels;
using Sentez.Common;
using Sentez.Common.Commands;
using Sentez.Common.ModuleBase;
using Sentez.Common.Security;
using Sentez.Common.ModuleBase;
using Sentez.Common.PresentationModels;
using Sentez.Common.Utilities;
using Sentez.Data.BusinessObjects;
using Sentez.Data.MetaData;
using Sentez.Localization;

using LiveCore.Desktop.Common;
using Sentez.Common.SystemServices;

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;

namespace Sentez.CashFlowManagementModule
{
    /// <summary>
    /// BankReceiptPM için ödeme planlama (fiş tipi 15) ekran kancaları, toolbar,
    /// onay komutları ve toplu aktarım işlemlerini yöneten modül parçası.
    /// </summary>
    public partial class CashFlowManagementModule
    {
        /// <summary>Ödeme planlama fişi PM örneğine bağlanan aktif BankReceiptPM referansı.</summary>
        BankReceiptPM _paymentOrderBankReceiptPm;

        /// <summary>Ödeme planlama komut ve UI kancalarının yalnızca bir kez uygulanmasını sağlar.</summary>
        bool _paymentOrderHooksApplied;

        /// <summary>Detay grid seçim değişiminde talimat export komutunun yenilenmesi için bağlandı mı.</summary>
        bool _paymentOrderExportSelectionHooked;

        /// <summary>Detay grid üstüne eklenen ödeme planlama toolbar panelinin adı.</summary>
        const string PaymentOrderLineApprovalToolbarName = "PaymentOrderLineApprovalToolbar";

        /// <summary>Kredi kartı ödeme uyarı mesajlarının gösterildiği etiket adı.</summary>
        const string PaymentOrderCreditCardValidationLabelName = "PaymentOrderCreditCardValidationLabel";

        /// <summary>Toolbar altındaki yaşlandırma aktarım parametre panelinin adı.</summary>
        const string PaymentOrderAgingImportParamsPanelName = "PaymentOrderAgingImportParamsPanel";

        /// <summary>Yaşlandırma parametre panelinde Tab sırası için başlangıç değeri.</summary>
        const int PaymentOrderAgingParamTabIndexBase = 100;

        /// <summary>
        /// BankReceiptPM init, dispose ve view loaded olaylarına ödeme planlama kancalarını kaydeder.
        /// </summary>
        void RegisterBankReceiptPmHooks()
        {
            PMBase.AddCustomInit("BankReceiptPM", PaymentOrderBankReceiptPm_Init);
            PMBase.AddCustomDispose("BankReceiptPM", PaymentOrderBankReceiptPm_Dispose);
            PMBase.AddCustomViewLoaded("BankReceiptPM", PaymentOrderBankReceiptPm_ViewLoaded);
            RegisterCollectionOrderBankReceiptPmHooks();
        }

        /// <summary>
        /// Verilen PM örneğinin ödeme planlama fişi bağlamında çalışıp çalışmadığını döndürür.
        /// </summary>
        /// <param name="pm">Kontrol edilecek PM örneği.</param>
        /// <returns>Ödeme planlama fişi ise true, aksi halde false.</returns>
        bool IsPaymentOrderPm(PMBase pm)
        {
            return BankReceiptPaymentOrderHelper.IsPaymentOrderContext(pm);
        }

        /// <summary>
        /// Ödeme planlama komutlarını ve onay değişim komutunu PM üzerine bir kez bağlar.
        /// </summary>
        /// <param name="pm">Kanca uygulanacak BankReceiptPM örneği.</param>
        /// <returns>Kancalar uygulandıysa veya daha önce uygulanmışsa true döner.</returns>
        bool TryApplyPaymentOrderHooks(BankReceiptPM pm)
        {
            if (_paymentOrderHooksApplied || pm == null) return _paymentOrderHooksApplied;

            if (!BankReceiptPaymentOrderHelper.IsPaymentOrderContext(pm)) return false;

            EnsureBankReceiptApprovedChangeCommandRegistered();
            HookPaymentOrderCommands(pm);
            RefreshPaymentOrderApprovedChangeContextMenuCommand();
            _paymentOrderHooksApplied = true;
            return true;
        }

        /// <summary>
        /// BankReceiptPM açılışında BO olaylarını bağlar ve ödeme planlama kancalarını etkinleştirir.
        /// </summary>
        /// <param name="pm">Başlatılan PM örneği.</param>
        /// <param name="parameter">PM başlatma parametreleri.</param>
        void PaymentOrderBankReceiptPm_Init(PMBase pm, PmParam parameter)
        {
            _paymentOrderBankReceiptPm = BankReceiptPmAccess.GetBankReceiptPm(pm);
            if (_paymentOrderBankReceiptPm?.ActiveBO == null) return;

            _paymentOrderBankReceiptPm.ActiveBO.ColumnChanged += PaymentOrderBankReceiptPm_ActiveBO_ColumnChanged;
            _paymentOrderBankReceiptPm.ActiveBO.AfterGet += PaymentOrderBankReceiptPm_ActiveBO_AfterGet;
            _paymentOrderBankReceiptPm.ActiveBO.PropertyChanged += PaymentOrderBankReceiptPm_ActiveBO_PropertyChanged;
            _paymentOrderBankReceiptPm.PropertyChanged += PaymentOrderBankReceiptPm_PropertyChanged;

            TryApplyPaymentOrderHooks(_paymentOrderBankReceiptPm);
        }

        /// <summary>
        /// BankReceiptPM kapanışında bağlanan olayları çözer ve ödeme planlama durumunu sıfırlar.
        /// </summary>
        /// <param name="pm">Kapatılan PM örneği.</param>
        /// <param name="parameter">PM kapatma parametreleri.</param>
        void PaymentOrderBankReceiptPm_Dispose(PMBase pm, PmParam parameter)
        {
            BankReceiptPM bankReceiptPm = BankReceiptPmAccess.GetBankReceiptPm(pm);
            if (bankReceiptPm != null)
                bankReceiptPm.PropertyChanged -= PaymentOrderBankReceiptPm_PropertyChanged;

            UnhookPaymentOrderExportSelectionChanged();

            if (bankReceiptPm?.ActiveBO != null)
            {
                bankReceiptPm.ActiveBO.ColumnChanged -= PaymentOrderBankReceiptPm_ActiveBO_ColumnChanged;
                bankReceiptPm.ActiveBO.AfterGet -= PaymentOrderBankReceiptPm_ActiveBO_AfterGet;
                bankReceiptPm.ActiveBO.PropertyChanged -= PaymentOrderBankReceiptPm_ActiveBO_PropertyChanged;
            }

            _paymentOrderBankReceiptPm = null;
            _paymentOrderHooksApplied = false;
            _paymentOrderExportSelectionHooked = false;
        }

        /// <summary>
        /// Ödeme planlama ekranı yüklendiğinde kolon, toolbar ve onay arayüzünü hazırlar.
        /// </summary>
        /// <param name="sender">Olay kaynağı.</param>
        /// <param name="e">Routed event argümanları.</param>
        void PaymentOrderBankReceiptPm_ViewLoaded(object sender, RoutedEventArgs e)
        {
            if (_paymentOrderBankReceiptPm == null) return;

            if (!TryApplyPaymentOrderHooks(_paymentOrderBankReceiptPm)) return;

            _paymentOrderBankReceiptPm.ActiveViewControl?.Dispatcher.BeginInvoke(
                new Action(() =>
                {
                    if (BankReceiptPaymentOrderHelper.IsPaymentOrderContext(_paymentOrderBankReceiptPm))
                    {
                        var session = SysMng.Instance.getSession();
                        if (session?.LookupList != null)
                            MetaFixedPaymentTypeHelper.RefreshLookupList(session.LookupList);
                        MetaFixedPaymentTypeHelper.RefreshLookupList(_paymentOrderBankReceiptPm.Lists);
                    }

                    EnsurePaymentOrderImportSettingsLoaded();
                    AddPaymentOrderDetailColumns();
                    EnsurePaymentOrderLineApprovalToolbar();
                    EnsurePaymentOrderAgingImportParamsPanel();
                    EnsurePaymentOrderExportSelectionHook();
                    SyncPaymentOrderAgingReportDate();
                    ApplyPaymentOrderCardLockState();
                    ApplyPaymentOrderApprovalColumnAccess();
                    ApplyPaymentOrderApprovalContextMenuAccess();
                    RefreshPaymentOrderApprovedChangeContextMenuCommand();
                    RefreshPaymentOrderCommandStates();
                }),
                DispatcherPriority.Loaded);
        }

        /// <summary>
        /// Fiş kaydı yüklendikten sonra ödeme planlama arayüz bileşenlerini yeniden uygular.
        /// </summary>
        /// <param name="sender">Olay kaynağı.</param>
        /// <param name="e">Olay argümanları.</param>
        void PaymentOrderBankReceiptPm_ActiveBO_AfterGet(object sender, EventArgs e)
        {
            if (_paymentOrderBankReceiptPm == null) return;

            if (!TryApplyPaymentOrderHooks(_paymentOrderBankReceiptPm)) return;

            _paymentOrderBankReceiptPm.ActiveViewControl?.Dispatcher.BeginInvoke(
                new Action(() =>
                {
                    if (BankReceiptPaymentOrderHelper.IsPaymentOrderContext(_paymentOrderBankReceiptPm))
                    {
                        var session = SysMng.Instance.getSession();
                        if (session?.LookupList != null)
                            MetaFixedPaymentTypeHelper.RefreshLookupList(session.LookupList);
                        MetaFixedPaymentTypeHelper.RefreshLookupList(_paymentOrderBankReceiptPm.Lists);
                    }

                    EnsurePaymentOrderImportSettingsLoaded();
                    AddPaymentOrderDetailColumns();
                    EnsurePaymentOrderLineApprovalToolbar();
                    EnsurePaymentOrderAgingImportParamsPanel();
                    EnsurePaymentOrderExportSelectionHook();
                    SyncPaymentOrderAgingReportDate();
                    ApplyPaymentOrderCardLockState();
                    ApplyPaymentOrderApprovalColumnAccess();
                    ApplyPaymentOrderApprovalContextMenuAccess();
                    RefreshPaymentOrderApprovedChangeContextMenuCommand();
                    RefreshPaymentOrderDetailGrid();
                }),
                DispatcherPriority.Background);
        }

        /// <summary>
        /// Fiş başlığı veya satır değişiminde ödeme planlama kart kilidini günceller.
        /// </summary>
        /// <param name="sender">Olay kaynağı.</param>
        /// <param name="e">PropertyChanged olay argümanları.</param>
        void PaymentOrderBankReceiptPm_ActiveBO_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (_paymentOrderBankReceiptPm == null || !IsPaymentOrderPm(_paymentOrderBankReceiptPm)) return;

            if (e.PropertyName == "IsNewRecord" || e.PropertyName == "CurrentRow")
            {
                ApplyPaymentOrderCardLockState();
                if (e.PropertyName == "CurrentRow")
                    BankReceiptItemAccessCodeHelper.ApplyDetailGridFilter(_paymentOrderBankReceiptPm);
            }
        }

        /// <summary>
        /// Ön değer banka hesabı değiştiğinde talimat ve aktarım komutlarının CanExecute durumunu yeniler.
        /// </summary>
        void PaymentOrderBankReceiptPm_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (_paymentOrderBankReceiptPm == null || !IsPaymentOrderPm(_paymentOrderBankReceiptPm)) return;

            if (e.PropertyName == nameof(PaymentOrderBankReceiptPM.DefaultBankAccountCode))
                RefreshPaymentOrderCommandStates();
        }

        /// <summary>
        /// Detay satırı veya başlık kolon değişiminde grid, kilit ve kredi kartı doğrulamasını yeniler.
        /// </summary>
        /// <param name="sender">Olay kaynağı.</param>
        /// <param name="e">Kolon değişim olay argümanları.</param>
        void PaymentOrderBankReceiptPm_ActiveBO_ColumnChanged(object sender, DataColumnChangeEventArgs e)
        {
            if (_paymentOrderBankReceiptPm == null) return;

            if (e.Row.Table.TableName == "Erp_BankReceiptItem" && e.Column.ColumnName == "IsApproved")
            {
                RefreshPaymentOrderDetailGrid();
            }
            else if (e.Row.Table.TableName == "Erp_BankReceiptItem" && e.Column.ColumnName == BankReceiptItemAccessCodeHelper.FieldAccessCode)
            {
                BankReceiptItemAccessCodeHelper.ApplyDetailGridFilter(_paymentOrderBankReceiptPm);
            }
            else if (e.Row.Table.TableName == "Erp_BankReceipt" && e.Column.ColumnName == "IsApproved")
            {
                ApplyPaymentOrderCardLockState();
                RefreshPaymentOrderDetailGrid();
            }
            else if (e.Row.Table.TableName == "Erp_BankReceipt" && e.Column.ColumnName == "ReceiptDate")
            {
                SyncPaymentOrderAgingReportDate();
            }
            else if (e.Row.Table.TableName == "Erp_BankReceiptItem" && e.Column.ColumnName == "ReceiptDate")
            {
                if (IsPaymentOrderPm(_paymentOrderBankReceiptPm)
                    && _paymentOrderBankReceiptPm.ActiveBO is BusinessObjectBase businessObject)
                {
                    BankReceiptPaymentOrderHelper.ProtectItemPaymentDateAfterReceiptDateChange(businessObject, e.Row);
                }
            }
            else if (e.Row.Table.TableName == "Erp_BankReceiptItem"
                     && (e.Column.ColumnName == "BankAccountId"
                         || e.Column.ColumnName == "UD_PaymentDate"
                         || e.Column.ColumnName == "Credit"
                         || e.Column.ColumnName == BankReceiptCreditCardHelper.FieldInstallmentCount))
            {
                NormalizeInstallmentCount(e.Row);
                RefreshPaymentOrderCreditCardValidationMessage(e.Row);

                if (e.Column.ColumnName == "Credit")
                    RefreshPaymentOrderCommandStates();
            }
        }

        /// <summary>
        /// Taksit sayısı alanını en az 1 olacak şekilde normalize eder.
        /// </summary>
        /// <param name="itemRow">Güncellenecek fiş satırı.</param>
        void NormalizeInstallmentCount(DataRow itemRow)
        {
            if (itemRow == null) return;
            if (!itemRow.Table.Columns.Contains(BankReceiptCreditCardHelper.FieldInstallmentCount)) return;

            if (itemRow.IsNull(BankReceiptCreditCardHelper.FieldInstallmentCount)
                || Convert.ToInt16(itemRow[BankReceiptCreditCardHelper.FieldInstallmentCount]) < 1)
            {
                itemRow[BankReceiptCreditCardHelper.FieldInstallmentCount] = (short)1;
            }
        }

        /// <summary>
        /// Seçili satır için kredi kartı ödeme uyarısını hesaplar ve ekran etiketini günceller.
        /// </summary>
        /// <param name="itemRow">Doğrulanacak fiş satırı.</param>
        void RefreshPaymentOrderCreditCardValidationMessage(DataRow itemRow)
        {
            if (_paymentOrderBankReceiptPm == null || itemRow == null) return;
            if (itemRow.IsNull("BankAccountId")) return;

            LiveSession session = SysMng.Instance.getSession() as LiveSession;
            if (session == null) return;

            BusinessObjectBase businessObject = _paymentOrderBankReceiptPm.ActiveBO as BusinessObjectBase;
            if (businessObject?.Connection == null) return;

            DateTime? fallbackDate = null;
            if (_paymentOrderBankReceiptPm.ActiveBO?.CurrentRow?.Row != null
                && _paymentOrderBankReceiptPm.ActiveBO.CurrentRow.Row.Table.Columns.Contains("UD_PaymentDate")
                && !_paymentOrderBankReceiptPm.ActiveBO.CurrentRow.Row.IsNull("UD_PaymentDate"))
            {
                fallbackDate = Convert.ToDateTime(_paymentOrderBankReceiptPm.ActiveBO.CurrentRow.Row["UD_PaymentDate"]);
            }

            CreditCardPaymentLineInput line = CreditCardPaymentLineInput.FromBankReceiptItem(itemRow, fallbackDate);
            CreditCardPaymentValidationResult result = CreditCardPaymentWarningService.ValidateLinePreview(
                businessObject.Provider,
                businessObject.Connection,
                businessObject.Transaction,
                session.ActiveCompany.RecId ?? 0,
                line);
            UpdatePaymentOrderCreditCardValidationLabel(result);
        }

        /// <summary>
        /// Kredi kartı doğrulama sonucunu toolbar üzerindeki uyarı etiketine yansıtır.
        /// </summary>
        /// <param name="result">Doğrulama sonucu; null ise etiket temizlenir.</param>
        void UpdatePaymentOrderCreditCardValidationLabel(CreditCardPaymentValidationResult result)
        {
            if (_paymentOrderBankReceiptPm?.ActiveViewControl == null) return;

            TextBlock label = _paymentOrderBankReceiptPm.ActiveViewControl.FindName(PaymentOrderCreditCardValidationLabelName) as TextBlock;
            if (label == null) return;

            if (result == null || (!result.IsBlocked && !result.HasWarning))
            {
                label.Text = string.Empty;
                label.Foreground = System.Windows.Media.Brushes.Black;
                return;
            }

            label.Text = result.IsBlocked ? result.BlockMessage : result.WarningMessage;
            label.Foreground = result.IsBlocked
                ? System.Windows.Media.Brushes.DarkRed
                : System.Windows.Media.Brushes.DarkOrange;
        }

        /// <summary>
        /// Ödeme planlama detay gridine onay, taksit ve sabit ödeme tipi kolonlarını ekler.
        /// </summary>
        void AddPaymentOrderDetailColumns()
        {
            if (_paymentOrderBankReceiptPm?.BankReceiptColumnCollection == null) return;

            AddColumnIfMissing("IsApproved", SLanguage.GetString("Onay Durumu"), EditorType.ComboBox, FieldUsage.None, 90, "ApprovedList");
            AddColumnIfMissing(BankReceiptCreditCardHelper.FieldInstallmentCount, SLanguage.GetString("Taksit Sayısı"), EditorType.TextEditor, FieldUsage.None, 90);
            BankReceiptItemAccessCodeHelper.EnsureDetailColumn(_paymentOrderBankReceiptPm.BankReceiptColumnCollection);
            EnsureFixedPaymentTypeDetailColumn();
            BankReceiptItemAuditHelper.AddAuditDetailColumns(_paymentOrderBankReceiptPm.BankReceiptColumnCollection);

            ReceiptColumnCollection columns = _paymentOrderBankReceiptPm.BankReceiptColumnCollection;
            _paymentOrderBankReceiptPm.BankReceiptColumnCollection = columns;

            ApplyPaymentOrderApprovalColumnAccess();
            RefreshPaymentOrderDetailGridColumns();
        }

        /// <summary>
        /// Detay grid kolon tanımlarını audit kolonları dahil olacak şekilde yeniden uygular.
        /// </summary>
        void RefreshPaymentOrderDetailGridColumns()
        {
            if (_paymentOrderBankReceiptPm == null || !IsPaymentOrderPm(_paymentOrderBankReceiptPm)) return;
            if (_paymentOrderBankReceiptPm.BankReceiptColumnCollection == null) return;

            LiveGridControl gridDetail = _paymentOrderBankReceiptPm.FCtrl("gridDetail") as LiveGridControl;
            if (gridDetail == null) return;

            gridDetail.SaveLayoutType = $"{BankReceiptPaymentOrderHelper.ReceiptType}-cfm-audit";
            gridDetail.ColumnDefinitions = _paymentOrderBankReceiptPm.BankReceiptColumnCollection;
            gridDetail.ApplyReceiptColumnDefinitions();
            gridDetail.RefreshData();
            BankReceiptItemAccessCodeHelper.ApplyDetailGridFilter(_paymentOrderBankReceiptPm);
        }

        /// <summary>
        /// Sabit ödeme tipi kolonunu oluşturur veya mevcut kolon özelliklerini günceller.
        /// </summary>
        void EnsureFixedPaymentTypeDetailColumn()
        {
            if (_paymentOrderBankReceiptPm?.BankReceiptColumnCollection == null) return;

            string columnName = BankReceiptFixedPaymentHelper.FieldFixedPaymentTypeId;
            ReceiptColumn column = _paymentOrderBankReceiptPm.BankReceiptColumnCollection
                .FirstOrDefault(c => c.ColumnName == columnName);

            if (column == null)
            {
                column = new ReceiptColumn
                {
                    ColumnName = columnName,
                    Caption = SLanguage.GetString("Ödeme Tipi"),
                    EditorType = EditorType.ComboBox,
                    ComboLookup = MetaFixedPaymentTypeHelper.LookupListName,
                    ComboDisplayMember = "FixedPaymentTypeName",
                    ComboValueMember = "RecId",
                    Width = 220,
                    UsageType = FieldUsage.None,
                    IsVisible = true
                };
                _paymentOrderBankReceiptPm.BankReceiptColumnCollection.Add(column);
                return;
            }

            column.Caption = SLanguage.GetString("Ödeme Tipi");
            column.EditorType = EditorType.ComboBox;
            column.ComboLookup = MetaFixedPaymentTypeHelper.LookupListName;
            column.ComboDisplayMember = "FixedPaymentTypeName";
            column.ComboValueMember = "RecId";
            column.Width = 220;
            column.UsageType = FieldUsage.None;
            column.IsVisible = true;
        }

        /// <summary>
        /// Belirtilen kolon detay koleksiyonda yoksa yeni ReceiptColumn olarak ekler.
        /// </summary>
        /// <param name="columnName">Kolon adı.</param>
        /// <param name="caption">Görünen başlık.</param>
        /// <param name="editorType">Editör tipi.</param>
        /// <param name="usageType">Alan kullanım tipi.</param>
        /// <param name="width">Kolon genişliği.</param>
        /// <param name="comboLookup">ComboBox için lookup listesi adı; isteğe bağlı.</param>
        void AddColumnIfMissing(string columnName, string caption, EditorType editorType, FieldUsage usageType, int width, string comboLookup = null)
        {
            if (_paymentOrderBankReceiptPm.BankReceiptColumnCollection.Any(c => c.ColumnName == columnName))
                return;

            ReceiptColumn column = new ReceiptColumn()
            {
                ColumnName = columnName,
                Caption = caption,
                EditorType = editorType,
                Width = width,
                UsageType = usageType,
                IsVisible = true
            };

            if (!string.IsNullOrEmpty(comboLookup))
            {
                column.ComboLookup = comboLookup;
                column.ComboDisplayMember = "Display";
                column.ComboValueMember = "Value";
            }

            _paymentOrderBankReceiptPm.BankReceiptColumnCollection.Add(column);
        }

        /// <summary>
        /// Ödeme planlama fişindeki rapor tarihini fiş tarihi ile senkronize eder.
        /// </summary>
        void SyncPaymentOrderAgingReportDate()
        {
            if (_paymentOrderBankReceiptPm is PaymentOrderBankReceiptPM paymentOrderPm)
                paymentOrderPm.SyncAgingReportDateFromReceipt();
        }

        /// <summary>
        /// Yaşlandırma aktarım ayarlarını ve ön değer banka hesabını yükler.
        /// </summary>
        void EnsurePaymentOrderImportSettingsLoaded()
        {
            if (_paymentOrderBankReceiptPm == null || !IsPaymentOrderPm(_paymentOrderBankReceiptPm)) return;

            var paymentOrderPm = _paymentOrderBankReceiptPm as PaymentOrderBankReceiptPM;
            if (paymentOrderPm == null) return;

            paymentOrderPm.LoadAgingImportWindowSettings();
            paymentOrderPm.EnsureDefaultBankAccountResolved();
        }

        /// <summary>
        /// Toolbar altına yaşlandırma aktarım parametrelerini içeren dikey panel ekler.
        /// </summary>
        void EnsurePaymentOrderAgingImportParamsPanel()
        {
            if (_paymentOrderBankReceiptPm == null || !IsPaymentOrderPm(_paymentOrderBankReceiptPm)) return;

            var paymentOrderPm = _paymentOrderBankReceiptPm as PaymentOrderBankReceiptPM;
            if (paymentOrderPm == null) return;

            EnsurePaymentOrderImportSettingsLoaded();

            LiveGridControl gridDetail = _paymentOrderBankReceiptPm.FCtrl("gridDetail") as LiveGridControl;
            if (gridDetail?.Parent is not Grid parentGrid) return;
            if (parentGrid.FindName(PaymentOrderAgingImportParamsPanelName) != null) return;

            int insertRow = parentGrid.FindName(PaymentOrderLineApprovalToolbarName) != null ? 1 : 0;
            parentGrid.RowDefinitions.Insert(insertRow, new RowDefinition { Height = GridLength.Auto });

            foreach (UIElement child in parentGrid.Children)
            {
                int row = Grid.GetRow(child);
                if (row >= insertRow)
                    Grid.SetRow(child, row + 1);
            }

            var panel = new Grid
            {
                Name = PaymentOrderAgingImportParamsPanelName,
                Margin = new Thickness(0, 0, 0, 4)
            };
            KeyboardNavigation.SetTabNavigation(panel, KeyboardNavigationMode.Local);

            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            panel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(280) });

            int tabIndex = PaymentOrderAgingParamTabIndexBase;
            AddPaymentOrderAgingParamRow(panel, 0, SLanguage.GetString("Rapor Tarihi"), CreateAgingReportDateEditor(paymentOrderPm, tabIndex++));
            AddPaymentOrderAgingParamRow(
                panel,
                1,
                SLanguage.GetString("Başlangıç Cari Kodu"),
                CreateAgingCurrentAccountLookup(
                    AgingStartCurrentAccountCodeFieldName,
                    nameof(PaymentOrderBankReceiptPM.AgingStartCurrentAccountCode),
                    "Erp_CurrentAccountCurrentAccountCodeList",
                    paymentOrderPm,
                    paymentOrderPm.OnAgingStartCurrentAccountCodeKeyDown,
                    tabIndex++));
            AddPaymentOrderAgingParamRow(
                panel,
                2,
                SLanguage.GetString("Bitiş Cari Kodu"),
                CreateAgingCurrentAccountLookup(
                    AgingEndCurrentAccountCodeFieldName,
                    nameof(PaymentOrderBankReceiptPM.AgingEndCurrentAccountCode),
                    "Erp_CurrentAccountCurrentAccountCodeList",
                    paymentOrderPm,
                    paymentOrderPm.OnAgingEndCurrentAccountCodeKeyDown,
                    tabIndex++));
            AddPaymentOrderAgingParamRow(
                panel,
                3,
                SLanguage.GetString("Ön Değer Banka Hesabı"),
                CreateAgingDefaultBankAccountLookup(paymentOrderPm, tabIndex++));
            AddPaymentOrderAgingDirectImportRow(panel, 4, paymentOrderPm, tabIndex);

            Grid.SetRow(panel, insertRow);
            Grid.SetColumnSpan(panel, 4);
            parentGrid.Children.Add(panel);
            parentGrid.RegisterName(PaymentOrderAgingImportParamsPanelName, panel);
        }

        static void AddPaymentOrderAgingParamRow(Grid panel, int row, string caption, UIElement editor)
        {
            var label = new LiveLabel
            {
                HorizontalAlignment = HorizontalAlignment.Right,
                HorizontalContentAlignment = HorizontalAlignment.Right,
                Padding = new Thickness(2),
                VerticalContentAlignment = VerticalAlignment.Center,
                Width = 150,
                Content = caption
            };

            Grid.SetRow(label, row);
            Grid.SetColumn(label, 0);
            Grid.SetRow(editor, row);
            Grid.SetColumn(editor, 1);

            panel.Children.Add(label);
            panel.Children.Add(editor);
        }

        static void AddPaymentOrderAgingDirectImportRow(Grid panel, int row, PaymentOrderBankReceiptPM paymentOrderPm, int tabIndex)
        {
            var checkEdit = new LiveCheckEdit
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(2, 1, 0, 2),
                Content = SLanguage.GetString("Yaşlandırma sonuçlarını doğrudan fişe aktar"),
                TabIndex = tabIndex,
                IsTabStop = true
            };
            checkEdit.SetBinding(
                LiveCheckEdit.IsCheckedProperty,
                new Binding(nameof(PaymentOrderBankReceiptPM.ImportAgingDirectlyToReceipt))
                {
                    Source = paymentOrderPm,
                    Mode = BindingMode.TwoWay,
                    UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                });

            Grid.SetRow(checkEdit, row);
            Grid.SetColumn(checkEdit, 0);
            Grid.SetColumnSpan(checkEdit, 2);
            panel.Children.Add(checkEdit);
        }

        static LiveDateEdit CreateAgingReportDateEditor(PaymentOrderBankReceiptPM paymentOrderPm, int tabIndex)
        {
            var dateEdit = new LiveDateEdit
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(2, 1, 0, 2),
                Width = 120,
                TabIndex = tabIndex,
                IsTabStop = true
            };
            dateEdit.SetBinding(
                LiveDateEdit.EditValueProperty,
                new Binding(nameof(PaymentOrderBankReceiptPM.AgingReportDate))
                {
                    Source = paymentOrderPm,
                    Mode = BindingMode.TwoWay,
                    UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                });
            return dateEdit;
        }

        static LiveLookUpEdit CreateAgingCurrentAccountLookup(
            string name,
            string propertyName,
            string workListName,
            PaymentOrderBankReceiptPM paymentOrderPm,
            KeyEventHandler keyDownHandler,
            int tabIndex)
        {
            var lookup = new LiveLookUpEdit
            {
                Name = name,
                WorkListName = workListName,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(2, 1, 0, 2),
                Width = 160,
                TabIndex = tabIndex,
                DataContext = paymentOrderPm
            };
            lookup.SetBinding(
                LiveLookUpEdit.TextProperty,
                new Binding(propertyName)
                {
                    Source = paymentOrderPm,
                    Mode = BindingMode.TwoWay,
                    UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                });
            lookup.PreviewKeyDown += keyDownHandler;
            return lookup;
        }

        static LiveLookUpEdit CreateAgingDefaultBankAccountLookup(PaymentOrderBankReceiptPM paymentOrderPm, int tabIndex)
        {
            var lookup = new LiveLookUpEdit
            {
                Name = DefaultBankAccountCodeFieldName,
                WorkListName = "Erp_BankAccountAccountCodeList",
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(2, 1, 0, 2),
                Width = 160,
                TabIndex = tabIndex,
                DataContext = paymentOrderPm
            };
            lookup.SetBinding(
                LiveLookUpEdit.TextProperty,
                new Binding(nameof(PaymentOrderBankReceiptPM.DefaultBankAccountCode))
                {
                    Source = paymentOrderPm,
                    Mode = BindingMode.TwoWay,
                    UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                });
            lookup.PreviewKeyDown += paymentOrderPm.OnDefaultBankAccountCodeKeyDown;
            return lookup;
        }

        const string DefaultBankAccountCodeFieldName = "TxtDefaultBankAccountCode";
        const string AgingStartCurrentAccountCodeFieldName = "TxtAgingStartCurrentAccountCode";
        const string AgingEndCurrentAccountCodeFieldName = "TxtAgingEndCurrentAccountCode";

        /// <summary>
        /// Onay durumu kolonunun düzenlenebilir olmasını sağlar.
        /// </summary>
        void ApplyPaymentOrderApprovalColumnAccess()
        {
            if (_paymentOrderBankReceiptPm?.BankReceiptColumnCollection == null) return;

            ReceiptColumn approvalColumn = _paymentOrderBankReceiptPm.BankReceiptColumnCollection
                .FirstOrDefault(c => c.ColumnName == "IsApproved");
            if (approvalColumn == null) return;

            if (approvalColumn.IsReadOnly)
            {
                approvalColumn.IsReadOnly = false;
                ReceiptColumnCollection columns = _paymentOrderBankReceiptPm.BankReceiptColumnCollection;
                _paymentOrderBankReceiptPm.BankReceiptColumnCollection = columns;
            }
        }

        /// <summary>
        /// Satır onay işlemleri için bağlam menüsü erişimini günceller.
        /// </summary>
        void ApplyPaymentOrderApprovalContextMenuAccess()
        {
            BankReceiptPaymentOrderApprovalHelper.RefreshPaymentOrderApprovalUi(_paymentOrderBankReceiptPm);
        }

        /// <summary>
        /// Bağlam menüsündeki onay değiştir komutunu PM komut listesiyle eşler.
        /// </summary>
        void RefreshPaymentOrderApprovedChangeContextMenuCommand()
        {
            if (_paymentOrderBankReceiptPm?.contextMenu == null || _paymentOrderBankReceiptPm.CmdList == null) return;

            ISysCommand approvedChangeCommand = _paymentOrderBankReceiptPm.CmdList["ApprovedChangeCommand"];
            if (approvedChangeCommand == null) return;

            foreach (object item in _paymentOrderBankReceiptPm.contextMenu.Items)
            {
                if (item is MenuItem menuItem && menuItem.Name == "ApprovedChangeCommand")
                    menuItem.Command = approvedChangeCommand;
            }
        }

        /// <summary>
        /// Fiş onay durumuna göre ödeme planlama ekranının düzenlenebilirliğini ayarlar.
        /// </summary>
        void ApplyPaymentOrderCardLockState()
        {
            if (_paymentOrderBankReceiptPm?.ActiveBO?.CurrentRow?.Row == null) return;

            bool isLocked = BankReceiptPaymentOrderHelper.ShouldLockPaymentOrder(_paymentOrderBankReceiptPm.ActiveBO);

            if (_paymentOrderBankReceiptPm is PMDesktop pmDesktop)
                pmDesktop.SetViewEnabled(!isLocked);
        }

        /// <summary>
        /// Detay grid verisini yeniden yükler.
        /// </summary>
        void RefreshPaymentOrderDetailGrid()
        {
            if (_paymentOrderBankReceiptPm == null) return;

            LiveGridControl gridDetail = _paymentOrderBankReceiptPm.FCtrl("gridDetail") as LiveGridControl;
            gridDetail?.RefreshData();
            BankReceiptItemAccessCodeHelper.ApplyDetailGridFilter(_paymentOrderBankReceiptPm);
            RefreshPaymentOrderCommandStates();
        }

        /// <summary>
        /// CanExecute koşulu olan ödeme planlama komutlarının buton durumunu yeniler.
        /// </summary>
        void RefreshPaymentOrderCommandStates()
        {
            if (_paymentOrderBankReceiptPm?.CmdList == null) return;

            RaisePaymentOrderCommandCanExecuteChanged("ExportPaymentInstructionCommand");
            RaisePaymentOrderCommandCanExecuteChanged("ImportFixedPaymentsCommand");
            RaisePaymentOrderCommandCanExecuteChanged("ImportCreditCardStatementSpendingCommand");
            RaisePaymentOrderCommandCanExecuteChanged("ImportCurrentAccountAgingCommand");
            RaisePaymentOrderCommandCanExecuteChanged("ApprovedChangeCommand");
        }

        void RaisePaymentOrderCommandCanExecuteChanged(string commandName)
        {
            if (_paymentOrderBankReceiptPm?.CmdList?[commandName] is SysCommand command)
                command.RaiseCanExecuteChanged();
        }

        /// <summary>
        /// Talimat export komutunun seçim değişiminde güncellenmesi için detay grid olayını bağlar.
        /// </summary>
        void EnsurePaymentOrderExportSelectionHook()
        {
            if (_paymentOrderExportSelectionHooked || _paymentOrderBankReceiptPm == null) return;
            if (!IsPaymentOrderPm(_paymentOrderBankReceiptPm)) return;

            LiveGridControl gridDetail = _paymentOrderBankReceiptPm.FCtrl("gridDetail") as LiveGridControl;
            if (gridDetail == null) return;

            gridDetail.SelectionChanged += PaymentOrderDetailGrid_SelectionChanged;
            _paymentOrderExportSelectionHooked = true;
        }

        void UnhookPaymentOrderExportSelectionChanged()
        {
            if (!_paymentOrderExportSelectionHooked || _paymentOrderBankReceiptPm == null) return;

            LiveGridControl gridDetail = _paymentOrderBankReceiptPm.FCtrl("gridDetail") as LiveGridControl;
            if (gridDetail != null)
                gridDetail.SelectionChanged -= PaymentOrderDetailGrid_SelectionChanged;

            _paymentOrderExportSelectionHooked = false;
        }

        void PaymentOrderDetailGrid_SelectionChanged(object sender, EventArgs e)
        {
            RefreshPaymentOrderCommandStates();
        }

        /// <summary>
        /// Detay gridde seçili ve talimat için uygun satırları döndürür.
        /// </summary>
        List<DataRow> GetSelectedPaymentOrderExportRows()
        {
            var rows = new List<DataRow>();
            if (_paymentOrderBankReceiptPm == null) return rows;

            LiveGridControl gridDetail = _paymentOrderBankReceiptPm.FCtrl("gridDetail") as LiveGridControl;
            if (gridDetail?.SelectedItems == null || gridDetail.SelectedItems.Count == 0)
                return rows;

            foreach (object selectedItem in gridDetail.SelectedItems)
            {
                if (selectedItem is not DataRowView rowView) continue;
                if (rowView.Row.RowState == DataRowState.Deleted || rowView.Row.RowState == DataRowState.Detached)
                    continue;

                rows.Add(rowView.Row);
            }

            return PaymentOrderInstructionExportService.NormalizeExportRows(rows);
        }

        /// <summary>
        /// Detay grid üstüne onay, aktarım butonları ve kredi kartı uyarı etiketini içeren toolbar ekler.
        /// </summary>
        void EnsurePaymentOrderLineApprovalToolbar()
        {
            if (_paymentOrderBankReceiptPm == null || !IsPaymentOrderPm(_paymentOrderBankReceiptPm)) return;

            LiveGridControl gridDetail = _paymentOrderBankReceiptPm.FCtrl("gridDetail") as LiveGridControl;
            if (gridDetail?.Parent is not Grid parentGrid) return;

            if (parentGrid.FindName(PaymentOrderLineApprovalToolbarName) != null) return;

            parentGrid.RowDefinitions.Insert(0, new RowDefinition { Height = GridLength.Auto });

            foreach (UIElement child in parentGrid.Children)
            {
                int row = Grid.GetRow(child);
                Grid.SetRow(child, row + 1);
            }

            var toolbar = new StackPanel
            {
                Name = PaymentOrderLineApprovalToolbarName,
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 4)
            };

            var btnApprove = new LiveButton
            {
                Content = SLanguage.GetString("Seçili Satırları Onayla"),
                Margin = new Thickness(0, 0, 6, 0),
                Padding = new Thickness(8, 2, 8, 2)
            };
            btnApprove.Command = _paymentOrderBankReceiptPm.CmdList["PaymentOrderBulkLineApproveCommand"];

            var btnUnapprove = new LiveButton
            {
                Content = SLanguage.GetString("Seçili Satırları Onaysız Yap"),
                Padding = new Thickness(8, 2, 8, 2)
            };
            btnUnapprove.Command = _paymentOrderBankReceiptPm.CmdList["PaymentOrderBulkLineUnapproveCommand"];

            toolbar.Children.Add(btnApprove);
            toolbar.Children.Add(btnUnapprove);

            var btnImportFixedPayments = new LiveButton
            {
                Content = SLanguage.GetString("Tekrar Eden Ödemeleri Aktar"),
                Margin = new Thickness(6, 0, 0, 0),
                Padding = new Thickness(8, 2, 8, 2)
            };
            btnImportFixedPayments.Command = _paymentOrderBankReceiptPm.CmdList["ImportFixedPaymentsCommand"];
            toolbar.Children.Add(btnImportFixedPayments);

            var btnImportStatementSpending = new LiveButton
            {
                Content = SLanguage.GetString("Kredi Kartı Harcamalarını Aktar"),
                Margin = new Thickness(6, 0, 0, 0),
                Padding = new Thickness(8, 2, 8, 2)
            };
            btnImportStatementSpending.Command = _paymentOrderBankReceiptPm.CmdList["ImportCreditCardStatementSpendingCommand"];
            toolbar.Children.Add(btnImportStatementSpending);

            var btnImportAging = new LiveButton
            {
                Content = SLanguage.GetString("Yaşlandırma Tutarlarını Aktar"),
                Margin = new Thickness(6, 0, 0, 0),
                Padding = new Thickness(8, 2, 8, 2)
            };
            btnImportAging.Command = _paymentOrderBankReceiptPm.CmdList["ImportCurrentAccountAgingCommand"];
            toolbar.Children.Add(btnImportAging);

            var btnExportInstruction = new LiveButton
            {
                Content = SLanguage.GetString("Talimat Dosyası Oluştur"),
                Margin = new Thickness(6, 0, 0, 0),
                Padding = new Thickness(8, 2, 8, 2)
            };
            btnExportInstruction.Command = _paymentOrderBankReceiptPm.CmdList["ExportPaymentInstructionCommand"];
            toolbar.Children.Add(btnExportInstruction);

            var validationLabel = new TextBlock
            {
                Name = PaymentOrderCreditCardValidationLabelName,
                Margin = new Thickness(12, 4, 0, 0),
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center
            };
            toolbar.Children.Add(validationLabel);

            Grid.SetRow(toolbar, 0);
            parentGrid.Children.Add(toolbar);
            parentGrid.RegisterName(PaymentOrderLineApprovalToolbarName, toolbar);
            parentGrid.RegisterName(PaymentOrderCreditCardValidationLabelName, validationLabel);
        }

        /// <summary>
        /// Ödeme planlama ekranına onay, toplu onay ve aktarım komutlarını PM komut listesine kaydeder.
        /// </summary>
        /// <param name="pm">Komutların bağlanacağı BankReceiptPM örneği.</param>
        void HookPaymentOrderCommands(BankReceiptPM pm)
        {
            if (pm?.CmdList == null) return;

            ISysCommand existingCommand = pm.CmdList["ApprovedChangeCommand"];
            if (existingCommand != null)
                pm.CmdList.Remove(existingCommand);

            pm.CmdList.AddCmd(
                115,
                "ApprovedChangeCommand",
                SLanguage.GetString("Onay İşlemi"),
                PaymentOrderOnApprovedChangeCommand,
                PaymentOrderCanApprovedChangeCommand);

            if (pm.CmdList["PaymentOrderBulkLineApproveCommand"] == null)
            {
                pm.CmdList.AddCmd(
                    320,
                    "PaymentOrderBulkLineApproveCommand",
                    SLanguage.GetString("Seçili Satırları Onayla"),
                    PaymentOrderBulkLineApproveCommand,
                    null);
            }

            if (pm.CmdList["PaymentOrderBulkLineUnapproveCommand"] == null)
            {
                pm.CmdList.AddCmd(
                    321,
                    "PaymentOrderBulkLineUnapproveCommand",
                    SLanguage.GetString("Seçili Satırları Onaysız Yap"),
                    PaymentOrderBulkLineUnapproveCommand,
                    null);
            }

            if (pm.CmdList["ImportFixedPaymentsCommand"] == null)
            {
                pm.CmdList.AddCmd(
                    322,
                    "ImportFixedPaymentsCommand",
                    SLanguage.GetString("Tekrar Eden Ödemeleri Aktar"),
                    ImportFixedPaymentsCommand,
                    CanImportFixedPaymentsCommand);
            }

            if (pm.CmdList["ImportCreditCardStatementSpendingCommand"] == null)
            {
                pm.CmdList.AddCmd(
                    323,
                    "ImportCreditCardStatementSpendingCommand",
                    SLanguage.GetString("Ekstre Harcamalarını Aktar"),
                    ImportCreditCardStatementSpendingCommand,
                    CanImportCreditCardStatementSpendingCommand);
            }

            if (pm.CmdList["ImportCurrentAccountAgingCommand"] == null)
            {
                pm.CmdList.AddCmd(
                    324,
                    "ImportCurrentAccountAgingCommand",
                    SLanguage.GetString("Yaşlandırma Tutarlarını Aktar"),
                    ImportCurrentAccountAgingCommand,
                    CanImportCurrentAccountAgingCommand);
            }

            if (pm.CmdList["ExportPaymentInstructionCommand"] == null)
            {
                pm.CmdList.AddCmd(
                    325,
                    "ExportPaymentInstructionCommand",
                    SLanguage.GetString("Talimat Dosyası Oluştur"),
                    ExportPaymentInstructionCommand,
                    CanExportPaymentInstructionCommand);
            }
        }

        /// <summary>
        /// Yaşlandırma tutarlarını aktar komutunun çalıştırılabilir olup olmadığını kontrol eder.
        /// </summary>
        /// <param name="obj">Komut parametreleri.</param>
        /// <returns>Ödeme planlama bağlamında ve yetki varsa true.</returns>
        bool CanImportCurrentAccountAgingCommand(ISysCommandParam obj)
        {
            if (_paymentOrderBankReceiptPm == null || !IsPaymentOrderPm(_paymentOrderBankReceiptPm)) return false;
            if (BankReceiptPaymentOrderHelper.ShouldLockPaymentOrder(_paymentOrderBankReceiptPm.ActiveBO)) return false;

            return SysMng.Instance.CheckRights(
                OperationType.Update,
                (short)Modules.ExternalModule16,
                (short)Modules.ExternalModule16,
                (short)CashFlowManagementModuleSecurityItems.CurrentAccountAgingImport,
                (short)CashFlowManagementModuleSecuritySubItems.None);
        }

        /// <summary>
        /// Yaşlandırma raporunu modal önizleme ekranında açar; seçilen satırlar fişe aktarılır.
        /// </summary>
        /// <param name="obj">Komut parametreleri.</param>
        void ImportCurrentAccountAgingCommand(ISysCommandParam obj)
        {
            if (_paymentOrderBankReceiptPm == null || !IsPaymentOrderPm(_paymentOrderBankReceiptPm)) return;

            var paymentOrderPm = _paymentOrderBankReceiptPm as PaymentOrderBankReceiptPM;
            if (paymentOrderPm == null) return;

            BusinessObjectBase businessObject = _paymentOrderBankReceiptPm.ActiveBO as BusinessObjectBase;
            if (businessObject?.CurrentRow?.Row == null) return;

            LiveSession session = SysMng.Instance.getSession() as LiveSession;
            if (session?.ActiveCompany?.RecId == null) return;

            paymentOrderPm.EnsureDefaultBankAccountResolved();

            DateTime reportDate = paymentOrderPm.AgingReportDate;
            if (reportDate == DateTime.MinValue)
            {
                reportDate = businessObject.CurrentRow.Row.IsNull("ReceiptDate")
                    ? DateTime.Today
                    : Convert.ToDateTime(businessObject.CurrentRow.Row["ReceiptDate"]);
            }

            var context = new PaymentOrderAgingImportContext
            {
                ReportDate = reportDate.Date,
                StartCurrentAccountCode = paymentOrderPm.AgingStartCurrentAccountCode,
                EndCurrentAccountCode = paymentOrderPm.AgingEndCurrentAccountCode,
                DefaultBankAccountId = paymentOrderPm.DefaultBankAccountId,
                DefaultBankAccountCode = paymentOrderPm.DefaultBankAccountCode,
                ImportDirectlyToReceipt = paymentOrderPm.ImportAgingDirectlyToReceipt
            };
            context.RefreshDefaultBankAccount = () =>
            {
                paymentOrderPm.RefreshDefaultBankAccountForImport();
                context.DefaultBankAccountId = paymentOrderPm.DefaultBankAccountId;
                context.DefaultBankAccountCode = paymentOrderPm.DefaultBankAccountCode;
            };

            if (paymentOrderPm.ImportAgingDirectlyToReceipt)
            {
                ExecuteDirectAgingImport(context, businessObject);
                return;
            }

            var previewPm = new PaymentOrderAgingImportPreviewPM(_container);

            previewPm.Init("PaymentOrderAgingImportPreviewViewW");
            previewPm.Initialize(context, businessObject, session);

            SysMng.Instance.ActWndMng.ShowWnd(
                previewPm,
                true,
                SLanguage.GetString("Yaşlandırma Önizleme"),
                Sentez.Common.InformationMessages.WindowStyle.SingleBorderWindow,
                1100,
                650,
                Sentez.Common.InformationMessages.ResizeMode.CanResize,
                9999,
                9999,
                false,
                SizeToContent.Manual,
                true);

            if (previewPm.WasImported)
                RefreshPaymentOrderDetailGrid();
        }

        /// <summary>
        /// Yaşlandırma rapor sonucunu doğrudan fiş detayına aktarır.
        /// </summary>
        /// <param name="context">Aktarım parametreleri.</param>
        /// <param name="businessObject">Hedef fiş BO.</param>
        void ExecuteDirectAgingImport(PaymentOrderAgingImportContext context, BusinessObjectBase businessObject)
        {
            CurrentAccountAgingReportDataResult reportData = CurrentAccountAgingReportDataService.LoadAgingData(
                _container,
                context.ReportDate,
                context.StartCurrentAccountCode,
                context.EndCurrentAccountCode);

            if (!reportData.IsSuccess)
            {
                SysMng.Instance.ActWndMng.ShowMsg(
                    string.IsNullOrWhiteSpace(reportData.ErrorMessage)
                        ? SLanguage.GetString("Yaşlandırma verisi alınamadı.")
                        : reportData.ErrorMessage,
                    ConstantStr.Warning);
                return;
            }

            var rows = new List<DataRow>();
            if (reportData.Data != null)
            {
                foreach (DataRow row in reportData.Data.Rows)
                {
                    if (row.RowState != DataRowState.Deleted)
                        rows.Add(row);
                }
            }

            context.RefreshDefaultBankAccount?.Invoke();

            CurrentAccountAgingImportResult importResult = CurrentAccountAgingImportService.ImportSelectedRows(
                businessObject,
                context.ReportDate,
                context.DefaultBankAccountId,
                rows,
                reportData.AmountColumnName,
                context.DefaultBankAccountCode);

            if (!string.IsNullOrEmpty(importResult.Message))
            {
                SysMng.Instance.ActWndMng.ShowMsg(
                    importResult.Message,
                    importResult.AddedCount > 0 || importResult.UpdatedCount > 0 ? null : ConstantStr.Warning);
            }

            if (importResult.AddedCount > 0 || importResult.UpdatedCount > 0)
                RefreshPaymentOrderDetailGrid();
        }

        /// <summary>
        /// Kredi kartı harcamalarını aktar komutunun çalıştırılabilir olup olmadığını kontrol eder.
        /// </summary>
        /// <param name="obj">Komut parametreleri.</param>
        /// <returns>Ödeme planlama bağlamında ve yetki varsa true.</returns>
        bool CanImportCreditCardStatementSpendingCommand(ISysCommandParam obj)
        {
            if (_paymentOrderBankReceiptPm == null || !IsPaymentOrderPm(_paymentOrderBankReceiptPm)) return false;
            if (BankReceiptPaymentOrderHelper.ShouldLockPaymentOrder(_paymentOrderBankReceiptPm.ActiveBO)) return false;

            return SysMng.Instance.CheckRights(
                OperationType.Update,
                (short)Modules.ExternalModule16,
                (short)Modules.ExternalModule16,
                (short)CashFlowManagementModuleSecurityItems.CreditCardStatementSpendingImport,
                (short)CashFlowManagementModuleSecuritySubItems.None);
        }

        /// <summary>
        /// Kredi kartı ekstre harcama toplamlarını fiş tarihine göre detay satırlarına aktarır.
        /// </summary>
        /// <param name="obj">Komut parametreleri.</param>
        void ImportCreditCardStatementSpendingCommand(ISysCommandParam obj)
        {
            if (_paymentOrderBankReceiptPm == null || !IsPaymentOrderPm(_paymentOrderBankReceiptPm)) return;

            BusinessObjectBase businessObject = _paymentOrderBankReceiptPm.ActiveBO as BusinessObjectBase;
            if (businessObject?.CurrentRow?.Row == null) return;

            LiveSession session = SysMng.Instance.getSession() as LiveSession;
            if (session?.ActiveCompany?.RecId == null) return;

            DateTime receiptDate = businessObject.CurrentRow.Row.IsNull("ReceiptDate")
                ? DateTime.Today
                : Convert.ToDateTime(businessObject.CurrentRow.Row["ReceiptDate"]);

            CreditCardStatementSpendingImportResult importResult = CreditCardStatementSpendingImportService.Import(
                businessObject,
                session,
                receiptDate);

            if (!string.IsNullOrEmpty(importResult.Message))
            {
                SysMng.Instance.ActWndMng.ShowMsg(
                    importResult.Message,
                    importResult.AddedCount > 0 || importResult.UpdatedCount > 0 ? null : ConstantStr.Warning);
            }

            RefreshPaymentOrderDetailGrid();
        }

        /// <summary>
        /// Tekrar eden ödemeleri aktar komutunun çalıştırılabilir olup olmadığını kontrol eder.
        /// </summary>
        /// <param name="obj">Komut parametreleri.</param>
        /// <returns>Ödeme planlama bağlamında ve yetki varsa true.</returns>
        bool CanImportFixedPaymentsCommand(ISysCommandParam obj)
        {
            if (_paymentOrderBankReceiptPm == null || !IsPaymentOrderPm(_paymentOrderBankReceiptPm)) return false;
            if (BankReceiptPaymentOrderHelper.ShouldLockPaymentOrder(_paymentOrderBankReceiptPm.ActiveBO)) return false;

            return SysMng.Instance.CheckRights(
                OperationType.Update,
                (short)Modules.ExternalModule16,
                (short)Modules.ExternalModule16,
                (short)CashFlowManagementModuleSecurityItems.FixedPaymentImport,
                (short)CashFlowManagementModuleSecuritySubItems.None);
        }

        /// <summary>
        /// Sabit ödeme tanımlarından fiş tarihine uygun satırları detay gridine aktarır.
        /// </summary>
        /// <param name="obj">Komut parametreleri.</param>
        void ImportFixedPaymentsCommand(ISysCommandParam obj)
        {
            if (_paymentOrderBankReceiptPm == null || !IsPaymentOrderPm(_paymentOrderBankReceiptPm)) return;

            var paymentOrderPm = _paymentOrderBankReceiptPm as PaymentOrderBankReceiptPM;
            if (paymentOrderPm == null) return;

            BusinessObjectBase businessObject = _paymentOrderBankReceiptPm.ActiveBO as BusinessObjectBase;
            if (businessObject?.CurrentRow?.Row == null) return;

            LiveSession session = SysMng.Instance.getSession() as LiveSession;
            if (session?.ActiveCompany?.RecId == null) return;

            paymentOrderPm.EnsureDefaultBankAccountResolved();

            DateTime receiptDate = businessObject.CurrentRow.Row.IsNull("ReceiptDate")
                ? DateTime.Today
                : Convert.ToDateTime(businessObject.CurrentRow.Row["ReceiptDate"]);

            FixedPaymentImportResult importResult = FixedPaymentImportService.Import(
                businessObject,
                session.ActiveCompany.RecId.Value,
                receiptDate,
                paymentOrderPm.DefaultBankAccountId);

            if (!string.IsNullOrEmpty(importResult.Message))
                SysMng.Instance.ActWndMng.ShowMsg(importResult.Message, importResult.AddedCount > 0 ? null : ConstantStr.Warning);

            RefreshPaymentOrderDetailGrid();
        }

        /// <summary>
        /// Talimat dosyası oluştur komutunun çalıştırılabilir olup olmadığını kontrol eder.
        /// </summary>
        bool CanExportPaymentInstructionCommand(ISysCommandParam obj)
        {
            if (_paymentOrderBankReceiptPm == null || !IsPaymentOrderPm(_paymentOrderBankReceiptPm)) return false;

            var paymentOrderPm = _paymentOrderBankReceiptPm as PaymentOrderBankReceiptPM;
            if (paymentOrderPm == null || !paymentOrderPm.EnsureDefaultBankAccountResolved()) return false;

            return GetSelectedPaymentOrderExportRows().Count > 0;
        }

        /// <summary>
        /// Ödeme planlama fişindeki seçili detay satırlarından banka talimat Excel dosyası üretir.
        /// </summary>
        void ExportPaymentInstructionCommand(ISysCommandParam obj)
        {
            if (_paymentOrderBankReceiptPm == null || !IsPaymentOrderPm(_paymentOrderBankReceiptPm)) return;

            var paymentOrderPm = _paymentOrderBankReceiptPm as PaymentOrderBankReceiptPM;
            if (paymentOrderPm == null) return;

            BusinessObjectBase businessObject = _paymentOrderBankReceiptPm.ActiveBO as BusinessObjectBase;
            if (businessObject?.CurrentRow?.Row == null) return;

            LiveSession session = SysMng.Instance.getSession() as LiveSession;
            if (session?.ActiveCompany?.RecId == null) return;

            if (!paymentOrderPm.EnsureDefaultBankAccountResolved())
            {
                SysMng.Instance.ActWndMng.ShowMsg(
                    SLanguage.GetString("Lütfen ön değer banka hesabını seçiniz."),
                    ConstantStr.Warning);
                return;
            }

            List<DataRow> selectedExportRows = GetSelectedPaymentOrderExportRows();
            if (selectedExportRows.Count == 0)
            {
                SysMng.Instance.ActWndMng.ShowMsg(
                    SLanguage.GetString("Lütfen talimat dosyasına aktarılacak satırları seçiniz."),
                    ConstantStr.Warning);
                return;
            }

            var saveDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Excel Dosyası (*.xlsx)|*.xlsx",
                DefaultExt = ".xlsx",
                FileName = PaymentOrderInstructionExportService.BuildSuggestedFileName(businessObject.CurrentRow.Row)
            };

            if (saveDialog.ShowDialog() != true)
                return;

            PaymentOrderInstructionExportResult exportResult = PaymentOrderInstructionExportService.Export(
                businessObject,
                session,
                paymentOrderPm.DefaultBankAccountId,
                selectedExportRows,
                saveDialog.FileName);

            if (!string.IsNullOrEmpty(exportResult.Message))
            {
                SysMng.Instance.ActWndMng.ShowMsg(
                    exportResult.Message,
                    exportResult.Succeeded ? null : ConstantStr.Warning);
            }
        }

        /// <summary>
        /// Fiş başlığı onay değiştir komutunun çalıştırılabilir olup olmadığını kontrol eder.
        /// </summary>
        /// <param name="obj">Komut parametreleri.</param>
        /// <returns>Onay değiştirilebiliyorsa true.</returns>
        bool PaymentOrderCanApprovedChangeCommand(ISysCommandParam obj)
        {
            if (_paymentOrderBankReceiptPm == null || !IsPaymentOrderPm(_paymentOrderBankReceiptPm)) return false;
            return BankReceiptPaymentOrderApprovalHelper.CanToggleHeaderApproval(_paymentOrderBankReceiptPm);
        }

        /// <summary>
        /// Ödeme planlama fişi başlık onay durumunu değiştirir ve detay gridini yeniler.
        /// </summary>
        /// <param name="obj">Komut parametreleri.</param>
        void PaymentOrderOnApprovedChangeCommand(ISysCommandParam obj)
        {
            if (_paymentOrderBankReceiptPm == null || !IsPaymentOrderPm(_paymentOrderBankReceiptPm)) return;

            BankReceiptPaymentOrderApprovalHelper.ExecuteHeaderApprovalToggle(_paymentOrderBankReceiptPm, obj);
            RefreshPaymentOrderDetailGrid();
        }

        /// <summary>
        /// Detay gridde seçili satırları onaylar ve onay meta verilerini günceller.
        /// </summary>
        /// <param name="obj">Komut parametreleri.</param>
        void PaymentOrderBulkLineApproveCommand(ISysCommandParam obj)
        {
            if (_paymentOrderBankReceiptPm == null || !IsPaymentOrderPm(_paymentOrderBankReceiptPm)) return;

            LiveGridControl gridDetail = _paymentOrderBankReceiptPm.FCtrl("gridDetail") as LiveGridControl;
            if (gridDetail?.SelectedItems == null || gridDetail.SelectedItems.Count == 0)
            {
                SysMng.Instance.ActWndMng.ShowMsg(
                    SLanguage.GetString("Lütfen işlem yapılacak satırları seçiniz."),
                    ConstantStr.Warning);
                return;
            }

            long? userId = SysMng.Instance.getSession()?.ActiveUser?.RecId;
            DateTime approvedAt = new DateHelper().GetToday();

            foreach (object selectedItem in gridDetail.SelectedItems)
            {
                if (selectedItem is not DataRowView rowView) continue;
                if (rowView.Row.RowState == DataRowState.Deleted) continue;

                DataRow itemRow = rowView.Row;
                itemRow["IsApproved"] = (byte)1;
                BankReceiptPaymentOrderHelper.SetLineApprovedMetadata(itemRow, true, userId, approvedAt);
            }

            RefreshPaymentOrderDetailGrid();
        }

        /// <summary>
        /// Detay gridde seçili satırların onayını kaldırır; yetki yoksa istisna fırlatır.
        /// </summary>
        /// <param name="obj">Komut parametreleri.</param>
        void PaymentOrderBulkLineUnapproveCommand(ISysCommandParam obj)
        {
            if (_paymentOrderBankReceiptPm == null || !IsPaymentOrderPm(_paymentOrderBankReceiptPm)) return;

            if (!BankReceiptPaymentOrderHelper.HasLineApprovedEditRight())
                throw new LiveCommandItemException(PaymentOrderTerminology.LineApprovalDeniedMessage);

            LiveGridControl gridDetail = _paymentOrderBankReceiptPm.FCtrl("gridDetail") as LiveGridControl;
            if (gridDetail?.SelectedItems == null || gridDetail.SelectedItems.Count == 0)
            {
                SysMng.Instance.ActWndMng.ShowMsg(
                    SLanguage.GetString("Lütfen işlem yapılacak satırları seçiniz."),
                    ConstantStr.Warning);
                return;
            }

            foreach (object selectedItem in gridDetail.SelectedItems)
            {
                if (selectedItem is not DataRowView rowView) continue;
                if (rowView.Row.RowState == DataRowState.Deleted) continue;

                DataRow itemRow = rowView.Row;
                if (BankReceiptPaymentOrderHelper.GetApprovedValue(itemRow) == 0
                    && BankReceiptPaymentOrderHelper.GetPersistedApprovedValue(itemRow) == 0)
                    continue;

                itemRow["IsApproved"] = (byte)0;
                BankReceiptPaymentOrderHelper.SetLineApprovedMetadata(itemRow, false, null, null);
            }

            RefreshPaymentOrderDetailGrid();
        }
    }
}
