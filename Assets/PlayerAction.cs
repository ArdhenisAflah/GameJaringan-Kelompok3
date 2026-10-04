using UnityEngine;
using FishNet.Object;

public class PlayerAction : NetworkBehaviour
{
    public enum TipeAlat { TanganKosong, Cangkul, AlatSiram }
    
    [Header("Status Tangan")]
    public TipeAlat alatDiTangan = TipeAlat.TanganKosong;

    [Header("Jangkauan Aksi")]
    [Tooltip("Jarak petak di depan pemain yang ditargetkan.")]
    [SerializeField] private float reachDistance = 1.0f;

    [Header("Visual Grid Highlight")]
    [Tooltip("Menampilkan kotak highlight target di Game View.")]
    [SerializeField] private bool showGridHighlight = true;
    [SerializeField] private Color highlightColorCangkul = new Color(1f, 0.85f, 0.2f, 0.85f);
    [SerializeField] private Color highlightColorSiram = new Color(0.2f, 0.75f, 1f, 0.85f);
    [SerializeField] private Color highlightColorDefault = new Color(1f, 1f, 1f, 0.5f);

    private PlayerMovement _playerMovement;
    private SiloSystem.PlayerInventory _playerInventory;
    private GameObject _gridHighlightObj;
    private LineRenderer _gridLineRenderer;

    private void Awake()
    {
        _playerMovement = GetComponent<PlayerMovement>();
        _playerInventory = GetComponent<SiloSystem.PlayerInventory>();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        // Hanya buat in-game highlight pada client pemilik karakter
        if (IsOwner && showGridHighlight)
        {
            SetupGridHighlighter();
        }
    }

    private void OnDestroy()
    {
        if (_gridHighlightObj != null)
        {
            Destroy(_gridHighlightObj);
        }
    }

    private void SetupGridHighlighter()
    {
        if (_gridHighlightObj != null) return;

        _gridHighlightObj = new GameObject("GridTargetHighlighter");
        _gridLineRenderer = _gridHighlightObj.AddComponent<LineRenderer>();
        _gridLineRenderer.useWorldSpace = true;
        _gridLineRenderer.loop = true;
        _gridLineRenderer.positionCount = 4;
        _gridLineRenderer.startWidth = 0.04f;
        _gridLineRenderer.endWidth = 0.04f;

        // Gunakan default sprite material agar tidak pink/missing
        Shader spriteShader = Shader.Find("Sprites/Default");
        if (spriteShader != null)
        {
            _gridLineRenderer.material = new Material(spriteShader);
        }

        _gridLineRenderer.startColor = highlightColorDefault;
        _gridLineRenderer.endColor = highlightColorDefault;
    }

    private void Update()
    {
        if (!IsOwner) return;

        // Update target posisi & in-game grid highlight
        Vector3 targetPos = GetTargetWorldPosition();
        Vector3 cellCenter = GetTargetCellCenter(targetPos);
        UpdateGridHighlighter(cellCenter);

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
                LahanManagerTilemap.Instance.MintaCangkul(targetPos);
            }
        }

        // Tekan ']' (Right Bracket) untuk MENYIRAM paksa petak target
        if (Input.GetKeyDown(KeyCode.RightBracket))
        {
            if (LahanManagerTilemap.Instance != null)
            {
                Debug.Log($"[Bypass] Menyiram tanah di {cellCenter}!");
                LahanManagerTilemap.Instance.MintaSiram(targetPos);
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
            alatDiTangan = TipeAlat.Cangkul;
            Debug.Log("[PlayerAction] Alat aktif: Cangkul");
        }
        else if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            alatDiTangan = TipeAlat.AlatSiram;
            Debug.Log("[PlayerAction] Alat aktif: Alat Siram");
        }

        // ==========================================
        // 3. GUNAKAN ALAT (Tombol Space)
        // ==========================================
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (LahanManagerTilemap.Instance == null)
            {
                Debug.LogWarning("[PlayerAction] LahanManagerTilemap belum ditemukan di scene!");
                return;
            }

            switch (alatDiTangan)
            {
                case TipeAlat.Cangkul:
                    LahanManagerTilemap.Instance.MintaCangkul(targetPos);
                    break;
                case TipeAlat.AlatSiram:
                    LahanManagerTilemap.Instance.MintaSiram(targetPos);
                    break;
                case TipeAlat.TanganKosong:
                    // Coba memanen tanaman jika ada tanaman matang di petak depan pemain
                    if (TryHarvestPlantAt(targetPos))
                    {
                        break;
                    }
                    Debug.Log("[PlayerAction] Tangan kosong! Tekan '2' untuk Cangkul, '3' untuk Alat Siram, atau dekati tanaman matang untuk memanen.");
                    break;
            }
        }
    }

    private bool TryHarvestPlantAt(Vector3 targetPos)
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

    private void UpdateGridHighlighter(Vector3 center)
    {
        if (_gridLineRenderer == null || !_gridLineRenderer.enabled) return;

        // Pilih warna berdasarkan alat yang dipegang
        Color activeColor = highlightColorDefault;
        if (alatDiTangan == TipeAlat.Cangkul) activeColor = highlightColorCangkul;
        else if (alatDiTangan == TipeAlat.AlatSiram) activeColor = highlightColorSiram;

        _gridLineRenderer.startColor = activeColor;
        _gridLineRenderer.endColor = activeColor;

        // Gambar 4 sudut kotak grid (ukuran 1x1 cell)
        float halfSize = 0.48f;
        _gridLineRenderer.SetPosition(0, new Vector3(center.x - halfSize, center.y - halfSize, 0f));
        _gridLineRenderer.SetPosition(1, new Vector3(center.x + halfSize, center.y - halfSize, 0f));
        _gridLineRenderer.SetPosition(2, new Vector3(center.x + halfSize, center.y + halfSize, 0f));
        _gridLineRenderer.SetPosition(3, new Vector3(center.x - halfSize, center.y + halfSize, 0f));
    }

    private void OnDrawGizmos()
    {
        // Visualisasi kotak grid yang ditargetkan di Scene View editor
        Vector3 targetPos = GetTargetWorldPosition();
        Vector3 cellCenter = GetTargetCellCenter(targetPos);

        Color gizmoColor = highlightColorDefault;
        if (alatDiTangan == TipeAlat.Cangkul) gizmoColor = highlightColorCangkul;
        else if (alatDiTangan == TipeAlat.AlatSiram) gizmoColor = highlightColorSiram;

        Gizmos.color = gizmoColor;
        Gizmos.DrawWireCube(cellCenter, new Vector3(0.96f, 0.96f, 0f));
        Gizmos.DrawLine(transform.position, cellCenter);
    }
}