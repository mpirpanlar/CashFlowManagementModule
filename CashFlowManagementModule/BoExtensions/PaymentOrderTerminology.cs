using System.Collections.Generic;
using System.Collections.ObjectModel;

using LiveCore.Desktop.SBase.MenuManager;

using Sentez.Common.Commands;
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

        public static void ApplyBankReceiptTypeDisplayNameAndRefreshMenus()
        {
            ApplyBankReceiptTypeDisplayName();
            RefreshAllPaymentOrderMenuCaptions();
        }

        public static void RefreshAllPaymentOrderMenuCaptions()
        {
            string caption = ReceiptTypeDisplayName;
            LiveSession session = SysMng.Instance?.getSession();
            if (session == null) return;

            if (session.userMenu != null)
            {
                foreach (MenuItem menuItem in session.userMenu)
                    RefreshMenuTreeReceiptCaptions(menuItem, caption);
            }

            foreach (MenuItem menuItem in session.UserMenu)
                RefreshMenuTreeReceiptCaptions(menuItem, caption);

            if (SysMng.Instance.ActWndMng is DesktopWndMng desktopWndMng
                && desktopWndMng.MenuStatCounter is MenuStatCounter menuStatCounter)
            {
                RefreshStatCounterMenuCaptions(menuStatCounter, caption);
            }
        }

        static void RefreshMenuTreeReceiptCaptions(MenuItem menuItem, string type15Caption)
        {
            if (menuItem == null) return;

            if (menuItem.HasReceiptType)
            {
                menuItem.ReceiptChildren = null;
                UpdateType15ReceiptCaptions(menuItem.ReceiptChildren, type15Caption);
            }

            if (menuItem.Children == null) return;

            foreach (MenuItem child in menuItem.Children)
                RefreshMenuTreeReceiptCaptions(child, type15Caption);
        }

        static void UpdateType15ReceiptCaptions(IEnumerable<MenuItem> receiptChildren, string type15Caption)
        {
            if (receiptChildren == null) return;

            foreach (MenuItem item in receiptChildren)
            {
                if (IsType15MenuItem(item))
                    item.Caption = type15Caption;
            }
        }

        static void RefreshStatCounterMenuCaptions(MenuStatCounter counter, string type15Caption)
        {
            UpdateStatMenuItemCaptions(counter.LastUsedItems, type15Caption);
            UpdateStatMenuItemCaptions(counter.MostUsedItems, type15Caption);

            if (counter.PinnedItems != null)
            {
                foreach (MenuStatCounter.CategoryItem category in counter.PinnedItems)
                    UpdateStatMenuItemCaptions(category.Children, type15Caption);
            }

            counter.InvokePropertyChanged("ToolItems");
            counter.InvokePropertyChanged("ToolItemsSpecial");
        }

        static void UpdateStatMenuItemCaptions(ObservableCollection<MenuItem> items, string type15Caption)
        {
            if (items == null) return;

            foreach (MenuItem item in items)
            {
                if (IsType15MenuItem(item))
                    item.Caption = type15Caption;
            }
        }

        static bool IsType15MenuItem(MenuItem item)
        {
            if (item?.MenuItemCommandParam is SysCommandParam commandParam
                && commandParam.BoParamObj != null)
                return commandParam.BoParamObj.Type == BankReceiptPaymentOrderHelper.ReceiptType;

            return false;
        }
    }
}
