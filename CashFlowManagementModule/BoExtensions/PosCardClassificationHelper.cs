using CashFlowManagementModule.Services;

using Sentez.Common.Utilities;
using Sentez.Data.MetaData;
using Sentez.Data.Tools;
using Sentez.Localization;

using System;
using System.Data;

namespace CashFlowManagementModule.BoExtensions
{
    /// <summary>
    /// Üye iş yeri / müşteri KK tahsilat satırlarında kart kaynağı ve kart kategorisi (UdtInt16 UD_ alanları).
    /// </summary>
    public static class PosCardClassificationHelper
    {
        public const string FieldCardSource = "UD_PosCardSource";
        public const string FieldCardCategory = "UD_PosCardCategory";

        public const string CardSourceLookupName = "PosCardSourceList";
        public const string CardCategoryLookupName = "PosCardCategoryList";

        public const short CardSourceMerchantPos = 1;
        public const short CardSourceOtherBank = 2;

        public const short CardCategoryOwnBankCredit = 1;
        public const short CardCategoryOwnBankDebit = 2;
        public const short CardCategoryDomesticCredit = 3;
        public const short CardCategoryDomesticDebit = 4;
        public const short CardCategoryForeign = 5;
        public const short CardCategoryAmexDomestic = 6;
        public const short CardCategoryAmexForeign = 7;

        public static void EnsureCardSourceLookup(LookupList lists)
        {
            if (lists == null || lists.Contains(CardSourceLookupName))
                return;

            lists.AddLookupList(
                CardSourceLookupName,
                "Display",
                typeof(string),
                new object[]
                {
                    SLanguage.GetString("Üye İş Yeri / Kendi Banka POS"),
                    SLanguage.GetString("Başka Banka / Kart")
                },
                "Value",
                typeof(short),
                new object[] { CardSourceMerchantPos, CardSourceOtherBank });
        }

        public static void EnsureCardCategoryLookup(LookupList lists)
        {
            if (lists == null || lists.Contains(CardCategoryLookupName))
                return;

            lists.AddLookupList(
                CardCategoryLookupName,
                "Display",
                typeof(string),
                new object[]
                {
                    SLanguage.GetString("Bankamız Paraf / Kredi Kartı"),
                    SLanguage.GetString("Bankamız Debit / Banka Kartı"),
                    SLanguage.GetString("Yurt İçi Kredi Kartı"),
                    SLanguage.GetString("Yurt İçi Debit / Banka Kartı"),
                    SLanguage.GetString("Yurt Dışı Kart"),
                    SLanguage.GetString("AMEX Yurt İçi"),
                    SLanguage.GetString("AMEX Yurt Dışı")
                },
                "Value",
                typeof(short),
                new object[]
                {
                    CardCategoryOwnBankCredit,
                    CardCategoryOwnBankDebit,
                    CardCategoryDomesticCredit,
                    CardCategoryDomesticDebit,
                    CardCategoryForeign,
                    CardCategoryAmexDomestic,
                    CardCategoryAmexForeign
                });
        }

        public static void EnsureLookups(LookupList lists)
        {
            EnsureCardSourceLookup(lists);
            EnsureCardCategoryLookup(lists);
        }

        public static void EnsureReceiptItemMetaDataFields(string tableName)
        {
            if (string.IsNullOrWhiteSpace(tableName) || Schema.Tables[tableName] == null)
                return;

            if (!Schema.Tables[tableName].Fields.Contains(FieldCardSource))
                CreatMetaDataFieldsService.CreatMetaDataFields(
                    tableName,
                    FieldCardSource,
                    SLanguage.GetString("Kart Kaynağı"),
                    (byte)UdtType.UdtInt16,
                    (byte)FieldUsage.None,
                    (byte)EditorType.ComboBox,
                    (byte)ValueInputMethod.FreeType,
                    0);

            if (!Schema.Tables[tableName].Fields.Contains(FieldCardCategory))
                CreatMetaDataFieldsService.CreatMetaDataFields(
                    tableName,
                    FieldCardCategory,
                    SLanguage.GetString("Kart Kategorisi"),
                    (byte)UdtType.UdtInt16,
                    (byte)FieldUsage.None,
                    (byte)EditorType.ComboBox,
                    (byte)ValueInputMethod.FreeType,
                    0);
        }

        public static void EnsureBankReceiptItemMetaDataFields()
        {
            EnsureReceiptItemMetaDataFields("Erp_BankReceiptItem");
        }

        public static void EnsureCurrentAccountReceiptItemMetaDataFields()
        {
            EnsureReceiptItemMetaDataFields("Erp_CurrentAccountReceiptItem");
        }

        public static short? TryGetShort(DataRow row, string columnName)
        {
            if (row == null
                || row.Table == null
                || row.RowState == DataRowState.Deleted
                || row.RowState == DataRowState.Detached
                || !row.Table.Columns.Contains(columnName)
                || row.IsNull(columnName))
                return null;

            short value = Convert.ToInt16(row[columnName]);
            return value > 0 ? value : null;
        }

        public static void CopyCardClassification(DataRow source, DataRow target)
        {
            if (source == null || target == null || target.Table == null)
                return;

            CopyField(source, target, FieldCardSource);
            CopyField(source, target, FieldCardCategory);
        }

        static void CopyField(DataRow source, DataRow target, string columnName)
        {
            if (!source.Table.Columns.Contains(columnName) || !target.Table.Columns.Contains(columnName))
                return;

            if (source.IsNull(columnName))
            {
                target[columnName] = DBNull.Value;
                return;
            }

            target[columnName] = source[columnName];
        }
    }
}
