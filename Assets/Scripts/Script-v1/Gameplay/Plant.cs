using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using SiloSystem;

namespace Farming
{
    /// <summary>
    /// Komponen NetworkBehaviour untuk objek Tanaman di jaringan FishNet.
    /// Diletakkan pada prefab Tanaman yang memiliki NetworkObject.
    /// Semua state tanaman (seperti fase pertumbuhan) dikelola secara otoritatif oleh Server.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class Plant : NetworkBehaviour
    {
        [Header("Pengaturan Tanaman")]
        [Tooltip("Nama jenis tanaman.")]
        [SerializeField] private string plantName = "Padi";

        [Tooltip("Jenis hasil panen yang dihasilkan tanaman ini.")]
        [SerializeField] private HarvestType cropType = HarvestType.Padi;

        [Tooltip("Jumlah hasil panen yang diperoleh per panen.")]
        [SerializeField] private int harvestYield = 1;

        [Tooltip("Jumlah tahap pertumbuhan maksimal sampai siap panen.")]
        [SerializeField] private int maxGrowthStage = 3;

        [Tooltip("Waktu dalam detik untuk naik ke tahap pertumbuhan berikutnya (di server).")]
        [SerializeField] private float timePerStage = 10f;

        [Header("Visual")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Sprite[] stageSprites;

        /// <summary>
        /// SyncVar modern FishNet v4: Nilai disinkronkan otomatis dari Server ke semua Client.
        /// Event OnChange didaftarkan di Awake() untuk memperbarui sprite secara otomatis di klien.
        /// </summary>
        private readonly SyncVar<int> _growthStage = new();

        private float _growthTimer;

        public string PlantName => plantName;
        public HarvestType CropType => cropType;
        public int HarvestYield => harvestYield;
        public int GrowthStage => _growthStage.Value;
        public bool IsMature => _growthStage.Value >= maxGrowthStage;

        /// <summary>
        /// Mengeksekusi pemanenan tanaman oleh pemain di Server (Server-Authoritative).
        /// Memasukkan hasil panen ke PlayerInventory jika tangan pemain kosong atau bertipe sama.
        /// </summary>
        public bool ServerHarvest(NetworkObject playerNob)
        {
            if (!IsServerStarted) return false;
            if (!IsMature) return false;
            if (playerNob == null) return false;

            PlayerInventory inv = playerNob.GetComponent<PlayerInventory>();
            if (inv == null) return false;

            // Aturan Batch 2: Pemain hanya bisa memegang 1 jenis item dalam satu waktu
            if (inv.HasItem && inv.HeldType != cropType)
            {
                Debug.Log($"[Plant] Pemain {playerNob.OwnerId} sedang membawa {inv.HeldType}, tidak dapat memanen {cropType}.");
                return false;
            }

            int newQty = inv.HasItem ? inv.HeldQuantity + harvestYield : harvestYield;
            bool success = inv.ServerSetHeldItem(cropType, newQty);
            if (success)
            {
                Debug.Log($"<color=green>[Plant]</color> Tanaman '{plantName}' berhasil dipanen oleh Pemain {playerNob.OwnerId}! Menambahkan {cropType} x{harvestYield}.");

                // Despawn tanaman resmi dari server FishNet
                ServerManager.Despawn(gameObject);
                return true;
            }

            return false;
        }

        private void Awake()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }

            _growthStage.OnChange += OnGrowthStageChanged;
        }

        private void OnDestroy()
        {
            _growthStage.OnChange -= OnGrowthStageChanged;

            if (IsServerStarted && LahanManagerTilemap.Instance != null)
            {
                LahanManagerTilemap.Instance.HapusTanaman(transform.position);
            }
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            _growthTimer = timePerStage;
        }

        private void Update()
        {
            // Pertumbuhan hanya diproses di Server (Server Authority)
            if (!IsServerStarted) return;

            if (!IsMature)
            {
                _growthTimer -= Time.deltaTime;
                if (_growthTimer <= 0f)
                {
                    _growthTimer = timePerStage;
                    _growthStage.Value++;
                }
            }
        }

        /// <summary>
        /// Callback otomatis saat SyncVar _growthStage berubah nilainya di client.
        /// </summary>
        private void OnGrowthStageChanged(int prev, int next, bool asServer)
        {
            UpdateVisuals(next);
        }

        private void UpdateVisuals(int stage)
        {
            if (spriteRenderer != null && stageSprites != null && stage < stageSprites.Length)
            {
                if (stageSprites[stage] != null)
                {
                    spriteRenderer.sprite = stageSprites[stage];
                }
            }
        }

        public static event System.Action<Plant> OnAnyPlantDamaged;
        public static event System.Action<Plant> OnAnyPlantEaten;

        private Coroutine _flashCoroutine;
        private Color _originalColor = Color.white;

        /// <summary>
        /// Dipanggil saat musuh menggigit tanaman ini untuk memicu efek visual putih (flash)
        /// dan observer event bagi Technical VFX coder.
        /// </summary>
        public void TriggerBiteFlash(float duration = 0.15f)
        {
            OnAnyPlantDamaged?.Invoke(this);

            if (IsServerStarted)
            {
                ObserversTriggerBiteFlash(duration);
            }
            else
            {
                StartFlashRoutine(duration);
            }
        }

        [ObserversRpc(BufferLast = false)]
        private void ObserversTriggerBiteFlash(float duration)
        {
            StartFlashRoutine(duration);
        }

        private void StartFlashRoutine(float duration)
        {
            if (spriteRenderer == null) return;
            if (_flashCoroutine != null) StopCoroutine(_flashCoroutine);
            _flashCoroutine = StartCoroutine(FlashWhiteRoutine(duration));
        }

        private System.Collections.IEnumerator FlashWhiteRoutine(float duration)
        {
            if (spriteRenderer != null)
            {
                _originalColor = Color.white;
                // Tint putih terang / flash
                spriteRenderer.color = new Color(2f, 2f, 2f, 1f);
                yield return new WaitForSeconds(duration);
                if (spriteRenderer != null)
                {
                    spriteRenderer.color = _originalColor;
                }
            }
            _flashCoroutine = null;
        }

        /// <summary>
        /// Menghapus dan mendespawn tanaman di server saat selesai dimakan hama/musuh.
        /// </summary>
        public bool ServerConsumeByEnemy()
        {
            if (!IsServerStarted) return false;

            Debug.Log($"<color=red>[Plant]</color> Tanaman '{plantName}' habis dimakan oleh musuh!");
            OnAnyPlantEaten?.Invoke(this);
            ServerManager.Despawn(gameObject);
            return true;
        }

        private void OnDrawGizmos()
        {
            // Visualisasi titik tanaman di Scene editor
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, 0.3f);
        }
    }
}
