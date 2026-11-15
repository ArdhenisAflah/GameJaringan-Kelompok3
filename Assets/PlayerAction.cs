using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using FishNet.Object;

public class PlayerAction : NetworkBehaviour
{
    public enum TipeAlat { TanganKosong, Cangkul, AlatSiram }
    
    [Header("Status Tangan")]
    public TipeAlat alatDiTangan = TipeAlat.TanganKosong;

    [Header("Jangkauan Aksi")]
    [Tooltip("Jarak petak di depan pemain yang ditargetkan.")]
    [SerializeField] private float reachDistance = 1.0f;

    [Header("Visual Spritesheet Selector")]
    [Tooltip("Menampilkan animasi spritesheet grid selector di Game View.")]
    [SerializeField] private bool showGridHighlight = true;
    [Tooltip("Daftar frame animasi opsional jika diisi manual via Inspector (default auto-load dari Placement Area.png).")]
    [SerializeField] private Sprite[] selectorFrames;

    [Header("Animasi & Durasi Aksi")]
    [Tooltip("Durasi animasi menyiram tanaman sebelum pemain dapat bergerak kembali (detik).")]
    [SerializeField] private float wateringDuration = 0.52f;
    [Tooltip("Durasi animasi mencangkul tanah sebelum pemain dapat bergerak kembali (detik).")]
    [SerializeField] private float diggingDuration = 0.35f;

    private PlayerMovement _playerMovement;
    private PlayerAnimation _playerAnimation;
    private SiloSystem.PlayerInventory _playerInventory;
    private Farming.PlayerPlanter _playerPlanter;

    private GameObject _tileIndicatorObj;
    private TilePlacementIndicator _tileIndicator;

    public TilePlacementIndicator TileIndicator => _tileIndicator;

    private bool _isActionLocked;
    private Coroutine _wateringCoroutine;
    private Coroutine _diggingCoroutine;

    private void Awake()
    {
        _playerMovement = GetComponent<PlayerMovement>();
        _playerAnimation = GetComponent<PlayerAnimation>();
        _playerInventory = GetComponent<SiloSystem.PlayerInventory>();
        _playerPlanter = GetComponent<Farming.PlayerPlanter>();
    }

    private void OnDisable()
    {
        if (_wateringCoroutine != null)
        {
            StopCoroutine(_wateringCoroutine);
            _wateringCoroutine = null;
        }

        if (_diggingCoroutine != null)
        {
            StopCoroutine(_diggingCoroutine);
            _diggingCoroutine = null;
        }

        if (_isActionLocked)
        {
            _isActionLocked = false;
            if (_playerMovement != null)
            {
                _playerMovement.SetMovementLocked(false);
            }
        }
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        // Hanya buat in-game highlight pada client pemilik karakter (IsOwner)
        if (IsOwner && showGridHighlight)
        {
            SetupGridHighlighter();
        }
    }

    private void OnDestroy()
    {
        if (_tileIndicatorObj != null)
        {
            Destroy(_tileIndicatorObj);
        }
    }

    private void SetupGridHighlighter()
    {
        if (_tileIndicatorObj != null) return;

        _tileIndicatorObj = new GameObject("TilePlacementIndicator");
        _tileIndicator = _tileIndicatorObj.AddComponent<TilePlacementIndicator>();
        _tileIndicator.Initialize(this, _playerMovement, _playerInventory, _playerPlanter, selectorFrames);
    }

    private void Update()
    {
        if (!IsOwner) return;

        // Update target posisi
        Vector3 targetPos = GetTargetWorldPosition();
        Vector3 cellCenter = GetTargetCellCenter(targetPos);

        // Jika pemain sedang dalam proses aksi (misal sedang menyiram/mencangkul), abaikan input baru
        if (_isActionLocked) return;

        // ==========================================
        // ATURAN BATCH 3 & SEED SYSTEM: LOCK AKSI SAAT MEMBAWA BARANG
        // ==========================================
        bool isCarryingItem = _playerInventory != null && _playerInventory.HasItem;
        if (isCarryingItem)
        {
            // Pastikan alat tidak aktif saat membawa barang
            alatDiTangan = TipeAlat.TanganKosong;

            // Jika pemain mencoba menggunakan alat, berikan peringatan dan batalkan aksi
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Backslash) || Input.GetKeyDown(KeyCode.RightBracket) ||
                Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Alpha3))
            {
                string itemTag = _playerInventory.IsHoldingSeed ? $"{_playerInventory.HeldType} [Benih]" : $"{_playerInventory.HeldType}";
                string actionHint = _playerInventory.IsHoldingSeed ? "Tanam dengan [E] pada lahan garapan terlebih dahulu." : "Setor ke Silo terlebih dahulu.";
                Debug.Log($"<color=orange>[PlayerAction]</color> Tangan sedang membawa '{itemTag}'! Tidak dapat mencangkul, menyiram, atau mengganti alat. {actionHint}");
            }
            return;
        }

        // ==========================================
        // 1. BYPASS SHORTCUT (Testing Cepat)
        // ==========================================
        // Tekan '\' (Backslash) untuk MENCANGKUL paksa petak target
        if (Input.GetKeyDown(KeyCode.Backslash))
        {
            if (LahanManagerTilemap.Instance != null)
            {
                Debug.Log($"[Bypass] Mencangkul tanah di {cellCenter}!");
                ExecuteDiggingAction(targetPos);
            }
        }

        // Tekan ']' (Right Bracket) untuk MENYIRAM paksa petak target
        if (Input.GetKeyDown(KeyCode.RightBracket))
        {
            if (LahanManagerTilemap.Instance != null)
            {
                Debug.Log($"[Bypass] Menyiram tanah di {cellCenter}!");
                ExecuteWateringAction(targetPos);
            }
        }

        // Tekan '[' (Left Bracket) untuk MENGEMBALIKAN petak tanah ke Rumput secara dinamis
        if (Input.GetKeyDown(KeyCode.LeftBracket))
        {
            if (LahanManagerTilemap.Instance != null)
            {
                ExecuteRestoreGrassAction(targetPos);
            }
        }

        // ==========================================
        // 2. GANTI ALAT CEPAT (Angka 1, 2, 3)
        // ==========================================
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            alatDiTangan = TipeAlat.TanganKosong;
            Debug.Log("[PlayerAction] Alat aktif: Tangan Kosong");
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            bool wasAlreadyCangkul = (alatDiTangan == TipeAlat.Cangkul);
            alatDiTangan = TipeAlat.Cangkul;
            Debug.Log("[PlayerAction] Alat aktif: Cangkul");
            if (wasAlreadyCangkul)
            {
                ExecuteDiggingAction(targetPos);
            }
        }
        else if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            alatDiTangan = TipeAlat.AlatSiram;
            Debug.Log("[PlayerAction] Alat aktif: Alat Siram");
        }

        // ==========================================
        // 3. GUNAKAN ALAT (Tombol Space atau Klik Kiri Mouse)
        // ==========================================
        bool isPointerOverUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        bool isLeftClick = Input.GetMouseButtonDown(0) && !isPointerOverUI;
        bool isSpaceKey = Input.GetKeyDown(KeyCode.Space);

        // Jika pemain memegang benih, penanaman (baik [E] maupun Klik Kiri) ditangani oleh PlayerPlanter
        bool isHoldingSeed = _playerInventory != null && _playerInventory.IsHoldingSeed;

        if (isSpaceKey || (isLeftClick && !isHoldingSeed))
        {
            if (LahanManagerTilemap.Instance == null)
            {
                Debug.LogWarning("[PlayerAction] LahanManagerTilemap belum ditemukan di scene!");
                return;
            }

            switch (alatDiTangan)
            {
                case TipeAlat.Cangkul:
                    ExecuteDiggingAction(targetPos);
                    break;
                case TipeAlat.AlatSiram:
                    ExecuteWateringAction(targetPos);
                    break;
                case TipeAlat.TanganKosong:
                    // Coba memanen tanaman jika ada tanaman matang di petak target
                    if (TryHarvestPlantAt(targetPos))
                    {
                        break;
                    }
                    if (isSpaceKey)
                    {
                        Debug.Log("[PlayerAction] Tangan kosong! Tekan '2' untuk Cangkul, '3' untuk Alat Siram, atau dekati tanaman matang untuk memanen.");
                    }
                    break;
            }
        }
    }

    /// <summary>
    /// Memeriksa apakah petak target dapat dicangkul (berada di rumput, bukan pohon, belum dicangkul, dan tidak ada tanaman).
    /// </summary>
    public bool CanDigAt(Vector3 targetPos)
    {
        if (LahanManagerTilemap.Instance == null) return false;

        if (LahanManagerTilemap.Instance.HasTileTree(targetPos))
        {
            Debug.Log("<color=orange>[PlayerAction]</color> Tidak dapat mencangkul: Terdapat pohon di petak target.");
            return false;
        }

        if (LahanManagerTilemap.Instance.tilemapGrass != null && !LahanManagerTilemap.Instance.HasTileRumput(targetPos))
        {
            Debug.Log("<color=orange>[PlayerAction]</color> Tidak dapat mencangkul: Petak tanah bukan area rumput.");
            return false;
        }

        // Validasi: Petak tanah belum dicangkul (belum berupa TileKering)
        if (LahanManagerTilemap.Instance.HasTileKering(targetPos))
        {
            Debug.Log("<color=orange>[PlayerAction]</color> Tidak dapat mencangkul: Petak tanah sudah dicangkul (sudah berupa TileKering).");
            return false;
        }

        // Validasi: Petak tanah tidak terdapat tanaman
        if (LahanManagerTilemap.Instance.IsPetakDitanam(targetPos))
        {
            Debug.Log("<color=orange>[PlayerAction]</color> Tidak dapat mencangkul: Petak tanah terdapat tanaman.");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Memeriksa apakah petak target dapat dikembalikan ke rumput (harus berupa tanah dan tidak ada tanaman).
    /// </summary>
    public bool CanRestoreGrassAt(Vector3 targetPos)
    {
        if (LahanManagerTilemap.Instance == null) return false;
        return LahanManagerTilemap.Instance.CanRestoreRumputAt(targetPos);
    }

    /// <summary>
    /// Mengembalikan petak tanah kosong (tanpa tanaman) kembali menjadi rumput secara dinamis.
    /// </summary>
    private void ExecuteRestoreGrassAction(Vector3 targetPos)
    {
        if (_isActionLocked) return;

        if (!CanRestoreGrassAt(targetPos))
        {
            Debug.Log("<color=orange>[PlayerAction]</color> Tidak dapat mengembalikan ke rumput: Petak bukan tanah garapan atau masih terdapat tanaman.");
            return;
        }

        LahanManagerTilemap.Instance.MintaKembalikanRumput(targetPos);
    }

    /// <summary>
    /// Menyelaraskan arah hadap karakter (flipX) ke petak target sebelum animasi aksi dimulai.
    /// </summary>
    private void AlignFacingToTarget(Vector3 targetPos)
    {
        if (_playerMovement != null)
        {
            Vector2 dir = (Vector2)(targetPos - transform.position);
            _playerMovement.SetFacingDirection(dir);
        }
    }

    /// <summary>
    /// Menjalankan aksi mencangkul tanah disertai animasi digging_animation dan penguncian pergerakan karakter.
    /// </summary>
    private void ExecuteDiggingAction(Vector3 targetPos)
    {
        if (_isActionLocked) return;

        // Validasi: Hanya picu animasi dan aksi jika tanah belum dicangkul
        if (!CanDigAt(targetPos)) return;

        // Hadapkan karakter ke arah target sebelum animasi dimulai
        AlignFacingToTarget(targetPos);

        if (_diggingCoroutine != null)
        {
            StopCoroutine(_diggingCoroutine);
        }
        _diggingCoroutine = StartCoroutine(DiggingRoutine(targetPos));
    }

    private IEnumerator DiggingRoutine(Vector3 targetPos)
    {
        _isActionLocked = true;

        // Kunci input dan pergerakan fisik karakter agar tidak bergeser saat mencangkul
        if (_playerMovement != null)
        {
            _playerMovement.SetMovementLocked(true);
        }

        // Picu animasi mencangkul (digging_animation) ke Animator lokal dan jaringan
        if (_playerAnimation != null)
        {
            _playerAnimation.TriggerDiggingAnimation();
        }

        // Minta sistem tilemap lahan untuk mencangkul petak tanah
        if (LahanManagerTilemap.Instance != null)
        {
            LahanManagerTilemap.Instance.MintaCangkul(targetPos);
        }

        // Tunggu durasi animasi mencangkul selesai
        yield return new WaitForSeconds(diggingDuration);

        // Buka kembali kunci pergerakan karakter
        if (_playerMovement != null)
        {
            _playerMovement.SetMovementLocked(false);
        }

        _isActionLocked = false;
        _diggingCoroutine = null;
    }

    /// <summary>
    /// Memeriksa apakah petak target dapat disiram (harus memiliki TileKering dan belum disiram).
    /// </summary>
    public bool CanWaterAt(Vector3 targetPos)
    {
        if (LahanManagerTilemap.Instance == null) return false;

        // Validasi: Petak tanah harus sudah dicangkul / memiliki TileKering
        if (!LahanManagerTilemap.Instance.HasTileKering(targetPos))
        {
            Debug.Log("<color=orange>[PlayerAction]</color> Tidak dapat menyiram: Petak tanah belum memiliki TileKering (belum dicangkul). Cangkul tanah terlebih dahulu.");
            return false;
        }

        // Validasi: Petak tanah belum disiram (belum basah)
        if (LahanManagerTilemap.Instance.IsPetakDisiram(targetPos))
        {
            Debug.Log("<color=orange>[PlayerAction]</color> Tidak dapat menyiram: Petak tanah sudah basah (sudah disiram).");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Menjalankan aksi menyiram tanaman disertai animasi watering_plant dan penguncian pergerakan karakter.
    /// </summary>
    private void ExecuteWateringAction(Vector3 targetPos)
    {
        if (_isActionLocked) return;

        // Validasi: Hanya picu animasi dan aksi jika tanah sudah berupa TileKering
        if (!CanWaterAt(targetPos)) return;

        // Hadapkan karakter ke arah target sebelum animasi dimulai
        AlignFacingToTarget(targetPos);

        if (_wateringCoroutine != null)
        {
            StopCoroutine(_wateringCoroutine);
        }
        _wateringCoroutine = StartCoroutine(WateringRoutine(targetPos));
    }

    private IEnumerator WateringRoutine(Vector3 targetPos)
    {
        _isActionLocked = true;

        // Kunci input dan pergerakan fisik karakter agar tidak meluncur saat menyiram
        if (_playerMovement != null)
        {
            _playerMovement.SetMovementLocked(true);
        }

        // Picu animasi menyiram (watering_plant) ke Animator lokal dan jaringan
        if (_playerAnimation != null)
        {
            _playerAnimation.TriggerWateringAnimation();
        }

        // Minta sistem tilemap lahan untuk membasahi petak
        if (LahanManagerTilemap.Instance != null)
        {
            LahanManagerTilemap.Instance.MintaSiram(targetPos);
        }

        // Tunggu durasi animasi menyiram selesai
        yield return new WaitForSeconds(wateringDuration);

        // Buka kembali kunci pergerakan karakter
        if (_playerMovement != null)
        {
            _playerMovement.SetMovementLocked(false);
        }

        _isActionLocked = false;
        _wateringCoroutine = null;
    }

    /// <summary>
    /// Memeriksa apakah ada tanaman yang telah matang pada petak target.
    /// </summary>
    public bool HasMaturePlantAt(Vector3 targetPos)
    {
        // 1. Cek via Physics2D Overlap di area petak depan
        Collider2D[] hits = Physics2D.OverlapCircleAll(targetPos, 0.45f);
        for (int i = 0; i < hits.Length; i++)
        {
            Farming.Plant plant = hits[i].GetComponentInParent<Farming.Plant>();
            if (plant != null && plant.IsMature)
            {
                return true;
            }
        }

        // 2. Fallback cek via LahanManagerTilemap jika collider tanaman belum aktif
        if (LahanManagerTilemap.Instance != null)
        {
            GameObject plantObj = LahanManagerTilemap.Instance.GetTanaman(targetPos);
            if (plantObj != null)
            {
                Farming.Plant plant = plantObj.GetComponent<Farming.Plant>();
                if (plant != null && plant.IsMature)
                {
                    return true;
                }
            }
        }

        return false;
    }

    public bool TryHarvestPlantAt(Vector3 targetPos)
    {
        // Hadapkan karakter ke arah target tanaman yang hendak dipanen
        AlignFacingToTarget(targetPos);

        // 1. Cek via Physics2D Overlap di area petak depan
        Collider2D[] hits = Physics2D.OverlapCircleAll(targetPos, 0.45f);
        for (int i = 0; i < hits.Length; i++)
        {
            Farming.Plant plant = hits[i].GetComponentInParent<Farming.Plant>();
            if (plant != null && plant.IsMature)
            {
                Debug.Log($"[PlayerAction] Memanen tanaman matang '{plant.PlantName}'...");
                ServerRequestHarvest(plant.GetComponent<NetworkObject>());
                return true;
            }
        }

        // 2. Fallback cek via LahanManagerTilemap jika collider tanaman belum aktif
        if (LahanManagerTilemap.Instance != null)
        {
            GameObject plantObj = LahanManagerTilemap.Instance.GetTanaman(targetPos);
            if (plantObj != null)
            {
                Farming.Plant plant = plantObj.GetComponent<Farming.Plant>();
                if (plant != null && plant.IsMature)
                {
                    Debug.Log($"[PlayerAction] Memanen tanaman matang '{plant.PlantName}' via LahanManager...");
                    ServerRequestHarvest(plant.GetComponent<NetworkObject>());
                    return true;
                }
            }
        }

        return false;
    }

    [ServerRpc]
    private void ServerRequestHarvest(NetworkObject plantNob)
    {
        if (plantNob == null) return;
        Farming.Plant plant = plantNob.GetComponent<Farming.Plant>();
        if (plant != null && plant.IsMature)
        {
            float dist = Vector2.Distance(transform.position, plant.transform.position);
            if (dist <= reachDistance + 1.2f)
            {
                plant.ServerHarvest(GetComponent<NetworkObject>());
            }
            else
            {
                Debug.LogWarning($"[PlayerAction] Permintaan panen ditolak: Jarak terlalu jauh ({dist:F2} unit).");
            }
        }
    }

    public Vector3 GetTargetWorldPosition()
    {
        if (_tileIndicator != null)
        {
            return _tileIndicator.CurrentTargetCenter;
        }

        Vector2 facing = (_playerMovement != null) ? _playerMovement.FacingDirection : Vector2.down;
        if (facing == Vector2.zero) facing = Vector2.down;
        return transform.position + (Vector3)(facing.normalized * reachDistance);
    }

    public Vector3 GetTargetCellCenter(Vector3 targetPos)
    {
        if (LahanManagerTilemap.Instance != null)
        {
            Vector3Int cellPos = LahanManagerTilemap.Instance.WorldToCell(targetPos);
            return LahanManagerTilemap.Instance.GetCellCenterWorld(cellPos);
        }

        // Fallback jika manager belum ada
        float snapX = Mathf.Floor(targetPos.x) + 0.5f;
        float snapY = Mathf.Floor(targetPos.y) + 0.5f;
        return new Vector3(snapX, snapY, 0f);
    }

    private void OnDrawGizmos()
    {
        // Visualisasi kotak grid yang ditargetkan di Scene View editor
        Vector3 targetPos = GetTargetWorldPosition();
        Vector3 cellCenter = GetTargetCellCenter(targetPos);

        Color gizmoColor = Color.white;
        if (alatDiTangan == TipeAlat.Cangkul) gizmoColor = Color.yellow;
        else if (alatDiTangan == TipeAlat.AlatSiram) gizmoColor = Color.cyan;

        Gizmos.color = gizmoColor;
        Gizmos.DrawWireCube(cellCenter, new Vector3(0.96f, 0.96f, 0f));
        Gizmos.DrawLine(transform.position, cellCenter);
    }
}