using CashFlowManagementModule.Services;

using Sentez.Data.BusinessObjects;
using Sentez.Data.MetaData;
using Sentez.Data.Tools;
using Sentez.Localization;

namespace CashFlowManagementModule.BoExtensions
{
    public static class CurrentAccountFixedPaymentHelper
    {
        public const string ScheduleTableName = "Erp_CurrentAccountFixedPaymentSchedule";
        public const string CurrentAccountFkName = "FK_Erp_CurrentAccountFixedPaymentSchedule_Erp_CurrentAccount";
        public const string FixedPaymentTypeFkName = "FK_Erp_CurrentAccountFixedPaymentSchedule_Meta_FixedPaymentType";
        public const string FieldIsFixedPayment = "UD_IsFixedPayment";
        public const string FieldFixedPaymentTypeId = "FixedPaymentTypeId";

        public static void EnsureCurrentAccountMetaDataFields()
        {
            if (!Schema.Tables["Erp_CurrentAccount"].Fields.Contains(FieldIsFixedPayment))
                CreatMetaDataFieldsService.CreatMetaDataFields(
                    "Erp_CurrentAccount",
                    FieldIsFixedPayment,
                    SLanguage.GetString("Tekrar Eden Ödeme Takibi"),
                    (byte)UdtType.UdtBool,
                    (byte)FieldUsage.Bool,
                    (byte)EditorType.CheckBox,
                    (byte)ValueInputMethod.FreeType,
                    0);
        }

        public static void EnsureFixedPaymentTypeLookups(BusinessObjectBase bo)
        {
            if (bo?.Lookups == null) return;

            bo.Lookups.AddLookUp(
                ScheduleTableName,
                FieldFixedPaymentTypeId,
                true,
                MetaFixedPaymentTypeHelper.TableName,
                MetaFixedPaymentTypeHelper.KeyField,
                MetaFixedPaymentTypeHelper.KeyField,
                "FixedPaymentTypeName",
                "FixedPaymentTypeName");
        }
    }
}
