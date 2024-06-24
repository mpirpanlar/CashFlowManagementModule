using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Sentez.Common.Utilities;
using Sentez.Data.BusinessObjects;
using Sentez.Data.Tools;
using Sentez.Localization;
using Sentez.Common.Commands;
using Prism.Ioc;

namespace Sentez.CRSUIModule.Views
{
    /// <summary>
    /// Interaction logic for CrsResourceProduce.xaml
    /// </summary>
    public partial class CrsResourceProduce : UserControl
    {
        public IBusinessObject ResourceBo { get; set; }
        private IBusinessObject _newResourceBo;
        public LookupList Lists { get; set; }
        public int ProduceCount { get; set; }
        public string ResourceTemplate { get; set; }
        public CrsResourceProduce()
        {
            InitializeComponent();
        }
        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            //if (ResourceBo != null)
            //{
            //    Lists = ResourceBo.ActiveSession.LookupList.GetChild(UtilityFunctions.GetConnection(ResourceBo.ActiveSession.dbInfo.DBProvider, ResourceBo.ActiveSession.dbInfo.ConnectionString));
            //    Lists.AddLookupList("ChequePeriodList", "Display", typeof(string), new object[] { SLanguage.GetString("Gün"), SLanguage.GetString("Hafta"), SLanguage.GetString("Ay"), SLanguage.GetString("Yıl") }, "Value", typeof(byte), new object[] { (byte)1, (byte)2, (byte)3, (byte)4 });
            //}

            DataContext = this;
            ProduceCount = 1;
            if (ResourceBo?.CurrentRow != null)
            {
                ResourceTemplate = ResourceBo.CurrentRow["ResourceCode"].ToString().Substring(0, 1);
                string tmptStr = string.Empty;
                for (int i = 0; i < ResourceBo.CurrentRow["ResourceCode"].ToString().Length - 1; i++)
                {
                    tmptStr += "#";
                }
                ResourceTemplate += tmptStr;
            }
        }
        private void btnOk_Click(object sender, RoutedEventArgs e)
        {
            _newResourceBo = SysMng.Instance._container.Resolve<IBusinessObject>("CrsResourceBO");
            if (_newResourceBo != null)
            {
                try
                {
                    _newResourceBo.Init(new BoParam(5));
                    for (int i = 0; i < ProduceCount; i++)
                    {
                        _newResourceBo.CodeGenerator.TemplateString = ResourceTemplate;
                        _newResourceBo.CodeGenerator.AllowEmptyTemplate = true;
                        _newResourceBo.CodeGenerator.RegenerateOnPost = true;
                        _newResourceBo.CodeGenerator.GenerateOnNewRow = true;
                        _newResourceBo.NewRecord();
                        _newResourceBo.CurrentRow["WorkplaceId"] = ResourceBo.CurrentRow["WorkplaceId"];
                        _newResourceBo.CurrentRow["ParentResourceId"] = ResourceBo.CurrentRow["ParentResourceId"];
                        _newResourceBo.CurrentRow["DepartmentId"] = ResourceBo.CurrentRow["DepartmentId"];
                        _newResourceBo.CurrentRow["EmployeeId"] = ResourceBo.CurrentRow["EmployeeId"];
                        _newResourceBo.CurrentRow["VehicleId"] = ResourceBo.CurrentRow["VehicleId"];
                        _newResourceBo.CurrentRow["ServiceId"] = ResourceBo.CurrentRow["ServiceId"];
                        _newResourceBo.CurrentRow["InventoryId"] = ResourceBo.CurrentRow["InventoryId"];
                        _newResourceBo.CurrentRow["Explanation"] = _newResourceBo.CurrentRow["ResourceCode"];//ResourceBo.CurrentRow["Explanation"];
                        _newResourceBo.CurrentRow["Capacity"] = ResourceBo.CurrentRow["Capacity"];
                        _newResourceBo.CurrentRow["ExtraCapacity"] = ResourceBo.CurrentRow["ExtraCapacity"];
                        _newResourceBo.CurrentRow["Flote"] = ResourceBo.CurrentRow["Flote"];
                        _newResourceBo.CurrentRow["IsOwned"] = ResourceBo.CurrentRow["IsOwned"];
                        _newResourceBo.CurrentRow["SupplierId"] = ResourceBo.CurrentRow["SupplierId"];
                        _newResourceBo.CurrentRow["SymbolId"] = ResourceBo.CurrentRow["SymbolId"];
                        _newResourceBo.CurrentRow["ProcessId"] = ResourceBo.CurrentRow["ProcessId"];
                        _newResourceBo.CurrentRow["Rpm"] = ResourceBo.CurrentRow["Rpm"];
                        _newResourceBo.CurrentRow["LoopCount"] = ResourceBo.CurrentRow["LoopCount"];
                        _newResourceBo.CurrentRow["AutomationCode"] = ResourceBo.CurrentRow["AutomationCode"];
                        _newResourceBo.CurrentRow["PlanningWindow"] = ResourceBo.CurrentRow["PlanningWindow"];
                        _newResourceBo.CurrentRow["DailyWorkingTime"] = ResourceBo.CurrentRow["DailyWorkingTime"];
                        _newResourceBo.CurrentRow["LocationBuilding"] = ResourceBo.CurrentRow["LocationBuilding"];
                        _newResourceBo.CurrentRow["LocationFloor"] = ResourceBo.CurrentRow["LocationFloor"];
                        _newResourceBo.CurrentRow["LocationAisle"] = ResourceBo.CurrentRow["LocationAisle"];
                        _newResourceBo.CurrentRow["SpecialCode"] = ResourceBo.CurrentRow["SpecialCode"];
                        _newResourceBo.CurrentRow["AccessCode"] = ResourceBo.CurrentRow["AccessCode"];
                        _newResourceBo.CurrentRow["ProxyNo"] = ResourceBo.CurrentRow["ProxyNo"];
                        _newResourceBo.CurrentRow["PhoneNumbers"] = ResourceBo.CurrentRow["PhoneNumbers"];
                        _newResourceBo.CurrentRow["LayoutTypeId"] = ResourceBo.CurrentRow["LayoutTypeId"];
                        _newResourceBo.CurrentRow["IsAvailable"] = ResourceBo.CurrentRow["IsAvailable"];
                        _newResourceBo.CurrentRow["BedTypeId"] = ResourceBo.CurrentRow["BedTypeId"];
                        _newResourceBo.CurrentRow["CleaningDay"] = ResourceBo.CurrentRow["CleaningDay"];
                        _newResourceBo.CurrentRow["ChangingDay"] = ResourceBo.CurrentRow["ChangingDay"];
                        _newResourceBo.CurrentRow["CompetencyPoints"] = ResourceBo.CurrentRow["CompetencyPoints"];
                        _newResourceBo.CurrentRow["CheckActiveManufacturing"] = ResourceBo.CurrentRow["CheckActiveManufacturing"];
                        if (_newResourceBo.PostData() != PostResult.Succeed)
                        {
                            SysMng.Instance.ActWndMng.ShowMsg(string.Format("Toplu oda tanımlama işlemi !!! TAMAMLANAMADI !!! Gerçekleşen Hata :{0}", _newResourceBo.ErrorMessage), ConstantStr.Warning, Common.InformationMessages.MessageBoxButton.OK, Common.InformationMessages.MessageBoxImage.Error);
                            break;
                        }
                        //_newResourceBo.Dispose();
                        //SysMng.Instance.ActWndMng.ShowMsg("Toplu oda tanımlama işlemi tamamlandı!", ConstantStr.Information, Common.InformationMessages.MessageBoxButton.OK, Common.InformationMessages.MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    _newResourceBo?.Dispose();
                    SysMng.Instance.ActWndMng.ShowMsg(string.Format("Toplu oda tanımlama işlemi !!! TAMAMLANAMADI !!! Gerçekleşen Hata :{0}", ex.Message), ConstantStr.Warning, Common.InformationMessages.MessageBoxButton.OK, Common.InformationMessages.MessageBoxImage.Error);
                }
                finally
                {
                    SysMng.Instance.ActWndMng.ShowMsg("Toplu oda tanımlama işlemi tamamlandı!", ConstantStr.Information, Common.InformationMessages.MessageBoxButton.OK, Common.InformationMessages.MessageBoxImage.Information);
                }
            }
            var window = Parent as Window;
            window?.Close();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            var window = Parent as Window;
            window?.Close();
        }
    }
}
