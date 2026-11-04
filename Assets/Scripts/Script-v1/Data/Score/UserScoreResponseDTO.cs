using System;
using System.Collections.Generic;
using UnityEngine;

namespace EdenFarm.Score.DTO
{
    /// <summary>
    /// Data Transfer Object untuk menangkap respon profil skor dan statistik pemain dari GET /api/score/user/:userId.
    /// </summary>
    [Serializable]
    public class UserScoreResponseDTO
    {
        public string status;
        public string message;
        public UserScoreDetailDTO data;

        public bool IsSuccess => string.Equals(status, "success", StringComparison.OrdinalIgnoreCase);

        public static UserScoreResponseDTO FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                return JsonUtility.FromJson<UserScoreResponseDTO>(json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UserScoreResponseDTO] Gagal deserialisasi JSON: {ex.Message}\nRaw: {json}");
                return null;
            }
        }
    }

    [Serializable]
    public class UserScoreDetailDTO
    {
        public int userId;
        public string username;
        public string nickname;
        public int highestScore;
        public int cumulativeScore;
        public string rank;
        public int tierLevel;
        public string badgeIconUrl;
        public UserScoreStatsSummaryDTO stats;
        public List<ScoreMatchRecordDTO> recentMatches;
    }

    [Serializable]
    public class UserScoreStatsSummaryDTO
    {
        public int farmsManaged;
        public int totalOrdersCompleted;
        public int totalMatchesPlayed;
    }

    [Serializable]
    public class ScoreMatchRecordDTO
    {
        public int id;
        public string sessionId;
        public int score;
        public int cropsHarvested;
        public int ordersLoaded;
        public string recordedAt;
    }
}
