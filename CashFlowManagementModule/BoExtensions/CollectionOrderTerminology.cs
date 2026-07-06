using System.Collections.Generic;
using System.Collections.ObjectModel;

using LiveCore.Desktop.SBase.MenuManager;

using Sentez.Common.Commands;
using Sentez.Common.SystemServices;
using Sentez.Localization;

namespace CashFlowManagementModule.BoExtensions
{
    internal static class CollectionOrderTerminology
    {
        public const string ReceiptTypeDisplayNameKey = "20-Tahsilat Planlama";

        public static string ReceiptTypeDisplayName => SLanguage.GetString(ReceiptTypeDisplayNameKey);

        public static string HeaderApprovalDeniedMessage =>
            SLanguage.GetString("Tahsilat planlama başlık onaylı kayda müdahale yetkiniz bulunmamaktadır.");

        public static string LineApprovalDeniedMessage =>
            SLanguage.GetString("Tahsilat planlama detayı onaylı kayda müdahale yetkiniz bulunmamaktadır.");

        public static string LineApprovalDeniedMessageAlt =>
            SLanguage.GetString("Tahsilat planlama detay onaylı kayda müdahale yetkiniz bulunmamaktadır.");

        public static string LockedReceiptMessage =>
            SLanguage.GetString("Onaylanmış tahsilat planlama fişi üzerinde işlem yapamazsınız.");

        public static string LockedLineMessage =>
            SLanguage.GetString("Onaylanmış tahsilat planlama satırı üzerinde işlem yapamazsınız.");

        public static string LineApprovalSecurityName =>
            SLanguage.GetString("Tahsilat Planlama Detayı Onaylı Kayda Müdahale");

        public static string HeaderApprovalSecurityName =>
            SLanguage.GetString("Tahsilat Planlama Başlık Onaylı Kayda Müdahale");

        public static void ApplyBankReceiptTypeDisplayName()
        {
            ReceiptTypeDefinition def = BankReceiptType.GetBankReceiptType(BankReceiptCollectionOrderHelper.ReceiptType);
            if (def == null) return;

            def.TypeName = SLanguage.GetString("20-Tahsilat Planlama");
            def.OriginalTypeName = SLanguage.GetString(1055, "20-Tahsilat Planlama");
        }

        public static void ApplyBankReceiptTypeDisplayNameAndRefreshMenus()
        {
            ApplyBankReceiptTypeDisplayName();
            RefreshAllCollectionOrderMenuCaptions();
        }

        public static void RefreshAllCollectionOrderMenuCaptions()
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

        static void RefreshMenuTreeReceiptCaptions(MenuItem menuItem, string type20Caption)
        {
            if (menuItem == null) return;

            if (menuItem.HasReceiptType)
            {
                menuItem.ReceiptChildren = null;
                UpdateType20ReceiptCaptions(menuItem.ReceiptChildren, type20Caption);
            }

            if (menuItem.Children == null) return;

            foreach (MenuItem child in menuItem.Children)
                RefreshMenuTreeReceiptCaptions(child, type20Caption);
        }

        static void UpdateType20ReceiptCaptions(IEnumerable<MenuItem> receiptChildren, string type20Caption)
        {
            if (receiptChildren == null) return;

            foreach (MenuItem item in receiptChildren)
            {
                if (IsType20MenuItem(item))
                    item.Caption = type20Caption;
            }
        }

        static void RefreshStatCounterMenuCaptions(MenuStatCounter counter, string type20Caption)
        {
            UpdateStatMenuItemCaptions(counter.LastUsedItems, type20Caption);
            UpdateStatMenuItemCaptions(counter.MostUsedItems, type20Caption);

            if (counter.PinnedItems != null)
            {
                foreach (MenuStatCounter.CategoryItem category in counter.PinnedItems)
                    UpdateStatMenuItemCaptions(category.Children, type20Caption);
            }

            counter.InvokePropertyChanged("ToolItems");
            counter.InvokePropertyChanged("ToolItemsSpecial");
        }

        static void UpdateStatMenuItemCaptions(ObservableCollection<MenuItem> items, string type20Caption)
        {
            if (items == null) return;

            foreach (MenuItem item in items)
            {
                if (IsType20MenuItem(item))
                    item.Caption = type20Caption;
            }
        }

        static bool IsType20MenuItem(MenuItem item)
        {
            if (item?.MenuItemCommandParam is SysCommandParam commandParam
                && commandParam.BoParamObj != null)
                return commandParam.BoParamObj.Type == BankReceiptCollectionOrderHelper.ReceiptType;

            return false;
        }
    }
}
