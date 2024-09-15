using LiveCore.Desktop.Common;
using LiveCore.Desktop.UI.Controls;

using Sentez.Common.ModuleBase;
using Sentez.Common.PresentationModels;
using Sentez.Common.Utilities;
using Sentez.Data.BusinessObjects;
using Sentez.Data.Tools;
using Sentez.Finance.PresentationModels;
using Sentez.HRMModule.PresentationModels;
using Sentez.InventoryModule.PresentationModels;
using Sentez.Localization;
using Sentez.QuotationModule.PresentationModels;
using Sentez.SettingsModule.PresentationModels;

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using System.Windows.Input;

namespace Sentez.CashFlowManagementModule
{
	public partial class CashFlowManagementModule :  LiveModule
	{
		public LookupList Lists { get; set; }
        public LookupList Lists_QuotationReceiptPM { get; set; }
        LiveTabItem ltiEmployeeLodging, ltiEmployeeLog;
        LiveDocumentPanel ltiEmployeeLodgingResource, ltiUserWorkplace;
        InventoryPM inventoryPm;
		QuotationReceiptPM quotationReceiptPm;
		HRMEmployeePM hrmEmployeePm { get; set; }
        CheckingPM checkingPm { get; set; }
        BatchCheckingPM batchCheckingPm { get; set; }
        BatchAccrualPM batchAccrualPm { get; set; }
        ExactAccrualPM exactAccrualPm { get; set; }
        MonthEndTakeoverOperationsPM monthEndTakeoverOperationsPm { get; set; }
        YearEndTakeoverOperationsPM yearEndTakeoverOperationsPm { get; set; }
        CurrentAccountPM currentAccountPm { get; set; }
        UserCardPM userCardPm { get; set; }

        bool _suppressEvent = false;
	}
}
