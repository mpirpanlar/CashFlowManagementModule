using LiveCore.Desktop.SBase.MenuManager;
using Sentez.Common;
using Sentez.Common.Commands;
using Sentez.Common.ModuleBase;
using Sentez.Common.ResourceManager;
using Sentez.Common.SystemServices;
using Sentez.Data.MetaData.DatabaseControl;
using System;
using System.IO;
using System.Reflection;
using LodgingNetCoreManagementModule.Services;
using Sentez.Data.MetaData;
using Sentez.Data.Tools;
using Sentez.Localization;
using LiveCore.Desktop.Common;
using Prism.Ioc;
using Sentez.Common.SBase;
using Sentez.LodgingNetCoreManagementModule.Models;
using Sentez.Data.BusinessObjects;
using Sentez.CRSModule.Services;
using Sentez.Core.ParameterClasses;
using Sentez.CRSModule.Models;
using Sentez.Parameters;
using LodgingManagementModule.BoExtensions;
using Sentez.Common.PresentationModels;
using Sentez.CRSUIModule.PresentationModels;
using Sentez.CRSModule.WorkList;
using Sentez.Common.Report;
using System.Windows.Input;
//using DevExpress.XtraScheduler.Outlook.Interop;
using Microsoft.Office.Interop.Outlook;
using Sentez.Finance.PresentationModels;
using DevExpress.XtraRichEdit.SpellChecker;
using System.Text;
using System.Data;
using DevExpress.XtraRichEdit.Model;
using System.Runtime.InteropServices;

namespace Sentez.LodgingNetCoreManagementModule
{
    public partial class LodgingNetCoreManagementModule : LiveModule
    {
        void RegisterModuleCommands_CurrentAccount()
        {
            ISysCommand _sendEmailCurAccBalanceCommand = SysMng.Instance.RegisterCmd(moduleID, 101, "SendEmailCurAccBalanceCmd", SLanguage.GetString("Bakiye Bilgisini Mail At"), SendEmailCurAccBalanceCommand, null);
            MenuItemPM SendEmailCurAccBalanceCommandMenuItem = new MenuItemPM();
            SendEmailCurAccBalanceCommandMenuItem.MenuItemCommandParam = new SysCommandParam() { Tag = "1" };
            SendEmailCurAccBalanceCommandMenuItem.MenuItemCommand = _sendEmailCurAccBalanceCommand;
            SendEmailCurAccBalanceCommandMenuItem.Caption = SLanguage.GetString("Bakiye Bilgisini Mail At");
            SendEmailCurAccBalanceCommandMenuItem.Name = "SendEmailCurAccBalanceCmd";
            SendEmailCurAccBalanceCommandMenuItem.ShortcutKey = Key.F12;

            _sysMng.ExtraContextItems.Add("CurrentAccountPM", new MenuItemPM("Separator_LodgingNetCoreManagementModule_SendEmailCurAccBalance", ""));
            _sysMng.ExtraContextItems.Add("CurrentAccountPM", SendEmailCurAccBalanceCommandMenuItem);

            _sysMng.ExtraContextItems.Add("Erp_CurrentAccountCurrentAccountCodeList", new MenuItemPM("Separator_LodgingNetCoreManagementModule_SendEmailCurAccBalance", ""));
            _sysMng.ExtraContextItems.Add("Erp_CurrentAccountCurrentAccountCodeList", SendEmailCurAccBalanceCommandMenuItem);
        }

        private void SendEmailCurAccBalanceCommand(ISysCommandParam param)
        {
            if (currentAccountPm == null && param?.SelectedFields?.Count == 0)
            {
                return;
            }

            // Create a new Outlook application.
            Application outlookApp = new Application();

            // Create a new mail item.
            MailItem mailItem = (MailItem)outlookApp.CreateItem(OlItemType.olMailItem);
            IBusinessObject currentAccountBo = _sysMng._container.Resolve<IBusinessObject>("CurrentAccountBO");
            try
            {
                if (currentAccountPm?.ActiveBO?.CurrentRow != null)
                {
                    // Set the subject and body of the email.
                    mailItem.Subject = $"Ödeme {currentAccountPm.ActiveBO.CurrentRow["CurrentAccountName"]}";
                    StringBuilder stringBuilder = new StringBuilder();
                    stringBuilder.AppendLine(currentAccountPm.ActiveBO.CurrentRow["CurrentAccountName"].ToString());
                    DataRow[] dataRows = currentAccountPm.ActiveBO.Data.Tables["Erp_CurrentAccountBank"].Select("IsNull(IsDefault,0)=1", "", DataViewRowState.CurrentRows);
                    if (dataRows?.Length > 0)
                    {
                        stringBuilder.AppendLine(dataRows[0]["IbanNo"].ToString());
                    }
                    DataRow[] balanceDataRows = currentAccountPm.ActiveBO.Data.Tables["MonthlyBalance"].Select("", "", DataViewRowState.CurrentRows);
                    if (balanceDataRows?.Length > 0)
                    {
                        decimal balance;
                        decimal.TryParse(balanceDataRows[balanceDataRows.Length - 1]["Balance"].ToString(), out balance);
                        string formattedValue = string.Format("{0:N2}", balance);
                        stringBuilder.AppendLine($"Tutar :{formattedValue} - {balanceDataRows[balanceDataRows.Length - 1]["BT"]}");
                    }
                    else
                    {
                        int startYear = DateTime.Now.Year;
                        int endYear = DateTime.Now.Year;

                        if (_sysMng.getSession().ActiveCompany.StartDate != null) startYear = Convert.ToDateTime(_sysMng.getSession().ActiveCompany.StartDate).Year;
                        if (_sysMng.getSession().ActiveCompany.EndDate != null) endYear = Convert.ToDateTime(_sysMng.getSession().ActiveCompany.EndDate).Year;

                        string debitStr = "(select sum(isnull(at.Debit01,0)+isnull(at.Debit02,0)+isnull(at.Debit03,0)+isnull(at.Debit04,0)+isnull(at.Debit05,0)+isnull(at.Debit06,0)+isnull(at.Debit07,0)+isnull(at.Debit08,0)+isnull(at.Debit09,0)+isnull(at.Debit10,0)+isnull(at.Debit11,0)+isnull(at.Debit12,0)) from Erp_CurrentAccountTotal at with (nolock) where at.CurrentAccountId=erp_curracc.RecId and at.ForexId is null";
                        if (_sysMng.getSession().ParamService.GetParameterClass<WorkPeriodParameters>().ReportDefaultPeriod == 0)
                            debitStr += $" and at.FiscalYear >= {startYear} and at.FiscalYear <= {endYear}";
                        debitStr += ")";
                        string creditStr = "(select sum(isnull(at.Credit01,0)+isnull(at.Credit02,0)+isnull(at.Credit03,0)+isnull(at.Credit04,0)+isnull(at.Credit05,0)+isnull(at.Credit06,0)+isnull(at.Credit07,0)+isnull(at.Credit08,0)+isnull(at.Credit09,0)+isnull(at.Credit10,0)+isnull(at.Credit11,0)+isnull(at.Credit12,0)) from Erp_CurrentAccountTotal at with (nolock) where at.CurrentAccountId=erp_curracc.RecId and at.ForexId is null";
                        if (_sysMng.getSession().ParamService.GetParameterClass<WorkPeriodParameters>().ReportDefaultPeriod == 0)
                            creditStr += $" and at.FiscalYear >= {startYear} and at.FiscalYear <= {endYear}";
                        creditStr += ")";
                        string balanceStr = "(case when " + debitStr + " > " + creditStr + " then " + debitStr + " - " + creditStr + " else " + creditStr + " - " + debitStr + " end)";
                        string balanceTypeStr = "(case when " + debitStr + " > " + creditStr + " then N'" + SLanguage.GetString("BB") + "' when " + creditStr + " > " + debitStr + " then N'" + SLanguage.GetString("AB") + "' else '' end)";
                        StringBuilder stringBuilder1 = new StringBuilder();
                        stringBuilder1.AppendLine("SELECT erp_curracc.RecId");
                        stringBuilder1.AppendLine($",{debitStr} TotalDebit");
                        stringBuilder1.AppendLine($",{creditStr} TotalCredit");
                        stringBuilder1.AppendLine($",{balanceStr} Balance");
                        stringBuilder1.AppendLine($",{balanceTypeStr} BalanceType");
                        stringBuilder1.AppendLine($"FROM Erp_CurrentAccount erp_curracc WITH (NOLOCK) WHERE erp_curracc.RecId={currentAccountPm.ActiveBO.CurrentRow["RecId"]}");
                        using (DataTable dataTable = UtilityFunctions.GetDataTableList(_sysMng.getSession().dbInfo.DBProvider, _sysMng.getSession().dbInfo.GetNewConnection(), null, "Erp_CurrentAccountTotal", stringBuilder1.ToString()))
                        {
                            if (dataTable?.Rows.Count > 0)
                            {
                                decimal balance;
                                decimal.TryParse(dataTable.Rows[0]["Balance"].ToString(), out balance);
                                string formattedValue = string.Format("{0:N2}", balance);
                                stringBuilder.AppendLine($"Tutar :{formattedValue} - {dataTable.Rows[0]["BalanceType"]}");
                            }
                        }
                    }
                    if (dataRows?.Length > 0)
                    {
                        stringBuilder.AppendLine(dataRows[0]["Explanation"].ToString());
                    }
                    mailItem.Body = stringBuilder.ToString();
                    /*
                    mailItem.Body = "ELA MERMERCİLİK İTH. İHR. TUR. VE TİC. LTD. ŞTİ\n" +
                                    "TR200001500158007308260300\n" +
                                    "Tutar     :200.000,00-tl\n" +
                                    "ELA MERMERCİLİK İTH ay-tur aşden";
                    */
                    // Display the mail item.
                    mailItem.Display(true); // true to show the inspector and allow the user to edit the email

                    // Optionally, you can also send the email.
                    // mailItem.Send();
                }
                else if (param?.SelectedFields?.Count > 0)
                {
                    foreach (var item in param.SelectedFields)
                    {
                        if (currentAccountBo.Get(Convert.ToInt64(item)) > 0)
                        {
                            mailItem = (MailItem)outlookApp.CreateItem(OlItemType.olMailItem);
                            mailItem.Subject = $"Ödeme {currentAccountBo.CurrentRow["CurrentAccountName"]}";

                            StringBuilder stringBuilder = new StringBuilder();
                            stringBuilder.AppendLine(currentAccountBo.CurrentRow["CurrentAccountName"].ToString());
                            DataRow[] dataRows = currentAccountBo.Data.Tables["Erp_CurrentAccountBank"].Select("IsNull(IsDefault,0)=1", "", DataViewRowState.CurrentRows);
                            if (dataRows?.Length > 0)
                            {
                                stringBuilder.AppendLine(dataRows[0]["IbanNo"].ToString());
                            }
                            if (currentAccountBo.Data.Tables.Contains("MonthlyBalance"))
                            {
                                DataRow[] balanceDataRows = currentAccountBo.Data.Tables["MonthlyBalance"].Select("", "", DataViewRowState.CurrentRows);
                                if (balanceDataRows?.Length > 0)
                                {
                                    decimal balance;
                                    decimal.TryParse(balanceDataRows[balanceDataRows.Length - 1]["Balance"].ToString(), out balance);
                                    string formattedValue = string.Format("{0:N2}", balance);
                                    stringBuilder.AppendLine($"Tutar :{formattedValue} - {balanceDataRows[balanceDataRows.Length - 1]["BT"]}");
                                }
                            }
                            else
                            {
                                int startYear = DateTime.Now.Year;
                                int endYear = DateTime.Now.Year;

                                if (_sysMng.getSession().ActiveCompany.StartDate != null) startYear = Convert.ToDateTime(_sysMng.getSession().ActiveCompany.StartDate).Year;
                                if (_sysMng.getSession().ActiveCompany.EndDate != null) endYear = Convert.ToDateTime(_sysMng.getSession().ActiveCompany.EndDate).Year;

                                string debitStr = "(select sum(isnull(at.Debit01,0)+isnull(at.Debit02,0)+isnull(at.Debit03,0)+isnull(at.Debit04,0)+isnull(at.Debit05,0)+isnull(at.Debit06,0)+isnull(at.Debit07,0)+isnull(at.Debit08,0)+isnull(at.Debit09,0)+isnull(at.Debit10,0)+isnull(at.Debit11,0)+isnull(at.Debit12,0)) from Erp_CurrentAccountTotal at with (nolock) where at.CurrentAccountId=erp_curracc.RecId and at.ForexId is null";
                                if (_sysMng.getSession().ParamService.GetParameterClass<WorkPeriodParameters>().ReportDefaultPeriod == 0)
                                    debitStr += $" and at.FiscalYear >= {startYear} and at.FiscalYear <= {endYear}";
                                debitStr += ")";
                                string creditStr = "(select sum(isnull(at.Credit01,0)+isnull(at.Credit02,0)+isnull(at.Credit03,0)+isnull(at.Credit04,0)+isnull(at.Credit05,0)+isnull(at.Credit06,0)+isnull(at.Credit07,0)+isnull(at.Credit08,0)+isnull(at.Credit09,0)+isnull(at.Credit10,0)+isnull(at.Credit11,0)+isnull(at.Credit12,0)) from Erp_CurrentAccountTotal at with (nolock) where at.CurrentAccountId=erp_curracc.RecId and at.ForexId is null";
                                if (_sysMng.getSession().ParamService.GetParameterClass<WorkPeriodParameters>().ReportDefaultPeriod == 0)
                                    creditStr += $" and at.FiscalYear >= {startYear} and at.FiscalYear <= {endYear}";
                                creditStr += ")";
                                string balanceStr = "(case when " + debitStr + " > " + creditStr + " then " + debitStr + " - " + creditStr + " else " + creditStr + " - " + debitStr + " end)";
                                string balanceTypeStr = "(case when " + debitStr + " > " + creditStr + " then N'" + SLanguage.GetString("BB") + "' when " + creditStr + " > " + debitStr + " then N'" + SLanguage.GetString("AB") + "' else '' end)";
                                StringBuilder stringBuilder1 = new StringBuilder();
                                stringBuilder1.AppendLine("SELECT erp_curracc.RecId");
                                stringBuilder1.AppendLine($",{debitStr} TotalDebit");
                                stringBuilder1.AppendLine($",{creditStr} TotalCredit");
                                stringBuilder1.AppendLine($",{balanceStr} Balance");
                                stringBuilder1.AppendLine($",{balanceTypeStr} BalanceType");
                                stringBuilder1.AppendLine($"FROM Erp_CurrentAccount erp_curracc WITH (NOLOCK) WHERE erp_curracc.RecId={item}");
                                using (DataTable dataTable = UtilityFunctions.GetDataTableList(_sysMng.getSession().dbInfo.DBProvider, _sysMng.getSession().dbInfo.GetNewConnection(), null, "Erp_CurrentAccountTotal", stringBuilder1.ToString()))
                                {
                                    if (dataTable?.Rows.Count > 0)
                                    {
                                        decimal balance;
                                        decimal.TryParse(dataTable.Rows[0]["Balance"].ToString(), out balance);
                                        string formattedValue = string.Format("{0:N2}", balance);
                                        stringBuilder.AppendLine($"Tutar :{formattedValue} - {dataTable.Rows[0]["BalanceType"]}");
                                    }
                                }
                            }
                            if (dataRows?.Length > 0)
                            {
                                stringBuilder.AppendLine(dataRows[0]["Explanation"].ToString());
                            }
                            mailItem.Body = stringBuilder.ToString();
                            /*
                            mailItem.Body = "ELA MERMERCİLİK İTH. İHR. TUR. VE TİC. LTD. ŞTİ\n" +
                                            "TR200001500158007308260300\n" +
                                            "Tutar     :200.000,00-tl\n" +
                                            "ELA MERMERCİLİK İTH ay-tur aşden";
                            */
                            // Display the mail item.
                            mailItem.Display(true); // true to show the inspector and allow the user to edit the email

                            // Optionally, you can also send the email.
                            // mailItem.Send();                        }
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                _sysMng.ActWndMng.ShowMsg(ex.Message);
            }
            finally
            {
                // Release COM objects to avoid memory leaks
                if (mailItem != null)
                {
                    Marshal.ReleaseComObject(mailItem);
                    mailItem = null;
                }
                if (outlookApp != null)
                {
                    Marshal.ReleaseComObject(outlookApp);
                    outlookApp = null;
                }

                // Force garbage collection to clean up the COM objects
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }

        private bool CurrentAccountPm_OnListCommand(PMBase pm, PmParam parameter, ISysCommandParam commandParam)
        {
            return false;
        }

        private void CurrentAccountPm_ViewLoaded(object sender, System.Windows.RoutedEventArgs e)
        {
        }

        private void CurrentAccountPm_Dispose(PMBase pm, PmParam parameter)
        {
        }

        private void CurrentAccountPm_Init(PMBase pm, PmParam parameter)
        {
            currentAccountPm = pm as CurrentAccountPM;
        }
    }
}
