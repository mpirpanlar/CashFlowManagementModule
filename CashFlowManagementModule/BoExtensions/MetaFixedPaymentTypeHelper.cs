using Sentez.Common.Utilities;
using Sentez.Data.MetaData;
using Sentez.Data.Query;

namespace CashFlowManagementModule.BoExtensions
{
    public static class MetaFixedPaymentTypeHelper
    {
        public const string TableName = "Meta_FixedPaymentType";
        public const string KeyField = "FixedPaymentTypeCode";
        public const string ListName = "Meta_FixedPaymentTypeFixedPaymentTypeCodeList";
        public const string LookupListName = "FixedPaymentTypeList";

        static LookupListParam CreateLookupListParam()
        {
            return new LookupListParam(
                LookupListName,
                TableName,
                "FixedPaymentTypeName",
                "RecId",
                new[]
                {
                    WhereField.GetIsDeletedRule(TableName),
                    new WhereField(TableName, "InUse", 1, WhereCondition.Equal)
                },
                EmptyLookupType.First);
        }

        public static void EnsureLookupList(LookupList lists)
        {
            if (lists == null || lists.Contains(LookupListName)) return;
            lists.AddLookupList(CreateLookupListParam());
        }

        public static void RefreshLookupList(LookupList lists)
        {
            if (lists == null) return;

            lists.AddLookupList(CreateLookupListParam());
            _ = lists[LookupListName];
        }
    }
}
