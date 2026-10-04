using UnityEngine;
using SiloSystem;

namespace Farming
{
    /// <summary>
    /// ScriptableObject untuk konfigurasi data tanaman.
    /// Memungkinkan pembuatan beragam jenis tanaman (Padi, Jagung, Tomat, dll.)
    /// tanpa perlu menulis ulang script atau membuat banyak prefab yang identik.
    /// </summary>
    [CreateAssetMenu(fileName = "NewPlantData", menuName = "Farming/Plant Data")]
    public class PlantData : ScriptableObject
    {
        [Header("Identitas Tanaman")]
        [Tooltip("Nama jenis tanaman.")]
        public string plantName = "Padi";

        [Header("Pengaturan Pertumbuhan & Timer")]
        [Tooltip("Jumlah tahap pertumbuhan maksimal sampai tanaman siap dipanen.")]
        public int maxGrowthStage = 3;

        [Tooltip("Durasi bawaan (detik) per tahap pertumbuhan jika array stageDurations tidak diisi.")]
        public float defaultTimePerStage = 10f;

        [Tooltip("Durasi khusus (detik) untuk masing-masing stage (index 0 = stage 0->1, index 1 = stage 1->2, dst). " +
                 "Jika diisi, durasi per stage bisa berbeda-beda!")]
        public float[] stageDurations;

        [Header("Syarat & Bonus Pertumbuhan")]
        [Tooltip("Jika true, tanaman hanya akan tumbuh jika petak tanah dalam kondisi basah (disiram).")]
        public bool requireWaterToGrow = false;

        [Tooltip("Pengganda kecepatan pertumbuhan saat petak tanah disiram (contoh: 1.5 = 50% lebih cepat).")]
        public float wateredSpeedMultiplier = 1.0f;

        [Header("Visual")]
        [Tooltip("Array sprite untuk setiap stage pertumbuhan (index 0 sampai maxGrowthStage).")]
        public Sprite[] stageSprites;

        [Header("Hasil Panen")]
        [Tooltip("Referensi ke data item hasil panen yang didapat saat tanaman dipanen.")]
        public HarvestItemData harvestItem;

        /// <summary>
        /// Mendapatkan durasi pertumbuhan (dalam detik) untuk stage tertentu.
        /// Menggunakan array stageDurations jika tersedia dan valid, atau defaultTimePerStage sebagai fallback.
        /// </summary>
        /// <param name="stage">Index stage pertumbuhan (0-indexed).</param>
        /// <returns>Durasi waktu dalam detik.</returns>
        public float GetDurationForStage(int stage)
        {
            if (stageDurations != null && stage >= 0 && stage < stageDurations.Length && stageDurations[stage] > 0f)
            {
                return stageDurations[stage];
            }
            return defaultTimePerStage;
        }

        /// <summary>
        /// Mendapatkan sprite visual untuk stage pertumbuhan tertentu.
        /// </summary>
        public Sprite GetSpriteForStage(int stage)
        {
            if (stageSprites != null && stage >= 0 && stage < stageSprites.Length)
            {
                return stageSprites[stage];
            }
            return null;
        }
    }
}
