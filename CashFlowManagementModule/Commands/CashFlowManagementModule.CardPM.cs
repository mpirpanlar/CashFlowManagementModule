using CashFlowManagementModule.BoExtensions;

using Sentez.Common.PresentationModels;
using Sentez.Data.Query;

using System.Windows;

namespace Sentez.CashFlowManagementModule
{
    public partial class CashFlowManagementModule
    {
        void RegisterCardPmHooks()
        {
            PMBase.AddCustomViewLoaded("CardPM", MetaListEditCardPm_ViewLoaded);
        }

        void MetaListEditCardPm_ViewLoaded(object sender, RoutedEventArgs e)
        {
            var pm = sender as PMBase;
            if (pm?.ActiveBO == null)
                return;

            string baseTable = pm.ActiveBO.BaseTable;
            if (baseTable != MetaFixedPaymentTypeHelper.TableName
                && baseTable != MetaPosDeductionTypeHelper.TableName)
                return;

            var table = pm.ActiveBO.Data.Tables[baseTable];
            if (table == null || table.Rows.Count > 1)
                return;

            if (table.Columns.Contains("IsDeleted"))
                pm.ActiveBO.GetAll(new WhereField[] { WhereField.GetIsDeletedRule(baseTable) });
            else
                pm.ActiveBO.GetAll();
        }
    }
}
