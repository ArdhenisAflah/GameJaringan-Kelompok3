using System;
using UnityEngine;

namespace EdenFarm.Auth.DTO
{
    /// <summary>
    /// Data Transfer Object untuk permintaan pendaftaran akun pemain ke POST /api/register.
    /// Dirancang untuk menerima input langsung dari TMP_InputField pada Auth Scene.
    /// Merujuk pada spesifikasi input tabel 'users' & 'player_profiles'.
    /// </summary>
    [Serializable]
    public class RegisterRequestDTO
    {
        [Tooltip("Username akun (wajib, minimal 3 karakter)")]
        public string username;

        [Tooltip("Password akun (wajib, minimal 6 karakter)")]
        public string password;

        [Tooltip("Nama panggilan karakter in-game (opsional, default: username)")]
        public string nickname;

        [Tooltip("Tanggal lahir pemain format YYYY-MM-DD (opsional)")]
        public string birthDate;

        public RegisterRequestDTO()
        {
            username = string.Empty;
            password = string.Empty;
            nickname = string.Empty;
            birthDate = string.Empty;
        }

        public RegisterRequestDTO(string username, string password, string nickname = "", string birthDate = "")
        {
            this.username = username?.Trim() ?? string.Empty;
            this.password = password ?? string.Empty;
            this.nickname = string.IsNullOrWhiteSpace(nickname) ? this.username : nickname.Trim();
            this.birthDate = birthDate?.Trim() ?? string.Empty;
        }

        /// <summary>
        /// Validasi lokal sebelum mengirim request HTTP melalui UnityWebRequest.
        /// </summary>
        public bool Validate(out string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                errorMessage = "Username tidak boleh kosong!";
                return false;
            }

            if (username.Length < 3)
            {
                errorMessage = "Username minimal harus 3 karakter!";
                return false;
            }

            if (string.IsNullOrEmpty(password))
            {
                errorMessage = "Password tidak boleh kosong!";
                return false;
            }

            if (password.Length < 6)
            {
                errorMessage = "Password minimal harus 6 karakter!";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        /// <summary>
        /// Mengubah objek DTO menjadi string JSON payload yang siap dikirim via HTTP POST.
        /// </summary>
        public string ToJson()
        {
            return JsonUtility.ToJson(this);
        }
    }
}
