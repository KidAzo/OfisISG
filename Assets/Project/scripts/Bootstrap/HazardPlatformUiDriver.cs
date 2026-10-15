using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Scripting;

namespace Woi.Settings
{
    /// <summary>
    /// Turns PcUI on and VR gameplay canvases off for a PC Hub launch.
    /// Lives on the Office HazardSystem object so the HUB player can run it
    /// from <c>Woi.OfficeFire.Bootstrap</c> even when other Hazard scripts are absent.
    /// </summary>
    [Preserve]
    public sealed class HazardPlatformUiDriver : MonoBehaviour
    {
        [SerializeField] GameObject pcUiRoot;
        [SerializeField] GameObject[] vrGameplayUiRoots;
        [SerializeField] GameObject vrResultUiRoot;

        void Awake()
        {
            bool xr = HazardHubLaunch.UseVrPresentation;

            if (pcUiRoot != null)
                pcUiRoot.SetActive(!xr);

            if (vrGameplayUiRoots != null)
            {
                for (int i = 0; i < vrGameplayUiRoots.Length; i++)
                {
                    if (vrGameplayUiRoots[i] != null)
                        vrGameplayUiRoots[i].SetActive(xr);
                }
            }

            if (vrResultUiRoot != null)
                vrResultUiRoot.SetActive(false);

            DisableHubLeaderboard();
            CleanupLoginAfterGameplayStart();

            Debug.Log("[HazardPlatformUiDriver] vr=" + xr + " hub=" + HazardHubLaunch.IsHubLaunch + " platform=" + HazardHubLaunch.PlatformId + ".", this);
        }

        void DisableHubLeaderboard()
        {
            if (!HazardHubLaunch.IsHubLaunch || vrGameplayUiRoots == null)
                return;

            for (int i = 0; i < vrGameplayUiRoots.Length; i++)
            {
                GameObject root = vrGameplayUiRoots[i];
                if (root == null || root.name != "LeaderBoardVRPanel")
                    continue;

                root.SetActive(false);
                Debug.Log("[HazardStartup] Hub launch disabled Gameplay/UI/LeaderBoardVRPanel.", root);
                return;
            }
        }

        static readonly string[] LoginSceneNames = { "LoginScreen", "HazardHunt_Login" };

        static bool _loginCompleted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetLoginCompleted()
        {
            _loginCompleted = false;
        }

        /// <summary>
        /// Called when <c>HazardHuntLogged</c> is raised. The login screen has already
        /// collected name, ID, and language, so Office must not show its legacy start panel.
        /// </summary>
        public static void MarkLoginCompleted()
        {
            _loginCompleted = true;
        }

        static void CleanupLoginAfterGameplayStart()
        {
            Debug.Log(
                "[HazardLoginCleanup]\nstage=gameplay-loaded\nloginSceneLoaded=" + IsLoginSceneLoaded() +
                "\nloginUiActive=" + IsLoginUiActive());

            HideLegacyOfficeLoginIfHubLaunch();
            DeactivateLeftoverLoginScenes();

            Debug.Log(
                "[HazardLoginCleanup]\nstage=cleanup-complete\nloginSceneLoaded=" + IsLoginSceneLoaded() +
                "\nloginUiActive=" + IsLoginUiActive());
        }

        /// <summary>
        /// Office keeps the old in-scene start panel on VRCanvas (1): name, staff ID,
        /// language toggle, and START GAME. Its button has no handler. Hub collects that
        /// data on HazardHunt_Login and standalone on LoginScreen, so the panel is hidden
        /// once either login has raised <c>HazardHuntLogged</c>.
        /// </summary>
        public static void HideLegacyOfficeLoginIfHubLaunch()
        {
            if (!HazardHubLaunch.IsHubLaunch && !_loginCompleted)
                return;

            Transform[] transforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform panel = transforms[i];
                if (!IsLegacyOfficeLoginPanel(panel) || !panel.gameObject.activeSelf)
                    continue;

                panel.gameObject.SetActive(false);
                Debug.Log("[HazardStartup] Hid legacy Office login object " + panel.name + " (hub=" + HazardHubLaunch.IsHubLaunch + ").", panel);
            }
        }

        static bool IsLegacyOfficeLoginPanel(Transform panel)
        {
            return panel != null && panel.parent != null && panel.parent.name == "VRCanvas (1)"
                && (panel.name == "WebInfo" || panel.name == "WebInfoPanelController");
        }

        /// <summary>
        /// Office loads with LoadSceneMode.Single, which normally unloads the login scene.
        /// If a login scene is still loaded, its roots (UI, camera, EventSystem, AudioListener) are switched off.
        /// </summary>
        static void DeactivateLeftoverLoginScenes()
        {
            for (int i = 0; i < LoginSceneNames.Length; i++)
            {
                Scene scene = SceneManager.GetSceneByName(LoginSceneNames[i]);
                if (!scene.IsValid() || !scene.isLoaded || scene == SceneManager.GetActiveScene())
                    continue;

                GameObject[] roots = scene.GetRootGameObjects();
                for (int r = 0; r < roots.Length; r++)
                {
                    if (roots[r] != null && roots[r].activeSelf)
                        roots[r].SetActive(false);
                }

                Debug.Log("[HazardLoginCleanup] Deactivated leftover login scene " + scene.name + ".");
            }
        }

        static bool IsLoginSceneLoaded()
        {
            for (int i = 0; i < LoginSceneNames.Length; i++)
            {
                if (SceneManager.GetSceneByName(LoginSceneNames[i]).isLoaded)
                    return true;
            }

            return false;
        }

        static bool IsLoginUiActive()
        {
            Woi.UI.LoginScreenController[] logins = Object.FindObjectsByType<Woi.UI.LoginScreenController>(FindObjectsSortMode.None);
            for (int i = 0; i < logins.Length; i++)
            {
                if (logins[i] != null && logins[i].isActiveAndEnabled)
                    return true;
            }

            Transform[] transforms = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (IsLegacyOfficeLoginPanel(transforms[i]) && transforms[i].gameObject.activeInHierarchy)
                    return true;
            }

            return false;
        }
    }
}
