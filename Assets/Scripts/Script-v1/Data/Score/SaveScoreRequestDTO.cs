using System;
using UnityEngine;

namespace EdenFarm.Score.DTO
{
    /// <summary>
    /// Data Transfer Object untuk mengirimkan perolehan skor match pemain ke POST /api/score/save
    /// atau PUT /api/score/user/:userId.
    /// </summary>
    [Serializable]
    public class SaveScoreRequestDTO
    {
        [Tooltip("Identifier unik akun pemain di backend")]
        public int userId;

        [Tooltip("Skor perolehan match yang dicapai")]
        public int score;

        [Tooltip("Jumlah order truk yang berhasil diselesaikan pada match")]
        public int ordersCompleted;

        [Tooltip("Jumlah order yang gagal/waktunya habis")]
        public int ordersFailed;

        [Tooltip("Jumlah hasil tani/padi yang dipanen selama match")]
        public int cropsHarvested;

        [Tooltip("Jumlah muatan panen yang dimasukkan ke dalam truk")]
        public int ordersLoaded;

        [Tooltip("Identifier unik sesi match (Lobby/Match ID)")]
        public string sessionId;

        public SaveScoreRequestDTO() { }

        public SaveScoreRequestDTO(int userId, int score, int ordersCompleted = 0, int ordersFailed = 0, int cropsHarvested = 0, int ordersLoaded = 0, string sessionId = null)
        {
            this.userId = userId;
            this.score = score;
            this.ordersCompleted = ordersCompleted;
            this.ordersFailed = ordersFailed;
            this.cropsHarvested = cropsHarvested;
            this.ordersLoaded = ordersLoaded;
            this.sessionId = sessionId;
        }

        public string ToJson()
        {
            return JsonUtility.ToJson(this);
        }
    }
}
