using System;
using System.Data;
using System.Data.Common;

using Sentez.Common.SystemServices;
using Sentez.Data.BusinessObjects;
using Sentez.Data.Tools;

namespace CashFlowManagementModule.Services
{
    public readonly struct CashFlowDbContext
    {
        public ProviderType Provider { get; }
        public DbConnection Connection { get; }
        public DbTransaction Transaction { get; }
        public bool KeepConnectionOpen { get; }

        public CashFlowDbContext(ProviderType provider, DbConnection connection, DbTransaction transaction, bool keepConnectionOpen)
        {
            Provider = provider;
            Connection = connection;
            Transaction = transaction;
            KeepConnectionOpen = keepConnectionOpen;
        }

        public bool IsValid => Connection != null;

        public static CashFlowDbContext FromBusinessObject(BusinessObjectBase businessObject)
        {
            if (businessObject == null)
                return default;

            return new CashFlowDbContext(
                businessObject.Provider,
                businessObject.Connection,
                businessObject.Transaction,
                keepConnectionOpen: true);
        }

        public static CashFlowDbContext FromSession(LiveSession session, bool keepConnectionOpen = true)
        {
            if (session?._dbInfo?.Connection == null)
                return default;

            return new CashFlowDbContext(
                session._dbInfo.DBProvider,
                session._dbInfo.Connection,
                null,
                keepConnectionOpen);
        }

        public static CashFlowDbContext From(
            DbConnection connection,
            DbTransaction transaction,
            ProviderType provider,
            bool keepConnectionOpen = true)
        {
            return new CashFlowDbContext(provider, connection, transaction, keepConnectionOpen);
        }
    }

    public sealed class CashFlowDbScope : IDisposable
    {
        readonly CashFlowDbContext _context;

        public CashFlowDbScope(CashFlowDbContext context)
        {
            _context = context;
        }

        public void Dispose()
        {
            CashFlowDbAccess.RestoreIfNeeded(_context);
        }
    }

    public static class CashFlowDbAccess
    {
        public static CashFlowDbScope BeginScope(CashFlowDbContext context)
        {
            return new CashFlowDbScope(context);
        }

        public static object ExecuteScalar(CashFlowDbContext context, string sql)
        {
            if (!context.IsValid) return null;

            try
            {
                return UtilityFunctions.SqlCustomScalarQuery(context.Connection, context.Transaction, sql);
            }
            finally
            {
                RestoreIfNeeded(context);
            }
        }

        public static int ExecuteNonQuery(CashFlowDbContext context, string sql)
        {
            if (!context.IsValid) return -1;

            try
            {
                return UtilityFunctions.SqlCustomNonQuery(context.Connection, context.Transaction, sql);
            }
            finally
            {
                RestoreIfNeeded(context);
            }
        }

        public static DataTable GetDataTable(CashFlowDbContext context, string tableName, string sql)
        {
            if (!context.IsValid) return null;

            try
            {
                return UtilityFunctions.GetDataTableList(
                    context.Provider,
                    context.Connection,
                    context.Transaction,
                    tableName,
                    sql);
            }
            finally
            {
                RestoreIfNeeded(context);
            }
        }

        public static DataRow ReadSingleRow(CashFlowDbContext context, string sql, string tableName = "Result")
        {
            if (!context.IsValid) return null;

            try
            {
                using DbDataReader reader = UtilityFunctions.SqlCustomQueryDTR(
                    context.Connection,
                    context.Transaction,
                    CommandBehavior.Default,
                    sql);
                if (reader == null || !reader.Read())
                    return null;

                var table = new DataTable(tableName);
                for (int i = 0; i < reader.FieldCount; i++)
                    table.Columns.Add(reader.GetName(i), reader.GetFieldType(i));

                object[] values = new object[reader.FieldCount];
                reader.GetValues(values);
                table.Rows.Add(values);
                return table.Rows[0];
            }
            finally
            {
                RestoreIfNeeded(context);
            }
        }

        public static void RestoreIfNeeded(CashFlowDbContext context)
        {
            if (!context.IsValid || !context.KeepConnectionOpen || context.Connection == null)
                return;

            if (context.Connection.State != ConnectionState.Open)
                context.Connection.Open();
        }
    }
}
