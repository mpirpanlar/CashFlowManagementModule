using System;
using System.ComponentModel;
using System.Data;
using Sentez.Data.BusinessObjects;
using Sentez.Localization;
using Sentez.Common.Utilities;

namespace CashFlowManagementModule.BoExtensions
{
    public class BankReceiptPaymentOrderControlExtension : BoExtensionBase
    {
        bool _lineApprovalInProgress;
        bool _explicitHeaderApproval;

        public BankReceiptPaymentOrderControlExtension(BusinessObjectBase bo)
            : base(bo)
        {
        }

        protected override void OnAfterGet(object sender, EventArgs e)
        {
            base.OnAfterGet(sender, e);
            if (!BankReceiptPaymentOrderHelper.IsPaymentOrderReceipt(BusinessObject)) return;

            BankReceiptPaymentOrderHelper.EnsurePaymentOrderValueFillerSetup(BusinessObject);
            EnsureHeaderUnapprovedOnNewRecord();
        }

        protected override void OnRowChanged(object sender, DataRowChangeEventArgs e)
        {
            base.OnRowChanged(sender, e);
            if (_suppressEvents || !BankReceiptPaymentOrderHelper.IsPaymentOrderReceipt(BusinessObject)) return;

            if (e.Row.Table.TableName == "Erp_BankReceipt" && e.Action == DataRowAction.Add)
            {
                BankReceiptPaymentOrderHelper.EnsurePaymentOrderValueFillerSetup(BusinessObject);
                EnsureHeaderUnapprovedOnNewRecord();
            }
        }

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

        protected override void OnTableNewRow(object sender, DataTableNewRowEventArgs e)
        {
            base.OnTableNewRow(sender, e);
            if (_suppressEvents || !BankReceiptPaymentOrderHelper.IsPaymentOrderReceipt(BusinessObject)) return;

            if (e.Row.Table.TableName == "Erp_BankReceiptItem")
            {
                BankReceiptPaymentOrderHelper.SetDefaultPaymentDateFromHeader(
                    e.Row, BusinessObject?.CurrentRow?.Row);

                BankReceiptPaymentOrderHelper.ResetApprovalFields(e.Row);
            }
        }

        protected override void OnColumnChanging(object sender, DataColumnChangeEventArgs e)
        {
            base.OnColumnChanging(sender, e);
            if (_suppressEvents || !BankReceiptPaymentOrderHelper.IsPaymentOrderReceipt(BusinessObject)) return;

            if (e.Row.Table.TableName == "Erp_BankReceipt" && e.Column.ColumnName == "ReceiptDate")
                return;

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

        protected override void OnColumnChanged(object sender, DataColumnChangeEventArgs e)
        {
            base.OnColumnChanged(sender, e);
            if (_suppressEvents || !BankReceiptPaymentOrderHelper.IsPaymentOrderReceipt(BusinessObject)) return;

            try
            {
                if (e.Row.Table.TableName == "Erp_BankReceiptItem" && e.Column.ColumnName == "IsApproved")
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

            if (!headerRow.IsNull("IsApproved")
                && Convert.ToByte(headerRow["IsApproved"]) == 1
                && !_explicitHeaderApproval
                && !BankReceiptPaymentOrderHelper.IsHeaderBeingApproved(headerRow))
            {
                _suppressEvents = true;
                headerRow["IsApproved"] = (byte)0;
                headerRow["ApprovedBy"] = DBNull.Value;
                headerRow["ApprovedAt"] = DBNull.Value;
                _suppressEvents = false;
            }
        }

        protected override void OnAfterPost(object sender, EventArgs e)
        {
            base.OnAfterPost(sender, e);
            _explicitHeaderApproval = false;
            _lineApprovalInProgress = false;
        }

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

        void EnsureHeaderUnapprovedOnNewRecord()
        {
            if (BusinessObject == null || !BusinessObject.IsNewRecord || BusinessObject.CurrentRow?.Row == null) return;

            _suppressEvents = true;
            BankReceiptPaymentOrderHelper.ResetApprovalFields(BusinessObject.CurrentRow.Row);
            BankReceiptPaymentOrderHelper.ResetAllItemApprovalFields(BusinessObject);
            _suppressEvents = false;
        }

        void PreventHeaderAutoApprovalFromLines()
        {
            DataRow headerRow = BusinessObject?.CurrentRow?.Row;
            if (headerRow == null || headerRow.IsNull("IsApproved")) return;
            if (Convert.ToByte(headerRow["IsApproved"]) != 1 || _explicitHeaderApproval) return;
            if (BankReceiptPaymentOrderHelper.IsHeaderBeingApproved(headerRow)) return;

            _suppressEvents = true;
            headerRow["IsApproved"] = (byte)0;
            headerRow["ApprovedBy"] = DBNull.Value;
            headerRow["ApprovedAt"] = DBNull.Value;
            _suppressEvents = false;
        }

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
                    if (itemRow.RowState == DataRowState.Deleted) continue;
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

        static bool IsApprovedValueChanging(DataRow row, object proposedValue)
        {
            byte currentValue = BankReceiptPaymentOrderHelper.GetApprovedValue(row);
            byte newValue = proposedValue == DBNull.Value ? (byte)0 : Convert.ToByte(proposedValue);
            return currentValue != newValue;
        }
    }
}
