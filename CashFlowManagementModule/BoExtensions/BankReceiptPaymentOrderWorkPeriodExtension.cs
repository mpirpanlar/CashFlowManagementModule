using System;
using System.ComponentModel;
using Sentez.Core.ParameterClasses;
using Sentez.Data.BusinessObjects;
using Sentez.Environment;

namespace CashFlowManagementModule.BoExtensions
{
    public class BankReceiptPaymentOrderWorkPeriodExtension : BoExtensionBase
    {
        static readonly DateTime ExpandedStartDate = new DateTime(1900, 1, 1);
        static readonly DateTime ExpandedEndDate = new DateTime(2099, 12, 31);

        DateTime? _savedStartDate;
        DateTime? _savedEndDate;
        DateTime? _savedCompanyStartDate;
        DateTime? _savedCompanyEndDate;
        bool _isWorkPeriodExpanded;

        public BankReceiptPaymentOrderWorkPeriodExtension(BusinessObjectBase bo)
            : base(bo)
        {
        }

        protected override void OnPreBeforePost(object sender, CancelEventArgs e)
        {
            base.OnPreBeforePost(sender, e);
            if (e.Cancel) return;

            ExpandWorkPeriodIfPaymentOrder();
        }

        protected override void OnAfterPost(object sender, EventArgs e)
        {
            base.OnAfterPost(sender, e);
            RestoreWorkPeriodIfExpanded();
        }

        protected override void OnAfterSucceededPost(object sender, EventArgs e)
        {
            base.OnAfterSucceededPost(sender, e);
            RestoreWorkPeriodIfExpanded();
        }

        protected override void OnPreBeforeDelete(object sender, CancelEventArgs e)
        {
            base.OnPreBeforeDelete(sender, e);
            if (e.Cancel) return;

            ExpandWorkPeriodIfPaymentOrder();
        }

        protected override void OnAfterDelete(object sender, EventArgs e)
        {
            base.OnAfterDelete(sender, e);
            RestoreWorkPeriodIfExpanded();
        }

        protected override void OnAfterSucceededDelete(object sender, EventArgs e)
        {
            base.OnAfterSucceededDelete(sender, e);
            RestoreWorkPeriodIfExpanded();
        }

        bool IsPaymentOrderReceipt()
        {
            return BankReceiptPaymentOrderHelper.IsPaymentOrderReceipt(BusinessObject);
        }

        WorkPeriodParameters GetWorkPeriodParameters()
        {
            return BusinessObject?.ActiveSession?.ParamService?.GetParameterClass<WorkPeriodParameters>();
        }

        void ExpandWorkPeriodIfPaymentOrder()
        {
            if (!IsPaymentOrderReceipt() || _isWorkPeriodExpanded) return;

            WorkPeriodParameters workPeriodParameters = GetWorkPeriodParameters();
            if (workPeriodParameters == null) return;

            _savedStartDate = workPeriodParameters.BankReceiptApprovedDate;
            _savedEndDate = workPeriodParameters.BankReceiptApprovedEndDate;
            workPeriodParameters.BankReceiptApprovedDate = ExpandedStartDate;
            workPeriodParameters.BankReceiptApprovedEndDate = ExpandedEndDate;

            CompanyInfo companyInfo = BusinessObject?.ActiveSession?._CompanyInfo;
            if (companyInfo != null)
            {
                _savedCompanyStartDate = companyInfo.StartDate;
                _savedCompanyEndDate = companyInfo.EndDate;
                companyInfo.StartDate = ExpandedStartDate;
                companyInfo.EndDate = ExpandedEndDate;
            }

            _isWorkPeriodExpanded = true;
        }

        void RestoreWorkPeriodIfExpanded()
        {
            if (!_isWorkPeriodExpanded) return;

            WorkPeriodParameters workPeriodParameters = GetWorkPeriodParameters();
            if (workPeriodParameters != null)
            {
                if (_savedStartDate.HasValue)
                    workPeriodParameters.BankReceiptApprovedDate = _savedStartDate.Value;
                if (_savedEndDate.HasValue)
                    workPeriodParameters.BankReceiptApprovedEndDate = _savedEndDate.Value;
            }

            CompanyInfo companyInfo = BusinessObject?.ActiveSession?._CompanyInfo;
            if (companyInfo != null)
            {
                if (_savedCompanyStartDate.HasValue)
                    companyInfo.StartDate = _savedCompanyStartDate;
                if (_savedCompanyEndDate.HasValue)
                    companyInfo.EndDate = _savedCompanyEndDate;
            }

            _savedStartDate = null;
            _savedEndDate = null;
            _savedCompanyStartDate = null;
            _savedCompanyEndDate = null;
            _isWorkPeriodExpanded = false;
        }
    }
}
