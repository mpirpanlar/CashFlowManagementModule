using System;
using System.ComponentModel;
using System.Data;
using System.Linq;

using CashFlowManagementModule.Services;

using Sentez.Common.SystemServices;
using Sentez.Data.BusinessObjects;
using Sentez.Localization;

namespace CashFlowManagementModule.BoExtensions
{
    public class CurrentAccountReceiptCreditCardExtension : BoExtensionBase
    {
        long _pendingDeleteReceiptId;
        short _pendingDeleteReceiptType;

        public CurrentAccountReceiptCreditCardExtension(BusinessObjectBase bo)
            : base(bo)
        {
        }

        static bool TryGetHeaderField(DataRow row, string columnName, DataRowVersion version, out object value)
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
                if (!row.HasVersion(version))
                {
                    if (row.HasVersion(DataRowVersion.Original))
                        version = DataRowVersion.Original;
                    else if (row.HasVersion(DataRowVersion.Current))
                        version = DataRowVersion.Current;
                    else
                        return false;
                }

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

        static DataRowVersion ResolveReadableVersion(DataRow row)
        {
            if (row == null)
                return DataRowVersion.Default;

            if (row.RowState == DataRowState.Deleted && row.HasVersion(DataRowVersion.Original))
                return DataRowVersion.Original;

            if (row.HasVersion(DataRowVersion.Current))
                return DataRowVersion.Current;

            if (row.HasVersion(DataRowVersion.Original))
                return DataRowVersion.Original;

            return DataRowVersion.Default;
        }

        static bool TryGetReceiptType(BusinessObjectBase businessObject, out short receiptType)
        {
            receiptType = 0;
            DataRow row = businessObject?.CurrentRow?.Row;
            if (!TryGetHeaderField(row, "ReceiptType", ResolveReadableVersion(row), out object value))
                return false;

            receiptType = Convert.ToInt16(value);
            return true;
        }

        static bool IsOwnCreditCardReceipt(BusinessObjectBase businessObject)
        {
            return TryGetReceiptType(businessObject, out short receiptType) && receiptType == 51;
        }

        static bool IsCustomerCreditCardCollectionReceipt(BusinessObjectBase businessObject)
        {
            return TryGetReceiptType(businessObject, out short receiptType) && receiptType == 50;
        }

        static bool IsPlanningLinkedCreditCardReceipt(BusinessObjectBase businessObject)
        {
            return IsOwnCreditCardReceipt(businessObject) || IsCustomerCreditCardCollectionReceipt(businessObject);
        }

        static bool IsPlanningLinkedReceiptType(short receiptType)
        {
            return receiptType == 50 || receiptType == 51;
        }

        static bool IsCreditCardReceipt(BusinessObjectBase businessObject)
        {
            if (!TryGetReceiptType(businessObject, out short receiptType))
                return false;

            ReceiptTypeDefinition receiptTypeDefinition =
                CurrentAccountReceiptType.GetCurrentAccountReceiptType(receiptType);
            return receiptTypeDefinition != null && receiptTypeDefinition.IsCreditCard;
        }

        static bool IsUsableItemRow(DataRow row)
        {
            return row != null
                && row.RowState != DataRowState.Deleted
                && row.RowState != DataRowState.Detached;
        }

        CashFlowDbContext GetDbContext()
        {
            return CashFlowDbContext.FromBusinessObject(BusinessObject);
        }

        protected override void OnAfterGet(object sender, EventArgs e)
        {
            base.OnAfterGet(sender, e);
            if (!IsPlanningLinkedCreditCardReceipt(BusinessObject))
                return;

            CurrentAccountReceiptCreditCardHelper.EnsureCurrentAccountReceiptItemColumns(BusinessObject.Data);
        }

        protected override void OnPreBeforePost(object sender, CancelEventArgs e)
        {
            base.OnPreBeforePost(sender, e);
            if (e.Cancel || !IsCreditCardReceipt(BusinessObject))
                return;

            PrepareCreditCardItemInstallmentFields();
        }

        protected override void OnBeforePost(object sender, CancelEventArgs e)
        {
            base.OnBeforePost(sender, e);
            if (e.Cancel || !IsPlanningLinkedCreditCardReceipt(BusinessObject))
                return;

            if (!BusinessObject.Data.Tables.Contains("Erp_CurrentAccountReceiptItem"))
                return;

            CashFlowDbContext context = GetDbContext();
            foreach (DataRow itemRow in BusinessObject.Data.Tables["Erp_CurrentAccountReceiptItem"].Rows.Cast<DataRow>()
                         .Where(IsUsableItemRow)
                         .Where(r => !r.IsNull("BankAccountId")))
            {
                long bankAccountId = Convert.ToInt64(itemRow["BankAccountId"]);
                if (!CreditCardStatementDataService.IsCreditCardBankAccount(context, bankAccountId))
                    continue;

                if (CreditCardStatementDataService.HasActivePeriods(context, bankAccountId))
                    continue;

                string accountName = CreditCardStatementDataService.GetBankAccountDisplayName(context, bankAccountId);
                BusinessObject.ErrorMessage = string.IsNullOrWhiteSpace(accountName)
                    ? SLanguage.GetString("Önce kredi kartı dönemlerini oluşturun.")
                    : string.Format(SLanguage.GetString("{0} kartı için önce kredi kartı dönemlerini oluşturun."), accountName);
                e.Cancel = true;
                return;
            }
        }

        protected override void OnAfterPost(object sender, EventArgs e)
        {
            base.OnAfterPost(sender, e);
            if (!IsPlanningLinkedCreditCardReceipt(BusinessObject))
                return;

            LiveSession session = BusinessObject.ActiveSession as LiveSession;
            if (session == null)
                return;

            if (!BusinessObject.Data.Tables.Contains("Erp_CurrentAccountReceiptItem"))
                return;

            CashFlowDbContext context = GetDbContext();
            foreach (DataRow itemRow in BusinessObject.Data.Tables["Erp_CurrentAccountReceiptItem"].Rows.Cast<DataRow>()
                         .Where(IsUsableItemRow)
                         .Where(r => !r.IsNull("BankAccountId")))
            {
                long bankAccountId = Convert.ToInt64(itemRow["BankAccountId"]);
                if (!CreditCardStatementDataService.IsCreditCardBankAccount(context, bankAccountId))
                    continue;

                if (itemRow.IsNull("RecId"))
                    continue;

                EnsureInstallmentFieldsPersisted(context, itemRow);

                long currentAccountReceiptItemId = Convert.ToInt64(itemRow["RecId"]);
                object existingCount = CashFlowDbAccess.ExecuteScalar(
                    context,
                    $@"select count(1) from Erp_BankAccountCreditCardPeriodAllocation with (nolock)
                       where CurrentAccountReceiptItemId={currentAccountReceiptItemId} and IsNull(IsDeleted,0)=0");
                if (existingCount != null && Convert.ToInt32(existingCount) > 0)
                    continue;

                DataRow bankReceiptItemRow = FindLinkedBankReceiptItem(context, itemRow);
                if (bankReceiptItemRow == null)
                    continue;

                CreditCardStatementAllocationService.AllocateFromBankReceiptItem(context, session, bankReceiptItemRow, itemRow);
            }
        }

        protected override void OnAfterSucceededPost(object sender, EventArgs e)
        {
            base.OnAfterSucceededPost(sender, e);
            if (!IsCreditCardReceipt(BusinessObject))
                return;

            LiveSession session = BusinessObject.ActiveSession as LiveSession;
            if (session == null)
                return;

            CashFlowDbContext context = CashFlowDbContext.FromSession(session);
            if (!context.IsValid)
                context = GetDbContext();
            if (!context.IsValid)
                return;

            CreditCardReceiptPaymentItemTermDateService.ReviseTermDates(context, BusinessObject, session);
        }

        protected override void OnBeforeDelete(object sender, CancelEventArgs e)
        {
            base.OnBeforeDelete(sender, e);

            _pendingDeleteReceiptId = 0;
            _pendingDeleteReceiptType = 0;

            DataRow headerRow = BusinessObject?.CurrentRow?.Row;
            DataRowVersion version = ResolveReadableVersion(headerRow);

            if (TryGetHeaderField(headerRow, "ReceiptType", version, out object receiptTypeValue))
                _pendingDeleteReceiptType = Convert.ToInt16(receiptTypeValue);

            if (TryGetHeaderField(headerRow, "RecId", version, out object receiptIdValue))
                _pendingDeleteReceiptId = Convert.ToInt64(receiptIdValue);
        }

        protected override void OnAfterDelete(object sender, EventArgs e)
        {
            base.OnAfterDelete(sender, e);

            if (!IsPlanningLinkedReceiptType(_pendingDeleteReceiptType))
                return;

            LiveSession session = BusinessObject.ActiveSession as LiveSession;
            if (session == null)
                return;

            if (_pendingDeleteReceiptId > 0)
                CreditCardStatementAllocationService.SoftDeleteByCurrentAccountReceipt(
                    GetDbContext(),
                    session,
                    _pendingDeleteReceiptId);

            _pendingDeleteReceiptId = 0;
            _pendingDeleteReceiptType = 0;
        }

        void PrepareCreditCardItemInstallmentFields()
        {
            DataRow headerRow = BusinessObject.CurrentRow?.Row;
            if (!IsUsableItemRow(headerRow))
                headerRow = null;

            DataTable itemTable = BusinessObject.Data?.Tables["Erp_CurrentAccountReceiptItem"];
            if (itemTable == null)
                return;

            CashFlowDbContext context = GetDbContext();
            foreach (DataRow itemRow in itemTable.Rows.Cast<DataRow>().Where(IsUsableItemRow))
            {
                if (itemRow.IsNull("BankAccountId"))
                    continue;

                if (!CreditCardStatementDataService.IsCreditCardBankAccount(context, Convert.ToInt64(itemRow["BankAccountId"])))
                    continue;

                if (itemRow.Table.Columns.Contains(CurrentAccountReceiptCreditCardHelper.FieldInstallmentCount)
                    && (itemRow.IsNull(CurrentAccountReceiptCreditCardHelper.FieldInstallmentCount)
                        || Convert.ToInt16(itemRow[CurrentAccountReceiptCreditCardHelper.FieldInstallmentCount]) < 1))
                {
                    itemRow[CurrentAccountReceiptCreditCardHelper.FieldInstallmentCount] = (short)1;
                }

                CurrentAccountReceiptCreditCardHelper.EnsureInstalmentStartDate(itemRow, headerRow);
            }
        }

        static void EnsureInstallmentFieldsPersisted(CashFlowDbContext context, DataRow itemRow)
        {
            if (!context.IsValid || !IsUsableItemRow(itemRow) || itemRow.IsNull("RecId"))
                return;

            long recId = Convert.ToInt64(itemRow["RecId"]);
            short? installmentCount = null;
            if (itemRow.Table.Columns.Contains(CurrentAccountReceiptCreditCardHelper.FieldInstallmentCount)
                && !itemRow.IsNull(CurrentAccountReceiptCreditCardHelper.FieldInstallmentCount))
            {
                short value = Convert.ToInt16(itemRow[CurrentAccountReceiptCreditCardHelper.FieldInstallmentCount]);
                if (value >= 1)
                    installmentCount = value;
            }

            DateTime? instalmentStartDate = null;
            if (itemRow.Table.Columns.Contains(CurrentAccountReceiptCreditCardHelper.FieldInstalmentStartDate)
                && !itemRow.IsNull(CurrentAccountReceiptCreditCardHelper.FieldInstalmentStartDate))
                instalmentStartDate = Convert.ToDateTime(itemRow[CurrentAccountReceiptCreditCardHelper.FieldInstalmentStartDate]).Date;

            if (!installmentCount.HasValue && !instalmentStartDate.HasValue)
                return;

            object dbInstallmentCount = itemRow.Table.Columns.Contains(CurrentAccountReceiptCreditCardHelper.FieldInstallmentCount)
                ? CashFlowDbAccess.ExecuteScalar(context, $"select InstallmentCount from Erp_CurrentAccountReceiptItem with (nolock) where RecId={recId}")
                : null;

            object dbInstalmentStartDate = itemRow.Table.Columns.Contains(CurrentAccountReceiptCreditCardHelper.FieldInstalmentStartDate)
                ? CashFlowDbAccess.ExecuteScalar(context, $"select InstalmentStartDate from Erp_CurrentAccountReceiptItem with (nolock) where RecId={recId}")
                : null;

            bool needsUpdate = false;
            if (installmentCount.HasValue)
            {
                short dbCount = dbInstallmentCount == null || dbInstallmentCount == DBNull.Value
                    ? (short)0
                    : Convert.ToInt16(dbInstallmentCount);
                if (dbCount != installmentCount.Value)
                    needsUpdate = true;
            }

            if (instalmentStartDate.HasValue)
            {
                DateTime dbDate = dbInstalmentStartDate == null || dbInstalmentStartDate == DBNull.Value
                    ? DateTime.MinValue
                    : Convert.ToDateTime(dbInstalmentStartDate).Date;
                if (dbDate != instalmentStartDate.Value)
                    needsUpdate = true;
            }

            if (!needsUpdate)
                return;

            var setParts = new System.Collections.Generic.List<string>();
            if (installmentCount.HasValue)
                setParts.Add($"InstallmentCount={installmentCount.Value}");
            if (instalmentStartDate.HasValue)
                setParts.Add($"InstalmentStartDate='{instalmentStartDate.Value:yyyy-MM-dd}'");

            CashFlowDbAccess.ExecuteNonQuery(
                context,
                $"update Erp_CurrentAccountReceiptItem set {string.Join(", ", setParts)} where RecId={recId}");
        }

        static DataRow FindLinkedBankReceiptItem(CashFlowDbContext context, DataRow currentAccountReceiptItemRow)
        {
            if (!context.IsValid || !IsUsableItemRow(currentAccountReceiptItemRow) || currentAccountReceiptItemRow.IsNull("RecId"))
                return null;

            long currentAccountReceiptItemId = Convert.ToInt64(currentAccountReceiptItemRow["RecId"]);
            return CashFlowDbAccess.ReadSingleRow(
                context,
                $@"select top 1 bri.*
                   from Erp_BankReceiptItem bri with (nolock)
                   inner join Erp_CurrentAccountReceiptItem cri with (nolock) on cri.CurrentAccountReceiptId=bri.UD_SourceId
                   where cri.RecId={currentAccountReceiptItemId}
                   order by bri.RecId desc",
                "Erp_BankReceiptItem");
        }
    }
}
