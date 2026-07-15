using System;
using System.Collections.Generic;
using System.Data;

using CashFlowManagementModule.BoExtensions;

using Sentez.Common.SystemServices;
using Sentez.Data.Tools;

namespace CashFlowManagementModule.Services
{
    public static class PosMerchantDataService
    {
        public static bool IsPosBankAccount(LiveSession session, long bankAccountId)
        {
            if (session?._dbInfo?.Connection == null || bankAccountId <= 0)
                return false;

            int companyId = session.ActiveCompany.RecId ?? 0;
            DataTable table = UtilityFunctions.GetDataTableList(
                session._dbInfo.DBProvider,
                session._dbInfo.Connection,
                null,
                "Erp_BankAccount",
                $@"select isnull(ba.AccountSubType,0) AccountSubType
                   from Erp_BankAccount ba with (nolock)
                   inner join Erp_Bank b with (nolock) on b.RecId = ba.BankId
                   where b.CompanyId = {companyId}
                     and ba.RecId = {bankAccountId}
                     and isnull(ba.IsDeleted,0)=0
                     and isnull(b.IsDeleted,0)=0");

            if (table == null || table.Rows.Count == 0 || table.Rows[0].IsNull("AccountSubType"))
                return false;

            return Convert.ToByte(table.Rows[0]["AccountSubType"]) == BankAccountSubTypeHelper.PosAccountSubType;
        }

        public static IList<long> LoadPosBankAccountIds(LiveSession session)
        {
            var ids = new List<long>();
            if (session?._dbInfo?.Connection == null)
                return ids;

            int companyId = session.ActiveCompany.RecId ?? 0;
            DataTable table = UtilityFunctions.GetDataTableList(
                session._dbInfo.DBProvider,
                session._dbInfo.Connection,
                null,
                "Erp_BankAccount",
                $@"select ba.RecId
                   from Erp_BankAccount ba with (nolock)
                   inner join Erp_Bank b with (nolock) on b.RecId = ba.BankId
                   where b.CompanyId = {companyId}
                     and isnull(ba.AccountSubType,0) = {BankAccountSubTypeHelper.PosAccountSubType}
                     and isnull(ba.IsDeleted,0)=0
                     and isnull(b.IsDeleted,0)=0
                   order by ba.AccountCode");

            if (table == null)
                return ids;

            foreach (DataRow row in table.Rows)
            {
                if (!row.IsNull("RecId"))
                    ids.Add(Convert.ToInt64(row["RecId"]));
            }

            return ids;
        }
    }
}
