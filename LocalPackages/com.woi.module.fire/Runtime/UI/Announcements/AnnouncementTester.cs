using UnityEngine;
using Woi.UI.Announcements;

namespace Woi.UI.Announcements
{
    /// <summary>
    /// Keyboard demo for <see cref="AnnouncementService"/> (slots 0–3 → keys Alpha1–4).
    /// Configure lists with audio-only, popup-only, combined, and interrupt scenarios.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Woi/UI/Tests/Announcement Tester")]
    public sealed class AnnouncementTester : MonoBehaviour
    {
        [SerializeField] private AnnouncementService announcementService;

        [SerializeField] private AnnouncementDefinition[] announcements = new AnnouncementDefinition[4];

        [Header("Keys (slot index + 1)")]
        [SerializeField] private KeyCode slot1 = KeyCode.Alpha1;
        [SerializeField] private KeyCode slot2 = KeyCode.Alpha2;
        [SerializeField] private KeyCode slot3 = KeyCode.Alpha3;
        [SerializeField] private KeyCode slot4 = KeyCode.Alpha4;

        private void Awake()
        {
            if (announcementService == null)
                announcementService = FindFirstObjectByType<AnnouncementService>();
        }

        private void Update()
        {
            if (announcementService == null)
                return;

            if (Input.GetKeyDown(slot1))
                Play(0);

            if (Input.GetKeyDown(slot2))
                Play(1);

            if (Input.GetKeyDown(slot3))
                Play(2);

            if (Input.GetKeyDown(slot4))
                Play(3);
        }

        private void Play(int index)
        {
            if (announcements == null || index < 0 || index >= announcements.Length)
                return;

            AnnouncementDefinition def = announcements[index];
            if (def == null)
                return;

            announcementService.Play(def);
        }
    }
}
