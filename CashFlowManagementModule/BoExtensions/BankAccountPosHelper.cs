using System;
using System.Data;

using CashFlowManagementModule.Services;

using Sentez.Common.Utilities;
using Sentez.Data.MetaData;
using Sentez.Data.Tools;
using Sentez.Localization;

namespace CashFlowManagementModule.BoExtensions
{
    /// <summary>
    /// Üye İş Yeri Hesabı (AccountSubType=50) ekstre alanları ve tablo sabitleri.
    /// Veri kaynağı: cari fiş tipleri 50 (tahsilat) ve 52 (iade).
    /// </summary>
    public static class BankAccountPosHelper
    {
        public const short CustomerCreditCardCollectionReceiptType = 50;
        public const short CustomerCreditCardRefundReceiptType = 52;

        public const string DeductionProfileTableName = "Erp_BankAccountPosDeductionProfile";
        public const string PeriodSummaryTableName = "Erp_BankAccountPosPeriodSummary";
        public const string PeriodDailyTableName = "Erp_BankAccountPosPeriodDaily";
        public const string PeriodSettlementTableName = "Erp_BankAccountPosPeriodSettlement";
        public const string PeriodFutureReceivableTableName = "Erp_BankAccountPosPeriodFutureReceivable";

        public const string BankAccountFkName = "FK_Erp_BankAccountPosDeductionProfile_Erp_BankAccount";

        public const string FieldMerchantNo = "UD_PosMerchantNo";
        public const string FieldStatementViewProfile = "UD_PosStatementViewProfile";

        public const string FieldProfileCardCategory = "CardCategory";
        public const string FieldProfileInstallmentCount = "InstallmentCount";
        public const string FieldProfileBlockDays = "BlockDays";

        public const byte SettlementKindCurrentMonth = 1;
        public const byte SettlementKindNextMonth = 2;

        public const byte CalculationBaseGross = 1;
        public const byte CalculationBaseNet = 2;

        public const string StatementViewProfileLookupName = "PosStatementViewProfileList";
        public const string CalculationBaseLookupName = "PosCalculationBaseList";
        public const byte StatementViewProfileStandard = 1;
        public const byte StatementViewProfileFull = 2;

        public static void EnsureStatementViewProfileLookup(LookupList lists)
        {
            if (lists == null || lists.Contains(StatementViewProfileLookupName))
                return;

            lists.AddLookupList(
                StatementViewProfileLookupName,
                "Display",
                typeof(string),
                new object[]
                {
                    SLanguage.GetString("Standart"),
                    SLanguage.GetString("Tam Ekstre")
                },
                "Value",
                typeof(byte),
                new object[] { StatementViewProfileStandard, StatementViewProfileFull });
        }

        public static void EnsureCalculationBaseLookup(LookupList lists)
        {
            if (lists == null || lists.Contains(CalculationBaseLookupName))
                return;

            lists.AddLookupList(
                CalculationBaseLookupName,
                "Display",
                typeof(string),
                new object[]
                {
                    SLanguage.GetString("Brüt"),
                    SLanguage.GetString("Net")
                },
                "Value",
                typeof(byte),
                new object[] { CalculationBaseGross, CalculationBaseNet });
        }

        public static bool IsPosAccountRow(DataRow row)
        {
            return BankAccountSubTypeHelper.IsPosAccount(row);
        }

        public static void EnsureBankAccountMetaDataFields()
        {
            if (!Schema.Tables["Erp_BankAccount"].Fields.Contains(FieldMerchantNo))
                CreatMetaDataFieldsService.CreatMetaDataFields(
                    "Erp_BankAccount",
                    FieldMerchantNo,
                    SLanguage.GetString("Üye İşyeri No"),
                    (byte)UdtType.UdtCode,
                    (byte)FieldUsage.None,
                    (byte)EditorType.TextEditor,
                    (byte)ValueInputMethod.FreeType,
                    0);

            if (!Schema.Tables["Erp_BankAccount"].Fields.Contains(FieldStatementViewProfile))
                CreatMetaDataFieldsService.CreatMetaDataFields(
                    "Erp_BankAccount",
                    FieldStatementViewProfile,
                    SLanguage.GetString("Ekstre Görünüm Profili"),
                    (byte)UdtType.UdtInt8,
                    (byte)FieldUsage.None,
                    (byte)EditorType.ComboBox,
                    (byte)ValueInputMethod.FreeType,
                    0);
        }

        public static void EnsureBankAccountDataColumns(DataSet data)
        {
            if (data == null || !data.Tables.Contains("Erp_BankAccount"))
                return;

            EnsureStringColumn(data.Tables["Erp_BankAccount"], FieldMerchantNo, SLanguage.GetString("Üye İşyeri No"));
        }

        static void EnsureStringColumn(DataTable table, string columnName, string caption)
        {
            if (table.Columns.Contains(columnName))
                return;

            table.Columns.Add(new DataColumn(columnName, typeof(string))
            {
                Caption = caption,
                DefaultValue = DBNull.Value,
                AllowDBNull = true
            });
        }
    }
}
