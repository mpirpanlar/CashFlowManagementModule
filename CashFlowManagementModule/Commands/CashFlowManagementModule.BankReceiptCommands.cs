using System;
using System.Collections;
using System.Data;
using System.Windows;

using CashFlowManagementModule.BoExtensions;
using CashFlowManagementModule.Services;

using LiveCore.Desktop.Common;
using LiveCore.Desktop.UI.Controls;

using Prism.Ioc;

using Sentez.BankModule;
using Sentez.BankModule.Models;
using Sentez.Common;
using Sentez.Common.Commands;
using Sentez.Common.ModuleBase;
using Sentez.Common.Security;
using Sentez.Common.SystemServices;
using Sentez.Common.Utilities;
using Sentez.Common.UISystem;
using Sentez.Common.Workflow;
using Sentez.Core.ParameterClasses;
using Sentez.Data.BusinessObjects;
using Sentez.Data.Tools;
using Sentez.Localization;

namespace Sentez.CashFlowManagementModule
{
    public partial class CashFlowManagementModule
    {
        ISysCommand _originalBankReceiptApprovedChangeCommand;

        void EnsureBankReceiptApprovedChangeCommandRegistered()
        {
            if (_bankReceiptApprovedChangeCommandRegistered) return;

            ISysCommand currentCommand = SysMng.Instance.GetCmd("BankReceiptApprovedChangeCommand");
            if (currentCommand == null) return;

            if (_originalBankReceiptApprovedChangeCommand == null)
                _originalBankReceiptApprovedChangeCommand = currentCommand;

            SysMng.Instance.SysCmdList.Remove(currentCommand);

            SysMng.Instance.RegisterCmd(
                currentCommand.ModuleId,
                currentCommand.cmdID,
                "BankReceiptApprovedChangeCommand",
                currentCommand.Caption,
                OnPaymentOrderAwareBankReceiptApprovedChangeCommand,
                CanPaymentOrderAwareBankReceiptApprovedChangeCommand);

            _bankReceiptApprovedChangeCommandRegistered = true;
        }

        bool CanPaymentOrderAwareBankReceiptApprovedChangeCommand(ISysCommandParam arg)
        {
            return true;
        }

        void OnPaymentOrderAwareBankReceiptApprovedChangeCommand(ISysCommandParam obj)
        {
            if (!ShouldHandleAsPaymentOrder(obj))
            {
                _originalBankReceiptApprovedChangeCommand?.Execute(obj);
                return;
            }

            OnPaymentOrderBankReceiptApprovedChangeCommand(obj);
        }

        bool ShouldHandleAsPaymentOrder(ISysCommandParam obj)
        {
            ArrayList recList = obj?.SelectedFields;
            if (recList == null || recList.Count == 0) return false;

            BankReceiptBO bankReceiptBo = _container.Resolve<BankReceiptBO>("BankReceiptBO");
            if (bankReceiptBo == null) return false;

            bool isHeaderRecId = obj.PmName == "BankReceiptPM";
            if (!BankReceiptPaymentOrderHelper.TryGetReceiptType(bankReceiptBo, (long)recList[0], isHeaderRecId, out short receiptType))
                return false;

            return BankReceiptPaymentOrderHelper.IsPaymentOrderReceiptType(receiptType);
        }

        void OnPaymentOrderBankReceiptApprovedChangeCommand(ISysCommandParam obj)
        {
            ArrayList recList = obj.SelectedFields;
            if (recList == null || recList.Count == 0)
                throw new LiveCommandItemException(SLanguage.GetString("İşlem Yapılacak kayıt bulunamadı."));

            LiveWaitIndicator.Instance.ShowList();
            BankReceiptBO bankReceiptBo = _container.Resolve<BankReceiptBO>("BankReceiptBO");
            if (bankReceiptBo == null)
            {
                LiveWaitIndicator.Instance.Close();
                throw new LiveCommandItemException(SLanguage.GetString("İşlem tamamlanamadı. Banka fişi data nesnesi oluşturulamadı."));
            }

            BankReceiptPaymentOrderHelper.DisableCoreApprovedReceiptControl(bankReceiptBo);

            string expText = string.Empty;
            for (int index = 0; index < recList.Count; index++)
            {
                if (obj.PmName == "BankReceiptPM")
                {
                    if (bankReceiptBo.Get((long)recList[index]) > 0)
                    {
                        if (!ValidatePaymentOrderGlPeriod(bankReceiptBo))
                        {
                            LiveWaitIndicator.Instance.Close();
                            return;
                        }

                        if (TryHandlePaymentOrderWorkflow(bankReceiptBo, (long)recList[index]))
                            continue;

                        expText = PromptApprovalExplanationIfRequired(expText);

                        if (!BankReceiptPaymentOrderHelper.CanToggleHeaderApproval(bankReceiptBo.CurrentRow.Row))
                        {
                            LiveWaitIndicator.Instance.Close();
                            throw new LiveCommandItemException(PaymentOrderTerminology.HeaderApprovalDeniedMessage);
                        }

                        byte newApproved = 0;
                        byte.TryParse(obj.Tag?.ToString(), out newApproved);
                        BankReceiptPaymentOrderHelper.ApplyHeaderApprovalChange(
                            bankReceiptBo.CurrentRow.Row,
                            newApproved,
                            expText);
                    }

                    if (bankReceiptBo.PostData() != PostResult.Succeed)
                    {
                        LiveWaitIndicator.Instance.Close();
                        if (string.IsNullOrEmpty(bankReceiptBo.ErrorMessage))
                            throw new LiveCommandItemException(SLanguage.GetString("Onay durumu değiştirme işlemi tamamlanamadı."));
                        throw new LiveCommandItemException(SLanguage.GetString($"Onay durumu değiştirme işlemi tamamlanamadı.\nHata :{bankReceiptBo.ErrorMessage}"));
                    }
                }
                else
                {
                    if (!SysMng.Instance.CheckRights(
                            OperationType.Update,
                            (short)obj.BoParamObj.LogicalModuleId,
                            (short)Modules.FinanceModule,
                            (short)BankSecurityItems.BankReceipt,
                            (short)BankReceiptSubItems.Approved))
                    {
                        LiveWaitIndicator.Instance.Close();
                        throw new LiveCommandItemException(SLanguage.GetString("Onay durumu değiştirme yetkiniz bulunmamaktadır."));
                    }

                    object bankReceiptId = CashFlowDbAccess.ExecuteScalar(
                        CashFlowDbContext.FromBusinessObject(bankReceiptBo),
                        $"select BankReceiptId from Erp_BankReceiptItem where RecId={recList[index]}");
                    if (bankReceiptId == null)
                    {
                        LiveWaitIndicator.Instance.Close();
                        return;
                    }

                    if (bankReceiptBo.Get((long)bankReceiptId) > 0)
                    {
                        if (!ValidatePaymentOrderGlPeriod(bankReceiptBo))
                        {
                            LiveWaitIndicator.Instance.Close();
                            return;
                        }

                        byte newApproved = 0;
                        byte.TryParse(obj.Tag?.ToString(), out newApproved);

                        foreach (DataRow itemRow in bankReceiptBo.Data.Tables["Erp_BankReceiptItem"].Select($"RecId={recList[index]}"))
                        {
                            if (newApproved == 0
                                && BankReceiptPaymentOrderHelper.GetPersistedApprovedValue(itemRow) == 1
                                && !BankReceiptPaymentOrderHelper.HasLineApprovedEditRight())
                            {
                                LiveWaitIndicator.Instance.Close();
                                throw new LiveCommandItemException(PaymentOrderTerminology.LineApprovalDeniedMessageAlt);
                            }

                            BankReceiptPaymentOrderHelper.ApplyLineApprovalChange(itemRow, newApproved);
                        }
                    }

                    if (bankReceiptBo.PostData() != PostResult.Succeed)
                    {
                        LiveWaitIndicator.Instance.Close();
                        throw new LiveCommandItemException(SLanguage.GetString("Onay durumu değiştirme işlemi tamamlanamadı. Hata : " + bankReceiptBo.ErrorMessage));
                    }
                }
            }

            LiveWaitIndicator.Instance.Close();
        }

        bool ValidatePaymentOrderGlPeriod(BankReceiptBO bankReceiptBo)
        {
            if (bankReceiptBo.ActiveSession.ParamService.GetParameterClass<GLParameters>().BankReceiptIntegration == 0)
                return true;

            WorkPeriodCheckService workPeriodCheckService = bankReceiptBo.Container.Resolve<WorkPeriodCheckService>();
            if (workPeriodCheckService == null) return true;

            DateTime receiptDate;
            DateTime.TryParse(bankReceiptBo.CurrentRow["ReceiptDate"].ToString(), out receiptDate);
            if (workPeriodCheckService.CheckGLReceiptApprovedPeriod(receiptDate))
                return true;

            SysMng.Instance.ActWndMng.ShowMsg(
                SLanguage.GetString("Entegrasyon fiş tarihi muhasebe hareket dönem tarihi dışında. Bu fiş üzerinde işlem yapamazsınız."),
                ConstantStr.Warning);
            return false;
        }

        bool TryHandlePaymentOrderWorkflow(BankReceiptBO bankReceiptBo, long recId)
        {
            if (!WorkFlowManager.Instance.HasWorkflowDefinition(bankReceiptBo))
                return false;

            ApproveControl approveControl = new ApproveControl(bankReceiptBo);
            LiveWaitIndicator.Instance.Close();
            object result = approveControl.ShowDialog();
            if (result is bool && (bool)result)
                bankReceiptBo.Get(recId);
            return true;
        }

        string PromptApprovalExplanationIfRequired(string currentExplanation)
        {
            if (ActiveSession.ParamService.GetParameterClass<WorkPeriodParameters>().EnterExplanationForApprovalStatusChange != 1)
                return currentExplanation;

            using (AddPublicInputScreen addExplanation = new AddPublicInputScreen())
            {
                SysMng.Instance.ActWndMng.ShowWnd(
                    addExplanation,
                    true,
                    SLanguage.GetString("Açıklama Giriniz"),
                    Common.InformationMessages.WindowStyle.ToolWindow,
                    450,
                    130,
                    Common.InformationMessages.ResizeMode.NoResize,
                    9999,
                    0,
                    false,
                    SizeToContent.Manual);
                return addExplanation.ExplanationText;
            }
        }
    }
}
