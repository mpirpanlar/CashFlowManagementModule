using System;
using System.Data;
using System.Windows;
using System.Windows.Controls;

using LiveCore.Desktop.UI.Controls;

using Sentez.BankModule.PresentationModels;
using Sentez.Common;
using Sentez.Common.Commands;
using Sentez.Common.PresentationModels;
using Sentez.Data.BusinessObjects;
using Sentez.Localization;

namespace CashFlowManagementModule.BoExtensions
{
    internal static class BankReceiptPaymentOrderApprovalHelper
    {
        public static bool CanToggleHeaderApproval(BankReceiptPM pm)
        {
            return BankReceiptPaymentOrderHelper.CanToggleHeaderApproval(pm?.ActiveBO?.CurrentRow?.Row);
        }

        public static void ExecuteHeaderApprovalToggle(BankReceiptPM pm, ISysCommandParam obj)
        {
            if (pm?.ActiveBO == null)
                throw new LiveCommandItemException("Uygun kayıt bulunamadı");

            DataRow headerRow = pm.ActiveBO.CurrentRow?.Row;
            if (headerRow == null)
                throw new LiveCommandItemException("Uygun kayıt bulunamadı");

            if (!BankReceiptPaymentOrderHelper.CanToggleHeaderApproval(headerRow))
                throw new LiveCommandItemException(PaymentOrderTerminology.HeaderApprovalDeniedMessage);

            if (pm.ActiveBO.IsNewRecord || pm.ActiveBO.HasDataChanges)
            {
                if (SysMng.Instance.ActWndMng.ShowMsgYesNo(
                        SLanguage.GetString("Değişiklikleri onaylıyor musunuz?"),
                        ConstantStr.Warning) == Sentez.Common.InformationMessages.MessageBoxResult.Yes)
                {
                    if (pm.ActiveBO.PostData() != PostResult.Succeed)
                        throw new LiveCommandItemException(pm.ActiveBO.ErrorMessage);
                }
                else
                    throw new LiveCommandItemException(SLanguage.GetString("Öncelikle değişiklikleri kayıt etmelisiniz."));
            }

            byte newApproved = ResolveNewApprovedValue(headerRow, obj);
            if (newApproved == 0
                && BankReceiptPaymentOrderHelper.GetPersistedApprovedValue(headerRow) == 1
                && !BankReceiptPaymentOrderHelper.HasHeaderApprovedEditRight())
                throw new LiveCommandItemException(PaymentOrderTerminology.HeaderApprovalDeniedMessage);

            BankReceiptPaymentOrderHelper.ApplyHeaderApprovalChange(headerRow, newApproved);

            if (pm.ActiveBO.PostData() != PostResult.Succeed)
                throw new LiveCommandItemException(pm.ActiveBO.ErrorMessage);

            pm.ActiveBO.Get(Convert.ToInt64(headerRow["RecId"]));
            RefreshPaymentOrderApprovalUi(pm);
        }

        public static void RefreshPaymentOrderApprovalUi(BankReceiptPM pm)
        {
            if (pm == null) return;

            pm.SetTitleName();

            if (pm is PMDesktop pmDesktop)
                pmDesktop.SetViewEnabled(!BankReceiptPaymentOrderHelper.ShouldLockPaymentOrder(pm.ActiveBO));

            if (pm.contextMenu == null) return;

            DataRow headerRow = pm.ActiveBO?.CurrentRow?.Row;
            bool canToggleHeaderApproval = BankReceiptPaymentOrderHelper.CanToggleHeaderApproval(headerRow);
            Visibility menuVisibility = canToggleHeaderApproval ? Visibility.Visible : Visibility.Collapsed;

            foreach (object item in pm.contextMenu.Items)
            {
                if (item is MenuItem menuItem
                    && (menuItem.Name == "ApprovedChangeCommand" || menuItem.Name == "Separator_ChangeApproved"))
                {
                    menuItem.Visibility = menuVisibility;
                }
            }
        }

        static byte ResolveNewApprovedValue(DataRow headerRow, ISysCommandParam obj)
        {
            if (obj != null && obj.Tag != null && byte.TryParse(obj.Tag.ToString(), out byte tagApproved))
                return tagApproved;

            byte currentApproved = BankReceiptPaymentOrderHelper.GetApprovedValue(headerRow);
            return currentApproved == 1 ? (byte)0 : (byte)1;
        }
    }
}
