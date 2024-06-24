using System;
using System.Windows;
using Prism.Ioc;

using Sentez.Common.PresentationModels;
using Sentez.Common.SystemServices;
using Sentez.Common.Utilities;
using Sentez.Localization;

namespace Sentez.CRSUIModule.PresentationModels
{
    public class CrsResourceAttributeSetPM : PMDesktop
    {
        public LookupList Lists { get; set; }

        public CrsResourceAttributeSetPM(IContainerExtension container_)
            : base(container_)
        {
        }
        public override void Init()
        {
            base.Init();
            if (ActiveBO != null)
            {
                if (_pmParam.itemID > 0)
                {
                    ActiveBO.Get(_pmParam.itemID);
                    if (ActiveBO.Data.Tables[ActiveBO.BaseTable].Columns.Contains("ResourceType") && ActiveBO.Data.Tables[ActiveBO.BaseTable].Rows.Count > 0)
                    {
                        if (Convert.ToInt64(ActiveBO.Data.Tables[ActiveBO.BaseTable].Rows[0]["ResourceType"]) == (int)ResourceTypeDefinition.ResourceType.RoomType)
                            PmTitle = SLanguage.GetString("Oda Tipi Kartı");
                        else if (Convert.ToInt64(ActiveBO.Data.Tables[ActiveBO.BaseTable].Rows[0]["ResourceType"]) == (int)ResourceTypeDefinition.ResourceType.Room)
                            PmTitle = SLanguage.GetString("Oda Özellik Setleri");
                        else if (Convert.ToInt64(ActiveBO.Data.Tables[ActiveBO.BaseTable].Rows[0]["ResourceType"]) == (int)ResourceTypeDefinition.ResourceType.Hall)
                            PmTitle = SLanguage.GetString("Salon Özellik Setleri");
                    }
                }
                else
                {
                    if (boParam != null && boParam._type == (int)ResourceTypeDefinition.ResourceType.RoomType)
                        PmTitle = SLanguage.GetString("Oda Tipi Kartı");
                    if (!ActiveBO.IsNewRecord) ActiveBO.NewRecord();
                }
            }
        }
        public override void Dispose()
        {
            if (disposed)
                return;
            Lists?.Dispose();
            Lists = null;
            base.Dispose();
        }
    }
}
