using Sentez.Common.Utilities;
using Sentez.Data.MetaData;
using Sentez.Data.Query;

namespace CashFlowManagementModule.BoExtensions
{
    public static class MetaPosDeductionTypeHelper
    {
        public const string TableName = "Meta_PosDeductionType";
        public const string KeyField = "PosDeductionTypeCode";
        public const string ListName = "Meta_PosDeductionTypePosDeductionTypeCodeList";
        public const string LookupListName = "PosDeductionTypeList";

        public const string CodeMerchantFee = "UYEIS";
        public const string CodeRewardExpense = "ODULG";
        public const string CodeServiceCommission = "SERVK";

        static LookupListParam CreateLookupListParam()
        {
            return new LookupListParam(
                LookupListName,
                TableName,
                "PosDeductionTypeName",
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
            if (lists == null || HasLocalLookupList(lists)) return;
            lists.AddLookupList(CreateLookupListParam());
        }

        static bool HasLocalLookupList(LookupList lists)
        {
            return lists.Parameters != null && lists.Parameters.Contains(LookupListName);
        }

        public static void RefreshLookupList(LookupList lists)
        {
            if (lists == null) return;
            lists.AddLookupList(CreateLookupListParam());
            _ = lists[LookupListName];
        }
    }
}
