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

            PrivilegeInfo.SecurityDefinitions.AddDefinition(mainSecurity);
        }
    }
}
