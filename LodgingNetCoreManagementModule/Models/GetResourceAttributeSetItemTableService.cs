using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Sentez.Common.SystemServices;
using Sentez.Common.Commands;
using Sentez.Data.Tools;
using System.Data.Common;
using Sentez.Data.Query;
using System.Data;
using System.Collections.ObjectModel;
using Sentez.Common;

namespace Sentez.CRSModule.Services
{
    public class GetResourceAttributeSetItemTableService : SystemServiceBase
    {
        public GetResourceAttributeSetItemTableService(SysMng smgr, Guid sid) : base(smgr, sid) { }

        public override object Execute(object input)
        {
            if (input is int)
                return GetResourceAttributeSetItemTable((ResourceTypeDefinition.ResourceType)((int)input));
            else if (input is ResourceTypeDefinition.ResourceType)
                return GetResourceAttributeSetItemList((ResourceTypeDefinition.ResourceType)input);

            return GetResourceAttributeSetItemTable(ResourceTypeDefinition.ResourceType.Room);
        }

        public override object Execute(params object[] inputs)
        {
            if (inputs != null && inputs.Length == 2 && inputs[0] is ResourceTypeDefinition.ResourceType && inputs[1] is bool)
                return GetResourceAttributeSetItemTable((ResourceTypeDefinition.ResourceType)inputs[0], (bool)inputs[1]);
            else
                return GetResourceAttributeSetItemTable(ResourceTypeDefinition.ResourceType.Room);

        }

        public ObservableCollection<object> GetResourceAttributeSetItemList(ResourceTypeDefinition.ResourceType _resourceType)
        {
            ObservableCollection<object> valueList = new ObservableCollection<object>();

            StringBuilder sb = new StringBuilder();

            sb.AppendLine("SELECT RAS.RecId ResAttSetId, RAS.ResourceType, RAS.AttributeCode, RAS.AttributeName");
            sb.AppendLine(", RASI.RecId ResAttSetItemId, RASI.AttributeItemCode, RASI.AttributeItemName");
            sb.AppendLine("FROM Erp_ResourceAttributeSet RAS with (nolock)");
            sb.AppendLine("LEFT JOIN Erp_ResourceAttributeSetItem RASI with (nolock) ON (RASI.AttributeSetId = RAs.RecId)");
            sb.AppendFormat(" WHERE RAS.ResourceType = {0} and RAS.CompanyId = {1}", (int)_resourceType, ActiveSession.ActiveCompany.RecId.Value);
            sb.AppendLine("and (select count(*) from Erp_ResourceAttributeSetItem where AttributeSetId =RAS.RecId) >0");
            sb.AppendLine("Order By AttributeItemCode");

            DataTable AttributeItemsTable = UtilityFunctions.GetDataTableList(ActiveSession.dbInfo.DBProvider, ActiveSession.dbInfo.Connection, null, "ResourceAttributeSetItem", sb.ToString());
            foreach (DataRow dr in AttributeItemsTable.Rows)
                valueList.Add(new ObjPair() { Display = dr["AttributeItemName"].ToString(), Value = Convert.ToInt64(dr["ResAttSetItemId"]) });

            return valueList;
        }

        public DataTable GetResourceAttributeSetItemTable(ResourceTypeDefinition.ResourceType _resourceType)
        {
            return GetResourceAttributeSetItemTable(_resourceType, false);
        }

        public DataTable GetResourceAttributeSetItemTable(ResourceTypeDefinition.ResourceType _resourceType, bool _forComboBox)
        {
            StringBuilder sb = new StringBuilder();

            if (!_forComboBox)
            {
                sb.AppendLine("SELECT RAS.RecId ResAttSetId, RAS.ResourceType, RAS.AttributeCode, RAS.AttributeName");
                sb.AppendLine(", RASI.RecId ResAttSetItemId, RASI.AttributeItemCode, RASI.AttributeItemName");
                sb.AppendLine("FROM Erp_ResourceAttributeSet RAS with (nolock)");
                sb.AppendLine("LEFT JOIN Erp_ResourceAttributeSetItem RASI with (nolock) ON (RASI.AttributeSetId = RAS.RecId)");
                sb.AppendFormat(" WHERE RAS.ResourceType = {0} and RAS.CompanyId = {1}", (int)_resourceType, ActiveSession.ActiveCompany.RecId.Value);
                sb.AppendLine("and (select count(*) from Erp_ResourceAttributeSetItem where AttributeSetId =RAS.RecId) >0");
                sb.AppendLine("Order By AttributeItemCode");
            }
            else
            {
                sb.AppendLine("SELECT RASI.RecId Value, RASI.AttributeItemCode + ' - ' + RASI.AttributeItemName Display");
                sb.AppendLine("FROM Erp_ResourceAttributeSet RAS with (nolock)");
                sb.AppendLine("LEFT JOIN Erp_ResourceAttributeSetItem RASI with (nolock) ON (RASI.AttributeSetId = RAS.RecId)");
                sb.AppendFormat(" WHERE RAS.ResourceType = {0} and RAS.CompanyId = {1}", (int)_resourceType, ActiveSession.ActiveCompany.RecId.Value);
                sb.AppendLine("and (select count(*) from Erp_ResourceAttributeSetItem where AttributeSetId =RAS.RecId) >0");
                sb.AppendLine("Order By AttributeItemCode");
            }
            return UtilityFunctions.GetDataTableList(ActiveSession.dbInfo.DBProvider, ActiveSession.dbInfo.Connection, null, "ResourceAttributeSetItem", sb.ToString());
        }
    }
}
