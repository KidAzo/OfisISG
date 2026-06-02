#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Woi.WasteCollectionMode.Editor
{
    public static class WasteLeaderboardEditorTools
    {
        [MenuItem("Waste Collection/Clear Leaderboard")]
        public static void ClearLeaderboard()
        {
            bool confirmed = EditorUtility.DisplayDialog(
                "Clear Leaderboard",
                "Tüm leaderboard verisi silinecek. Bu işlem geri alınamaz.\n\nDevam edilsin mi?",
                "Sil",
                "Vazgeç");

            if (!confirmed)
                return;

            WasteLeaderboardStore.Clear();
            Debug.Log("[WasteLeaderboardStore] Leaderboard temizlendi.");
        }
    }
}
#endif
