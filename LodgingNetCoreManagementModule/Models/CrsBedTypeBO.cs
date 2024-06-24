using Sentez.Common.Security;
using Sentez.Common.ModuleBase;
using Sentez.Data.BusinessObjects;
using Prism.Ioc;

namespace Sentez.CRSModule.Models
{
    [SecurityModuleId((short)Modules.CRSModule)]
    //[SecurityItemId((short)CRSSecurityItems.BedType)]
    public class CrsBedTypeBO : BusinessObjectBase
    {
        public CrsBedTypeBO(IContainerExtension container)
            : base(container, 0, "BedTypeCode", string.Empty, new string[] { "Meta_BedType" })
        {
            this.ValueFiller.AddRule("Meta_BedType", "InUse", 1);

            SecurityChecker.LogicalModuleID = (short)Modules.CRSModule;
        }
    }
}
