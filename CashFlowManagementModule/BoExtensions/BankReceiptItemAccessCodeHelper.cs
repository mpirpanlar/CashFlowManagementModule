using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

using LiveCore.Desktop.UI.Controls;

using Sentez.BankModule.PresentationModels;
using Sentez.Common.Commands;
using Sentez.Common.SystemServices;
using Sentez.Common.Utilities;
using Sentez.Data.BusinessObjects;
using Sentez.Data.MetaData;
using Sentez.Data.Query;
using Sentez.Environment;
using Sentez.Localization;

namespace CashFlowManagementModule.BoExtensions
{
    internal static class BankReceiptItemAccessCodeHelper
    {
        public const string FieldAccessCode = "AccessCode";
        const string ItemTableName = "Erp_BankReceiptItem";
        const string ItemHeaderForeignKey = "FK_Erp_BankReceiptItem_Erp_BankReceipt";

        public static bool IsFieldAvailable()
        {
            return Schema.Tables.Contains(ItemTableName)
                && Schema.Tables[ItemTableName].Fields.Contains(FieldAccessCode);
        }

        static bool IsFieldAvailable(DataTable table)
        {
            return table?.Columns.Contains(FieldAccessCode) == true;
        }

        static bool IsUsableDataRow(DataRow row)
        {
            return row != null
                && row.Table != null
                && row.RowState != DataRowState.Deleted
                && row.RowState != DataRowState.Detached;
        }

        public static string BuildDetailRowFilter(AuthUserInfo user)
        {
            if (user?.AccessCodes == null || user.AccessCodes.Count == 0)
                return string.Empty;

            var parts = new List<string> { "AccessCode IS NULL", "AccessCode = ''" };
            foreach (UserAccessCode accessCode in user.AccessCodes)
            {
                if (string.IsNullOrEmpty(accessCode?.AccessCode))
                    continue;

                parts.Add($"AccessCode = '{accessCode.AccessCode.Replace("'", "''")}'");
            }

            return string.Join(" OR ", parts);
        }

        public static void ApplyDetailGridFilter(BankReceiptPM pm)
        {
            if (pm?.ActiveBO?.CurrentRow == null)
                return;

            if (!IsFieldAvailable(pm.ActiveBO.Data?.Tables[ItemTableName]))
                return;

            LiveGridControl gridDetail = pm.FCtrl("gridDetail") as LiveGridControl;
            if (gridDetail == null)
                return;

            DataView view = pm.ActiveBO.CurrentRow.CreateChildView(ItemHeaderForeignKey);
            string accessFilter = BuildDetailRowFilter(SysMng.Instance.getSession()?.ActiveUser);
            if (!string.IsNullOrEmpty(accessFilter))
            {
                string baseFilter = view.RowFilter;
                view.RowFilter = string.IsNullOrEmpty(baseFilter)
                    ? accessFilter
                    : $"({baseFilter}) AND ({accessFilter})";
            }

            gridDetail.ItemsSource = view;
        }

        public static void EnsureDetailColumn(ReceiptColumnCollection columns)
        {
            if (columns == null || !IsFieldAvailable())
                return;

            ReceiptColumn column = columns.FirstOrDefault(c => c.ColumnName == FieldAccessCode);
            if (column == null)
            {
                column = new ReceiptColumn
                {
                    ColumnName = FieldAccessCode,
                    Caption = SLanguage.GetString("Erişim Kodu"),
                    EditorType = EditorType.ComboBox,
                    ComboLookup = "AccessCodeList",
                    ComboDisplayMember = "Display",
                    ComboValueMember = "Value",
                    Width = 100,
                    UsageType = FieldUsage.None,
                    IsVisible = true
                };
                columns.Add(column);
                return;
            }

            column.Caption = SLanguage.GetString("Erişim Kodu");
            column.EditorType = EditorType.ComboBox;
            column.ComboLookup = "AccessCodeList";
            column.ComboDisplayMember = "Display";
            column.ComboValueMember = "Value";
            column.Width = 100;
            column.UsageType = FieldUsage.None;
            column.IsVisible = true;
        }

        public static void SetDefaultAccessCodeForNewItem(DataRow itemRow, AuthUserInfo user = null)
        {
            if (!IsFieldAvailable(itemRow?.Table) || !IsUsableDataRow(itemRow))
                return;

            if (!itemRow.IsNull(FieldAccessCode) && !string.IsNullOrWhiteSpace(itemRow[FieldAccessCode].ToString()))
                return;

            user ??= SysMng.Instance.getSession()?.ActiveUser;
            if (user?.AccessCodes == null || user.AccessCodes.Count == 0)
                return;

            string defaultCode = user.AccessCodes[0]?.AccessCode;
            if (string.IsNullOrEmpty(defaultCode))
                return;

            itemRow[FieldAccessCode] = defaultCode;
        }

        public static void DisableItemAccessCodeConditionalKeyField(BusinessObjectBase businessObject)
        {
            if (businessObject?.ConditionalKeyFields == null)
                return;

            businessObject.ConditionalKeyFields.RemoveAll(wf =>
                wf?.InnerConditions != null
                && wf.InnerConditions.Any(c =>
                    string.Equals(c.TableName, ItemTableName, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(c.FieldName, FieldAccessCode, StringComparison.OrdinalIgnoreCase)));
        }
    }
}
