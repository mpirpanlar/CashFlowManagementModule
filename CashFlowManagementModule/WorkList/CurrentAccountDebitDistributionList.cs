using System.Collections.Generic;
using System.Linq;
using Sentez.Common.SqlBuilder;
using Reeb.SqlOM;
using Sentez.Common.Report;
using Sentez.Data.Tools;
using Prism.Ioc;
using Sentez.Localization;
using Sentez.Data.MetaData;
using System.Windows;
using System.Data;
using Sentez.Common.Utilities;
using Sentez.FinanceModule.Services;
using Sentez.Core.ParameterClasses;
using System;
using Sentez.Common;
using Sentez.Common.Commands;
using Sentez.Common.Security;
using Sentez.Common.ModuleBase;
using Sentez.Common.SystemServices;
using Sentez.Data.BusinessObjects;
using Sentez.Common.PresentationModels;

namespace Sentez.FinanceModule.Reports
{
    public class CurrentAccountDebitDistributionList : ReportBase
    {
        public static Dictionary<string, string> colonList = new Dictionary<string, string>();
        bool firstFilter = true;
        public CurrentAccountDebitDistributionList(IContainerExtension container)
            : base(container)
        {
            Name = "CurrentAccountDebitDistributionList";
            Title = SLanguage.GetString("Borç Dağılım Listesi");
            WorkMode = ReportWorkMode.Report;
        }

        public override void Init()
        {
            InitBegin();
            InitStatements(null);
            InitEnd();
        }

        public override void InitStatements(object prm)
        {
            bool Aging = (PolicyParam != null && PolicyParam.FieldName.Contains("Aging"));
            CurrentAccountTermInterestStatusOptions iTermInterestStatusOptions = new CurrentAccountTermInterestStatusOptions();

            object reportGroup3 = SelectedOpsItem("OptionsGroup3");
            if (reportGroup3 != null)
            {
                if (((ObjPair)reportGroup3).Value.ToString() == "IsOnlyTL") iTermInterestStatusOptions.IsNotForex = true;
                if (((ObjPair)reportGroup3).Value.ToString() == "IsForex") iTermInterestStatusOptions.IsForex = true;
            }

            iTermInterestStatusOptions.ActualBalance = true;
            iTermInterestStatusOptions.MakeSQL = 2;
            iTermInterestStatusOptions.DebitDistribution = true;
            iTermInterestStatusOptions.DebitDistributionType = 1;
            //iTermInterestStatusOptions.IsForex = IsOpsChecked("IsForex");

            iTermInterestStatusOptions.IsIbanNo = IsOpsChecked("IsIbanNo");
            iTermInterestStatusOptions.IsForexCorrection = IsOpsChecked("IsForexCorrection");
            iTermInterestStatusOptions.IsTaxNo = IsOpsChecked("IsTaxNo");
            iTermInterestStatusOptions.IsGsmPhone = IsOpsChecked("IsGsmPhone");
            iTermInterestStatusOptions.IsAddressPhone = IsOpsChecked("IsAddressPhone");
            iTermInterestStatusOptions.IsAddressFax = IsOpsChecked("IsAddressFax");
            iTermInterestStatusOptions.IsAddressInfo = IsOpsChecked("IsAddressInfo");

            iTermInterestStatusOptions.IsIbanNo = true;
            iTermInterestStatusOptions.IsForexCorrection = true;
            iTermInterestStatusOptions.IsTaxNo = true;
            iTermInterestStatusOptions.IsGsmPhone = true;
            iTermInterestStatusOptions.IsAddressPhone = true;
            iTermInterestStatusOptions.IsAddressFax = true;
            iTermInterestStatusOptions.IsAddressInfo = true;


            if (PolicyParam?.FieldName != null && PolicyParam.FieldName.StartsWith("CExtreList"))
            {
                var tag2 = PolicyParam?.Tag2 as CurrentAccountTermInterestStatusOptions;
                if (tag2 != null)
                {
                    iTermInterestStatusOptions = tag2;
                    iTermInterestStatusOptions.ActualBalance = true;
                    iTermInterestStatusOptions.DebitDistribution = true;
                    iTermInterestStatusOptions.TermInterest = true;
                    iTermInterestStatusOptions.DebitDistributionType = 1;
                    iTermInterestStatusOptions.MakeSQL = 2;
                }
                iTermInterestStatusOptions.WhereStr = "C.RecId = " + PolicyParam.RecordRecId;

            }
            else if (PolicyParam?.FieldName != null
                     && PolicyParam.FieldName.Contains("Aging")
                     && !string.IsNullOrWhiteSpace(PolicyParam.WhereStr))
            {
                iTermInterestStatusOptions.WhereStr = PolicyParam.WhereStr;
            }

            CurrentAccountParameters cp = activeSession.ParamService.GetParameterClass<CurrentAccountParameters>();
            if ((Aging && (cp.FirstRange != 0 || cp.SecondRange != 0)) || (!Aging && (cp.DebitFirstRange != 0 || cp.DebitSecondRange != 0)))
            {
                short firstRange = 0, secondRange = 0, thirdRange = 0, fourthRange = 0, fifthRange = 0, sixthRange = 0, seventhRange = 0, eighthRange = 0, ninthRange = 0, tenthRange = 0;
                firstRange = Aging ? cp.FirstRange : cp.DebitFirstRange;
                secondRange = Aging ? cp.SecondRange : cp.DebitSecondRange;
                thirdRange = Aging ? cp.ThirdRange : cp.DebitThirdRange;
                fourthRange = Aging ? cp.FourthRange : cp.DebitFourthRange;
                fifthRange = Aging ? cp.FifthRange : cp.DebitFifthRange;
                sixthRange = Aging ? cp.SixthRange : cp.DebitSixthRange;
                seventhRange = Aging ? cp.SeventhRange : cp.DebitSeventhRange;
                eighthRange = Aging ? cp.EighthRange : cp.DebitEighthRange;
                ninthRange = Aging ? cp.NinthRange : cp.DebitNinthRange;
                tenthRange = Aging ? cp.TenthRange : cp.DebitTenthRange;

                iTermInterestStatusOptions.DebitDistributionDay = new short[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
                iTermInterestStatusOptions.DebitDistributionDay[0] = firstRange;
                iTermInterestStatusOptions.DebitDistributionDay[1] = secondRange;
                iTermInterestStatusOptions.DebitDistributionDay[2] = thirdRange;
                iTermInterestStatusOptions.DebitDistributionDay[3] = fourthRange;
                iTermInterestStatusOptions.DebitDistributionDay[4] = fifthRange;
                iTermInterestStatusOptions.DebitDistributionDay[5] = sixthRange;
                iTermInterestStatusOptions.DebitDistributionDay[6] = seventhRange;
                iTermInterestStatusOptions.DebitDistributionDay[7] = eighthRange;
                iTermInterestStatusOptions.DebitDistributionDay[8] = ninthRange;
                iTermInterestStatusOptions.DebitDistributionDay[9] = tenthRange;

                int count = tenthRange == 0 ? 9 : 10;
                if (ninthRange == 0) count = 8;
                if (ninthRange == 0 && eighthRange == 0) count = 7;
                if (eighthRange == 0 && seventhRange == 0) count = 6;
                if (seventhRange == 0 && sixthRange == 0) count = 5;
                if (sixthRange == 0 && fifthRange == 0) count = 4;
                if (fifthRange == 0 && fourthRange == 0) count = 3;
                if (fourthRange == 0 && thirdRange == 0) count = 2;
                if (tenthRange != 0) count = 10;

                if (cp.TenthRange < 360)
                {
                    for (int i = count; i < iTermInterestStatusOptions.DebitDistributionDay.Length; i++)
                    {
                        if (iTermInterestStatusOptions.DebitDistributionDay.Length >= i)
                            iTermInterestStatusOptions.DebitDistributionDay[i] = (iTermInterestStatusOptions.DebitDistributionDay[i - 1].ToString().Substring(0, 1) != "-" ? (short)(iTermInterestStatusOptions.DebitDistributionDay[i - 1] + 30) : (short)(iTermInterestStatusOptions.DebitDistributionDay[i - 1] - 30));
                    }
                }
                iTermInterestStatusOptions.DebitDistributionParam = Aging ? true : false;
            }

            if (Aging)
            {
                iTermInterestStatusOptions.DebitDistributionType = -1;
                Title = SLanguage.GetString("Yaşlandırma Listesi");
                //iTermInterestStatusOptions.IsGsmPhone = true;
                if (iTermInterestStatusOptions.IsForex)
                    Title = SLanguage.GetString("Dövizli Yaşlandırma Listesi");
            }

            DateTime debitDistributionDate = new DateHelper().GetToday();
            object reportGroup = SelectedOpsItem("OptionsGroup2");
            if (reportGroup != null) DateTime.TryParse(reportGroup.ToString(), out debitDistributionDate);
            iTermInterestStatusOptions.ReportDate = debitDistributionDate;

            CurrentAccountTermInterestStatusService currentAccountTermInterestStatusService = _container.Resolve<CurrentAccountTermInterestStatusService>("");
            string sqlstr = (string)currentAccountTermInterestStatusService.Execute(iTermInterestStatusOptions);

            Statement _statement1 = new Statement("CurrentAccountDebitDistributionRpr");
            _statement1.UseSchema = false;
            _statement1.UseSqlOm = false;

            _statement1.AddTable("#ReceiptPaymentDetailTmp", "temporary");
            _statement1.AddTable("Erp_CurrentAccount", "C");
            _statement1.AddTable("Erp_CurrentAccountGroup", "CG");
            _statement1.AddTable("Erp_ReceiptPaymentItem", "P");
            _statement1.AddTable("Erp_Employee", "E");
            _statement1.AddTable("Meta_Forex", "MF");
            if (iTermInterestStatusOptions.DebitDistributionType == -1)
                _statement1.AddTable("Erp_TradingGroup", "ETG");
            _statement1.LoadAllFields(false);
            _statement1.AddSql(sqlstr);
            _statement1.SetBaseTable("C");
            _statement1.SetViewTable("temporary");

            _statement1.AddCol("CurrentAccountId", "temporary", "CurrentAccountId", false);
            _statement1.AddCol("CurrentAccountId", "temporary", "RecId", false);
            _statement1.AddCol(SLanguage.GetString("Cari Hesap Kodu"), "temporary", SLanguage.GetString("Cari Hesap Kodu"));
            _statement1.AddCol(SLanguage.GetString("Cari Hesap Adı"), "temporary", SLanguage.GetString("Cari Hesap Adı"));
            _statement1.AddCol(SLanguage.GetString("Cari Hesap"), "temporary", SLanguage.GetString("Cari Hesap"), false);
            _statement1.AddCol(SLanguage.GetString("Ticari Ünvan"), "temporary", SLanguage.GetString("Ticari Ünvan"), false);
            _statement1.AddCol(SLanguage.GetString("Özel Kod"), "temporary", SLanguage.GetString("Özel Kod"), false);
            _statement1.AddCol(SLanguage.GetString("Erişim Kodu"), "temporary", SLanguage.GetString("Erişim Kodu"), false);
            _statement1.AddCol(SLanguage.GetString("Grup Kodu"), "temporary", SLanguage.GetString("Grup Kodu"), false);
            _statement1.AddCol(SLanguage.GetString("Grup Adı"), "temporary", SLanguage.GetString("Grup Adı"), false);
            if (iTermInterestStatusOptions.IsGsmPhone)
                _statement1.AddCol(SLanguage.GetString("Cep Telefonu"), "temporary", SLanguage.GetString("Cep Telefonu"));
            if (iTermInterestStatusOptions.IsAddressPhone)
                _statement1.AddCol(SLanguage.GetString("Adres Telefon"), "temporary", SLanguage.GetString("Adres Telefon"));
            if (iTermInterestStatusOptions.IsAddressFax)
                _statement1.AddCol(SLanguage.GetString("Adres Faks"), "temporary", SLanguage.GetString("Adres Faks"));
            if (iTermInterestStatusOptions.IsAddressInfo)
                _statement1.AddCol(SLanguage.GetString("Adres Bilgisi"), "temporary", SLanguage.GetString("Adres Bilgisi"));
            if (iTermInterestStatusOptions.IsIbanNo)
            {
                _statement1.AddCol(SLanguage.GetString("Iban No"), "temporary", SLanguage.GetString("Iban No"));
                _statement1.AddCol(SLanguage.GetString("Hesap Adı"), "temporary", SLanguage.GetString("Müşteri Hesap Adı"));
                _statement1.AddCol(SLanguage.GetString("Açıklama"), "temporary", SLanguage.GetString("Açıklama"));
            }
            if (iTermInterestStatusOptions.IsTaxNo)
            {
                _statement1.AddCol(SLanguage.GetString("Vergi Kimlik No"), "temporary", SLanguage.GetString("Vergi Kimlik No"));
                _statement1.AddCol(SLanguage.GetString("T.C. Kimlik No"), "temporary", SLanguage.GetString("T.C. Kimlik No"));
            }
            _statement1.AddCol(SLanguage.GetString("Vade Günü"), "temporary", SLanguage.GetString("Vade Günü"), 0, SqlAggregationFunction.None, false, false, FieldUsage.VariantQuantity);
            _statement1.AddCol(SLanguage.GetString("Vade Faiz Oranı"), "temporary", SLanguage.GetString("Vade Faiz Oranı"), 0, SqlAggregationFunction.None, false, false, FieldUsage.Percentage);
            _statement1.AddCol(SLanguage.GetString("Bakiye"), "temporary", SLanguage.GetString("Bakiye"), 0, SqlAggregationFunction.None, true, false, FieldUsage.Amount);
            _statement1.AddCol(SLanguage.GetString("Güncel Bakiye"), "temporary", SLanguage.GetString("Güncel Bakiye"), 0, SqlAggregationFunction.None, true, false, FieldUsage.Amount);
            _statement1.AddCol(SLanguage.GetString("BT"), "temporary", SLanguage.GetString("BT"));
            if (iTermInterestStatusOptions.IsForex)
                _statement1.AddCol(SLanguage.GetString("Döviz"), "temporary", SLanguage.GetString("Döviz"));
            _statement1.AddCol(SLanguage.GetString("Cari Döviz"), "temporary", SLanguage.GetString("Cari Döviz"), false);
            //int firstValue = 0;
            _statement1.AddCol("- 0", "temporary", "- 0", 0, SqlAggregationFunction.None, !iTermInterestStatusOptions.DebitDistributionParam || iTermInterestStatusOptions.DebitDistributionDay[0] == 0, false, FieldUsage.Amount);

            //cari hesap özel alanlar ekleniyor
            foreach (MetaField mf in Schema.Tables["Erp_CurrentAccount"].GetExtensionFields())
            {
                string fName = string.Format("{0}", !string.IsNullOrEmpty(mf.Caption) ? mf.Caption : mf.Name);
                _statement1.AddCol(fName, "temporary", fName, false);
            }

            colonList.Clear();

            if (iTermInterestStatusOptions.DebitDistributionDay.Length > 0)
            {
                if (iTermInterestStatusOptions.DebitDistributionParam && iTermInterestStatusOptions.DebitDistributionDay != null && iTermInterestStatusOptions.DebitDistributionDay.Length > 0 && iTermInterestStatusOptions.DebitDistributionDay[0] != 0)
                {
                    for (int index = 0; index < iTermInterestStatusOptions.DebitDistributionDay.Length; index++)
                    {
                        if (iTermInterestStatusOptions.DebitDistributionDay[index].ToString().Contains("-"))
                        {
                            if (!colonList.ContainsKey(iTermInterestStatusOptions.DebitDistributionDay[index].ToString()))
                            {
                                if (index == 0)
                                {
                                    colonList.Add(index.ToString(), " - " + (iTermInterestStatusOptions.DebitDistributionDay[index] + 1));
                                    _statement1.AddCol($"{index} - {iTermInterestStatusOptions.DebitDistributionDay[index] + 1}", "temporary", $"{index} - {iTermInterestStatusOptions.DebitDistributionDay[index] + 1}", 0, SqlAggregationFunction.None, true, false, FieldUsage.Amount);
                                }
                                else
                                {
                                    colonList.Add(iTermInterestStatusOptions.DebitDistributionDay[index - 1].ToString(), " - " + (iTermInterestStatusOptions.DebitDistributionDay[index] + 1));
                                    _statement1.AddCol($"{iTermInterestStatusOptions.DebitDistributionDay[index - 1]} - {iTermInterestStatusOptions.DebitDistributionDay[index] + 1}", "temporary", $"{iTermInterestStatusOptions.DebitDistributionDay[index - 1]} - {iTermInterestStatusOptions.DebitDistributionDay[index] + 1}", 0, SqlAggregationFunction.None, true, false, FieldUsage.Amount);
                                }
                            }
                        }
                        else if (iTermInterestStatusOptions.DebitDistributionDay[index] == 0)
                        {
                            if (!colonList.ContainsKey(iTermInterestStatusOptions.DebitDistributionDay[index].ToString()))
                            {
                                colonList.Add("-" + iTermInterestStatusOptions.DebitDistributionDay[index].ToString(), (iTermInterestStatusOptions.DebitDistributionDay[index]).ToString());
                                _statement1.AddCol(string.Format("{0}", iTermInterestStatusOptions.DebitDistributionDay[index]), "temporary", string.Format("{0}", iTermInterestStatusOptions.DebitDistributionDay[index]), 0, SqlAggregationFunction.None, true, false, FieldUsage.Amount);
                            }
                        }
                        else
                        {
                            if (index == 0)
                            {
                                colonList.Add(index.ToString(), " - " + (iTermInterestStatusOptions.DebitDistributionDay[index] - 1));
                                _statement1.AddCol($"{index} - {iTermInterestStatusOptions.DebitDistributionDay[index] - 1}", "temporary", $"{index} - {iTermInterestStatusOptions.DebitDistributionDay[index] - 1}", 0, SqlAggregationFunction.None, true, false, FieldUsage.Amount);
                            }
                            else
                            {
                                colonList.Add(iTermInterestStatusOptions.DebitDistributionDay[index - 1].ToString(), " - " + (iTermInterestStatusOptions.DebitDistributionDay[index] - 1));
                                _statement1.AddCol($"{iTermInterestStatusOptions.DebitDistributionDay[index - 1]} - {iTermInterestStatusOptions.DebitDistributionDay[index] - 1}", "temporary", $"{iTermInterestStatusOptions.DebitDistributionDay[index - 1]} - {iTermInterestStatusOptions.DebitDistributionDay[index] - 1}", 0, SqlAggregationFunction.None, true, false, FieldUsage.Amount);
                            }
                        }
                    }
                    colonList.Add(iTermInterestStatusOptions.DebitDistributionDay[iTermInterestStatusOptions.DebitDistributionDay.Length - 1].ToString(), iTermInterestStatusOptions.DebitDistributionDay[iTermInterestStatusOptions.DebitDistributionDay.Length - 1] + " +");
                    _statement1.AddCol($"{iTermInterestStatusOptions.DebitDistributionDay[iTermInterestStatusOptions.DebitDistributionDay.Length - 1]}", "temporary", $"{iTermInterestStatusOptions.DebitDistributionDay[iTermInterestStatusOptions.DebitDistributionDay.Length - 1]} +", 0, SqlAggregationFunction.None, true, false, FieldUsage.Amount);
                    AddParam("colonList", colonList);
                }

                else
                {
                    int firstValue = 0;

                    for (int index = 0; index < iTermInterestStatusOptions.DebitDistributionDay.Length; index++)
                    {

                        //if (iTermInterestStatusOptions.DebitDistributionDay[index] == 0)
                        //    break;

                        if (iTermInterestStatusOptions.DebitDistributionDay[index].ToString().Contains("-"))
                        {
                            if (iTermInterestStatusOptions.DebitDistributionDay[index] == 0) continue;
                            else
                            {
                                colonList.Add(firstValue.ToString(), " - " + (iTermInterestStatusOptions.DebitDistributionDay[index] + 1));
                                _statement1.AddCol($"{firstValue} - {iTermInterestStatusOptions.DebitDistributionDay[index] + 1}", "temporary", $"{firstValue} - {iTermInterestStatusOptions.DebitDistributionDay[index] + 1}", 0, SqlAggregationFunction.None, true, false, FieldUsage.Amount);
                            }
                        }
                        else
                        {
                            if (iTermInterestStatusOptions.DebitDistributionDay[index] == 0) continue;
                            else
                            {
                                colonList.Add(firstValue.ToString(), " - " + (iTermInterestStatusOptions.DebitDistributionDay[index] - 1));
                                _statement1.AddCol($"{firstValue} - {iTermInterestStatusOptions.DebitDistributionDay[index] - 1}", "temporary", $"{firstValue} - {iTermInterestStatusOptions.DebitDistributionDay[index] - 1}", 0, SqlAggregationFunction.None, true, false, FieldUsage.Amount);
                            }
                        }

                        firstValue = iTermInterestStatusOptions.DebitDistributionDay[index];
                        if (index == 11)
                        {
                            colonList.Add(firstValue.ToString(), " +");
                            _statement1.AddCol($"{firstValue} +", "temporary", $"{firstValue} +", 0, SqlAggregationFunction.None, true, false, FieldUsage.Amount);
                        }
                    }
                }
                AddParam("colonList", colonList);
            }
            else
            {
                _statement1.AddCol("0 - 29", "temporary", "0 - 29", 0, SqlAggregationFunction.None, true, false, FieldUsage.Amount);
                _statement1.AddCol("30 - 59", "temporary", "30 - 59", 0, SqlAggregationFunction.None, true, false, FieldUsage.Amount);
                _statement1.AddCol("60 - 89", "temporary", "60 - 89", 0, SqlAggregationFunction.None, true, false, FieldUsage.Amount);
                _statement1.AddCol("90 - 119", "temporary", "90 - 119", 0, SqlAggregationFunction.None, true, false, FieldUsage.Amount);
                _statement1.AddCol("120 - 149", "temporary", "120 - 149", 0, SqlAggregationFunction.None, true, false, FieldUsage.Amount);
                _statement1.AddCol("150 - 179", "temporary", "150 - 179", 0, SqlAggregationFunction.None, true, false, FieldUsage.Amount);
                _statement1.AddCol("180 - 209", "temporary", "180 - 209", 0, SqlAggregationFunction.None, true, false, FieldUsage.Amount);
                _statement1.AddCol("210 - 239", "temporary", "210 - 239", 0, SqlAggregationFunction.None, true, false, FieldUsage.Amount);
                _statement1.AddCol("240 - 269", "temporary", "240 - 269", 0, SqlAggregationFunction.None, true, false, FieldUsage.Amount);
                _statement1.AddCol("270 - 299", "temporary", "270 - 299", 0, SqlAggregationFunction.None, true, false, FieldUsage.Amount);
                _statement1.AddCol("300 - 329", "temporary", "300 - 329", 0, SqlAggregationFunction.None, true, false, FieldUsage.Amount);
                _statement1.AddCol("330 - 359", "temporary", "330 - 359", 0, SqlAggregationFunction.None, true, false, FieldUsage.Amount);
                _statement1.AddCol("360 +", "temporary", "360 +", 0, SqlAggregationFunction.None, true, false, FieldUsage.Amount);
            }


            AddStatement(_statement1);

            //cari hesap listesi üzerinden açılıyor ve sadece o cari ekrana alınıyor
            if (firstFilter && _statement1.filterList != null && _statement1.filterList.Count > 0 && PolicyParam?.FieldName != null && PolicyParam.FieldName.Contains("CurrentAccountList"))
            {
                firstFilter = false;
                DataTable dt = UtilityFunctions.GetDataTableList(activeSession.dbInfo.DBProvider, activeSession.dbInfo.Connection, null, "Erp_CurrentAccount", "select isnull(CurrentAccountCode,'')CurrentAccountCode from Erp_CurrentAccount with (nolock) where RecId = " + PolicyParam.RecordRecId);
                if (dt != null && dt.Rows.Count > 0)
                {
                    var sel = (from FilterItem c in _statement1.filterList where c.filterTable1Alias == "C" && c.field1Name == "CurrentAccountCode" select c).FirstOrDefault();
                    if (sel != null) { sel.valueList[0] = dt.Rows[0]["CurrentAccountCode"].ToString(); sel.valueList[1] = dt.Rows[0]["CurrentAccountCode"].ToString(); }
                }
            }

            ViewStatement = _statement1;
        }

        public override MenuItemPM GetCommands()
        {
            RootMenu = new MenuItemPM();
            SeparatorCmd = new MenuItemPM("-", "");
            BoParam boparam = new BoParam
            {
                Type = 0,
                ValRefs = { ["ActiveRecordId"] = GetActiveRefId },
                LogicalModuleId = (short)Modules.FinanceModule
            };
            BoParam boparam2 = new BoParam
            {
                Type = 0,
                LogicalModuleId = (short)Modules.FinanceModule
            };
            PmParam pmparam = new PmParam("CurrentAccountPM", "BOCardContext");
            OpenCmd = new MenuItemPM("Cari Hesap Kartı (Değiştir)", "CurrentAccountCard")
            {
                MenuItemCommandParam = new SysCommandParam("CurrentAccount", "CurrentAccountPM", pmparam, "CurrentAccountBO", boparam, "", "CurrentAccountId")
                {
                    logicalModuleID = boparam.LogicalModuleId,
                    moduleID = (short)Modules.FinanceModule,
                    secID = (short)FinanceSecurityItems.CurrentAccount,
                    subsecID = (short)CurrentAccountSubItems.None
                },
                ShortcutKey = System.Windows.Input.Key.F4,
                ShortcutKeyModifier = System.Windows.Input.ModifierKeys.Shift
            };
            RootMenu.Children.Add(OpenCmd);
            NewCmd = new MenuItemPM("Cari Hesap Kartı (Yeni)", "CurrentAccountCard")
            {
                MenuItemCommandParam = new SysCommandParam("CurrentAccount", "CurrentAccountPM", pmparam, "CurrentAccountBO", boparam2, "", "")
                {
                    logicalModuleID = boparam2.LogicalModuleId,
                    moduleID = (short)Modules.FinanceModule,
                    secID = (short)FinanceSecurityItems.CurrentAccount,
                    subsecID = (short)CurrentAccountSubItems.None
                },
                ShortcutKey = System.Windows.Input.Key.F4
            };
            RootMenu.Children.Add(NewCmd);
            MenuItemPM DeleteCmd = new MenuItemPM("Cari Hesap Kartı (Sil)", "Delete")
            {
                MenuItemCommandParam = new SysCommandParam("CurrentAccount", "CurrentAccountPM", pmparam, "CurrentAccountBO", boparam, "", "CurrentAccountId")
                {
                    logicalModuleID = boparam.LogicalModuleId,
                    moduleID = (short)Modules.FinanceModule,
                    secID = (short)FinanceSecurityItems.CurrentAccount,
                    subsecID = (short)CurrentAccountSubItems.None
                },
                ShortcutKey = System.Windows.Input.Key.F6
            };
            RootMenu.Children.Add(DeleteCmd);
            RootMenu.Children.Add(SeparatorCmd);
            MenuItemPM extresubitem = new MenuItemPM("Ekstre / Raporlar", "");
            MenuItemPM itmpm1 = new MenuItemPM("Ekstre", "CurrentAccountExtreDetailCommand")
            {
                MenuItemCommandParam = new SysCommandParam("CExtreListW", "CExtreListPM", pmparam, "CurrentAccountBO", boparam, "", "CurrentAccountId")
                {
                    logicalModuleID = boparam.LogicalModuleId,
                    moduleID = (short)Modules.FinanceModule,
                    secID = (short)FinanceSecurityItems.CurrentAccount,
                    subsecID = (short)CurrentAccountSubItems.Extre
                },
                ShortcutKey = System.Windows.Input.Key.E,
                ShortcutKeyModifier = System.Windows.Input.ModifierKeys.Control
            };
            extresubitem.Children.Add(itmpm1);
            extresubitem.Children.Add(SeparatorCmd);
            itmpm1 = new MenuItemPM("Cari Hesap Ekstresi - Rapor", "DefaultReport");
            SysCommandParam extreparam = new SysCommandParam("", "", "", "CurrentAccountExtNewRpr", "Extre", "", "CurrentAccountId");
            itmpm1.MenuItemCommandParam = extreparam;
            itmpm1.MenuItemCommandParam.logicalModuleID = boparam.LogicalModuleId;
            itmpm1.MenuItemCommandParam.moduleID = (short)Modules.FinanceModule;
            itmpm1.MenuItemCommandParam.secID = (short)FinanceSecurityItems.CurrentAccountExtRpr;
            itmpm1.MenuItemCommandParam.subsecID = 0;
            extresubitem.Children.Add(itmpm1);
            itmpm1 = new MenuItemPM("Vade Farkı Ekstresi", "DefaultReport");
            extreparam = new SysCommandParam("", "", "", "CurrentAccountTermInterestRpr", "CurrentAccountList", "", "CurrentAccountId");
            itmpm1.MenuItemCommandParam = extreparam;
            itmpm1.MenuItemCommandParam.logicalModuleID = boparam.LogicalModuleId;
            itmpm1.MenuItemCommandParam.moduleID = (short)Modules.FinanceModule;
            itmpm1.MenuItemCommandParam.secID = (short)FinanceSecurityItems.CurrentAccountTermInterestRpr;
            itmpm1.MenuItemCommandParam.subsecID = 0;
            extresubitem.Children.Add(itmpm1);
            itmpm1 = new MenuItemPM("Ödeme / Tahsilat Programı", "DefaultReport");
            extreparam = new SysCommandParam("", "", "", "CurrentAccountPaymentCollectingRpr", "CurrentAccountList", "", "CurrentAccountId");
            itmpm1.MenuItemCommandParam = extreparam;
            itmpm1.MenuItemCommandParam.logicalModuleID = boparam.LogicalModuleId;
            itmpm1.MenuItemCommandParam.moduleID = (short)Modules.FinanceModule;
            itmpm1.MenuItemCommandParam.secID = (short)FinanceSecurityItems.CurrentAccountPaymentCollectingRpr;
            itmpm1.MenuItemCommandParam.subsecID = 0;
            extresubitem.Children.Add(itmpm1);
            itmpm1 = new MenuItemPM("Özet Adat Raporu", "DefaultReport");
            extreparam = new SysCommandParam("", "", "", "CurrentAccountSummaryTermInterestRpr", "CurrentAccountList", "", "CurrentAccountId");
            itmpm1.MenuItemCommandParam = extreparam;
            itmpm1.MenuItemCommandParam.logicalModuleID = boparam.LogicalModuleId;
            itmpm1.MenuItemCommandParam.moduleID = (short)Modules.FinanceModule;
            itmpm1.MenuItemCommandParam.secID = (short)FinanceSecurityItems.CurrentAccountSummaryTermInterestRpr;
            itmpm1.MenuItemCommandParam.subsecID = 0;
            extresubitem.Children.Add(itmpm1);
            itmpm1 = new MenuItemPM("Borç Dağılım Raporu", "DefaultReport");
            extreparam = new SysCommandParam("", "", "", "CurrentAccountDebitDistributionRpr", "CurrentAccountList", "", "CurrentAccountId");
            itmpm1.MenuItemCommandParam = extreparam;
            itmpm1.MenuItemCommandParam.logicalModuleID = boparam.LogicalModuleId;
            itmpm1.MenuItemCommandParam.moduleID = (short)Modules.FinanceModule;
            itmpm1.MenuItemCommandParam.secID = (short)FinanceSecurityItems.CurrentAccountDebitDistributionRpr;
            itmpm1.MenuItemCommandParam.subsecID = 0;
            extresubitem.Children.Add(itmpm1);
            itmpm1 = new MenuItemPM("Yaşlandırma Raporu", "DefaultReport");
            extreparam = new SysCommandParam("", "", "", "CurrentAccountDebitDistributionRpr", "Aging;CurrentAccountList", "", "CurrentAccountId");
            itmpm1.MenuItemCommandParam = extreparam;
            itmpm1.MenuItemCommandParam.logicalModuleID = boparam.LogicalModuleId;
            itmpm1.MenuItemCommandParam.moduleID = (short)Modules.FinanceModule;
            itmpm1.MenuItemCommandParam.secID = (short)FinanceSecurityItems.CurrentAccountDebitAgingDistributionRpr;
            itmpm1.MenuItemCommandParam.subsecID = 0;
            extresubitem.Children.Add(itmpm1);
            itmpm1 = new MenuItemPM("Cari Hesap Mutabakat Mektubu", "DefaultReport");
            extreparam = new SysCommandParam("", "", "", "CurrentAccountAgreementLetterRpr", "CurrentAccountList", "", "CurrentAccountId");
            itmpm1.MenuItemCommandParam = extreparam;
            itmpm1.MenuItemCommandParam.logicalModuleID = boparam.LogicalModuleId;
            itmpm1.MenuItemCommandParam.moduleID = (short)Modules.FinanceModule;
            itmpm1.MenuItemCommandParam.secID = (short)FinanceSecurityItems.CurrentAccountBalanceRpr;
            itmpm1.MenuItemCommandParam.subsecID = 0;
            itmpm1.MenuItemCommandParam.Tag = "AgreementLetter";
            itmpm1.Tag = "AgreementLetter";
            extresubitem.Children.Add(itmpm1);
            extresubitem.Children.Add(SeparatorCmd);
            itmpm1 = new MenuItemPM("Cari Hesap Kart Analizi", "CmdGeneralOpen");
            extreparam = new SysCommandParam("CurrentAccountAnalysis", "CurrentAccountAnalysisPM", new PmParam("CurrentAccountAnalysisPM"), "CurrentAccountBO", boparam, "", "CurrentAccountId");
            itmpm1.MenuItemCommandParam = extreparam;
            itmpm1.MenuItemCommandParam.logicalModuleID = boparam.LogicalModuleId;
            itmpm1.MenuItemCommandParam.moduleID = (short)Modules.FinanceModule;
            itmpm1.MenuItemCommandParam.secID = (short)FinanceSecurityItems.CurrentAccountDebitAgingDistributionRpr;
            itmpm1.MenuItemCommandParam.subsecID = 0;
            itmpm1.ShortcutKey = System.Windows.Input.Key.K;
            itmpm1.ShortcutKeyModifier = System.Windows.Input.ModifierKeys.Control;
            extresubitem.Children.Add(itmpm1);
            RootMenu.Children.Add(extresubitem);
            if (WorkMode == ReportWorkMode.WorkList)
            {
                string[] typeNameStr;
                RootMenu.Children.Add(SeparatorCmd);
                if (SysMng.Instance.CheckRights(activeSession, Common.OperationType.Select, (short)Modules.CRMModule, (short)Modules.CRMModule, (short)CRMModule.CRMSecurityItems.CustomerTransaction, (short)CRMModule.CRMSecuritySubItems.None))
                {
                    MenuItemPM crmNewActivity = new MenuItemPM("Aktivite Oluştur", "CmdGeneralOpen");
                    BoParam _boParam = new BoParam
                    {
                        Type = activeSession.ActiveUser.DepartmentId ?? 0,
                        ValRefs = { ["SelectedFields"] = GetSelectedFields }
                    };
                    SysCommandParam newObj = new SysCommandParam("CRMCustomerTransaction", "CRMCustomerTransactionPM", "CRMCustomerTransactionPM,BOCardContext", "CRMCustomerTransactionBO", "", "", "")
                    {
                        logicalModuleID = (short)Modules.CRMModule,
                        moduleID = (short)Modules.CRMModule,
                        secID = 11,
                        subsecID = 0,
                        BoParamObj = _boParam
                    };
                    crmNewActivity.MenuItemCommandName = "CTRL+D";
                    crmNewActivity.MenuItemCommandParam = newObj;
                    crmNewActivity.ShortcutKey = System.Windows.Input.Key.D;
                    crmNewActivity.ShortcutKeyModifier = System.Windows.Input.ModifierKeys.Control;
                    RootMenu.Children.Add(crmNewActivity);
                    MenuItemPM crmHistory = new MenuItemPM("Müşteri Hareketleri Tarihçesi", "CmdGeneralOpen");
                    SysCommandParam obj = new SysCommandParam("CustomerTransactionHistory", "CustomerTransactionHistoryPM", "CustomerTransactionHistoryPM", "", "", "", "CurrentAccountId")
                    {
                        logicalModuleID = (short)Modules.CRMModule,
                        moduleID = (short)Modules.CRMModule,
                        secID = (short)CRMModule.CRMSecurityItems.CustomerTransaction,
                        subsecID = (short)CRMModule.CRMSecuritySubItems.None
                    };
                    crmHistory.MenuItemCommandParam = obj;
                    RootMenu.Children.Add(crmHistory);
                }
                MenuItemPM subitem = new MenuItemPM("İşlemler", "");

                #region cari hesap
                MenuItemPM subsubitem = new MenuItemPM("Cari Hesap Hareketleri", "");
                MenuItemPM itmpm2 = new MenuItemPM(SLanguage.GetString("Nakit Ödeme Oluşturma"), "CreateReciptFormCurrentAccount");
                SysCommandParam param = new SysCommandParam("CurrentAccountReceipt", "CurrentAccountReceiptPM", null, "CurrentAccountReceiptBO", new BoParam(1), SLanguage.GetString("Cari Hesap Fişi"), "") { ValRefs = { ["SelectedFields"] = GetSelectedFields } };
                itmpm2.MenuItemServiceName = "CreateCurrentAccountListReceiptService";
                itmpm2.MenuItemCommandParam = param;
                itmpm2.MenuItemCommandParam.logicalModuleID = (short)Modules.FinanceModule;
                itmpm2.MenuItemCommandParam.moduleID = (short)Modules.FinanceModule;
                itmpm2.MenuItemCommandParam.secID = (short)FinanceSecurityItems.CurrentAccountReceipt;
                itmpm2.MenuItemCommandParam.subsecID = 0;
                subsubitem.Children.Add(itmpm2);
                itmpm2 = new MenuItemPM(SLanguage.GetString("Nakit Tahsilat Oluşturma"), "CreateReciptFormCurrentAccount");
                param = new SysCommandParam("CurrentAccountReceipt", "CurrentAccountReceiptPM", null, "CurrentAccountReceiptBO", new BoParam(2), SLanguage.GetString("Cari Hesap Fişi"), "") { ValRefs = { ["SelectedFields"] = GetSelectedFields } };
                itmpm2.MenuItemServiceName = "CreateCurrentAccountListReceiptService";
                itmpm2.MenuItemCommandParam = param;
                itmpm2.MenuItemCommandParam.logicalModuleID = (short)Modules.FinanceModule;
                itmpm2.MenuItemCommandParam.moduleID = (short)Modules.FinanceModule;
                itmpm2.MenuItemCommandParam.secID = (short)FinanceSecurityItems.CurrentAccountReceipt;
                itmpm2.MenuItemCommandParam.subsecID = 0;
                subsubitem.Children.Add(itmpm2);
                itmpm2 = new MenuItemPM(SLanguage.GetString("Borç Dekontu Oluşturma"), "CreateReciptFormCurrentAccount");
                param = new SysCommandParam("CurrentAccountReceipt", "CurrentAccountReceiptPM", null, "CurrentAccountReceiptBO", new BoParam(3), SLanguage.GetString("Cari Hesap Fişi"), "") { ValRefs = { ["SelectedFields"] = GetSelectedFields } };
                itmpm2.MenuItemServiceName = "CreateCurrentAccountListReceiptService";
                itmpm2.MenuItemCommandParam = param;
                itmpm2.MenuItemCommandParam.logicalModuleID = (short)Modules.FinanceModule;
                itmpm2.MenuItemCommandParam.moduleID = (short)Modules.FinanceModule;
                itmpm2.MenuItemCommandParam.secID = (short)FinanceSecurityItems.CurrentAccountReceipt;
                itmpm2.MenuItemCommandParam.subsecID = 0;
                subsubitem.Children.Add(itmpm2);
                itmpm2 = new MenuItemPM(SLanguage.GetString("Alacak Dekontu Oluşturma"), "CreateReciptFormCurrentAccount");
                param = new SysCommandParam("CurrentAccountReceipt", "CurrentAccountReceiptPM", null, "CurrentAccountReceiptBO", new BoParam(4), SLanguage.GetString("Cari Hesap Fişi"), "") { ValRefs = { ["SelectedFields"] = GetSelectedFields } };
                itmpm2.MenuItemServiceName = "CreateCurrentAccountListReceiptService";
                itmpm2.MenuItemCommandParam = param;
                itmpm2.MenuItemCommandParam.logicalModuleID = (short)Modules.FinanceModule;
                itmpm2.MenuItemCommandParam.moduleID = (short)Modules.FinanceModule;
                itmpm2.MenuItemCommandParam.secID = (short)FinanceSecurityItems.CurrentAccountReceipt;
                itmpm2.MenuItemCommandParam.subsecID = 0;
                subsubitem.Children.Add(itmpm2);
                subitem.Children.Add(subsubitem);
                subitem.Children.Add(SeparatorCmd);
                #endregion

                #region çek
                subsubitem = new MenuItemPM(SLanguage.GetString("Çek / Senet Hareketleri"), "");
                itmpm2 = new MenuItemPM(SLanguage.GetString("Çek Giriş Bordrosu Oluşturma"), "CreateReciptFormCurrentAccount");
                param = new SysCommandParam("ChequeReceipt", "ChequeReceiptPM", null, "ChequeReceiptBO", new BoParam(1), SLanguage.GetString("Çek / Senet Bordrosu"), "") { ValRefs = { ["SelectedFields"] = GetSelectedFields } };
                itmpm2.MenuItemServiceName = "CreateCurrentAccountListReceiptService";
                itmpm2.MenuItemCommandParam = param;
                itmpm2.MenuItemCommandParam.logicalModuleID = (short)Modules.FinanceModule;
                itmpm2.MenuItemCommandParam.moduleID = (short)Modules.ChequeModule;
                itmpm2.MenuItemCommandParam.secID = (short)SecurityHelper.GetSecId("ChequeModule", "ChequeSecurityItems", "ChequeReceipt");
                itmpm2.MenuItemCommandParam.subsecID = 0;
                subsubitem.Children.Add(itmpm2);
                itmpm2 = new MenuItemPM(SLanguage.GetString("Senet Giriş Bordrosu Oluşturma"), "CreateReciptFormCurrentAccount");
                param = new SysCommandParam("ChequeReceipt", "ChequeReceiptPM", null, "ChequeReceiptBO", new BoParam(2), SLanguage.GetString("Çek / Senet Bordrosu"), "") { ValRefs = { ["SelectedFields"] = GetSelectedFields } };
                itmpm2.MenuItemServiceName = "CreateCurrentAccountListReceiptService";
                itmpm2.MenuItemCommandParam = param;
                itmpm2.MenuItemCommandParam.logicalModuleID = (short)Modules.FinanceModule;
                itmpm2.MenuItemCommandParam.moduleID = (short)Modules.ChequeModule;
                itmpm2.MenuItemCommandParam.secID = (short)SecurityHelper.GetSecId("ChequeModule", "ChequeSecurityItems", "ChequeReceipt");
                itmpm2.MenuItemCommandParam.subsecID = 0;
                subsubitem.Children.Add(itmpm2);
                itmpm2 = new MenuItemPM(SLanguage.GetString("Çek Çıkış Bordrosu Oluşturma"), "CreateReciptFormCurrentAccount");
                param = new SysCommandParam("ChequeReceipt", "ChequeReceiptPM", null, "ChequeReceiptBO", new BoParam(3), SLanguage.GetString("Çek / Senet Bordrosu"), "") { ValRefs = { ["SelectedFields"] = GetSelectedFields } };
                itmpm2.MenuItemServiceName = "CreateCurrentAccountListReceiptService";
                itmpm2.MenuItemCommandParam = param;
                itmpm2.MenuItemCommandParam.logicalModuleID = (short)Modules.FinanceModule;
                itmpm2.MenuItemCommandParam.moduleID = (short)Modules.ChequeModule;
                itmpm2.MenuItemCommandParam.secID = (short)SecurityHelper.GetSecId("ChequeModule", "ChequeSecurityItems", "ChequeReceipt");
                itmpm2.MenuItemCommandParam.subsecID = 0;
                subsubitem.Children.Add(itmpm2);
                itmpm2 = new MenuItemPM(SLanguage.GetString("Senet Çıkış Bordrosu Oluşturma"), "CreateReciptFormCurrentAccount");
                param = new SysCommandParam("ChequeReceipt", "ChequeReceiptPM", null, "ChequeReceiptBO", new BoParam(6), SLanguage.GetString("Çek / Senet Bordrosu"), "") { ValRefs = { ["SelectedFields"] = GetSelectedFields } };
                itmpm2.MenuItemServiceName = "CreateCurrentAccountListReceiptService";
                itmpm2.MenuItemCommandParam = param;
                itmpm2.MenuItemCommandParam.logicalModuleID = (short)Modules.FinanceModule;
                itmpm2.MenuItemCommandParam.moduleID = (short)Modules.ChequeModule;
                itmpm2.MenuItemCommandParam.secID = (short)SecurityHelper.GetSecId("ChequeModule", "ChequeSecurityItems", "ChequeReceipt");
                itmpm2.MenuItemCommandParam.subsecID = 0;
                subsubitem.Children.Add(itmpm2);
                subitem.Children.Add(subsubitem);
                subitem.Children.Add(SeparatorCmd);
                #endregion

                #region banka
                subsubitem = new MenuItemPM(SLanguage.GetString("Banka Hareketleri"), "");
                itmpm2 = new MenuItemPM(SLanguage.GetString("Gelen Havale Hareketi Oluşturma"), "CreateReciptFormCurrentAccount");
                param = new SysCommandParam("BankReceipt", "BankReceiptPM", null, "BankReceiptBO", new BoParam(3), SLanguage.GetString("Banka Fişi"), "") { ValRefs = { ["SelectedFields"] = GetSelectedFields } };
                itmpm2.MenuItemServiceName = "CreateCurrentAccountListReceiptService";
                itmpm2.MenuItemCommandParam = param;
                itmpm2.MenuItemCommandParam.logicalModuleID = (short)Modules.FinanceModule;
                itmpm2.MenuItemCommandParam.moduleID = (short)Modules.BankModule;
                itmpm2.MenuItemCommandParam.secID = (short)SecurityHelper.GetSecId("BankModule", "BankSecurityItems", "BankReceipt");
                itmpm2.MenuItemCommandParam.subsecID = 0;
                subsubitem.Children.Add(itmpm2);
                itmpm2 = new MenuItemPM(SLanguage.GetString("Gönderilen Havale Hareketi Oluşturma"), "CreateReciptFormCurrentAccount");
                param = new SysCommandParam("BankReceipt", "BankReceiptPM", null, "BankReceiptBO", new BoParam(4), SLanguage.GetString("Banka Fişi"), "") { ValRefs = { ["SelectedFields"] = GetSelectedFields } };
                itmpm2.MenuItemServiceName = "CreateCurrentAccountListReceiptService";
                itmpm2.MenuItemCommandParam = param;
                itmpm2.MenuItemCommandParam.logicalModuleID = (short)Modules.FinanceModule;
                itmpm2.MenuItemCommandParam.moduleID = (short)Modules.BankModule;
                itmpm2.MenuItemCommandParam.secID = (short)SecurityHelper.GetSecId("BankModule", "BankSecurityItems", "BankReceipt");
                itmpm2.MenuItemCommandParam.subsecID = 0;
                subsubitem.Children.Add(itmpm2);
                subitem.Children.Add(subsubitem);
                subitem.Children.Add(SeparatorCmd);
                #endregion

                #region satın alma
                subsubitem = new MenuItemPM(SLanguage.GetString("Satın Alma Hareketleri"), "");
                MenuItemPM subsubsubitem = new MenuItemPM(SLanguage.GetString("Sipariş İşlemleri"), "");
                itmpm2 = new MenuItemPM(SLanguage.GetString("Satın Alma Siparişi Oluşturma"), "CreateReciptFormCurrentAccount");
                param = new SysCommandParam("OrderReceipt", "OrderReceiptPM", null, "OrderReceiptBO", new BoParam(1), SLanguage.GetString("Sipariş Fişi"), "") { ValRefs = { ["SelectedFields"] = GetSelectedFields } };
                itmpm2.MenuItemServiceName = "CreateCurrentAccountListReceiptService";
                itmpm2.MenuItemCommandParam = param;
                itmpm2.MenuItemCommandParam.logicalModuleID = (short)Modules.PurchaseModule;
                itmpm2.MenuItemCommandParam.moduleID = (short)Modules.OrderModule;
                itmpm2.MenuItemCommandParam.secID = SecurityHelper.GetSecId("OrderModule", "OrderSecurityItems", "OrderReceipt");
                itmpm2.MenuItemCommandParam.subsecID = 0;
                subsubsubitem.Children.Add(itmpm2);
                subsubitem.Children.Add(subsubsubitem);
                subsubitem.Children.Add(SeparatorCmd);
                subsubsubitem = new MenuItemPM(SLanguage.GetString("İrsaliye İşlemleri"), "");
                foreach (IType itype in InventoryReceiptType.GetInventoryReceiptListModule((int)ReceiptTypeDefinition.ReceiptModules.Purchase))
                {
                    typeNameStr = itype.TypeName.Split('-');
                    if (typeNameStr.Length > 0)
                        typeNameStr[0] = typeNameStr[1];// + " " + SLanguage.GetString("Oluşturma");
                    //else
                    //    typeNameStr[0] += " " + SLanguage.GetString("Oluşturma");
                    typeNameStr[0] = string.Format(SLanguage.GetString("{0} Oluşturma"), typeNameStr[0]);

                    itmpm2 = new MenuItemPM(typeNameStr[0], "CreateReciptFormCurrentAccount");
                    param = new SysCommandParam("InventoryReceipt", "InventoryReceiptPM", null, "InventoryReceiptBO", new BoParam(itype.Type), SLanguage.GetString("İrsaliye"), "") { ValRefs = { ["SelectedFields"] = GetSelectedFields } };
                    itmpm2.MenuItemServiceName = "CreateCurrentAccountListReceiptService";
                    itmpm2.MenuItemCommandParam = param;
                    itmpm2.MenuItemCommandParam.logicalModuleID = (short)Modules.PurchaseModule;
                    itmpm2.MenuItemCommandParam.moduleID = (short)Modules.InventoryModule;
                    itmpm2.MenuItemCommandParam.secID = SecurityHelper.GetSecId("InventoryModule", "InventorySecurityItems", "InventoryReceipt");
                    itmpm2.MenuItemCommandParam.subsecID = 0;
                    subsubsubitem.Children.Add(itmpm2);
                }
                subsubitem.Children.Add(subsubsubitem);
                subsubitem.Children.Add(SeparatorCmd);
                subsubsubitem = new MenuItemPM(SLanguage.GetString("Fatura İşlemleri"), "");
                foreach (IType itype in InvoiceReceiptType.GetInvoiceReceiptListModule((int)ReceiptTypeDefinition.ReceiptModules.Purchase))
                {
                    typeNameStr = itype.TypeName.Split('-');
                    if (typeNameStr.Length > 0)
                        typeNameStr[0] = typeNameStr[1];// +" " + SLanguage.GetString("Oluşturma");
                    //else
                    //    typeNameStr[0] += " " + SLanguage.GetString("Oluşturma");
                    typeNameStr[0] = string.Format(SLanguage.GetString("{0} Oluşturma"), typeNameStr[0]);
                    itmpm2 = new MenuItemPM(typeNameStr[0], "CreateReciptFormCurrentAccount");
                    param = new SysCommandParam("Invoice", "InvoicePM", null, "InvoiceBO", new BoParam(itype.Type), SLanguage.GetString("Fatura"), "") { ValRefs = { ["SelectedFields"] = GetSelectedFields } };
                    itmpm2.MenuItemServiceName = "CreateCurrentAccountListReceiptService";
                    itmpm2.MenuItemCommandParam = param;
                    itmpm2.MenuItemCommandParam.logicalModuleID = (short)Modules.PurchaseModule;
                    itmpm2.MenuItemCommandParam.moduleID = (short)Modules.InvoiceModule;
                    itmpm2.MenuItemCommandParam.secID = SecurityHelper.GetSecId("InvoiceModule", "InvoiceSecurityItems", "Invoice");
                    itmpm2.MenuItemCommandParam.subsecID = 0;
                    subsubsubitem.Children.Add(itmpm2);
                }
                subsubitem.Children.Add(subsubsubitem);
                subitem.Children.Add(subsubitem);
                subitem.Children.Add(SeparatorCmd);
                #endregion

                #region satış
                subsubitem = new MenuItemPM(SLanguage.GetString("Satış Hareketleri"), "");
                subsubsubitem = new MenuItemPM(SLanguage.GetString("Sipariş İşlemleri"), "");
                itmpm2 = new MenuItemPM(SLanguage.GetString("Müşteri Siparişi Oluşturma"), "CreateReciptFormCurrentAccount");
                param = new SysCommandParam("OrderReceipt", "OrderReceiptPM", null, "OrderReceiptBO", new BoParam(2), SLanguage.GetString("Sipariş Fişi"), "") { ValRefs = { ["SelectedFields"] = GetSelectedFields } };
                itmpm2.MenuItemServiceName = "CreateCurrentAccountListReceiptService";
                itmpm2.MenuItemCommandParam = param;
                itmpm2.MenuItemCommandParam.logicalModuleID = (short)Modules.PurchaseModule;
                itmpm2.MenuItemCommandParam.moduleID = (short)Modules.OrderModule;
                itmpm2.MenuItemCommandParam.secID = SecurityHelper.GetSecId("OrderModule", "OrderSecurityItems", "OrderReceipt");
                itmpm2.MenuItemCommandParam.subsecID = 0;
                subsubsubitem.Children.Add(itmpm2);
                subsubitem.Children.Add(subsubsubitem);
                subsubitem.Children.Add(SeparatorCmd);
                subsubsubitem = new MenuItemPM(SLanguage.GetString("İrsaliye İşlemleri"), "");

                foreach (IType itype in InventoryReceiptType.GetInventoryReceiptListModule((int)ReceiptTypeDefinition.ReceiptModules.Sales))
                {
                    typeNameStr = itype.TypeName.Split('-');
                    if (typeNameStr.Length > 0)
                        typeNameStr[0] = typeNameStr[1];// + " " + SLanguage.GetString("Oluşturma");
                    //else
                    //    typeNameStr[0] += " " + SLanguage.GetString("Oluşturma");
                    typeNameStr[0] = string.Format(SLanguage.GetString("{0} Oluşturma"), typeNameStr[0]);
                    itmpm2 = new MenuItemPM(typeNameStr[0], "CreateReciptFormCurrentAccount");
                    param = new SysCommandParam("InventoryReceipt", "InventoryReceiptPM", null, "InventoryReceiptBO", new BoParam(itype.Type), SLanguage.GetString("İrsaliye"), "") { ValRefs = { ["SelectedFields"] = GetSelectedFields } };
                    itmpm2.MenuItemServiceName = "CreateCurrentAccountListReceiptService";
                    itmpm2.MenuItemCommandParam = param;
                    itmpm2.MenuItemCommandParam.logicalModuleID = (short)Modules.PurchaseModule;
                    itmpm2.MenuItemCommandParam.moduleID = (short)Modules.InventoryModule;
                    itmpm2.MenuItemCommandParam.secID = SecurityHelper.GetSecId("InventoryModule", "InventorySecurityItems", "InventoryReceipt");
                    itmpm2.MenuItemCommandParam.subsecID = 0;
                    subsubsubitem.Children.Add(itmpm2);
                }
                subsubitem.Children.Add(subsubsubitem);
                subsubitem.Children.Add(SeparatorCmd);
                subsubsubitem = new MenuItemPM(SLanguage.GetString("Fatura İşlemleri"), "");
                foreach (IType itype in InvoiceReceiptType.GetInvoiceReceiptListModule((int)ReceiptTypeDefinition.ReceiptModules.Sales))
                {
                    typeNameStr = itype.TypeName.Split('-');
                    if (typeNameStr.Length > 0)
                        typeNameStr[0] = typeNameStr[1];// + " " + SLanguage.GetString("Oluşturma");
                    //else
                    //    typeNameStr[0] += " " + SLanguage.GetString("Oluşturma");
                    typeNameStr[0] = string.Format(SLanguage.GetString("{0} Oluşturma"), typeNameStr[0]);

                    itmpm2 = new MenuItemPM(typeNameStr[0], "CreateReciptFormCurrentAccount");
                    param = new SysCommandParam("Invoice", "InvoicePM", null, "InvoiceBO", new BoParam(itype.Type), SLanguage.GetString("Fatura"), "") { ValRefs = { ["SelectedFields"] = GetSelectedFields } };
                    itmpm2.MenuItemServiceName = "CreateCurrentAccountListReceiptService";
                    itmpm2.MenuItemCommandParam = param;
                    itmpm2.MenuItemCommandParam.logicalModuleID = (short)Modules.PurchaseModule;
                    itmpm2.MenuItemCommandParam.moduleID = (short)Modules.InvoiceModule;
                    itmpm2.MenuItemCommandParam.secID = SecurityHelper.GetSecId("InvoiceModule", "InvoiceSecurityItems", "Invoice");
                    itmpm2.MenuItemCommandParam.subsecID = 0;
                    subsubsubitem.Children.Add(itmpm2);
                }
                subsubitem.Children.Add(subsubsubitem);
                subitem.Children.Add(subsubitem);
                #endregion
                subitem.Children.Add(SeparatorCmd);
                itmpm2 = new MenuItemPM("Sadakat Kart Oluşturma", "CreateLoyalityCardCmd");
                param = new SysCommandParam("CurrentAccount", "CurrentAccountPM", pmparam, "CurrentAccountBO", boparam, "", "CurrentAccountId") { ValRefs = { ["SelectedFields"] = GetSelectedFields } };
                itmpm2.MenuItemCommandParam = param;
                itmpm2.MenuItemCommandParam = param;
                itmpm2.MenuItemCommandParam.logicalModuleID = (short)Modules.FinanceModule;
                itmpm2.MenuItemCommandParam.moduleID = (short)Modules.FinanceModule;
                itmpm2.MenuItemCommandParam.secID = (short)FinanceSecurityItems.CreateLoyalityCard;
                itmpm2.MenuItemCommandParam.subsecID = 0;
                subitem.Children.Add(itmpm2);
                if (activeSession?.ActiveCompany != null && activeSession.ActiveCompany.IsEInvoice == 1 && SysMng.Instance.CheckRights(Common.OperationType.Select, (short)Modules.FinanceModule, (short)Modules.FinanceModule, (short)FinanceSecurityItems.CurrentAccount, (short)CurrentAccountSubItems.eInvoiceControl))
                {
                    subitem.Children.Add(SeparatorCmd);
                    MenuItemPM einvocepm = new MenuItemPM("e-Fatura Kullanıcı Kontrolü", "eInvoiceControlCommand")
                    {
                        MenuItemCommandParam = new SysCommandParam
                        {
                            logicalModuleID = boparam.LogicalModuleId,
                            moduleID = (short)Modules.FinanceModule,
                            secID = (short)FinanceSecurityItems.CurrentAccount,
                            subsecID = (short)CurrentAccountSubItems.eInvoiceControl
                        }
                    };
                    subitem.Children.Add(einvocepm);
                }
                if (activeSession?.ActiveCompany != null && activeSession.ActiveCompany.IsEInvoice == 1 && activeSession.ActiveCompany.IsEDespatch == 1 && SysMng.Instance.CheckRights(Common.OperationType.Select, (short)Modules.FinanceModule, (short)Modules.FinanceModule, (short)FinanceSecurityItems.CurrentAccount, (short)CurrentAccountSubItems.eDespatchControl))
                {
                    subitem.Children.Add(SeparatorCmd);
                    MenuItemPM edespatchpm = new MenuItemPM("e-İrsaliye Kullanıcı Kontrolü", "eDespatchControlCommand")
                    {
                        MenuItemCommandParam = new SysCommandParam
                        {
                            logicalModuleID = boparam.LogicalModuleId,
                            moduleID = (short)Modules.FinanceModule,
                            secID = (short)FinanceSecurityItems.CurrentAccount,
                            subsecID = (short)CurrentAccountSubItems.eDespatchControl
                        }
                    };
                    subitem.Children.Add(edespatchpm);
                }
                RootMenu.Children.Add(subitem);
            }
            RootMenu.Children.Add(SeparatorCmd);
            itmpm1 = new MenuItemPM("Kopyalama", "CopyToOtherCompanyCommand")
            {
                MenuItemCommandParam = new SysCommandParam("", null, "CurrentAccountBO", "CurrentAccountId"),
                ShortcutKey = System.Windows.Input.Key.F8
            };
            RootMenu.Children.Add(itmpm1);
            /*
            MenuItemPM itmpm2 = new MenuItemPM("Kod Değiştirme", "CardChangeCommand");
            itmpm2.MenuItemCommandParam = new SysCommandParam("", null, "CurrentAccountBO", "CurrentAccountId");
            itmpm2.ShortcutKey = System.Windows.Input.Key.F8;
            itmpm2.ShortcutKeyModifier = System.Windows.Input.ModifierKeys.Shift;
            RootMenu.Children.Add(itmpm2);
            */
            RootMenu.Children.Add(SeparatorCmd);
            MenuItemPM invoicePaymentCmd = new MenuItemPM(SLanguage.GetString("Fatura Ödeme / Kapatma İşlemleri"), "CurrentAccountInvoicePayment")
            {
                MenuItemCommandParam = new SysCommandParam("CurrentAccount", "CurrentAccountPM", pmparam, "CurrentAccountBO", boparam, "", "CurrentAccountId")
                {
                    logicalModuleID = (short)Modules.SalesModule,
                    moduleID = (short)Modules.InvoiceModule,
                    secID = SecurityHelper.GetSecId("InvoiceModule", "InvoiceSecurityItems", "Invoice"),
                    subsecID = SecurityHelper.GetSecId("InvoiceModule", "InvoiceSubItems", "InvoicePayment")
                },
                ShortcutKey = System.Windows.Input.Key.F,
                ShortcutKeyModifier = System.Windows.Input.ModifierKeys.Alt
            };
            RootMenu.Children.Add(invoicePaymentCmd);
            //PmParam pmparam = new PmParam("CurrentAccountPM", "BOCardContext");
            //if (PolicyParam != null && PolicyParam.FieldName == "DealerCode") pmparam.Tag = "Dealer";
            MenuItemPM abalienateCloseCmd = new MenuItemPM(SLanguage.GetString("Temlik Kapatma İşlemleri"), "CurrentAccountInvoicePayment")
            {
                MenuItemCommandParam = new SysCommandParam("CurrentAccount", "CurrentAccountPM", new PmParam("CurrentAccountPM", "BOCardContext") { TagStr = "AlienationClose" }, "CurrentAccountBO", boparam, "", "CurrentAccountId")
                {
                    logicalModuleID = (short)Modules.SalesModule,
                    moduleID = (short)Modules.InvoiceModule,
                    secID = SecurityHelper.GetSecId("InvoiceModule", "InvoiceSecurityItems", "Invoice"),
                    subsecID = SecurityHelper.GetSecId("InvoiceModule", "InvoiceSubItems", "InvoicePayment")
                },
                ShortcutKey = System.Windows.Input.Key.T,
                ShortcutKeyModifier = System.Windows.Input.ModifierKeys.Alt
            };
            RootMenu.Children.Add(abalienateCloseCmd);
            RootMenu.Children.Add(SeparatorCmd);
            //if (activeSession.ActiveCompany.IsEInvoice == 1)
            //{
            //    RootMenu.Children.Add(new MenuItemPM(SLanguage.GetString("e-Fatura Kayıtlı Kullanıcı Güncelleme"), "UpdateEInvoiceCurrentAccount"));
            //    RootMenu.Children.Add(SeparatorCmd);
            //}
            MenuItemPM ListCmd = new MenuItemPM("Liste", "ListCommand") { ShortcutKey = System.Windows.Input.Key.F9 };
            RootMenu.Children.Add(ListCmd);

            return RootMenu;
        }

        public override void LoadOptions()
        {
            base.LoadOptions();
            RepOps ops1 = AddOptionGroup("OptionsGroup1", "Seçenekler", true, EditorType.CheckBox);
            //ops1.AddOption("IsForex", "Hareket Döviz", Visibility.Visible);
            //ops1 = AddOptionGroup("OptionsGroup1", "Seçenekler", true, EditorType.CheckBox);
            ops1.AddOption("IsIbanNo", "Iban No", Visibility.Visible);
            ops1.AddOption("IsTaxNo", "Vergi Kimlik No", Visibility.Visible);
            ops1.AddOption("IsForexCorrection", "Kur Farkı Hareketleri Dahil", Visibility.Visible).IsChecked = true;

            RepOps ops3 = AddOptionGroup("OptionsGroup3", "Döviz Seçenekleri", false, EditorType.ComboBox);
            ops3.AddItem("IsCurrentBalance", "Güncel Bakiye"); //dövizli hareketlerinde TL değerleri için
            ops3.AddItem("IsForex", "Hareket Döviz"); //döviz filtresinde girilen değerlere göre raporun dövizli gelmesi sağlanacaktır
            ops3.AddItem("IsOnlyTL", "Sadece Yerel Para Birimi"); //sadece TL hareketler için

            RepOps ops2 = AddOptionGroup("OptionsGroup2", "Rapor Tarihi", false, EditorType.DateEditor);
            ops2.selectedItem = new DateHelper().GetToday();

            RepOps ops44 = AddOptionGroup("OptionsGroup4", "İletişim Bilgileri", true, EditorType.CheckBox);
            ops44.AddOption("IsGsmPhone", "Cep Telefonu", Visibility.Visible);
            ops44.AddOption("IsAddressPhone", "Adres Telefon", Visibility.Visible);
            ops44.AddOption("IsAddressFax", "Adres Faks", Visibility.Visible);
            ops44.AddOption("IsAddressInfo", "Adres Detayı", Visibility.Visible);

            RepOps ops50 = AddOptionGroup("OptionsGroup5", "Aralık Bilgileri", true, EditorType.CheckBox);

            /*
            CurrentAccountParam.FirstRange}"/
            CurrentAccountParam.SecondRange}"
            CurrentAccountParam.ThirdRange}"/
            CurrentAccountParam.FourthRange}"
            CurrentAccountParam.FifthRange}"/
            CurrentAccountParam.SixthRange}"/
            CurrentAccountParam.SeventhRange}
            CurrentAccountParam.EighthRange}"
            CurrentAccountParam.NinthRange}"/
            CurrentAccountParam.TenthRange}"/
             */

            ops50.AddOption("FirstRange", "1. Aralık", Visibility.Visible);
            ops50.AddOption("SecondRange", "2. Aralık", Visibility.Visible);
            ops50.AddOption("ThirdRange", "3. Aralık", Visibility.Visible);
            ops50.AddOption("FourthRange", "4. Aralık", Visibility.Visible);
            ops50.AddOption("FifthRange", "5. Aralık", Visibility.Visible);
            ops50.AddOption("SixthRange", "6. Aralık", Visibility.Visible);
            ops50.AddOption("SeventhRange", "7. Aralık", Visibility.Visible);
            ops50.AddOption("EighthRange", "8. Aralık", Visibility.Visible);
            ops50.AddOption("NinthRange", "9. Aralık", Visibility.Visible);
            ops50.AddOption("TenthRange", "10. Aralık", Visibility.Visible);
        }

        public override void AfterCreateDataset()
        {
            base.AfterCreateDataset();
            if (Data.Tables.Count > 0)
            {
                if (Data.Tables[0].Rows.Count > 0)
                {

                }
            }
        }
    }
}
