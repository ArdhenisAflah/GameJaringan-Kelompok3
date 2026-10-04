using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using Farming.Timer;

namespace Farming
{
    /// <summary>
    /// Komponen NetworkBehaviour untuk objek Tanaman di jaringan FishNet.
    /// Diletakkan pada prefab Tanaman yang memiliki NetworkObject.
    /// Menggunakan sistem timer modular (IGrowthTimer) dan mendukung konfigurasi via PlantData (ScriptableObject).
    /// Semua state tanaman dikelola secara otoritatif oleh Server.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class Plant : NetworkBehaviour
    {
        [Header("Konfigurasi Data Tanaman (Modular)")]
        [Tooltip("Data tanaman berbasis ScriptableObject. Jika diisi, properti di bawahnya akan menggunakan data dari PlantData.")]
        [SerializeField] private PlantData plantData;

        [Header("Fallback Inspector (Digunakan jika PlantData kosong)")]
        [Tooltip("Nama jenis tanaman.")]
        [SerializeField] private string plantName = "Default Plant";

        [Tooltip("Jumlah tahap pertumbuhan maksimal sampai siap panen.")]
        [SerializeField] private int maxGrowthStage = 3;

        [Tooltip("Waktu dalam detik untuk naik ke tahap pertumbuhan berikutnya (fallback).")]
        [SerializeField] private float timePerStage = 10f;

        [Tooltip("Durasi spesifik per stage (fallback jika tidak memakai PlantData). Index 0 = stage 0->1, dst.")]
        [SerializeField] private float[] stageDurations;

        [Header("Visual")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Sprite[] stageSprites;

        /// <summary>
        /// SyncVar modern FishNet v4: Nilai disinkronkan otomatis dari Server ke semua Client.
        /// Event OnChange didaftarkan di Awake() untuk memperbarui sprite secara otomatis di klien.
        /// </summary>
        private readonly SyncVar<int> _growthStage = new();

        /// <summary>
        /// SyncVar rasio progres timer (0.0 - 1.0) untuk sinkronisasi tampilan progres ke seluruh client.
        /// </summary>
        private readonly SyncVar<float> _timerProgressRatio = new();

        // Sistem Timer Modular
        private IGrowthTimer _growthTimer;

        // Event C# untuk memisahkan logika / UI dari class ini
        public event System.Action<int> OnGrowthStageChangedEvent;
        public event System.Action OnPlantMatureEvent;

        public PlantData Data => plantData;
        public string PlantName => plantData != null ? plantData.plantName : plantName;
        public int MaxGrowthStage => plantData != null ? plantData.maxGrowthStage : maxGrowthStage;
        public int GrowthStage => _growthStage.Value;
        public bool IsMature => _growthStage.Value >= MaxGrowthStage;

        /// <summary>
        /// Progres timer pertumbuhan stage saat ini (0.0 sampai 1.0).
        /// </summary>
        public float CurrentStageProgress => IsServerStarted ? (_growthTimer?.ProgressRatio ?? 0f) : _timerProgressRatio.Value;

        /// <summary>
        /// Sisa waktu (detik) untuk stage saat ini (khusus Server).
        /// </summary>
        public float RemainingTimeForCurrentStage => IsServerStarted ? (_growthTimer?.RemainingTime ?? 0f) : 0f;

        /// <summary>
        /// Akses langsung ke instance IGrowthTimer (hanya valid di Server).
        /// </summary>
        public IGrowthTimer GrowthTimer => _growthTimer;

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

            // Inisialisasi Timer System Modular dengan durasi stage pertama (stage 0)
            float initialDuration = GetDurationForStage(0);
            _growthTimer = new PlantGrowthTimer(initialDuration);
            _timerProgressRatio.Value = 0f;
        }

        private void Update()
        {
            // Pertumbuhan hanya diproses di Server (Server Authority)
            if (!IsServerStarted || IsMature || _growthTimer == null) return;

            // Evaluasi kondisi penyiraman tanah jika terintegrasi dengan LahanManagerTilemap
            EvaluateSoilCondition();

            // Update timer modular
            _growthTimer.Tick(Time.deltaTime);
            _timerProgressRatio.Value = _growthTimer.ProgressRatio;

            // Jika timer stage saat ini selesai, naikkan stage pertumbuhan
            if (_growthTimer.IsFinished)
            {
                AdvanceStage();
            }
        }

        /// <summary>
        /// Mengevaluasi kondisi tanah (apakah disiram) untuk menyesuaikan kecepatan atau pause timer.
        /// </summary>
        private void EvaluateSoilCondition()
        {
            if (LahanManagerTilemap.Instance == null) return;

            bool isDisiram = LahanManagerTilemap.Instance.IsPetakDisiram(transform.position);

            if (plantData != null)
            {
                if (plantData.requireWaterToGrow)
                {
                    _growthTimer.IsPaused = !isDisiram;
                }

                if (isDisiram && plantData.wateredSpeedMultiplier > 0f)
                {
                    _growthTimer.SpeedMultiplier = plantData.wateredSpeedMultiplier;
                }
                else
                {
                    _growthTimer.SpeedMultiplier = 1.0f;
                }
            }
            else
            {
                // Fallback bonus kecepatan jika tanah disiram (1.25x jika disiram)
                if (isDisiram)
                {
                    _growthTimer.SpeedMultiplier = 1.25f;
                }
                else
                {
                    _growthTimer.SpeedMultiplier = 1.0f;
                }
            }
        }

        /// <summary>
        /// Memajukan stage pertumbuhan tanaman ke tingkat berikutnya.
        /// </summary>
        private void AdvanceStage()
        {
            _growthStage.Value++;

            if (IsMature)
            {
                _growthTimer.Pause();
                OnPlantMatureEvent?.Invoke();
            }
            else
            {
                float nextDuration = GetDurationForStage(_growthStage.Value);
                _growthTimer.ResetTimer(nextDuration);
            }

            OnGrowthStageChangedEvent?.Invoke(_growthStage.Value);
        }

        /// <summary>
        /// Mendapatkan durasi (detik) untuk stage tertentu.
        /// </summary>
        public float GetDurationForStage(int stage)
        {
            if (plantData != null)
            {
                return plantData.GetDurationForStage(stage);
            }

            if (stageDurations != null && stage >= 0 && stage < stageDurations.Length && stageDurations[stage] > 0f)
            {
                return stageDurations[stage];
            }

            return timePerStage;
        }

        /// <summary>
        /// Mengatur pengganda kecepatan pertumbuhan secara kustom (misal dari pupuk/booster).
        /// </summary>
        public void SetGrowthSpeedMultiplier(float multiplier)
        {
            if (_growthTimer != null)
            {
                _growthTimer.SpeedMultiplier = multiplier;
            }
        }

        /// <summary>
        /// Menghentikan sementara / melanjutkan timer pertumbuhan.
        /// </summary>
        public void SetPaused(bool paused)
        {
            if (_growthTimer != null)
            {
                _growthTimer.IsPaused = paused;
            }
        }

        /// <summary>
        /// Callback otomatis saat SyncVar _growthStage berubah nilainya di client maupun server.
        /// </summary>
        private void OnGrowthStageChanged(int prev, int next, bool asServer)
        {
            UpdateVisuals(next);
        }

        private void UpdateVisuals(int stage)
        {
            if (spriteRenderer == null) return;

            Sprite newSprite = null;

            if (plantData != null)
            {
                newSprite = plantData.GetSpriteForStage(stage);
            }
            else if (stageSprites != null && stage >= 0 && stage < stageSprites.Length)
            {
                newSprite = stageSprites[stage];
            }

            if (newSprite != null)
            {
                spriteRenderer.sprite = newSprite;
            }
        }

        private void OnDrawGizmos()
        {
            // Visualisasi titik tanaman di Scene editor
            Gizmos.color = IsMature ? Color.yellow : Color.green;
            Gizmos.DrawWireSphere(transform.position, 0.3f);
        }
    }
}
