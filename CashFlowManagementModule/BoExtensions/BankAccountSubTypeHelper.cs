using System;
using System.Data;

using Sentez.Common.Utilities;
using Sentez.Localization;

namespace CashFlowManagementModule.BoExtensions
{
    public static class BankAccountSubTypeHelper
    {
        public const string LookupListName = "BankAccountSubTypeList";
        public const byte PosAccountSubType = 50;

        public static void EnsureLookupList(LookupList lists)
        {
            if (lists == null) return;
            lists.AddLookupList(LookupListName, "Display", typeof(string),
                new object[]
                {
                    SLanguage.GetString("Ticari Hesap"),
                    SLanguage.GetString("Kredi Hesabı"),
                    SLanguage.GetString("Kredi Kartı Hesabı"),
                    SLanguage.GetString("Vadeli Hesap"),
                    SLanguage.GetString("Üye İş Yeri Hesabı")
                },
                "Value", typeof(byte),
                new object[] { (byte)1, (byte)2, (byte)3, (byte)4, PosAccountSubType });
        }

        static bool IsUsableDataRow(DataRow row)
        {
            return row != null
                && row.Table != null
                && row.RowState != DataRowState.Deleted
                && row.RowState != DataRowState.Detached;
        }

        public static bool IsPosAccount(DataRow row)
        {
            if (!IsUsableDataRow(row) || row.IsNull("AccountSubType")) return false;
            return Convert.ToByte(row["AccountSubType"]) == PosAccountSubType;
        }

        public static bool ShouldShowCreditCardDetailTab(DataRow row)
        {
            if (!IsUsableDataRow(row) || IsPosAccount(row)) return false;
            return !row.IsNull("ForCreditCard") && Convert.ToBoolean(row["ForCreditCard"]);
        }

        public static bool ShouldShowPosDetailTab(DataRow row)
        {
            return IsPosAccount(row);
        }
    }
}
