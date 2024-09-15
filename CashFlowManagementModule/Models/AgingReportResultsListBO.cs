using Sentez.Data.BusinessObjects;
using Sentez.Data.Query;
using Prism.Ioc;
using Sentez.Common.ModuleBase;
using Sentez.Common.Security;
using Sentez.Common.Commands;

namespace Sentez.FinanceModule.Models
{
    [BusinessObjectExplanation("Yaşlandırma Rapor Tanımı")]
    [SecurityModuleId((short)Modules.FinanceModule)]
    [SecurityItemId((short)FinanceSecurityItems.CurrentAccountGroup)]
    public class AgingReportResultsListBO : BusinessObjectBase
    {
        
        public AgingReportResultsListBO(IContainerExtension container)
            : base(container, 0, "ReportNo", string.Empty, new string[] { "Erp_AgingReportResults" })
        {
            KeyFields.Add(new WhereField("Erp_AgingReportResults", "CompanyId", _companyId, WhereCondition.Equal));

            this.ValueFiller.AddRule("Erp_AgingReportResults", "InUse", 1);

            SecurityChecker.LogicalModuleID = (short)Modules.FinanceModule;
        }
    }
}