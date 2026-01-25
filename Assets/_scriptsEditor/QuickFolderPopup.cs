#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
// Mini popup pencere - M tuşu ile açılır

public class QuickFolderPopup : EditorWindow
{
    private string folderName = "New Folder";
    private GameObject[] objectsToMove;
    
    public static void ShowWindow(GameObject[] objects)
    {
        if (objects == null || objects.Length == 0)
            return;
            
        var window = CreateInstance<QuickFolderPopup>();
        window.objectsToMove = objects;
        window.titleContent = new GUIContent("Create Folder");
        
        // Mouse pozisyonunda aç
        Vector2 mousePos = GUIUtility.GUIToScreenPoint(Event.current.mousePosition);
        window.position = new Rect(mousePos.x - 150, mousePos.y - 25, 300, 100);
        
        window.ShowUtility(); // ShowPopup yerine ShowUtility - taşınabilir
    }
    
   private void OnGUI()
{
    // 1. Klavye Dinleyici (Enter ve Esc için)
    // En tepede olması, diğer elementler etkileşime girmeden yakalamasını sağlar.
    Event e = Event.current;
    if (e.type == EventType.KeyDown)
    {
        if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
        {
            CreateFolderAndMove(); // Klasörü oluştur ve objeleri taşı
            Close();               // Pencereyi kapat
            e.Use();               // Event'i tüket
            return;
        }
        else if (e.keyCode == KeyCode.Escape)
        {
            Close();               // İptal et ve kapat
            e.Use();
            return;
        }
    }

    EditorGUILayout.Space(10);
    
    // Değişken adın: folderName
    EditorGUILayout.LabelField($"📁 Folder name for {objectsToMove.Length} objects:", EditorStyles.boldLabel);
    EditorGUILayout.Space(5);

    // 2. Yazı Alanı ve Odaklanma
    GUI.SetNextControlName("FolderNameField");
    folderName = EditorGUILayout.TextField(folderName); // Senin değişkenin burada

    // Pencere açıldığında imleci otomatik içine koyar
    if (Event.current.type == EventType.Layout)
        EditorGUI.FocusTextInControl("FolderNameField");

    EditorGUILayout.Space(10);

    // 3. Butonlar
    EditorGUILayout.BeginHorizontal();
    GUILayout.FlexibleSpace();

    if (GUILayout.Button("✓ Create", GUILayout.Width(120), GUILayout.Height(30)))
    {
        CreateFolderAndMove();
        Close();
    }

    if (GUILayout.Button("✗ Cancel", GUILayout.Width(120), GUILayout.Height(30)))
    {
        Close();
    }

    GUILayout.FlexibleSpace();
    EditorGUILayout.EndHorizontal();
    EditorGUILayout.Space(5);
}
    
private void CreateFolderAndMove()
{
    if (string.IsNullOrWhiteSpace(folderName))
        folderName = "New Folder";

    var list = objectsToMove?.Where(o => o != null).Distinct().ToList() ?? new List<GameObject>();
    if (list.Count == 0) return;

    // 1. Gereksiz alt objeleri temizle (Parent seçiliyse child'ı işleme alma)
    var set = new HashSet<Transform>(list.Select(o => o.transform));
    list = list.Where(o =>
    {
        var p = o.transform.parent;
        while (p != null)
        {
            if (set.Contains(p)) return false;
            p = p.parent;
        }
        return true;
    }).ToList();

    // 2. UNDO GRUBU BAŞLAT
    // Bu sayede tek Ctrl+Z ile hem klasör silinir hem objeler eski yerine döner.
    Undo.IncrementCurrentGroup();
    Undo.SetCurrentGroupName("Create Folder and Move");
    int group = Undo.GetCurrentGroup();

    // 3. KLASÖRÜ OLUŞTUR
    GameObject folder = new GameObject(folderName);
    
    // ÖNEMLİ: Eğer seçili objelerin bir parent'ı varsa, yeni klasörü de orada oluştur.
    if (list[0].transform.parent != null) {
        folder.transform.SetParent(list[0].transform.parent, false);
    }

    // Klasörün oluşturulmasını kaydet
    Undo.RegisterCreatedObjectUndo(folder, "Create Folder");

    // 4. OBJELERİ TAŞI
    foreach (var obj in list)
    {
        // Undo.SetTransformParent, objenin eski parent'ını ve pozisyonunu hafızaya alır.
        Undo.SetTransformParent(obj.transform, folder.transform, "Move To Folder");
    }

    // 5. GRUBU KAPAT
    Undo.CollapseUndoOperations(group);

    Selection.activeGameObject = folder;

    // Arayüzü tazele
    EditorApplication.RepaintHierarchyWindow();
}
}
#endif