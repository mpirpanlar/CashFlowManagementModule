using System;
using System.ComponentModel;
using System.Data;
using Sentez.Data.BusinessObjects;
using Sentez.Localization;
using Sentez.Common.Utilities;

namespace CashFlowManagementModule.BoExtensions
{
    /// <summary>
    /// Banka fişi tip 20 (tahsilat planlama) kayıtları için onay kilidi, yetki kontrolü
    /// ve yeni satır varsayılan alan yönetimini sağlayan BO extension.
    /// </summary>
    public class BankReceiptCollectionOrderControlExtension : BoExtensionBase
    {
        /// <summary>Detay satırı onay değişimi sırasında başlık onay senkronunu geçici olarak bastırır.</summary>
        bool _lineApprovalInProgress;

        /// <summary>Başlık onayının kullanıcı tarafından bilinçli olarak verildiğini işaretler.</summary>
        bool _explicitHeaderApproval;

        /// <summary>
        /// Tahsilat planlama fişi BO extension'ını oluşturur.
        /// </summary>
        public BankReceiptCollectionOrderControlExtension(BusinessObjectBase bo)
            : base(bo)
        {
        }

        /// <summary>
        /// Kayıt yüklendikten sonra value filler ayarlarını uygular ve yeni kayıtta onay alanlarını sıfırlar.
        /// </summary>
        protected override void OnAfterGet(object sender, EventArgs e)
        {
            base.OnAfterGet(sender, e);
            if (!BankReceiptCollectionOrderHelper.IsCollectionOrderReceipt(BusinessObject)) return;

            BankReceiptCollectionOrderHelper.EnsureCollectionOrderValueFillerSetup(BusinessObject);
            EnsureHeaderUnapprovedOnNewRecord();
        }

        /// <summary>
        /// Başlık veya detay satırı eklendiğinde kurulum ve yeni satır varsayılanlarını uygular.
        /// </summary>
        protected override void OnRowChanged(object sender, DataRowChangeEventArgs e)
        {
            base.OnRowChanged(sender, e);
            if (_suppressEvents || !BankReceiptCollectionOrderHelper.IsCollectionOrderReceipt(BusinessObject)) return;

            if (e.Row.Table.TableName == "Erp_BankReceipt" && e.Action == DataRowAction.Add)
            {
                BankReceiptCollectionOrderHelper.EnsureCollectionOrderValueFillerSetup(BusinessObject);
                EnsureHeaderUnapprovedOnNewRecord();
            }
            else if (e.Row.Table.TableName == "Erp_BankReceiptItem" && e.Action == DataRowAction.Add)
            {
                _suppressEvents = true;
                BankReceiptCollectionOrderHelper.ApplyNewItemDefaults(e.Row);
                _suppressEvents = false;
            }
        }

        /// <summary>
        /// Yeni kayıt moduna geçildiğinde value filler ve onay alanı başlangıç durumunu hazırlar.
        /// </summary>
        protected override void OnBOPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            base.OnBOPropertyChanged(sender, e);
            if (_suppressEvents || !BankReceiptCollectionOrderHelper.IsCollectionOrderReceipt(BusinessObject)) return;

            if (e.PropertyName == "IsNewRecord" && BusinessObject.IsNewRecord)
            {
                BankReceiptCollectionOrderHelper.EnsureCollectionOrderValueFillerSetup(BusinessObject);
                EnsureHeaderUnapprovedOnNewRecord();
            }
        }

        /// <summary>
        /// Yeni detay satırında UD_PaymentDate için günün tarihini atar ve onay alanlarını sıfırlar.
        /// </summary>
        protected override void OnTableNewRow(object sender, DataTableNewRowEventArgs e)
        {
            base.OnTableNewRow(sender, e);
            if (_suppressEvents || !BankReceiptCollectionOrderHelper.IsCollectionOrderReceipt(BusinessObject)) return;

            if (e.Row.Table.TableName == "Erp_BankReceiptItem")
                BankReceiptCollectionOrderHelper.ApplyNewItemDefaults(e.Row);
        }

        /// <summary>
        /// Onaylı fiş/satır kilidi ve onay yetkisi kurallarını kolon değişiminden önce uygular.
        /// ReceiptDate alanları bu kontrolden muaf tutulur.
        /// </summary>
        protected override void OnColumnChanging(object sender, DataColumnChangeEventArgs e)
        {
            base.OnColumnChanging(sender, e);
            if (_suppressEvents || !BankReceiptCollectionOrderHelper.IsCollectionOrderReceipt(BusinessObject)) return;
            if (e.Row?.Table == null) return;

            if (e.Row.Table.TableName == "Erp_BankReceipt" && e.Column.ColumnName == "ReceiptDate")
            {
                BankReceiptCollectionOrderHelper.BeginHeaderReceiptDateChange(BusinessObject);
                return;
            }

            if (e.Row.Table.TableName == "Erp_BankReceiptItem" && e.Column.ColumnName == "ReceiptDate")
                return;

            if (e.Row.Table.TableName == "Erp_BankReceipt")
            {
                if (e.Column.ColumnName == "IsApproved")
                {
                    if (IsApprovedValueChanging(e.Row, e.ProposedValue)
                        && BankReceiptCollectionOrderHelper.IsHeaderBeingUnapproved(e.Row, e.ProposedValue)
                        && !BankReceiptCollectionOrderHelper.HasHeaderApprovedEditRight())
                    {
                        e.ProposedValue = e.Row["IsApproved"];
                        BusinessObject.ErrorMessage = CollectionOrderTerminology.HeaderApprovalDeniedMessage;
                        return;
                    }

                    if (_lineApprovalInProgress && e.ProposedValue != DBNull.Value && Convert.ToByte(e.ProposedValue) == 1)
                    {
                        e.ProposedValue = e.Row["IsApproved"];
                        return;
                    }

                    if (e.ProposedValue != DBNull.Value && Convert.ToByte(e.ProposedValue) == 1 && !_lineApprovalInProgress)
                        _explicitHeaderApproval = true;
                    return;
                }

                if (BankReceiptCollectionOrderHelper.IsHeaderEditBlocked(e.Row))
                {
                    e.ProposedValue = e.Row[e.Column];
                    BusinessObject.ErrorMessage = CollectionOrderTerminology.LockedReceiptMessage;
                    return;
                }

                return;
            }

            if (e.Row.Table.TableName == "Erp_BankReceiptItem")
            {
                if (e.Column.ColumnName == "IsApproved")
                {
                    if (IsApprovedValueChanging(e.Row, e.ProposedValue)
                        && BankReceiptCollectionOrderHelper.IsLineBeingUnapproved(e.Row, e.ProposedValue)
                        && !BankReceiptCollectionOrderHelper.HasLineApprovedEditRight())
                    {
                        e.ProposedValue = e.Row["IsApproved"];
                        BusinessObject.ErrorMessage = CollectionOrderTerminology.LineApprovalDeniedMessage;
                        return;
                    }

                    _lineApprovalInProgress = true;
                    return;
                }

                if (BankReceiptCollectionOrderHelper.IsLineEditBlocked(e.Row))
                {
                    e.ProposedValue = e.Row[e.Column];
                    BusinessObject.ErrorMessage = CollectionOrderTerminology.LockedLineMessage;
                }
            }
        }

        /// <summary>
        /// Detay satırı onay değişiminde metadata alanlarını günceller ve başlık otomatik onayını engeller.
        /// </summary>
        protected override void OnColumnChanged(object sender, DataColumnChangeEventArgs e)
        {
            base.OnColumnChanged(sender, e);
            if (_suppressEvents || !BankReceiptCollectionOrderHelper.IsCollectionOrderReceipt(BusinessObject)) return;
            if (e.Row?.Table == null) return;

            try
            {
                if (e.Row.Table.TableName == "Erp_BankReceiptItem" && e.Column.ColumnName == "ReceiptDate")
                {
                    _suppressEvents = true;
                    BankReceiptCollectionOrderHelper.ProtectItemPaymentDateAfterReceiptDateChange(BusinessObject, e.Row);
                    _suppressEvents = false;
                }
                else if (e.Row.Table.TableName == "Erp_BankReceipt" && e.Column.ColumnName == "ReceiptDate")
                {
                    _suppressEvents = true;
                    BankReceiptCollectionOrderHelper.RestoreItemPaymentDates(
                        BankReceiptCollectionOrderHelper.GetActivePaymentDateSnapshot(BusinessObject));
                    BankReceiptCollectionOrderHelper.EndHeaderReceiptDateChange(BusinessObject);
                    _suppressEvents = false;
                }
                else if (e.Row.Table.TableName == "Erp_BankReceiptItem" && e.Column.ColumnName == "IsApproved")
                {
                    _lineApprovalInProgress = true;
                    PreventHeaderAutoApprovalFromLines();

                    byte newValue = BankReceiptCollectionOrderHelper.GetApprovedValue(e.Row);
                    long? userId = BusinessObject.ActiveSession?.ActiveUser?.RecId;
                    DateTime? approvedAt = newValue == 1 ? new DateHelper().GetToday() : (DateTime?)null;

                    _suppressEvents = true;
                    BankReceiptCollectionOrderHelper.SetLineApprovedMetadata(e.Row, newValue == 1, userId, approvedAt);
                    _suppressEvents = false;
                }
                else if (e.Row.Table.TableName == "Erp_BankReceipt" && e.Column.ColumnName == "IsApproved")
                {
                    _lineApprovalInProgress = false;
                }
            }
            finally
            {
                _lineApprovalInProgress = false;
            }
        }

        /// <summary>
        /// Kayıt öncesi onaylı fiş kilidi, yetki doğrulaması ve istenmeyen başlık otomatik onayını kontrol eder.
        /// </summary>
        protected override void OnBeforePost(object sender, CancelEventArgs e)
        {
            base.OnBeforePost(sender, e);
            if (e.Cancel || !BankReceiptCollectionOrderHelper.IsCollectionOrderReceipt(BusinessObject)) return;

            DataRow headerRow = BusinessObject.CurrentRow?.Row;
            if (headerRow == null) return;

            if (BankReceiptCollectionOrderHelper.GetPersistedApprovedValue(headerRow) == 1
                && BusinessObject.Data.HasChanges()
                && !BankReceiptCollectionOrderHelper.IsHeaderBeingUnapproved(headerRow)
                && BankReceiptCollectionOrderHelper.IsHeaderEditBlocked(headerRow))
            {
                BusinessObject.ErrorMessage = CollectionOrderTerminology.LockedReceiptMessage;
                e.Cancel = true;
                return;
            }

            if (!ValidateApprovalChangeRights(headerRow))
            {
                e.Cancel = true;
                return;
            }

            if (!BankReceiptCollectionOrderHelper.IsHeaderBeingApproved(headerRow))
                PreventHeaderAutoApprovalFromLines();

            if (BankReceiptCollectionOrderHelper.GetApprovedValue(headerRow) == 1
                && !_explicitHeaderApproval
                && !BankReceiptCollectionOrderHelper.IsHeaderBeingApproved(headerRow))
            {
                _suppressEvents = true;
                headerRow["IsApproved"] = (byte)0;
                headerRow["ApprovedBy"] = DBNull.Value;
                headerRow["ApprovedAt"] = DBNull.Value;
                _suppressEvents = false;
            }

            BankReceiptItemAuditHelper.ApplyItemAuditMetadataBeforePost(BusinessObject);
        }

        /// <summary>
        /// Kayıt sonrası onay akışı için kullanılan geçici bayrakları sıfırlar.
        /// </summary>
        protected override void OnAfterPost(object sender, EventArgs e)
        {
            base.OnAfterPost(sender, e);
            _explicitHeaderApproval = false;
            _lineApprovalInProgress = false;
        }

        /// <summary>
        /// Onaylı tahsilat planlama fişinin silinmesini engeller.
        /// </summary>
        protected override void OnBeforeDelete(object sender, CancelEventArgs e)
        {
            base.OnBeforeDelete(sender, e);
            if (e.Cancel || !BankReceiptCollectionOrderHelper.IsCollectionOrderReceipt(BusinessObject)) return;

            if (BankReceiptCollectionOrderHelper.ShouldLockCollectionOrder(BusinessObject))
            {
                BusinessObject.ErrorMessage = CollectionOrderTerminology.LockedReceiptMessage;
                e.Cancel = true;
            }
        }

        /// <summary>
        /// Yeni kayıtta başlık ve tüm detay satırlarının onay alanlarını varsayılan (onaysız) duruma getirir.
        /// </summary>
        void EnsureHeaderUnapprovedOnNewRecord()
        {
            if (BusinessObject == null || !BusinessObject.IsNewRecord || BusinessObject.CurrentRow?.Row == null) return;

            _suppressEvents = true;
            BankReceiptCollectionOrderHelper.ResetApprovalFields(BusinessObject.CurrentRow.Row);
            BankReceiptCollectionOrderHelper.ResetAllItemApprovalFields(BusinessObject);
            _suppressEvents = false;
        }

        /// <summary>
        /// Detay satır onayından kaynaklanan başlık otomatik onayını geri alır.
        /// </summary>
        void PreventHeaderAutoApprovalFromLines()
        {
            DataRow headerRow = BusinessObject?.CurrentRow?.Row;
            if (!BankReceiptCollectionOrderHelper.IsCollectionOrderReceipt(headerRow)) return;
            if (BankReceiptCollectionOrderHelper.GetApprovedValue(headerRow) != 1 || _explicitHeaderApproval) return;
            if (BankReceiptCollectionOrderHelper.IsHeaderBeingApproved(headerRow)) return;

            _suppressEvents = true;
            headerRow["IsApproved"] = (byte)0;
            headerRow["ApprovedBy"] = DBNull.Value;
            headerRow["ApprovedAt"] = DBNull.Value;
            _suppressEvents = false;
        }

        /// <summary>
        /// Başlık ve detay satırlarında onay geri alma işlemleri için kullanıcı yetkisini doğrular.
        /// </summary>
        /// <returns>Yetki yeterliyse true, aksi halde false.</returns>
        bool ValidateApprovalChangeRights(DataRow headerRow)
        {
            if (headerRow != null
                && BankReceiptCollectionOrderHelper.IsApprovedValueChanged(headerRow)
                && BankReceiptCollectionOrderHelper.IsHeaderBeingUnapproved(headerRow)
                && !BankReceiptCollectionOrderHelper.HasHeaderApprovedEditRight())
            {
                BusinessObject.ErrorMessage = CollectionOrderTerminology.HeaderApprovalDeniedMessage;
                return false;
            }

            if (BusinessObject.Data.Tables.Contains("Erp_BankReceiptItem"))
            {
                foreach (DataRow itemRow in BusinessObject.Data.Tables["Erp_BankReceiptItem"].Rows)
                {
                    if (itemRow.RowState == DataRowState.Deleted || itemRow.RowState == DataRowState.Detached) continue;
                    if (BankReceiptCollectionOrderHelper.IsApprovedValueChanged(itemRow)
                        && BankReceiptCollectionOrderHelper.GetPersistedApprovedValue(itemRow) == 1
                        && BankReceiptCollectionOrderHelper.GetApprovedValue(itemRow) == 0
                        && !BankReceiptCollectionOrderHelper.HasLineApprovedEditRight())
                    {
                        BusinessObject.ErrorMessage = CollectionOrderTerminology.LineApprovalDeniedMessage;
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// IsApproved alanında gerçek bir değişiklik olup olmadığını kontrol eder.
        /// </summary>
        static bool IsApprovedValueChanging(DataRow row, object proposedValue)
        {
            byte currentValue = BankReceiptCollectionOrderHelper.GetApprovedValue(row);
            byte newValue = proposedValue == DBNull.Value ? (byte)0 : Convert.ToByte(proposedValue);
            return currentValue != newValue;
        }
    }
}
