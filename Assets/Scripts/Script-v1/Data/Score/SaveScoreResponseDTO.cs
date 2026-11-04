using System;
using UnityEngine;

namespace EdenFarm.Score.DTO
{
    /// <summary>
    /// Data Transfer Object untuk menangkap respon setelah skor berhasil disimpan di backend.
    /// </summary>
    [Serializable]
    public class SaveScoreResponseDTO
    {
        public string status;
        public string message;
        public int scoreRecordId;
        public int addedScore;
        public UpdatedPlayerStatsDTO playerStats;

        public bool IsSuccess => string.Equals(status, "success", StringComparison.OrdinalIgnoreCase);

        public static SaveScoreResponseDTO FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                return JsonUtility.FromJson<SaveScoreResponseDTO>(json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveScoreResponseDTO] Gagal deserialisasi JSON: {ex.Message}\nRaw: {json}");
                return null;
            }
        }
    }

    [Serializable]
    public class UpdatedPlayerStatsDTO
    {
        public int userId;
        public string username;
        public string nickname;
        public int highestScore;
        public int farmsManaged;
        public int totalOrdersCompleted;
        public string currentRank;
        public int tierLevel;
    }
}
