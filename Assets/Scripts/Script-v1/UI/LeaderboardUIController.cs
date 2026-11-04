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
    /// Controller UI Leaderboard & Statistik Pemain di Scene Lobby.
    /// Mengikuti kaidah Canvas Controller: Controller Canvas tetap aktif, hanya child panel
    /// (modalRoot) yang di-toggle SetActive(true/false).
    /// </summary>
    public class LeaderboardUIController : MonoBehaviour
    {
        public static LeaderboardUIController Instance { get; private set; }

        [Header("Modal Popup Panel (Child)")]
        [Tooltip("Root GameObject dari modal popup (di-toggle SetActive)")]
        [SerializeField] private GameObject modalPopupRoot;
        [SerializeField] private Button closeButton;

        [Header("Tab Controls")]
        [SerializeField] private Button tabLeaderboardButton;
        [SerializeField] private Button tabMyStatsButton;
        [SerializeField] private GameObject leaderboardContainer;
        [SerializeField] private GameObject myStatsContainer;

        [Header("Leaderboard List View")]
        [SerializeField] private Transform leaderboardContentList;
        [SerializeField] private GameObject leaderboardRowPrefab; // Optional prefab fallback
        [SerializeField] private TextMeshProUGUI leaderboardStatusText;

        [Header("My Stats Text Fields")]
        [SerializeField] private TextMeshProUGUI statPlayerNameText;
        [SerializeField] private TextMeshProUGUI statHighestScoreText;
        [SerializeField] private TextMeshProUGUI statCumulativeScoreText;
        [SerializeField] private TextMeshProUGUI statRankTierText;
        [SerializeField] private TextMeshProUGUI statOrdersCompletedText;
        [SerializeField] private TextMeshProUGUI statMatchesPlayedText;
        [SerializeField] private Transform recentMatchesContentList;

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

            // Default: Modal tertutup saat scene dimuat
            if (modalPopupRoot != null)
            {
                modalPopupRoot.SetActive(false);
            }
        }

        private void Start()
        {
            if (closeButton != null) closeButton.onClick.AddListener(CloseModal);
            if (tabLeaderboardButton != null) tabLeaderboardButton.onClick.AddListener(ShowLeaderboardTab);
            if (tabMyStatsButton != null) tabMyStatsButton.onClick.AddListener(ShowMyStatsTab);
        }

        public void OpenModal()
        {
            if (modalPopupRoot != null) modalPopupRoot.SetActive(true);
            ShowLeaderboardTab();
        }

        public void CloseModal()
        {
            if (modalPopupRoot != null) modalPopupRoot.SetActive(false);
        }

        public void ShowLeaderboardTab()
        {
            if (leaderboardContainer != null) leaderboardContainer.SetActive(true);
            if (myStatsContainer != null) myStatsContainer.SetActive(false);

            FetchLeaderboardData();
        }

        public void ShowMyStatsTab()
        {
            if (leaderboardContainer != null) leaderboardContainer.SetActive(false);
            if (myStatsContainer != null) myStatsContainer.SetActive(true);

            FetchMyStatsData();
        }

        private void FetchLeaderboardData()
        {
            if (leaderboardStatusText != null)
            {
                leaderboardStatusText.gameObject.SetActive(true);
                leaderboardStatusText.text = "Memuat data papan peringkat...";
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
                    leaderboardStatusText.text = "Belum ada catatan skor tercatat.";
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
                    // Fallback runtime text jika prefab belum dislot di inspector
                    rowGo = new GameObject($"Row_{entry.position}", typeof(RectTransform));
                    rowGo.transform.SetParent(leaderboardContentList, false);
                    var txt = rowGo.AddComponent<TextMeshProUGUI>();
                    txt.fontSize = 20;
                    txt.color = (i == 0) ? Color.yellow : Color.white;
                    txt.text = $"#{entry.position}  <b>{entry.nickname}</b>  -  {entry.score} Poin  ({entry.rank})";
                    continue;
                }

                // Jika prefab memiliki TextMeshProUGUI
                var label = rowGo.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null)
                {
                    label.text = $"#{entry.position}  {entry.nickname}  -  {entry.score} pts  [{entry.rank}]";
                }
            }
        }

        private void FetchMyStatsData()
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

                if (statPlayerNameText != null) statPlayerNameText.text = $"{data.nickname} (@{data.username})";
                if (statHighestScoreText != null) statHighestScoreText.text = $"{data.highestScore} Poin";
                if (statCumulativeScoreText != null) statCumulativeScoreText.text = $"{data.cumulativeScore} Poin";
                if (statRankTierText != null) statRankTierText.text = $"{data.rank} (Tier {data.tierLevel})";

                if (data.stats != null)
                {
                    if (statOrdersCompletedText != null) statOrdersCompletedText.text = $"{data.stats.totalOrdersCompleted}";
                    if (statMatchesPlayedText != null) statMatchesPlayedText.text = $"{data.stats.totalMatchesPlayed}";
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
                var txt = itemGo.AddComponent<TextMeshProUGUI>();
                txt.fontSize = 16;
                txt.color = Color.white;
                txt.text = $"• Skor: +{match.score}  |  Hasil Tani: {match.cropsHarvested}  |  Order: {match.ordersLoaded}  ({match.recordedAt})";
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
    }
}
