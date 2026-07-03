using CashFlowManagementModule.BoExtensions;

using Prism.Ioc;

using Reeb.SqlOM;

using Sentez.Common.Report;
using Sentez.Common.SqlBuilder;
using Sentez.Localization;

namespace CashFlowManagementModule.WorkList
{
    public class MetaFixedPaymentTypeList : ReportBase
    {
        public override bool CacheResults => true;

        public MetaFixedPaymentTypeList(IContainerExtension container)
            : base(container)
        {
            Name = MetaFixedPaymentTypeHelper.ListName;
            Title = SLanguage.GetString("Tekrar Eden Ödeme Tipi Tanımları Listesi");
            WorkMode = ReportWorkMode.WorkList;
        }

        public override void Init()
        {
            InitBegin();

            Statement statement = new Statement(MetaFixedPaymentTypeHelper.TableName);
            statement.AddTable(MetaFixedPaymentTypeHelper.TableName, "meta_fixedpaymenttype");
            statement.SetBaseTable("meta_fixedpaymenttype");
            statement.LoadAllFields();
            statement.AddCol("RecId", "meta_fixedpaymenttype", "RecId", false);
            statement.AddColMandatory("FixedPaymentTypeCode", "meta_fixedpaymenttype", SLanguage.GetString("Kod"));
            statement.AddColMandatory("FixedPaymentTypeName", "meta_fixedpaymenttype", SLanguage.GetString("Adı"));
            statement.AddColMandatory("InUse", "meta_fixedpaymenttype", SLanguage.GetString("Kullanımda"));
            statement.AddMandatoryFilters(activeSession);
            statement.OrderBy("meta_fixedpaymenttype", "FixedPaymentTypeCode", OrderByDirection.Ascending);

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
