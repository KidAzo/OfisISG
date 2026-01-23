using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace WoiUtils.SceneTransition
{
    public abstract class SceneTransitionSO : ScriptableObject
    {
        public AnimationCurve LerpCurve;
        public float AnimationTime = 0.25f;
        protected Image AnimatedObject;

        public abstract UniTask Enter(Canvas Parent);
        public abstract UniTask Exit(Canvas Parent);

        protected virtual Image CreateImage(Canvas Parent)
        {
            GameObject child = new GameObject("Transition Image");
            child.transform.SetParent(Parent.transform, false);

            return child.AddComponent<Image>();
        }
    }
}
