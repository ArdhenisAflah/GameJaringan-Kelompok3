using UnityEngine;
using SiloSystem;
using UI.Orders;

namespace UI.Orders
{
    /// <summary>
    /// Skrip pengujian interaktif untuk menguji komponen OrderCardUI di Unity Play Mode.
    /// Memungkinkan pembuatan pesanan sampel (Item, Jumlah, Hadiah Koin, Timer, dan Callback Tombol Kirim).
    /// </summary>
    public class OrderCardTester : MonoBehaviour
    {
        [Header("Komponen Kartu Pesanan")]
        [SerializeField] private OrderCardUI orderCard;

        [Header("Data Pesanan Sampel (Pengujian)")]
        [Tooltip("Data item hasil panen (ScriptableObject HarvestItemData dari SiloSystem).")]
        [SerializeField] private HarvestItemData sampleHarvestItem;

        [Tooltip("Jumlah item yang diminta.")]
        [SerializeField] private int requiredQuantity = 5;

        [Tooltip("Jumlah koin hadiah saat pesanan diserahkan.")]
        [SerializeField] private int rewardCoins = 150;

        [Tooltip("Durasi batas waktu pesanan dalam detik.")]
        [SerializeField] private float durationSeconds = 20f;

        [Header("Pengaturan Auto Start")]
        [Tooltip("Jalankan pesanan sampel otomatis saat masuk Play Mode.")]
        [SerializeField] private bool autoSetupOnPlay = true;

        [Header("Kontrol Keyboard (Play Mode)")]
        [Tooltip("Tekan [Space] untuk memuat ulang/memulai pesanan baru.")]
        [SerializeField] private KeyCode refreshOrderKey = KeyCode.Space;

        private void Start()
        {
            if (orderCard == null)
            {
                orderCard = GetComponent<OrderCardUI>();
            }

            if (autoSetupOnPlay)
            {
                TestSetupOrder();
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(refreshOrderKey))
            {
                TestSetupOrder();
            }
        }

        [ContextMenu("1. Jalankan Pesanan Sampel (Setup Order)")]
        public void TestSetupOrder()
        {
            if (orderCard == null)
            {
                Debug.LogWarning("[OrderCardTester] OrderCardUI belum terpasang di Inspector!");
                return;
            }

            Debug.Log($"[OrderCardTester] 📋 Memuat kartu pesanan sampel ({requiredQuantity}x, {rewardCoins} Koin, {durationSeconds}s)...");

            orderCard.SetupOrder(
                sampleHarvestItem,
                requiredQuantity,
                rewardCoins,
                durationSeconds,
                OnDeliverOrderClicked
            );

            if (orderCard.OrderTimer != null)
            {
                orderCard.OrderTimer.OnTimerExpired -= OnTimerExpiredCallback;
                orderCard.OrderTimer.OnTimerExpired += OnTimerExpiredCallback;
            }
        }

        /// <summary>
        /// Callback saat pemain mengklik tombol Serahkan / Kirim pada Kartu Pesanan.
        /// </summary>
        private void OnDeliverOrderClicked()
        {
            Debug.Log("🎉 [OrderCardTester] SUCCESS! Tombol Serahkan/Kirim diklik oleh pemain!");
            Debug.Log($"🎁 Pemain mendapatkan +{rewardCoins} Koin!");

            // Hentikan timer pesanan karena sudah selesai dikirim
            if (orderCard.OrderTimer != null)
            {
                orderCard.OrderTimer.StopTimer();
            }
        }

        /// <summary>
        /// Callback saat timer pesanan habis sebelum dikirim.
        /// </summary>
        private void OnTimerExpiredCallback()
        {
            Debug.LogWarning("⏰ [OrderCardTester] FAILED! Pesanan gagal dikirim karena waktu telah habis (Expired).");
        }
    }
}
