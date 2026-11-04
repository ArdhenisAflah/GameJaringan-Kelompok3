using System;
using System.Collections.Generic;
using UnityEngine;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;

namespace SiloSystem
{
    /// <summary>
    /// Komponen NetworkBehaviour untuk mengelola inventori/penyimpanan hasil panen di dalam Silo.
    /// Dilengkapi dengan:
    /// 1. FishNet SyncList untuk sinkronisasi otomatis ke seluruh klien (Server-Authoritative).
    /// 2. Database-like Transaction Locking (mencegah race condition multi-player).
    /// 3. API-like Endpoint Functions (mudah dikonsumsi oleh UI dan script Player).
    /// </summary>
    public class SiloStorage : NetworkBehaviour
    {
        public static SiloStorage Instance { get; private set; }

        [Header("Konfigurasi Silo")]
        [Tooltip("Nama identifikasi bangunan Silo.")]
        [SerializeField] private string siloName = "Silo Utama";

        [Tooltip("Kapasitas total unit panen yang dapat ditampung.")]
        [SerializeField] private int maxCapacity = 999;

        [Header("Stok Awal (Server Only)")]
        [Tooltip("Stok hasil panen bawaan saat server pertama kali menyala (opsional untuk testing).")]
        [SerializeField] private List<HarvestSlot> defaultStock = new List<HarvestSlot>
        {
            new HarvestSlot(HarvestType.Padi, 10),
            new HarvestSlot(HarvestType.Jagung, 5)
        };

        /// <summary>
        /// SyncList FishNet: Menyimpan daftar slot panen di jaringan.
        /// Otomatis sinkron dari Server ke seluruh Client.
        /// </summary>
        public readonly SyncList<HarvestSlot> storedItems = new();

        /// <summary>
        /// Event yang dipicu setiap kali isi penyimpanan Silo berubah (untuk update UI Silo).
        /// </summary>
        public event Action OnStorageChanged;

        /// <summary>
        /// Event feedback transaksi ke UI klien lokal (success, message).
        /// </summary>
        public event Action<bool, string> OnTransactionFeedback;

        // ==========================================
        // DATABASE-LIKE TRANSACTION LOCK SYSTEM
        // ==========================================
        private readonly HashSet<HarvestType> _lockedHarvestTypes = new();
        private readonly Dictionary<HarvestType, float> _lockTimestamps = new();
        private const float LOCK_TIMEOUT_SECONDS = 2.5f;

        public string SiloName => siloName;
        public int MaxCapacity => maxCapacity;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }

            storedItems.OnChange += HandleSyncListChanged;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            storedItems.OnChange -= HandleSyncListChanged;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();

            // Isi stok awal jika silo masih kosong saat server dimulai
            if (storedItems.Count == 0 && defaultStock != null && defaultStock.Count > 0)
            {
                for (int i = 0; i < defaultStock.Count; i++)
                {
                    if (defaultStock[i].itemType != HarvestType.None && defaultStock[i].quantity > 0)
                    {
                        storedItems.Add(defaultStock[i]);
                    }
                }
            }
        }

        private void HandleSyncListChanged(SyncListOperation op, int index, HarvestSlot oldItem, HarvestSlot newItem, bool asServer)
        {
            OnStorageChanged?.Invoke();
        }

        // ==========================================
        // QUERY METHODS (CLIENT & SERVER SAFE)
        // ==========================================
        public int GetTotalItemCount()
        {
            int total = 0;
            for (int i = 0; i < storedItems.Count; i++)
            {
                total += storedItems[i].quantity;
            }
            return total;
        }

        public int GetItemCount(HarvestType type)
        {
            for (int i = 0; i < storedItems.Count; i++)
            {
                if (storedItems[i].itemType == type)
                {
                    return storedItems[i].quantity;
                }
            }
            return 0;
        }

        public bool HasSpaceFor(int amount)
        {
            return (GetTotalItemCount() + amount) <= maxCapacity;
        }

        public List<HarvestSlot> GetStoredSlotsCopy()
        {
            List<HarvestSlot> copy = new List<HarvestSlot>(storedItems.Count);
            for (int i = 0; i < storedItems.Count; i++)
            {
                copy.Add(storedItems[i]);
            }
            return copy;
        }

        // ==========================================
        // DATABASE LOCK IMPLEMENTATION (SERVER ONLY)
        // ==========================================
        private bool TryAcquireTransactionLock(HarvestType type)
        {
            if (_lockedHarvestTypes.Contains(type))
            {
                // Cek timeout pencegah deadlock
                if (_lockTimestamps.TryGetValue(type, out float lockTime) && Time.time - lockTime > LOCK_TIMEOUT_SECONDS)
                {
                    Debug.LogWarning($"[SiloStorage Lock] Lock untuk item {type} kadaluarsa (> {LOCK_TIMEOUT_SECONDS}s). Mengambil alih lock.");
                    _lockTimestamps[type] = Time.time;
                    return true;
                }
                return false; // Item sedang diproses transaksi lain
            }

            _lockedHarvestTypes.Add(type);
            _lockTimestamps[type] = Time.time;
            return true;
        }

        private void ReleaseTransactionLock(HarvestType type)
        {
            _lockedHarvestTypes.Remove(type);
            _lockTimestamps.Remove(type);
        }

        // ==========================================
        // API-LIKE ENDPOINT: INSERT ITEM KE SILO
        // ==========================================
        /// <summary>
        /// Endpoint API: Memasukkan item yang sedang dipegang pemain ke dalam Silo.
        /// Melindungi transaksi dengan locking database agar tidak terjadi desync saat multi-user.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void Api_RequestInsertHeldItem(NetworkObject playerNob, int amount = 1)
        {
            if (playerNob == null) return;
            NetworkConnection callerConn = playerNob.Owner;

            PlayerInventory playerInv = playerNob.GetComponent<PlayerInventory>();
            if (playerInv == null || !playerInv.HasItem)
            {
                Target_OnTransactionResult(callerConn, false, "Gagal: Pemain tidak sedang memegang item apapun.", HarvestType.None, 0);
                return;
            }

            HarvestType type = playerInv.HeldType;
            int transferAmount = Mathf.Min(amount, playerInv.HeldQuantity);

            if (transferAmount <= 0)
            {
                Target_OnTransactionResult(callerConn, false, "Gagal: Jumlah item tidak valid.", type, 0);
                return;
            }

            // 1. Dapatkan Lock Transaksi (Pessimistic Lock)
            if (!TryAcquireTransactionLock(type))
            {
                Target_OnTransactionResult(callerConn, false, $"Gagal: Item {type} sedang diproses oleh pemain lain. Coba lagi.", type, 0);
                return;
            }

            try
            {
                // 2. Validasi Kapasitas Silo
                if (!HasSpaceFor(transferAmount))
                {
                    Target_OnTransactionResult(callerConn, false, "Gagal: Silo sudah penuh!", type, 0);
                    return;
                }

                // 3. Mutasi Silo Storage
                int existingIndex = -1;
                for (int i = 0; i < storedItems.Count; i++)
                {
                    if (storedItems[i].itemType == type)
                    {
                        existingIndex = i;
                        break;
                    }
                }

                if (existingIndex >= 0)
                {
                    int newQty = storedItems[existingIndex].quantity + transferAmount;
                    storedItems[existingIndex] = new HarvestSlot(type, newQty);
                }
                else
                {
                    storedItems.Add(new HarvestSlot(type, transferAmount));
                }

                // 4. Mutasi Player Inventory
                playerInv.ServerConsumeHeldItem(transferAmount);

                Debug.Log($"[SiloStorage API] Client {callerConn?.ClientId} berhasil menyetor {transferAmount} {type}. Sisa di Silo: {GetItemCount(type)}.");
                Target_OnTransactionResult(callerConn, true, $"Berhasil menyetor {transferAmount} {type} ke Silo.", type, transferAmount);
            }
            finally
            {
                // 5. Lepaskan Lock
                ReleaseTransactionLock(type);
            }
        }

        // ==========================================
        // API-LIKE ENDPOINT: PICK ITEM DARI SILO
        // ==========================================
        /// <summary>
        /// Endpoint API: Mengambil sejumlah item dari Silo ke tangan pemain.
        /// Melindungi transaksi dengan locking database agar dua pemain tidak mengambil unit yang sama secara bersamaan.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void Api_RequestPickItem(NetworkObject playerNob, HarvestType type, int amount = 1)
        {
            if (playerNob == null || type == HarvestType.None || amount <= 0) return;
            NetworkConnection callerConn = playerNob.Owner;

            PlayerInventory playerInv = playerNob.GetComponent<PlayerInventory>();
            if (playerInv == null)
            {
                Target_OnTransactionResult(callerConn, false, "Gagal: PlayerInventory tidak ditemukan pada karakter.", type, 0);
                return;
            }

            // Aturan Batch 2: Tiap player hanya bisa membawa 1 jenis item panen
            if (playerInv.HasItem && playerInv.HeldType != type)
            {
                Target_OnTransactionResult(callerConn, false, $"Gagal: Tanganmu sedang membawa {playerInv.HeldType}. Kosongkan tangan terlebih dahulu.", type, 0);
                return;
            }

            // 1. Dapatkan Lock Transaksi
            if (!TryAcquireTransactionLock(type))
            {
                Target_OnTransactionResult(callerConn, false, $"Gagal: Item {type} sedang diproses pemain lain. Coba beberapa saat lagi.", type, 0);
                return;
            }

            try
            {
                // 2. Validasi Stok Silo
                int existingIndex = -1;
                for (int i = 0; i < storedItems.Count; i++)
                {
                    if (storedItems[i].itemType == type)
                    {
                        existingIndex = i;
                        break;
                    }
                }

                if (existingIndex < 0 || storedItems[existingIndex].quantity <= 0)
                {
                    Target_OnTransactionResult(callerConn, false, $"Gagal: Stok {type} di Silo sudah habis!", type, 0);
                    return;
                }

                HarvestSlot slot = storedItems[existingIndex];
                int actualAmount = Mathf.Min(amount, slot.quantity);

                // 3. Mutasi Silo Storage
                int remainingSilo = slot.quantity - actualAmount;
                if (remainingSilo > 0)
                {
                    storedItems[existingIndex] = new HarvestSlot(type, remainingSilo);
                }
                else
                {
                    storedItems.RemoveAt(existingIndex);
                }

                // 4. Mutasi Player Inventory (Aturan single-item: isi tangan pemain)
                int newPlayerQty = playerInv.HasItem ? (playerInv.HeldQuantity + actualAmount) : actualAmount;
                playerInv.ServerSetHeldItem(type, newPlayerQty);

                Debug.Log($"[SiloStorage API] Client {callerConn?.ClientId} berhasil mengambil {actualAmount} {type}. Sisa di Silo: {GetItemCount(type)}.");
                Target_OnTransactionResult(callerConn, true, $"Berhasil mengambil {actualAmount} {type}.", type, actualAmount);
            }
            finally
            {
                // 5. Lepaskan Lock
                ReleaseTransactionLock(type);
            }
        }

        // ==========================================
        // CLIENT TARGET RPC (FEEDBACK TRANSAKSI)
        // ==========================================
        [TargetRpc]
        private void Target_OnTransactionResult(NetworkConnection conn, bool success, string message, HarvestType type, int quantity)
        {
            if (success)
            {
                Debug.Log($"<color=green>[Silo Transaksi]</color> {message}");
            }
            else
            {
                Debug.LogWarning($"<color=orange>[Silo Transaksi]</color> {message}");
            }

            OnTransactionFeedback?.Invoke(success, message);
        }
    }
}
