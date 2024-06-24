using Sentez.Data.BusinessObjects;
using Sentez.Common.ModuleBase;
using Sentez.Common.Security;
using Prism.Ioc;

namespace Sentez.LodgingNetCoreManagementModule.Models
{
    [BusinessObjectExplanation("Turunc Log Bilgileri")]
    [SecurityModuleId((short)Modules.ExternalModule12)]
    [SecurityItemId(0)]
    public class LogTransactionTuruncBO : BusinessObjectBase
    {
        public LogTransactionTuruncBO(IContainerExtension container)
            : base(container, 0, "", string.Empty, new string[] { "Log_TransactionTurunc" })
        {
            SecurityChecker.LogicalModuleID = (short)Modules.ExternalModule12;
        }
    }
}
