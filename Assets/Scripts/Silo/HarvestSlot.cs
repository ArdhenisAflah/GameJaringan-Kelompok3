using System;

namespace SiloSystem
{
    /// <summary>
    /// Struct data yang merepresentasikan satu jenis item panen beserta jumlahnya.
    /// Serializable untuk FishNet networking dan penyimpanan.
    /// </summary>
    [Serializable]
    public struct HarvestSlot : IEquatable<HarvestSlot>
    {
        public HarvestType itemType;
        public int quantity;

        public HarvestSlot(HarvestType itemType, int quantity)
        {
            this.itemType = itemType;
            this.quantity = quantity;
        }

        public bool Equals(HarvestSlot other)
        {
            return itemType == other.itemType && quantity == other.quantity;
        }

        public override bool Equals(object obj)
        {
            return obj is HarvestSlot other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine((int)itemType, quantity);
        }

        public override string ToString()
        {
            return $"{itemType} x{quantity}";
        }
    }
}
