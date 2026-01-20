using UnityEngine;
using UnityEditor;
using Sirenix.OdinInspector;

[ExecuteAlways]
public class DisableContributeGIRecursive : MonoBehaviour
{
    [Button]
    public void Apply()
    {
        var renderers = GetComponentsInChildren<MeshRenderer>(true);
        int changed = 0;

        foreach (var r in renderers)
        {
            var go = r.gameObject;

            Undo.RecordObject(go, "Disable Contribute GI");

            var flags = GameObjectUtility.GetStaticEditorFlags(go);

            if ((flags & StaticEditorFlags.ContributeGI) != 0)
            {
                flags &= ~StaticEditorFlags.ContributeGI; // bayraðý kaldýr
                GameObjectUtility.SetStaticEditorFlags(go, flags);
                EditorUtility.SetDirty(go);
                changed++;
            }
        }

        Debug.Log($"[DisableContributeGIRecursive] Updated {changed} objects under {name}");
    }
}
