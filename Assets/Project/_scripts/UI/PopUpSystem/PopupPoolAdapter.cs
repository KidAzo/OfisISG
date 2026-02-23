using Reflex.Attributes;
using UnityEngine;
using Woi.Porting;
using WoiUtils.Pooling;

namespace Woi.PopUpSystem
{
    public class PopupPoolAdapter : MonoBehaviour, IModeParticipant
    {
        [SerializeField] Transform pcContainer;
        [SerializeField] Transform vrContainer;
        [SerializeField] BasePopup popupPrefab;
        [Inject] IPortingService portingService;
        IObjectPool<BasePopup> pool;

        Transform currentCanvas;

        void OnEnable()
        {
            portingService.Register(this);
        }

        void OnDisable()
        {
            portingService.Unregister(this);
        }


        void Start()
        {
            SetCanvas(portingService.CurrentMode);
            InitializePool();
        }

        void InitializePool()
        {
            pool = new ObjectPool<BasePopup>(
                CreatePopup,
                OnTakeFromPool,
                OnReturnedToPool,
                OnDestroyPoolObject,
                5,
                20);
        }

        BasePopup CreatePopup()
        {
            var p = Instantiate(popupPrefab, currentCanvas);
            p.transform.rotation = Quaternion.identity; 
            p.gameObject.SetActive(false);
            return p;
        }

        void OnTakeFromPool(BasePopup p) => p.Show();
        void OnReturnedToPool(BasePopup p) => p.Hide();
        void OnDestroyPoolObject(BasePopup p) => Destroy(p.gameObject);
        public BasePopup Get() => pool.Get();
       
        public void Return(BasePopup popup)
        {
            if (popup == null) return;

            if (!popup || popup.gameObject == null) return;

            pool.ReturnToPool(popup);
        }

        public void OnBeforeModeChange(AppMode from, AppMode to)
        {
            
        }

        public void OnAfterModeChange(AppMode mode)
        {
            SetCanvas(mode);   
        }

        void SetCanvas(AppMode mode)
        {
            currentCanvas = mode == AppMode.XR ? vrContainer : pcContainer;
        }
    }
}
