using CashFlowManagementModule.BoExtensions;

using Prism.Ioc;

using Sentez.CashFlowManagementModule;
using Sentez.Common.ModuleBase;
using Sentez.Common.Security;
using Sentez.Data.BusinessObjects;
using Sentez.Data.Query;

namespace CashFlowManagementModule.Models
{
    [BusinessObjectExplanation("Tekrar Eden Ödeme Tipi Tanımları")]
    [SecurityModuleId((short)Modules.ExternalModule16)]
    [SecurityItemId((short)CashFlowManagementModuleSecurityItems.FixedPaymentType)]
    public class MetaFixedPaymentTypeBO : BusinessObjectBase
    {
        public MetaFixedPaymentTypeBO(IContainerExtension container)
            : base(container, 0, MetaFixedPaymentTypeHelper.KeyField, string.Empty, new[] { MetaFixedPaymentTypeHelper.TableName })
        {
            KeyFields.Add(WhereField.GetIsDeletedRule(MetaFixedPaymentTypeHelper.TableName));
            ValueFiller.AddRule(MetaFixedPaymentTypeHelper.TableName, "InUse", 1);
            ValueFiller.AddRule(MetaFixedPaymentTypeHelper.TableName, "IsDeleted", 0);
            SecurityChecker.LogicalModuleID = (short)Modules.ExternalModule16;
        }
    }
}
