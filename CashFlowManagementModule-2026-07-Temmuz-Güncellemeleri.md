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

2. **Banka Hesap Kartı — Pos hesabında kredi kartı detay sekmesi**

* **Sekme görünürlüğü:** Hesap tipi **Pos Hesabı** seçildiğinde banka hesap kartında **Kredi Kartı Detay Bilgileri** sekmesi görünür hale gelir.

* **Mevcut davranış:** Daha önce kredi kartı işareti ile açılan hesaplarda sekme görünmeye devam eder. Pos hesabı olmayan ve kredi kartı işareti bulunmayan hesaplarda sekme gizli kalır.

3. **Ödeme Planlama — Talimat dosyası oluşturma iyileştirmeleri**

* **Aynı banka hesabı zorunluluğu:** Talimat dosyası oluşturulurken seçilen satırların aynı banka hesabına ait olması gerekir. Farklı banka hesaplarına bağlı satırlar birlikte seçildiğinde işlem durdurulur ve uyarı mesajı gösterilir.

* **Banka hesabı tanımsız satırlar:** Seçili satırlardan birinde banka hesabı tanımlı değilse talimat dosyası oluşturulamaz; kullanıcıya uyarı verilir.

* **Önerilen dosya adı:** Kayıt penceresinde önerilen dosya adı fiş numarası ile gridde ilk seçilen satırın **Ödeme Tarihi** bilgisini içerir (örnek: `Talimat_OP-2026-001_2026-07-15.xlsx`). Satırda ödeme tarihi yoksa bugünün tarihi kullanılır.

## 09.07.2026 - Perşembe Güncellemeleri
1. **Banka Hesap Kartı — Pos ekstre bilgileri sekmesi**

* **Yeni sekme:** Hesap tipi **Pos Hesabı** seçildiğinde banka hesap kartında **Pos Ekstre Bilgileri** sekmesi görünür.

* **Kimlik alanları:** Sekmede **Üye İşyeri No** ve **Ekstre Görünüm Profili** alanları tanımlanabilir.

* **Kesinti oran profili:** Aynı sekmede Pos hesabına ait **Kesinti Oran Profili** gridinde kesinti türü, oran, sabit tutar ve geçerlilik tarihleri tanımlanır.

2. **Pos Kesinti Türleri — Tanım ekranı**

* **Yeni menü:** Finans Yönetimi → Nakit Akış Yönetimi altında **Pos Kesinti Türleri** listesi açılır; kesinti türleri (Üye İşyeri Ücreti, İşyeri Ödül Gideri, Servis Komisyonu vb.) yönetilir.

3. **Pos Ekstre Analizi — Üye iş yeri ekstre görünümü**

* **Veri kaynağı:** Ekstre görünümü **Müşteri Kredi Kartı Tahsilat (50)** ve **Müşteri Kredi Kartı İade (52)** fiş hareketlerinden hesaplanır; PDF veya dış dosya aktarımı yoktur.

* **Sekmeler / paneller:** Günlük hareket özeti, kesinti dökümü, hesaba geçişler, gelecek aylar alacakları ve yönetici özeti KPI’ları görüntülenir.

* **Kesinti hesabı:** Kesinti tutarları banka hesap kartındaki oran profilinden brüt tahsilat üzerinden hesaplanır; profil tanımlı değilse brüt/iade gösterilir, net ve kesinti için uyarı verilir.

* **Özet kaydet:** Seçili Pos hesabı ve dönem için hesaplanan özet veritabanına kaydedilebilir.

* **Mutabakat:** Ekranda fiş hareketleri ile hesaplanan brüt/iade tutarlarının uyumu kontrol edilir.

4. **Tahsilat Planlama — Pos hesaba geçiş aktarımı**

* **Yeni buton:** Tahsilat Planlama fiş ekranında **Pos Hesaba Geçişleri Aktar** komutu ile Pos hesaplarının seçili ay **hesaba geçiş net** tutarları (valor/TermDate günü bazında) fiş detayına satır olarak eklenir.

* **Kaynak:** Aktarım, Pos ekstre analizinde hesaplanan hesaba geçiş satırlarından yapılır; onaylı satırlar güncellenmez.

## 15.07.2026 - Çarşamba Güncellemeleri
1. **Banka Hesap Kartı — Üye İş Yeri Hesabı adlandırma**

* **Hesap tipi adı:** Banka hesap kartındaki hesap tipi listesinde daha önce **Pos Hesabı** olarak görünen seçenek artık **Üye İş Yeri Hesabı** olarak görünür.

* **Sekme adı:** Bu tip seçildiğinde açılan ekstre bilgileri sekmesi **Üye İş Yeri Ekstre Bilgileri** başlığıyla gösterilir.

2. **Üye İş Yeri — Kesinti oran matrisi**

* **Yeni kolonlar:** Üye iş yeri hesabındaki kesinti oran profilinde **Kart Kategorisi**, **Taksit Sayısı** ve **Bloke / Valör Günü** tanımlanabilir.

* **Kullanım:** Aynı kesinti türü için peşin ve taksitli işlemler ile farklı kart grupları (banka kartı, yurt içi, yurt dışı, AMEX vb.) ayrı satırlarla tarifelendirilir. Eski genel satırlar kategori/taksit boş bırakılarak tüm işlemler için geçerli kalır.

3. **Müşteri Kredi Kartı Tahsilat / Planlama — Kart sınıflandırma alanları**

* **Yeni satır alanları:** Cari fiş (50-Müşteri Kredi Kartı ile Tahsilat) ve Ödeme/Tahsilat Planlama satırlarında **Kart Kaynağı** (üye iş yeri / başka banka) ile **Kart Kategorisi** seçilebilir.

* **Otomatik oran ve valör:** Üye iş yeri hesabına bağlı tip 50 fiş kaydedilirken, satırdaki kart kategorisi ve taksit sayısına göre tarifedeki komisyon oranı satıra yazılır; bloke/valör günü tanımlıysa hesaba geçiş tarihi (vade) buna göre doldurulur. Kullanıcının girdiği farklı vade tarihi korunur.

* **Planlamadan üretme:** Tahsilat emrinden müşteri kredi kartı tahsilat fişi oluşturulurken kart kaynağı ve kategori alanları satıra taşınır.

4. **Pos Ekstre Analizi — Matrisli kesinti**

* **Hesaplama:** Ekstre kesintileri artık hareket satırındaki kart kategorisi ve taksit ile eşleşen profil satırlarına göre hesaplanır; genel (boş kategori/taksit) satırlar geriye uyumlu yedek kural olarak kalır.
