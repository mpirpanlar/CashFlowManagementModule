using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

using Sentez.Common.Utilities;
using Sentez.Data.BusinessObjects;
using Sentez.Data.MetaData;
using Sentez.Data.Tools;
using Sentez.Localization;

namespace CashFlowManagementModule.BoExtensions
{
    internal static class BankReceiptItemAuditHelper
    {
        const string ItemTableName = "Erp_BankReceiptItem";
        const string HeaderTableName = "Erp_BankReceipt";
        const string ItemHeaderForeignKey = "FK_Erp_BankReceiptItem_Erp_BankReceipt";
        const string DateTimeFormat = "dd.MM.yyyy HH:mm:ss";

        static readonly string[] AuditFields = { "InsertedAt", "InsertedBy", "UpdatedAt", "UpdatedBy" };

        public static bool IsAuditReceiptType(short receiptType)
        {
            return BankReceiptPaymentOrderHelper.IsPaymentOrderReceiptType(receiptType)
                || BankReceiptCollectionOrderHelper.IsCollectionOrderReceiptType(receiptType);
        }

        public static bool IsAuditReceipt(BusinessObjectBase businessObject)
        {
            if (businessObject?.CurrentRow?.Row == null) return false;
            return IsAuditReceipt(businessObject.CurrentRow.Row);
        }

        public static bool IsAuditReceipt(DataRow headerRow)
        {
            if (headerRow == null
                || headerRow.Table == null
                || headerRow.RowState == DataRowState.Deleted
                || headerRow.RowState == DataRowState.Detached
                || !headerRow.Table.Columns.Contains("ReceiptType")
                || headerRow.IsNull("ReceiptType"))
            {
                return false;
            }

            return IsAuditReceiptType(Convert.ToInt16(headerRow["ReceiptType"]));
        }

        public static void EnsureAuditLookups(BusinessObjectBase businessObject)
        {
            if (businessObject?.Lookups == null) return;

            businessObject.Lookups.AddLookUp(
                ItemTableName,
                "InsertedBy",
                true,
                "Meta_User",
                "UserCode",
                "InsertedByUserCode",
                "UserName",
                "InsertedByUserName");

            businessObject.Lookups.AddLookUp(
                ItemTableName,
                "UpdatedBy",
                true,
                "Meta_User",
                "UserCode",
                "UpdatedByUserCode",
                "UserName",
                "UpdatedByUserName");
        }

        public static void DisableItemAuditFkSync(BusinessObjectBase businessObject)
        {
            if (businessObject?.ValueFiller == null) return;

            foreach (DefaultValueFiller.DefaultValueParameter param in businessObject.ValueFiller.Parameters.ToList())
            {
                if (param.Table != ItemTableName
                    || param.SourceTable != HeaderTableName
                    || param.ForeignKey != ItemHeaderForeignKey
                    || !AuditFields.Contains(param.Field))
                {
                    continue;
                }

                businessObject.ValueFiller.RemoveRule(param);
            }
        }

        public static void ApplyItemAuditMetadataBeforePost(BusinessObjectBase businessObject)
        {
            if (!IsAuditReceipt(businessObject)
                || businessObject?.Data?.Tables == null
                || !businessObject.Data.Tables.Contains(ItemTableName))
            {
                return;
            }

            long? userId = businessObject.ActiveSession?.ActiveUser?.RecId;
            if (!userId.HasValue) return;

            DateTime now = DateTime.Now;

            foreach (DataRow itemRow in businessObject.Data.Tables[ItemTableName].Rows)
            {
                if (itemRow.RowState == DataRowState.Deleted || itemRow.RowState == DataRowState.Detached)
                    continue;

                if (itemRow.RowState == DataRowState.Added)
                {
                    SetInsertedMetadata(itemRow, userId.Value, now);
                }
                else if (itemRow.RowState == DataRowState.Modified)
                {
                    SetUpdatedMetadata(itemRow, userId.Value, now);
                }
            }
        }

        static void SetInsertedMetadata(DataRow itemRow, long userId, DateTime timestamp)
        {
            if (itemRow.Table.Columns.Contains("InsertedBy"))
                itemRow["InsertedBy"] = userId;

            if (itemRow.Table.Columns.Contains("InsertedAt"))
                itemRow["InsertedAt"] = timestamp;
        }

        static void SetUpdatedMetadata(DataRow itemRow, long userId, DateTime timestamp)
        {
            if (itemRow.Table.Columns.Contains("UpdatedBy"))
                itemRow["UpdatedBy"] = userId;

            if (itemRow.Table.Columns.Contains("UpdatedAt"))
                itemRow["UpdatedAt"] = timestamp;
        }

        public static void AddAuditDetailColumns(ReceiptColumnCollection columns)
        {
            if (columns == null) return;

            AddAuditColumnIfMissing(columns, "InsertedAt", SLanguage.GetString("Kayıt Zamanı"), EditorType.DateTimeEditor, FieldUsage.DateTime, 150, isDateTime: true);
            AddAuditColumnIfMissing(columns, "InsertedByUserName", SLanguage.GetString("Kayıt Eden"), EditorType.TextEditor, FieldUsage.None, 120);
            AddAuditColumnIfMissing(columns, "UpdatedAt", SLanguage.GetString("Değişiklik Zamanı"), EditorType.DateTimeEditor, FieldUsage.DateTime, 150, isDateTime: true);
            AddAuditColumnIfMissing(columns, "UpdatedByUserName", SLanguage.GetString("Değiştiren"), EditorType.TextEditor, FieldUsage.None, 120);
        }

        static void AddAuditColumnIfMissing(
            ReceiptColumnCollection columns,
            string columnName,
            string caption,
            EditorType editorType,
            FieldUsage usageType,
            int width,
            bool isDateTime = false)
        {
            ReceiptColumn column = columns.FirstOrDefault(c => c.ColumnName == columnName);
            if (column == null)
            {
                column = new ReceiptColumn
                {
                    ColumnName = columnName,
                    Caption = caption,
                    EditorType = editorType,
                    Width = width,
                    UsageType = usageType,
                    IsReadOnly = true,
                    IsVisible = true,
                    ColumnOrder = GetNextColumnOrder(columns)
                };
                ApplyDateTimeColumnSettings(column, isDateTime);
                columns.Add(column);
                return;
            }

            column.Caption = caption;
            column.EditorType = editorType;
            column.Width = width;
            column.UsageType = usageType;
            column.IsReadOnly = true;
            column.IsVisible = true;
            if (column.ColumnOrder < 0)
                column.ColumnOrder = GetNextColumnOrder(columns);
            ApplyDateTimeColumnSettings(column, isDateTime);
        }

        static void ApplyDateTimeColumnSettings(ReceiptColumn column, bool isDateTime)
        {
            if (!isDateTime || column == null) return;

            column.DataType = typeof(DateTime);
            column.UdtType = UdtType.UdtDateTime;
            column.FormatString = DateTimeFormat;
            column.MaskString = DateTimeFormat;
        }

        static int GetNextColumnOrder(ReceiptColumnCollection columns)
        {
            if (columns == null || columns.Count == 0)
                return 0;

            int maxOrder = columns.Max(c => c.ColumnOrder);
            return maxOrder >= 0 ? maxOrder + 1 : columns.Count;
        }
    }
}
