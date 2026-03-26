using System;
using UnityEngine;
using UnityEngine.UIElements;
using Sirenix.OdinInspector;

namespace Woi.Leaderboard
{
    /// <summary>
    /// Attach to a UIDocument GameObject in the Bootstrapper scene.
    /// Reads LeaderboardService.GetTop10() and populates the leaderboard panel.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class LeaderboardUIController : MonoBehaviour
    {
        [Tooltip("How often (seconds) the leaderboard auto-refreshes. 0 = only on Start.")]
        [SerializeField] private float refreshInterval = 5f;

        private UIDocument _document;
        private ScrollView _playerList;
        private Label _headerSubtitle;

        private float _refreshTimer;

        void Awake()
        {
            _document = GetComponent<UIDocument>();
        }

        void Start()
        {
            var root = _document.rootVisualElement;
            _playerList      = root.Q<ScrollView>("player-list");
            _headerSubtitle  = root.Q<Label>("header-subtitle");

            Refresh();
        }

        void Update()
        {
            if (refreshInterval <= 0f) return;

            _refreshTimer += Time.deltaTime;
            if (_refreshTimer >= refreshInterval)
            {
                _refreshTimer = 0f;
                Refresh();
            }
        }

        // ─────────────────────────────────────────────────────
        //  Public API — call after submitting a new score
        // ─────────────────────────────────────────────────────
        public void Refresh()
        {
            if (_playerList == null) return;

            _playerList.Clear();

            LeaderboardEntry[] entries = LeaderboardService.GetTop10();

            if (_headerSubtitle != null)
                _headerSubtitle.text = entries.Length > 0 ? $"TOP {entries.Length} PLAYERS" : "NO SCORES YET";

            if (entries.Length == 0)
            {
                _playerList.Add(BuildEmptyState());
                return;
            }

            for (int i = 0; i < entries.Length; i++)
                _playerList.Add(BuildRow(i + 1, entries[i]));
        }

        // ─────────────────────────────────────────────────────
        //  Row builder
        // ─────────────────────────────────────────────────────
        private VisualElement BuildRow(int rank, LeaderboardEntry entry)
        {
            // ── player-item ──
            var item = new VisualElement();
            item.AddToClassList("player-item");

            // ── Left side ──
            var left = new VisualElement();
            left.AddToClassList("player-info-left");

            var rankBadge = new Label(rank.ToString());
            rankBadge.AddToClassList("rank-badge");
            rankBadge.AddToClassList(RankClass(rank));
            rankBadge.pickingMode = PickingMode.Ignore;
            left.Add(rankBadge);

            var details = new VisualElement();
            details.AddToClassList("player-details");

            var playerName = new Label(entry.UserName ?? "—");
            playerName.AddToClassList("player-name");
            playerName.pickingMode = PickingMode.Ignore;

            var playerId = new Label($"ID: {entry.UserId ?? "—"}");
            playerId.AddToClassList("player-id");
            playerId.pickingMode = PickingMode.Ignore;

            details.Add(playerName);
            details.Add(playerId);
            left.Add(details);

            // ── Right side ──
            var right = new VisualElement();
            right.AddToClassList("player-info-right");

            var score = new Label(entry.BestScore.ToString());
            score.AddToClassList("player-score");
            score.pickingMode = PickingMode.Ignore;

            var pts = new Label("pts");
            pts.AddToClassList("player-pts");
            pts.pickingMode = PickingMode.Ignore;

            right.Add(score);
            right.Add(pts);

            item.Add(left);
            item.Add(right);

            return item;
        }

        private VisualElement BuildEmptyState()
        {
            var wrapper = new VisualElement();
            wrapper.AddToClassList("empty-state");

            var icon = new Label("◇");
            icon.AddToClassList("empty-icon");
            icon.pickingMode = PickingMode.Ignore;

            var text = new Label("NO SCORES YET");
            text.AddToClassList("empty-text");
            text.pickingMode = PickingMode.Ignore;

            wrapper.Add(icon);
            wrapper.Add(text);
            return wrapper;
        }

        // ─────────────────────────────────────────────────────
        //  Helpers
        // ─────────────────────────────────────────────────────
        private static string RankClass(int rank) => rank switch
        {
            1 => "rank-1",
            2 => "rank-2",
            3 => "rank-3",
            _ => "rank-normal"
        };

        [Button]
            public void ResetLeaderboard()
            {
                LeaderboardService.ResetLeaderboard();
                Debug.Log("Leaderboard resetted.");
            }
    }
}
