using System;
using UnityEngine;
using FishNet;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using SiloSystem;

namespace TruckOrder
{
    /// <summary>
    /// Sistem sentral pengelolaan pesanan truk (Order System).
    /// Server-authoritative: Mengatur siklus hidup pesanan, kuantitas kebutuhan,
    /// progres penyetoran Padi dari pemain, dan timer sabar truk yang tersinkronisasi via FishNet.
    /// </summary>
    public class OrderSystem : NetworkBehaviour
    {
        public static OrderSystem Instance { get; private set; }

        [Header("Referensi Sistem")]
        [Tooltip("Komponen pengelola uang/reward. Jika null, akan otomatis mencari di scene.")]
        [SerializeField] private MonoBehaviour moneyManagerComponent;

        [Header("Konfigurasi Reward")]
        [SerializeField] private int defaultRewardPerUnit = 10;

        // FishNet Synchronized Variables (Server Authority)
        public readonly SyncVar<HarvestType> SyncedItemType = new(HarvestType.Padi);
        public readonly SyncVar<int> SyncedRequiredAmount = new(0);
        public readonly SyncVar<int> SyncedCurrentAmount = new(0);
        public readonly SyncVar<OrderStatus> SyncedStatus = new(OrderStatus.Pending);
        public readonly SyncVar<float> SyncedRemainingPatience = new(0f);
        public readonly SyncVar<float> SyncedTotalPatience = new(0f);
        public readonly SyncVar<int> SyncedOrderId = new(0);

        // Events untuk UI, Audio, dan GameStateManager
        public event Action<Order> OnOrderActivated;
        public event Action<Order, int> OnOrderProgressChanged;
        public event Action<Order> OnOrderCompleted;
        public event Action<Order> OnOrderFailed;
        public static event Action<int> OnMoneyEarned;

        public Order ActiveOrder { get; private set; }
        
        private TruckController _activeTruck;
        public TruckController ActiveTruck
        {
            get
            {
                if (_activeTruck != null) return _activeTruck;
                if (TruckQueueManager.Instance != null) return TruckQueueManager.Instance.ActiveTruck;
                return null;
            }
            private set => _activeTruck = value;
        }

        public bool HasActiveOrder => SyncedStatus.Value == OrderStatus.Active;
        public float RemainingPatienceTime => _localRemainingPatience;
        public float TotalPatienceTime => SyncedTotalPatience.Value;

        private IMoneyManager _moneyManager;
        private float _localRemainingPatience = 0f;
        private int _rewardPerUnit = 10;

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
            _rewardPerUnit = defaultRewardPerUnit;

            SyncedStatus.OnChange += HandleStatusChanged;
            SyncedCurrentAmount.OnChange += HandleCurrentAmountChanged;
            SyncedRemainingPatience.OnChange += HandlePatienceChanged;
            SyncedRequiredAmount.OnChange += HandleRequiredAmountChanged;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            // Late-join support: Jika client baru bergabung saat order sudah aktif di server
            if (SyncedStatus.Value == OrderStatus.Active)
            {
                EnsureActiveOrder();
                _localRemainingPatience = SyncedRemainingPatience.Value;
                Debug.Log($"<color=cyan>[OrderSystem]</color> Late-join sync: Menerima order aktif {ActiveOrder.RequiredAmount} {ActiveOrder.ItemType}. Sisa waktu: {_localRemainingPatience:F1}s.");
                OnOrderActivated?.Invoke(ActiveOrder);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            SyncedStatus.OnChange -= HandleStatusChanged;
            SyncedCurrentAmount.OnChange -= HandleCurrentAmountChanged;
            SyncedRemainingPatience.OnChange -= HandlePatienceChanged;
            SyncedRequiredAmount.OnChange -= HandleRequiredAmountChanged;
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
                    GameObject moneyObj = new GameObject("MoneyManager");
                    _moneyManager = moneyObj.AddComponent<SimpleMoneyManager>();
                }
            }
        }

        private void Update()
        {
            if (!HasActiveOrder) return;

            bool isServer = IsServerStarted || (!InstanceFinder.IsServerStarted && !InstanceFinder.IsClientStarted);
            if (isServer)
            {
                // Server authoritative timer countdown
                SyncedRemainingPatience.Value -= Time.deltaTime;
                _localRemainingPatience = SyncedRemainingPatience.Value;

                if (SyncedRemainingPatience.Value <= 0f)
                {
                    SyncedRemainingPatience.Value = 0f;
                    _localRemainingPatience = 0f;
                    FailActiveOrder();
                }
            }
            else
            {
                // Client smooth countdown interpolation
                if (_localRemainingPatience > 0f)
                {
                    _localRemainingPatience -= Time.deltaTime;
                    if (_localRemainingPatience < 0f) _localRemainingPatience = 0f;
                }
            }
        }

        // ==========================================
        // SYNCVAR CHANGE HANDLERS (ALL CLIENTS & SERVER)
        // ==========================================
        private void HandleStatusChanged(OrderStatus prev, OrderStatus next, bool asServer)
        {
            if (prev == next) return;

            if (next == OrderStatus.Active)
            {
                EnsureActiveOrder();
                _localRemainingPatience = SyncedTotalPatience.Value;
                Debug.Log($"<color=cyan>[OrderSystem]</color> Order aktif: Butuh {ActiveOrder.RequiredAmount} {ActiveOrder.ItemType}. Reward: {ActiveOrder.TotalReward} Gold.");
                OnOrderActivated?.Invoke(ActiveOrder);
            }
            else if (next == OrderStatus.Completed)
            {
                if (ActiveOrder != null)
                {
                    ActiveOrder.SetStatus(OrderStatus.Completed);
                }
                Debug.Log($"<color=green>[OrderSystem]</color> Order COMPLETED!");
                OnOrderCompleted?.Invoke(ActiveOrder);

                var truck = ActiveTruck;
                if (truck != null)
                {
                    truck.NotifyOrderCompleted();
                }
            }
            else if (next == OrderStatus.Failed)
            {
                if (ActiveOrder != null)
                {
                    ActiveOrder.SetStatus(OrderStatus.Failed);
                }
                Debug.LogWarning("<color=orange>[OrderSystem]</color> Order FAILED! Waktu habis.");
                OnOrderFailed?.Invoke(ActiveOrder);

                var truck = ActiveTruck;
                if (truck != null)
                {
                    truck.NotifyOrderFailed();
                }
            }
            else if (next == OrderStatus.Pending)
            {
                ActiveOrder = null;
            }
        }

        private void HandleCurrentAmountChanged(int prev, int next, bool asServer)
        {
            EnsureActiveOrder();
            ActiveOrder.SetCurrentAmount(next);
            int added = next - prev;
            OnOrderProgressChanged?.Invoke(ActiveOrder, added);
        }

        private void HandlePatienceChanged(float prev, float next, bool asServer)
        {
            _localRemainingPatience = next;
        }

        private void HandleRequiredAmountChanged(int prev, int next, bool asServer)
        {
            EnsureActiveOrder();
            ActiveOrder.SetRequiredAmount(next);
        }

        private void EnsureActiveOrder()
        {
            if (ActiveOrder == null || ActiveOrder.ItemType != SyncedItemType.Value || ActiveOrder.RequiredAmount != SyncedRequiredAmount.Value)
            {
                ActiveOrder = new Order(SyncedItemType.Value, SyncedRequiredAmount.Value, _rewardPerUnit);
            }
            ActiveOrder.SetCurrentAmount(SyncedCurrentAmount.Value);
            ActiveOrder.SetStatus(SyncedStatus.Value);
        }

        // ==========================================
        // SERVER-AUTHORITATIVE ORDER MANAGEMENT
        // ==========================================
        /// <summary>
        /// Mengaktifkan pesanan baru dari Server saat truk tiba di loading spot.
        /// </summary>
        public void ServerActivateNewOrder(HarvestType itemType, int requiredAmount, float patienceTime, int rewardPerUnit, TruckController truck = null)
        {
            bool isServer = IsServerStarted || (!InstanceFinder.IsServerStarted && !InstanceFinder.IsClientStarted);
            if (!isServer) return;

            _activeTruck = truck;
            _rewardPerUnit = rewardPerUnit;

            SyncedOrderId.Value++;
            SyncedItemType.Value = itemType;
            SyncedRequiredAmount.Value = requiredAmount;
            SyncedCurrentAmount.Value = 0;
            SyncedTotalPatience.Value = patienceTime;
            SyncedRemainingPatience.Value = patienceTime;
            _localRemainingPatience = patienceTime;
            SyncedStatus.Value = OrderStatus.Active;

            EnsureActiveOrder();
            Debug.Log($"<color=cyan>[OrderSystem]</color> Server mengaktifkan order #{SyncedOrderId.Value}: {requiredAmount}x {itemType}, patience {patienceTime:F1}s.");

            // Fallback jika mode offline murni
            if (!IsServerStarted)
            {
                OnOrderActivated?.Invoke(ActiveOrder);
            }
        }

        /// <summary>
        /// Kompatibilitas mundur: Mengaktifkan pesanan lewat objek Order lokal.
        /// </summary>
        public void ActivateOrder(Order order, TruckController truck)
        {
            if (order == null) return;
            float patience = (truck != null && truck.TotalPatienceTime > 0f) ? truck.TotalPatienceTime : 120f;
            ServerActivateNewOrder(order.ItemType, order.RequiredAmount, patience, order.RewardPerUnit, truck);
        }

        // ==========================================
        // ITEM DEPOSIT LOGIC (CLIENT RPC & SERVER EXEC)
        // ==========================================
        /// <summary>
        /// Meminta penyetoran item dari inventory pemain ke pesanan aktif.
        /// </summary>
        public void RequestDepositItem(PlayerInventory player)
        {
            if (player == null) return;

            if (IsServerStarted || (!InstanceFinder.IsServerStarted && !InstanceFinder.IsClientStarted))
            {
                ProcessDepositItem(player);
            }
            else if (IsClientStarted)
            {
                ServerRequestDepositItem(player.NetworkObject);
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void ServerRequestDepositItem(NetworkObject playerNetObj)
        {
            if (playerNetObj == null) return;
            PlayerInventory player = playerNetObj.GetComponent<PlayerInventory>();
            if (player == null) return;

            ProcessDepositItem(player);
        }

        private void ProcessDepositItem(PlayerInventory player)
        {
            if (!HasActiveOrder)
            {
                Debug.LogWarning("[OrderSystem] Tidak ada order aktif saat ini.");
                return;
            }

            if (player == null || !player.HasItem)
            {
                Debug.LogWarning("[OrderSystem] Pemain tidak memegang item.");
                return;
            }

            if (player.IsHoldingSeed)
            {
                Debug.LogWarning("[OrderSystem] Pemain membawa benih, hanya menerima hasil panen.");
                return;
            }

            if (player.HeldType != SyncedItemType.Value)
            {
                Debug.LogWarning($"[OrderSystem] Item mismatch: Truk butuh {SyncedItemType.Value}, pemain membawa {player.HeldType}.");
                return;
            }

            // Validasi jarak anti-cheat
            Vector3 targetCenter = ActiveTruck != null ? ActiveTruck.transform.position : transform.position;
            if (Vector2.Distance(player.transform.position, targetCenter) > 4.5f)
            {
                Debug.LogWarning($"[OrderSystem] Pemain terlalu jauh ({Vector2.Distance(player.transform.position, targetCenter):F2}m > 4.5m).");
                return;
            }

            int needed = SyncedRequiredAmount.Value - SyncedCurrentAmount.Value;
            if (needed <= 0) return;

            int addedAmount = Mathf.Min(player.HeldQuantity, needed);

            // Konsumsi item dari inventory pemain
            if (IsServerStarted)
            {
                player.ServerConsumeHeldItem(addedAmount);
            }
            else
            {
                player.ServerSetHeldItem(player.HeldType, player.HeldQuantity - addedAmount, player.HeldCategory);
            }

            SyncedCurrentAmount.Value += addedAmount;
            if (ActiveOrder != null)
            {
                ActiveOrder.SetCurrentAmount(SyncedCurrentAmount.Value);
            }

            Debug.Log($"<color=green>[OrderSystem]</color> +{addedAmount} {SyncedItemType.Value} disetor oleh player. Progress: {SyncedCurrentAmount.Value}/{SyncedRequiredAmount.Value}");

            if (!IsServerStarted)
            {
                OnOrderProgressChanged?.Invoke(ActiveOrder, addedAmount);
            }

            if (SyncedCurrentAmount.Value >= SyncedRequiredAmount.Value)
            {
                CompleteActiveOrder();
            }
        }

        /// <summary>
        /// Kompatibilitas mundur untuk interaksi lokal.
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

            if (player.HeldType != SyncedItemType.Value)
            {
                feedback = $"Truk hanya membutuhkan {SyncedItemType.Value}, bukan {player.HeldType}!";
                return false;
            }

            int needed = SyncedRequiredAmount.Value - SyncedCurrentAmount.Value;
            if (needed <= 0)
            {
                feedback = "Pesanan sudah terpenuhi!";
                return false;
            }

            addedAmount = Mathf.Min(player.HeldQuantity, needed);
            RequestDepositItem(player);
            feedback = $"Menyetor {addedAmount} {SyncedItemType.Value} ke truk.";
            return true;
        }

        /// <summary>
        /// Menyelesaikan pesanan aktif, memberikan reward, dan memberitahu truk.
        /// </summary>
        public void CompleteActiveOrder()
        {
            if (!HasActiveOrder) return;

            SyncedStatus.Value = OrderStatus.Completed;
            if (ActiveOrder != null)
            {
                ActiveOrder.SetStatus(OrderStatus.Completed);
            }

            int reward = SyncedRequiredAmount.Value * _rewardPerUnit;
            if (_moneyManager != null)
            {
                _moneyManager.AddMoney(reward);
            }
            OnMoneyEarned?.Invoke(reward);

            Debug.Log($"<color=green>[OrderSystem]</color> Order COMPLETED! Reward: {reward} Gold.");

            if (!IsServerStarted)
            {
                OnOrderCompleted?.Invoke(ActiveOrder);
                var truck = ActiveTruck;
                if (truck != null)
                {
                    truck.NotifyOrderCompleted();
                }
            }
        }

        /// <summary>
        /// Menandai pesanan gagal saat waktu tunggu habis.
        /// </summary>
        public void FailActiveOrder()
        {
            if (!HasActiveOrder) return;

            SyncedStatus.Value = OrderStatus.Failed;
            if (ActiveOrder != null)
            {
                ActiveOrder.SetStatus(OrderStatus.Failed);
            }

            Debug.LogWarning("<color=orange>[OrderSystem]</color> Order FAILED! Waktu habis.");

            if (!IsServerStarted)
            {
                OnOrderFailed?.Invoke(ActiveOrder);
                var truck = ActiveTruck;
                if (truck != null)
                {
                    truck.NotifyOrderFailed();
                }
            }
        }

        /// <summary>
        /// Mengosongkan referensi order aktif setelah truk pergi.
        /// </summary>
        public void ClearActiveOrder()
        {
            bool isServer = IsServerStarted || (!InstanceFinder.IsServerStarted && !InstanceFinder.IsClientStarted);
            if (isServer)
            {
                SyncedStatus.Value = OrderStatus.Pending;
            }
            ActiveOrder = null;
            _activeTruck = null;
        }
    }
}
