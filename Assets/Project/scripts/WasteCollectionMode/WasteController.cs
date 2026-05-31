using UnityEngine;
using Woi.Events;
using Woi.SelectionSystem;

namespace Woi.WasteCollectionMode
{
    public class WasteController : MonoBehaviour, ISelectable
    {
            [SerializeField] private Waste waste;
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
                EventBus.Raise(new WasteSelectedEvent(waste.name));
            }

            public void Deselect()
            {
            }
    }
}
