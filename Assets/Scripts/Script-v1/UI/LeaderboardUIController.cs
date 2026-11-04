using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using EdenFarm.Auth.Session;
using EdenFarm.Score.DTO;
using EdenFarm.Score.Networking;

namespace EdenFarm.Score.UI
{
    /// <summary>
    /// Controller UI Panel Leaderboard & Statistik Pemain di Scene Lobby.
    /// Tampil langsung sebagai panel berdampingan dengan Lobby Browser Panel (bukan pop-up modal).
    /// Otomatis mengonsumsi REST API Node.js backend saat scene Lobby dimuat atau saat tombol refresh ditekan.
    /// </summary>
    public class LeaderboardUIController : MonoBehaviour
    {
        public static LeaderboardUIController Instance { get; private set; }

        [Header("Main Panel Root")]
        [Tooltip("Root GameObject panel leaderboard (selalu aktif berdampingan dengan Lobby)")]
        [SerializeField] private GameObject panelRoot;

        [Header("Tab Controls")]
        [SerializeField] private Button tabLeaderboardButton;
        [SerializeField] private Button tabMyStatsButton;
        [SerializeField] private Button refreshButton;
        [SerializeField] private GameObject leaderboardContainer;
        [SerializeField] private GameObject myStatsContainer;

        [Header("Leaderboard List View")]
        [SerializeField] private Transform leaderboardContentList;
        [SerializeField] private GameObject leaderboardRowPrefab; // Optional prefab
        [SerializeField] private TextMeshProUGUI leaderboardStatusText;

        [Header("My Stats Text Fields")]
        [SerializeField] private TextMeshProUGUI statPlayerNameText;
        [SerializeField] private TextMeshProUGUI statHighestScoreText;
        [SerializeField] private TextMeshProUGUI statCumulativeScoreText;
        [SerializeField] private TextMeshProUGUI statRankTierText;
        [SerializeField] private TextMeshProUGUI statOrdersCompletedText;
        [SerializeField] private TextMeshProUGUI statMatchesPlayedText;
        [SerializeField] private Transform recentMatchesContentList;

        private enum TabMode { Leaderboard, MyStats }
        private TabMode _currentTab = TabMode.Leaderboard;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            if (panelRoot == null)
            {
                panelRoot = gameObject;
            }

            AutoDiscoverReferences();
        }

        private void Start()
        {
            if (tabLeaderboardButton != null) tabLeaderboardButton.onClick.AddListener(ShowLeaderboardTab);
            if (tabMyStatsButton != null) tabMyStatsButton.onClick.AddListener(ShowMyStatsTab);
            if (refreshButton != null) refreshButton.onClick.AddListener(RefreshCurrentTab);

            // Buka tab default dan muat data dari API backend
            ShowLeaderboardTab();
        }

        public void SetPanelVisible(bool visible)
        {
            if (panelRoot != null)
            {
                panelRoot.SetActive(visible);
            }
        }

        public void RefreshCurrentTab()
        {
            if (_currentTab == TabMode.Leaderboard)
            {
                FetchLeaderboardData();
            }
            else
            {
                FetchMyStatsData();
            }
        }

        public void ShowLeaderboardTab()
        {
            _currentTab = TabMode.Leaderboard;

            if (leaderboardContainer != null) leaderboardContainer.SetActive(true);
            if (myStatsContainer != null) myStatsContainer.SetActive(false);

            UpdateTabButtonVisuals(tabLeaderboardButton, tabMyStatsButton);
            FetchLeaderboardData();
        }

        public void ShowMyStatsTab()
        {
            _currentTab = TabMode.MyStats;

            if (leaderboardContainer != null) leaderboardContainer.SetActive(false);
            if (myStatsContainer != null) myStatsContainer.SetActive(true);

            UpdateTabButtonVisuals(tabMyStatsButton, tabLeaderboardButton);
            FetchMyStatsData();
        }

        private void UpdateTabButtonVisuals(Button activeBtn, Button inactiveBtn)
        {
            if (activeBtn != null)
            {
                var img = activeBtn.GetComponent<Image>();
                if (img != null) img.color = new Color(0.2f, 0.45f, 0.8f, 1f);
            }
            if (inactiveBtn != null)
            {
                var img = inactiveBtn.GetComponent<Image>();
                if (img != null) img.color = new Color(0.18f, 0.22f, 0.32f, 1f);
            }
        }

        /// <summary>
        /// Mengonsumsi GET /api/leaderboard dari backend server Node.js.
        /// </summary>
        public void FetchLeaderboardData()
        {
            if (leaderboardStatusText != null)
            {
                leaderboardStatusText.gameObject.SetActive(true);
                leaderboardStatusText.text = "Memuat leaderboard dari server...";
            }

            EnsureScoreNetworkClient();
            ScoreNetworkClient.Instance.SendGetLeaderboard((response) =>
            {
                if (leaderboardStatusText != null) leaderboardStatusText.gameObject.SetActive(false);
                RenderLeaderboardRows(response.data);
            }, (error) =>
            {
                if (leaderboardStatusText != null)
                {
                    leaderboardStatusText.gameObject.SetActive(true);
                    leaderboardStatusText.text = $"<color=red>{error}</color>";
                }
            });
        }

        private void RenderLeaderboardRows(List<LeaderboardEntryDTO> entries)
        {
            if (leaderboardContentList == null) return;

            // Bersihkan baris lama
            foreach (Transform child in leaderboardContentList)
            {
                Destroy(child.gameObject);
            }

            if (entries == null || entries.Count == 0)
            {
                if (leaderboardStatusText != null)
                {
                    leaderboardStatusText.gameObject.SetActive(true);
                    leaderboardStatusText.text = "Belum ada pemain di papan peringkat.";
                }
                return;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                GameObject rowGo;

                if (leaderboardRowPrefab != null)
                {
                    rowGo = Instantiate(leaderboardRowPrefab, leaderboardContentList);
                }
                else
                {
                    rowGo = new GameObject($"Row_{entry.position}", typeof(RectTransform));
                    rowGo.transform.SetParent(leaderboardContentList, false);
                    var layout = rowGo.AddComponent<LayoutElement>();
                    layout.minHeight = 44;
                    layout.preferredHeight = 44;

                    var txt = rowGo.AddComponent<TextMeshProUGUI>();
                    txt.fontSize = 18;
                    txt.color = (i == 0) ? new Color(1f, 0.85f, 0.2f) : (i == 1) ? new Color(0.85f, 0.85f, 0.9f) : (i == 2) ? new Color(0.8f, 0.5f, 0.2f) : Color.white;
                    txt.alignment = TextAlignmentOptions.MidlineLeft;
                    txt.text = $"<b>#{entry.position}</b>  {entry.nickname}  <color=#88ccff>({entry.score} pts)</color>  <size=14><color=#aaaaaa>[{entry.rank}]</color></size>";
                    continue;
                }

                var label = rowGo.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null)
                {
                    label.text = $"#{entry.position}  {entry.nickname}  -  {entry.score} pts  [{entry.rank}]";
                }
            }
        }

        /// <summary>
        /// Mengonsumsi GET /api/score/user/:userId dari backend server Node.js.
        /// </summary>
        public void FetchMyStatsData()
        {
            var session = SessionManager.GetCurrentSession();
            int userId = session != null ? session.userId : 0;

            if (userId <= 0)
            {
                if (statPlayerNameText != null) statPlayerNameText.text = "Tamu (Belum Login)";
                return;
            }

            EnsureScoreNetworkClient();
            ScoreNetworkClient.Instance.SendGetUserScore(userId, (response) =>
            {
                var data = response.data;
                if (data == null) return;

                if (statPlayerNameText != null) statPlayerNameText.text = $"<b>{data.nickname}</b> (@{data.username})";
                if (statHighestScoreText != null) statHighestScoreText.text = $"{data.highestScore} Poin";
                if (statCumulativeScoreText != null) statCumulativeScoreText.text = $"{data.cumulativeScore} Poin";
                if (statRankTierText != null) statRankTierText.text = $"{data.rank} (Tier {data.tierLevel})";

                if (data.stats != null)
                {
                    if (statOrdersCompletedText != null) statOrdersCompletedText.text = $"{data.stats.totalOrdersCompleted} Order";
                    if (statMatchesPlayedText != null) statMatchesPlayedText.text = $"{data.stats.totalMatchesPlayed} Match";
                }

                RenderRecentMatches(data.recentMatches);
            }, (error) =>
            {
                Debug.LogWarning($"[LeaderboardUIController] Gagal memuat profil skor: {error}");
            });
        }

        private void RenderRecentMatches(List<ScoreMatchRecordDTO> matches)
        {
            if (recentMatchesContentList == null) return;

            foreach (Transform child in recentMatchesContentList)
            {
                Destroy(child.gameObject);
            }

            if (matches == null || matches.Count == 0) return;

            foreach (var match in matches)
            {
                GameObject itemGo = new GameObject($"Match_{match.id}", typeof(RectTransform));
                itemGo.transform.SetParent(recentMatchesContentList, false);
                var layout = itemGo.AddComponent<LayoutElement>();
                layout.minHeight = 32;

                var txt = itemGo.AddComponent<TextMeshProUGUI>();
                txt.fontSize = 15;
                txt.color = new Color(0.9f, 0.9f, 0.9f);
                txt.text = $"• Skor: <b>+{match.score}</b>  |  Panen: {match.cropsHarvested}  |  Order: {match.ordersLoaded}";
            }
        }

        private void EnsureScoreNetworkClient()
        {
            if (ScoreNetworkClient.Instance == null)
            {
                GameObject go = new GameObject("ScoreNetworkClient");
                go.AddComponent<ScoreNetworkClient>();
            }
        }

        private void AutoDiscoverReferences()
        {
            if (leaderboardContainer == null)
            {
                var t = transform.Find("LeaderboardContainer");
                if (t != null) leaderboardContainer = t.gameObject;
            }
            if (myStatsContainer == null)
            {
                var t = transform.Find("MyStatsContainer");
                if (t != null) myStatsContainer = t.gameObject;
            }
            if (leaderboardContentList == null && leaderboardContainer != null)
            {
                var t = leaderboardContainer.transform.Find("ScrollView/Viewport/Content");
                if (t != null) leaderboardContentList = t;
            }
            if (recentMatchesContentList == null && myStatsContainer != null)
            {
                var t = myStatsContainer.transform.Find("HistoryScrollView/Viewport/Content");
                if (t != null) recentMatchesContentList = t;
            }
        }
    }
}
