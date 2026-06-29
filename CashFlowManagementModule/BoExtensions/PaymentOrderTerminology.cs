using Sentez.Common.SystemServices;
using Sentez.Localization;

namespace CashFlowManagementModule.BoExtensions
{
    internal static class PaymentOrderTerminology
    {
        public const string ReceiptTypeDisplayNameKey = "15-Ödeme Planlama";

        public static string ReceiptTypeDisplayName => SLanguage.GetString(ReceiptTypeDisplayNameKey);

        public static string HeaderApprovalDeniedMessage =>
            SLanguage.GetString("Ödeme planlama başlık onaylı kayda müdahale yetkiniz bulunmamaktadır.");

        public static string LineApprovalDeniedMessage =>
            SLanguage.GetString("Ödeme planlama detayı onaylı kayda müdahale yetkiniz bulunmamaktadır.");

        public static string LineApprovalDeniedMessageAlt =>
            SLanguage.GetString("Ödeme planlama detay onaylı kayda müdahale yetkiniz bulunmamaktadır.");

        public static string LockedReceiptMessage =>
            SLanguage.GetString("Onaylanmış ödeme planlama fişi üzerinde işlem yapamazsınız.");

        public static string LockedLineMessage =>
            SLanguage.GetString("Onaylanmış ödeme planlama satırı üzerinde işlem yapamazsınız.");

        public static string LineApprovalSecurityName =>
            SLanguage.GetString("Ödeme Planlama Detayı Onaylı Kayda Müdahale");

        public static string HeaderApprovalSecurityName =>
            SLanguage.GetString("Ödeme Planlama Başlık Onaylı Kayda Müdahale");

        public static void ApplyBankReceiptTypeDisplayName()
        {
            ReceiptTypeDefinition def = BankReceiptType.GetBankReceiptType(BankReceiptPaymentOrderHelper.ReceiptType);
            if (def == null) return;

            def.TypeName = SLanguage.GetString("15-Ödeme Planlama");
            def.OriginalTypeName = SLanguage.GetString(1055, "15-Ödeme Planlama");
        }
    }
}
