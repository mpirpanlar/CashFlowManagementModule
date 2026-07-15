using CashFlowManagementModule.BoExtensions;

using Sentez.Data.BusinessObjects;

namespace Sentez.CashFlowManagementModule
{
    public partial class CashFlowManagementModule
    {
        void RegisterCurrentAccountReceiptHooks()
        {
            RegisterCurrentAccountReceiptBoHooks();
            BusinessObjectBase.AddCustomExtension("CurrentAccountReceiptBO", typeof(CurrentAccountReceiptCreditCardExtension));
            BusinessObjectBase.AddCustomExtension("CurrentAccountReceiptBO", typeof(CurrentAccountReceiptPosMerchantExtension));
            BusinessObjectBase.AddCustomExtension("CurrentAccountReceiptBO", typeof(CurrentAccountReceiptPosTariffExtension));
            RegisterCurrentAccountReceiptPmHooks();
        }
    }
}
