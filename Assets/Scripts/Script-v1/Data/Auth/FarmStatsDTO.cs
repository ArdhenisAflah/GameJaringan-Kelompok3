using System;
using UnityEngine;

namespace EdenFarm.Auth.DTO
{
    /// <summary>
    /// Model DTO untuk statistik aktivitas bertani pemain di Eden Farm.
    /// Merujuk pada kolom atribut di tabel SQLite 'player_profiles'.
    /// Digunakan di dalam PlayerDataDTO.
    /// </summary>
    [Serializable]
    public class FarmStatsDTO
    {
        [Tooltip("Total sesi kebun yang pernah dikelola pemain (farms_managed_count)")]
        public int farmsManaged;

        [Tooltip("Total pesanan truk yang sukses diantar (total_orders_completed)")]
        public int ordersCompleted;

        [Tooltip("Skor match tertinggi pemain (highest_score)")]
        public int highestScore;

        public FarmStatsDTO()
        {
            farmsManaged = 0;
            ordersCompleted = 0;
            highestScore = 0;
        }

        public FarmStatsDTO(int farmsManaged, int ordersCompleted, int highestScore)
        {
            this.farmsManaged = farmsManaged;
            this.ordersCompleted = ordersCompleted;
            this.highestScore = highestScore;
        }
    }
}
