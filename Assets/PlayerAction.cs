using UnityEngine;
using UnityEngine.EventSystems;
using FishNet.Object;

public class PlayerAction : NetworkBehaviour
{
    public enum TipeAlat { TanganKosong, Cangkul, AlatSiram }
    
    [Header("Status Tangan")]
    public TipeAlat alatDiTangan = TipeAlat.TanganKosong;

    [Header("Jangkauan Aksi")]
    [Tooltip("Jarak petak jangkauan aksi pemain.")]
    [SerializeField] private float reachDistance = 1.0f;

    [Header("Visual Spritesheet Selector")]
    [Tooltip("Menampilkan animasi spritesheet grid selector di Game View.")]
    [SerializeField] private bool showGridHighlight = true;
    [Tooltip("Daftar frame animasi opsional jika diisi manual via Inspector (default auto-load dari Placement Area.png).")]
    [SerializeField] private Sprite[] selectorFrames;

    private PlayerMovement _playerMovement;
    private SiloSystem.PlayerInventory _playerInventory;
    private Farming.PlayerPlanter _playerPlanter;

    private GameObject _tileIndicatorObj;
    private TilePlacementIndicator _tileIndicator;

    public TilePlacementIndicator TileIndicator => _tileIndicator;

    private void Awake()
    {
        _playerMovement = GetComponent<PlayerMovement>();
        _playerInventory = GetComponent<SiloSystem.PlayerInventory>();
        _playerPlanter = GetComponent<Farming.PlayerPlanter>();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        // Hanya inisialisasi indikator pada client lokal pemilik karakter (IsOwner)
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

        _tileIndicatorObj = new GameObject("GridTileSelector");
        _tileIndicator = _tileIndicatorObj.AddComponent<TilePlacementIndicator>();
        _tileIndicator.Initialize(this, _playerMovement, _playerInventory, _playerPlanter, selectorFrames);
    }

    private void Update()
    {
        if (!IsOwner) return;

        Vector3 targetCenter = GetTargetCellCenter(GetTargetWorldPosition());

        // ==========================================
        // ATURAN PEMBAWAAN BARANG (Crop vs Seed)
        // ==========================================
        bool isCarryingItem = _playerInventory != null && _playerInventory.HasItem;
        if (isCarryingItem)
        {
            alatDiTangan = TipeAlat.TanganKosong;
        }

        // ==========================================
        // 1. GANTI ALAT CEPAT (Angka 1, 2, 3)
        // ==========================================
        if (!isCarryingItem)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                alatDiTangan = TipeAlat.TanganKosong;
                Debug.Log("[PlayerAction] Alat aktif: Tangan Kosong");
            }
            else if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                alatDiTangan = TipeAlat.Cangkul;
                Debug.Log("[PlayerAction] Alat aktif: Cangkul");
            }
            else if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                alatDiTangan = TipeAlat.AlatSiram;
                Debug.Log("[PlayerAction] Alat aktif: Alat Siram");
            }
        }

        // ==========================================
        // 2. BYPASS SHORTCUT (Testing Cepat)
        // ==========================================
        if (Input.GetKeyDown(KeyCode.Backslash))
        {
            if (LahanManagerTilemap.Instance != null)
            {
                Debug.Log($"[Bypass] Mencangkul tanah di {targetCenter}!");
                LahanManagerTilemap.Instance.MintaCangkul(targetCenter);
            }
        }

        if (Input.GetKeyDown(KeyCode.RightBracket))
        {
            if (LahanManagerTilemap.Instance != null)
            {
                Debug.Log($"[Bypass] Menyiram tanah di {targetCenter}!");
                LahanManagerTilemap.Instance.MintaSiram(targetCenter);
            }
        }

        // ==========================================
        // 3. EKSEKUSI AKSI HYBRID (Klik Kiri Mouse & Tombol Space)
        // ==========================================
        bool mouseClicked = Input.GetMouseButtonDown(0);
        bool spacePressed = Input.GetKeyDown(KeyCode.Space);

        // Jika mouse diklik namun kursor sedang berada di atas elemen UI, batalkan eksekusi aksi
        if (mouseClicked && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        if (mouseClicked || spacePressed)
        {
            ExecuteActionAt(targetCenter);
        }
    }

    private void ExecuteActionAt(Vector3 targetCenter)
    {
        // 1. Jika membawa hasil panen (Crop), tolak aksi mencangkul/menyiram/menanam
        if (_playerInventory != null && _playerInventory.IsHoldingCrop)
        {
            Debug.Log($"<color=orange>[PlayerAction]</color> Tangan sedang membawa hasil panen '{_playerInventory.HeldType}'! Tidak dapat mencangkul, menyiram, atau menanam. Setor ke Silo terlebih dahulu.");
            return;
        }

        // 2. Jika membawa benih (Seed), eksekusi penanaman ke petak
        if (_playerInventory != null && _playerInventory.IsHoldingSeed)
        {
            if (_playerPlanter != null)
            {
                _playerPlanter.TryPlantAt(targetCenter);
            }
            return;
        }

        // 3. Putar arah hadap pemain menghadap petak target
        if (_playerMovement != null)
        {
            Vector2 aimDir = (Vector2)(targetCenter - transform.position);
            _playerMovement.SetFacingDirection(aimDir);
        }

        if (LahanManagerTilemap.Instance == null)
        {
            Debug.LogWarning("[PlayerAction] LahanManagerTilemap belum ditemukan di scene!");
            return;
        }

        // 4. Eksekusi alat aktif
        switch (alatDiTangan)
        {
            case TipeAlat.Cangkul:
                LahanManagerTilemap.Instance.MintaCangkul(targetCenter);
                break;

            case TipeAlat.AlatSiram:
                LahanManagerTilemap.Instance.MintaSiram(targetCenter);
                break;

            case TipeAlat.TanganKosong:
                if (TryHarvestPlantAt(targetCenter))
                {
                    break;
                }
                Debug.Log("[PlayerAction] Tangan kosong! Tekan '2' untuk Cangkul, '3' untuk Alat Siram, atau arahkan ke tanaman matang untuk memanen.");
                break;
        }
    }

    public bool HasMaturePlantAt(Vector3 targetPos)
    {
        // 1. Cek via Physics2D Overlap di area petak target
        Collider2D[] hits = Physics2D.OverlapCircleAll(targetPos, 0.45f);
        for (int i = 0; i < hits.Length; i++)
        {
            Farming.Plant plant = hits[i].GetComponentInParent<Farming.Plant>();
            if (plant != null && plant.IsMature)
            {
                return true;
            }
        }

        // 2. Fallback cek via LahanManagerTilemap jika collider belum terdeteksi
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
        Vector3 cellCenter = GetTargetCellCenter(GetTargetWorldPosition());

        Color gizmoColor = Color.white;
        if (alatDiTangan == TipeAlat.Cangkul) gizmoColor = Color.yellow;
        else if (alatDiTangan == TipeAlat.AlatSiram) gizmoColor = Color.cyan;

        Gizmos.color = gizmoColor;
        Gizmos.DrawWireCube(cellCenter, new Vector3(0.96f, 0.96f, 0f));
        Gizmos.DrawLine(transform.position, cellCenter);
    }
}