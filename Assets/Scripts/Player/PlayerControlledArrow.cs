using UnityEngine;
using FishNet.Connection;
using FishNet.Object;
using SiloSystem;

namespace PlayerVisual
{
    /// <summary>
    /// Komponen modular pada prefab Player untuk menampilkan indikator visual panah (Arrow)
    /// di atas kepala karakter yang sedang dikontrol oleh client lokal (IsOwner).
    /// Karakter milik pemain lain di layar akan otomatis disembunyikan panahnya.
    /// Mendukung animasi mengambang (bobbing) dan penyesuaian ketinggian otomatis
    /// saat pemain menggendong barang (hasil panen/benih).
    /// </summary>
    public class PlayerControlledArrow : NetworkBehaviour
    {
        [Header("Sprite Panah")]
        [Tooltip("Sprite panah indikator (Player Controlled Arrow).")]
        [SerializeField] private Sprite arrowSprite;

        [Header("Posisi & Skala")]
        [Tooltip("Offset posisi panah di atas kepala karakter saat tidak membawa barang.")]
        [SerializeField] private Vector3 normalHeadOffset = new Vector3(0f, 1.15f, 0f);

        [Tooltip("Offset posisi panah di atas kepala karakter saat membawa barang panen/benih.")]
        [SerializeField] private Vector3 heldItemHeadOffset = new Vector3(0f, 1.55f, 0f);

        [Tooltip("Skala ukuran panah agar proporsional dengan kepala karakter.")]
        [SerializeField] private Vector3 arrowScale = new Vector3(0.5f, 0.5f, 1f);

        [Tooltip("Kecepatan transisi perpindahan ketinggian panah saat mengambil/meletakkan barang.")]
        [SerializeField] private float heightLerpSpeed = 10f;

        [Header("Sorting Layer")]
        [Tooltip("Sorting layer agar panah dirender di atas kepala karakter dan barang bawaan.")]
        [SerializeField] private string sortingLayerName = "Karakter";

        [Tooltip("Sorting order (default 30: di atas player order 10 dan barang panen order 25).")]
        [SerializeField] private int sortingOrder = 30;

        [Header("Animasi Mengambang (Floating Bobbing)")]
        [SerializeField] private bool enableBobbing = true;
        [SerializeField] private float bobAmplitude = 0.05f;
        [SerializeField] private float bobFrequency = 4f;

        [Header("Pengujian Offline / Editor")]
        [Tooltip("Jika true, panah tetap ditampilkan saat pengujian langsung di editor tanpa server jaringan.")]
        [SerializeField] private bool testShowInEditor = true;

        [Header("Referensi Opsional")]
        [SerializeField] private PlayerInventory playerInventory;

        private GameObject _arrowObj;
        private SpriteRenderer _spriteRenderer;
        private float _currentBaseY;

        private void Awake()
        {
            if (playerInventory == null)
            {
                playerInventory = GetComponent<PlayerInventory>();
            }

            _currentBaseY = normalHeadOffset.y;
            SetupVisualArrow();
            LoadDefaultSpriteIfNeeded();
        }

        private void Start()
        {
            UpdateVisibility();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            UpdateVisibility();
        }

        public override void OnOwnershipClient(NetworkConnection prevOwner)
        {
            base.OnOwnershipClient(prevOwner);
            UpdateVisibility();
        }

        private void Update()
        {
            if (_arrowObj == null || !_arrowObj.activeSelf) return;

            // 1. Tentukan target ketinggian (beradaptasi jika pemain membawa barang)
            bool isHoldingItem = playerInventory != null && playerInventory.HasItem;
            float targetY = isHoldingItem ? heldItemHeadOffset.y : normalHeadOffset.y;

            _currentBaseY = Mathf.Lerp(_currentBaseY, targetY, Time.deltaTime * heightLerpSpeed);

            // 2. Terapkan animasi mengambang (bobbing) naik-turun halus
            float bobOffset = 0f;
            if (enableBobbing)
            {
                bobOffset = Mathf.Sin(Time.time * bobFrequency) * bobAmplitude;
            }

            _arrowObj.transform.localPosition = new Vector3(normalHeadOffset.x, _currentBaseY + bobOffset, normalHeadOffset.z);
        }

        /// <summary>
        /// Mengatur visibilitas panah: hanya aktif pada karakter milik client lokal (IsOwner).
        /// </summary>
        public void UpdateVisibility()
        {
            bool isControlled = IsOwner;

            // Fallback saat diuji di Unity Editor tanpa spawn FishNet
            if (Application.isEditor && (NetworkObject == null || !NetworkObject.IsSpawned))
            {
                isControlled = testShowInEditor;
            }

            if (_arrowObj != null)
            {
                _arrowObj.SetActive(isControlled);
            }
        }

        private void SetupVisualArrow()
        {
            if (_arrowObj == null)
            {
                Transform existing = transform.Find("ControlledArrowAnchor");
                if (existing != null)
                {
                    _arrowObj = existing.gameObject;
                }
                else
                {
                    _arrowObj = new GameObject("ControlledArrowAnchor");
                    _arrowObj.transform.SetParent(transform, false);
                }
            }

            _arrowObj.transform.localPosition = normalHeadOffset;
            _arrowObj.transform.localRotation = Quaternion.identity;
            _arrowObj.transform.localScale = arrowScale;

            _spriteRenderer = _arrowObj.GetComponent<SpriteRenderer>();
            if (_spriteRenderer == null)
            {
                _spriteRenderer = _arrowObj.AddComponent<SpriteRenderer>();
            }

            if (!string.IsNullOrEmpty(sortingLayerName))
            {
                _spriteRenderer.sortingLayerName = sortingLayerName;
            }
            _spriteRenderer.sortingOrder = sortingOrder;

            if (arrowSprite != null)
            {
                _spriteRenderer.sprite = arrowSprite;
            }

            // Sembunyikan default sampai kepemilikan jaringan diverifikasi
            _arrowObj.SetActive(false);
        }

        private void LoadDefaultSpriteIfNeeded()
        {
            if (arrowSprite != null)
            {
                if (_spriteRenderer != null && _spriteRenderer.sprite == null)
                {
                    _spriteRenderer.sprite = arrowSprite;
                }
                return;
            }

#if UNITY_EDITOR
            string path = "Assets/SpriteSheet/Player Controlled Arrow.png";
            arrowSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (_spriteRenderer != null && arrowSprite != null)
            {
                _spriteRenderer.sprite = arrowSprite;
            }
#endif
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();

            if (_spriteRenderer != null)
            {
                if (!string.IsNullOrEmpty(sortingLayerName))
                {
                    _spriteRenderer.sortingLayerName = sortingLayerName;
                }
                _spriteRenderer.sortingOrder = sortingOrder;

                if (arrowSprite != null)
                {
                    _spriteRenderer.sprite = arrowSprite;
                }
            }

            if (_arrowObj != null)
            {
                _arrowObj.transform.localScale = arrowScale;
            }
        }
#endif
    }
}
