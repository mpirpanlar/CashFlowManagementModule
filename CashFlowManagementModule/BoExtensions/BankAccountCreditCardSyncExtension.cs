using CashFlowManagementModule.Services;

using Sentez.Common.SystemServices;
using Sentez.Data.BusinessObjects;

using System;
using System.Data;

namespace CashFlowManagementModule.BoExtensions
{
    public class BankAccountCreditCardSyncExtension : BoExtensionBase
    {
        public BankAccountCreditCardSyncExtension(BusinessObjectBase bo)
            : base(bo)
        {
        }

        protected override void OnAfterGet(object sender, EventArgs e)
        {
            base.OnAfterGet(sender, e);
            if (BusinessObject?.Data == null)
                return;

            BankAccountCreditCardHelper.EnsureBankAccountMetaDataFields();
            CreditCardPaymentDueDaysSyncService.EnsureVirtualColumns(BusinessObject.Data);
            RecalculatePaymentDueDays();
            RefreshPeriodPaymentSummary();
        }

        public void RefreshPeriodPaymentSummary()
        {
            if (BusinessObject?.Data == null || BusinessObject.CurrentRow?.Row == null)
                return;

            var bankAccountRow = BusinessObject.CurrentRow.Row;
            if (bankAccountRow.IsNull("ForCreditCard") || !Convert.ToBoolean(bankAccountRow["ForCreditCard"]))
                return;

            if (bankAccountRow.IsNull("RecId"))
                return;

            if (BusinessObject.ActiveSession is not LiveSession session)
                return;

            long bankAccountId = Convert.ToInt64(bankAccountRow["RecId"]);
            CreditCardPeriodPaymentSummaryService.RefreshSummary(session, BusinessObject.Data, bankAccountId);
            ApplyCreditCardPeriodGridFilter();
        }

        static void ApplyCreditCardPeriodGridFilter(DataSet data)
        {
            if (data?.Tables == null || !data.Tables.Contains(BankAccountCreditCardHelper.PeriodTableName))
                return;

            data.Tables[BankAccountCreditCardHelper.PeriodTableName].DefaultView.RowFilter =
                "IsDeleted = 0 OR IsDeleted IS NULL";
        }

        void ApplyCreditCardPeriodGridFilter()
        {
            ApplyCreditCardPeriodGridFilter(BusinessObject.Data);
        }

        protected override void OnColumnChanging(object sender, DataColumnChangeEventArgs e)
        {
            base.OnColumnChanging(sender, e);
            if (_suppressEvents || e.Column == null || e.Row == null)
                return;

            string validationError = null;
            var tableName = e.Row.Table?.TableName;
            if (tableName == "Erp_BankAccount")
                validationError = CreditCardPaymentDueDaysSyncService.ValidateHeaderColumnChange(e.Row, e.Column.ColumnName, e.ProposedValue);
            else if (tableName == BankAccountCreditCardHelper.PeriodTableName)
                validationError = CreditCardPaymentDueDaysSyncService.ValidatePeriodColumnChange(e.Row, e.Column.ColumnName, e.ProposedValue);

            if (!string.IsNullOrEmpty(validationError))
                throw new DataException(validationError);
        }

        protected override void OnColumnChanged(object sender, DataColumnChangeEventArgs e)
        {
            base.OnColumnChanged(sender, e);
            if (_suppressEvents || CreditCardPaymentDueDaysSyncService.IsSyncing || e.Column == null || e.Row == null)
                return;

            var tableName = e.Row.Table?.TableName;
            if (tableName == "Erp_BankAccount")
            {
                if (!IsHeaderPaymentDueSyncColumn(e.Column.ColumnName))
                    return;

                ExecuteSync(() => CreditCardPaymentDueDaysSyncService.SyncHeaderOnColumnChange(e.Row, e.Column.ColumnName));
                return;
            }

            if (tableName == BankAccountCreditCardHelper.PeriodTableName)
            {
                if (e.Column.ColumnName == BankAccountCreditCardHelper.FieldPeriodTotalCreditLimit)
                {
                    int amountDec = GetAmountDec();
                    ExecuteSync(() =>
                    {
                        CreditCardPeriodPaymentSummaryService.RecalculatePeriodRemainingLimitRow(e.Row, amountDec);
                        CreditCardPeriodPaymentSummaryService.ApplyHeaderLimitSummaryFromCurrentPeriod(
                            BusinessObject.Data,
                            BusinessObject.Data.Tables[BankAccountCreditCardHelper.PeriodTableName],
                            CreditCardPeriodPaymentSummaryService.LoadPeriodsFromTable(
                                BusinessObject.Data.Tables[BankAccountCreditCardHelper.PeriodTableName]),
                            amountDec);
                    });
                    return;
                }

                if (!IsPeriodPaymentDueSyncColumn(e.Column.ColumnName))
                    return;

                ExecuteSync(() => CreditCardPaymentDueDaysSyncService.SyncPeriodOnColumnChange(e.Row, e.Column.ColumnName));
            }
        }

        int GetAmountDec()
        {
            if (BusinessObject?.ActiveSession is not LiveSession session)
                return 2;

            return session.ParamService?.GetParameterClass<Sentez.Core.ParameterClasses.GeneralParameters>()?.AmountDec ?? 2;
        }

        public void RecalculatePaymentDueDays()
        {
            if (BusinessObject?.Data == null)
                return;

            CreditCardPaymentDueDaysSyncService.EnsureVirtualColumns(BusinessObject.Data);

            try
            {
                _suppressEvents = true;

                if (BusinessObject.CurrentRow?.Row != null)
                    CreditCardPaymentDueDaysSyncService.RecalculateHeaderDays(BusinessObject.CurrentRow.Row);

                if (BusinessObject.Data.Tables.Contains(BankAccountCreditCardHelper.PeriodTableName))
                    CreditCardPaymentDueDaysSyncService.RecalculateAllPeriodRows(
                        BusinessObject.Data.Tables[BankAccountCreditCardHelper.PeriodTableName]);
            }
            finally
            {
                _suppressEvents = false;
            }
        }

        void ExecuteSync(Action syncAction)
        {
            if (syncAction == null)
                return;

            try
            {
                _suppressEvents = true;
                syncAction();
            }
            finally
            {
                _suppressEvents = false;
            }
        }

        static bool IsHeaderPaymentDueSyncColumn(string columnName)
        {
            return columnName == BankAccountCreditCardHelper.FieldStatementCutDate
                || columnName == BankAccountCreditCardHelper.FieldPaymentDueDate
                || columnName == BankAccountCreditCardHelper.FieldPaymentDueDays;
        }

        internal static bool IsPeriodPaymentDueSyncColumn(string columnName)
        {
            return columnName == "StatementDate"
                || columnName == "PaymentDueDate"
                || columnName == BankAccountCreditCardHelper.FieldPaymentDueDays;
        }
    }
}
