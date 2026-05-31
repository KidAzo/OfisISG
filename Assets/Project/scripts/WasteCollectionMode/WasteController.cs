using UnityEngine;
using Woi.Events;
using Woi.SelectionSystem;

namespace Woi.WasteCollectionMode
{
    public class WasteController : MonoBehaviour, ISelectable
    {
            Outline outline;

            private void Awake()
            {
                outline = GetComponent<Outline>();
            }

            private void Start()
            {
                outline.enabled = false;
            }

            private void SetOutline()
            {
                outline.enabled = true;
                outline.OutlineWidth = 5f;
            }

            public void Select()
            {
                SetOutline();

                WasteCollectable collectable = GetComponent<WasteCollectable>();
                if (collectable != null && WasteCollectTracker.TryGetActive(out WasteCollectTracker tracker))
                    tracker.Collect(collectable);
            }

            public void Deselect()
            {
            }
    }
}
