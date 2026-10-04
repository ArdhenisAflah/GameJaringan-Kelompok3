using System;
using UnityEngine;
using SiloSystem;

namespace TruckOrder
{
    /// <summary>
    /// Sistem sentral pengelolaan pesanan truk (Order System).
    /// Memegang order aktif, memvalidasi input Padi dari inventory pemain,
    /// mengatur transisi status pesanan, serta memicu event untuk UI dan Truk.
    /// </summary>
    public class OrderSystem : MonoBehaviour
    {
        public static OrderSystem Instance { get; private set; }

        [Header("Referensi Sistem")]
        [Tooltip("Komponen pengelola uang/reward. Jika null, akan otomatis mencari di scene.")]
        [SerializeField] private MonoBehaviour moneyManagerComponent;

        public Order ActiveOrder { get; private set; }
        public TruckController ActiveTruck { get; private set; }

        public bool HasActiveOrder => ActiveOrder != null && ActiveOrder.Status == OrderStatus.Active;

        // Events
        public event Action<Order> OnOrderActivated;
        public event Action<Order, int> OnOrderProgressChanged;
        public event Action<Order> OnOrderCompleted;
        public event Action<Order> OnOrderFailed;
        public static event Action<int> OnMoneyEarned;

        private IMoneyManager _moneyManager;

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

            ResolveMoneyManager();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void ResolveMoneyManager()
        {
            if (moneyManagerComponent is IMoneyManager mgr)
            {
                _moneyManager = mgr;
            }
            else
            {
                _moneyManager = FindObjectOfType<SimpleMoneyManager>();
                if (_moneyManager == null)
                {
                    // Buat SimpleMoneyManager runtime jika belum ada
                    GameObject moneyObj = new GameObject("MoneyManager");
                    _moneyManager = moneyObj.AddComponent<SimpleMoneyManager>();
                }
            }
        }

        /// <summary>
        /// Mengaktifkan pesanan baru saat truk terdepan tiba di loading spot.
        /// </summary>
        public void ActivateOrder(Order order, TruckController truck)
        {
            if (order == null || truck == null) return;

            ActiveOrder = order;
            ActiveTruck = truck;

            ActiveOrder.SetStatus(OrderStatus.Active);
            Debug.Log($"<color=cyan>[OrderSystem]</color> Order aktif: Butuh {ActiveOrder.RequiredAmount} {ActiveOrder.ItemType}. Reward: {ActiveOrder.TotalReward} Gold.");

            OnOrderActivated?.Invoke(ActiveOrder);
        }

        /// <summary>
        /// Memproses penyetoran item dari inventory pemain ke pesanan aktif.
        /// Mengambil padi sejumlah min(stok player, sisa kebutuhan).
        /// </summary>
        public bool TryInsertItem(PlayerInventory player, out int addedAmount, out string feedback)
        {
            addedAmount = 0;

            if (!HasActiveOrder)
            {
                feedback = "Tidak ada truk pesanan yang aktif saat ini.";
                return false;
            }

            if (player == null || !player.HasItem)
            {
                feedback = "Tangan kosong! Bawa Padi ke dekat truk untuk mengisi pesanan.";
                return false;
            }

            if (player.IsHoldingSeed)
            {
                feedback = "Tangan membawa Benih! Truk pesanan hanya menerima hasil panen Padi.";
                return false;
            }

            if (player.HeldType != ActiveOrder.ItemType)
            {
                feedback = $"Truk hanya membutuhkan {ActiveOrder.ItemType}, bukan {player.HeldType}!";
                return false;
            }

            int needed = ActiveOrder.RemainingAmount;
            if (needed <= 0)
            {
                feedback = "Pesanan sudah terpenuhi!";
                return false;
            }

            // Ambil sejumlah min(stok player, sisa kebutuhan)
            addedAmount = Mathf.Min(player.HeldQuantity, needed);

            // Kurangi item dari tangan player (Server-Authoritative jika multiplayer, atau lokal)
            player.ServerConsumeHeldItem(addedAmount);

            // Tambahkan ke progress order
            ActiveOrder.AddAmount(addedAmount);
            Debug.Log($"<color=green>[OrderSystem]</color> +{addedAmount} {ActiveOrder.ItemType} disetor ke pesanan. Progress: {ActiveOrder.CurrentAmount}/{ActiveOrder.RequiredAmount}");

            OnOrderProgressChanged?.Invoke(ActiveOrder, addedAmount);

            // Periksa jika order telah terpenuhi sepenuhnya
            if (ActiveOrder.IsFulfilled)
            {
                feedback = $"Order terpenuhi! ({ActiveOrder.CurrentAmount}/{ActiveOrder.RequiredAmount})";
                CompleteActiveOrder();
            }
            else
            {
                feedback = $"Tersisa {ActiveOrder.RemainingAmount} {ActiveOrder.ItemType} lagi.";
            }

            return true;
        }

        /// <summary>
        /// Menyelesaikan pesanan aktif, memberikan reward, dan memberitahu truk.
        /// </summary>
        public void CompleteActiveOrder()
        {
            if (ActiveOrder == null) return;

            ActiveOrder.SetStatus(OrderStatus.Completed);
            int reward = ActiveOrder.TotalReward;

            if (_moneyManager != null)
            {
                _moneyManager.AddMoney(reward);
            }
            OnMoneyEarned?.Invoke(reward);

            Debug.Log($"<color=green>[OrderSystem]</color> Order COMPLETED! Mendapatkan {reward} Gold.");
            OnOrderCompleted?.Invoke(ActiveOrder);

            if (ActiveTruck != null)
            {
                ActiveTruck.NotifyOrderCompleted();
            }
        }

        /// <summary>
        /// Menandai pesanan gagal (misal timer sabar truk habis).
        /// </summary>
        public void FailActiveOrder()
        {
            if (ActiveOrder == null) return;

            ActiveOrder.SetStatus(OrderStatus.Failed);
            Debug.LogWarning($"<color=orange>[OrderSystem]</color> Order FAILED! Waktu habis, truk kabur tanpa reward.");
            OnOrderFailed?.Invoke(ActiveOrder);

            if (ActiveTruck != null)
            {
                ActiveTruck.NotifyOrderFailed();
            }
        }

        /// <summary>
        /// Mengosongkan referensi order aktif setelah truk pergi.
        /// </summary>
        public void ClearActiveOrder()
        {
            ActiveOrder = null;
            ActiveTruck = null;
        }
    }
}
