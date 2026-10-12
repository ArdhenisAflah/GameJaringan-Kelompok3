using System;
using UnityEngine;
using FishNet.Object;
using SiloSystem;

namespace Farming
{
    /// <summary>
    /// Komponen interaksi pada Player untuk berinteraksi dengan SeedSource.
    /// Mendeteksi sumber benih terdekat dan mengeksekusi pengambilan benih saat tombol 'F' ditekan.
    /// </summary>
    [RequireComponent(typeof(PlayerInventory))]
    public class PlayerSeedInteractor : MonoBehaviour
    {
        public bool IsOwner => _networkObject != null && _networkObject.IsOwner;
        [Header("Konfigurasi Input")]
        [Tooltip("Tombol untuk mengambil benih dari SeedSource terdekat.")]
        [SerializeField] private KeyCode pickSeedKey = KeyCode.F;

        private PlayerInventory _playerInventory;
        private NetworkObject _networkObject;
        private SeedSource _nearestSeedSource;

        public SeedSource NearestSeedSource => _nearestSeedSource;
        public bool IsNearSeedSource => _nearestSeedSource != null;

        public event Action<SeedSource, bool> OnSeedSourceProximityChanged;

        private void Awake()
        {
            _playerInventory = GetComponent<PlayerInventory>();
            _networkObject = GetComponent<NetworkObject>();
        }

        private void Update()
        {
            if (!IsOwner) return;

            EvaluateNearestSeedSource();

            if (_nearestSeedSource != null && Input.GetKeyDown(pickSeedKey))
            {
                TryPickSeed();
            }
        }

        private void EvaluateNearestSeedSource()
        {
            var sources = FindObjectsOfType<SeedSource>();
            SeedSource closest = null;
            float minDistance = float.MaxValue;

            for (int i = 0; i < sources.Length; i++)
            {
                float dist = Vector2.Distance(transform.position, sources[i].transform.position);
                if (dist <= sources[i].InteractionRadius && dist < minDistance)
                {
                    minDistance = dist;
                    closest = sources[i];
                }
            }

            if (_nearestSeedSource != closest)
            {
                _nearestSeedSource = closest;
                OnSeedSourceProximityChanged?.Invoke(_nearestSeedSource, _nearestSeedSource != null);

                if (_nearestSeedSource != null)
                {
                    Debug.Log($"<color=cyan>[PlayerSeedInteractor]</color> Berada di dekat '{_nearestSeedSource.SeedName}'. Tekan [{pickSeedKey}] untuk mengambil benih.");
                }
            }
        }

        public void TryPickSeed()
        {
            if (_nearestSeedSource == null) return;

            if (_playerInventory != null)
            {
                if (_playerInventory.IsHoldingCrop)
                {
                    Debug.LogWarning($"<color=orange>[PlayerSeedInteractor]</color> Tangan sedang membawa hasil panen '{_playerInventory.HeldType}'! Setor ke Silo terlebih dahulu.");
                    return;
                }

                if (_playerInventory.IsHoldingSeed)
                {
                    Debug.LogWarning($"<color=orange>[PlayerSeedInteractor]</color> Tangan sudah membawa '{_playerInventory.HeldType} [Benih]'! Tanam terlebih dahulu dengan tombol [E].");
                    return;
                }
            }

            Debug.Log($"[PlayerSeedInteractor] Mengambil {_nearestSeedSource.SeedName} dari sumber benih...");
            _playerInventory.ServerRequestPickSeed(_nearestSeedSource.transform.position, _nearestSeedSource.SeedType);
        }
    }
}
