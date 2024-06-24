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
    public class GetRoomTypeService : SystemServiceBase
    {
        public GetRoomTypeService(SysMng smgr, Guid sid) : base(smgr, sid) { }

        public override object Execute(object input)
        {
            if (input == null)
                return GetRoomTypeServiceTypes();
            else return GetRoomTypeServiceTypes((bool)input);
        }

        public override object Execute(params object[] inputs)
        {
            return GetRoomTypeServiceTypes();
        }

        public object GetRoomTypeServiceTypes()
        {
            return GetRoomTypeServiceTypes(false);
        }

        public object GetRoomTypeServiceTypes(bool _addEmptyRow)
        {
            string workplaceIdStr = string.Empty;
            if (ActiveSession.ActiveUser.UserCompany != null)
            {
                foreach (var workplace in ActiveSession.ActiveUser.UserCompany)
                {
                    if (!workplace.WorkplaceId.HasValue) continue;
                    if (string.IsNullOrEmpty(workplaceIdStr))
                        workplaceIdStr = workplace.WorkplaceId.ToString();
                    else
                        workplaceIdStr += "," + workplace.WorkplaceId.ToString();
                }
                if (!string.IsNullOrEmpty(workplaceIdStr))
                    workplaceIdStr = $"and WorkplaceId in ({workplaceIdStr})";
            }
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("select RecId Value");
            sb.AppendLine(", IsNull(ResourceCode,'') + ' - ' + IsNull(Explanation,'') Display");
            sb.AppendFormat(", IsNull(ResourceCode,'') '{0}'", SLanguage.GetString("Oda Tipi Kodu"));
            sb.AppendFormat(", Explanation '{0}'", SLanguage.GetString("Oda Tipi Adı"));
            sb.AppendLine(" from");
            sb.AppendLine(" Erp_Resource with (nolock)");
            sb.AppendFormat(" Where CompanyId={0}  and IsNull(InUse,0)=1 and ResourceType = {1}", _activeSession.ActiveCompany.RecId.Value, (int)ResourceTypeDefinition.ResourceType.RoomType);
            if (!string.IsNullOrEmpty(workplaceIdStr))
                sb.AppendLine($"{workplaceIdStr}");
            else if (ActiveSession.Workplace != null && ActiveSession.Workplace.RecId.HasValue)
                sb.AppendLine($"and WorkplaceId = {ActiveSession.Workplace.RecId.Value}");
            sb.AppendLine(" Order By Explanation");

            DataTable dt1 = UtilityFunctions.GetDataTableList(ActiveSession.dbInfo.DBProvider, ActiveSession.dbInfo.Connection, null, "crs_Resource", sb.ToString());

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
