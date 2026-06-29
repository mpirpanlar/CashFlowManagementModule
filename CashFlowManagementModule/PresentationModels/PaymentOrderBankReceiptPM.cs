using CashFlowManagementModule.BoExtensions;

using Prism.Ioc;

using Sentez.BankModule.PresentationModels;
using Sentez.Common.Commands;

namespace CashFlowManagementModule.PresentationModels
{
    public class PaymentOrderBankReceiptPM : BankReceiptPM
    {
        public PaymentOrderBankReceiptPM(IContainerExtension container) : base(container)
        {
        }

        public override bool CanApprovedChangeCommand(ISysCommandParam obj)
        {
            if (BankReceiptPaymentOrderHelper.IsPaymentOrderContext(this))
                return BankReceiptPaymentOrderApprovalHelper.CanToggleHeaderApproval(this);

            return base.CanApprovedChangeCommand(obj);
        }

        public override void OnApprovedChangeCommand(ISysCommandParam obj)
        {
            if (BankReceiptPaymentOrderHelper.IsPaymentOrderContext(this))
            {
                BankReceiptPaymentOrderApprovalHelper.ExecuteHeaderApprovalToggle(this, obj);
                return;
            }

            base.OnApprovedChangeCommand(obj);
        }
    }
}
