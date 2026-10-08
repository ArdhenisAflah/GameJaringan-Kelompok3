using System;
using UnityEngine;
using SiloSystem;

namespace TruckOrder
{
    /// <summary>
    /// Data murni representasi satu pesanan truk.
    /// Bukan MonoBehaviour agar ringan dan mudah diuji secara modular.
    /// </summary>
    [Serializable]
    public class Order
    {
        [SerializeField] private HarvestType itemType = HarvestType.Padi;
        [SerializeField] private int requiredAmount;
        [SerializeField] private int currentAmount;
        [SerializeField] private OrderStatus status = OrderStatus.Pending;
        [SerializeField] private int rewardPerUnit = 10;

        public HarvestType ItemType => itemType;
        public int RequiredAmount => requiredAmount;
        public int CurrentAmount => currentAmount;
        public OrderStatus Status => status;
        public int RewardPerUnit => rewardPerUnit;

        public int RemainingAmount => Mathf.Max(0, requiredAmount - currentAmount);
        public bool IsFulfilled => currentAmount >= requiredAmount;
        public int TotalReward => requiredAmount * rewardPerUnit;
        public float ProgressNormalized => requiredAmount > 0 ? Mathf.Clamp01((float)currentAmount / requiredAmount) : 0f;

        public Order(HarvestType itemType, int requiredAmount, int rewardPerUnit)
        {
            this.itemType = itemType;
            this.requiredAmount = Mathf.Max(1, requiredAmount);
            this.rewardPerUnit = Mathf.Max(1, rewardPerUnit);
            this.currentAmount = 0;
            this.status = OrderStatus.Pending;
        }

        /// <summary>
        /// Menambahkan progres kuantitas yang telah dimasukkan.
        /// </summary>
        public void AddAmount(int amount)
        {
            currentAmount = Mathf.Clamp(currentAmount + amount, 0, requiredAmount);
        }

        /// <summary>
        /// Mengatur kuantitas yang telah dimasukkan secara langsung (sinkronisasi network).
        /// </summary>
        public void SetCurrentAmount(int amount)
        {
            currentAmount = Mathf.Clamp(amount, 0, requiredAmount);
        }

        /// <summary>
        /// Mengatur kuantitas kebutuhan order secara langsung (sinkronisasi network).
        /// </summary>
        public void SetRequiredAmount(int amount)
        {
            requiredAmount = Mathf.Max(1, amount);
        }

        /// <summary>
        /// Mengatur status order (hanya dipanggil oleh OrderSystem).
        /// </summary>
        public void SetStatus(OrderStatus newStatus)
        {
            status = newStatus;
        }

        public override string ToString()
        {
            return $"Order[{itemType} {currentAmount}/{requiredAmount} - {status}]";
        }
    }
}
