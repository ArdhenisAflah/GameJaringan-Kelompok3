using System;
using UnityEngine;
using EdenFarm.Auth.DTO;

namespace EdenFarm.Auth.Session
{
    /// <summary>
    /// Model data sesi aktif yang disimpan di penyimpanan lokal (PlayerPrefs).
    /// </summary>
    [Serializable]
    public class UserSessionData
    {
        public string token;
        public int userId;
        public string username;
        public string nickname;
        public string rank;
        public int tierLevel;
        public int highestScore;
        public long savedTimestamp;

        public bool IsValid => !string.IsNullOrEmpty(token) && userId > 0;
    }

    /// <summary>
    /// Local Session Storage Manager untuk mengelola sesi perangkat di client Unity.
    /// Menyimpan token JWT, userId, serta data profil pengguna sehingga mendukung fitur Auto-Login.
    /// </summary>
    public static class SessionManager
    {
        private const string PREF_KEY_TOKEN = "EDEN_FARM_AUTH_TOKEN";
        private const string PREF_KEY_USER_ID = "EDEN_FARM_USER_ID";
        private const string PREF_KEY_USERNAME = "EDEN_FARM_USERNAME";
        private const string PREF_KEY_NICKNAME = "EDEN_FARM_NICKNAME";
        private const string PREF_KEY_RANK = "EDEN_FARM_RANK";
        private const string PREF_KEY_TIER = "EDEN_FARM_TIER";
        private const string PREF_KEY_HIGH_SCORE = "EDEN_FARM_HIGH_SCORE";
        private const string PREF_KEY_TIMESTAMP = "EDEN_FARM_SESSION_TIME";

        // Fallback backward compatibility keys
        private const string LEGACY_KEY_TOKEN = "AUTH_TOKEN";
        private const string LEGACY_KEY_USERNAME = "SAVED_USERNAME";

        private static UserSessionData _cachedSession;

        /// <summary>
        /// Mengembalikan data sesi aktif dari memory cache atau PlayerPrefs lokal.
        /// </summary>
        public static UserSessionData GetCurrentSession()
        {
            if (_cachedSession != null && _cachedSession.IsValid)
            {
                return _cachedSession;
            }

            string token = PlayerPrefs.GetString(PREF_KEY_TOKEN, string.Empty);
            if (string.IsNullOrEmpty(token))
            {
                token = PlayerPrefs.GetString(LEGACY_KEY_TOKEN, string.Empty);
            }

            if (string.IsNullOrEmpty(token))
            {
                return null;
            }

            _cachedSession = new UserSessionData
            {
                token = token,
                userId = PlayerPrefs.GetInt(PREF_KEY_USER_ID, 0),
                username = PlayerPrefs.GetString(PREF_KEY_USERNAME, PlayerPrefs.GetString(LEGACY_KEY_USERNAME, string.Empty)),
                nickname = PlayerPrefs.GetString(PREF_KEY_NICKNAME, string.Empty),
                rank = PlayerPrefs.GetString(PREF_KEY_RANK, "Novice Planter"),
                tierLevel = PlayerPrefs.GetInt(PREF_KEY_TIER, 1),
                highestScore = PlayerPrefs.GetInt(PREF_KEY_HIGH_SCORE, 0),
                savedTimestamp = long.TryParse(PlayerPrefs.GetString(PREF_KEY_TIMESTAMP, "0"), out long ts) ? ts : 0
            };

            return _cachedSession.IsValid ? _cachedSession : null;
        }

        /// <summary>
        /// Mengecek apakah ada token sesi tersimpan di perangkat lokal.
        /// </summary>
        public static bool HasSavedSession()
        {
            return PlayerPrefs.HasKey(PREF_KEY_TOKEN) || PlayerPrefs.HasKey(LEGACY_KEY_TOKEN);
        }

        /// <summary>
        /// Menyimpan data sesi setelah login atau registrasi berhasil.
        /// </summary>
        public static void SaveSession(string token, int userId, PlayerDataDTO data)
        {
            if (string.IsNullOrEmpty(token)) return;

            long currentEpoch = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            PlayerPrefs.SetString(PREF_KEY_TOKEN, token);
            PlayerPrefs.SetInt(PREF_KEY_USER_ID, userId);
            PlayerPrefs.SetString(PREF_KEY_TIMESTAMP, currentEpoch.ToString());

            // Kompatibilitas legacy
            PlayerPrefs.SetString(LEGACY_KEY_TOKEN, token);

            string username = data != null ? data.username : string.Empty;
            string nickname = data != null ? data.nickname : username;
            string rank = data != null ? data.rank : "Novice Planter";
            int tier = data != null ? data.tierLevel : 1;
            int highScore = (data != null && data.stats != null) ? data.stats.highestScore : PlayerPrefs.GetInt(PREF_KEY_HIGH_SCORE, 0);

            if (!string.IsNullOrEmpty(username))
            {
                PlayerPrefs.SetString(PREF_KEY_USERNAME, username);
                PlayerPrefs.SetString(LEGACY_KEY_USERNAME, username);
            }

            if (!string.IsNullOrEmpty(nickname))
            {
                PlayerPrefs.SetString(PREF_KEY_NICKNAME, nickname);
            }

            PlayerPrefs.SetString(PREF_KEY_RANK, rank);
            PlayerPrefs.SetInt(PREF_KEY_TIER, tier);
            PlayerPrefs.SetInt(PREF_KEY_HIGH_SCORE, highScore);

            PlayerPrefs.Save();

            _cachedSession = new UserSessionData
            {
                token = token,
                userId = userId,
                username = username,
                nickname = nickname,
                rank = rank,
                tierLevel = tier,
                highestScore = highScore,
                savedTimestamp = currentEpoch
            };

            Debug.Log($"<color=green>[SessionManager] Sesi tersimpan untuk pemain: {username} (ID: {userId})</color>");
        }

        /// <summary>
        /// Memperbarui skor tertinggi pemain secara lokal.
        /// </summary>
        public static void UpdateHighScore(int newHighScore)
        {
            int currentBest = PlayerPrefs.GetInt(PREF_KEY_HIGH_SCORE, 0);
            if (newHighScore > currentBest)
            {
                PlayerPrefs.SetInt(PREF_KEY_HIGH_SCORE, newHighScore);
                PlayerPrefs.Save();
                if (_cachedSession != null)
                {
                    _cachedSession.highestScore = newHighScore;
                }
            }
        }

        public static int GetPersonalHighScore()
        {
            return PlayerPrefs.GetInt(PREF_KEY_HIGH_SCORE, 0);
        }

        /// <summary>
        /// Menghapus sesi lokal perangkat (Logout).
        /// </summary>
        public static void ClearSession()
        {
            PlayerPrefs.DeleteKey(PREF_KEY_TOKEN);
            PlayerPrefs.DeleteKey(PREF_KEY_USER_ID);
            PlayerPrefs.DeleteKey(PREF_KEY_TIMESTAMP);
            PlayerPrefs.DeleteKey(PREF_KEY_NICKNAME);
            PlayerPrefs.DeleteKey(PREF_KEY_RANK);
            PlayerPrefs.DeleteKey(PREF_KEY_TIER);
            PlayerPrefs.DeleteKey(LEGACY_KEY_TOKEN);

            // Pertahankan PREF_KEY_USERNAME agar nama akun terakhir tetap terisi di textbox login
            PlayerPrefs.Save();

            _cachedSession = null;
            Debug.Log("<color=yellow>[SessionManager] Sesi lokal berhasil dibersihkan.</color>");
        }

        /// <summary>
        /// Mengambil username akun terakhir untuk autofill form login.
        /// </summary>
        public static string GetLastUsername()
        {
            return PlayerPrefs.GetString(PREF_KEY_USERNAME, PlayerPrefs.GetString(LEGACY_KEY_USERNAME, string.Empty));
        }
    }
}
