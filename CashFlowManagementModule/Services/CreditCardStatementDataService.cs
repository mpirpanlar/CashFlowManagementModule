using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Linq;

using Sentez.Data.Tools;

namespace CashFlowManagementModule.Services
{
    /// <summary>
    /// Kredi kartı ekstre dönemleri, banka hesabı doğrulama ve taksit dağıtımı için ortak veri erişim servisi.
    /// </summary>
    public static class CreditCardStatementDataService
    {
        /// <summary>
        /// Ekstre kesim tarihine yakınlık uyarısı için kullanılan gün eşiği.
        /// </summary>
        public const int StatementCutNearDaysThreshold = 3;

        /// <summary>
        /// Verilen banka hesabının kredi kartı hesabı olup olmadığını kontrol eder.
        /// </summary>
        /// <param name="context">Veritabanı bağlantı ve transaction bilgisini taşıyan bağlam.</param>
        /// <param name="bankAccountId">Kontrol edilecek banka hesabı RecId değeri.</param>
        /// <returns>Hesap kredi kartı ise true, aksi halde false döner.</returns>
        public static bool IsCreditCardBankAccount(CashFlowDbContext context, long bankAccountId)
        {
            if (bankAccountId <= 0 || !context.IsValid) return false;

            object value = CashFlowDbAccess.ExecuteScalar(
                context,
                $"select IsNull(ForCreditCard,0) from Erp_BankAccount with (nolock) where RecId={bankAccountId}");
            return value != null && Convert.ToBoolean(value);
        }

        /// <summary>
        /// Verilen banka hesabının kredi kartı hesabı olup olmadığını kontrol eder (bağlantı/transaction overload).
        /// </summary>
        /// <param name="connection">Aktif veritabanı bağlantısı.</param>
        /// <param name="transaction">İsteğe bağlı aktif transaction; yoksa null.</param>
        /// <param name="bankAccountId">Kontrol edilecek banka hesabı RecId değeri.</param>
        /// <returns>Hesap kredi kartı ise true, aksi halde false döner.</returns>
        public static bool IsCreditCardBankAccount(DbConnection connection, DbTransaction transaction, long bankAccountId)
        {
            return IsCreditCardBankAccount(
                CashFlowDbContext.From(connection, transaction, default, keepConnectionOpen: true),
                bankAccountId);
        }

        /// <summary>
        /// Kredi kartı hesabı için tanımlı aktif (silinmemiş) ekstre dönemi olup olmadığını kontrol eder.
        /// </summary>
        /// <param name="context">Veritabanı bağlantı ve transaction bilgisini taşıyan bağlam.</param>
        /// <param name="bankAccountId">Kontrol edilecek kredi kartı banka hesabı RecId değeri.</param>
        /// <returns>En az bir aktif dönem varsa true, aksi halde false döner.</returns>
        public static bool HasActivePeriods(CashFlowDbContext context, long bankAccountId)
        {
            if (bankAccountId <= 0 || !context.IsValid) return false;

            object value = CashFlowDbAccess.ExecuteScalar(
                context,
                $@"select count(1) from Erp_BankAccountCreditCardPeriod with (nolock)
                   where BankAccountId={bankAccountId} and IsNull(IsDeleted,0)=0");
            return value != null && Convert.ToInt32(value) > 0;
        }

        /// <summary>
        /// Kredi kartı hesabı için tanımlı aktif ekstre dönemi olup olmadığını kontrol eder (bağlantı/transaction overload).
        /// </summary>
        /// <param name="connection">Aktif veritabanı bağlantısı.</param>
        /// <param name="transaction">İsteğe bağlı aktif transaction; yoksa null.</param>
        /// <param name="bankAccountId">Kontrol edilecek kredi kartı banka hesabı RecId değeri.</param>
        /// <returns>En az bir aktif dönem varsa true, aksi halde false döner.</returns>
        public static bool HasActivePeriods(DbConnection connection, DbTransaction transaction, long bankAccountId)
        {
            return HasActivePeriods(
                CashFlowDbContext.From(connection, transaction, default, keepConnectionOpen: true),
                bankAccountId);
        }

        /// <summary>
        /// Banka hesabının ekranda gösterilecek adını (hesap kodu + hesap adı) döndürür.
        /// </summary>
        /// <param name="context">Veritabanı bağlantı ve transaction bilgisini taşıyan bağlam.</param>
        /// <param name="bankAccountId">Görüntü adı alınacak banka hesabı RecId değeri.</param>
        /// <returns>Hesap kodu ve adından oluşan metin; kayıt bulunamazsa RecId string olarak döner.</returns>
        public static string GetBankAccountDisplayName(CashFlowDbContext context, long bankAccountId)
        {
            if (bankAccountId <= 0 || !context.IsValid) return string.Empty;

            DataTable table = CashFlowDbAccess.GetDataTable(
                context,
                "Erp_BankAccount",
                $@"select AccountCode, AccountName
                   from Erp_BankAccount with (nolock)
                   where RecId={bankAccountId}");
            if (table == null || table.Rows.Count == 0)
                return bankAccountId.ToString(CultureInfo.InvariantCulture);

            DataRow row = table.Rows[0];
            string code = row.IsNull("AccountCode") ? string.Empty : row["AccountCode"].ToString();
            string name = row.IsNull("AccountName") ? string.Empty : row["AccountName"].ToString();
            return string.IsNullOrWhiteSpace(name) ? code : $"{code} {name}".Trim();
        }

        /// <summary>
        /// Banka hesabının ekranda gösterilecek adını döndürür (bağlantı/transaction overload).
        /// </summary>
        /// <param name="provider">Veritabanı sağlayıcı tipi.</param>
        /// <param name="connection">Aktif veritabanı bağlantısı.</param>
        /// <param name="transaction">İsteğe bağlı aktif transaction; yoksa null.</param>
        /// <param name="bankAccountId">Görüntü adı alınacak banka hesabı RecId değeri.</param>
        /// <returns>Hesap kodu ve adından oluşan metin; kayıt bulunamazsa RecId string olarak döner.</returns>
        public static string GetBankAccountDisplayName(
            ProviderType provider,
            DbConnection connection,
            DbTransaction transaction,
            long bankAccountId)
        {
            return GetBankAccountDisplayName(
                CashFlowDbContext.From(connection, transaction, provider, keepConnectionOpen: true),
                bankAccountId);
        }

        /// <summary>
        /// Kredi kartı hesabına ait aktif ekstre dönemlerini son ödeme tarihine göre sıralı yükler.
        /// </summary>
        /// <param name="context">Veritabanı bağlantı ve transaction bilgisini taşıyan bağlam.</param>
        /// <param name="bankAccountId">Dönemleri yüklenecek kredi kartı banka hesabı RecId değeri.</param>
        /// <returns>Aktif dönem listesi; geçersiz parametre veya kayıt yoksa boş liste döner.</returns>
        public static IList<CreditCardPeriodInfo> LoadActivePeriods(CashFlowDbContext context, long bankAccountId)
        {
            var periods = new List<CreditCardPeriodInfo>();
            if (bankAccountId <= 0 || !context.IsValid) return periods;

            DataTable table = CashFlowDbAccess.GetDataTable(
                context,
                "Erp_BankAccountCreditCardPeriod",
                $@"select RecId, PeriodNo, PeriodYear, PeriodMonth, StatementStartDate, StatementDate, PaymentDueDate
                   from Erp_BankAccountCreditCardPeriod with (nolock)
                   where BankAccountId={bankAccountId} and IsNull(IsDeleted,0)=0
                   order by PaymentDueDate, PeriodNo");
            if (table == null) return periods;

            foreach (DataRow row in table.Rows)
            {
                short periodYear = Convert.ToInt16(row["PeriodYear"]);
                short periodMonth = Convert.ToInt16(row["PeriodMonth"]);
                periods.Add(new CreditCardPeriodInfo
                {
                    RecId = Convert.ToInt64(row["RecId"]),
                    PeriodNo = Convert.ToInt16(row["PeriodNo"]),
                    PeriodYear = periodYear,
                    PeriodMonth = periodMonth,
                    StatementStartDate = row.IsNull("StatementStartDate")
                        ? new DateTime(periodYear, periodMonth, 1)
                        : Convert.ToDateTime(row["StatementStartDate"]).Date,
                    StatementDate = Convert.ToDateTime(row["StatementDate"]).Date,
                    PaymentDueDate = Convert.ToDateTime(row["PaymentDueDate"]).Date
                });
            }

            return periods;
        }

        /// <summary>
        /// Kredi kartı hesabına ait aktif ekstre dönemlerini yükler (bağlantı/transaction overload).
        /// </summary>
        /// <param name="provider">Veritabanı sağlayıcı tipi.</param>
        /// <param name="connection">Aktif veritabanı bağlantısı.</param>
        /// <param name="transaction">İsteğe bağlı aktif transaction; yoksa null.</param>
        /// <param name="bankAccountId">Dönemleri yüklenecek kredi kartı banka hesabı RecId değeri.</param>
        /// <returns>Aktif dönem listesi; geçersiz parametre veya kayıt yoksa boş liste döner.</returns>
        public static IList<CreditCardPeriodInfo> LoadActivePeriods(
            ProviderType provider,
            DbConnection connection,
            DbTransaction transaction,
            long bankAccountId)
        {
            return LoadActivePeriods(
                CashFlowDbContext.From(connection, transaction, provider, keepConnectionOpen: true),
                bankAccountId);
        }

        /// <summary>
        /// Şirkete ait tüm aktif kredi kartı banka hesabı RecId değerlerini yükler.
        /// </summary>
        /// <param name="context">Veritabanı bağlantı ve transaction bilgisini taşıyan bağlam.</param>
        /// <param name="companyId">Kredi kartı hesapları listelenecek şirket RecId değeri.</param>
        /// <returns>Hesap koduna göre sıralı kredi kartı banka hesabı RecId listesi.</returns>
        public static IList<long> LoadCreditCardBankAccountIds(CashFlowDbContext context, int companyId)
        {
            var ids = new List<long>();
            if (!context.IsValid) return ids;

            DataTable table = CashFlowDbAccess.GetDataTable(
                context,
                "Erp_BankAccount",
                $@"select ba.RecId
                   from Erp_BankAccount ba with (nolock)
                   inner join Erp_Bank b with (nolock) on b.RecId = ba.BankId
                   where b.CompanyId = {companyId}
                     and IsNull(ba.ForCreditCard, 0) = 1
                     and IsNull(ba.IsDeleted, 0) = 0
                     and IsNull(b.IsDeleted, 0) = 0
                   order by ba.AccountCode");
            if (table == null) return ids;

            foreach (DataRow row in table.Rows)
                ids.Add(Convert.ToInt64(row["RecId"]));

            return ids;
        }

        /// <summary>
        /// Şirkete ait tüm aktif kredi kartı banka hesabı RecId değerlerini yükler (bağlantı/transaction overload).
        /// </summary>
        /// <param name="provider">Veritabanı sağlayıcı tipi.</param>
        /// <param name="connection">Aktif veritabanı bağlantısı.</param>
        /// <param name="transaction">İsteğe bağlı aktif transaction; yoksa null.</param>
        /// <param name="companyId">Kredi kartı hesapları listelenecek şirket RecId değeri.</param>
        /// <returns>Hesap koduna göre sıralı kredi kartı banka hesabı RecId listesi.</returns>
        public static IList<long> LoadCreditCardBankAccountIds(
            ProviderType provider,
            DbConnection connection,
            DbTransaction transaction,
            int companyId)
        {
            return LoadCreditCardBankAccountIds(
                CashFlowDbContext.From(connection, transaction, provider, keepConnectionOpen: true),
                companyId);
        }

        /// <summary>
        /// Ödeme referans tarihine göre ilk uygun ekstre döneminin liste indeksini bulur.
        /// Son ödeme tarihi (PaymentDueDate) referans tarihinden büyük veya eşit olan ilk dönem seçilir.
        /// </summary>
        /// <param name="periods">Aktif ekstre dönemleri listesi (genelde PaymentDueDate sıralı).</param>
        /// <param name="paymentReferenceDate">Ödeme veya tahsis için referans alınan tarih.</param>
        /// <returns>Uygun dönemin sıfır tabanlı indeksi; bulunamazsa -1 döner.</returns>
        public static int FindStartPeriodIndex(IList<CreditCardPeriodInfo> periods, DateTime paymentReferenceDate)
        {
            if (periods == null || periods.Count == 0) return -1;

            for (int i = 0; i < periods.Count; i++)
            {
                if (periods[i].PaymentDueDate.Date >= paymentReferenceDate.Date)
                    return i;
            }

            return -1;
        }

        /// <summary>
        /// Hareket tarihinin hangi ekstre kesim döngüsüne düştüğünü bulur.
        /// Tarih StatementStartDate ile StatementDate aralığındaysa o dönem; son dönemden sonraysa son dönem indeksi döner.
        /// </summary>
        /// <param name="periods">Aktif ekstre dönemleri listesi.</param>
        /// <param name="movementDate">Harcama veya taksit başlangıç tarihi gibi hareket tarihi.</param>
        /// <returns>Eşleşen dönemin sıfır tabanlı indeksi; ilk dönemden önceyse -1 döner.</returns>
        public static int FindPeriodIndexByStatementCycle(IList<CreditCardPeriodInfo> periods, DateTime movementDate)
        {
            if (periods == null || periods.Count == 0)
                return -1;

            var date = movementDate.Date;

            for (int i = 0; i < periods.Count; i++)
            {
                if (periods[i].StatementStartDate.Date <= date && date <= periods[i].StatementDate.Date)
                    return i;
            }

            if (date < periods[0].StatementStartDate.Date)
                return -1;

            if (date > periods[periods.Count - 1].StatementDate.Date)
                return periods.Count - 1;

            for (int i = 0; i < periods.Count; i++)
            {
                if (date <= periods[i].StatementDate.Date)
                    return i;
            }

            return -1;
        }

        /// <summary>
        /// Taksit tahsisi öncesi önizleme bilgisini oluşturur (dönemler, ilk taksit vadesi, hesap adı).
        /// </summary>
        /// <param name="context">Veritabanı bağlantı ve transaction bilgisini taşıyan bağlam.</param>
        /// <param name="bankAccountId">Önizleme yapılacak kredi kartı banka hesabı RecId değeri.</param>
        /// <param name="paymentReferenceDate">Taksit başlangıç veya ödeme referans tarihi.</param>
        /// <param name="installmentCount">Taksit sayısı (önizleme bağlamında kullanılır).</param>
        /// <returns>Dönem listesi ve ilk taksit vadesini içeren önizleme nesnesi.</returns>
        public static CreditCardAllocationPreview BuildAllocationPreview(
            CashFlowDbContext context,
            long bankAccountId,
            DateTime paymentReferenceDate,
            short installmentCount)
        {
            var periods = LoadActivePeriods(context, bankAccountId);
            int startIndex = FindPeriodIndexByStatementCycle(periods, paymentReferenceDate);
            DateTime firstDueDate = startIndex >= 0 ? periods[startIndex].PaymentDueDate : DateTime.MinValue;

            return new CreditCardAllocationPreview
            {
                BankAccountId = bankAccountId,
                BankAccountDisplayName = GetBankAccountDisplayName(context, bankAccountId),
                FirstInstallmentDueDate = firstDueDate,
                Periods = periods
            };
        }

        /// <summary>
        /// Taksit tahsisi önizleme bilgisini oluşturur (bağlantı/transaction overload).
        /// </summary>
        /// <param name="provider">Veritabanı sağlayıcı tipi.</param>
        /// <param name="connection">Aktif veritabanı bağlantısı.</param>
        /// <param name="transaction">İsteğe bağlı aktif transaction; yoksa null.</param>
        /// <param name="bankAccountId">Önizleme yapılacak kredi kartı banka hesabı RecId değeri.</param>
        /// <param name="paymentReferenceDate">Taksit başlangıç veya ödeme referans tarihi.</param>
        /// <param name="installmentCount">Taksit sayısı (önizleme bağlamında kullanılır).</param>
        /// <returns>Dönem listesi ve ilk taksit vadesini içeren önizleme nesnesi.</returns>
        public static CreditCardAllocationPreview BuildAllocationPreview(
            ProviderType provider,
            DbConnection connection,
            DbTransaction transaction,
            long bankAccountId,
            DateTime paymentReferenceDate,
            short installmentCount)
        {
            return BuildAllocationPreview(
                CashFlowDbContext.From(connection, transaction, provider, keepConnectionOpen: true),
                bankAccountId,
                paymentReferenceDate,
                installmentCount);
        }

        /// <summary>
        /// Toplam tutarı taksit sayısına eşit parçalara böler; kuruş farkı son taksite eklenir.
        /// </summary>
        /// <param name="totalAmount">Bölünecek toplam tutar.</param>
        /// <param name="installmentCount">Taksit sayısı; 1'den küçükse 1 kabul edilir.</param>
        /// <returns>Her taksit için tutar dizisi; uzunluk installmentCount kadardır.</returns>
        public static decimal[] SplitAmount(decimal totalAmount, short installmentCount)
        {
            if (installmentCount < 1) installmentCount = 1;

            var amounts = new decimal[installmentCount];
            decimal baseAmount = Math.Round(totalAmount / installmentCount, 2, MidpointRounding.AwayFromZero);
            decimal allocated = 0m;

            for (int i = 0; i < installmentCount - 1; i++)
            {
                amounts[i] = baseAmount;
                allocated += baseAmount;
            }

            amounts[installmentCount - 1] = totalAmount - allocated;
            return amounts;
        }

        /// <summary>
        /// Tarihi ekranda kullanılan standart formata (dd.MM.yyyy) dönüştürür.
        /// </summary>
        /// <param name="date">Biçimlendirilecek tarih.</param>
        /// <returns>dd.MM.yyyy formatında tarih metni.</returns>
        public static string FormatDate(DateTime date)
        {
            return date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
        }
    }
}
