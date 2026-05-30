using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Woi.UI.Announcements;

namespace Woi.OfficeFire.Editor
{
    public static class OfficeFireScenarioContentSetup
    {
        const string KitchenAssetPath =
            "Assets/Project/OfficeFire/ScriptableObjects/KitchenCafe/Content/KitchenCafeScenarioContentDatabase.asset";

        const string ArchiveAssetPath =
            "Assets/Project/OfficeFire/ScriptableObjects/ArchiveRoom/Content/ArchiveRoomScenarioContentDatabase.asset";

        const string ServerAssetPath =
            "Assets/Project/OfficeFire/ScriptableObjects/ServerRoom/Content/ServerRoomScenarioContentDatabase.asset";

        [MenuItem("Woi/Office Fire/Create And Wire Scenario Content Databases")]
        public static void CreateAndWireAll()
        {
            KitchenCafeScenarioContentDatabase kitchenDb = CreateOrLoadKitchenDatabase();
            OfficeFireVoiceLineContentDatabase archiveDb = CreateOrLoadVoiceDatabase(
                ArchiveAssetPath,
                OfficeFireScenarioId.ArchiveRoom);
            OfficeFireVoiceLineContentDatabase serverDb = CreateOrLoadVoiceDatabase(
                ServerAssetPath,
                OfficeFireScenarioId.ServerRoom);

            OfficeFireScenarioContentDatabaseSync.SyncServerFromArchive();
            serverDb = AssetDatabase.LoadAssetAtPath<OfficeFireVoiceLineContentDatabase>(ServerAssetPath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            for (int i = 0; i < SceneManager.loadedSceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded)
                {
                    continue;
                }

                WireScene(scene, kitchenDb, archiveDb, serverDb);
            }

            Debug.Log(
                "[OfficeFire] Scenario content databases created/updated and wired.\n" +
                $"- Kitchen: {KitchenAssetPath}\n" +
                $"- Archive: {ArchiveAssetPath}\n" +
                $"- Server: {ServerAssetPath}");
        }

        static KitchenCafeScenarioContentDatabase CreateOrLoadKitchenDatabase()
        {
            var existing = AssetDatabase.LoadAssetAtPath<KitchenCafeScenarioContentDatabase>(KitchenAssetPath);
            if (existing != null)
            {
                existing.EditorEnsureAllDefaults();
                EditorUtility.SetDirty(existing);
                return existing;
            }

            EnsureFolder("Assets/Project/OfficeFire/ScriptableObjects/KitchenCafe/Content");
            var db = ScriptableObject.CreateInstance<KitchenCafeScenarioContentDatabase>();
            db.EditorEnsureAllDefaults();
            AssetDatabase.CreateAsset(db, KitchenAssetPath);
            return db;
        }

        static OfficeFireVoiceLineContentDatabase CreateOrLoadVoiceDatabase(
            string path,
            OfficeFireScenarioId scenarioId)
        {
            var existing = AssetDatabase.LoadAssetAtPath<OfficeFireVoiceLineContentDatabase>(path);
            if (existing != null)
            {
                existing.EditorSetScenario(scenarioId);
                existing.EditorFillForAssignedScenario();
                EditorUtility.SetDirty(existing);
                return existing;
            }

            string folder = path.Substring(0, path.LastIndexOf('/'));
            EnsureFolder(folder);
            var db = ScriptableObject.CreateInstance<OfficeFireVoiceLineContentDatabase>();
            db.EditorSetScenario(scenarioId);
            db.EditorFillForAssignedScenario();
            AssetDatabase.CreateAsset(db, path);
            return db;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = System.IO.Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, name);
        }

        static void WireScene(
            Scene scene,
            KitchenCafeScenarioContentDatabase kitchenDb,
            OfficeFireVoiceLineContentDatabase archiveDb,
            OfficeFireVoiceLineContentDatabase serverDb)
        {
            Transform contentRoot = FindChildByName(scene, "02_Content");
            if (contentRoot == null)
            {
                return;
            }

            WireKitchen(contentRoot, kitchenDb);
            WireVoicePresenter(
                contentRoot,
                "ArchiveRoomContentPresenter",
                archiveDb,
                FindController<ArchiveRoomScenarioController>(scene));
            WireVoicePresenter(
                contentRoot,
                "ServerRoomContentPresenter",
                serverDb,
                FindController<ServerRoomScenarioController>(scene));

            EditorSceneManager.MarkSceneDirty(scene);
        }

        static void WireKitchen(Transform contentRoot, KitchenCafeScenarioContentDatabase kitchenDb)
        {
            KitchenCafeContentPresenter presenter = FindOrCreatePresenter<KitchenCafeContentPresenter>(
                contentRoot,
                "KitchenCafeContentPresenter");
            if (presenter == null || kitchenDb == null)
            {
                return;
            }

            Undo.RecordObject(presenter, "Wire Kitchen Content Presenter");
            SerializedObject presenterSo = new SerializedObject(presenter);
            presenterSo.FindProperty("database").objectReferenceValue = kitchenDb;
            EnsureAnnouncementAudioAdapter(presenter.gameObject, presenterSo.FindProperty("announcementAudioAdapter"));
            presenterSo.ApplyModifiedProperties();

            KitchenCafeScenarioController kitchenController = FindController<KitchenCafeScenarioController>(
                presenter.gameObject.scene);
            if (kitchenController == null)
            {
                return;
            }

            if (!HasPersistentListener(kitchenController.OnContentCueRequested, presenter, nameof(KitchenCafeContentPresenter.PlayContentCue)))
            {
                UnityEventTools.AddPersistentListener(
                    kitchenController.OnContentCueRequested,
                    presenter.PlayContentCue);
            }

            EditorUtility.SetDirty(kitchenController);
        }

        static void WireVoicePresenter<TController>(
            Transform contentRoot,
            string presenterObjectName,
            OfficeFireVoiceLineContentDatabase database,
            TController controller)
            where TController : OfficeFireScenarioController
        {
            if (database == null)
            {
                return;
            }

            OfficeFireVoiceLineContentPresenter presenter = FindOrCreatePresenter<OfficeFireVoiceLineContentPresenter>(
                contentRoot,
                presenterObjectName);
            if (presenter == null)
            {
                return;
            }

            Undo.RecordObject(presenter, "Wire Voice Line Content Presenter");
            SerializedObject presenterSo = new SerializedObject(presenter);
            presenterSo.FindProperty("database").objectReferenceValue = database;
            EnsureAnnouncementAudioAdapter(presenter.gameObject, presenterSo.FindProperty("announcementAudioAdapter"));
            presenterSo.ApplyModifiedProperties();

            if (controller == null)
            {
                return;
            }

            if (!HasPersistentListener(controller.OnAnnouncementRequested, presenter, nameof(OfficeFireVoiceLineContentPresenter.PlayVoiceLine)))
            {
                UnityEventTools.AddPersistentListener(
                    controller.OnAnnouncementRequested,
                    presenter.PlayVoiceLine);
            }

            EditorUtility.SetDirty(controller);
        }

        static bool HasPersistentListener(UnityEngine.Events.UnityEventBase unityEvent, Object target, string methodName)
        {
            if (unityEvent == null || target == null || string.IsNullOrEmpty(methodName))
            {
                return false;
            }

            int count = unityEvent.GetPersistentEventCount();
            for (int i = 0; i < count; i++)
            {
                if (unityEvent.GetPersistentTarget(i) == target &&
                    unityEvent.GetPersistentMethodName(i) == methodName)
                {
                    return true;
                }
            }

            return false;
        }

        static void EnsureAnnouncementAudioAdapter(GameObject host, SerializedProperty adapterProp)
        {
            if (adapterProp == null || adapterProp.objectReferenceValue != null)
            {
                return;
            }

            WoiAnnouncementAudioAdapter adapter = host.GetComponent<WoiAnnouncementAudioAdapter>();
            if (adapter == null)
            {
                adapter = Undo.AddComponent<WoiAnnouncementAudioAdapter>(host);
            }

            adapterProp.objectReferenceValue = adapter;
        }

        static TPresenter FindOrCreatePresenter<TPresenter>(Transform contentRoot, string objectName)
            where TPresenter : Component
        {
            Transform child = contentRoot.Find(objectName);
            if (child == null)
            {
                var go = new GameObject(objectName);
                Undo.RegisterCreatedObjectUndo(go, "Create content presenter");
                go.transform.SetParent(contentRoot, false);
                child = go.transform;
            }

            TPresenter presenter = child.GetComponent<TPresenter>();
            if (presenter == null)
            {
                presenter = Undo.AddComponent<TPresenter>(child.gameObject);
            }

            return presenter;
        }

        static TController FindController<TController>(Scene scene)
            where TController : Object
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                TController[] found = root.GetComponentsInChildren<TController>(true);
                if (found.Length > 0)
                {
                    return found[0];
                }
            }

            return null;
        }

        static Transform FindChildByName(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == name)
                {
                    return root.transform;
                }

                Transform[] all = root.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i].name == name)
                    {
                        return all[i];
                    }
                }
            }

            return null;
        }
    }
}
