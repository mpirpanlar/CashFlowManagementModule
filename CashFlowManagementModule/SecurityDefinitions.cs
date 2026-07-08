using Sentez.Common.ModuleBase;
using Sentez.Common.Security;
using Sentez.Localization;
using CashFlowManagementModule.BoExtensions;

namespace Sentez.CashFlowManagementModule
{
    class CashFlowManagementModuleSecurity
    {
        public static void RegisterSecurityDefinitions()
        {
            short moduleId = (short)Modules.ExternalModule16;
            short logicalModuleId = moduleId;

            SecurityDefinition mainSecurity = new SecurityDefinition(
                SLanguage.GetString("Aytur Nakit Akış Yönetimi"),
                logicalModuleId, moduleId, 0, 0, Privileges.Select);

            mainSecurity.AddChild(new SecurityDefinition(
                PaymentOrderTerminology.LineApprovalSecurityName,
                logicalModuleId, moduleId,
                (short)CashFlowManagementModuleSecurityItems.PaymentOrderLineApproval,
                (short)CashFlowManagementModuleSecuritySubItems.None,
                Privileges.Update));

            mainSecurity.AddChild(new SecurityDefinition(
                PaymentOrderTerminology.HeaderApprovalSecurityName,
                logicalModuleId, moduleId,
                (short)CashFlowManagementModuleSecurityItems.PaymentOrderHeaderApproval,
                (short)CashFlowManagementModuleSecuritySubItems.None,
                Privileges.Update));

            mainSecurity.AddChild(new SecurityDefinition(
                SLanguage.GetString("Tekrar Eden Ödeme Tipleri"),
                logicalModuleId, moduleId,
                (short)CashFlowManagementModuleSecurityItems.FixedPaymentType,
                (short)CashFlowManagementModuleSecuritySubItems.None,
                Privileges.Select | Privileges.Insert | Privileges.Update | Privileges.Delete));

            mainSecurity.AddChild(new SecurityDefinition(
                SLanguage.GetString("Tekrar Eden Ödemeleri Aktar"),
                logicalModuleId, moduleId,
                (short)CashFlowManagementModuleSecurityItems.FixedPaymentImport,
                (short)CashFlowManagementModuleSecuritySubItems.None,
                Privileges.Update));

            mainSecurity.AddChild(new SecurityDefinition(
                SLanguage.GetString("Kredi Kartı Harcamalarını Aktar"),
                logicalModuleId, moduleId,
                (short)CashFlowManagementModuleSecurityItems.CreditCardStatementSpendingImport,
                (short)CashFlowManagementModuleSecuritySubItems.None,
                Privileges.Update));

            mainSecurity.AddChild(new SecurityDefinition(
                SLanguage.GetString("Yaşlandırma Tutarlarını Aktar"),
                logicalModuleId, moduleId,
                (short)CashFlowManagementModuleSecurityItems.CurrentAccountAgingImport,
                (short)CashFlowManagementModuleSecuritySubItems.None,
                Privileges.Update));

            mainSecurity.AddChild(new SecurityDefinition(
                CollectionOrderTerminology.LineApprovalSecurityName,
                logicalModuleId, moduleId,
                (short)CashFlowManagementModuleSecurityItems.CollectionOrderLineApproval,
                (short)CashFlowManagementModuleSecuritySubItems.None,
                Privileges.Update));

            mainSecurity.AddChild(new SecurityDefinition(
                CollectionOrderTerminology.HeaderApprovalSecurityName,
                logicalModuleId, moduleId,
                (short)CashFlowManagementModuleSecurityItems.CollectionOrderHeaderApproval,
                (short)CashFlowManagementModuleSecuritySubItems.None,
                Privileges.Update));

            mainSecurity.AddChild(new SecurityDefinition(
                SLanguage.GetString("Tekrar Eden Tahsilatları Aktar"),
                logicalModuleId, moduleId,
                (short)CashFlowManagementModuleSecurityItems.FixedCollectionImport,
                (short)CashFlowManagementModuleSecuritySubItems.None,
                Privileges.Update));

            mainSecurity.AddChild(new SecurityDefinition(
                SLanguage.GetString("Alacak Yaşlandırma Tutarlarını Aktar"),
                logicalModuleId, moduleId,
                (short)CashFlowManagementModuleSecurityItems.CurrentAccountCollectionAgingImport,
                (short)CashFlowManagementModuleSecuritySubItems.None,
                Privileges.Update));

            PrivilegeInfo.SecurityDefinitions.AddDefinition(mainSecurity);
        }
    }
}
