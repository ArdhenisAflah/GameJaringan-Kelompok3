using UnityEngine;
using UI.Orders;

namespace UI.Orders
{
    /// <summary>
    /// Skrip pengujian interaktif untuk menguji komponen OrderTimerUI di Unity Editor Play Mode.
    /// Memungkinkan pengujian start, pause, resume, reset, dan event kehabisan waktu melalui Context Menu atau Tombol Keyboard.
    /// </summary>
    public class OrderTimerTester : MonoBehaviour
    {
        [Header("Komponen yang Diuji")]
        [SerializeField] private OrderTimerUI orderTimer;

        [Header("Pengaturan Pengujian")]
        [Tooltip("Durasi waktu pengujian (dalam detik).")]
        [SerializeField] private float testDurationSeconds = 15f;

        [Tooltip("Jalankan timer secara otomatis saat masuk ke Play Mode.")]
        [SerializeField] private bool autoStartOnPlay = true;

        [Header("Kontrol Keyboard (Play Mode)")]
        [Tooltip("Tekan [Space] untuk memulai ulang timer.")]
        [SerializeField] private KeyCode restartKey = KeyCode.Space;

        [Tooltip("Tekan [P] untuk Pause / Resume timer.")]
        [SerializeField] private KeyCode pauseKey = KeyCode.P;

        private void Start()
        {
            if (orderTimer == null)
            {
                orderTimer = GetComponent<OrderTimerUI>();
            }

            if (orderTimer != null)
            {
                // Dapatkan notifikasi event saat timer habis
                orderTimer.OnTimerExpired += HandleTimerExpired;
                orderTimer.OnTimerUpdated += HandleTimerUpdated;

                if (autoStartOnPlay)
                {
                    TestStartTimer();
                }
            }
            else
            {
                Debug.LogWarning("[OrderTimerTester] OrderTimerUI belum terpasang pada Inspector!");
            }
        }

        private void OnDestroy()
        {
            if (orderTimer != null)
            {
                orderTimer.OnTimerExpired -= HandleTimerExpired;
                orderTimer.OnTimerUpdated -= HandleTimerUpdated;
            }
        }

        private void Update()
        {
            // Kontrol cepat menggunakan Keyboard saat Play Mode
            if (Input.GetKeyDown(restartKey))
            {
                TestStartTimer();
            }

            if (Input.GetKeyDown(pauseKey))
            {
                TogglePause();
            }
        }

        [ContextMenu("1. Jalankan Timer (Start 15 Detik)")]
        public void TestStartTimer()
        {
            if (orderTimer != null)
            {
                Debug.Log($"[OrderTimerTester] 🚀 Memulai timer pesanan ({testDurationSeconds} detik)...");
                orderTimer.StartTimer(testDurationSeconds);
            }
        }

        [ContextMenu("2. Pause / Resume Timer")]
        public void TogglePause()
        {
            if (orderTimer != null)
            {
                if (orderTimer.IsPaused)
                {
                    orderTimer.ResumeTimer();
                    Debug.Log("[OrderTimerTester] ▶️ Timer dilanjutkan (Resume).");
                }
                else
                {
                    orderTimer.PauseTimer();
                    Debug.Log("[OrderTimerTester] ⏸️ Timer dihentikan sementara (Paused).");
                }
            }
        }

        [ContextMenu("3. Hentikan Timer (Stop)")]
        public void TestStopTimer()
        {
            if (orderTimer != null)
            {
                orderTimer.StopTimer();
                Debug.Log("[OrderTimerTester] ⏹️ Timer dihentikan.");
            }
        }

        private void HandleTimerUpdated(float remaining, float duration)
        {
            // Opsional: Log perkembangan waktu per detik tertentu
        }

        private void HandleTimerExpired()
        {
            Debug.Log("⏰ [OrderTimerTester] SUCCESS! Timer pesanan telah habis (Expired Callback Dipanggil).");
        }
    }
}
