using CashFlowManagementModule.BoExtensions;

using Prism.Ioc;

using Reeb.SqlOM;

using Sentez.Common.Report;
using Sentez.Common.SqlBuilder;
using Sentez.Localization;

namespace CashFlowManagementModule.WorkList
{
    public class MetaPosDeductionTypeList : ReportBase
    {
        public override bool CacheResults => true;

        public MetaPosDeductionTypeList(IContainerExtension container)
            : base(container)
        {
            Name = MetaPosDeductionTypeHelper.ListName;
            Title = SLanguage.GetString("Pos Kesinti Türü Tanımları Listesi");
            WorkMode = ReportWorkMode.WorkList;
        }

        public override void Init()
        {
            InitBegin();

            Statement statement = new Statement(MetaPosDeductionTypeHelper.TableName);
            statement.AddTable(MetaPosDeductionTypeHelper.TableName, "meta_posdeductiontype");
            statement.SetBaseTable("meta_posdeductiontype");
            statement.LoadAllFields();
            statement.AddCol("RecId", "meta_posdeductiontype", "RecId", false);
            statement.AddColMandatory("PosDeductionTypeCode", "meta_posdeductiontype", SLanguage.GetString("Kod"));
            statement.AddColMandatory("PosDeductionTypeName", "meta_posdeductiontype", SLanguage.GetString("Adı"));
            statement.AddColMandatory("InUse", "meta_posdeductiontype", SLanguage.GetString("Kullanımda"));
            statement.AddMandatoryFilters(activeSession);
            statement.OrderBy("meta_posdeductiontype", "PosDeductionTypeCode", OrderByDirection.Ascending);

            AddStatement(statement);
            InitEnd();
        }

        public override object GetResultFieldValue(int row)
        {
            if (!Data.Tables[0].Columns.Contains(GetResultFieldName())) return null;
            return Data.Tables[0].DefaultView[row][GetResultFieldName()];
        }
    }
}
