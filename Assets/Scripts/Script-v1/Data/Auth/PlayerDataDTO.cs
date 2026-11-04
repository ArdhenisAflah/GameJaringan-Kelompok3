using System;
using UnityEngine;

namespace EdenFarm.Auth.DTO
{
    /// <summary>
    /// Model DTO untuk profil dan metadata pemain.
    /// Merujuk pada relasi tabel 'users', 'player_profiles', dan 'farm_ranks'.
    /// Digunakan di dalam respon /api/register dan /api/login, serta reusable di MainMenu/Lobby.
    /// </summary>
    [Serializable]
    public class PlayerDataDTO
    {
        [Tooltip("Identifier unik akun pemain di tabel users (id)")]
        public int userId;

        [Tooltip("Kredensial username unik pemain")]
        public string username;

        [Tooltip("Nama display in-game pemain dari player_profiles (nickname)")]
        public string nickname;

        [Tooltip("Nama tier peringkat pemain dari farm_ranks (rank_name)")]
        public string rank;

        [Tooltip("Level tingkatan tier (tier_level: 1=Novice, 2=Apprentice, 3=Master, 4=Legend)")]
        public int tierLevel;

        [Tooltip("Statistik gameplay pemain")]
        public FarmStatsDTO stats;

        public PlayerDataDTO()
        {
            userId = 0;
            username = string.Empty;
            nickname = string.Empty;
            rank = "Novice Planter";
            tierLevel = 1;
            stats = new FarmStatsDTO();
        }
    }
}
