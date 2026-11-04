using System;
using UnityEngine;

namespace EdenFarm.Auth.DTO
{
    /// <summary>
    /// Data Transfer Object untuk permintaan autentikasi pemain ke POST /api/login.
    /// Menerima input dari TMP_InputField pada form login di Auth Scene.
    /// </summary>
    [Serializable]
    public class LoginRequestDTO
    {
        [Tooltip("Username pemain")]
        public string username;

        [Tooltip("Password pemain")]
        public string password;

        public LoginRequestDTO()
        {
            username = string.Empty;
            password = string.Empty;
        }

        public LoginRequestDTO(string username, string password)
        {
            this.username = username?.Trim() ?? string.Empty;
            this.password = password ?? string.Empty;
        }

        /// <summary>
        /// Validasi format input sebelum ditembakkan ke endpoint /api/login.
        /// </summary>
        public bool Validate(out string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                errorMessage = "Username tidak boleh kosong!";
                return false;
            }

            if (string.IsNullOrEmpty(password))
            {
                errorMessage = "Password tidak boleh kosong!";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        /// <summary>
        /// Mengubah DTO ke string JSON untuk payload UnityWebRequest.
        /// </summary>
        public string ToJson()
        {
            return JsonUtility.ToJson(this);
        }
    }
}
