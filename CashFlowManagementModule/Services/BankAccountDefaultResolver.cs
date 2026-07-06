using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;

using Sentez.Common.SystemServices;
using Sentez.Data.Tools;

namespace CashFlowManagementModule.Services
{
    internal static class BankAccountDefaultResolver
    {
        public static long ResolveBankAccountId(LiveSession session, long bankAccountId, string bankAccountCode)
        {
            if (session?.ActiveCompany?.RecId == null)
                return 0;

            if (bankAccountId > 0 && TryGetBankAccountCode(session, bankAccountId, out _))
                return bankAccountId;

            foreach (string candidate in GetLookupCandidates(bankAccountCode))
            {
                long resolvedId = QueryBankAccountIdByCode(session, candidate);
                if (resolvedId > 0)
                    return resolvedId;
            }

            return 0;
        }

        public static DataRow LoadBankAccountRow(LiveSession session, long bankAccountId)
        {
            if (session?.ActiveCompany?.RecId == null || bankAccountId <= 0)
                return null;

            int companyId = session.ActiveCompany.RecId.Value;
            DataTable table = UtilityFunctions.GetDataTableList(
                session._dbInfo.DBProvider,
                session._dbInfo.Connection,
                null,
                "Erp_BankAccount",
                $@"select ba.RecId,
                          isnull(ba.AccountCode,'') AccountCode,
                          isnull(ba.AccountName,'') AccountName
                   from Erp_BankAccount ba with (nolock)
                   inner join Erp_Bank b with (nolock) on b.RecId = ba.BankId
                   where isnull(ba.IsDeleted,0)=0
                     and isnull(b.IsDeleted,0)=0
                     and b.CompanyId = {companyId}
                     and ba.RecId = {bankAccountId}");

            if (table == null || table.Rows.Count == 0)
                return null;

            return table.Rows[0];
        }

        public static string NormalizeDisplayCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return string.Empty;

            return code.Trim();
        }

        public static bool AreEquivalentCodes(string left, string right)
        {
            foreach (string leftCandidate in GetLookupCandidates(left))
            {
                foreach (string rightCandidate in GetLookupCandidates(right))
                {
                    if (string.Equals(leftCandidate, rightCandidate, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            return false;
        }

        public static IEnumerable<string> GetLookupCandidates(string code)
        {
            var candidates = new List<string>();
            if (string.IsNullOrWhiteSpace(code))
                return candidates;

            AddCandidate(candidates, code.Trim());
            AddCandidate(candidates, code.Trim().Replace(" ", string.Empty));

            string withoutSeparators = code.Trim()
                .Replace(" ", string.Empty)
                .Replace(".", string.Empty)
                .Replace(",", string.Empty);
            AddCandidate(candidates, withoutSeparators);

            if (decimal.TryParse(code.Trim(), NumberStyles.Number, CultureInfo.CurrentCulture, out decimal numericCode))
                AddCandidate(candidates, numericCode.ToString("0", CultureInfo.InvariantCulture));

            if (decimal.TryParse(withoutSeparators, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal plainNumericCode))
                AddCandidate(candidates, plainNumericCode.ToString("0", CultureInfo.InvariantCulture));

            return candidates;
        }

        static void AddCandidate(ICollection<string> candidates, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;

            foreach (string existing in candidates)
            {
                if (string.Equals(existing, value, StringComparison.OrdinalIgnoreCase))
                    return;
            }

            candidates.Add(value);
        }

        static bool TryGetBankAccountCode(LiveSession session, long bankAccountId, out string accountCode)
        {
            accountCode = null;
            if (session?.ActiveCompany?.RecId == null || bankAccountId <= 0)
                return false;

            int companyId = session.ActiveCompany.RecId.Value;
            DataTable table = UtilityFunctions.GetDataTableList(
                session._dbInfo.DBProvider,
                session._dbInfo.Connection,
                null,
                "Erp_BankAccount",
                $@"select isnull(ba.AccountCode,'') AccountCode
                   from Erp_BankAccount ba with (nolock)
                   inner join Erp_Bank b with (nolock) on b.RecId = ba.BankId
                   where isnull(ba.IsDeleted,0)=0
                     and isnull(b.IsDeleted,0)=0
                     and b.CompanyId = {companyId}
                     and ba.RecId = {bankAccountId}");

            if (table == null || table.Rows.Count == 0 || table.Rows[0].IsNull("AccountCode"))
                return false;

            accountCode = Convert.ToString(table.Rows[0]["AccountCode"]);
            return !string.IsNullOrWhiteSpace(accountCode);
        }

        static long QueryBankAccountIdByCode(LiveSession session, string accountCode)
        {
            if (string.IsNullOrWhiteSpace(accountCode) || session?.ActiveCompany?.RecId == null)
                return 0;

            int companyId = session.ActiveCompany.RecId.Value;
            string escapedCode = accountCode.Replace("'", "''");
            DataTable table = UtilityFunctions.GetDataTableList(
                session._dbInfo.DBProvider,
                session._dbInfo.Connection,
                null,
                "Erp_BankAccount",
                $@"select ba.RecId
                   from Erp_BankAccount ba with (nolock)
                   inner join Erp_Bank b with (nolock) on b.RecId = ba.BankId
                   where isnull(ba.IsDeleted,0)=0
                     and isnull(b.IsDeleted,0)=0
                     and b.CompanyId = {companyId}
                     and ba.AccountCode = '{escapedCode}'");

            if (table == null || table.Rows.Count == 0 || table.Rows[0].IsNull("RecId"))
                return 0;

            return Convert.ToInt64(table.Rows[0]["RecId"]);
        }
    }
}
