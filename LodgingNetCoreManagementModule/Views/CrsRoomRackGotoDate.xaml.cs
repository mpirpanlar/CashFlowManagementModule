using System;
using System.Collections.Generic;
using System.Data;
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
using Sentez.Common.SystemServices;
using System.ComponentModel;
using Prism.Ioc;

namespace Sentez.CRSUIModule.Views
{
    /// <summary>
    /// Interaction logic for CrsResourceProduce.xaml
    /// </summary>
    public partial class CrsRoomRackGotoDate : UserControl
    {
        public DateTime RoomRackDate { get; set; }
        ISystemService service { get; set; }
        IContainerExtension container { get; set; }

        public bool IsDateOk;
        public CrsRoomRackGotoDate(IContainerExtension co)
        {
            container = co;
            InitializeComponent();
        }
        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            service = container.Resolve<ISystemService>("GetAgileGlobalTodayDateHelperService");
            DataContext = this;
            DateTime eofDate = (DateTime)service.Execute(null);
            RoomRackDate = eofDate;
        }
        private void btnOk_Click(object sender, RoutedEventArgs e)
        {
            IsDateOk = true;
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
