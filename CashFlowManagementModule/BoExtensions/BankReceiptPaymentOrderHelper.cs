using System;
using System.Data;
using System.Linq;

using CashFlowManagementModule.Services;

using Sentez.Common;
using Sentez.Common.Commands;
using Sentez.Common.ModuleBase;
using Sentez.Common.PresentationModels;
using Sentez.Common.Security;
using Sentez.Common.SystemServices;
using Sentez.Common.Utilities;
using Sentez.CashFlowManagementModule;
using Sentez.Data.BusinessObjects;
using Sentez.Data.Tools;

namespace CashFlowManagementModule.BoExtensions
{
    internal static class PaymentOrderUdFields
    {
        public const string PaymentDate = "UD_PaymentDate";
    }

    internal static class BankReceiptPaymentOrderHelper
    {
        public const short ReceiptType = 15;
        const short ModuleId = (short)Modules.ExternalModule16;
        const string IsApprovedColumn = "IsApproved";

        static bool IsUsableDataRow(DataRow row)
        {
            return row != null
                && row.Table != null
                && row.RowState != DataRowState.Deleted
                && row.RowState != DataRowState.Detached;
        }

        static bool HasColumn(DataRow row, string columnName)
        {
            return IsUsableDataRow(row)
                && !string.IsNullOrEmpty(columnName)
                && row.Table.Columns.Contains(columnName);
        }

        static bool TryGetRowByte(DataRow row, string columnName, DataRowVersion version, out byte value)
        {
            value = 0;
            if (!HasColumn(row, columnName))
                return false;

            try
            {
                object rawValue;
                if (version == DataRowVersion.Default)
                    rawValue = row[columnName];
                else if (!row.HasVersion(version))
                    return false;
                else
                    rawValue = row[columnName, version];

                if (rawValue == null || rawValue == DBNull.Value)
                    return false;

                value = Convert.ToByte(rawValue);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool IsPaymentOrderReceipt(BusinessObjectBase businessObject)
        {
            if (businessObject?.CurrentRow?.Row == null) return false;
            return IsPaymentOrderReceipt(businessObject.CurrentRow.Row);
        }

        public static bool IsPaymentOrderReceipt(DataRow headerRow)
        {
            if (!IsUsableDataRow(headerRow) || !HasColumn(headerRow, "ReceiptType") || headerRow.IsNull("ReceiptType"))
                return false;

            return Convert.ToInt16(headerRow["ReceiptType"]) == ReceiptType;
        }

        public static bool IsPaymentOrderReceiptType(short receiptType)
        {
            return receiptType == ReceiptType;
        }

        public static bool IsPaymentOrderContext(PMBase pm)
        {
            if (pm == null) return false;

            if (pm.ActiveBO?.BoBoParam != null && pm.ActiveBO.BoBoParam.Type == ReceiptType)
                return true;

            if (pm.pmParam != null)
            {
                if (pm.pmParam.Type == ReceiptType) return true;
                if (pm.pmParam.SubSecId == ReceiptType) return true;
            }

            if (pm.ActiveBO is BusinessObjectBase businessObject && IsPaymentOrderReceipt(businessObject))
                return true;

            return false;
        }

        public static bool TryGetReceiptType(BusinessObjectBase businessObject, long recId, bool isHeaderRecId, out short receiptType)
        {
            receiptType = 0;
            if (businessObject == null) return false;

            long headerRecId;
            if (isHeaderRecId)
            {
                headerRecId = recId;
            }
            else
            {
                object bankReceiptId = CashFlowDbAccess.ExecuteScalar(
                    CashFlowDbContext.FromBusinessObject(businessObject),
                    $"select BankReceiptId from Erp_BankReceiptItem where RecId={recId}");
                if (bankReceiptId == null) return false;
                headerRecId = Convert.ToInt64(bankReceiptId);
            }

            if (businessObject.Get(headerRecId) <= 0) return false;

            DataRow headerRow = businessObject.CurrentRow?.Row;
            if (!IsUsableDataRow(headerRow) || !HasColumn(headerRow, "ReceiptType") || headerRow.IsNull("ReceiptType"))
                return false;

            receiptType = Convert.ToInt16(headerRow["ReceiptType"]);
            return true;
        }

        public static void ApplyHeaderApprovalChange(DataRow headerRow, byte newApproved, string approvedExplanation = null)
        {
            if (!IsUsableDataRow(headerRow) || !HasColumn(headerRow, IsApprovedColumn)) return;

            if (!string.IsNullOrEmpty(approvedExplanation)
                && headerRow.Table.Columns.Contains("ApprovedExplanation"))
                headerRow["ApprovedExplanation"] = approvedExplanation;

            headerRow["IsApproved"] = newApproved;

            long? userId = SysMng.Instance.getSession()?.ActiveUser?.RecId;
            DateTime? approvedAt = newApproved == 1 ? new DateHelper().GetToday() : (DateTime?)null;
            SetHeaderApprovedMetadata(headerRow, newApproved == 1, userId, approvedAt);
        }

        public static void ApplyLineApprovalChange(DataRow itemRow, byte newApproved)
        {
            if (!IsUsableDataRow(itemRow) || !HasColumn(itemRow, IsApprovedColumn)) return;

            bool isApproving = newApproved == 1;
            itemRow["IsApproved"] = newApproved;

            long? userId = SysMng.Instance.getSession()?.ActiveUser?.RecId;
            DateTime? approvedAt = isApproving ? new DateHelper().GetToday() : (DateTime?)null;
            SetLineApprovedMetadata(itemRow, isApproving, userId, approvedAt);
        }

        public static bool IsHeaderApproved(BusinessObjectBase businessObject)
        {
            return GetApprovedValue(businessObject?.CurrentRow?.Row) == 1;
        }

        public static bool ShouldLockPaymentOrder(IBusinessObject businessObject)
        {
            if (businessObject == null || businessObject.IsNewRecord) return false;
            return IsHeaderEditBlocked(businessObject.CurrentRow?.Row);
        }

        public static bool IsLineEditBlocked(DataRow itemRow)
        {
            return GetPersistedApprovedValue(itemRow) == 1 && !HasLineApprovedEditRight();
        }

        public static bool IsHeaderEditBlocked(DataRow headerRow)
        {
            return GetPersistedApprovedValue(headerRow) == 1 && !HasHeaderApprovedEditRight();
        }

        public static bool IsLineBeingUnapproved(DataRow itemRow, object proposedValue)
        {
            byte newValue = proposedValue == DBNull.Value ? (byte)0 : Convert.ToByte(proposedValue);
            return GetPersistedApprovedValue(itemRow) == 1 && newValue == 0;
        }

        public static bool IsHeaderBeingUnapproved(DataRow headerRow, object proposedValue)
        {
            byte newValue = proposedValue == DBNull.Value ? (byte)0 : Convert.ToByte(proposedValue);
            return GetPersistedApprovedValue(headerRow) == 1 && newValue == 0;
        }

        public static byte GetPersistedApprovedValue(DataRow row)
        {
            if (!IsUsableDataRow(row)) return 0;

            if (row.HasVersion(DataRowVersion.Original))
                return GetApprovedValue(row, DataRowVersion.Original);

            return GetApprovedValue(row);
        }

        public static bool IsHeaderBeingApproved(DataRow headerRow)
        {
            return GetPersistedApprovedValue(headerRow) == 0 && GetApprovedValue(headerRow) == 1;
        }

        public static bool IsHeaderBeingUnapproved(DataRow headerRow)
        {
            return GetPersistedApprovedValue(headerRow) == 1 && GetApprovedValue(headerRow) == 0;
        }

        public static void ResetApprovalFields(DataRow row)
        {
            if (!HasColumn(row, IsApprovedColumn)) return;

            row[IsApprovedColumn] = (byte)0;

            if (row.Table.Columns.Contains("ApprovedBy"))
                row["ApprovedBy"] = DBNull.Value;
            if (row.Table.Columns.Contains("ApprovedAt"))
                row["ApprovedAt"] = DBNull.Value;
        }

        public static void ResetAllItemApprovalFields(BusinessObjectBase businessObject)
        {
            if (businessObject?.Data?.Tables == null || !businessObject.Data.Tables.Contains("Erp_BankReceiptItem"))
                return;

            foreach (DataRow itemRow in businessObject.Data.Tables["Erp_BankReceiptItem"].Rows)
            {
                if (itemRow.RowState == DataRowState.Deleted) continue;
                ResetApprovalFields(itemRow);
            }
        }

        public static void SetLineApprovedMetadata(DataRow itemRow, bool approved, long? userId, DateTime? approvedAt)
        {
            if (!IsUsableDataRow(itemRow)) return;

            if (!approved)
            {
                if (itemRow.Table.Columns.Contains("ApprovedBy"))
                    itemRow["ApprovedBy"] = DBNull.Value;
                if (itemRow.Table.Columns.Contains("ApprovedAt"))
                    itemRow["ApprovedAt"] = DBNull.Value;
                return;
            }

            if (userId.HasValue && itemRow.Table.Columns.Contains("ApprovedBy"))
                itemRow["ApprovedBy"] = userId.Value;
            if (approvedAt.HasValue && itemRow.Table.Columns.Contains("ApprovedAt"))
                itemRow["ApprovedAt"] = approvedAt.Value;
        }

        public static void SetHeaderApprovedMetadata(DataRow headerRow, bool approved, long? userId, DateTime? approvedAt)
        {
            if (!IsUsableDataRow(headerRow)) return;

            if (!approved)
            {
                if (headerRow.Table.Columns.Contains("ApprovedBy"))
                    headerRow["ApprovedBy"] = DBNull.Value;
                if (headerRow.Table.Columns.Contains("ApprovedAt"))
                    headerRow["ApprovedAt"] = DBNull.Value;
                return;
            }

            if (userId.HasValue && headerRow.Table.Columns.Contains("ApprovedBy"))
                headerRow["ApprovedBy"] = userId.Value;
            if (approvedAt.HasValue && headerRow.Table.Columns.Contains("ApprovedAt"))
                headerRow["ApprovedAt"] = approvedAt.Value;
        }

        public static void DisableCoreApprovedReceiptControl(BusinessObjectBase businessObject)
        {
            if (!IsPaymentOrderReceipt(businessObject)) return;
            if (!businessObject.Extensions.ContainsKey("UpdateApprovedReceiptControlExtension")) return;

            if (businessObject.Extensions["UpdateApprovedReceiptControlExtension"] is UpdateApprovedReceiptControlExtension coreExtension)
                coreExtension.Enabled = false;
        }

        public static void DisableItemIsApprovedFkSync(BusinessObjectBase businessObject)
        {
            if (businessObject?.ValueFiller == null) return;

            foreach (DefaultValueFiller.DefaultValueParameter param in businessObject.ValueFiller.Parameters.ToList())
            {
                if (param.Table == "Erp_BankReceiptItem"
                    && param.Field == "IsApproved"
                    && param.SourceTable == "Erp_BankReceipt"
                    && param.SourceField == "IsApproved"
                    && param.ForeignKey == "FK_Erp_BankReceiptItem_Erp_BankReceipt")
                {
                    businessObject.ValueFiller.RemoveRule(param);
                }
            }
        }

        public static void EnsurePaymentOrderValueFillerSetup(BusinessObjectBase businessObject)
        {
            if (!IsPaymentOrderReceipt(businessObject)) return;

            DisableItemIsApprovedFkSync(businessObject);
            DisableCoreApprovedReceiptControl(businessObject);
        }

        public static bool HasLineApprovedEditRight()
        {
            return SysMng.Instance.CheckRights(
                OperationType.Update, ModuleId, ModuleId,
                (short)CashFlowManagementModuleSecurityItems.PaymentOrderLineApproval,
                (short)CashFlowManagementModuleSecuritySubItems.None);
        }

        public static bool HasHeaderApprovedEditRight()
        {
            return SysMng.Instance.CheckRights(
                OperationType.Update, ModuleId, ModuleId,
                (short)CashFlowManagementModuleSecurityItems.PaymentOrderHeaderApproval,
                (short)CashFlowManagementModuleSecuritySubItems.None);
        }

        public static bool CanToggleHeaderApproval(DataRow headerRow)
        {
            if (!IsUsableDataRow(headerRow)) return false;
            if (GetPersistedApprovedValue(headerRow) == 0) return true;
            return HasHeaderApprovedEditRight();
        }

        public static byte GetApprovedValue(DataRow row)
        {
            return GetApprovedValue(row, DataRowVersion.Default);
        }

        public static byte GetApprovedValue(DataRow row, DataRowVersion version)
        {
            return TryGetRowByte(row, IsApprovedColumn, version, out byte value) ? value : (byte)0;
        }

        public static bool IsApprovedValueChanged(DataRow row)
        {
            if (!IsUsableDataRow(row) || !row.HasVersion(DataRowVersion.Original)) return false;
            return GetApprovedValue(row) != GetApprovedValue(row, DataRowVersion.Original);
        }

        public static bool HasUdColumn(DataRow row, string columnName)
        {
            return HasColumn(row, columnName);
        }

        public static bool HasUdColumn(DataTable table, string columnName)
        {
            return table?.Columns.Contains(columnName) == true;
        }

        public static void SetDefaultPaymentDateForNewItem(DataRow itemRow)
        {
            if (!HasUdColumn(itemRow, PaymentOrderUdFields.PaymentDate) || !IsUsableDataRow(itemRow)) return;
            if (!itemRow.IsNull(PaymentOrderUdFields.PaymentDate)) return;

            itemRow[PaymentOrderUdFields.PaymentDate] = new DateHelper().GetToday();
        }
    }
}
