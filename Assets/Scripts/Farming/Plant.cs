using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;

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
        [SerializeField] private string plantName = "Default Plant";

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
        public int GrowthStage => _growthStage.Value;
        public bool IsMature => _growthStage.Value >= maxGrowthStage;

        private void Awake()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }

            _growthStage.OnChange += OnGrowthStageChanged;
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

        private void OnDrawGizmos()
        {
            // Visualisasi titik tanaman di Scene editor
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, 0.3f);
        }
    }
}
