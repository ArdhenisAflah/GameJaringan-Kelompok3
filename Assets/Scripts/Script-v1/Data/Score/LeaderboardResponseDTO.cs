using System;
using System.Collections.Generic;
using UnityEngine;

namespace EdenFarm.Score.DTO
{
    /// <summary>
    /// Data Transfer Object untuk menangkap respon papan peringkat (Top 10) dari GET /api/leaderboard.
    /// </summary>
    [Serializable]
    public class LeaderboardResponseDTO
    {
        public string status;
        public string message;
        public int totalEntries;
        public List<LeaderboardEntryDTO> data;

        public bool IsSuccess => string.Equals(status, "success", StringComparison.OrdinalIgnoreCase);

        public static LeaderboardResponseDTO FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                return JsonUtility.FromJson<LeaderboardResponseDTO>(json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LeaderboardResponseDTO] Gagal deserialisasi JSON: {ex.Message}\nRaw: {json}");
                return null;
            }
        }
    }

    [Serializable]
    public class LeaderboardEntryDTO
    {
        public int position;
        public int userId;
        public string username;
        public string nickname;
        public int score;
        public string rank;
        public int tierLevel;
        public string badgeIconUrl;
        public LeaderboardEntryStatsDTO stats;
    }

    [Serializable]
    public class LeaderboardEntryStatsDTO
    {
        public int ordersCompleted;
        public int farmsManaged;
    }
}
