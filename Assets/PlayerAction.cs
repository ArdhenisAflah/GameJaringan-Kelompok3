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
    private GameObject _gridHighlightObj;
    private LineRenderer _gridLineRenderer;

    private void Awake()
    {
        _playerMovement = GetComponent<PlayerMovement>();
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
                    Debug.Log("[PlayerAction] Tangan kosong! Tekan '2' untuk Cangkul, '3' untuk Alat Siram, atau tombol shortcut '\\' dan ']'.");
                    break;
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