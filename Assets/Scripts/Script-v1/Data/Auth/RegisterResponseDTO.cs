using System;
using UnityEngine;

namespace EdenFarm.Auth.DTO
{
    /// <summary>
    /// Data Transfer Object untuk menangkap respon dari POST /api/register.
    /// Menangani respon sukses (201 Created) maupun pesan error (400 Bad Request, 409 Conflict, 500 Error).
    /// </summary>
    [Serializable]
    public class RegisterResponseDTO
    {
        [Tooltip("Status respon dari backend: 'success' atau 'error'")]
        public string status;

        [Tooltip("Pesan keterangan dari server backend")]
        public string message;

        [Tooltip("Identifier unik akun yang baru didaftarkan")]
        public int userId;

        [Tooltip("Detail data profil pemain yang terinisialisasi")]
        public PlayerDataDTO data;

        /// <summary>
        /// Properti kemudahan untuk mengecek apakah registrasi berhasil.
        /// </summary>
        public bool IsSuccess => string.Equals(status, "success", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Melakukan parsing dari teks JSON yang diterima dari UnityWebRequest.downloadHandler.text.
        /// </summary>
        public static RegisterResponseDTO FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;

            try
            {
                return JsonUtility.FromJson<RegisterResponseDTO>(json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RegisterResponseDTO] Gagal deserialisasi JSON: {ex.Message}\nJSON String: {json}");
                return null;
            }
        }
    }
}
