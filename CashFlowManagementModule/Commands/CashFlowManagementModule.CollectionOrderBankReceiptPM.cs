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
    /// BankReceiptPM için tahsilat planlama (fiş tipi 20) ekran kancaları, toolbar,
    /// onay komutları ve toplu aktarım işlemlerini yöneten modül parçası.
    /// </summary>
    public partial class CashFlowManagementModule
    {
        /// <summary>Tahsilat planlama fişi PM örneğine bağlanan aktif BankReceiptPM referansı.</summary>
        BankReceiptPM _collectionOrderBankReceiptPm;

        /// <summary>Tahsilat planlama komut ve UI kancalarının yalnızca bir kez uygulanmasını sağlar.</summary>
        bool _collectionOrderHooksApplied;

        /// <summary>Detay grid üstüne eklenen tahsilat planlama toolbar panelinin adı.</summary>
        const string CollectionOrderLineApprovalToolbarName = "CollectionOrderLineApprovalToolbar";

        /// <summary>Kredi kartı tahsilat uyarı mesajlarının gösterildiği etiket adı.</summary>
        const string CollectionOrderCreditCardValidationLabelName = "CollectionOrderCreditCardValidationLabel";

        /// <summary>Toolbar altındaki yaşlandırma aktarım parametre panelinin adı.</summary>
        const string CollectionOrderAgingImportParamsPanelName = "CollectionOrderAgingImportParamsPanel";

        /// <summary>Yaşlandırma parametre panelinde Tab sırası için başlangıç değeri.</summary>
        const int CollectionOrderAgingParamTabIndexBase = 100;

        const string CollectionOrderDefaultBankAccountCodeFieldName = "TxtCollectionOrderDefaultBankAccountCode";
        const string CollectionOrderAgingStartCurrentAccountCodeFieldName = "TxtCollectionOrderAgingStartCurrentAccountCode";
        const string CollectionOrderAgingEndCurrentAccountCodeFieldName = "TxtCollectionOrderAgingEndCurrentAccountCode";

        /// <summary>
        /// BankReceiptPM init, dispose ve view loaded olaylarına tahsilat planlama kancalarını kaydeder.
        /// RegisterBankReceiptPmHooks tarafından çağrılır.
        /// </summary>
        void RegisterCollectionOrderBankReceiptPmHooks()
        {
            PMBase.AddCustomInit("BankReceiptPM", CollectionOrderBankReceiptPm_Init);
            PMBase.AddCustomDispose("BankReceiptPM", CollectionOrderBankReceiptPm_Dispose);
            PMBase.AddCustomViewLoaded("BankReceiptPM", CollectionOrderBankReceiptPm_ViewLoaded);
        }

        /// <summary>
        /// Verilen PM örneğinin tahsilat planlama fişi bağlamında çalışıp çalışmadığını döndürür.
        /// </summary>
        /// <param name="pm">Kontrol edilecek PM örneği.</param>
        /// <returns>Tahsilat planlama fişi ise true, aksi halde false.</returns>
        bool IsCollectionOrderPm(PMBase pm)
        {
            return BankReceiptCollectionOrderHelper.IsCollectionOrderContext(pm);
        }

        /// <summary>
        /// Tahsilat planlama komutlarını ve onay değişim komutunu PM üzerine bir kez bağlar.
        /// </summary>
        /// <param name="pm">Kanca uygulanacak BankReceiptPM örneği.</param>
        /// <returns>Kancalar uygulandıysa veya daha önce uygulanmışsa true döner.</returns>
        bool TryApplyCollectionOrderHooks(BankReceiptPM pm)
        {
            if (_collectionOrderHooksApplied || pm == null) return _collectionOrderHooksApplied;

            if (!BankReceiptCollectionOrderHelper.IsCollectionOrderContext(pm)) return false;

            EnsureBankReceiptApprovedChangeCommandRegistered();
            HookCollectionOrderCommands(pm);
            RefreshCollectionOrderApprovedChangeContextMenuCommand();
            _collectionOrderHooksApplied = true;
            return true;
        }

        /// <summary>
        /// BankReceiptPM açılışında BO olaylarını bağlar ve tahsilat planlama kancalarını etkinleştirir.
        /// </summary>
        /// <param name="pm">Başlatılan PM örneği.</param>
        /// <param name="parameter">PM başlatma parametreleri.</param>
        void CollectionOrderBankReceiptPm_Init(PMBase pm, PmParam parameter)
        {
            _collectionOrderBankReceiptPm = BankReceiptPmAccess.GetBankReceiptPm(pm);
            if (_collectionOrderBankReceiptPm?.ActiveBO == null) return;

            _collectionOrderBankReceiptPm.ActiveBO.ColumnChanged += CollectionOrderBankReceiptPm_ActiveBO_ColumnChanged;
            _collectionOrderBankReceiptPm.ActiveBO.AfterGet += CollectionOrderBankReceiptPm_ActiveBO_AfterGet;
            _collectionOrderBankReceiptPm.ActiveBO.PropertyChanged += CollectionOrderBankReceiptPm_ActiveBO_PropertyChanged;

            TryApplyCollectionOrderHooks(_collectionOrderBankReceiptPm);
        }

        /// <summary>
        /// BankReceiptPM kapanışında bağlanan olayları çözer ve tahsilat planlama durumunu sıfırlar.
        /// </summary>
        /// <param name="pm">Kapatılan PM örneği.</param>
        /// <param name="parameter">PM kapatma parametreleri.</param>
        void CollectionOrderBankReceiptPm_Dispose(PMBase pm, PmParam parameter)
        {
            if (_collectionOrderBankReceiptPm?.ActiveBO != null)
            {
                _collectionOrderBankReceiptPm.ActiveBO.ColumnChanged -= CollectionOrderBankReceiptPm_ActiveBO_ColumnChanged;
                _collectionOrderBankReceiptPm.ActiveBO.AfterGet -= CollectionOrderBankReceiptPm_ActiveBO_AfterGet;
                _collectionOrderBankReceiptPm.ActiveBO.PropertyChanged -= CollectionOrderBankReceiptPm_ActiveBO_PropertyChanged;
            }

            _collectionOrderBankReceiptPm = null;
            _collectionOrderHooksApplied = false;
        }

        /// <summary>
        /// Tahsilat planlama ekranı yüklendiğinde kolon, toolbar ve onay arayüzünü hazırlar.
        /// </summary>
        /// <param name="sender">Olay kaynağı.</param>
        /// <param name="e">Routed event argümanları.</param>
        void CollectionOrderBankReceiptPm_ViewLoaded(object sender, RoutedEventArgs e)
        {
            if (_collectionOrderBankReceiptPm == null) return;

            if (!TryApplyCollectionOrderHooks(_collectionOrderBankReceiptPm)) return;

            _collectionOrderBankReceiptPm.ActiveViewControl?.Dispatcher.BeginInvoke(
                new Action(() =>
                {
                    if (BankReceiptCollectionOrderHelper.IsCollectionOrderContext(_collectionOrderBankReceiptPm))
                    {
                        var session = SysMng.Instance.getSession();
                        if (session?.LookupList != null)
                            MetaFixedPaymentTypeHelper.RefreshLookupList(session.LookupList);
                        MetaFixedPaymentTypeHelper.RefreshLookupList(_collectionOrderBankReceiptPm.Lists);
                    }

                    AddCollectionOrderDetailColumns();
                    EnsureCollectionOrderLineApprovalToolbar();
                    EnsureCollectionOrderAgingImportParamsPanel();
                    SyncCollectionOrderAgingReportDate();
                    ApplyCollectionOrderCardLockState();
                    ApplyCollectionOrderApprovalColumnAccess();
                    ApplyCollectionOrderApprovalContextMenuAccess();
                    RefreshCollectionOrderApprovedChangeContextMenuCommand();
                }),
                DispatcherPriority.Loaded);
        }

        /// <summary>
        /// Fiş kaydı yüklendikten sonra tahsilat planlama arayüz bileşenlerini yeniden uygular.
        /// </summary>
        /// <param name="sender">Olay kaynağı.</param>
        /// <param name="e">Olay argümanları.</param>
        void CollectionOrderBankReceiptPm_ActiveBO_AfterGet(object sender, EventArgs e)
        {
            if (_collectionOrderBankReceiptPm == null) return;

            if (!TryApplyCollectionOrderHooks(_collectionOrderBankReceiptPm)) return;

            _collectionOrderBankReceiptPm.ActiveViewControl?.Dispatcher.BeginInvoke(
                new Action(() =>
                {
                    if (BankReceiptCollectionOrderHelper.IsCollectionOrderContext(_collectionOrderBankReceiptPm))
                    {
                        var session = SysMng.Instance.getSession();
                        if (session?.LookupList != null)
                            MetaFixedPaymentTypeHelper.RefreshLookupList(session.LookupList);
                        MetaFixedPaymentTypeHelper.RefreshLookupList(_collectionOrderBankReceiptPm.Lists);
                    }

                    AddCollectionOrderDetailColumns();
                    EnsureCollectionOrderLineApprovalToolbar();
                    EnsureCollectionOrderAgingImportParamsPanel();
                    SyncCollectionOrderAgingReportDate();
                    ApplyCollectionOrderCardLockState();
                    ApplyCollectionOrderApprovalColumnAccess();
                    ApplyCollectionOrderApprovalContextMenuAccess();
                    RefreshCollectionOrderApprovedChangeContextMenuCommand();
                    RefreshCollectionOrderDetailGrid();
                }),
                DispatcherPriority.Background);
        }

        /// <summary>
        /// Fiş başlığı veya satır değişiminde tahsilat planlama kart kilidini günceller.
        /// </summary>
        /// <param name="sender">Olay kaynağı.</param>
        /// <param name="e">PropertyChanged olay argümanları.</param>
        void CollectionOrderBankReceiptPm_ActiveBO_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (_collectionOrderBankReceiptPm == null || !IsCollectionOrderPm(_collectionOrderBankReceiptPm)) return;

            if (e.PropertyName == "IsNewRecord" || e.PropertyName == "CurrentRow")
            {
                ApplyCollectionOrderCardLockState();
                if (e.PropertyName == "CurrentRow")
                    BankReceiptItemAccessCodeHelper.ApplyDetailGridFilter(_collectionOrderBankReceiptPm);
            }
        }

        /// <summary>
        /// Detay satırı veya başlık kolon değişiminde grid, kilit ve kredi kartı doğrulamasını yeniler.
        /// </summary>
        /// <param name="sender">Olay kaynağı.</param>
        /// <param name="e">Kolon değişim olay argümanları.</param>
        void CollectionOrderBankReceiptPm_ActiveBO_ColumnChanged(object sender, DataColumnChangeEventArgs e)
        {
            if (_collectionOrderBankReceiptPm == null) return;

            if (e.Row.Table.TableName == "Erp_BankReceiptItem" && e.Column.ColumnName == "IsApproved")
            {
                RefreshCollectionOrderDetailGrid();
            }
            else if (e.Row.Table.TableName == "Erp_BankReceiptItem" && e.Column.ColumnName == BankReceiptItemAccessCodeHelper.FieldAccessCode)
            {
                BankReceiptItemAccessCodeHelper.ApplyDetailGridFilter(_collectionOrderBankReceiptPm);
            }
            else if (e.Row.Table.TableName == "Erp_BankReceipt" && e.Column.ColumnName == "IsApproved")
            {
                ApplyCollectionOrderCardLockState();
                RefreshCollectionOrderDetailGrid();
            }
            else if (e.Row.Table.TableName == "Erp_BankReceipt" && e.Column.ColumnName == "ReceiptDate")
            {
                SyncCollectionOrderAgingReportDate();
            }
            else if (e.Row.Table.TableName == "Erp_BankReceiptItem" && e.Column.ColumnName == "ReceiptDate")
            {
                if (BankReceiptCollectionOrderHelper.IsCollectionOrderContext(_collectionOrderBankReceiptPm)
                    && _collectionOrderBankReceiptPm.ActiveBO is BusinessObjectBase businessObject)
                {
                    BankReceiptCollectionOrderHelper.ProtectItemPaymentDateAfterReceiptDateChange(businessObject, e.Row);
                }
            }
            else if (e.Row.Table.TableName == "Erp_BankReceiptItem"
                     && (e.Column.ColumnName == "BankAccountId"
                         || e.Column.ColumnName == "UD_PaymentDate"
                         || e.Column.ColumnName == "Debit"
                         || e.Column.ColumnName == BankReceiptCreditCardHelper.FieldInstallmentCount))
            {
                NormalizeInstallmentCount(e.Row);
                RefreshCollectionOrderCreditCardValidationMessage(e.Row);
            }
        }

        /// <summary>
        /// Seçili satır için kredi kartı tahsilat uyarısını hesaplar ve ekran etiketini günceller.
        /// </summary>
        /// <param name="itemRow">Doğrulanacak fiş satırı.</param>
        void RefreshCollectionOrderCreditCardValidationMessage(DataRow itemRow)
        {
            if (_collectionOrderBankReceiptPm == null || itemRow == null) return;
            if (itemRow.IsNull("BankAccountId")) return;

            LiveSession session = SysMng.Instance.getSession() as LiveSession;
            if (session == null) return;

            BusinessObjectBase businessObject = _collectionOrderBankReceiptPm.ActiveBO as BusinessObjectBase;
            if (businessObject?.Connection == null) return;

            DateTime? fallbackDate = null;
            if (DataRowSafety.TryGetCurrentRow(_collectionOrderBankReceiptPm.ActiveBO, out DataRow headerRow)
                && headerRow.Table.Columns.Contains("UD_PaymentDate")
                && !headerRow.IsNull("UD_PaymentDate"))
            {
                fallbackDate = Convert.ToDateTime(headerRow["UD_PaymentDate"]);
            }

            DateTime paymentDate = fallbackDate ?? DateTime.Today;
            if (itemRow.Table.Columns.Contains("UD_PaymentDate") && !itemRow.IsNull("UD_PaymentDate"))
                paymentDate = Convert.ToDateTime(itemRow["UD_PaymentDate"]).Date;

            decimal amount = 0m;
            if (!itemRow.IsNull("Debit"))
                amount = Convert.ToDecimal(itemRow["Debit"]);

            decimal? forexAmount = null;
            if (itemRow.Table.Columns.Contains("ForexDebit") && !itemRow.IsNull("ForexDebit"))
                forexAmount = Convert.ToDecimal(itemRow["ForexDebit"]);

            var line = new CreditCardPaymentLineInput
            {
                BankReceiptItemId = itemRow.IsNull("RecId") ? 0L : Convert.ToInt64(itemRow["RecId"]),
                BankAccountId = Convert.ToInt64(itemRow["BankAccountId"]),
                PaymentReferenceDate = paymentDate,
                Amount = amount,
                ForexAmount = forexAmount,
                InstallmentCount = BankReceiptCreditCardHelper.GetInstallmentCount(itemRow)
            };

            CreditCardPaymentValidationResult result = CreditCardPaymentWarningService.ValidateLinePreview(
                businessObject.Provider,
                businessObject.Connection,
                businessObject.Transaction,
                session.ActiveCompany.RecId ?? 0,
                line);
            UpdateCollectionOrderCreditCardValidationLabel(result);
        }

        /// <summary>
        /// Kredi kartı doğrulama sonucunu toolbar üzerindeki uyarı etiketine yansıtır.
        /// </summary>
        /// <param name="result">Doğrulama sonucu; null ise etiket temizlenir.</param>
        void UpdateCollectionOrderCreditCardValidationLabel(CreditCardPaymentValidationResult result)
        {
            if (_collectionOrderBankReceiptPm?.ActiveViewControl == null) return;

            TextBlock label = _collectionOrderBankReceiptPm.ActiveViewControl.FindName(CollectionOrderCreditCardValidationLabelName) as TextBlock;
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
        /// Tahsilat planlama detay gridine onay, taksit ve sabit tahsilat tipi kolonlarını ekler.
        /// </summary>
        void AddCollectionOrderDetailColumns()
        {
            if (_collectionOrderBankReceiptPm?.BankReceiptColumnCollection == null) return;

            AddCollectionOrderColumnIfMissing("IsApproved", SLanguage.GetString("Onay Durumu"), EditorType.ComboBox, FieldUsage.None, 90, "ApprovedList");
            AddCollectionOrderColumnIfMissing(BankReceiptCreditCardHelper.FieldInstallmentCount, SLanguage.GetString("Taksit Sayısı"), EditorType.TextEditor, FieldUsage.None, 90);
            AddCollectionOrderColumnIfMissing(PosCardClassificationHelper.FieldCardSource, SLanguage.GetString("Kart Kaynağı"), EditorType.ComboBox, FieldUsage.None, 150, PosCardClassificationHelper.CardSourceLookupName);
            AddCollectionOrderColumnIfMissing(PosCardClassificationHelper.FieldCardCategory, SLanguage.GetString("Kart Kategorisi"), EditorType.ComboBox, FieldUsage.None, 170, PosCardClassificationHelper.CardCategoryLookupName);
            BankReceiptItemAccessCodeHelper.EnsureDetailColumn(_collectionOrderBankReceiptPm.BankReceiptColumnCollection);
            EnsureFixedCollectionTypeDetailColumn();
            BankReceiptItemAuditHelper.AddAuditDetailColumns(_collectionOrderBankReceiptPm.BankReceiptColumnCollection);

            ReceiptColumnCollection columns = _collectionOrderBankReceiptPm.BankReceiptColumnCollection;
            _collectionOrderBankReceiptPm.BankReceiptColumnCollection = columns;

            ApplyCollectionOrderApprovalColumnAccess();
            RefreshCollectionOrderDetailGridColumns();
        }

        /// <summary>
        /// Detay grid kolon tanımlarını audit kolonları dahil olacak şekilde yeniden uygular.
        /// </summary>
        void RefreshCollectionOrderDetailGridColumns()
        {
            if (_collectionOrderBankReceiptPm == null || !IsCollectionOrderPm(_collectionOrderBankReceiptPm)) return;
            if (_collectionOrderBankReceiptPm.BankReceiptColumnCollection == null) return;

            LiveGridControl gridDetail = _collectionOrderBankReceiptPm.FCtrl("gridDetail") as LiveGridControl;
            if (gridDetail == null) return;

            gridDetail.SaveLayoutType = $"{BankReceiptCollectionOrderHelper.ReceiptType}-cfm-audit";
            gridDetail.ColumnDefinitions = _collectionOrderBankReceiptPm.BankReceiptColumnCollection;
            gridDetail.ApplyReceiptColumnDefinitions();
            gridDetail.RefreshData();
            BankReceiptItemAccessCodeHelper.ApplyDetailGridFilter(_collectionOrderBankReceiptPm);
        }

        /// <summary>
        /// Sabit tahsilat tipi kolonunu oluşturur veya mevcut kolon özelliklerini günceller.
        /// </summary>
        void EnsureFixedCollectionTypeDetailColumn()
        {
            if (_collectionOrderBankReceiptPm?.BankReceiptColumnCollection == null) return;

            string columnName = BankReceiptFixedPaymentHelper.FieldFixedPaymentTypeId;
            ReceiptColumn column = _collectionOrderBankReceiptPm.BankReceiptColumnCollection
                .FirstOrDefault(c => c.ColumnName == columnName);

            if (column == null)
            {
                column = new ReceiptColumn
                {
                    ColumnName = columnName,
                    Caption = SLanguage.GetString("Tahsilat Tipi"),
                    EditorType = EditorType.ComboBox,
                    ComboLookup = MetaFixedPaymentTypeHelper.LookupListName,
                    ComboDisplayMember = "FixedPaymentTypeName",
                    ComboValueMember = "RecId",
                    Width = 220,
                    UsageType = FieldUsage.None,
                    IsVisible = true
                };
                _collectionOrderBankReceiptPm.BankReceiptColumnCollection.Add(column);
                return;
            }

            column.Caption = SLanguage.GetString("Tahsilat Tipi");
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
        void AddCollectionOrderColumnIfMissing(string columnName, string caption, EditorType editorType, FieldUsage usageType, int width, string comboLookup = null)
        {
            if (_collectionOrderBankReceiptPm.BankReceiptColumnCollection.Any(c => c.ColumnName == columnName))
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

            _collectionOrderBankReceiptPm.BankReceiptColumnCollection.Add(column);
        }

        /// <summary>
        /// Tahsilat planlama fişindeki rapor tarihini fiş tarihi ile senkronize eder.
        /// </summary>
        void SyncCollectionOrderAgingReportDate()
        {
            if (BankReceiptPmAccess.GetCollectionOrderPm(_collectionOrderBankReceiptPm) is CollectionOrderBankReceiptPM collectionOrderPm)
                collectionOrderPm.SyncAgingReportDateFromReceipt();
        }

        /// <summary>
        /// Toolbar altına yaşlandırma aktarım parametrelerini içeren dikey panel ekler.
        /// </summary>
        void EnsureCollectionOrderAgingImportParamsPanel()
        {
            if (_collectionOrderBankReceiptPm == null || !IsCollectionOrderPm(_collectionOrderBankReceiptPm)) return;

            var collectionOrderPm = BankReceiptPmAccess.GetCollectionOrderPm(_collectionOrderBankReceiptPm);
            if (collectionOrderPm == null) return;

            collectionOrderPm.LoadAgingImportWindowSettings();
            collectionOrderPm.EnsureDefaultBankAccountResolved();

            LiveGridControl gridDetail = _collectionOrderBankReceiptPm.FCtrl("gridDetail") as LiveGridControl;
            if (gridDetail?.Parent is not Grid parentGrid) return;
            if (parentGrid.FindName(CollectionOrderAgingImportParamsPanelName) != null) return;

            int insertRow = parentGrid.FindName(CollectionOrderLineApprovalToolbarName) != null ? 1 : 0;
            parentGrid.RowDefinitions.Insert(insertRow, new RowDefinition { Height = GridLength.Auto });

            foreach (UIElement child in parentGrid.Children)
            {
                int row = Grid.GetRow(child);
                if (row >= insertRow)
                    Grid.SetRow(child, row + 1);
            }

            var panel = new Grid
            {
                Name = CollectionOrderAgingImportParamsPanelName,
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

            int tabIndex = CollectionOrderAgingParamTabIndexBase;
            AddCollectionOrderAgingParamRow(panel, 0, SLanguage.GetString("Rapor Tarihi"), CreateCollectionOrderAgingReportDateEditor(collectionOrderPm, tabIndex++));
            AddCollectionOrderAgingParamRow(
                panel,
                1,
                SLanguage.GetString("Başlangıç Cari Kodu"),
                CreateCollectionOrderAgingCurrentAccountLookup(
                    CollectionOrderAgingStartCurrentAccountCodeFieldName,
                    nameof(CollectionOrderBankReceiptPM.AgingStartCurrentAccountCode),
                    "Erp_CurrentAccountCurrentAccountCodeList",
                    collectionOrderPm,
                    collectionOrderPm.OnAgingStartCurrentAccountCodeKeyDown,
                    tabIndex++));
            AddCollectionOrderAgingParamRow(
                panel,
                2,
                SLanguage.GetString("Bitiş Cari Kodu"),
                CreateCollectionOrderAgingCurrentAccountLookup(
                    CollectionOrderAgingEndCurrentAccountCodeFieldName,
                    nameof(CollectionOrderBankReceiptPM.AgingEndCurrentAccountCode),
                    "Erp_CurrentAccountCurrentAccountCodeList",
                    collectionOrderPm,
                    collectionOrderPm.OnAgingEndCurrentAccountCodeKeyDown,
                    tabIndex++));
            AddCollectionOrderAgingParamRow(
                panel,
                3,
                SLanguage.GetString("Ön Değer Banka Hesabı"),
                CreateCollectionOrderAgingDefaultBankAccountLookup(collectionOrderPm, tabIndex++));
            AddCollectionOrderAgingDirectImportRow(panel, 4, collectionOrderPm, tabIndex);

            Grid.SetRow(panel, insertRow);
            Grid.SetColumnSpan(panel, 4);
            parentGrid.Children.Add(panel);
            parentGrid.RegisterName(CollectionOrderAgingImportParamsPanelName, panel);
        }

        static void AddCollectionOrderAgingParamRow(Grid panel, int row, string caption, UIElement editor)
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

        static void AddCollectionOrderAgingDirectImportRow(Grid panel, int row, CollectionOrderBankReceiptPM collectionOrderPm, int tabIndex)
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
                new Binding(nameof(CollectionOrderBankReceiptPM.ImportAgingDirectlyToReceipt))
                {
                    Source = collectionOrderPm,
                    Mode = BindingMode.TwoWay,
                    UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                });

            Grid.SetRow(checkEdit, row);
            Grid.SetColumn(checkEdit, 0);
            Grid.SetColumnSpan(checkEdit, 2);
            panel.Children.Add(checkEdit);
        }

        static LiveDateEdit CreateCollectionOrderAgingReportDateEditor(CollectionOrderBankReceiptPM collectionOrderPm, int tabIndex)
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
                new Binding(nameof(CollectionOrderBankReceiptPM.AgingReportDate))
                {
                    Source = collectionOrderPm,
                    Mode = BindingMode.TwoWay,
                    UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                });
            return dateEdit;
        }

        static LiveLookUpEdit CreateCollectionOrderAgingCurrentAccountLookup(
            string name,
            string propertyName,
            string workListName,
            CollectionOrderBankReceiptPM collectionOrderPm,
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
                DataContext = collectionOrderPm
            };
            lookup.SetBinding(
                LiveLookUpEdit.TextProperty,
                new Binding(propertyName)
                {
                    Source = collectionOrderPm,
                    Mode = BindingMode.TwoWay,
                    UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                });
            lookup.PreviewKeyDown += keyDownHandler;
            return lookup;
        }

        static LiveLookUpEdit CreateCollectionOrderAgingDefaultBankAccountLookup(CollectionOrderBankReceiptPM collectionOrderPm, int tabIndex)
        {
            var lookup = new LiveLookUpEdit
            {
                Name = CollectionOrderDefaultBankAccountCodeFieldName,
                WorkListName = "Erp_BankAccountAccountCodeList",
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(2, 1, 0, 2),
                Width = 160,
                TabIndex = tabIndex,
                DataContext = collectionOrderPm
            };
            lookup.SetBinding(
                LiveLookUpEdit.TextProperty,
                new Binding(nameof(CollectionOrderBankReceiptPM.DefaultBankAccountCode))
                {
                    Source = collectionOrderPm,
                    Mode = BindingMode.TwoWay,
                    UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                });
            lookup.PreviewKeyDown += collectionOrderPm.OnDefaultBankAccountCodeKeyDown;
            return lookup;
        }

        /// <summary>
        /// Onay durumu kolonunun düzenlenebilir olmasını sağlar.
        /// </summary>
        void ApplyCollectionOrderApprovalColumnAccess()
        {
            if (_collectionOrderBankReceiptPm?.BankReceiptColumnCollection == null) return;

            ReceiptColumn approvalColumn = _collectionOrderBankReceiptPm.BankReceiptColumnCollection
                .FirstOrDefault(c => c.ColumnName == "IsApproved");
            if (approvalColumn == null) return;

            if (approvalColumn.IsReadOnly)
            {
                approvalColumn.IsReadOnly = false;
                ReceiptColumnCollection columns = _collectionOrderBankReceiptPm.BankReceiptColumnCollection;
                _collectionOrderBankReceiptPm.BankReceiptColumnCollection = columns;
            }
        }

        /// <summary>
        /// Satır onay işlemleri için bağlam menüsü erişimini günceller.
        /// </summary>
        void ApplyCollectionOrderApprovalContextMenuAccess()
        {
            BankReceiptCollectionOrderApprovalHelper.RefreshCollectionOrderApprovalUi(_collectionOrderBankReceiptPm);
        }

        /// <summary>
        /// Bağlam menüsündeki onay değiştir komutunu PM komut listesiyle eşler.
        /// </summary>
        void RefreshCollectionOrderApprovedChangeContextMenuCommand()
        {
            if (_collectionOrderBankReceiptPm?.contextMenu == null || _collectionOrderBankReceiptPm.CmdList == null) return;

            ISysCommand approvedChangeCommand = _collectionOrderBankReceiptPm.CmdList["ApprovedChangeCommand"];
            if (approvedChangeCommand == null) return;

            foreach (object item in _collectionOrderBankReceiptPm.contextMenu.Items)
            {
                if (item is MenuItem menuItem && menuItem.Name == "ApprovedChangeCommand")
                    menuItem.Command = approvedChangeCommand;
            }
        }

        /// <summary>
        /// Fiş onay durumuna göre tahsilat planlama ekranının düzenlenebilirliğini ayarlar.
        /// </summary>
        void ApplyCollectionOrderCardLockState()
        {
            if (!DataRowSafety.TryGetCurrentRow(_collectionOrderBankReceiptPm?.ActiveBO, out _)) return;

            bool isLocked = BankReceiptCollectionOrderHelper.ShouldLockCollectionOrder(_collectionOrderBankReceiptPm.ActiveBO);

            if (_collectionOrderBankReceiptPm is PMDesktop pmDesktop)
                pmDesktop.SetViewEnabled(!isLocked);
        }

        /// <summary>
        /// Detay grid verisini yeniden yükler.
        /// </summary>
        void RefreshCollectionOrderDetailGrid()
        {
            if (_collectionOrderBankReceiptPm == null) return;

            LiveGridControl gridDetail = _collectionOrderBankReceiptPm.FCtrl("gridDetail") as LiveGridControl;
            gridDetail?.RefreshData();
            BankReceiptItemAccessCodeHelper.ApplyDetailGridFilter(_collectionOrderBankReceiptPm);
        }

        /// <summary>
        /// Detay grid üstüne onay, aktarım butonları ve kredi kartı uyarı etiketini içeren toolbar ekler.
        /// </summary>
        void EnsureCollectionOrderLineApprovalToolbar()
        {
            if (_collectionOrderBankReceiptPm == null || !IsCollectionOrderPm(_collectionOrderBankReceiptPm)) return;

            LiveGridControl gridDetail = _collectionOrderBankReceiptPm.FCtrl("gridDetail") as LiveGridControl;
            if (gridDetail?.Parent is not Grid parentGrid) return;

            if (parentGrid.FindName(CollectionOrderLineApprovalToolbarName) != null) return;

            parentGrid.RowDefinitions.Insert(0, new RowDefinition { Height = GridLength.Auto });

            foreach (UIElement child in parentGrid.Children)
            {
                int row = Grid.GetRow(child);
                Grid.SetRow(child, row + 1);
            }

            var toolbar = new StackPanel
            {
                Name = CollectionOrderLineApprovalToolbarName,
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 4)
            };

            var btnApprove = new LiveButton
            {
                Content = SLanguage.GetString("Seçili Satırları Onayla"),
                Margin = new Thickness(0, 0, 6, 0),
                Padding = new Thickness(8, 2, 8, 2)
            };
            btnApprove.Command = _collectionOrderBankReceiptPm.CmdList["CollectionOrderBulkLineApproveCommand"];

            var btnUnapprove = new LiveButton
            {
                Content = SLanguage.GetString("Seçili Satırları Onaysız Yap"),
                Padding = new Thickness(8, 2, 8, 2)
            };
            btnUnapprove.Command = _collectionOrderBankReceiptPm.CmdList["CollectionOrderBulkLineUnapproveCommand"];

            toolbar.Children.Add(btnApprove);
            toolbar.Children.Add(btnUnapprove);

            var btnImportFixedCollections = new LiveButton
            {
                Content = SLanguage.GetString("Tekrar Eden Tahsilatları Aktar"),
                Margin = new Thickness(6, 0, 0, 0),
                Padding = new Thickness(8, 2, 8, 2)
            };
            btnImportFixedCollections.Command = _collectionOrderBankReceiptPm.CmdList["ImportFixedCollectionsCommand"];
            toolbar.Children.Add(btnImportFixedCollections);

            var btnImportPosSettlement = new LiveButton
            {
                Content = SLanguage.GetString("Pos Hesaba Geçişleri Aktar"),
                Margin = new Thickness(6, 0, 0, 0),
                Padding = new Thickness(8, 2, 8, 2)
            };
            btnImportPosSettlement.Command = _collectionOrderBankReceiptPm.CmdList["ImportPosSettlementToCollectionCommand"];
            toolbar.Children.Add(btnImportPosSettlement);

            var btnImportAging = new LiveButton
            {
                Content = SLanguage.GetString("Alacak Yaşlandırma Tutarlarını Aktar"),
                Margin = new Thickness(6, 0, 0, 0),
                Padding = new Thickness(8, 2, 8, 2)
            };
            btnImportAging.Command = _collectionOrderBankReceiptPm.CmdList["ImportCollectionAgingCommand"];
            toolbar.Children.Add(btnImportAging);

            var validationLabel = new TextBlock
            {
                Name = CollectionOrderCreditCardValidationLabelName,
                Margin = new Thickness(12, 4, 0, 0),
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center
            };
            toolbar.Children.Add(validationLabel);

            Grid.SetRow(toolbar, 0);
            parentGrid.Children.Add(toolbar);
            parentGrid.RegisterName(CollectionOrderLineApprovalToolbarName, toolbar);
            parentGrid.RegisterName(CollectionOrderCreditCardValidationLabelName, validationLabel);
        }

        /// <summary>
        /// Tahsilat planlama ekranına onay, toplu onay ve aktarım komutlarını PM komut listesine kaydeder.
        /// </summary>
        /// <param name="pm">Komutların bağlanacağı BankReceiptPM örneği.</param>
        void HookCollectionOrderCommands(BankReceiptPM pm)
        {
            if (pm?.CmdList == null) return;

            ISysCommand existingCommand = pm.CmdList["ApprovedChangeCommand"];
            if (existingCommand != null)
                pm.CmdList.Remove(existingCommand);

            pm.CmdList.AddCmd(
                116,
                "ApprovedChangeCommand",
                SLanguage.GetString("Onay İşlemi"),
                CollectionOrderOnApprovedChangeCommand,
                CollectionOrderCanApprovedChangeCommand);

            if (pm.CmdList["CollectionOrderBulkLineApproveCommand"] == null)
            {
                pm.CmdList.AddCmd(
                    330,
                    "CollectionOrderBulkLineApproveCommand",
                    SLanguage.GetString("Seçili Satırları Onayla"),
                    CollectionOrderBulkLineApproveCommand,
                    null);
            }

            if (pm.CmdList["CollectionOrderBulkLineUnapproveCommand"] == null)
            {
                pm.CmdList.AddCmd(
                    331,
                    "CollectionOrderBulkLineUnapproveCommand",
                    SLanguage.GetString("Seçili Satırları Onaysız Yap"),
                    CollectionOrderBulkLineUnapproveCommand,
                    null);
            }

            if (pm.CmdList["ImportFixedCollectionsCommand"] == null)
            {
                pm.CmdList.AddCmd(
                    332,
                    "ImportFixedCollectionsCommand",
                    SLanguage.GetString("Tekrar Eden Tahsilatları Aktar"),
                    ImportFixedCollectionsCommand,
                    CanImportFixedCollectionsCommand);
            }

            if (pm.CmdList["ImportCollectionAgingCommand"] == null)
            {
                pm.CmdList.AddCmd(
                    333,
                    "ImportCollectionAgingCommand",
                    SLanguage.GetString("Alacak Yaşlandırma Tutarlarını Aktar"),
                    ImportCollectionAgingCommand,
                    CanImportCollectionAgingCommand);
            }

            if (pm.CmdList["ImportPosSettlementToCollectionCommand"] == null)
            {
                pm.CmdList.AddCmd(
                    334,
                    "ImportPosSettlementToCollectionCommand",
                    SLanguage.GetString("Pos Hesaba Geçişleri Aktar"),
                    ImportPosSettlementToCollectionCommand,
                    CanImportPosSettlementToCollectionCommand);
            }
        }

        /// <summary>
        /// Tekrar eden tahsilatları aktar komutunun çalıştırılabilir olup olmadığını kontrol eder.
        /// </summary>
        /// <param name="obj">Komut parametreleri.</param>
        /// <returns>Tahsilat planlama bağlamında ve yetki varsa true.</returns>
        bool CanImportFixedCollectionsCommand(ISysCommandParam obj)
        {
            if (_collectionOrderBankReceiptPm == null || !IsCollectionOrderPm(_collectionOrderBankReceiptPm)) return false;
            if (BankReceiptCollectionOrderHelper.ShouldLockCollectionOrder(_collectionOrderBankReceiptPm.ActiveBO)) return false;

            return SysMng.Instance.CheckRights(
                OperationType.Update,
                (short)Modules.ExternalModule16,
                (short)Modules.ExternalModule16,
                (short)CashFlowManagementModuleSecurityItems.FixedCollectionImport,
                (short)CashFlowManagementModuleSecuritySubItems.None);
        }

        /// <summary>
        /// Sabit tahsilat tanımlarından fiş tarihine uygun satırları detay gridine aktarır.
        /// </summary>
        /// <param name="obj">Komut parametreleri.</param>
        void ImportFixedCollectionsCommand(ISysCommandParam obj)
        {
            if (_collectionOrderBankReceiptPm == null || !IsCollectionOrderPm(_collectionOrderBankReceiptPm)) return;

            var collectionOrderPm = BankReceiptPmAccess.GetCollectionOrderPm(_collectionOrderBankReceiptPm);
            if (collectionOrderPm == null) return;

            BusinessObjectBase businessObject = _collectionOrderBankReceiptPm.ActiveBO as BusinessObjectBase;
            if (!DataRowSafety.TryGetCurrentRow(businessObject, out DataRow headerRow)) return;

            LiveSession session = SysMng.Instance.getSession() as LiveSession;
            if (session?.ActiveCompany?.RecId == null) return;

            collectionOrderPm.EnsureDefaultBankAccountResolved();

            DateTime receiptDate = headerRow.IsNull("ReceiptDate")
                ? DateTime.Today
                : Convert.ToDateTime(headerRow["ReceiptDate"]);

            FixedCollectionImportResult importResult = FixedCollectionImportService.Import(
                businessObject,
                session.ActiveCompany.RecId.Value,
                receiptDate,
                collectionOrderPm.DefaultBankAccountId);

            if (!string.IsNullOrEmpty(importResult.Message))
                SysMng.Instance.ActWndMng.ShowMsg(importResult.Message, importResult.AddedCount > 0 ? null : ConstantStr.Warning);

            RefreshCollectionOrderDetailGrid();
        }

        bool CanImportPosSettlementToCollectionCommand(ISysCommandParam obj)
        {
            if (_collectionOrderBankReceiptPm == null || !IsCollectionOrderPm(_collectionOrderBankReceiptPm)) return false;
            if (BankReceiptCollectionOrderHelper.ShouldLockCollectionOrder(_collectionOrderBankReceiptPm.ActiveBO)) return false;

            return SysMng.Instance.CheckRights(
                OperationType.Update,
                (short)Modules.ExternalModule16,
                (short)Modules.ExternalModule16,
                (short)CashFlowManagementModuleSecurityItems.PosSettlementCollectionImport,
                (short)CashFlowManagementModuleSecuritySubItems.None);
        }

        void ImportPosSettlementToCollectionCommand(ISysCommandParam obj)
        {
            if (_collectionOrderBankReceiptPm == null || !IsCollectionOrderPm(_collectionOrderBankReceiptPm)) return;

            BusinessObjectBase businessObject = _collectionOrderBankReceiptPm.ActiveBO as BusinessObjectBase;
            if (!DataRowSafety.TryGetCurrentRow(businessObject, out DataRow headerRow)) return;

            LiveSession session = SysMng.Instance.getSession() as LiveSession;
            if (session?.ActiveCompany?.RecId == null) return;

            DateTime receiptDate = headerRow.IsNull("ReceiptDate")
                ? DateTime.Today
                : Convert.ToDateTime(headerRow["ReceiptDate"]);

            PosSettlementCollectionImportResult importResult = PosSettlementCollectionImportService.Import(
                businessObject,
                session,
                receiptDate);

            if (!string.IsNullOrEmpty(importResult.Message))
                SysMng.Instance.ActWndMng.ShowMsg(importResult.Message, importResult.AddedCount > 0 || importResult.UpdatedCount > 0 ? null : ConstantStr.Warning);

            RefreshCollectionOrderDetailGrid();
        }

        /// <summary>
        /// Fiş başlığı onay değiştir komutunun çalıştırılabilir olup olmadığını kontrol eder.
        /// </summary>
        /// <param name="obj">Komut parametreleri.</param>
        /// <returns>Onay değiştirilebiliyorsa true.</returns>
        bool CollectionOrderCanApprovedChangeCommand(ISysCommandParam obj)
        {
            if (_collectionOrderBankReceiptPm == null || !IsCollectionOrderPm(_collectionOrderBankReceiptPm)) return false;
            return BankReceiptCollectionOrderApprovalHelper.CanToggleHeaderApproval(_collectionOrderBankReceiptPm);
        }

        /// <summary>
        /// Tahsilat planlama fişi başlık onay durumunu değiştirir ve detay gridini yeniler.
        /// </summary>
        /// <param name="obj">Komut parametreleri.</param>
        void CollectionOrderOnApprovedChangeCommand(ISysCommandParam obj)
        {
            if (_collectionOrderBankReceiptPm == null || !IsCollectionOrderPm(_collectionOrderBankReceiptPm)) return;

            BankReceiptCollectionOrderApprovalHelper.ExecuteHeaderApprovalToggle(_collectionOrderBankReceiptPm, obj);
            RefreshCollectionOrderDetailGrid();
        }

        /// <summary>
        /// Detay gridde seçili satırları onaylar ve onay meta verilerini günceller.
        /// </summary>
        /// <param name="obj">Komut parametreleri.</param>
        void CollectionOrderBulkLineApproveCommand(ISysCommandParam obj)
        {
            if (_collectionOrderBankReceiptPm == null || !IsCollectionOrderPm(_collectionOrderBankReceiptPm)) return;

            LiveGridControl gridDetail = _collectionOrderBankReceiptPm.FCtrl("gridDetail") as LiveGridControl;
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
                if (DataRowSafety.IsDeletedOrDetached(rowView.Row)) continue;

                DataRow itemRow = rowView.Row;
                itemRow["IsApproved"] = (byte)1;
                BankReceiptCollectionOrderHelper.SetLineApprovedMetadata(itemRow, true, userId, approvedAt);
            }

            RefreshCollectionOrderDetailGrid();
        }

        /// <summary>
        /// Detay gridde seçili satırların onayını kaldırır; yetki yoksa istisna fırlatır.
        /// </summary>
        /// <param name="obj">Komut parametreleri.</param>
        void CollectionOrderBulkLineUnapproveCommand(ISysCommandParam obj)
        {
            if (_collectionOrderBankReceiptPm == null || !IsCollectionOrderPm(_collectionOrderBankReceiptPm)) return;

            if (!BankReceiptCollectionOrderHelper.HasLineApprovedEditRight())
                throw new LiveCommandItemException(CollectionOrderTerminology.LineApprovalDeniedMessage);

            LiveGridControl gridDetail = _collectionOrderBankReceiptPm.FCtrl("gridDetail") as LiveGridControl;
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
                if (DataRowSafety.IsDeletedOrDetached(rowView.Row)) continue;

                DataRow itemRow = rowView.Row;
                if (BankReceiptCollectionOrderHelper.GetApprovedValue(itemRow) == 0
                    && BankReceiptCollectionOrderHelper.GetPersistedApprovedValue(itemRow) == 0)
                    continue;

                itemRow["IsApproved"] = (byte)0;
                BankReceiptCollectionOrderHelper.SetLineApprovedMetadata(itemRow, false, null, null);
            }

            RefreshCollectionOrderDetailGrid();
        }

        bool CanImportCollectionAgingCommand(ISysCommandParam obj)
        {
            if (_collectionOrderBankReceiptPm == null || !IsCollectionOrderPm(_collectionOrderBankReceiptPm)) return false;
            if (BankReceiptCollectionOrderHelper.ShouldLockCollectionOrder(_collectionOrderBankReceiptPm.ActiveBO)) return false;

            return SysMng.Instance.CheckRights(
                OperationType.Update,
                (short)Modules.ExternalModule16,
                (short)Modules.ExternalModule16,
                (short)CashFlowManagementModuleSecurityItems.CurrentAccountCollectionAgingImport,
                (short)CashFlowManagementModuleSecuritySubItems.None);
        }

        void ImportCollectionAgingCommand(ISysCommandParam obj)
        {
            if (_collectionOrderBankReceiptPm == null || !IsCollectionOrderPm(_collectionOrderBankReceiptPm)) return;

            var collectionOrderPm = BankReceiptPmAccess.GetCollectionOrderPm(_collectionOrderBankReceiptPm);
            if (collectionOrderPm == null) return;

            BusinessObjectBase businessObject = _collectionOrderBankReceiptPm.ActiveBO as BusinessObjectBase;
            if (!DataRowSafety.TryGetCurrentRow(businessObject, out DataRow headerRow)) return;

            LiveSession session = SysMng.Instance.getSession() as LiveSession;
            if (session?.ActiveCompany?.RecId == null) return;

            collectionOrderPm.EnsureDefaultBankAccountResolved();

            DateTime reportDate = collectionOrderPm.AgingReportDate;
            if (reportDate == DateTime.MinValue)
            {
                reportDate = headerRow.IsNull("ReceiptDate")
                    ? DateTime.Today
                    : Convert.ToDateTime(headerRow["ReceiptDate"]);
            }

            var context = new CollectionOrderAgingImportContext
            {
                ReportDate = reportDate.Date,
                StartCurrentAccountCode = collectionOrderPm.AgingStartCurrentAccountCode,
                EndCurrentAccountCode = collectionOrderPm.AgingEndCurrentAccountCode,
                DefaultBankAccountId = collectionOrderPm.DefaultBankAccountId,
                DefaultBankAccountCode = collectionOrderPm.DefaultBankAccountCode,
                ImportDirectlyToReceipt = collectionOrderPm.ImportAgingDirectlyToReceipt
            };
            context.RefreshDefaultBankAccount = () =>
            {
                collectionOrderPm.RefreshDefaultBankAccountForImport();
                context.DefaultBankAccountId = collectionOrderPm.DefaultBankAccountId;
                context.DefaultBankAccountCode = collectionOrderPm.DefaultBankAccountCode;
            };

            if (collectionOrderPm.ImportAgingDirectlyToReceipt)
            {
                ExecuteDirectCollectionAgingImport(context, businessObject);
                return;
            }

            var previewPm = new CollectionOrderAgingImportPreviewPM(_container);
            previewPm.Init("CollectionOrderAgingImportPreviewViewW");
            previewPm.Initialize(context, businessObject, session);

            SysMng.Instance.ActWndMng.ShowWnd(
                previewPm,
                true,
                SLanguage.GetString("Alacak Yaşlandırma Önizleme"),
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
                RefreshCollectionOrderDetailGrid();
        }

        void ExecuteDirectCollectionAgingImport(CollectionOrderAgingImportContext context, BusinessObjectBase businessObject)
        {
            CurrentAccountAgingReportDataResult reportData = CurrentAccountCollectionAgingReportDataService.LoadAgingData(
                _container,
                context.ReportDate,
                context.StartCurrentAccountCode,
                context.EndCurrentAccountCode);

            if (!reportData.IsSuccess)
            {
                SysMng.Instance.ActWndMng.ShowMsg(
                    string.IsNullOrWhiteSpace(reportData.ErrorMessage)
                        ? SLanguage.GetString("Alacak yaşlandırma verisi alınamadı.")
                        : reportData.ErrorMessage,
                    ConstantStr.Warning);
                return;
            }

            var rows = new List<DataRow>();
            if (reportData.Data != null)
            {
                foreach (DataRow row in reportData.Data.Rows)
                {
                    if (DataRowSafety.IsUsable(row))
                        rows.Add(row);
                }
            }

            context.RefreshDefaultBankAccount?.Invoke();

            CurrentAccountCollectionAgingImportResult importResult = CurrentAccountCollectionAgingImportService.ImportSelectedRows(
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
                RefreshCollectionOrderDetailGrid();
        }
    }
}
