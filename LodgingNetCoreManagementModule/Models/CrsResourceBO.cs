using System;
using Sentez.Common.Security;
using Sentez.Common.ModuleBase;
using Sentez.Data.BusinessObjects;
using Sentez.Data.Query;
using Sentez.Common.SystemServices;
using Sentez.Localization;
//using Sentez.CRSModule.BoExtensions;
using Sentez.Core.ParameterClasses;
using Sentez.Common.Licensing;
using System.Linq;
using System.Data;
using Sentez.Data.Tools;
using Prism.Ioc;

namespace Sentez.CRSModule.Models
{
    [SecurityModuleId((short)Modules.ExternalModule15)]
    [SecurityItemId(0)]
    public class CrsResourceBO : BusinessObjectBase
    {
        CrsParameters _crsParameters = null;

        public CrsResourceBO(IContainerExtension container)
            : base(container, 0, "ResourceCode", string.Empty, new string[] { "Erp_Resource", "Erp_ResourceAttribute" })
        {
            //IsLicenseLimitControl = true;
            //DemoRecordCount = 2;

            //if (LicenseManager.Instance.IsDataLoaded && LicenseManager.Instance.IsLicenseControl && !LicenseManager.Instance.IsDemo)
            //    LicenseRecordCount = LicenseManager.Instance.DbLicenses.FirstOrDefault(x => x.LicenseType == 1).License.AgileRoomCount;

            _crsParameters = ActiveSession.ParamService.GetParameterClass<CrsParameters>();

            KeyFields.Add(new WhereField("Erp_Resource", "CompanyId", _companyId, WhereCondition.Equal));

            Lookups.AddLookUp("Erp_Resource", "WorkplaceId", true, "Erp_Workplace", "WorkplaceCode", "WorkplaceCode", "WorkplaceName", "WorkplaceName");
            Lookups.AddLookUp("Erp_Resource", "EmployeeId", true, "Erp_Employee", "EmployeeCode", "EmployeeCode", "EmployeeName", "EmployeeName");
            //Lookups.AddLookUp("Erp_Resource", "BedTypeId", true, "Meta_BedType", "BedTypeCode", "BedTypeCode", "BedTypeName", "BedTypeName");
            Lookups.AddLookUp("Erp_Resource", "ParentResourceId", true, "Erp_Resource", "ResourceCode", "ParentResourceCode", new string[] { "Explanation", "InUse", "Capacity", "ExtraCapacity" }, new string[] { "ParentResourceExplanation", "ParentResourceInUse", "ParentResourceCapacity", "ParentResourceExtraCapacity" });

            Lookups.AddLookUp("Erp_ResourceAttribute", "AttributeSetItemId", true, "Erp_ResourceAttributeSetItem", "AttributeItemCode", "AttributeItemCode", "AttributeItemName", "AttributeItemName");

            if (ActiveSession != null && ActiveSession.Workplace != null && ActiveSession.Workplace.RecId.HasValue) ValueFiller.AddRule("Erp_Resource", "WorkplaceId", ActiveSession.Workplace.RecId.Value);
            if (_crsParameters != null)
            {
                //ValueFiller.AddRule("Erp_Resource", "CleaningDay", _crsParameters.RoomCleaningDay);
                //ValueFiller.AddRule("Erp_Resource", "ChangingDay", _crsParameters.RoomChangingDay);
            }

            //ValueFiller.AddRule("Erp_Resource", "IsAvailable", 1);
            ValueFiller.AddRule("Erp_Resource", "InUse", 1);
            ValueFiller.AddRule("Erp_ResourceAttribute", "InUse", 1);

            SecurityChecker.LogicalModuleID = (short)Modules.CRSModule;

            //new CheckResourceRequiredFieldExtension(this);
        }

        public override void Init(BoParam boParam)
        {
            base.Init(boParam);

            if (boParam == null)
                return;

            int resourceType = 0;
            resourceType = boParam.Type;

            // eğer kaynak tipi bulunamadıysa ön değer olarak Oda atıyoruz.
            if (resourceType == 0)
                resourceType = (int)ResourceTypeDefinition.ResourceType.Room;

            ResourceTypeDefinition dtd = ResourceTypeDefinitions.GetResourceType(resourceType);
            if (dtd == null)
            {
                throw new Exception(SLanguage.GetString("Kaynak tipi hatalı."));
            }

            ValueFiller.AddRule("Erp_Resource", "ResourceType", resourceType);
            KeyFields.Add(new WhereField("Erp_Resource", "ResourceType", resourceType, WhereCondition.Equal));

            //if (resourceType == (int)ResourceTypeDefinition.ResourceType.Room)
            //    new CheckResourceRequiredFieldExtension(this);

            if (resourceType != 5) IsLicenseLimitControl = false;
        }

        protected override void OnAfterGet(object sender, EventArgs ea)
        {
            base.OnAfterGet(sender, ea);
        }
    }
}
