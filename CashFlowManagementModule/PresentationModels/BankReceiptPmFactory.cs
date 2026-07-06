using Prism.Ioc;

using Sentez.BankModule.PresentationModels;
using Sentez.Common;
using Sentez.Common.Commands;
using Sentez.Common.PresentationModels;
using Sentez.Common.SBase;
using Sentez.Common.Views;
using Sentez.Data.BusinessObjects;

using System.ComponentModel;

namespace CashFlowManagementModule.PresentationModels
{
    public class BankReceiptPmFactory : IPMBase
    {
        const short PaymentOrderReceiptType = 15;
        const short CollectionOrderReceiptType = 20;

        readonly IContainerExtension _container;
        BankReceiptPM _innerPm;

        public BankReceiptPmFactory(IContainerExtension container)
        {
            _container = container;
            SetInnerPm(new BankReceiptPM(_container));
        }

        public BankReceiptPM InnerPm => _innerPm;

        public IBusinessObject ActiveBO => _innerPm.ActiveBO;

        public SView<object> ActiveView => _innerPm.ActiveView;

        public SysCommandList CmdList
        {
            get => _innerPm.CmdList;
            set => _innerPm.CmdList = value;
        }

        public IContainerExtension container
        {
            get => _innerPm.container;
            set => _innerPm.container = value;
        }

        public string KeyField
        {
            get => _innerPm.KeyField;
            set => _innerPm.KeyField = value;
        }

        public MenuItemPM MenuItemsRoot
        {
            get => _innerPm.MenuItemsRoot;
            set => _innerPm.MenuItemsRoot = value;
        }

        public string PmName
        {
            get => _innerPm.PmName;
            set => _innerPm.PmName = value;
        }

        public string PmTitle
        {
            get => _innerPm.PmTitle;
            set => _innerPm.PmTitle = value;
        }

        public string TitleKeyField
        {
            get => _innerPm.TitleKeyField;
            set => _innerPm.TitleKeyField = value;
        }

        public string Version
        {
            get => _innerPm.Version;
            set => _innerPm.Version = value;
        }

        public PmParam pmParam
        {
            get => _innerPm.pmParam;
            set => _innerPm.pmParam = value;
        }

        public int TypeFieldValue
        {
            get => _innerPm.TypeFieldValue;
            set => _innerPm.TypeFieldValue = value;
        }

        public object Tag
        {
            get => _innerPm.Tag;
            set => _innerPm.Tag = value;
        }

        public object ControlContainer
        {
            get => _innerPm.ControlContainer;
            set => _innerPm.ControlContainer = value;
        }

        public bool IsModal
        {
            get => _innerPm.IsModal;
            set => _innerPm.IsModal = value;
        }

        public bool Enable
        {
            get => _innerPm.Enable;
            set => _innerPm.Enable = value;
        }

        public string PmExplanation
        {
            get => _innerPm.PmExplanation;
            set => _innerPm.PmExplanation = value;
        }

        public bool IsVisible
        {
            get => _innerPm.IsVisible;
            set => _innerPm.IsVisible = value;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public void Init()
        {
            _innerPm.Init();
        }

        public void ReInit()
        {
            _innerPm.ReInit();
        }

        public void Init(string strViewName)
        {
            _innerPm.Init(strViewName);
        }

        public void Init(string strViewName, PmParam pmparam)
        {
            EnsureInnerPm(ResolveReceiptType(pmparam, null, null));
            _innerPm.Init(strViewName, pmparam);
        }

        public void Init(string strViewName, PmParam pmparam, IBusinessObject businessObject, BoParam boparam)
        {
            EnsureInnerPm(ResolveReceiptType(pmparam, boparam, businessObject));
            _innerPm.Init(strViewName, pmparam, businessObject, boparam);
        }

        public void InitReceiptTypes(object receiptType, bool first)
        {
            _innerPm.InitReceiptTypes(receiptType, first);
        }

        public void LoadCommands()
        {
            _innerPm.LoadCommands();
        }

        public object LoadRes(string ressname)
        {
            return _innerPm.LoadRes(ressname);
        }

        public void SetActiveBO(IBusinessObject bo)
        {
            _innerPm.SetActiveBO(bo);
        }

        public void SetActiveBO(IBusinessObject bo, BoParam boParam)
        {
            _innerPm.SetActiveBO(bo, boParam);
        }

        public void SetActiveBOex(IBusinessObject bo, BoParam boParam)
        {
            _innerPm.SetActiveBOex(bo, boParam);
        }

        public void SetTitleName()
        {
            _innerPm.SetTitleName();
        }

        public void FocusOnKeyField()
        {
            _innerPm.FocusOnKeyField();
        }

        public void KeyFieldChange(string strVal)
        {
            _innerPm.KeyFieldChange(strVal);
        }

        public int GetRefId(string strRefField)
        {
            return _innerPm.GetRefId(strRefField);
        }

        public object GetSelectedFields(string propName, object propVal)
        {
            return _innerPm.GetSelectedFields(propName, propVal);
        }

        public object GetKeyFieldElm()
        {
            return _innerPm.GetKeyFieldElm();
        }

        public object FCtrl(string strCtrl)
        {
            return _innerPm.FCtrl(strCtrl);
        }

        public T FCtrl<T>(string strCtrl)
        {
            return _innerPm.FCtrl<T>(strCtrl);
        }

        public bool CheckStatus()
        {
            return _innerPm.CheckStatus();
        }

        public bool CheckStatus(bool cancelData)
        {
            return _innerPm.CheckStatus(cancelData);
        }

        public bool IntegrationControl(byte type)
        {
            return _innerPm.IntegrationControl(type);
        }

        public void CardListCommand(byte type)
        {
            _innerPm.CardListCommand(type);
        }

        public void Close()
        {
            _innerPm.Close();
        }

        public void Dispose()
        {
            if (_innerPm != null)
                _innerPm.PropertyChanged -= InnerPm_PropertyChanged;

            _innerPm?.Dispose();
            _innerPm = null;
        }

        public bool Closed(object param)
        {
            return _innerPm.Closed(param);
        }

        public bool Closing(object param)
        {
            return _innerPm.Closing(param);
        }

        public bool CanCancelDataCommand(ISysCommandParam obj) => _innerPm.CanCancelDataCommand(obj);

        public bool CanCardChangeCommand(ISysCommandParam arg) => _innerPm.CanCardChangeCommand(arg);

        public bool CanCardCopyCommand(ISysCommandParam arg) => _innerPm.CanCardCopyCommand(arg);

        public bool CanCloseCommand(ISysCommandParam obj) => _innerPm.CanCloseCommand(obj);

        public bool CanDeleteDataCommand(ISysCommandParam obj) => _innerPm.CanDeleteDataCommand(obj);

        public bool CanExtreListCommand(ISysCommandParam obj) => _innerPm.CanExtreListCommand(obj);

        public bool CanFirstCommand(ISysCommandParam obj) => _innerPm.CanFirstCommand(obj);

        public bool CanLastCommand(ISysCommandParam obj) => _innerPm.CanLastCommand(obj);

        public bool CanListCommand(ISysCommandParam obj) => _innerPm.CanListCommand(obj);

        public bool CanNewRecordCommand(ISysCommandParam obj) => _innerPm.CanNewRecordCommand(obj);

        public bool CanNextCommand(ISysCommandParam obj) => _innerPm.CanNextCommand(obj);

        public bool CanPostDataCommand(ISysCommandParam obj) => _innerPm.CanPostDataCommand(obj);

        public bool CanPreviousCommand(ISysCommandParam obj) => _innerPm.CanPreviousCommand(obj);

        public void OnCancelDataCommand(ISysCommandParam obj) => _innerPm.OnCancelDataCommand(obj);

        public void OnCardChangeCommand(ISysCommandParam obj) => _innerPm.OnCardChangeCommand(obj);

        public void OnCardCopyCommand(ISysCommandParam obj) => _innerPm.OnCardCopyCommand(obj);

        public void OnCloseCommand(ISysCommandParam obj) => _innerPm.OnCloseCommand(obj);

        public void OnDeleteDataCommand(ISysCommandParam obj) => _innerPm.OnDeleteDataCommand(obj);

        public void OnExtreListCommand(ISysCommandParam obj) => _innerPm.OnExtreListCommand(obj);

        public void OnFirstCommand(ISysCommandParam obj) => _innerPm.OnFirstCommand(obj);

        public void OnLastCommand(ISysCommandParam obj) => _innerPm.OnLastCommand(obj);

        public void OnListCommand(ISysCommandParam obj) => _innerPm.OnListCommand(obj);

        public void OnListCommandReturnValueHandler(DlgArgs result) => _innerPm.OnListCommandReturnValueHandler(result);

        public void OnNewRecordCommand(ISysCommandParam obj) => _innerPm.OnNewRecordCommand(obj);

        public void OnNextCommand(ISysCommandParam obj) => _innerPm.OnNextCommand(obj);

        public void OnPostDataCommand(ISysCommandParam obj) => _innerPm.OnPostDataCommand(obj);

        public void OnPreviousCommand(ISysCommandParam obj) => _innerPm.OnPreviousCommand(obj);

        public void OnReturnDlgCommand(ISysCommandParam obj) => _innerPm.OnReturnDlgCommand(obj);

        public void OnScreenChangeCommand(ISysCommandParam obj) => _innerPm.OnScreenChangeCommand(obj);

        void SetInnerPm(BankReceiptPM innerPm)
        {
            if (_innerPm != null)
                _innerPm.PropertyChanged -= InnerPm_PropertyChanged;

            _innerPm = innerPm;

            if (_innerPm != null)
                _innerPm.PropertyChanged += InnerPm_PropertyChanged;
        }

        void InnerPm_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            PropertyChanged?.Invoke(this, e);
        }

        void EnsureInnerPm(short receiptType)
        {
            BankReceiptPM requiredInnerPm = CreateInnerPm(receiptType);
            if (_innerPm != null && _innerPm.GetType() == requiredInnerPm.GetType())
                return;

            SetInnerPm(requiredInnerPm);
        }

        BankReceiptPM CreateInnerPm(short receiptType)
        {
            switch (receiptType)
            {
                case PaymentOrderReceiptType:
                    return new PaymentOrderBankReceiptPM(_container);
                case CollectionOrderReceiptType:
                    return new CollectionOrderBankReceiptPM(_container);
                default:
                    return new BankReceiptPM(_container);
            }
        }

        static short ResolveReceiptType(PmParam pmparam, BoParam boparam, IBusinessObject businessObject)
        {
            if (boparam != null && boparam.Type > 0)
                return (short)boparam.Type;

            if (pmparam != null)
            {
                if (pmparam.Type > 0)
                    return (short)pmparam.Type;

                if (pmparam.SubSecId > 0)
                    return (short)pmparam.SubSecId;
            }

            if (businessObject is BusinessObjectBase businessObjectBase
                && businessObjectBase.CurrentRow?.Row != null
                && businessObjectBase.CurrentRow.Row.Table.Columns.Contains("ReceiptType")
                && !businessObjectBase.CurrentRow.Row.IsNull("ReceiptType"))
            {
                return System.Convert.ToInt16(businessObjectBase.CurrentRow.Row["ReceiptType"]);
            }

            return 0;
        }
    }
}
