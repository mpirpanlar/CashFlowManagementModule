using System;
using System.ComponentModel;
using System.Data;

using CashFlowManagementModule.Services;

using Sentez.Common.SystemServices;
using Sentez.Data.BusinessObjects;

namespace CashFlowManagementModule.BoExtensions
{
    /// <summary>
    /// Tip 50 Müşteri Kredi Kartı Tahsilat satırlarında üye iş yeri tarifesinden InterestRate ve TermDate yazar.
    /// </summary>
    public class CurrentAccountReceiptPosTariffExtension : BoExtensionBase
    {
        public CurrentAccountReceiptPosTariffExtension(BusinessObjectBase bo)
            : base(bo)
        {
        }

        protected override void OnPreBeforePost(object sender, CancelEventArgs e)
        {
            base.OnPreBeforePost(sender, e);
            if (e.Cancel)
                return;

            ApplyTariffToItems();
        }

        void ApplyTariffToItems()
        {
            if (!IsCustomerCreditCardCollectionReceipt())
                return;

            LiveSession session = BusinessObject.ActiveSession as LiveSession;
            if (session?._dbInfo?.Connection == null || BusinessObject.Data == null)
                return;

            if (!BusinessObject.Data.Tables.Contains("Erp_CurrentAccountReceiptItem"))
                return;

            DateTime headerReceiptDate = ResolveHeaderReceiptDate();

            foreach (DataRow itemRow in BusinessObject.Data.Tables["Erp_CurrentAccountReceiptItem"].Rows)
            {
                if (itemRow.RowState == DataRowState.Deleted || itemRow.RowState == DataRowState.Detached)
                    continue;

                ApplyTariffToItem(session, itemRow, headerReceiptDate);
            }
        }

        bool IsCustomerCreditCardCollectionReceipt()
        {
            DataRow header = BusinessObject?.CurrentRow?.Row;
            if (header == null
                || header.RowState == DataRowState.Deleted
                || header.RowState == DataRowState.Detached
                || !header.Table.Columns.Contains("ReceiptType")
                || header.IsNull("ReceiptType"))
                return false;

            return Convert.ToInt16(header["ReceiptType"]) == BankAccountPosHelper.CustomerCreditCardCollectionReceiptType;
        }

        DateTime ResolveHeaderReceiptDate()
        {
            DataRow header = BusinessObject?.CurrentRow?.Row;
            if (header != null
                && header.Table.Columns.Contains("ReceiptDate")
                && !header.IsNull("ReceiptDate"))
                return Convert.ToDateTime(header["ReceiptDate"]).Date;

            return DateTime.Today;
        }

        static void ApplyTariffToItem(LiveSession session, DataRow itemRow, DateTime headerReceiptDate)
        {
            if (!itemRow.Table.Columns.Contains("BankAccountId") || itemRow.IsNull("BankAccountId"))
                return;

            long bankAccountId = Convert.ToInt64(itemRow["BankAccountId"]);
            if (!PosMerchantDataService.IsPosBankAccount(session, bankAccountId))
                return;

            DateTime asOfDate = ResolveItemReceiptDate(itemRow, headerReceiptDate);
            PosTariffResolution resolution = PosTariffResolveService.ResolveForReceiptItem(session, itemRow, asOfDate);
            if (!resolution.Found)
                return;

            if (itemRow.Table.Columns.Contains("InterestRate") && resolution.PrimaryRatePercent > 0m)
                itemRow["InterestRate"] = resolution.PrimaryRatePercent;

            if (resolution.BlockDays.HasValue && resolution.BlockDays.Value > 0 && itemRow.Table.Columns.Contains("TermDate"))
            {
                DateTime baseDate = ResolveValorBaseDate(itemRow, asOfDate);
                DateTime computedTermDate = baseDate.AddDays(resolution.BlockDays.Value);
                if (ShouldWriteTermDate(itemRow, asOfDate, computedTermDate))
                    itemRow["TermDate"] = computedTermDate;
            }
        }

        static DateTime ResolveItemReceiptDate(DataRow itemRow, DateTime headerReceiptDate)
        {
            if (itemRow.Table.Columns.Contains("ReceiptDate") && !itemRow.IsNull("ReceiptDate"))
                return Convert.ToDateTime(itemRow["ReceiptDate"]).Date;

            return headerReceiptDate;
        }

        static DateTime ResolveValorBaseDate(DataRow itemRow, DateTime receiptDate)
        {
            if (itemRow.Table.Columns.Contains(CurrentAccountReceiptCreditCardHelper.FieldInstalmentStartDate)
                && !itemRow.IsNull(CurrentAccountReceiptCreditCardHelper.FieldInstalmentStartDate))
                return Convert.ToDateTime(itemRow[CurrentAccountReceiptCreditCardHelper.FieldInstalmentStartDate]).Date;

            return receiptDate;
        }

        static bool ShouldWriteTermDate(DataRow itemRow, DateTime receiptDate, DateTime computedTermDate)
        {
            if (itemRow.IsNull("TermDate"))
                return true;

            DateTime existing = Convert.ToDateTime(itemRow["TermDate"]).Date;
            if (existing == receiptDate)
                return true;

            // Aynı valör zaten yazılmışsa tekrar yaz (idempotent).
            if (existing == computedTermDate)
                return true;

            // Kullanıcı manuel farklı tarih girmiş olabilir; üzerine yazma.
            return false;
        }
    }
}
