using UnityEngine;
using WoiUtils.Pooling;

namespace Woi.PopUpSystem
{
    public class PopupPoolAdapter : MonoBehaviour
    {
        [SerializeField] Transform parent;
        [SerializeField] BasePopup popupPrefab;
        IObjectPool<BasePopup> pool;

        void Awake()
        {
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
            var p = Instantiate(popupPrefab, parent);
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
    }
}
