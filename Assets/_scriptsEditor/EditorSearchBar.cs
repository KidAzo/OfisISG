#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System;

[Serializable]
public class EditorSearchBar
{
    public string SearchString = "";
    public bool IsActive => !string.IsNullOrWhiteSpace(SearchString);

    // Event: Arama değiştiğinde editörün Repaint olması için
    public Action OnSearchChanged;

    public void DrawLayout()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        
        string newSearch = EditorGUILayout.TextField(SearchString, EditorStyles.toolbarSearchField);
        
        if (newSearch != SearchString)
        {
            SearchString = newSearch;
            OnSearchChanged?.Invoke();
        }

        if (GUILayout.Button("X", EditorStyles.toolbarButton, GUILayout.Width(20)))
        {
            SearchString = "";
            GUI.FocusControl(null);
            OnSearchChanged?.Invoke();
        }

        EditorGUILayout.EndHorizontal();
    }

    // Basit bir eşleşme kontrolü (Helper)
    public bool Matches(string targetName)
    {
        if (!IsActive) return true;
        return targetName.IndexOf(SearchString, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
#endif