using UnityEngine;
using Woi.SelectionSystem;
using Woi.Events;

namespace Woi.WasteCollectionMode
{
    public class WasteCollectionController : MonoBehaviour
    {
        private void OnEnable()
        {
            EventBus.Register<WasteSelectedEvent>(OnWasteSelected);
        }

        private void OnDisable()
        {
            EventBus.Deregister<WasteSelectedEvent>(OnWasteSelected);
        }

        private void OnWasteSelected(WasteSelectedEvent evt)
        {
            Debug.Log("Waste selected: " + evt.Name);
        }
    }
}



