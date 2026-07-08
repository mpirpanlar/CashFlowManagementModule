using System;

namespace CashFlowManagementModule.Services
{
    public sealed class CollectionOrderAgingImportContext
    {
        public DateTime ReportDate { get; set; }
        public string StartCurrentAccountCode { get; set; }
        public string EndCurrentAccountCode { get; set; }
        public long DefaultBankAccountId { get; set; }
        public string DefaultBankAccountCode { get; set; }
        public bool ImportDirectlyToReceipt { get; set; }
        public Action RefreshDefaultBankAccount { get; set; }
    }
}
