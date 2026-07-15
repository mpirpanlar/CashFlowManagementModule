using System.Data;

using Sentez.Data.BusinessObjects;

namespace CashFlowManagementModule.BoExtensions
{
    /// <summary>
    /// Deleted/Detached DataRow erişimlerinde RowNotInTableException önlemek için ortak kontroller.
    /// </summary>
    public static class DataRowSafety
    {
        public static bool IsUsable(DataRow row)
        {
            return row != null
                && row.Table != null
                && row.RowState != DataRowState.Deleted
                && row.RowState != DataRowState.Detached;
        }

        public static bool TryGetCurrentRow(object businessObject, out DataRow row)
        {
            row = null;
            if (businessObject is not BusinessObjectBase bo)
                return false;

            row = bo.CurrentRow?.Row;
            return IsUsable(row);
        }

        public static bool IsDeletedOrDetached(DataRow row)
        {
            return row == null
                || row.RowState == DataRowState.Deleted
                || row.RowState == DataRowState.Detached;
        }
    }
}
