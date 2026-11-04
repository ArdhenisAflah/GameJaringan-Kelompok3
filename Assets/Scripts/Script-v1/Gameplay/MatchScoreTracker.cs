using System;
using UnityEngine;
using TruckOrder;
using EdenFarm.Auth.Session;
using EdenFarm.Score.DTO;
using EdenFarm.Score.Networking;

namespace EdenFarm.Score.Gameplay
{
    /// <summary>
    /// Pelacak skor dan kontribusi gameplay pemain selama match berlangsung.
    /// Mendengarkan event dari OrderSystem (penyelesaian pesanan) dan SimpleMoneyManager.
    /// Saat match berakhir, komponen ini otomatis merangkum dan mengirimkan skor ke backend.
    /// </summary>
    public class MatchScoreTracker : MonoBehaviour
    {
        public static MatchScoreTracker Instance { get; private set; }

        [Header("Statistik Match Berjalan")]
        [SerializeField] private int matchScore = 0;
        [SerializeField] private int ordersCompleted = 0;
        [SerializeField] private int ordersFailed = 0;
        [SerializeField] private int cropsHarvested = 0;
        [SerializeField] private int ordersLoaded = 0;
        [SerializeField] private string currentSessionId = string.Empty;

        public int MatchScore => matchScore;
        public int OrdersCompleted => ordersCompleted;
        public int OrdersFailed => ordersFailed;
        public int CropsHarvested => cropsHarvested;
        public int OrdersLoaded => ordersLoaded;

        public static event Action<int> OnScoreUpdated;
        public static event Action<SaveScoreResponseDTO> OnScoreSubmittedSuccess;
        public static event Action<string> OnScoreSubmittedFailed;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }
        }

        private void Start()
        {
            // Subscribe ke event OrderSystem jika ada di scene
            if (OrderSystem.Instance != null)
            {
                OrderSystem.Instance.OnOrderCompleted += HandleOrderCompleted;
                OrderSystem.Instance.OnOrderFailed += HandleOrderFailed;
                OrderSystem.Instance.OnOrderProgressChanged += HandleOrderProgressChanged;
            }

            OrderSystem.OnMoneyEarned += HandleMoneyEarned;
        }

        private void OnDestroy()
        {
            if (OrderSystem.Instance != null)
            {
                OrderSystem.Instance.OnOrderCompleted -= HandleOrderCompleted;
                OrderSystem.Instance.OnOrderFailed -= HandleOrderFailed;
                OrderSystem.Instance.OnOrderProgressChanged -= HandleOrderProgressChanged;
            }

            OrderSystem.OnMoneyEarned -= HandleMoneyEarned;
        }

        public void SetSessionId(string sessionId)
        {
            currentSessionId = sessionId;
        }

        public void RecordCropHarvested(int count = 1)
        {
            cropsHarvested += count;
            AddScore(count * 5); // 5 poin per panen
        }

        private void HandleOrderProgressChanged(Order order, int addedAmount)
        {
            if (addedAmount > 0)
            {
                ordersLoaded += addedAmount;
                AddScore(addedAmount * 10); // 10 poin per muatan dimasukkan
            }
        }

        private void HandleOrderCompleted(Order order)
        {
            ordersCompleted++;
            AddScore(100); // 100 poin bonus penyelesaian order truk
            Debug.Log($"<color=green>[MatchScoreTracker]</color> Order truk selesai! Total Order: {ordersCompleted}, Skor: {matchScore}");
        }

        private void HandleOrderFailed(Order order)
        {
            ordersFailed++;
            Debug.Log($"<color=orange>[MatchScoreTracker]</color> Order gagal/kabur. Total Gagal: {ordersFailed}");
        }

        private void HandleMoneyEarned(int amount)
        {
            AddScore(amount);
        }

        public void AddScore(int amount)
        {
            if (amount <= 0) return;
            matchScore += amount;
            OnScoreUpdated?.Invoke(matchScore);
        }

        /// <summary>
        /// Mengirimkan akumulasi hasil skor match ke backend.
        /// Dipanggil saat match usai atau Game Over.
        /// </summary>
        public void SubmitMatchScore(Action<SaveScoreResponseDTO> onCustomSuccess = null, Action<string> onCustomFailure = null)
        {
            var session = SessionManager.GetCurrentSession();
            int userId = session != null ? session.userId : 0;

            if (userId <= 0)
            {
                string warn = "Tidak dapat mengirimkan skor: Akun pemain belum login / userId tidak valid.";
                Debug.LogWarning($"[MatchScoreTracker] {warn}");
                onCustomFailure?.Invoke(warn);
                OnScoreSubmittedFailed?.Invoke(warn);
                return;
            }

            EnsureScoreNetworkClient();

            var req = new SaveScoreRequestDTO(
                userId: userId,
                score: matchScore,
                ordersCompleted: ordersCompleted,
                ordersFailed: ordersFailed,
                cropsHarvested: cropsHarvested,
                ordersLoaded: ordersLoaded,
                sessionId: !string.IsNullOrEmpty(currentSessionId) ? currentSessionId : Guid.NewGuid().ToString().Substring(0, 8)
            );

            Debug.Log($"<color=cyan>[MatchScoreTracker]</color> Mengirimkan skor match ke backend (User: {userId}, Skor: {matchScore})...");

            ScoreNetworkClient.Instance.SendSaveScore(req, (res) =>
            {
                Debug.Log($"<color=green>[MatchScoreTracker] Skor berhasil tersimpan di database! (Record ID: {res.scoreRecordId})</color>");
                onCustomSuccess?.Invoke(res);
                OnScoreSubmittedSuccess?.Invoke(res);
            }, (err) =>
            {
                Debug.LogError($"<color=red>[MatchScoreTracker] Gagal menyimpan skor: {err}</color>");
                onCustomFailure?.Invoke(err);
                OnScoreSubmittedFailed?.Invoke(err);
            });
        }

        private void EnsureScoreNetworkClient()
        {
            if (ScoreNetworkClient.Instance == null)
            {
                GameObject go = new GameObject("ScoreNetworkClient");
                go.AddComponent<ScoreNetworkClient>();
            }
        }
    }
}
