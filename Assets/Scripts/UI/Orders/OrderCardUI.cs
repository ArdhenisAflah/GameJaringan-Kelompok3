using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SiloSystem;

namespace UI.Orders
{
    /// <summary>
    /// Komponen UI pengendali Kartu Pesanan (Order Card) yang mengintegrasikan:
    /// 1. Latar belakang pesanan (Order Box)
    /// 2. Bar timer pesanan (Order Timer Base & Fill via OrderTimerUI)
    /// 3. Informasi item pesanan (Ikon, Nama, Jumlah, Imbalan Koin)
    /// </summary>
    public class OrderCardUI : MonoBehaviour
    {
        [Header("Sistem Timer Pesanan")]
        [Tooltip("Komponen OrderTimerUI yang terpasang pada prefab kartu pesanan.")]
        [SerializeField] private OrderTimerUI orderTimer;

        [Header("Elemen Visual Pesanan")]
        [Tooltip("Gambar ikon item hasil panen yang dipesan.")]
        [SerializeField] private Image itemIconImage;

        [Tooltip("Teks nama item yang dipesan.")]
        [SerializeField] private TextMeshProUGUI itemNameText;

        [Tooltip("Teks jumlah item yang dipesan.")]
        [SerializeField] private TextMeshProUGUI itemQuantityText;

        [Tooltip("Teks jumlah koin/hadiah yang didapatkan.")]
        [SerializeField] private TextMeshProUGUI rewardCoinsText;

        [Header("Tombol Aksi")]
        [Tooltip("Tombol untuk mengirim/menyelesaikan pesanan.")]
        [SerializeField] private Button deliverButton;

        public OrderTimerUI OrderTimer => orderTimer;

        private void Awake()
        {
            if (orderTimer == null)
            {
                orderTimer = GetComponentInChildren<OrderTimerUI>();
            }
        }

        /// <summary>
        /// Mengonfigurasi tampilan pesanan secara lengkap dan menjalankan timer.
        /// </summary>
        /// <param name="itemData">Data item panen dari SiloSystem.</param>
        /// <param name="requiredQuantity">Jumlah item yang diminta.</param>
        /// <param name="rewardCoins">Jumlah koin hadiah.</param>
        /// <param name="durationSeconds">Durasi batas waktu pesanan dalam detik.</param>
        /// <param name="onDeliverClicked">Callback saat tombol serahkan/kirim diklik.</param>
        public void SetupOrder(HarvestItemData itemData, int requiredQuantity, int rewardCoins, float durationSeconds, System.Action onDeliverClicked)
        {
            if (itemData != null)
            {
                if (itemIconImage != null) itemIconImage.sprite = itemData.icon;
                if (itemNameText != null) itemNameText.text = itemData.displayName;
            }

            if (itemQuantityText != null) itemQuantityText.text = $"x{requiredQuantity}";
            if (rewardCoinsText != null) rewardCoinsText.text = $"{rewardCoins}";

            if (deliverButton != null && onDeliverClicked != null)
            {
                deliverButton.onClick.RemoveAllListeners();
                deliverButton.onClick.AddListener(() => onDeliverClicked.Invoke());
            }

            if (orderTimer != null)
            {
                orderTimer.StartTimer(durationSeconds);
            }
        }
    }
}
