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
            if (bo == null || parameter == null)
                return;

            PosCardClassificationHelper.EnsureCurrentAccountReceiptItemMetaDataFields();

            if (parameter.Type != 51 && parameter.Type != 50)
                return;

            CurrentAccountReceiptCreditCardHelper.EnsureCurrentAccountReceiptItemColumns(bo.Data);
            if (parameter.Type == 51)
                bo.ValueFiller.AddRule("Erp_CurrentAccountReceiptItem", CurrentAccountReceiptCreditCardHelper.FieldInstallmentCount, (short)1);
        }
    }
}
