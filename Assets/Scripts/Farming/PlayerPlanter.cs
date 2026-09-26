using UnityEngine;
using FishNet.Object;
using Farming.Placement;

namespace Farming
{
    /// <summary>
    /// Komponen NetworkBehaviour untuk menangani aksi menanam (Planting) oleh pemain.
    /// Memanfaatkan arsitektur modular:
    /// 1. Input Legacy (KeyCode) hanya dideteksi pada Client pemilik (IsOwner).
    /// 2. Posisi target dihitung menggunakan interface IPlacementStrategy (Bebas / Grid).
    /// 3. Permintaan dikirim ke Server via [ServerRpc] untuk validasi dan spawning resmi.
    /// </summary>
    public class PlayerPlanter : NetworkBehaviour
    {
        [Header("Input Legacy")]
        [Tooltip("Tombol keyboard untuk memicu penanaman.")]
        [SerializeField] private KeyCode plantKey = KeyCode.E;

        [Header("Konfigurasi Penempatan")]
        [Tooltip("Pilih tipe penempatan: Free (jarak bebas) atau Grid (menyesuaikan petak koordinat).")]
        [SerializeField] private PlacementType placementType = PlacementType.Grid;

        [Tooltip("Jarak jangkauan titik tanam di depan pemain.")]
        [SerializeField] private float reachDistance = 1.0f;

        [Tooltip("Ukuran petak jika menggunakan PlacementType.Grid.")]
        [SerializeField] private float gridSize = 1.0f;

        [Header("Prefab & Validasi")]
        [Tooltip("Prefab tanaman yang akan di-spawn. Prefab ini WAJIB memiliki NetworkObject dan terdaftar di FishNet DefaultPrefabObjects.")]
        [SerializeField] private GameObject plantPrefab;

        [Tooltip("Layer obstacle atau tanaman lain untuk mencegah penanaman bertumpuk atau menembus tembok.")]
        [SerializeField] private LayerMask blockedLayers;

        private PlayerMovement _playerMovement;
        private IPlacementStrategy _placementStrategy;

        private void Awake()
        {
            _playerMovement = GetComponent<PlayerMovement>();
            UpdateStrategy();
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            // Memperbarui strategi saat nilai diubah di Unity Inspector
            UpdateStrategy();
        }

        private void UpdateStrategy()
        {
            _placementStrategy = PlacementStrategyFactory.GetStrategy(placementType);
        }

        private void Update()
        {
            // Hanya client pemilik karakter yang berhak membaca input lokal
            if (!IsOwner) return;

            if (Input.GetKeyDown(plantKey))
            {
                RequestPlantAction();
            }
        }

        /// <summary>
        /// Menghitung posisi target dan mengirim RPC ke server.
        /// </summary>
        private void RequestPlantAction()
        {
            Vector2 facingDir = _playerMovement != null ? _playerMovement.FacingDirection : Vector2.down;
            Vector2 targetPosition = GetTargetPosition(facingDir);

            // Kirim permintaan ke Server (Server-Authoritative)
            ServerPlant(targetPosition);
        }

        /// <summary>
        /// Menghitung koordinat posisi target di depan pemain menggunakan strategi yang dipilih.
        /// </summary>
        public Vector2 GetTargetPosition(Vector2 facingDir)
        {
            if (_placementStrategy == null) UpdateStrategy();
            return _placementStrategy.CalculatePosition(transform.position, facingDir, reachDistance, gridSize);
        }

        /// <summary>
        /// ServerRpc: Fungsi yang dipanggil oleh Client, namun dieksekusi di Server.
        /// Server memvalidasi aturan game sebelum benar-benar memunculkan objek di jaringan.
        /// </summary>
        /// <param name="targetPosition">Koordinat target tempat tanaman akan diletakkan.</param>
        [ServerRpc]
        private void ServerPlant(Vector2 targetPosition)
        {
            // 1. Validasi Prefab
            if (plantPrefab == null)
            {
                Debug.LogWarning("[PlayerPlanter] plantPrefab belum dipasang pada Inspector!");
                return;
            }

            // 2. Validasi Jarak (Anti-Cheat: cegah client mengirim koordinat di luar jangkauan pemain)
            float maxAllowedDistance = reachDistance + gridSize + 0.5f;
            if (Vector2.Distance(transform.position, targetPosition) > maxAllowedDistance)
            {
                Debug.LogWarning($"[PlayerPlanter] Permintaan tanam ditolak: Jarak terlalu jauh dari pemain ({Vector2.Distance(transform.position, targetPosition)} > {maxAllowedDistance}).");
                return;
            }

            // 3. Validasi Halangan / Tumpang Tindih (Cegah menanam di tembok atau di atas tanaman lain)
            Collider2D overlap = Physics2D.OverlapCircle(targetPosition, 0.2f, blockedLayers);
            if (overlap != null)
            {
                Debug.LogWarning($"[PlayerPlanter] Permintaan tanam ditolak: Titik {targetPosition} terhalang oleh {overlap.name}.");
                return;
            }

            // 4. Instansiasi & Network Spawn di Server
            GameObject spawnedPlant = Instantiate(plantPrefab, targetPosition, Quaternion.identity);
            
            // Spawn ke seluruh jaringan menggunakan FishNet ServerManager
            ServerManager.Spawn(spawnedPlant);
        }

        private void OnDrawGizmosSelected()
        {
            // Menampilkan gizmo preview titik tanam di editor Unity
            Vector2 facingDir = _playerMovement != null ? _playerMovement.FacingDirection : Vector2.down;
            Vector2 targetPos = GetTargetPosition(facingDir);

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(targetPos, 0.25f);
            Gizmos.DrawLine(transform.position, targetPos);
        }
    }
}
