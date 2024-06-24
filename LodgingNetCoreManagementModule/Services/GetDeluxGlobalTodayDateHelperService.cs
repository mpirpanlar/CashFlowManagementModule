using System;
using System.Data.Common;
using Sentez.Common.Commands;
using Sentez.Common.SystemServices;
using Sentez.Common.Utilities;

namespace Sentez.CRSModule.Services
{
    public class GetAgileGlobalTodayDateHelperService : SystemServiceBase
    {
        DateHelper dateHelper = null;

        public GetAgileGlobalTodayDateHelperService(SysMng smgr, Guid sid) : base(smgr, sid) { }

        public override object Execute(object input)
        {
            if (input == null)
                return GetToday();
            else if (input != null && input is int && ((int)input) == -1)
                return GetLastToday();

            return null;
        }

        public override object Execute(params object[] inputs)
        {
            if (inputs == null)
                return GetToday();
            if (inputs.Length == 2 && inputs[0] is DateHelper && inputs[1] is int)
            {
                dateHelper = (DateHelper)inputs[0];

                if ((int)inputs[1] == 0)
                    return GetToday();
                if ((int)inputs[1] == -1)
                    return GetLastToday();
            }
            else if (inputs.Length == 2 && inputs[0] is DateTime && inputs[1] is int)
                return CompareDate((DateTime)inputs[0], (int)inputs[1]);

            return null;
        }

        public DateTime GetToday(DbTransaction tr = null)
        {
            if (dateHelper == null)
                dateHelper = new DateHelper(_container);
            else dateHelper.Container = _container;
            dateHelper.OperationMode = OperationMode.AgileMode;
            if (ActiveSession?.Workplace?.RecId != null)
                dateHelper.WorkplaceId = ActiveSession.Workplace.RecId.Value;

            return dateHelper.GetToday(tr);
            //return dateHelper.GetToday();
        }

        public DateTime GetLastToday()
        {
            if (dateHelper == null)
                dateHelper = new DateHelper(_container);

            dateHelper.OperationMode = OperationMode.AgileMode;
            if (ActiveSession?.Workplace?.RecId != null)
                dateHelper.WorkplaceId = ActiveSession.Workplace.RecId.Value;

            return dateHelper.GetToday();
        }

        public bool CompareDate(DateTime sourceDate, int compareType)
        {
            if (compareType == -1)
            {
                if (GetLastToday() != sourceDate)
                    return false;
            }
            else if (compareType == 0)
            {
                if (GetToday() != sourceDate)
                    return false;
            }

            return true;
        }
    }
}
