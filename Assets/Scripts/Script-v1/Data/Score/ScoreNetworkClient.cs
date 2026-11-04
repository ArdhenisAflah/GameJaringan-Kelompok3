using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using EdenFarm.Auth.Networking;
using EdenFarm.Auth.Session;
using EdenFarm.Score.DTO;

namespace EdenFarm.Score.Networking
{
    /// <summary>
    /// Klien HTTP UnityWebRequest untuk sistem Score Management.
    /// Mengelola komunikasi ke endpoint:
    /// - POST /api/score/save (Simpan skor match)
    /// - GET /api/score/user/:userId (Lihat profil & skor pemain)
    /// - GET /api/leaderboard (Lihat papan peringkat Top 10)
    /// </summary>
    public class ScoreNetworkClient : MonoBehaviour
    {
        public static ScoreNetworkClient Instance { get; private set; }

        [Header("Konfigurasi API")]
        [Tooltip("Base URL backend server Node.js / Express")]
        [SerializeField] private string apiBaseUrl = "http://localhost:5000";

        [Tooltip("Batas waktu request HTTP dalam satuan detik")]
        [SerializeField] private int timeoutSeconds = 10;

        public string BaseUrl
        {
            get
            {
                if (AuthNetworkClient.Instance != null && !string.IsNullOrEmpty(AuthNetworkClient.Instance.BaseUrl))
                {
                    return AuthNetworkClient.Instance.BaseUrl;
                }
                return apiBaseUrl.TrimEnd('/');
            }
        }

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
        /// Mengirimkan skor hasil match ke server.
        /// </summary>
        public void SendSaveScore(SaveScoreRequestDTO requestDto, Action<SaveScoreResponseDTO> onSuccess, Action<string> onFailure)
        {
            string token = SessionManager.GetCurrentSession()?.token;
            string url = $"{BaseUrl}/api/score/save";

            StartCoroutine(PostJsonRoutine(url, requestDto.ToJson(), token, (jsonResponse) =>
            {
                SaveScoreResponseDTO response = SaveScoreResponseDTO.FromJson(jsonResponse);
                if (response != null && response.IsSuccess)
                {
                    onSuccess?.Invoke(response);
                }
                else
                {
                    string msg = response?.message ?? "Gagal menyimpan skor match.";
                    onFailure?.Invoke(msg);
                }
            }, onFailure));
        }

        /// <summary>
        /// Mengambil skor detail dan 5 riwayat match pemain berdasarkan userId.
        /// </summary>
        public void SendGetUserScore(int userId, Action<UserScoreResponseDTO> onSuccess, Action<string> onFailure)
        {
            string token = SessionManager.GetCurrentSession()?.token;
            string url = $"{BaseUrl}/api/score/user/{userId}";

            StartCoroutine(GetJsonRoutine(url, token, (jsonResponse) =>
            {
                UserScoreResponseDTO response = UserScoreResponseDTO.FromJson(jsonResponse);
                if (response != null && response.IsSuccess)
                {
                    onSuccess?.Invoke(response);
                }
                else
                {
                    string msg = response?.message ?? "Gagal memuat profil skor pemain.";
                    onFailure?.Invoke(msg);
                }
            }, onFailure));
        }

        /// <summary>
        /// Mengambil daftar Top 10 leaderboard pemain dari backend.
        /// </summary>
        public void SendGetLeaderboard(Action<LeaderboardResponseDTO> onSuccess, Action<string> onFailure)
        {
            string token = SessionManager.GetCurrentSession()?.token;
            string url = $"{BaseUrl}/api/leaderboard";

            StartCoroutine(GetJsonRoutine(url, token, (jsonResponse) =>
            {
                LeaderboardResponseDTO response = LeaderboardResponseDTO.FromJson(jsonResponse);
                if (response != null && response.IsSuccess)
                {
                    onSuccess?.Invoke(response);
                }
                else
                {
                    string msg = response?.message ?? "Gagal memuat leaderboard.";
                    onFailure?.Invoke(msg);
                }
            }, onFailure));
        }

        // ==========================================
        // Helper Routines (UnityWebRequest JSON)
        // ==========================================

        private IEnumerator PostJsonRoutine(string url, string jsonBody, string bearerToken, Action<string> onSuccess, Action<string> onFailure)
        {
            Debug.Log($"<color=#FFD700>[ScoreNetworkClient] [REQ-POST]</color> Sending request to: <b>{url}</b>\n<color=#B0BEC5>Payload:</color> {jsonBody}");

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
            Debug.Log($"<color=#FFD700>[ScoreNetworkClient] [REQ-GET]</color> Sending request to: <b>{url}</b>");

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
                Debug.Log($"<color=#00FF7F>[ScoreNetworkClient] [RES-OK {statusCode}]</color> <b>{url}</b>\n<color=#E0E0E0>{preview}</color>");

                onSuccess?.Invoke(responseText);
                return;
            }

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
                    // Fallback non-json error
                }
            }

            if (string.IsNullOrEmpty(errorMessage))
            {
                errorMessage = $"Koneksi API Gagal ({request.responseCode}): {request.error}";
            }

            Debug.LogWarning($"<color=#FF5252>[ScoreNetworkClient] [RES-FAIL {statusCode}]</color> <b>{url}</b>\n<color=#FFCDD2>Error:</color> {errorMessage}\n<color=#B0BEC5>Raw:</color> {responseText}");
            onFailure?.Invoke(errorMessage);
        }
    }
}
