using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Sentez.Common.Report;
using Sentez.Localization;
using Sentez.Common.SqlBuilder;
using Sentez.Common.Commands;
using Sentez.Common.SystemServices;
using System.Data;
using Sentez.Data.Tools;

namespace Sentez.CRSModule.Services
{
    public class GetBedTypeService : SystemServiceBase
    {
        public GetBedTypeService(SysMng smgr, Guid sid) : base(smgr, sid) { }

        public override object Execute(object input)
        {
            if (input != null)
                return GetBedTypeServiceTypes((bool)input);
            
            return GetBedTypeServiceTypes(false);
        }

        public override object Execute(object[] inputs)
        {
            if (inputs != null && inputs.Length == 1 && inputs[0] is bool)
                return GetBedTypeServiceTypes((bool)inputs[0]);

            return GetBedTypeServiceTypes(false);
        }

        public object GetBedTypeServiceTypes()
        {
            return GetBedTypeServiceTypes(false);
        }

        public object GetBedTypeServiceTypes(bool _addEmptyRow)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("select RecId Value");
            sb.AppendFormat(",BedTypeCode '{0}', BedTypeName '{1}', BedTypeCode + ' - ' + BedTypeName Display", SLanguage.GetString("Yatak Tipi Kodu"), SLanguage.GetString("Yatak Tipi Adı"));
            sb.AppendLine(" from");
            sb.AppendLine(" Meta_BedType with (nolock) ");

            DataTable dt1 = UtilityFunctions.GetDataTableList(ActiveSession.dbInfo.DBProvider, ActiveSession.dbInfo.Connection, null, "Meta_BedType", sb.ToString());

            if (_addEmptyRow)
            {
                DataTable dtClone = dt1.Clone();
                DataRow eRow = dtClone.NewRow(); dtClone.Rows.Add(eRow);

                foreach (DataRow dr in dt1.Rows)
                    dtClone.ImportRow(dr);

                dt1 = dtClone;
            }

            return dt1;
        }
    }
}
