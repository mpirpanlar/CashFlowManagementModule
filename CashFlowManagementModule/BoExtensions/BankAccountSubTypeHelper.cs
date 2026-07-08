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
                    SLanguage.GetString("Pos Hesabı")
                },
                "Value", typeof(byte),
                new object[] { (byte)1, (byte)2, (byte)3, (byte)4, PosAccountSubType });
        }
    }
}
