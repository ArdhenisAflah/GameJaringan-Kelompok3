using System;
using UnityEngine;

namespace EdenFarm.Auth.DTO
{
    /// <summary>
    /// Data Transfer Object untuk menangkap respon autentikasi dari POST /api/login.
    /// Berisi token sesi JWT (24 jam) dan seluruh profil serta riwayat progres pemain.
    /// </summary>
    [Serializable]
    public class LoginResponseDTO
    {
        [Tooltip("Status respon dari backend: 'success' atau 'error'")]
        public string status;

        [Tooltip("Pesan keterangan dari server backend")]
        public string message;

        [Tooltip("JSON Web Token (JWT) untuk otentikasi request berikutnya")]
        public string token;

        [Tooltip("Identifier unik akun pemain")]
        public int userId;

        [Tooltip("Data profil lengkap pemain termasuk rank dan statistik")]
        public PlayerDataDTO data;

        /// <summary>
        /// Properti kemudahan untuk mengecek apakah login berhasil.
        /// </summary>
        public bool IsSuccess => string.Equals(status, "success", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Melakukan parsing dari teks JSON yang diterima dari UnityWebRequest.downloadHandler.text.
        /// </summary>
        public static LoginResponseDTO FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;

            try
            {
                return JsonUtility.FromJson<LoginResponseDTO>(json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LoginResponseDTO] Gagal deserialisasi JSON: {ex.Message}\nJSON String: {json}");
                return null;
            }
        }
    }
}
