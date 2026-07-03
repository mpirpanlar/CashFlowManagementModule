using CashFlowManagementModule.BoExtensions;

using Sentez.Core.ParameterClasses;
using Sentez.Data.BusinessObjects;

namespace Sentez.CashFlowManagementModule
{
    public partial class CashFlowManagementModule
    {
        void RegisterCurrentAccountReceiptBoHooks()
        {
            BusinessObjectBase.AddCustomInit("CurrentAccountReceiptBO", CurrentAccountReceiptBo_Init);
        }

        void CurrentAccountReceiptBo_Init(BusinessObjectBase bo, BoParam parameter)
        {
            if (bo == null || parameter == null || parameter.Type != 51)
                return;

            CurrentAccountReceiptCreditCardHelper.EnsureCurrentAccountReceiptItemColumns(bo.Data);
            bo.ValueFiller.AddRule("Erp_CurrentAccountReceiptItem", CurrentAccountReceiptCreditCardHelper.FieldInstallmentCount, (short)1);
        }
    }
}
