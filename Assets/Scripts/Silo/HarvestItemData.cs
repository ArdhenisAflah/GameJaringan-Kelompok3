using UnityEngine;

namespace SiloSystem
{
    /// <summary>
    /// ScriptableObject untuk mendefinisikan informasi visual & konfigurasi setiap jenis hasil panen.
    /// Memungkinkan penambahan tanaman/panen baru secara modular tanpa mengubah script.
    /// </summary>
    [CreateAssetMenu(fileName = "NewHarvestItem", menuName = "Farming/Harvest Item Data")]
    public class HarvestItemData : ScriptableObject
    {
        [Header("Identitas Item")]
        [Tooltip("Jenis hasil panen yang diwakili.")]
        public HarvestType harvestType = HarvestType.Padi;

        [Tooltip("Nama tampilan untuk UI.")]
        public string displayName = "Padi";

        [Tooltip("Deskripsi singkat item hasil panen.")]
        [TextArea(2, 4)]
        public string description = "Padi segar hasil panen lahan sawah.";

        [Header("Visual & Nilai")]
        [Tooltip("Ikon sprite untuk UI inventori / silo.")]
        public Sprite icon;

        [Tooltip("Kapasitas maksimal per slot inventori (jika ada batasan slot).")]
        public int maxStack = 999;

        [Tooltip("Harga jual dasar ke pasar / toko.")]
        public int baseSellPrice = 10;
    }
}
