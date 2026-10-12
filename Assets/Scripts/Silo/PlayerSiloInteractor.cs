using System;
using UnityEngine;
using FishNet.Object;

namespace SiloSystem
{
    /// <summary>
    /// Komponen NetworkBehaviour pada Player untuk mengonsumsi API Silo (Insert & Pick).
    /// Mendeteksi keberadaan Silo di sekitar pemain dan menyediakan shortcut cepat (misal tombol F)
    /// untuk menyetor item yang sedang dipegang langsung ke Silo tanpa harus membuka panel UI.
    /// </summary>
    public class PlayerSiloInteractor : NetworkBehaviour
    {
        [Header("Konfigurasi Input Interaksi")]
        [Tooltip("Tombol untuk menyetor cepat item yang sedang dibawa ke Silo terdekat.")]
        [SerializeField] private KeyCode quickDepositKey = KeyCode.F;

        [Tooltip("Jarak maksimal interaksi dengan Silo.")]
        [SerializeField] private float interactionDistance = 2.5f;

        [Header("Referensi Komponen Lokal")]
        [SerializeField] private PlayerInventory playerInventory;

        private SiloStorage _nearestSilo;
        private NetworkObject _networkObject;

        public SiloStorage NearestSilo => _nearestSilo;
        public bool IsNearSilo => _nearestSilo != null;

        public event Action<SiloStorage, bool> OnSiloProximityChanged;

        private void Awake()
        {
            if (playerInventory == null)
            {
                playerInventory = GetComponent<PlayerInventory>();
            }

            _networkObject = GetComponent<NetworkObject>();
        }

        private void Update()
        {
            // Hanya client pemilik karakter (IsOwner) yang membaca input dan mengevaluasi interaksi
            if (!IsOwner) return;

            EvaluateNearestSilo();

            // Shortcut Quick Deposit: Tekan 'F' saat membawa barang di dekat Silo
            if (_nearestSilo != null && playerInventory != null && playerInventory.HasItem)
            {
                if (Input.GetKeyDown(quickDepositKey))
                {
                    QuickDepositHeldItem();
                }
            }
        }

        private void EvaluateNearestSilo()
        {
            var silos = FindObjectsOfType<SiloStorage>();
            SiloStorage closest = null;
            float minDistance = interactionDistance;

            for (int i = 0; i < silos.Length; i++)
            {
                float dist = Vector2.Distance(transform.position, silos[i].transform.position);
                if (dist <= minDistance)
                {
                    minDistance = dist;
                    closest = silos[i];
                }
            }

            if (_nearestSilo != closest)
            {
                _nearestSilo = closest;
                OnSiloProximityChanged?.Invoke(_nearestSilo, _nearestSilo != null);

                if (_nearestSilo != null && playerInventory != null && playerInventory.HasItem)
                {
                    Debug.Log($"<color=cyan>[PlayerSiloInteractor]</color> Berada di dekat '{_nearestSilo.SiloName}'. Tekan '[{quickDepositKey}]' untuk Setor Cepat item di tangan.");
                }
            }
        }

        /// <summary>
        /// Mengonsumsi API Api_RequestInsertHeldItem untuk menyetor hasil panen di tangan ke Silo terdekat.
        /// </summary>
        public void QuickDepositHeldItem()
        {
            if (_nearestSilo == null)
            {
                Debug.LogWarning("[PlayerSiloInteractor] Tidak ada Silo di dekat pemain untuk disetor!");
                return;
            }

            if (playerInventory == null || !playerInventory.HasItem)
            {
                Debug.Log("[PlayerSiloInteractor] Tangan pemain kosong, tidak ada item untuk disetor.");
                return;
            }

            Debug.Log($"[PlayerSiloInteractor] Mengirim permintaan setor cepat {playerInventory.HeldType} x1 ke '{_nearestSilo.SiloName}'...");
            _nearestSilo.Api_RequestInsertHeldItem(_networkObject, 1);
        }

        /// <summary>
        /// Mengonsumsi API Api_RequestPickItem untuk mengambil item dari Silo ke tangan pemain.
        /// </summary>
        public void RequestPickItem(SiloStorage silo, HarvestType type, int amount = 1)
        {
            if (silo == null) return;
            silo.Api_RequestPickItem(_networkObject, type, amount);
        }

        /// <summary>
        /// Mengonsumsi API Api_RequestInsertHeldItem untuk menyetor item dari tangan pemain ke Silo.
        /// </summary>
        public void RequestInsertHeldItem(SiloStorage silo, int amount = 1)
        {
            if (silo == null) return;
            silo.Api_RequestInsertHeldItem(_networkObject, amount);
        }
    }
}
