using System;
using System.Data;
using System.Linq;

using CashFlowManagementModule.Services;

using Sentez.Common.SystemServices;

using Sentez.Data.BusinessObjects;

namespace CashFlowManagementModule.BoExtensions
{
    public class CurrentAccountReceiptCreditCardExtension : BoExtensionBase
    {
        public CurrentAccountReceiptCreditCardExtension(BusinessObjectBase bo)
            : base(bo)
        {
        }

        static bool IsOwnCreditCardReceipt(BusinessObjectBase businessObject)
        {
            if (businessObject?.CurrentRow?.Row == null) return false;
            if (businessObject.CurrentRow.Row.IsNull("ReceiptType")) return false;
            return Convert.ToInt16(businessObject.CurrentRow.Row["ReceiptType"]) == 51;
        }

        protected override void OnAfterPost(object sender, EventArgs e)
        {
            base.OnAfterPost(sender, e);
            if (!IsOwnCreditCardReceipt(BusinessObject)) return;

            LiveSession session = BusinessObject.ActiveSession as LiveSession;
            if (session == null) return;

            foreach (DataRow itemRow in BusinessObject.Data.Tables["Erp_CurrentAccountReceiptItem"].Rows.Cast<DataRow>()
                         .Where(r => r.RowState != DataRowState.Deleted && !r.IsNull("BankAccountId")))
            {
                long bankAccountId = Convert.ToInt64(itemRow["BankAccountId"]);
                if (!CreditCardStatementDataService.IsCreditCardBankAccount(BusinessObject.Connection, BusinessObject.Transaction, bankAccountId))
                    continue;

                if (itemRow.IsNull("RecId")) continue;
                long currentAccountReceiptItemId = Convert.ToInt64(itemRow["RecId"]);
                object existingCount = Sentez.Data.Tools.UtilityFunctions.SqlCustomScalarQuery(
                    BusinessObject.Connection,
                    BusinessObject.Transaction,
                    $@"select count(1) from Erp_BankAccountCreditCardPeriodAllocation with (nolock)
                       where CurrentAccountReceiptItemId={currentAccountReceiptItemId} and IsNull(IsDeleted,0)=0");
                if (existingCount != null && Convert.ToInt32(existingCount) > 0)
                    continue;

                DataRow bankReceiptItemRow = FindLinkedBankReceiptItem(session, itemRow);
                CreditCardStatementAllocationService.AllocateFromBankReceiptItem(session, bankReceiptItemRow, itemRow);
            }
        }

        protected override void OnAfterDelete(object sender, EventArgs e)
        {
            base.OnAfterDelete(sender, e);
            if (!IsOwnCreditCardReceipt(BusinessObject)) return;

            LiveSession session = BusinessObject.ActiveSession as LiveSession;
            if (session == null) return;

            long receiptId = BusinessObject.CurrentRow.Row.IsNull("RecId")
                ? 0L
                : Convert.ToInt64(BusinessObject.CurrentRow.Row["RecId"]);
            if (receiptId > 0)
                CreditCardStatementAllocationService.SoftDeleteByCurrentAccountReceipt(session, receiptId);
        }

        static DataRow FindLinkedBankReceiptItem(LiveSession session, DataRow currentAccountReceiptItemRow)
        {
            if (currentAccountReceiptItemRow == null || currentAccountReceiptItemRow.IsNull("RecId"))
                return null;

            long currentAccountReceiptItemId = Convert.ToInt64(currentAccountReceiptItemRow["RecId"]);
            using var reader = Sentez.Data.Tools.UtilityFunctions.SqlCustomQueryDTR(
                session._dbInfo.Connection,
                null,
                $@"select top 1 bri.*
                   from Erp_BankReceiptItem bri with (nolock)
                   inner join Erp_CurrentAccountReceiptItem cri with (nolock) on cri.CurrentAccountReceiptId=bri.UD_SourceId
                   where cri.RecId={currentAccountReceiptItemId}
                   order by bri.RecId desc");
            if (reader == null || !reader.Read())
                return null;

            var table = new DataTable("Erp_BankReceiptItem");
            for (int i = 0; i < reader.FieldCount; i++)
                table.Columns.Add(reader.GetName(i), reader.GetFieldType(i));

            object[] values = new object[reader.FieldCount];
            reader.GetValues(values);
            table.Rows.Add(values);
            return table.Rows[0];
        }
    }
}
