using System;
using System.ComponentModel;
using System.Data;
using Sentez.Data.BusinessObjects;
using Sentez.Localization;
using Sentez.Common.Utilities;

namespace CashFlowManagementModule.BoExtensions
{
    /// <summary>
    /// Banka fişi tip 15 (ödeme planlama) kayıtları için onay kilidi, yetki kontrolü
    /// ve yeni satır varsayılan alan yönetimini sağlayan BO extension.
    /// </summary>
    public class BankReceiptPaymentOrderControlExtension : BoExtensionBase
    {
        /// <summary>Detay satırı onay değişimi sırasında başlık onay senkronunu geçici olarak bastırır.</summary>
        bool _lineApprovalInProgress;

        /// <summary>Başlık onayının kullanıcı tarafından bilinçli olarak verildiğini işaretler.</summary>
        bool _explicitHeaderApproval;

        /// <summary>
        /// Ödeme planlama fişi BO extension'ını oluşturur.
        /// </summary>
        public BankReceiptPaymentOrderControlExtension(BusinessObjectBase bo)
            : base(bo)
        {
        }

        /// <summary>
        /// Kayıt yüklendikten sonra value filler ayarlarını uygular ve yeni kayıtta onay alanlarını sıfırlar.
        /// </summary>
        protected override void OnAfterGet(object sender, EventArgs e)
        {
            base.OnAfterGet(sender, e);
            if (!BankReceiptPaymentOrderHelper.IsPaymentOrderReceipt(BusinessObject)) return;

            BankReceiptPaymentOrderHelper.EnsurePaymentOrderValueFillerSetup(BusinessObject);
            EnsureHeaderUnapprovedOnNewRecord();
        }

        /// <summary>
        /// Başlık veya detay satırı eklendiğinde kurulum ve yeni satır varsayılanlarını uygular.
        /// </summary>
        protected override void OnRowChanged(object sender, DataRowChangeEventArgs e)
        {
            base.OnRowChanged(sender, e);
            if (_suppressEvents || !BankReceiptPaymentOrderHelper.IsPaymentOrderReceipt(BusinessObject)) return;

            if (e.Row.Table.TableName == "Erp_BankReceipt" && e.Action == DataRowAction.Add)
            {
                BankReceiptPaymentOrderHelper.EnsurePaymentOrderValueFillerSetup(BusinessObject);
                EnsureHeaderUnapprovedOnNewRecord();
            }
            else if (e.Row.Table.TableName == "Erp_BankReceiptItem" && e.Action == DataRowAction.Add)
            {
                _suppressEvents = true;
                BankReceiptPaymentOrderHelper.ApplyNewItemDefaults(e.Row);
                _suppressEvents = false;
            }
        }

        /// <summary>
        /// Yeni kayıt moduna geçildiğinde value filler ve onay alanı başlangıç durumunu hazırlar.
        /// </summary>
        protected override void OnBOPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            base.OnBOPropertyChanged(sender, e);
            if (_suppressEvents || !BankReceiptPaymentOrderHelper.IsPaymentOrderReceipt(BusinessObject)) return;

            if (e.PropertyName == "IsNewRecord" && BusinessObject.IsNewRecord)
            {
                BankReceiptPaymentOrderHelper.EnsurePaymentOrderValueFillerSetup(BusinessObject);
                EnsureHeaderUnapprovedOnNewRecord();
            }
        }

        /// <summary>
        /// Yeni detay satırında UD_PaymentDate için günün tarihini atar ve onay alanlarını sıfırlar.
        /// </summary>
        protected override void OnTableNewRow(object sender, DataTableNewRowEventArgs e)
        {
            base.OnTableNewRow(sender, e);
            if (_suppressEvents || !BankReceiptPaymentOrderHelper.IsPaymentOrderReceipt(BusinessObject)) return;

            if (e.Row.Table.TableName == "Erp_BankReceiptItem")
                BankReceiptPaymentOrderHelper.ApplyNewItemDefaults(e.Row);
        }

        /// <summary>
        /// Onaylı fiş/satır kilidi ve onay yetkisi kurallarını kolon değişiminden önce uygular.
        /// ReceiptDate alanları bu kontrolden muaf tutulur.
        /// </summary>
        protected override void OnColumnChanging(object sender, DataColumnChangeEventArgs e)
        {
            base.OnColumnChanging(sender, e);
            if (_suppressEvents || !BankReceiptPaymentOrderHelper.IsPaymentOrderReceipt(BusinessObject)) return;
            if (e.Row?.Table == null) return;

            if (e.Row.Table.TableName == "Erp_BankReceipt" && e.Column.ColumnName == "ReceiptDate")
            {
                BankReceiptPaymentOrderHelper.BeginHeaderReceiptDateChange(BusinessObject);
                return;
            }

            if (e.Row.Table.TableName == "Erp_BankReceiptItem" && e.Column.ColumnName == "ReceiptDate")
                return;

            if (e.Row.Table.TableName == "Erp_BankReceipt")
            {
                if (e.Column.ColumnName == "IsApproved")
                {
                    if (IsApprovedValueChanging(e.Row, e.ProposedValue)
                        && BankReceiptPaymentOrderHelper.IsHeaderBeingUnapproved(e.Row, e.ProposedValue)
                        && !BankReceiptPaymentOrderHelper.HasHeaderApprovedEditRight())
                    {
                        e.ProposedValue = e.Row["IsApproved"];
                        BusinessObject.ErrorMessage = PaymentOrderTerminology.HeaderApprovalDeniedMessage;
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

                if (BankReceiptPaymentOrderHelper.IsHeaderEditBlocked(e.Row))
                {
                    e.ProposedValue = e.Row[e.Column];
                    BusinessObject.ErrorMessage = PaymentOrderTerminology.LockedReceiptMessage;
                    return;
                }

                return;
            }

            if (e.Row.Table.TableName == "Erp_BankReceiptItem")
            {
                if (e.Column.ColumnName == "IsApproved")
                {
                    if (IsApprovedValueChanging(e.Row, e.ProposedValue)
                        && BankReceiptPaymentOrderHelper.IsLineBeingUnapproved(e.Row, e.ProposedValue)
                        && !BankReceiptPaymentOrderHelper.HasLineApprovedEditRight())
                    {
                        e.ProposedValue = e.Row["IsApproved"];
                        BusinessObject.ErrorMessage = PaymentOrderTerminology.LineApprovalDeniedMessage;
                        return;
                    }

                    _lineApprovalInProgress = true;
                    return;
                }

                if (BankReceiptPaymentOrderHelper.IsLineEditBlocked(e.Row))
                {
                    e.ProposedValue = e.Row[e.Column];
                    BusinessObject.ErrorMessage = PaymentOrderTerminology.LockedLineMessage;
                }
            }
        }

        /// <summary>
        /// Detay satırı onay değişiminde metadata alanlarını günceller ve başlık otomatik onayını engeller.
        /// </summary>
        protected override void OnColumnChanged(object sender, DataColumnChangeEventArgs e)
        {
            base.OnColumnChanged(sender, e);
            if (_suppressEvents || !BankReceiptPaymentOrderHelper.IsPaymentOrderReceipt(BusinessObject)) return;
            if (e.Row?.Table == null) return;

            try
            {
                if (e.Row.Table.TableName == "Erp_BankReceiptItem" && e.Column.ColumnName == "ReceiptDate")
                {
                    _suppressEvents = true;
                    BankReceiptPaymentOrderHelper.ProtectItemPaymentDateAfterReceiptDateChange(BusinessObject, e.Row);
                    _suppressEvents = false;
                }
                else if (e.Row.Table.TableName == "Erp_BankReceipt" && e.Column.ColumnName == "ReceiptDate")
                {
                    _suppressEvents = true;
                    BankReceiptPaymentOrderHelper.RestoreItemPaymentDates(
                        BankReceiptPaymentOrderHelper.GetActivePaymentDateSnapshot(BusinessObject));
                    BankReceiptPaymentOrderHelper.EndHeaderReceiptDateChange(BusinessObject);
                    _suppressEvents = false;
                }
                else if (e.Row.Table.TableName == "Erp_BankReceiptItem" && e.Column.ColumnName == "IsApproved")
                {
                    _lineApprovalInProgress = true;
                    PreventHeaderAutoApprovalFromLines();

                    byte newValue = BankReceiptPaymentOrderHelper.GetApprovedValue(e.Row);
                    long? userId = BusinessObject.ActiveSession?.ActiveUser?.RecId;
                    DateTime? approvedAt = newValue == 1 ? new DateHelper().GetToday() : (DateTime?)null;

                    _suppressEvents = true;
                    BankReceiptPaymentOrderHelper.SetLineApprovedMetadata(e.Row, newValue == 1, userId, approvedAt);
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
            if (e.Cancel || !BankReceiptPaymentOrderHelper.IsPaymentOrderReceipt(BusinessObject)) return;

            DataRow headerRow = BusinessObject.CurrentRow?.Row;
            if (headerRow == null) return;

            if (BankReceiptPaymentOrderHelper.GetPersistedApprovedValue(headerRow) == 1
                && BusinessObject.Data.HasChanges()
                && !BankReceiptPaymentOrderHelper.IsHeaderBeingUnapproved(headerRow)
                && BankReceiptPaymentOrderHelper.IsHeaderEditBlocked(headerRow))
            {
                BusinessObject.ErrorMessage = PaymentOrderTerminology.LockedReceiptMessage;
                e.Cancel = true;
                return;
            }

            if (!ValidateApprovalChangeRights(headerRow))
            {
                e.Cancel = true;
                return;
            }

            if (!BankReceiptPaymentOrderHelper.IsHeaderBeingApproved(headerRow))
                PreventHeaderAutoApprovalFromLines();

            if (BankReceiptPaymentOrderHelper.GetApprovedValue(headerRow) == 1
                && !_explicitHeaderApproval
                && !BankReceiptPaymentOrderHelper.IsHeaderBeingApproved(headerRow))
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
        /// Onaylı ödeme planlama fişinin silinmesini engeller.
        /// </summary>
        protected override void OnBeforeDelete(object sender, CancelEventArgs e)
        {
            base.OnBeforeDelete(sender, e);
            if (e.Cancel || !BankReceiptPaymentOrderHelper.IsPaymentOrderReceipt(BusinessObject)) return;

            if (BankReceiptPaymentOrderHelper.ShouldLockPaymentOrder(BusinessObject))
            {
                BusinessObject.ErrorMessage = PaymentOrderTerminology.LockedReceiptMessage;
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
            BankReceiptPaymentOrderHelper.ResetApprovalFields(BusinessObject.CurrentRow.Row);
            BankReceiptPaymentOrderHelper.ResetAllItemApprovalFields(BusinessObject);
            _suppressEvents = false;
        }

        /// <summary>
        /// Detay satır onayından kaynaklanan başlık otomatik onayını geri alır.
        /// </summary>
        void PreventHeaderAutoApprovalFromLines()
        {
            DataRow headerRow = BusinessObject?.CurrentRow?.Row;
            if (!BankReceiptPaymentOrderHelper.IsPaymentOrderReceipt(headerRow)) return;
            if (BankReceiptPaymentOrderHelper.GetApprovedValue(headerRow) != 1 || _explicitHeaderApproval) return;
            if (BankReceiptPaymentOrderHelper.IsHeaderBeingApproved(headerRow)) return;

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
                && BankReceiptPaymentOrderHelper.IsApprovedValueChanged(headerRow)
                && BankReceiptPaymentOrderHelper.IsHeaderBeingUnapproved(headerRow)
                && !BankReceiptPaymentOrderHelper.HasHeaderApprovedEditRight())
            {
                BusinessObject.ErrorMessage = PaymentOrderTerminology.HeaderApprovalDeniedMessage;
                return false;
            }

            if (BusinessObject.Data.Tables.Contains("Erp_BankReceiptItem"))
            {
                foreach (DataRow itemRow in BusinessObject.Data.Tables["Erp_BankReceiptItem"].Rows)
                {
                    if (itemRow.RowState == DataRowState.Deleted || itemRow.RowState == DataRowState.Detached) continue;
                    if (BankReceiptPaymentOrderHelper.IsApprovedValueChanged(itemRow)
                        && BankReceiptPaymentOrderHelper.GetPersistedApprovedValue(itemRow) == 1
                        && BankReceiptPaymentOrderHelper.GetApprovedValue(itemRow) == 0
                        && !BankReceiptPaymentOrderHelper.HasLineApprovedEditRight())
                    {
                        BusinessObject.ErrorMessage = PaymentOrderTerminology.LineApprovalDeniedMessage;
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
            byte currentValue = BankReceiptPaymentOrderHelper.GetApprovedValue(row);
            byte newValue = proposedValue == DBNull.Value ? (byte)0 : Convert.ToByte(proposedValue);
            return currentValue != newValue;
        }
    }
}
