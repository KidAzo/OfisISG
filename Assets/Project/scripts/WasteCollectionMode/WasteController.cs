using PrimeTween;
using UnityEngine;
using Woi.Events;
using Woi.SelectionSystem;
using WOI.Modules.SDK;

namespace Woi.WasteCollectionMode
{
    public class WasteController : MonoBehaviour, ISelectable
    {
        [SerializeField] private float scaleDuration = 0.4f;

        private Outline outline;
        private Vector3 initialLocalScale;
        private Tween scaleTween;
        private FeedbackManager feedbackManager;

        private void Awake()
        {
            outline = GetComponent<Outline>();
            initialLocalScale = transform.localScale;
        }

        private void Start()
        {
            if (ServiceLocator.TryGet(out FeedbackManager feedbackManager)) 
                this.feedbackManager = feedbackManager;
            else    
                Debug.LogError("FeedbackManager not found");

            if (outline != null)
                outline.enabled = false;
        }

        private void SetOutline()
        {
            if (outline == null)
                return;

            outline.enabled = true;
            outline.OutlineWidth = 5f;
        }

        public void Select()
        {
            Feedback();

            WasteCollectable collectable = GetComponent<WasteCollectable>();
            if (collectable != null && WasteCollectTracker.TryGetActive(out WasteCollectTracker tracker))
                tracker.Collect(collectable);
        }

        public void Deselect()
        {
        }

        private void Feedback()
        {
            SetOutline();

            if (feedbackManager != null)
                feedbackManager.PlayFeedback(transform);

            WasteCollectable collectable = GetComponent<WasteCollectable>();
            string wasteName = collectable != null && collectable.Definition != null
                ? collectable.Definition.Name
                : string.Empty;

            if (scaleTween.isAlive)
                scaleTween.Stop();

            transform.localScale = initialLocalScale;
            scaleTween = Tween.Scale(transform, Vector3.zero, scaleDuration, Ease.InBack)
                .OnComplete(gameObject, _ =>
                {
                    int totalCollected = 0;
                    if (WasteCollectTracker.TryGetActive(out WasteCollectTracker tracker))
                        totalCollected = tracker.Records.Count;

                    EventBus.Raise(new WasteCollectedEvent(wasteName, totalCollected));
                    gameObject.SetActive(false);
                });
        }
    }
}
