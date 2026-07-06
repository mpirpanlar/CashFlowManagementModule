using Sentez.BankModule.PresentationModels;
using Sentez.Common.SBase;

namespace CashFlowManagementModule.PresentationModels
{
    public static class BankReceiptPmAccess
    {
        public static BankReceiptPM GetBankReceiptPm(IPMBase pm)
        {
            if (pm is BankReceiptPmFactory factory) return factory.InnerPm;
            return pm as BankReceiptPM;
        }

        public static PaymentOrderBankReceiptPM GetPaymentOrderPm(IPMBase pm) => GetBankReceiptPm(pm) as PaymentOrderBankReceiptPM;

        public static CollectionOrderBankReceiptPM GetCollectionOrderPm(IPMBase pm) => GetBankReceiptPm(pm) as CollectionOrderBankReceiptPM;
    }
}
