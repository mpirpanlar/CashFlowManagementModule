using Prism.Ioc;

using Sentez.Common.Commands;
using Sentez.Common.Report;
using Sentez.Data.BusinessObjects;
using Sentez.Data.MetaData;
using Sentez.Localization;

using System;
using System.ComponentModel;
using System.Data;

namespace LodgingManagementModule.BoExtensions
{
    public class LogTransactionTuruncExtension : BoExtensionBase
    {
        public LogTransactionTuruncExtension(BusinessObjectBase bo)
            : base(bo)
        {
        }

        protected override void OnBeforePost(object sender, CancelEventArgs e)
        {
            base.OnBeforePost(sender, e);
            if (!e.Cancel)
            {
                IBusinessObject logTransactionTuruncBo = Container.Resolve<IBusinessObject>("LogTransactionTuruncBO");
                try
                {
                    if (logTransactionTuruncBo != null)
                    {
                        string mcAdress = string.Empty;
                        var macAdress = SysMng.GetPhysicalAddresses();
                        if (macAdress != null && macAdress.Length > 0)
                            mcAdress = macAdress[0].ToString();

                        logTransactionTuruncBo.Init(new BoParam());
                        foreach (System.Data.DataTable dt in BusinessObject.Data.Tables)
                        {
                            if (!Schema.Tables.Contains(dt.TableName))
                                continue;
                            foreach (DataRow row in dt.Rows)
                            {
                                if (!(row.RowState == DataRowState.Deleted || row.RowState == DataRowState.Detached))
                                {
                                    foreach (System.Data.DataColumn dataColumn in row.Table.Columns)
                                    {
                                        if (!Schema.Tables[row.Table.TableName].Fields.Contains(dataColumn.ColumnName))
                                            continue;
                                        if (row.HasVersion(DataRowVersion.Original))
                                            if (!row[dataColumn, DataRowVersion.Current].Equals(row[dataColumn, DataRowVersion.Original]))
                                            {
                                                /*
                                                    [CompanyCode] [dbo].[UdtCode] NULL,
                                                    [UserCode] [dbo].[UdtCode] NULL,
                                                    [SysUserCode] [dbo].[UdtCode] NULL,
                                                    [SysAdress] [dbo].[UdtCode] NULL,
                                                    [OperationType] [dbo].[UdtInt8] NULL,
                                                    [OperationDate] [dbo].[UdtDateTime] NULL,
                                                    [OperationTime] [dbo].[UdtDateTime] NULL,
                                                    [Program] [dbo].[UdtInt16] NULL,
                                                    [LogicalModule] [dbo].[UdtInt16] NULL,
                                                    [Module] [dbo].[UdtInt16] NULL,
                                                    [CommandName] [dbo].[UdtName] NULL,
                                                    [CommandId] [dbo].[UdtInt16] NULL,
                                                    [ItemId] [dbo].[UdtInt32] NULL,
                                                    [SubItemId] [dbo].[UdtInt32] NULL,
                                                    [ItemCode] [dbo].[UdtCode] NULL,
                                                    [Explanation] [dbo].[UdtExpLong] NULL,
                                                    [BOName] [dbo].[UdtName] NULL,
                                                    [BORecId] [dbo].[UdtInt64] NULL,
                                                    [GpsXCoordinate] [dbo].[UdtCoordinate] NULL,
                                                    [GpsYCoordinate] [dbo].[UdtCoordinate] NULL,
                                                    [IsReaded] [dbo].[UdtBool] NULL,
                                                    [LogData] [dbo].[UdtBinary] NULL,
                                                    [LogDataText] [dbo].[UdtTextMax] NULL,
                                                */
                                                logTransactionTuruncBo.NewRecord();
                                                logTransactionTuruncBo.CurrentRow["CompanyCode"] = BusinessObject.ActiveSession.ActiveCompany.CompanyCode;
                                                logTransactionTuruncBo.CurrentRow["UserCode"] = BusinessObject.ActiveSession.ActiveUser.UserCode;
                                                logTransactionTuruncBo.CurrentRow["SysAdress"] = mcAdress;
                                                logTransactionTuruncBo.CurrentRow["OperationType"] = (byte)Sentez.Common.OperationType.Update;
                                                logTransactionTuruncBo.CurrentRow["OperationDate"] = DateTime.Now.Date;
                                                logTransactionTuruncBo.CurrentRow["OperationTime"] = new DateTime(1899, 12, 30, DateTime.Now.TimeOfDay.Hours, DateTime.Now.TimeOfDay.Minutes, DateTime.Now.TimeOfDay.Seconds);
                                                logTransactionTuruncBo.CurrentRow["BOName"] = BusinessObject.GetType().Name;
                                                logTransactionTuruncBo.CurrentRow["BORecId"] = BusinessObject.CurrentRow.Row["RecId"];
                                                logTransactionTuruncBo.CurrentRow["Module"] = BusinessObject.ModuleId;
                                                logTransactionTuruncBo.CurrentRow["LogicalModule"] = BusinessObject.ModuleId;
                                                logTransactionTuruncBo.CurrentRow["Explanation"] = Schema.Tables[row.Table.TableName].Fields[dataColumn.ColumnName].Caption;
                                                if (BusinessObject.CurrentRow.Row.Table.Columns.Contains(BusinessObject.KeyColumn))
                                                    logTransactionTuruncBo.CurrentRow["ItemCode"] = BusinessObject.CurrentRow[BusinessObject.KeyColumn];
                                                logTransactionTuruncBo.CurrentRow["LogDataText"] = $"{row[dataColumn, DataRowVersion.Original]} >>> {row[dataColumn, DataRowVersion.Current]}";
                                                PostResult postResult = logTransactionTuruncBo.PostData(BusinessObject.Transaction);
                                            }
                                    }
                                }
                                else
                                {
                                    logTransactionTuruncBo.NewRecord();
                                    logTransactionTuruncBo.CurrentRow["CompanyCode"] = BusinessObject.ActiveSession.ActiveCompany.CompanyCode;
                                    logTransactionTuruncBo.CurrentRow["UserCode"] = BusinessObject.ActiveSession.ActiveUser.UserCode;
                                    logTransactionTuruncBo.CurrentRow["OperationType"] = (byte)Sentez.Common.OperationType.Delete;
                                    logTransactionTuruncBo.CurrentRow["OperationDate"] = DateTime.Now.Date;
                                    logTransactionTuruncBo.CurrentRow["OperationTime"] = new DateTime(1899, 12, 30, DateTime.Now.TimeOfDay.Hours, DateTime.Now.TimeOfDay.Minutes, DateTime.Now.TimeOfDay.Seconds);
                                    logTransactionTuruncBo.CurrentRow["BOName"] = BusinessObject.GetType().Name;
                                    logTransactionTuruncBo.CurrentRow["BORecId"] = BusinessObject.CurrentRow.Row["RecId"];
                                    logTransactionTuruncBo.CurrentRow["Module"] = BusinessObject.ModuleId;
                                    logTransactionTuruncBo.CurrentRow["LogicalModule"] = BusinessObject.ModuleId;
                                    logTransactionTuruncBo.CurrentRow["Explanation"] = $"{SLanguage.GetString("Silindi")} - {row.Table.TableName}";
                                    if (BusinessObject.CurrentRow.Row.Table.Columns.Contains(BusinessObject.KeyColumn))
                                        logTransactionTuruncBo.CurrentRow["ItemCode"] = BusinessObject.CurrentRow[BusinessObject.KeyColumn];
                                    string sVal = "";
                                    foreach (System.Data.DataColumn dataColumn in row.Table.Columns)
                                    {
                                        if (!Schema.Tables[row.Table.TableName].Fields.Contains(dataColumn.ColumnName))
                                            continue;
                                        if (row[dataColumn, DataRowVersion.Original] == DBNull.Value)
                                            continue;

                                        if (row.HasVersion(DataRowVersion.Original))
                                        {
                                            if (string.IsNullOrEmpty(sVal))
                                                sVal = $"{dataColumn.ColumnName} >>> {row[dataColumn, DataRowVersion.Original]}";
                                            else
                                                sVal += $"\n{dataColumn.ColumnName} >>> {row[dataColumn, DataRowVersion.Original]}";
                                        }
                                    }
                                    logTransactionTuruncBo.CurrentRow["LogDataText"] = sVal;
                                    PostResult postResult = logTransactionTuruncBo.PostData(BusinessObject.Transaction);
                                }
                            }
                        }

                    }
                }
                finally
                {
                    logTransactionTuruncBo?.Dispose();
                }
            }
        }
    }
}
