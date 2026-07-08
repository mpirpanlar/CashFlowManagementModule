## 06.07.2026 - Pazartesi Güncellemeleri
1. **Ödeme / Tahsilat Planlama — Satır kayıt bilgileri**

* **Detayda yeni sütunlar:** Ödeme Planlama ve Tahsilat Planlama fişlerinin satır gridinde **Kayıt Zamanı**, **Kayıt Eden**, **Değişiklik Zamanı** ve **Değiştiren** sütunları görünür; değerler salt okunurdur.

* **Satır bazlı güncelleme:** Yeni satır kaydedildiğinde kayıt eden ve kayıt zamanı satıra yazılır. Mevcut bir satır değiştirildiğinde yalnızca değişiklik bilgileri güncellenir; fiş başlığındaki kullanıcı/zaman bilgileri satırı etkilemez.

* **Zaman formatı:** Zaman sütunları gün.ay.yıl saat:dakika:saniye biçiminde gösterilir.

2. **Ödeme Planlama — Talimat dosyası oluşturma**

* **Yeni buton:** Ödeme Planlama fiş ekranında **Talimat Dosyası Oluştur** butonu ile seçilen satırlar banka talimat Excel dosyasına aktarılır.

* **Kaynak banka:** Dosyanın üst bilgisi (banka adı, şube, IBAN) ekranda seçili ön değer banka hesabından doldurulur; ön değer banka seçilmeden işlem yapılamaz.

* **Satır aktarımı:** Seçilen satırların cari / alıcı bilgileri, IBAN ve tutarları şablondaki satırlara yazılır; kayıt yolu kullanıcıdan sorulur.

## 08.07.2026 - Çarşamba Güncellemeleri
1. **Banka Hesap Kartı — Pos Hesabı tipi eklendi**

* **Yeni hesap tipi:** CashFlowManagementModule (Nakit Akış Yönetimi) yüklüyken banka hesap kartındaki **Hesap Tipi** listesinde **Pos Hesabı** seçeneği görünür.

* **Kayıt:** Bu tip seçilerek kaydedilen hesaplar Pos hesabı olarak tutulur; modül yüklü değilse listede yalnızca standart tipler (Ticari, Kredi, Kredi Kartı, Vadeli) kalır.