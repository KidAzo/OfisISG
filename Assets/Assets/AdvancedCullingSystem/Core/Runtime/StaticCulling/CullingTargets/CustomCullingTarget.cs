using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace NGS.AdvancedCullingSystem.Static
{
    public class CustomCullingTarget : CullingTarget
    {
        public CustomTargetEvent OnVisible
        {
            get
            {
                return _onVisible;
            }
        }
        public CustomTargetEvent OnInvisible
        {
            get
            {
                return _onInvisible;
            }
        }

        [field : SerializeField, HideInInspector]
        public bool IsOccluder { get; set; }

        [SerializeField]
        private CustomTargetEvent _onVisible;

        [SerializeField]
        private CustomTargetEvent _onInvisible;


        private void Awake()
        {
            if (_onVisible == null)
                _onInvisible = new CustomTargetEvent();

            if (_onInvisible == null)
                _onInvisible = new CustomTargetEvent();
        }

        public void SetActions(CustomTargetEvent onVisible, CustomTargetEvent onInvisible)
        {
            _onVisible = onVisible;
            _onInvisible = onInvisible;
        }

        protected override void MakeVisible()
        {
            _onVisible.Invoke(this);
        }

        protected override void MakeInvisible()
        {
            _onInvisible.Invoke(this);
        }
    }

    [System.Serializable]
    public class CustomTargetEvent : UnityEvent<CullingTarget>
    {
        
    }
}
