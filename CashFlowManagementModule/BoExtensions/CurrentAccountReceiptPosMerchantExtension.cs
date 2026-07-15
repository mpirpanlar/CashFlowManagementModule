using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;

using CashFlowManagementModule.Services;

using Sentez.Common.SystemServices;
using Sentez.Data.BusinessObjects;

namespace CashFlowManagementModule.BoExtensions
{
    public class CurrentAccountReceiptPosMerchantExtension : BoExtensionBase
    {
        IReadOnlyCollection<PosSnapshotPeriodKey> _pendingDeletePeriods;

        public CurrentAccountReceiptPosMerchantExtension(BusinessObjectBase bo)
            : base(bo)
        {
        }

        static bool TryGetHeaderField(DataRow row, string columnName, out object value)
        {
            value = null;
            if (row == null || row.Table == null || string.IsNullOrEmpty(columnName))
                return false;

            if (!row.Table.Columns.Contains(columnName))
                return false;

            if (row.RowState == DataRowState.Detached && !row.HasVersion(DataRowVersion.Original))
                return false;

            try
            {
                DataRowVersion version = DataRowVersion.Current;
                if (row.RowState == DataRowState.Deleted)
                    version = DataRowVersion.Original;
                else if (!row.HasVersion(DataRowVersion.Current) && row.HasVersion(DataRowVersion.Original))
                    version = DataRowVersion.Original;

                if (!row.HasVersion(version))
                    return false;

                DataColumn column = row.Table.Columns[columnName];
                if (row.IsNull(column, version))
                    return false;

                value = row[column, version];
                return true;
            }
            catch (RowNotInTableException)
            {
                return false;
            }
            catch (DeletedRowInaccessibleException)
            {
                return false;
            }
        }

        static bool IsPosMerchantReceipt(BusinessObjectBase businessObject)
        {
            if (!TryGetHeaderField(businessObject?.CurrentRow?.Row, "ReceiptType", out object receiptTypeValue))
                return false;

            short receiptType = Convert.ToInt16(receiptTypeValue);
            return receiptType == BankAccountPosHelper.CustomerCreditCardCollectionReceiptType
                || receiptType == BankAccountPosHelper.CustomerCreditCardRefundReceiptType;
        }

        protected override void OnAfterPost(object sender, EventArgs e)
        {
            base.OnAfterPost(sender, e);
            RefreshAffectedPosSnapshots();
        }

        protected override void OnBeforeDelete(object sender, CancelEventArgs e)
        {
            base.OnBeforeDelete(sender, e);

            _pendingDeletePeriods = null;
            if (e.Cancel || !IsPosMerchantReceipt(BusinessObject))
                return;

            LiveSession session = BusinessObject.ActiveSession as LiveSession;
            if (session?._dbInfo?.Connection == null)
                return;

            // Silme sonrası CurrentRow/item satırları Detached olur; dönemleri silmeden önce topla.
            _pendingDeletePeriods = PosSnapshotRefreshService.CollectAffectedPeriods(BusinessObject, session);
        }

        protected override void OnAfterDelete(object sender, EventArgs e)
        {
            base.OnAfterDelete(sender, e);

            IReadOnlyCollection<PosSnapshotPeriodKey> periods = _pendingDeletePeriods;
            _pendingDeletePeriods = null;
            if (periods == null || periods.Count == 0)
                return;

            LiveSession session = BusinessObject.ActiveSession as LiveSession;
            if (session?._dbInfo?.Connection == null)
                return;

            int userId = (int)(session.ActiveUser?.RecId ?? 0);
            PosSnapshotRefreshService.RefreshPeriods(session, periods, userId);
        }

        void RefreshAffectedPosSnapshots()
        {
            if (!IsPosMerchantReceipt(BusinessObject))
                return;

            LiveSession session = BusinessObject.ActiveSession as LiveSession;
            if (session?._dbInfo?.Connection == null)
                return;

            int userId = (int)(session.ActiveUser?.RecId ?? 0);
            PosSnapshotRefreshService.RefreshAffectedPeriods(BusinessObject, session, userId);
        }
    }
}
