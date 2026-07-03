using System;
using System.Data;
using System.Globalization;
using System.Linq;

using CashFlowManagementModule.BoExtensions;

using Sentez.Common.SystemServices;
using Sentez.Data.BusinessObjects;

namespace CashFlowManagementModule.Services
{
    /// <summary>
    /// Core'un oluşturduğu Erp_ReceiptPaymentItem kayıtlarının TermDate değerlerini,
    /// kredi kartı ekstre dönemlerinin PaymentDueDate alanına göre revize eder.
    /// </summary>
    public static class CreditCardReceiptPaymentItemTermDateService
    {
        /// <summary>
        /// Finance modülü kaynak modül numarası (Erp_ReceiptPaymentItem.SourceModule).
        /// </summary>
        const int FinanceSourceModule = 3;

        /// <summary>
        /// Cari fişteki tüm kredi kartı satırları için RPI vade tarihlerini ekstre dönemlerine göre günceller.
        /// </summary>
        /// <param name="context">Veritabanı bağlantı ve transaction bilgisini taşıyan bağlam.</param>
        /// <param name="businessObject">Cari fiş business object; CRI satırları bu nesnenin Data'sından okunur.</param>
        /// <param name="session">Aktif oturum; ileride audit veya kullanıcı bağlamı için kullanılabilir.</param>
        public static void ReviseTermDates(CashFlowDbContext context, BusinessObjectBase businessObject, LiveSession session)
        {
            if (!context.IsValid || businessObject?.Data?.Tables["Erp_CurrentAccountReceiptItem"] == null)
                return;

            DataRow headerRow = businessObject.CurrentRow?.Row;
            DataTable itemTable = businessObject.Data.Tables["Erp_CurrentAccountReceiptItem"];

            foreach (DataRow itemRow in itemTable.Rows.Cast<DataRow>()
                         .Where(r => r.RowState != DataRowState.Deleted))
            {
                ReviseTermDatesForItem(context, itemRow, headerRow);
            }
        }

        /// <summary>
        /// Tek bir CRI satırına bağlı ödenmemiş RPI kayıtlarının vadelerini ardışık ekstre dönemlerine eşler.
        /// </summary>
        /// <param name="context">Veritabanı bağlantı ve transaction bilgisini taşıyan bağlam.</param>
        /// <param name="itemRow">Revize edilecek cari fiş satırı (Erp_CurrentAccountReceiptItem).</param>
        /// <param name="headerRow">Cari fiş başlık satırı; InstalmentStartDate cascade için kullanılır.</param>
        static void ReviseTermDatesForItem(CashFlowDbContext context, DataRow itemRow, DataRow headerRow)
        {
            if (itemRow == null || itemRow.IsNull("RecId"))
                return;

            if (itemRow.Table.Columns.Contains("PaymentPlanId") && !itemRow.IsNull("PaymentPlanId"))
                return;

            if (itemRow.IsNull("BankAccountId"))
                return;

            long bankAccountId = Convert.ToInt64(itemRow["BankAccountId"]);
            if (!CreditCardStatementDataService.IsCreditCardBankAccount(context, bankAccountId))
                return;

            var periods = CreditCardStatementDataService.LoadActivePeriods(context, bankAccountId);
            if (periods == null || periods.Count == 0)
                return;

            DateTime? instalmentStartDate = CurrentAccountReceiptCreditCardHelper.ResolveInstalmentStartDate(itemRow, headerRow);
            if (!instalmentStartDate.HasValue)
                return;

            int startIndex = CreditCardStatementDataService.FindPeriodIndexByStatementCycle(periods, instalmentStartDate.Value);
            if (startIndex < 0)
                return;

            long criRecId = Convert.ToInt64(itemRow["RecId"]);
            DataTable rpiTable = LoadUnpaidReceiptPaymentItems(context, criRecId);
            if (rpiTable == null || rpiTable.Rows.Count == 0)
                return;

            for (int i = 0; i < rpiTable.Rows.Count; i++)
            {
                int periodIndex = startIndex + i;
                if (periodIndex >= periods.Count)
                    break;

                DataRow rpiRow = rpiTable.Rows[i];
                long rpiRecId = Convert.ToInt64(rpiRow["RecId"]);
                DateTime currentTermDate = Convert.ToDateTime(rpiRow["TermDate"]).Date;
                DateTime newTermDate = periods[periodIndex].PaymentDueDate.Date;

                if (currentTermDate == newTermDate)
                    continue;

                UpdateTermDate(context, rpiRecId, newTermDate);
            }
        }

        /// <summary>
        /// CRI satırına bağlı ödenmemiş ve silinmemiş ReceiptPaymentItem kayıtlarını yükler.
        /// </summary>
        /// <param name="context">Veritabanı bağlantı ve transaction bilgisini taşıyan bağlam.</param>
        /// <param name="criRecId">Kaynak cari fiş satırı RecId (SourceItemId).</param>
        /// <returns>TermDate ve RecId sıralı RPI satırları; kayıt yoksa boş tablo.</returns>
        static DataTable LoadUnpaidReceiptPaymentItems(CashFlowDbContext context, long criRecId)
        {
            return CashFlowDbAccess.GetDataTable(
                context,
                "Erp_ReceiptPaymentItem",
                $@"select RecId, TermDate, Amount, ItemOrderNo
                   from Erp_ReceiptPaymentItem with (nolock)
                   where SourceModule = {FinanceSourceModule}
                     and SourceItemId = {criRecId}
                     and isnull(IsDeleted,0) = 0
                     and isnull(IsPaid,0) = 0
                     and isnull(IsIntalmentPaid,0) = 0
                     and isnull(PaidAmount,0) = 0
                   order by TermDate, RecId");
        }

        /// <summary>
        /// Tek bir RPI kaydının TermDate alanını doğrudan SQL UPDATE ile günceller (PostData çağrılmaz).
        /// </summary>
        /// <param name="context">Veritabanı bağlantı ve transaction bilgisini taşıyan bağlam.</param>
        /// <param name="rpiRecId">Güncellenecek Erp_ReceiptPaymentItem RecId değeri.</param>
        /// <param name="newTermDate">Yeni vade tarihi (ekstre PaymentDueDate).</param>
        static void UpdateTermDate(CashFlowDbContext context, long rpiRecId, DateTime newTermDate)
        {
            string termDateLiteral = newTermDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

            CashFlowDbAccess.ExecuteNonQuery(
                context,
                $@"update Erp_ReceiptPaymentItem
                   set TermDate = '{termDateLiteral}', UpdatedAt = getdate()
                   where RecId = {rpiRecId}
                     and isnull(IsDeleted,0) = 0
                     and isnull(IsPaid,0) = 0
                     and isnull(IsIntalmentPaid,0) = 0
                     and isnull(PaidAmount,0) = 0");
        }
    }
}
