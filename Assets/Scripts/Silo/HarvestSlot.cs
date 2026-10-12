using System;

namespace SiloSystem
{
    /// <summary>
    /// Kategori item yang dibawa pemain (Hasil Panen vs Benih Tanaman).
    /// </summary>
    public enum ItemCategory : byte
    {
        Crop = 0,
        Seed = 1
    }

    /// <summary>
    /// Struct data yang merepresentasikan satu jenis item panen beserta jumlahnya.
    /// Serializable untuk FishNet networking dan penyimpanan.
    /// </summary>
    [Serializable]
    public struct HarvestSlot : IEquatable<HarvestSlot>
    {
        public HarvestType itemType;
        public int quantity;
        public ItemCategory category;

        public HarvestSlot(HarvestType itemType, int quantity, ItemCategory category = ItemCategory.Crop)
        {
            this.itemType = itemType;
            this.quantity = quantity;
            this.category = category;
        }

        public bool Equals(HarvestSlot other)
        {
            return itemType == other.itemType && quantity == other.quantity && category == other.category;
        }

        public override bool Equals(object obj)
        {
            return obj is HarvestSlot other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine((int)itemType, quantity, (int)category);
        }

        public override string ToString()
        {
            string catTag = category == ItemCategory.Seed ? " [Benih]" : "";
            return $"{itemType}{catTag} x{quantity}";
        }
    }
}
