using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Komponen visual Grid / Tile Selector beranimasi spritesheet (Placement Indicator)
/// dengan kontrol targeting bergaya Stardew Valley (mengikuti kursor mouse dalam radius jangkauan pemain).
/// Mendukung animasi loop mulus, tint validasi (merah jika aksi tidak valid), serta visibilitas cerdas.
/// </summary>
public class TilePlacementIndicator : MonoBehaviour
{
    [Header("Frames Animasi Spritesheet")]
    [Tooltip("Daftar frame animasi dari Placement Area.png (8 frame).")]
    [SerializeField] private Sprite[] animationFrames;

    [Tooltip("Kecepatan putaran animasi (frame per detik, rekomendasi 8 - 10 FPS).")]
    [SerializeField] private float framesPerSecond = 9.0f;

    [Header("Warna Validasi")]
    [Tooltip("Warna asli sprite saat petak valid untuk aksi yang dipilih.")]
    [SerializeField] private Color validColor = Color.white;

    [Tooltip("Warna tint merah saat petak TIDAK valid untuk aksi yang dipilih.")]
    [SerializeField] private Color invalidColor = new Color(1f, 0.25f, 0.25f, 0.85f);

    [Header("Pengaturan Render")]
    [Tooltip("Sorting layer agar selector tampil rapi di atas tanah/lahan garapan.")]
    [SerializeField] private string sortingLayerName = "Tanaman";
    [SerializeField] private int sortingOrder = 10;
    [Tooltip("Ukuran target petak dalam world unit (default 1.0 petak).")]
    [SerializeField] private float targetCellSize = 1.0f;

    [Header("Jangkauan Target")]
    [Tooltip("Jarak maksimal jangkauan selektor di sekeliling pemain (unit).")]
    [SerializeField] private float reachDistance = 1.0f;

    private PlayerAction _playerAction;
    private PlayerMovement _playerMovement;
    private SiloSystem.PlayerInventory _playerInventory;
    private Farming.PlayerPlanter _playerPlanter;

    private SpriteRenderer _spriteRenderer;
    private float _animTimer;
    private int _currentFrameIndex;
    private Camera _cachedCamera;

    public Vector3 CurrentTargetCenter { get; private set; }
    public bool IsCurrentTargetValid { get; private set; }

    public void Initialize(PlayerAction playerAction, PlayerMovement movement, SiloSystem.PlayerInventory inventory, Farming.PlayerPlanter planter, Sprite[] customFrames = null)
    {
        _playerAction = playerAction;
        _playerMovement = movement;
        _playerInventory = inventory;
        _playerPlanter = planter;

        if (customFrames != null && customFrames.Length > 0)
        {
            animationFrames = customFrames;
        }

        SetupVisual();
    }

    private void Awake()
    {
        SetupVisual();
    }

    private void SetupVisual()
    {
        if (_spriteRenderer == null)
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
            if (_spriteRenderer == null)
            {
                _spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            }
        }

        if (!string.IsNullOrEmpty(sortingLayerName))
        {
            _spriteRenderer.sortingLayerName = sortingLayerName;
        }
        _spriteRenderer.sortingOrder = sortingOrder;

        LoadDefaultFramesIfNeeded();

        if (animationFrames != null && animationFrames.Length > 0 && animationFrames[0] != null)
        {
            _spriteRenderer.sprite = animationFrames[0];
            AdjustScaleToFitCell();
        }

        // Mulai dalam kondisi tersembunyi sampai diverifikasi di Update
        _spriteRenderer.enabled = false;
    }

    private void LoadDefaultFramesIfNeeded()
    {
        if (animationFrames != null && animationFrames.Length > 0 && animationFrames[0] != null)
        {
            return;
        }

#if UNITY_EDITOR
        string path = "Assets/SpriteSheet/Cursor/Placement Area.png";
        Sprite[] loaded = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path)
            .OfType<Sprite>()
            .OrderBy(s => s.name)
            .ToArray();

        if (loaded != null && loaded.Length > 0)
        {
            animationFrames = loaded;
        }
#endif
    }

    private void AdjustScaleToFitCell()
    {
        if (_spriteRenderer == null || _spriteRenderer.sprite == null) return;

        float spriteWidth = _spriteRenderer.sprite.rect.width / _spriteRenderer.sprite.pixelsPerUnit;
        if (spriteWidth > 0.001f)
        {
            float scale = targetCellSize / spriteWidth;
            transform.localScale = new Vector3(scale, scale, 1f);
        }
    }

    private void Update()
    {
        if (_playerAction == null) return;

        // 1. Jalankan animasi looping berputar
        UpdateAnimation();

        // 2. Hitung target petak grid (Stardew Valley style: mengarah ke mouse dalam radius pemain)
        UpdateTargetPosition();

        // 3. Evaluasi aturan visibilitas dan pewarnaan validasi
        UpdateVisibilityAndValidation();
    }

    private void UpdateAnimation()
    {
        if (animationFrames == null || animationFrames.Length == 0) return;

        _animTimer += Time.deltaTime;
        float frameDuration = 1f / Mathf.Max(1f, framesPerSecond);

        if (_animTimer >= frameDuration)
        {
            _animTimer -= frameDuration;
            _currentFrameIndex = (_currentFrameIndex + 1) % animationFrames.Length;
            if (_spriteRenderer != null && animationFrames[_currentFrameIndex] != null)
            {
                _spriteRenderer.sprite = animationFrames[_currentFrameIndex];
            }
        }
    }

    private void UpdateTargetPosition()
    {
        Vector3 playerPos = _playerAction.transform.position;
        Camera cam = GetCamera();

        Vector3 mouseWorld;
        if (cam != null)
        {
            Vector3 mouseScreen = Input.mousePosition;
            mouseWorld = cam.ScreenToWorldPoint(mouseScreen);
            mouseWorld.z = 0f;
        }
        else
        {
            Vector2 defaultFacing = _playerMovement != null ? _playerMovement.FacingDirection : Vector2.down;
            mouseWorld = playerPos + (Vector3)(defaultFacing.normalized * reachDistance);
        }

        Vector2 toMouse = (Vector2)(mouseWorld - playerPos);
        float dist = toMouse.magnitude;

        Vector3 candidatePos;
        // Jika kursor mouse dekat dengan karakter (dalam radius reachDistance + 0.5f), sorot langsung petak kursor
        if (dist <= reachDistance + 0.5f)
        {
            candidatePos = mouseWorld;
        }
        else
        {
            // Jika kursor diarahkan jauh, proyeksikan ke arah kursor sejauh reachDistance
            Vector2 dir = toMouse.sqrMagnitude > 0.001f ? toMouse.normalized : Vector2.down;
            candidatePos = playerPos + (Vector3)(dir * reachDistance);
        }

        if (LahanManagerTilemap.Instance != null)
        {
            Vector3Int cellPos = LahanManagerTilemap.Instance.WorldToCell(candidatePos);
            CurrentTargetCenter = LahanManagerTilemap.Instance.GetCellCenterWorld(cellPos);
        }
        else
        {
            float snapX = Mathf.Floor(candidatePos.x) + 0.5f;
            float snapY = Mathf.Floor(candidatePos.y) + 0.5f;
            CurrentTargetCenter = new Vector3(snapX, snapY, 0f);
        }

        transform.position = CurrentTargetCenter;
    }

    private void UpdateVisibilityAndValidation()
    {
        if (_spriteRenderer == null) return;

        // Pengaman: Jangan tampilkan jika mouse melayang di atas elemen UI
        bool isPointerOverUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        bool isHoldingCrop = _playerInventory != null && _playerInventory.IsHoldingCrop;
        bool isHoldingSeed = _playerInventory != null && _playerInventory.IsHoldingSeed;
        bool isToolActive = _playerAction.alatDiTangan == PlayerAction.TipeAlat.Cangkul ||
                            _playerAction.alatDiTangan == PlayerAction.TipeAlat.AlatSiram;
        bool isHarvestable = _playerAction.alatDiTangan == PlayerAction.TipeAlat.TanganKosong &&
                             !isHoldingCrop && !isHoldingSeed &&
                             _playerAction.HasMaturePlantAt(CurrentTargetCenter);

        // Aturan Visibilitas Cerdas
        bool shouldShow = !isPointerOverUI && !isHoldingCrop && (isToolActive || isHoldingSeed || isHarvestable);

        _spriteRenderer.enabled = shouldShow;

        if (!shouldShow) return;

        // Evaluasi Validitas Petak
        bool isValid = false;
        if (isHoldingSeed)
        {
            isValid = _playerPlanter != null && _playerPlanter.CanPlantAt(CurrentTargetCenter);
        }
        else if (_playerAction.alatDiTangan == PlayerAction.TipeAlat.Cangkul)
        {
            isValid = CanHoeAt(CurrentTargetCenter);
        }
        else if (_playerAction.alatDiTangan == PlayerAction.TipeAlat.AlatSiram)
        {
            isValid = CanWaterAt(CurrentTargetCenter);
        }
        else if (isHarvestable)
        {
            isValid = true;
        }

        IsCurrentTargetValid = isValid;
        _spriteRenderer.color = isValid ? validColor : invalidColor;
    }

    private bool CanHoeAt(Vector3 targetCenter)
    {
        if (LahanManagerTilemap.Instance == null) return true;

        // Sudah dicangkul (sudah ada TileKering) -> tidak valid untuk dicangkul lagi
        if (LahanManagerTilemap.Instance.HasTileKering(targetCenter)) return false;

        // Sudah ada tanaman di petak ini
        if (LahanManagerTilemap.Instance.IsPetakDitanam(targetCenter)) return false;

        return true;
    }

    private bool CanWaterAt(Vector3 targetCenter)
    {
        if (LahanManagerTilemap.Instance == null) return false;

        // Wajib sudah dicangkul terlebih dahulu
        if (!LahanManagerTilemap.Instance.HasTileKering(targetCenter)) return false;

        // Sudah disiram -> tidak valid untuk disiram lagi
        if (LahanManagerTilemap.Instance.IsPetakDisiram(targetCenter)) return false;

        return true;
    }

    private Camera GetCamera()
    {
        if (_cachedCamera != null && _cachedCamera.isActiveAndEnabled)
        {
            return _cachedCamera;
        }

        if (CameraSystem.PlayerCameraFollow.Instance != null)
        {
            _cachedCamera = CameraSystem.PlayerCameraFollow.Instance.GetComponent<Camera>();
            if (_cachedCamera != null) return _cachedCamera;
        }

        _cachedCamera = Camera.main;
        if (_cachedCamera == null)
        {
            _cachedCamera = FindObjectOfType<Camera>();
        }

        return _cachedCamera;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_spriteRenderer != null)
        {
            if (!string.IsNullOrEmpty(sortingLayerName))
            {
                _spriteRenderer.sortingLayerName = sortingLayerName;
            }
            _spriteRenderer.sortingOrder = sortingOrder;
            AdjustScaleToFitCell();
        }
    }
#endif
}
