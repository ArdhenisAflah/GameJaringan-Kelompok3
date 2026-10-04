using System;
using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;

namespace SiloSystem
{
    /// <summary>
    /// Komponen NetworkBehaviour pada karakter pemain untuk mengelola item yang sedang dibawa di tangan.
    /// Aturan Batch 2: Tiap player hanya bisa membawa 1 jenis item panen dalam satu waktu.
    /// Menggunakan SyncVar FishNet modern agar status item sinkron ke seluruh pemain di room.
    /// </summary>
    public class PlayerInventory : NetworkBehaviour
    {
        /// <summary>
        /// SyncVar menyimpan item panen yang sedang dipegang pemain.
        /// </summary>
        public readonly SyncVar<HarvestSlot> heldItem = new(new HarvestSlot(HarvestType.None, 0));

        /// <summary>
        /// Event lokal dipicu ketika item di tangan berubah (untuk update HUD / UI).
        /// </summary>
        public event Action<HarvestSlot> OnHeldItemChanged;

        public bool HasItem => heldItem.Value.itemType != HarvestType.None && heldItem.Value.quantity > 0;
        public HarvestType HeldType => heldItem.Value.itemType;
        public int HeldQuantity => heldItem.Value.quantity;
        public ItemCategory HeldCategory => heldItem.Value.category;
        public bool IsHoldingCrop => HasItem && heldItem.Value.category == ItemCategory.Crop;
        public bool IsHoldingSeed => HasItem && heldItem.Value.category == ItemCategory.Seed;

        private void Awake()
        {
            heldItem.OnChange += HandleHeldItemChanged;
        }

        private void OnDestroy()
        {
            heldItem.OnChange -= HandleHeldItemChanged;
        }

        private void HandleHeldItemChanged(HarvestSlot prev, HarvestSlot next, bool asServer)
        {
            OnHeldItemChanged?.Invoke(next);
        }

        /// <summary>
        /// Mengisi tangan pemain dengan item (Server-Authoritative).
        /// Mendukung hasil panen (Crop) maupun benih tanaman (Seed).
        /// </summary>
        public bool ServerSetHeldItem(HarvestType type, int quantity, ItemCategory category = ItemCategory.Crop)
        {
            if (!IsServerStarted)
            {
                Debug.LogError("[PlayerInventory] ServerSetHeldItem hanya boleh dipanggil di Server!");
                return false;
            }

            if (type == HarvestType.None || quantity <= 0)
            {
                heldItem.Value = new HarvestSlot(HarvestType.None, 0, ItemCategory.Crop);
                return true;
            }

            heldItem.Value = new HarvestSlot(type, quantity, category);
            return true;
        }

        /// <summary>
        /// Mengosongkan tangan pemain (Server-Authoritative).
        /// </summary>
        public void ServerClearHeldItem()
        {
            if (!IsServerStarted) return;
            heldItem.Value = new HarvestSlot(HarvestType.None, 0, ItemCategory.Crop);
        }

        /// <summary>
        /// Mengurangi kuantitas item di tangan pemain sejumlah amount (Server-Authoritative).
        /// </summary>
        public bool ServerConsumeHeldItem(int amount)
        {
            if (!IsServerStarted) return false;
            if (!HasItem || amount <= 0) return false;

            int currentQty = heldItem.Value.quantity;
            if (currentQty <= amount)
            {
                ServerClearHeldItem();
            }
            else
            {
                heldItem.Value = new HarvestSlot(heldItem.Value.itemType, currentQty - amount, heldItem.Value.category);
            }
            return true;
        }

        /// <summary>
        /// ServerRpc: Permintaan pemain untuk mengambil benih dari SeedSource terdekat.
        /// Server memvalidasi jarak ke sumber benih dan kondisi tangan pemain (Server-Authoritative).
        /// </summary>
        [ServerRpc]
        public void ServerRequestPickSeed(Vector2 sourcePosition, HarvestType seedType)
        {
            if (!IsServerStarted) return;

            // 1. Validasi jarak pemain ke sumber benih (Anti-Cheat)
            float dist = Vector2.Distance(transform.position, sourcePosition);
            if (dist > 3.5f)
            {
                Debug.LogWarning($"[PlayerInventory] Permintaan benih ditolak: Jarak terlalu jauh ({dist:F2}m > 3.5m).");
                return;
            }

            // 2. Validasi tangan pemain
            if (HasItem)
            {
                if (IsHoldingCrop)
                {
                    Debug.LogWarning($"[PlayerInventory] Pemain {OwnerId} sedang membawa hasil panen '{HeldType}'. Setor ke Silo terlebih dahulu.");
                    return;
                }

                if (IsHoldingSeed)
                {
                    Debug.LogWarning($"[PlayerInventory] Pemain {OwnerId} sudah membawa benih '{HeldType}'.");
                    return;
                }
            }

            // 3. Set benih ke tangan pemain
            ServerSetHeldItem(seedType, 1, ItemCategory.Seed);
            Debug.Log($"<color=green>[PlayerInventory]</color> Pemain {OwnerId} berhasil mengambil 1 benih {seedType} dari sumber benih!");
        }
    }
}
