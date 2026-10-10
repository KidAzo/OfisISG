using UnityEngine;
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
            HideLegacyOfficeLoginIfHubLaunch();

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

        /// <summary>
        /// Office keeps the old in-scene start panel on VRCanvas (1): name, staff ID,
        /// language toggle, and START GAME. Hub already collected that on HazardHunt_Login.
        /// Standalone still shows the panel.
        /// </summary>
        public static void HideLegacyOfficeLoginIfHubLaunch()
        {
            if (!HazardHubLaunch.IsHubLaunch)
                return;

            Transform[] transforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform panel = transforms[i];
                if (panel == null || panel.parent == null || panel.parent.name != "VRCanvas (1)")
                    continue;

                if (panel.name != "WebInfo" && panel.name != "WebInfoPanelController")
                    continue;

                if (!panel.gameObject.activeSelf)
                    continue;

                panel.gameObject.SetActive(false);
                Debug.Log("[HazardStartup] Hub launch hid legacy Office login object " + panel.name + ".", panel);
            }
        }
    }
}
