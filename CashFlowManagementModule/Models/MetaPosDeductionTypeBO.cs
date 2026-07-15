using CashFlowManagementModule.BoExtensions;

using Prism.Ioc;

using Sentez.CashFlowManagementModule;
using Sentez.Common.ModuleBase;
using Sentez.Common.Security;
using Sentez.Data.BusinessObjects;
using Sentez.Data.Query;

namespace CashFlowManagementModule.Models
{
    [BusinessObjectExplanation("Pos Kesinti Türü Tanımları")]
    [SecurityModuleId((short)Modules.ExternalModule16)]
    [SecurityItemId((short)CashFlowManagementModuleSecurityItems.PosDeductionType)]
    public class MetaPosDeductionTypeBO : BusinessObjectBase
    {
        public MetaPosDeductionTypeBO(IContainerExtension container)
            : base(container, 0, MetaPosDeductionTypeHelper.KeyField, string.Empty, new[] { MetaPosDeductionTypeHelper.TableName })
        {
            KeyFields.Add(WhereField.GetIsDeletedRule(MetaPosDeductionTypeHelper.TableName));
            ValueFiller.AddRule(MetaPosDeductionTypeHelper.TableName, "InUse", 1);
            ValueFiller.AddRule(MetaPosDeductionTypeHelper.TableName, "IsDeleted", 0);
            SecurityChecker.LogicalModuleID = (short)Modules.ExternalModule16;
        }
    }
}
