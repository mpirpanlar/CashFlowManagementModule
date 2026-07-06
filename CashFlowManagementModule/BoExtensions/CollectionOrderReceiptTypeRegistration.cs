using Sentez.Common.ModuleBase;
using Sentez.Common.SystemServices;
using Sentez.Localization;

namespace CashFlowManagementModule.BoExtensions
{
    internal static class CollectionOrderReceiptTypeRegistration
    {
        public static void RegisterCollectionOrderReceiptType()
        {
            const int receiptType = BankReceiptCollectionOrderHelper.ReceiptType;
            if (BankReceiptType.BankReceiptTypes.ContainsKey(receiptType))
                return;

            var def = new ReceiptTypeDefinition
            {
                Type = receiptType,
                TypeName = SLanguage.GetString("20-Tahsilat Planlama"),
                OriginalTypeName = SLanguage.GetString(1055, "20-Tahsilat Planlama"),
                TypeShortName = "TEP",
                LogicalModule = (int)Modules.FinanceModule,
                Module = (int)ReceiptTypeDefinition.ReceiptModules.Bank,
                Eft = true,
                IsPaymentOrder = true,
                Debit = true,
                Credit = false,
                CurrentAccountIntegration = false,
                GLAccountIntegration = true,
                BankAccountIntegration = false,
                Visible = true
            };
            BankReceiptType.BankReceiptTypes.Add(receiptType, def);
        }
    }
}
