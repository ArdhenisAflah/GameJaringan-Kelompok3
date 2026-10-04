using System;
using System.Collections.Generic;
using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;

namespace SiloSystem
{
    /// <summary>
    /// Komponen NetworkBehaviour untuk mengelola inventori/penyimpanan hasil panen di dalam Silo.
    /// Menggunakan FishNet SyncList untuk sinkronisasi otomatis ke seluruh klien (Server-Authoritative).
    /// Modular: dapat menampung berbagai macam HarvestType (Padi, Jagung, Gandum, dll).
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
            new HarvestSlot(HarvestType.Padi, 10)
        };

        /// <summary>
        /// SyncList FishNet: Menyimpan daftar slot panen di jaringan.
        /// Otomatis sinkron dari Server ke seluruh Client.
        /// </summary>
        public readonly SyncList<HarvestSlot> storedItems = new();

        /// <summary>
        /// Event yang dipicu setiap kali isi penyimpanan Silo berubah (bermanfaat untuk UI Batch 2).
        /// </summary>
        public event Action OnStorageChanged;

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

        /// <summary>
        /// Menghitung total seluruh item panen yang sedang tersimpan di Silo.
        /// </summary>
        public int GetTotalItemCount()
        {
            int total = 0;
            for (int i = 0; i < storedItems.Count; i++)
            {
                total += storedItems[i].quantity;
            }
            return total;
        }

        /// <summary>
        /// Mengambil jumlah stok spesifik untuk satu jenis tanaman.
        /// </summary>
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

        /// <summary>
        /// Mengecek apakah Silo masih memiliki ruang untuk menampung sejumlah hasil panen.
        /// </summary>
        public bool HasSpaceFor(int amount)
        {
            return (GetTotalItemCount() + amount) <= maxCapacity;
        }

        /// <summary>
        /// Menyimpan hasil panen ke dalam Silo (Client memanggil via ServerRpc).
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void ServerDepositItem(HarvestType type, int amount)
        {
            if (type == HarvestType.None || amount <= 0) return;

            int currentTotal = GetTotalItemCount();
            int allowedAmount = Mathf.Min(amount, maxCapacity - currentTotal);
            if (allowedAmount <= 0)
            {
                Debug.LogWarning($"[SiloStorage] Kapasitas Silo penuh! Tidak dapat menyimpan {type}.");
                return;
            }

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
                int newQty = storedItems[existingIndex].quantity + allowedAmount;
                storedItems[existingIndex] = new HarvestSlot(type, newQty);
            }
            else
            {
                storedItems.Add(new HarvestSlot(type, allowedAmount));
            }

            Debug.Log($"[SiloStorage] Berhasil menyimpan {allowedAmount} {type} ke dalam Silo. Total tersimpan: {GetItemCount(type)}.");
        }

        /// <summary>
        /// Mengambil hasil panen dari dalam Silo (Client memanggil via ServerRpc).
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void ServerWithdrawItem(HarvestType type, int amount)
        {
            if (type == HarvestType.None || amount <= 0) return;

            int existingIndex = -1;
            for (int i = 0; i < storedItems.Count; i++)
            {
                if (storedItems[i].itemType == type)
                {
                    existingIndex = i;
                    break;
                }
            }

            if (existingIndex < 0)
            {
                Debug.LogWarning($"[SiloStorage] Item {type} tidak ditemukan di dalam Silo.");
                return;
            }

            HarvestSlot slot = storedItems[existingIndex];
            int withdrawAmount = Mathf.Min(amount, slot.quantity);
            int remaining = slot.quantity - withdrawAmount;

            if (remaining > 0)
            {
                storedItems[existingIndex] = new HarvestSlot(type, remaining);
            }
            else
            {
                storedItems.RemoveAt(existingIndex);
            }

            Debug.Log($"[SiloStorage] Berhasil mengambil {withdrawAmount} {type} dari Silo. Sisa: {GetItemCount(type)}.");
        }

        /// <summary>
        /// Mengembalikan daftar salinan slot yang saat ini tersimpan untuk pembacaan UI.
        /// </summary>
        public List<HarvestSlot> GetStoredSlotsCopy()
        {
            List<HarvestSlot> copy = new List<HarvestSlot>(storedItems.Count);
            for (int i = 0; i < storedItems.Count; i++)
            {
                copy.Add(storedItems[i]);
            }
            return copy;
        }
    }
}
