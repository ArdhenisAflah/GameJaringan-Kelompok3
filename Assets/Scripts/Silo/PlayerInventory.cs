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
        /// </summary>
        public bool ServerSetHeldItem(HarvestType type, int quantity)
        {
            if (!IsServerStarted)
            {
                Debug.LogError("[PlayerInventory] ServerSetHeldItem hanya boleh dipanggil di Server!");
                return false;
            }

            if (type == HarvestType.None || quantity <= 0)
            {
                heldItem.Value = new HarvestSlot(HarvestType.None, 0);
                return true;
            }

            heldItem.Value = new HarvestSlot(type, quantity);
            return true;
        }

        /// <summary>
        /// Mengosongkan tangan pemain (Server-Authoritative).
        /// </summary>
        public void ServerClearHeldItem()
        {
            if (!IsServerStarted) return;
            heldItem.Value = new HarvestSlot(HarvestType.None, 0);
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
                heldItem.Value = new HarvestSlot(heldItem.Value.itemType, currentQty - amount);
            }
            return true;
        }
    }
}
