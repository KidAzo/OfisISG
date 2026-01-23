using Cysharp.Threading.Tasks;
using UnityEngine;

namespace WoiUtils.SceneTransition
{
	[CreateAssetMenu(fileName = "Circle", menuName = "SO/Scene Transitions/Circle")]
    public class CircleTransitionScriptableObject : SceneTransitionSO
    {
        public Sprite CircleSprite;
        public Color Color;
        
        public override async UniTask Enter(Canvas parent)
        {
            float time = 0;
            float size = Mathf.Sqrt(
                Mathf.Pow(Screen.width, 2) + Mathf.Pow(Screen.height, 2)
            );
            Vector2 initialSize = new Vector2(size, size);
            while (time < 1)
            {
                AnimatedObject.rectTransform.sizeDelta = Vector2.Lerp(
                    initialSize, 
                    Vector2.zero, 
                    LerpCurve.Evaluate(time)
                );
                
                await UniTask.Yield();
                time += Time.deltaTime / AnimationTime;
            }

            Destroy(AnimatedObject.gameObject);
        }
 
        public override async UniTask Exit(Canvas Parent)
        {
            AnimatedObject = CreateImage(Parent);
            AnimatedObject.color = Color;
            AnimatedObject.rectTransform.sizeDelta = Vector2.zero;
            AnimatedObject.sprite = CircleSprite;

            float time = 0;
            float size = Mathf.Sqrt(
                Mathf.Pow(Screen.width, 2) + Mathf.Pow(Screen.height, 2)
            );
            Vector2 targetSize = new Vector2(size, size);
            while (time < 1)
            {
                AnimatedObject.rectTransform.sizeDelta = Vector2.Lerp(
                    Vector2.zero, 
                    targetSize, 
                    LerpCurve.Evaluate(time)
                );
                
                await UniTask.Yield();
                time += Time.deltaTime / AnimationTime;
            }
        }
    }
}
