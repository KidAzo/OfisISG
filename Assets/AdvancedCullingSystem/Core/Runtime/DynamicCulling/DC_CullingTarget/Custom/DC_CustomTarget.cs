using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace NGS.AdvancedCullingSystem.Dynamic
{
    public class DC_CustomTarget : ICullingTarget
    {
        public GameObject GameObject { get; private set; }
        public Bounds Bounds { get; private set; }

        private DC_CustomTargetEvent _onVisible;
        private DC_CustomTargetEvent _onInvisible;

        public DC_CustomTarget(GameObject go, Bounds bounds,
            DC_CustomTargetEvent onVisible,
            DC_CustomTargetEvent onInvisible)
        {
            GameObject = go;
            Bounds = bounds;

            _onVisible = onVisible != null ? onVisible : new DC_CustomTargetEvent();
            _onInvisible = onInvisible != null ? onInvisible : new DC_CustomTargetEvent();
        }

        public void MakeVisible()
        {
            _onVisible?.Invoke(this);
        }

        public void MakeInvisible()
        {
            _onInvisible?.Invoke(this);
        }
    }

    [System.Serializable]
    public class DC_CustomTargetEvent : UnityEvent<DC_CustomTarget>
    {

    }
}