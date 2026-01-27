using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Woi.CollectionsEditor;

public class MoveToCollectionWindow : EditorWindow
{
    // ---- open / hotkey ----
    [MenuItem("Edit/Move To Collection (M) _m")]
    private static void OpenHotkey()
    {
        var sel = Selection.gameObjects;
        if (sel == null || sel.Length == 0)
        {
            EditorUtility.DisplayDialog("Move To Collection", "Select at least one GameObject.", "OK");
            return;
        }

        var w = CreateInstance<MoveToCollectionWindow>();
        w.selection = sel;

        // mouse'a yakın aç
        Vector2 mouse = GUIUtility.GUIToScreenPoint(Event.current?.mousePosition ?? Vector2.zero);
        if (mouse == Vector2.zero)
        {
            var fw = focusedWindow;
            if (fw != null) mouse = fw.position.center;
            else mouse = new Vector2(600, 350);
        }

        w.titleContent = new GUIContent("Move to Collection");
        w.position = new Rect(mouse.x - 180, mouse.y - 40, 360, 420);
        w.ShowUtility(); // küçük floating
        w.Focus();
    }

    // ---- state ----
    private GameObject[] selection;
    private string search = "";
    private Vector2 scroll;
    private List<CollectionNode> cached = new();

    // drag window bar
    private bool draggingWindow;
    private Vector2 dragStartMouse;
    private Vector2 dragStartPos;

    private const float TitleBarH = 28f;
    private const float Padding = 8f;

    // ---- types ----
    private class CollectionNode
    {
        public GameObject go;
        public int depth;
        public bool hasChildren;
    }

    private void OnEnable()
    {
        Refresh();
        Undo.undoRedoPerformed += OnUndoRedo;
        Selection.selectionChanged += OnSelectionChanged;
    }

    private void OnDisable()
    {
        Undo.undoRedoPerformed -= OnUndoRedo;
        Selection.selectionChanged -= OnSelectionChanged;
    }

    private void OnUndoRedo()
    {
        Refresh();
        Repaint();
    }

    private void OnSelectionChanged()
    {
        // bu pencere açıkken seçim değişirse de çalışsın
        selection = Selection.gameObjects;
        Repaint();
    }

private void Refresh()
{
    // Sahnedeki tüm GameObject'ler (inactive dahil)
    var all = Resources.FindObjectsOfTypeAll<GameObject>()
        .Where(go => go != null && go.scene.IsValid())
        .Where(go => (go.hideFlags & HideFlags.NotEditable) == 0)
        .Where(go => (go.hideFlags & HideFlags.HideAndDontSave) == 0);

    // ✅ Collection = child'ı olanlar
    var collections = all
        .Where(go => go.transform.childCount > 0)
        .Distinct()
        .ToList();

    cached = BuildFlattened(collections);
}

private bool HasNonCollectionChild(Transform t)
{
    // collection child’larını sayma (Collections root altındaysa)
    // gerçek obje var mı diye bak
    for (int i = 0; i < t.childCount; i++)
    {
        var ch = t.GetChild(i);
        // Eğer child da collection ise sayma (Collections root altı kuralı)
        if (ch.parent != null && ch.parent.name == "Collections")
            continue;

        return true; // en az bir gerçek child var
    }
    return false;
}

    private static List<CollectionNode> BuildFlattened(List<GameObject> collections)
    {
        var nodes = collections.ToDictionary(go => go, go => new Temp(go));
        foreach (var go in collections)
        {
            var p = go.transform.parent ? go.transform.parent.gameObject : null;
            if (p != null && nodes.TryGetValue(p, out var pn))
            {
                pn.children.Add(nodes[go]);
                nodes[go].hasParent = true;
            }
        }

        var top = nodes.Values.Where(n => !n.hasParent).OrderBy(n => n.go.name).ToList();
        var list = new List<CollectionNode>(collections.Count);

        void Walk(Temp t, int depth)
        {
            list.Add(new CollectionNode
            {
                go = t.go,
                depth = depth,
                hasChildren = t.children.Count > 0
            });

            foreach (var ch in t.children.OrderBy(x => x.go.name))
                Walk(ch, depth + 1);
        }

        foreach (var t in top) Walk(t, 0);
        return list;
    }

    private class Temp
    {
        public GameObject go;
        public bool hasParent;
        public List<Temp> children = new();
        public Temp(GameObject go) { this.go = go; }
    }

   private void OnGUI()
{
    DrawTitleBar();

    // ✅ İçeriği title bar'ın altına taşı
    Rect contentRect = new Rect(0, TitleBarH, position.width, position.height - TitleBarH);
    GUILayout.BeginArea(contentRect);

    GUILayout.Space(8);

    using (new EditorGUILayout.HorizontalScope())
    {
        GUILayout.Space(Padding);
        GUILayout.Label("Search", GUILayout.Width(45));
        search = GUILayout.TextField(search);

        if (GUILayout.Button("↻", GUILayout.Width(28)))
            Refresh();

        GUILayout.Space(Padding);
    }

    GUILayout.Space(8);

    using (new EditorGUILayout.HorizontalScope())
    {
        GUILayout.Space(Padding);

        if (GUILayout.Button("+  New Collection", GUILayout.Height(24)))
        {
            CreateNewCollectionAndMove();
            GUIUtility.ExitGUI();
        }

        GUILayout.Space(Padding);
    }

    GUILayout.Space(8);

    using (new EditorGUILayout.HorizontalScope())
    {
        GUILayout.Space(Padding);

        using (var sv = new EditorGUILayout.ScrollViewScope(scroll))
        {
            scroll = sv.scrollPosition;

            var shown = Filtered();
            if (shown.Count == 0)
            {
                EditorGUILayout.HelpBox("No collections found.", MessageType.Info);
            }
            else
            {
                foreach (var n in shown)
                    DrawCollectionRow(n);
            }
        }

        GUILayout.Space(Padding);
    }

    GUILayout.EndArea();
}


    // --------- draggable title bar ---------
    private void DrawTitleBar()
    {
        Rect bar = new Rect(0, 0, position.width, TitleBarH);
        EditorGUI.DrawRect(bar, new Color(0f, 0f, 0f, 0.18f));

        // Title
        var titleRect = new Rect(10, 5, position.width - 60, 18);
        GUI.Label(titleRect, "Move to Collection", EditorStyles.boldLabel);

        // close button
        var closeRect = new Rect(position.width - 28, 4, 24, 20);
        if (GUI.Button(closeRect, "✕"))
        {
            Close();
            GUIUtility.ExitGUI();
        }

        HandleTitleBarDrag(bar);
    }

    private void HandleTitleBarDrag(Rect bar)
    {
        var e = Event.current;
        if (e == null) return;

        // sadece title bar alanında drag
        if (e.type == EventType.MouseDown && bar.Contains(e.mousePosition) && e.button == 0)
        {
            draggingWindow = true;
            dragStartMouse = GUIUtility.GUIToScreenPoint(e.mousePosition);
            dragStartPos = new Vector2(position.x, position.y);
            e.Use();
        }
        else if (e.type == EventType.MouseDrag && draggingWindow)
        {
            Vector2 now = GUIUtility.GUIToScreenPoint(e.mousePosition);
            Vector2 delta = now - dragStartMouse;
            position = new Rect(dragStartPos.x + delta.x, dragStartPos.y + delta.y, position.width, position.height);
            e.Use();
        }
        else if (e.type == EventType.MouseUp && draggingWindow)
        {
            draggingWindow = false;
            e.Use();
        }
    }

    // --------- rows ---------
    private List<CollectionNode> Filtered()
    {
        if (cached == null) return new List<CollectionNode>();

        string q = search?.Trim();
        if (string.IsNullOrEmpty(q)) return cached;

        q = q.ToLowerInvariant();
        return cached.Where(n => n.go != null && n.go.name.ToLowerInvariant().Contains(q)).ToList();
    }

    private void DrawCollectionRow(CollectionNode n)
    {
        if (n.go == null) return;

        Rect r = GUILayoutUtility.GetRect(10, 22, GUILayout.ExpandWidth(true));

        // indent
        float indent = 14f * n.depth;
        var labelRect = new Rect(r.x + 8 + indent, r.y + 3, r.width - 16 - indent, r.height - 6);

        // hover bg
        if (r.Contains(Event.current.mousePosition))
            EditorGUI.DrawRect(r, new Color(0.3f, 0.5f, 1f, 0.15f));

        // icon + name
        GUI.Label(labelRect, n.go.name, EditorStyles.label);

        // click to move
        if (Event.current.type == EventType.MouseUp && r.Contains(Event.current.mousePosition) && Event.current.button == 0)
        {
            MoveSelectionTo(n.go.transform);
            Close();
            Event.current.Use();
            GUIUtility.ExitGUI();
        }
    }

    // --------- actions ---------
  private void CreateNewCollectionAndMove()
{
    NamePromptWindow.Show(
        title: "New Collection",
        defaultValue: "New Collection",
        onCreate: name =>
        {
            if (string.IsNullOrWhiteSpace(name))
                name = "New Collection";

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Create Collection + Move Selection");

            var collection = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(collection, "Create Collection");
            collection.AddComponent<CollectionRoot>();

            foreach (var go in FilterTopLevel(selection))
                Undo.SetTransformParent(go.transform, collection.transform, "Move To Collection");

            Undo.CollapseUndoOperations(group);

            Selection.activeGameObject = collection;
            EditorGUIUtility.PingObject(collection);

            Refresh();
            Repaint();
        }
    );
}

    private static IEnumerable<GameObject> FilterTopLevel(IEnumerable<GameObject> gos)
    {
        if (gos == null) yield break;

        var list = gos.Where(g => g != null).Distinct().ToList();
        var set = new HashSet<Transform>(list.Select(g => g.transform));

        foreach (var go in list)
        {
            var p = go.transform.parent;
            bool skip = false;
            while (p != null)
            {
                if (set.Contains(p)) { skip = true; break; }
                p = p.parent;
            }
            if (!skip) yield return go;
        }
    }

    private void MoveSelectionTo(Transform target)
    {
        if (target == null) return;

        var filtered = FilterTopLevel(selection)
            .Where(go => go != null && !target.IsChildOf(go.transform)) // cycle prevention
            .ToArray();

        if (filtered.Length == 0) return;

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Move To Collection");

        foreach (var go in filtered)
            Undo.SetTransformParent(go.transform, target, "Move To Collection");

        Undo.CollapseUndoOperations(group);

        Selection.objects = filtered;
        EditorGUIUtility.PingObject(target.gameObject);
    }

}

internal sealed class NamePromptWindow : EditorWindow
{
    private const string Field = "NameField";

    private string titleText;
    private string value;
    private Action<string> onCreate;

    public static void Show(string title, string defaultValue, Action<string> onCreate)
    {
        var w = CreateInstance<NamePromptWindow>();
        w.titleText = title;
        w.value = defaultValue;
        w.onCreate = onCreate;

        w.titleContent = new GUIContent(title);

        // Pencereyi MoveToCollectionWindow'un yakınına aç
        var fw = EditorWindow.focusedWindow;
        if (fw != null)
        {
            var p = fw.position;
            w.position = new Rect(p.x + p.width * 0.5f - 170, p.y + 60, 340, 105);
        }
        else
        {
            w.position = new Rect(600, 350, 340, 105);
        }

        w.ShowUtility();
        w.Focus();
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField(titleText, EditorStyles.boldLabel);

        EditorGUILayout.Space(6);

        GUI.SetNextControlName(Field);
        value = EditorGUILayout.TextField(value);

        // İlk frame focus
        if (Event.current.type == EventType.Repaint)
            EditorGUI.FocusTextInControl(Field);

        bool enter = Event.current.type == EventType.KeyDown &&
                    (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter);

        bool escape = Event.current.type == EventType.KeyDown &&
                      Event.current.keyCode == KeyCode.Escape;

        EditorGUILayout.Space(8);

        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Create", GUILayout.Width(90)) || enter)
            {
                onCreate?.Invoke(value);
                Close();
                if (enter) Event.current.Use();
                GUIUtility.ExitGUI();
            }

            if (GUILayout.Button("Cancel", GUILayout.Width(90)) || escape)
            {
                Close();
                if (escape) Event.current.Use();
                GUIUtility.ExitGUI();
            }

            GUILayout.FlexibleSpace();
        }
    }
}

