using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;

using CashFlowManagementModule.BoExtensions;

using Sentez.Data.BusinessObjects;
using Sentez.Data.Tools;
using Sentez.Localization;

namespace CashFlowManagementModule.Services
{
    public sealed class FixedPaymentImportResult
    {
        public int AddedCount { get; set; }
        public int SkippedCount { get; set; }
        public string Message { get; set; }
    }

    public static class FixedPaymentImportService
    {
        public static FixedPaymentImportResult Import(
            BusinessObjectBase businessObject,
            int companyId,
            DateTime receiptDate,
            long defaultBankAccountId)
        {
            var result = new FixedPaymentImportResult();

            if (businessObject?.Data == null || businessObject.CurrentRow?.Row == null)
            {
                result.Message = SLanguage.GetString("Fiş bilgisi bulunamadı.");
                return result;
            }

            if (defaultBankAccountId <= 0)
            {
                result.Message = SLanguage.GetString("Lütfen ön değer banka hesabını seçiniz.");
                return result;
            }

            if (receiptDate == DateTime.MinValue)
            {
                result.Message = SLanguage.GetString("Fiş tarihi geçersiz.");
                return result;
            }

            DataRow headerRow = businessObject.CurrentRow.Row;
            DataTable itemTable = businessObject.Data.Tables["Erp_BankReceiptItem"];
            if (itemTable == null)
            {
                result.Message = SLanguage.GetString("Fiş detay tablosu bulunamadı.");
                return result;
            }

            short receiptMonth = (short)receiptDate.Month;
            int receiptYear = receiptDate.Year;

            string udIsFixedPaymentFilter = SchemaHasUdIsFixedPayment()
                ? "and isnull(ca.UD_IsFixedPayment,0)=1"
                : string.Empty;

            string sql = $@"
select ca.RecId CurrentAccountId,
       ca.CurrentAccountCode,
       ca.CurrentAccountName,
       fps.PaymentDay,
       fps.PaymentMonth,
       fps.Amount,
       fps.FixedPaymentTypeId
from Erp_CurrentAccount ca with (nolock)
inner join Erp_CurrentAccountFixedPaymentSchedule fps with (nolock)
    on fps.CurrentAccountId = ca.RecId
where ca.CompanyId = {companyId}
  and isnull(ca.InUse,0)=1
  and isnull(ca.IsDeleted,0)=0
  {udIsFixedPaymentFilter}
  and fps.PaymentMonth = {receiptMonth}
  and isnull(fps.InUse,0)=1
  and isnull(fps.IsDeleted,0)=0
order by ca.CurrentAccountCode, fps.PaymentDay";

            DataTable scheduleData = UtilityFunctions.GetDataTableList(
                businessObject.Provider,
                businessObject.Connection,
                businessObject.Transaction,
                "FixedPaymentImport",
                sql);

            if (scheduleData == null || scheduleData.Rows.Count == 0)
            {
                result.Message = SLanguage.GetString("Bu ay için aktarılacak tekrar eden ödeme tanımı bulunamadı.");
                return result;
            }

            HashSet<string> existingKeys = BuildExistingItemKeys(itemTable);

            foreach (DataRow scheduleRow in scheduleData.Rows)
            {
                if (scheduleRow.IsNull("CurrentAccountId") || scheduleRow.IsNull("Amount")) continue;

                long currentAccountId = Convert.ToInt64(scheduleRow["CurrentAccountId"]);
                short paymentDay = scheduleRow.IsNull("PaymentDay") ? (short)1 : Convert.ToInt16(scheduleRow["PaymentDay"]);
                byte paymentMonth = scheduleRow.IsNull("PaymentMonth") ? (byte)receiptMonth : Convert.ToByte(scheduleRow["PaymentMonth"]);
                decimal amount = Convert.ToDecimal(scheduleRow["Amount"], CultureInfo.InvariantCulture);

                DateTime paymentDate = BuildPaymentDate(receiptYear, paymentMonth, paymentDay);
                long? fixedPaymentTypeId = scheduleRow.IsNull("FixedPaymentTypeId")
                    ? (long?)null
                    : Convert.ToInt64(scheduleRow["FixedPaymentTypeId"]);

                string duplicateKey = BuildItemKey(currentAccountId, paymentDate, fixedPaymentTypeId);
                if (existingKeys.Contains(duplicateKey))
                {
                    result.SkippedCount++;
                    continue;
                }

                DataRow itemRow = itemTable.NewRow();
                itemRow.SetParentRow(headerRow);
                itemTable.Rows.Add(itemRow);
                itemRow["BankAccountId"] = defaultBankAccountId;
                itemRow["CurrentAccountId"] = currentAccountId;
                itemRow["Credit"] = amount;
                itemRow["IsApproved"] = (byte)0;

                if (itemTable.Columns.Contains(BankReceiptCreditCardHelper.FieldInstallmentCount))
                    itemRow[BankReceiptCreditCardHelper.FieldInstallmentCount] = (short)1;

                if (itemTable.Columns.Contains(PaymentOrderUdFields.PaymentDate))
                    itemRow[PaymentOrderUdFields.PaymentDate] = paymentDate;

                if (fixedPaymentTypeId.HasValue && itemTable.Columns.Contains(BankReceiptFixedPaymentHelper.FieldFixedPaymentTypeId))
                    itemRow[BankReceiptFixedPaymentHelper.FieldFixedPaymentTypeId] = fixedPaymentTypeId.Value;

                if (itemTable.Columns.Contains("Explanation") && !scheduleRow.IsNull("CurrentAccountName"))
                    itemRow["Explanation"] = $"{scheduleRow["CurrentAccountName"]} - {SLanguage.GetString("Tekrar Eden Ödeme")}";

                existingKeys.Add(duplicateKey);
                result.AddedCount++;
            }

            result.Message = string.Format(
                SLanguage.GetString("{0} satır eklendi, {1} satır zaten mevcuttu."),
                result.AddedCount,
                result.SkippedCount);

            return result;
        }

        static bool SchemaHasUdIsFixedPayment()
        {
            try
            {
                return Sentez.Data.MetaData.Schema.Tables["Erp_CurrentAccount"].Fields.Contains(CurrentAccountFixedPaymentHelper.FieldIsFixedPayment);
            }
            catch
            {
                return false;
            }
        }

        static HashSet<string> BuildExistingItemKeys(DataTable itemTable)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);

            foreach (DataRow row in itemTable.Rows)
            {
                if (row.RowState == DataRowState.Deleted) continue;
                if (row.IsNull("CurrentAccountId")) continue;

                long currentAccountId = Convert.ToInt64(row["CurrentAccountId"]);
                DateTime? paymentDate = null;
                if (row.Table.Columns.Contains(PaymentOrderUdFields.PaymentDate) && !row.IsNull(PaymentOrderUdFields.PaymentDate))
                    paymentDate = Convert.ToDateTime(row[PaymentOrderUdFields.PaymentDate]).Date;

                long? fixedPaymentTypeId = null;
                if (row.Table.Columns.Contains(BankReceiptFixedPaymentHelper.FieldFixedPaymentTypeId)
                    && !row.IsNull(BankReceiptFixedPaymentHelper.FieldFixedPaymentTypeId))
                    fixedPaymentTypeId = Convert.ToInt64(row[BankReceiptFixedPaymentHelper.FieldFixedPaymentTypeId]);

                if (!paymentDate.HasValue) continue;
                keys.Add(BuildItemKey(currentAccountId, paymentDate.Value, fixedPaymentTypeId));
            }

            return keys;
        }

        static string BuildItemKey(long currentAccountId, DateTime paymentDate, long? fixedPaymentTypeId)
        {
            return $"{currentAccountId}|{paymentDate:yyyyMMdd}|{fixedPaymentTypeId ?? 0}";
        }

        static DateTime BuildPaymentDate(int year, int month, short day)
        {
            int maxDay = DateTime.DaysInMonth(year, month);
            int safeDay = Math.Min(Math.Max((int)day, 1), maxDay);
            return new DateTime(year, month, safeDay);
        }
    }
}
