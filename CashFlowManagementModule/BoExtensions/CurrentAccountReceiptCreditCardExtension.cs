using System;

using System.ComponentModel;

using System.Data;

using System.Data.Common;

using System.Linq;



using CashFlowManagementModule.Services;



using Sentez.Common.SystemServices;



using Sentez.Data.BusinessObjects;



using Sentez.Localization;



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



        static bool IsCustomerCreditCardCollectionReceipt(BusinessObjectBase businessObject)

        {

            if (businessObject?.CurrentRow?.Row == null) return false;

            if (businessObject.CurrentRow.Row.IsNull("ReceiptType")) return false;

            return Convert.ToInt16(businessObject.CurrentRow.Row["ReceiptType"]) == 50;

        }



        static bool IsPlanningLinkedCreditCardReceipt(BusinessObjectBase businessObject)

        {

            return IsOwnCreditCardReceipt(businessObject) || IsCustomerCreditCardCollectionReceipt(businessObject);

        }



        static bool IsCreditCardReceipt(BusinessObjectBase businessObject)

        {

            if (businessObject?.CurrentRow?.Row == null) return false;

            if (businessObject.CurrentRow.Row.IsNull("ReceiptType")) return false;



            ReceiptTypeDefinition receiptType = CurrentAccountReceiptType.GetCurrentAccountReceiptType(

                Convert.ToInt32(businessObject.CurrentRow.Row["ReceiptType"]));

            return receiptType != null && receiptType.IsCreditCard;

        }



        CashFlowDbContext GetDbContext()

        {

            return CashFlowDbContext.FromBusinessObject(BusinessObject);

        }



        protected override void OnAfterGet(object sender, EventArgs e)

        {

            base.OnAfterGet(sender, e);

            if (!IsPlanningLinkedCreditCardReceipt(BusinessObject)) return;



            CurrentAccountReceiptCreditCardHelper.EnsureCurrentAccountReceiptItemColumns(BusinessObject.Data);

        }



        protected override void OnPreBeforePost(object sender, CancelEventArgs e)

        {

            base.OnPreBeforePost(sender, e);

            if (e.Cancel || !IsCreditCardReceipt(BusinessObject)) return;

            PrepareCreditCardItemInstallmentFields();

        }



        protected override void OnBeforePost(object sender, CancelEventArgs e)

        {

            base.OnBeforePost(sender, e);

            if (e.Cancel || !IsOwnCreditCardReceipt(BusinessObject)) return;



            CashFlowDbContext context = GetDbContext();

            foreach (DataRow itemRow in BusinessObject.Data.Tables["Erp_CurrentAccountReceiptItem"].Rows.Cast<DataRow>()

                         .Where(r => r.RowState != DataRowState.Deleted && !r.IsNull("BankAccountId")))

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

            if (!IsOwnCreditCardReceipt(BusinessObject)) return;



            LiveSession session = BusinessObject.ActiveSession as LiveSession;

            if (session == null) return;



            CashFlowDbContext context = GetDbContext();

            foreach (DataRow itemRow in BusinessObject.Data.Tables["Erp_CurrentAccountReceiptItem"].Rows.Cast<DataRow>()

                         .Where(r => r.RowState != DataRowState.Deleted && !r.IsNull("BankAccountId")))

            {

                long bankAccountId = Convert.ToInt64(itemRow["BankAccountId"]);

                if (!CreditCardStatementDataService.IsCreditCardBankAccount(context, bankAccountId))

                    continue;



                if (itemRow.IsNull("RecId")) continue;



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

            if (!IsCreditCardReceipt(BusinessObject)) return;



            LiveSession session = BusinessObject.ActiveSession as LiveSession;

            if (session == null) return;



            CashFlowDbContext context = CashFlowDbContext.FromSession(session);

            if (!context.IsValid)

                context = GetDbContext();

            if (!context.IsValid) return;



            CreditCardReceiptPaymentItemTermDateService.ReviseTermDates(context, BusinessObject, session);

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

                CreditCardStatementAllocationService.SoftDeleteByCurrentAccountReceipt(GetDbContext(), session, receiptId);

        }



        void PrepareCreditCardItemInstallmentFields()

        {

            DataRow headerRow = BusinessObject.CurrentRow?.Row;

            DataTable itemTable = BusinessObject.Data?.Tables["Erp_CurrentAccountReceiptItem"];

            if (itemTable == null) return;



            CashFlowDbContext context = GetDbContext();

            foreach (DataRow itemRow in itemTable.Rows.Cast<DataRow>().Where(r => r.RowState != DataRowState.Deleted))

            {

                if (itemRow.IsNull("BankAccountId")) continue;

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

            if (!context.IsValid || itemRow == null || itemRow.IsNull("RecId"))

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

            if (!context.IsValid || currentAccountReceiptItemRow == null || currentAccountReceiptItemRow.IsNull("RecId"))

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


