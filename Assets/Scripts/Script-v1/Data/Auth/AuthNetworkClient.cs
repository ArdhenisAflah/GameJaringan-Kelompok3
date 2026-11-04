using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using EdenFarm.Auth.DTO;

namespace EdenFarm.Auth.Networking
{
    /// <summary>
    /// Wrapper untuk menangkap pesan error respon JSON backend jika terjadi kegagalan (4xx / 5xx).
    /// </summary>
    [Serializable]
    public class ApiErrorResponse
    {
        public string status;
        public string message;
        public string error;
    }

    /// <summary>
    /// Klien HTTP UnityWebRequest untuk berinteraksi dengan REST API backend GameJaringan.
    /// Mengelola pipeline request/response untuk Register, Login, Verify Session, dan Logout.
    /// </summary>
    public class AuthNetworkClient : MonoBehaviour
    {
        public static AuthNetworkClient Instance { get; private set; }

        [Header("Backend API Configuration")]
        [Tooltip("Base URL backend server Node.js / Express")]
        [SerializeField] private string apiBaseUrl = "http://localhost:5000";

        [Tooltip("Batas waktu request HTTP dalam satuan detik")]
        [SerializeField] private int timeoutSeconds = 10;

        public string BaseUrl => apiBaseUrl.TrimEnd('/');

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// Mengirimkan registrasi player baru ke POST /api/register.
        /// </summary>
        public void SendRegister(RegisterRequestDTO requestDto, Action<RegisterResponseDTO> onSuccess, Action<string> onFailure)
        {
            StartCoroutine(PostJsonRoutine($"{BaseUrl}/api/register", requestDto.ToJson(), null, (jsonResponse) =>
            {
                RegisterResponseDTO response = RegisterResponseDTO.FromJson(jsonResponse);
                if (response != null && response.IsSuccess)
                {
                    onSuccess?.Invoke(response);
                }
                else
                {
                    string msg = response?.message ?? "Registrasi gagal diproses.";
                    onFailure?.Invoke(msg);
                }
            }, onFailure));
        }

        /// <summary>
        /// Mengirimkan autentikasi kredensial login ke POST /api/login.
        /// </summary>
        public void SendLogin(LoginRequestDTO requestDto, Action<LoginResponseDTO> onSuccess, Action<string> onFailure)
        {
            StartCoroutine(PostJsonRoutine($"{BaseUrl}/api/login", requestDto.ToJson(), null, (jsonResponse) =>
            {
                LoginResponseDTO response = LoginResponseDTO.FromJson(jsonResponse);
                if (response != null && response.IsSuccess)
                {
                    onSuccess?.Invoke(response);
                }
                else
                {
                    string msg = response?.message ?? "Login gagal diproses.";
                    onFailure?.Invoke(msg);
                }
            }, onFailure));
        }

        /// <summary>
        /// Mengirimkan verifikasi token sesi tersimpan ke GET /api/auth/verify (Auto-Login).
        /// </summary>
        public void SendVerifySession(string token, Action<LoginResponseDTO> onSuccess, Action<string> onFailure)
        {
            StartCoroutine(GetJsonRoutine($"{BaseUrl}/api/auth/verify", token, (jsonResponse) =>
            {
                LoginResponseDTO response = LoginResponseDTO.FromJson(jsonResponse);
                if (response != null && response.IsSuccess)
                {
                    // Lengkapi token pada DTO jika tidak disertakan di root
                    if (string.IsNullOrEmpty(response.token)) response.token = token;
                    onSuccess?.Invoke(response);
                }
                else
                {
                    string msg = response?.message ?? "Sesi tidak valid.";
                    onFailure?.Invoke(msg);
                }
            }, onFailure));
        }

        /// <summary>
        /// Memberitahukan server bahwa sesi di-logout ke POST /api/auth/logout.
        /// </summary>
        public void SendLogout(string token, Action onCompleted = null)
        {
            StartCoroutine(PostJsonRoutine($"{BaseUrl}/api/auth/logout", "{}", token, (res) =>
            {
                onCompleted?.Invoke();
            }, (err) =>
            {
                onCompleted?.Invoke();
            }));
        }

        // ==========================================
        // Helper Routines (UnityWebRequest JSON)
        // ==========================================

        private IEnumerator PostJsonRoutine(string url, string jsonBody, string bearerToken, Action<string> onSuccess, Action<string> onFailure)
        {
            Debug.Log($"<color=#00E5FF>[AuthNetworkClient] [REQ-POST]</color> Sending request to: <b>{url}</b>\n<color=#B0BEC5>Payload:</color> {(url.Contains("/login") || url.Contains("/register") ? "***[CREDENTIALS PROTECTED]***" : jsonBody)}");

            using (UnityWebRequest request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
            {
                byte[] rawPayload = Encoding.UTF8.GetBytes(jsonBody);
                request.uploadHandler = new UploadHandlerRaw(rawPayload);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = timeoutSeconds;

                if (!string.IsNullOrEmpty(bearerToken))
                {
                    request.SetRequestHeader("Authorization", $"Bearer {bearerToken}");
                }

                yield return request.SendWebRequest();

                HandleApiResponse(request, onSuccess, onFailure);
            }
        }

        private IEnumerator GetJsonRoutine(string url, string bearerToken, Action<string> onSuccess, Action<string> onFailure)
        {
            Debug.Log($"<color=#00E5FF>[AuthNetworkClient] [REQ-GET]</color> Sending request to: <b>{url}</b>");

            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                request.downloadHandler = new DownloadHandlerBuffer();
                request.timeout = timeoutSeconds;

                if (!string.IsNullOrEmpty(bearerToken))
                {
                    request.SetRequestHeader("Authorization", $"Bearer {bearerToken}");
                }

                yield return request.SendWebRequest();

                HandleApiResponse(request, onSuccess, onFailure);
            }
        }

        private void HandleApiResponse(UnityWebRequest request, Action<string> onSuccess, Action<string> onFailure)
        {
            string responseText = request.downloadHandler?.text;
            long statusCode = request.responseCode;
            string url = request.url;

            if (request.result == UnityWebRequest.Result.Success)
            {
                string preview = !string.IsNullOrEmpty(responseText) && responseText.Length > 200 
                    ? responseText.Substring(0, 200) + "..." 
                    : responseText;
                Debug.Log($"<color=#00FF7F>[AuthNetworkClient] [RES-OK {statusCode}]</color> <b>{url}</b>\n<color=#E0E0E0>{preview}</color>");

                onSuccess?.Invoke(responseText);
                return;
            }

            // Gagal koneksi / HTTP error (4xx atau 5xx)
            string errorMessage = null;

            if (!string.IsNullOrEmpty(responseText))
            {
                try
                {
                    ApiErrorResponse errorObj = JsonUtility.FromJson<ApiErrorResponse>(responseText);
                    if (errorObj != null && !string.IsNullOrEmpty(errorObj.message))
                    {
                        errorMessage = errorObj.message;
                    }
                }
                catch
                {
                    // Fallback bila response bukan JSON
                }
            }

            if (string.IsNullOrEmpty(errorMessage))
            {
                if (request.result == UnityWebRequest.Result.ConnectionError)
                {
                    errorMessage = "Gagal terhubung ke server backend (koneksi terputus/server offline).";
                }
                else
                {
                    errorMessage = $"Permintaan gagal ({request.responseCode}): {request.error}";
                }
            }

            Debug.LogWarning($"<color=#FF5252>[AuthNetworkClient] [RES-FAIL {statusCode}]</color> <b>{url}</b>\n<color=#FFCDD2>Error:</color> {errorMessage}\n<color=#B0BEC5>Raw:</color> {responseText}");
            onFailure?.Invoke(errorMessage);
        }
    }
}
