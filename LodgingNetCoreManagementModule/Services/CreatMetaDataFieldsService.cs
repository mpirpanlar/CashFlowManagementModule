using Prism.Ioc;

using Sentez.ApplicationTools;
using Sentez.Common.Commands;
using Sentez.Common.SystemServices;
using Sentez.Data.MetaData;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LodgingNetCoreManagementModule.Services
{
    public class CreatMetaDataFieldsService :SystemServiceBase
    {
        public CreatMetaDataFieldsService(SysMng smgr, Guid sid) : base(smgr, sid) { }

        public override object Execute(object input)
        {
            return base.Execute(input);
        }

        public override object Execute(params object[] inputs)
        {
            return base.Execute(inputs);
        }

        /// <summary>
        /// Projede kullanılacak olan Kullanıcı Tanımlı alanları oluşturan ana metod
        /// </summary>
        /// <param name="tableName">
        /// Tablo adı
        /// </param>
        /// <param name="fieldName">
        /// Alan adı
        /// </param>
        /// <param name="caption">
        /// Alan başlığı
        /// </param>
        /// <param name="udtType">
        /// Alan tipi
        /// </param>
        /// <param name="fieldUsage">
        /// Alan kullanım şekli
        /// </param>
        /// <param name="editorType">
        /// Editör tipi
        /// </param>
        /// <param name="valueInputMethod">
        /// Veri giriş şekli (Free, SelectList vb.)
        /// </param>
        /// <param name="multiLine">
        /// Çoklu satır veri girişi var mı
        /// </param>
        public static void CreatMetaDataFields(string tableName, string fieldName, string caption, byte udtType, byte fieldUsage, byte editorType, byte valueInputMethod, byte multiLine)
        {
            CustomFieldsModel Data = null;
            try
            {
                Data = SysMng.Instance._container.Resolve<CustomFieldsModel>();
            }
            catch (Exception)
            {
                Data = new CustomFieldsModel(SysMng.Instance._container);
            }
            ExtendedFieldDefinition editedDefinition;
            editedDefinition = new ExtendedFieldDefinition();
            editedDefinition.TableName = tableName.ToString();
            editedDefinition.FieldName = fieldName.ToString();
            editedDefinition.Caption = caption.ToString();
            editedDefinition.Explanation = caption.ToString();
            editedDefinition.DataType = (byte)udtType;
            editedDefinition.UsageType = (byte)fieldUsage;
            editedDefinition.UIElementType = (byte)editorType;
            editedDefinition.Usage = (byte)valueInputMethod;
            editedDefinition.IsMultiLine = (byte)multiLine;
            try
            {
                if (Data.AddField(editedDefinition))
                    if (Data.PostDefinition(editedDefinition))
                        Data.AddToSchema(editedDefinition);
            }
            catch { }
        }
    }
}
