using System.Collections;
using UnityEngine;
using Woi.Events;
using Woi.PopUpSystem;
using WoiUtils.AudioSystem;

namespace Woi.HazardSystem
{
    public class HazardInfoPopupPresenter : MonoBehaviour
    {
        const string PrefabPath = "Assets/Project/_scripts/UI/Prefabs/Popup.prefab";
        const float FallbackDuration = 1f;

        [SerializeField] Popup2D popupPrefab;
        [SerializeField] Transform pcContainer;
        [SerializeField] Transform vrContainer;

        Popup2D _card;
        int _generation;
        Coroutine _hideRoutine;

        public static void EnsureInstalled()
        {
            if (FindFirstObjectByType<HazardInfoPopupPresenter>(FindObjectsInactive.Include) != null)
                return;

            var host = FindNamed("FeedbackSystem");
            if (host == null)
            {
                host = new GameObject("FeedbackSystem");
            }

            host.AddComponent<HazardInfoPopupPresenter>();
        }

        void OnEnable()
        {
            EventBus.Register<OnHazardFixed>(OnHazardFixed);
        }

        void OnDisable()
        {
            EventBus.Deregister<OnHazardFixed>(OnHazardFixed);
            StopHideRoutine();
        }

        void OnHazardFixed(OnHazardFixed evt)
        {
            if (!EnsureCard())
                return;

            _generation++;
            int token = _generation;

            StopHideRoutine();

            _card.SetTitle(evt.hazardTitle);
            _card.SetMessage(evt.description);
            _card.SetCloseDuration(0.2f);
            _card.Show();

            float duration = ResolveDuration(evt.soundDefinition, evt.hazardID);
            _hideRoutine = StartCoroutine(HideAfter(duration, token));
        }

        IEnumerator HideAfter(float duration, int token)
        {
            yield return new WaitForSeconds(duration);
            if (token != _generation)
                yield break;

            _card?.Hide();
            _hideRoutine = null;
        }

        void StopHideRoutine()
        {
            if (_hideRoutine == null)
                return;

            StopCoroutine(_hideRoutine);
            _hideRoutine = null;
        }

        bool EnsureCard()
        {
            if (_card != null)
                return true;

            ResolveRefs();
            Transform parent = FirePlatformRuntime.CurrentMode == AppMode.XR
                ? vrContainer
                : pcContainer;

            if (parent == null || popupPrefab == null)
                return false;

            var instance = Instantiate(popupPrefab, parent, false);
            instance.name = "Popup";
            instance.transform.localScale = Vector3.one;
            instance.gameObject.SetActive(false);
            _card = instance;
            return _card != null;
        }

        void ResolveRefs()
        {
            if (pcContainer == null)
                pcContainer = FindNamed("PCPopupContainer")?.transform;
            if (vrContainer == null)
                vrContainer = FindNamed("VRPopupContainer")?.transform;
            if (popupPrefab == null)
                popupPrefab = LoadPrefab();
        }

        static Popup2D LoadPrefab()
        {
#if UNITY_EDITOR
            var go = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (go != null)
                return go.GetComponent<Popup2D>();
#endif
            return null;
        }

        static GameObject FindNamed(string name)
        {
            var found = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < found.Length; i++)
            {
                if (found[i] != null && found[i].name == name)
                    return found[i].gameObject;
            }

            return null;
        }

        static float ResolveDuration(SoundDefinition soundDefinition, int hazardID)
        {
            if (soundDefinition == null || soundDefinition.clips == null || hazardID <= 0)
                return FallbackDuration;

            int index = hazardID - 1;
            if (index < 0 || index >= soundDefinition.clips.Count)
                return FallbackDuration;

            var entry = soundDefinition.clips[index];
            if (entry == null || entry.clip == null)
                return FallbackDuration;

            return entry.clip.length;
        }
    }
}
