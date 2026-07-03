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
            PMBase.AddCustomViewLoaded("CardPM", MetaFixedPaymentTypeCardPm_ViewLoaded);
        }

        void MetaFixedPaymentTypeCardPm_ViewLoaded(object sender, RoutedEventArgs e)
        {
            var pm = sender as PMBase;
            if (pm?.ActiveBO == null || pm.ActiveBO.BaseTable != MetaFixedPaymentTypeHelper.TableName)
                return;

            var table = pm.ActiveBO.Data.Tables[pm.ActiveBO.BaseTable];
            if (table == null || table.Rows.Count > 1)
                return;

            if (table.Columns.Contains("IsDeleted"))
                pm.ActiveBO.GetAll(new WhereField[] { WhereField.GetIsDeletedRule(pm.ActiveBO.BaseTable) });
            else
                pm.ActiveBO.GetAll();
        }
    }
}
