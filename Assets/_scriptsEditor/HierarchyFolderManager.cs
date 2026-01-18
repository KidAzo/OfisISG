using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

public class HierarchyFolderManager : EditorWindow
{
    private Vector2 scrollPos;
    private List<FolderNode> rootFolders = new List<FolderNode>();
    private GameObject[] selectedObjects;
    private GameObject[] draggedObjectsFromPanel;
    private string newFolderName = "New Folder";
    private bool isDraggingFromPanel = false;
    private FolderNode hoveredNode = null;
    private double mouseDownTime = 0;
    private Vector2 mouseDownPos;
    private const float dragThreshold = 5f; // pixel
    private const float dragDelayTime = 0.1f; // saniye
    private EditorSearchBar searchBar = new EditorSearchBar();
    private GameObject lastSelectedObject; // Shift seçimi için başlangıç noktası

    private class FolderNode
    {
        public GameObject gameObject;
        public List<FolderNode> children = new List<FolderNode>();
        public bool isExpanded = true;
        
        public FolderNode(GameObject obj)
        {
            gameObject = obj;
        }
    }

    [MenuItem("Tools/Hierarchy Folder Manager")]
    public static void ShowWindow()
    {
        var window = GetWindow<HierarchyFolderManager>("Folder Manager");
        window.minSize = new Vector2(350, 400);
    }
    
    // M tuşu için shortcut
    [MenuItem("Edit/Move Selection to Folder (M) _m")]
    private static void QuickFolderShortcut()
    {
        if (Selection.gameObjects.Length > 0)
        {
            QuickFolderPopup.ShowWindow(Selection.gameObjects);
        }
    }

   private void OnEnable()
{
    // Arama barı repaint event'i
    searchBar.OnSearchChanged = Repaint;

    // Undo veya Redo yapıldığında ağacı yenile
    Undo.undoRedoPerformed += OnUndoRedoPerformed;
    Selection.selectionChanged += OnSelectionChanged;
    
    RefreshFolderTree();
}

private void OnDisable()
{
    Undo.undoRedoPerformed -= OnUndoRedoPerformed;
    Selection.selectionChanged -= OnSelectionChanged;
}

private void OnUndoRedoPerformed()
{
    // Sahne eski haline döndü, listeyi tekrar tara
    RefreshFolderTree();
    Repaint();
}

    private void OnSelectionChanged()
    {
        selectedObjects = Selection.gameObjects;
        Repaint();
    }

    private void RefreshFolderTree()
    {
        rootFolders.Clear();
        
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var rootObjects = scene.GetRootGameObjects().ToList();
        
        foreach (var root in rootObjects)
        {
            var node = new FolderNode(root);
            BuildTree(node, root.transform);
            rootFolders.Add(node);
        }
    }

    private void BuildTree(FolderNode node, Transform parent)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            var childNode = new FolderNode(child.gameObject);
            BuildTree(childNode, child);
            node.children.Add(childNode);
        }
    }

  private void OnGUI()
{
    EditorGUILayout.Space(5);
    
    // 1. Arama Barını en üste çiziyoruz
    if (searchBar != null)
    {
        searchBar.DrawLayout();
    }

    // Header
    EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
    GUILayout.Label("Hierarchy Folder Manager", EditorStyles.boldLabel);
    
    if (GUILayout.Button("↻", GUILayout.Width(30)))
    {
        RefreshFolderTree();
    }
    EditorGUILayout.EndHorizontal();
    
    EditorGUILayout.Space(5);
    
    // Kullanım bilgisi (Arama varken gizlenebilir veya kalabilir, tercih senin)
    if (!searchBar.IsActive) 
    {
        if (isDraggingFromPanel && draggedObjectsFromPanel != null)
            EditorGUILayout.HelpBox($"🖱️ Dragging {draggedObjectsFromPanel.Length} object(s)", MessageType.Info);
        else if (selectedObjects != null && selectedObjects.Length > 0)
            EditorGUILayout.HelpBox($"✋ {selectedObjects.Length} object(s) selected", MessageType.Info);
    }
    
    // Yeni klasör oluşturma
    EditorGUILayout.BeginHorizontal();
    EditorGUILayout.LabelField("New Folder:", GUILayout.Width(80));
    newFolderName = EditorGUILayout.TextField(newFolderName);
    if (GUILayout.Button("+", GUILayout.Width(30)))
    {
        CreateNewFolder(null);
    }
    EditorGUILayout.EndHorizontal();
    
    EditorGUILayout.Space(5);
    
    // Klasör ağacı
    scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
    
    EditorGUILayout.LabelField("Root (Top level in scene)", EditorStyles.boldLabel);
    DrawRootDropArea();
    
    EditorGUILayout.Space(5);
    
    // --- KRİTİK GÜNCELLEME BURADA ---
    foreach (var folder in rootFolders)
    {
        // Sadece arama kriterine uyan klasörleri/dalları çiz
        if (ShouldShowNode(folder))
        {
            DrawFolderNode(folder, 0);
        }
    }
    
    EditorGUILayout.EndScrollView();
    
    // Alt bilgi (Arama aktifse kalabalık etmesin diye gizlenebilir)
    if (!searchBar.IsActive)
        EditorGUILayout.HelpBox("📖 Two modes: [Hierarchy → Click] or [Panel → Drag]", MessageType.None);
    
    // Mouse up ve Repaint işlemleri (Değişmedi)
    if (Event.current.type == EventType.MouseUp && isDraggingFromPanel)
    {
        isDraggingFromPanel = false;
        draggedObjectsFromPanel = null;
        hoveredNode = null;
        Repaint();
    }
    
    if (isDraggingFromPanel) Repaint();
}

private bool ShouldShowNode(FolderNode node)
{
    // Arama yapılmıyorsa her şeyi göster
    if (searchBar == null || !searchBar.IsActive) return true;

    // 1. Bu klasörün ismi aranan kelimeyi içeriyor mu?
    if (searchBar.Matches(node.gameObject.name)) return true;

    // 2. Bu klasörün çocuklarından (alt klasörlerinden) herhangi biri içeriyor mu?
    return node.children.Any(child => ShouldShowNode(child));
}
    private void DrawRootDropArea()
    {
        Rect rect = GUILayoutUtility.GetRect(GUIContent.none, GUI.skin.box, GUILayout.Height(40), GUILayout.ExpandWidth(true));
        
        bool isHovering = rect.Contains(Event.current.mousePosition) && isDraggingFromPanel;
        bool isClickable = selectedObjects != null && selectedObjects.Length > 0 && !isDraggingFromPanel;
        
        Color originalColor = GUI.backgroundColor;
        
        if (isHovering)
        {
            GUI.backgroundColor = new Color(0.5f, 1f, 0.5f, 0.8f);
        }
        else if (isClickable && rect.Contains(Event.current.mousePosition))
        {
            GUI.backgroundColor = new Color(0.5f, 1f, 0.5f, 0.5f);
        }
        else if (isDraggingFromPanel)
        {
            GUI.backgroundColor = new Color(0.7f, 0.9f, 1f, 0.5f);
        }
        
        GUI.Box(rect, "⬇ Move to Root", EditorStyles.helpBox);
        GUI.backgroundColor = originalColor;
        
        // Hierarchy'den seçili objeler için TIK
        if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition) && isClickable)
        {
            MoveObjectsToParent(null, selectedObjects);
            Event.current.Use();
        }
        
        // Panel'den sürüklenenler için DROP
        if (Event.current.type == EventType.MouseUp && rect.Contains(Event.current.mousePosition))
        {
            if (isDraggingFromPanel && draggedObjectsFromPanel != null && draggedObjectsFromPanel.Length > 0)
            {
                MoveObjectsToParent(null, draggedObjectsFromPanel);
                isDraggingFromPanel = false;
                draggedObjectsFromPanel = null;
                Event.current.Use();
            }
        }
    }

private void DrawFolderNode(FolderNode node, int level)
{
    if (node.gameObject == null) return;

    // Arama filtrelemesi (Önceki adımda eklemiştik)
    if (searchBar.IsActive && ShouldShowNode(node))
        node.isExpanded = true;

    EditorGUILayout.BeginHorizontal();
    if (level > 0) GUILayout.Space(level * 20);

    // Foldout (Ok işareti)
    if (node.children.Count > 0)
    {
        Rect foldoutRect = GUILayoutUtility.GetRect(12, 18, GUILayout.Width(12));
        node.isExpanded = EditorGUI.Foldout(foldoutRect, node.isExpanded, "");
    }
    else GUILayout.Space(12);

    // --- KLASÖR SATIRI TASARIMI ---
    GUIContent content = new GUIContent($"📁 {node.gameObject.name}");
    Rect folderRect = GUILayoutUtility.GetRect(content, EditorStyles.label, GUILayout.ExpandWidth(true), GUILayout.Height(18));
    
    bool isSelected = Selection.gameObjects.Contains(node.gameObject);
    bool isHovering = folderRect.Contains(Event.current.mousePosition);

    // Görsel Geri Bildirim (Arka Plan)
    DrawNodeBackground(folderRect, isSelected, isHovering);
    GUI.Label(folderRect, content, EditorStyles.label);

    // --- EVENT HANDLING (SEÇİM VE DRAG) ---
    HandleNodeEvents(folderRect, node);

    // Yeni alt klasör butonu
    if (GUILayout.Button("+", GUILayout.Width(25))) CreateNewFolder(node.gameObject.transform);

    EditorGUILayout.EndHorizontal();

    if (node.isExpanded)
    {
        foreach (var child in node.children)
        {
            if (ShouldShowNode(child)) DrawFolderNode(child, level + 1);
        }
    }
}

private void HandleNodeEvents(Rect rect, FolderNode node)
{
    Event e = Event.current;
    GameObject currentObj = node.gameObject;

    switch (e.type)
    {
        case EventType.MouseDown:
            if (rect.Contains(e.mousePosition) && e.button == 0)
            {
                mouseDownPos = e.mousePosition;

                // --- SHIFT SEÇİMİ ---
                if (e.shift && lastSelectedObject != null && lastSelectedObject != currentObj)
                {
                    List<GameObject> visibleNodes = new List<GameObject>();
                    GetVisibleFlattenedNodes(rootFolders, visibleNodes);
                    int startIdx = visibleNodes.IndexOf(lastSelectedObject);
                    int endIdx = visibleNodes.IndexOf(currentObj);

                    if (startIdx != -1 && endIdx != -1)
                    {
                        int min = Mathf.Min(startIdx, endIdx);
                        int max = Mathf.Max(startIdx, endIdx);
                        List<GameObject> newSelection = new List<GameObject>();
                        if (e.control || e.command) newSelection.AddRange(Selection.gameObjects);
                        for (int i = min; i <= max; i++)
                        {
                            if (!newSelection.Contains(visibleNodes[i])) newSelection.Add(visibleNodes[i]);
                        }
                        Selection.objects = newSelection.ToArray();
                    }
                }
                // --- CTRL / CMD SEÇİMİ ---
                else if (e.control || e.command)
                {
                    List<GameObject> sel = Selection.gameObjects.ToList();
                    if (sel.Contains(currentObj)) sel.Remove(currentObj);
                    else sel.Add(currentObj);
                    Selection.objects = sel.ToArray();
                    lastSelectedObject = currentObj;
                }
                // --- NORMAL SEÇİM ---
                else
                {
                    // Eğer tıkladığımız şey zaten seçiliyse, seçimi bozma (çünkü drag yapacak olabiliriz)
                    if (!Selection.gameObjects.Contains(currentObj))
                    {
                        Selection.activeGameObject = currentObj;
                    }
                    lastSelectedObject = currentObj;
                }
                e.Use();
            }
            break;

        case EventType.MouseDrag:
            if (rect.Contains(e.mousePosition) && Vector2.Distance(mouseDownPos, e.mousePosition) > dragThreshold)
            {
                DragAndDrop.PrepareStartDrag();

                // KRİTİK NOKTA: Eğer sürüklenen obje seçiliyse, Selection.gameObjects dizisinin TAMAMINI sürükle.
                // Böylece Ctrl ile seçtiğin 10 obje birden taşınır.
                GameObject[] draggedObjects;
                if (Selection.gameObjects.Contains(currentObj))
                {
                    draggedObjects = Selection.gameObjects;
                }
                else
                {
                    draggedObjects = new GameObject[] { currentObj };
                    Selection.objects = draggedObjects; // Sürüklediğimizi seçili yapalım
                }

                DragAndDrop.objectReferences = draggedObjects;
                DragAndDrop.StartDrag("Move Hierarchy Objects");
                e.Use();
            }
            break;

        case EventType.DragUpdated:
            if (rect.Contains(e.mousePosition))
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Move;
                e.Use();
            }
            break;

        case EventType.DragPerform:
            if (rect.Contains(e.mousePosition))
            {
                DragAndDrop.AcceptDrag();
                // DragAndDrop.objectReferences içindeki her şeyi (toplu seçimi) yeni parent'a taşı
                MoveObjectsToParent(node.gameObject.transform, DragAndDrop.objectReferences.OfType<GameObject>().ToArray());
                e.Use();
            }
            break;
    }
}
private void GetVisibleFlattenedNodes(List<FolderNode> nodes, List<GameObject> flattenedList)
{
    foreach (var node in nodes)
    {
        // Arama yapılıyorsa sadece aramaya uyanları listeye al
        if (node.gameObject == null || !ShouldShowNode(node)) continue;

        flattenedList.Add(node.gameObject);
        
        // Eğer klasör açıksa (isExpanded), alt çocukları da sıraya ekle
        if (node.isExpanded)
        {
            GetVisibleFlattenedNodes(node.children, flattenedList);
        }
    }
}


private void DrawNodeBackground(Rect rect, bool isSelected, bool isHovering)
{
    // Sürükleme işlemi sırasında hedef klasörün üzerine geliniyorsa (Drop hedefi)
    if (isHovering && DragAndDrop.objectReferences.Length > 0)
    {
        EditorGUI.DrawRect(rect, new Color(0.3f, 1f, 0.3f, 0.2f)); // Hafif yeşil
    }
    else if (isSelected)
    {
        EditorGUI.DrawRect(rect, new Color(0.24f, 0.49f, 0.91f, 0.5f)); // Seçim mavisi
    }
    else if (isHovering)
    {
        EditorGUI.DrawRect(rect, new Color(1f, 1f, 1f, 0.05f)); // Hover aydınlığı
    }
}
private void MoveObjectsToParent(Transform parent, GameObject[] objectsToMove)
{
    if (objectsToMove == null || objectsToMove.Length == 0) return;

    // İşlemleri gruplandır (Tek Ctrl+Z ile hepsini geri almak için)
    Undo.IncrementCurrentGroup();
    Undo.SetCurrentGroupName("Move Objects to Folder");
    int group = Undo.GetCurrentGroup();

    foreach (var obj in objectsToMove)
    {
        if (obj != null && obj.transform.parent != parent)
        {
            // KRİTİK: Normal SetParent yerine bunu kullan
            Undo.SetTransformParent(obj.transform, parent, "Move Objects");
        }
    }

    Undo.CollapseUndoOperations(group);

    // İşlem bittikten sonra gecikmeli olarak ağacı yenile
    EditorApplication.delayCall += RefreshFolderTree;
}

private void CreateNewFolder(Transform parent)
{
    // 1. Klasörü oluştur
    GameObject folder = new GameObject(newFolderName);
    
    // 2. Oluşturma işlemini kaydet
    Undo.RegisterCreatedObjectUndo(folder, "Create Folder");

    // 3. Eğer bir parent varsa, hiyerarşi değişikliğini de kaydet
    if (parent != null)
    {
        Undo.SetTransformParent(folder.transform, parent, "Parent Folder");
    }

    Selection.activeGameObject = folder;
    newFolderName = "New Folder";
    
    EditorApplication.delayCall += RefreshFolderTree;
}
    
    private Color GetFolderColor(bool isBeingDragged, bool isHovering, bool isClickable, bool isSelected)
    {
        // Panel içinde drag & drop renkleri
        if (isDraggingFromPanel)
        {
            if (isBeingDragged)
            {
                return new Color(0.3f, 1f, 0.3f, 0.6f); // Yeşil - sürüklenen (seçili)
            }
            else if (isHovering)
            {
                return new Color(0.3f, 0.6f, 1f, 0.7f); // Mavi - üzerine gelinen (hedef)
            }
            else
            {
                return new Color(0.5f, 0.5f, 0.5f, 0.2f); // Gri - diğer alanlar
            }
        }
        
        // Hierarchy'den tıklama renkleri
        if (isClickable && isHovering)
        {
            return new Color(0.5f, 1f, 0.5f, 0.5f); // Açık yeşil - hierarchy'den tıklanabilir
        }
        else if (isSelected)
        {
            return new Color(0.4f, 0.7f, 1f, 0.3f); // Açık mavi - hierarchy'de seçili
        }
        
        return new Color(0f, 0f, 0f, 0f); // Şeffaf - normal
    }
    
    private Texture2D MakeTex(int width, int height, Color col)
    {
        Color[] pix = new Color[width * height];
        for (int i = 0; i < pix.Length; i++)
        {
            pix[i] = col;
        }
        
        Texture2D result = new Texture2D(width, height);
        result.SetPixels(pix);
        result.Apply();
        return result;
    }
}
