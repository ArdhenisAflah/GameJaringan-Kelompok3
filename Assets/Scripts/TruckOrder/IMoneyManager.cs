using System;
using UnityEngine;

namespace TruckOrder
{
    /// <summary>
    /// Interface minimal untuk sistem mata uang/keuangan di dalam game.
    /// Memudahkan integrasi dengan sistem ekonomi atau toko di masa depan.
    /// </summary>
    public interface IMoneyManager
    {
        int CurrentMoney { get; }
        void AddMoney(int amount);
        bool TrySpendMoney(int amount);
    }

    /// <summary>
    /// Implementasi default penyimpan saldo uang pemain di runtime (in-memory).
    /// </summary>
    public class SimpleMoneyManager : MonoBehaviour, IMoneyManager
    {
        public static SimpleMoneyManager Instance { get; private set; }

        [Header("Konfigurasi Saldo")]
        [SerializeField] private int initialMoney = 0;
        [SerializeField] private int currentMoney = 0;

        public int CurrentMoney => currentMoney;

        public static event Action<int> OnMoneyChanged;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                currentMoney = initialMoney;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
            }
        }

        public void AddMoney(int amount)
        {
            if (amount <= 0) return;
            currentMoney += amount;
            Debug.Log($"<color=yellow>[MoneyManager]</color> +{amount} Gold! Total Saldo: {currentMoney}");
            OnMoneyChanged?.Invoke(currentMoney);
        }

        public bool TrySpendMoney(int amount)
        {
            if (amount <= 0) return false;
            if (currentMoney >= amount)
            {
                currentMoney -= amount;
                Debug.Log($"<color=yellow>[MoneyManager]</color> -{amount} Gold! Sisa Saldo: {currentMoney}");
                OnMoneyChanged?.Invoke(currentMoney);
                return true;
            }
            return false;
        }
    }
}
