using System;
using UnityEngine;
using FishNet.Object;
using FishNet.Connection;
using SiloSystem;

namespace Farming
{
    /// <summary>
    /// Komponen NetworkBehaviour untuk objek Source Seed di dunia.
    /// Bertanggung jawab menyimpan dan meng-generate satu jenis benih tanaman spesifik.
    /// Pemain yang berada di dekat objek dapat menekan tombol 'F' untuk mengambil benih ke atas kepala.
    /// </summary>
    public class SeedSource : NetworkBehaviour
    {
        [Header("Konfigurasi Benih")]
        [Tooltip("Jenis benih yang di-generate oleh sumber ini.")]
        [SerializeField] private HarvestType seedType = HarvestType.Padi;

        [Tooltip("Nama tampilan benih untuk visual prompt.")]
        [SerializeField] private string seedName = "Benih Padi";

        [Tooltip("Jika true, sumber benih tidak akan pernah habis.")]
        [SerializeField] private bool infiniteSupply = true;

        [Tooltip("Jumlah stok benih yang tersedia jika pasokan terbatas.")]
        [SerializeField] private int currentStock = 50;

        [Header("Interaksi Jarak")]
        [Tooltip("Jarak maksimal interaksi pemain dengan sumber benih.")]
        [SerializeField] private float interactionRadius = 2.0f;

        [Tooltip("Tombol interaksi untuk mengambil benih.")]
        [SerializeField] private KeyCode pickKey = KeyCode.F;

        [Header("Visual Prompt")]
        [Tooltip("Tampilkan prompt teks mengambang saat pemain mendekat.")]
        [SerializeField] private bool showPrompt = true;

        private GameObject _promptObj;
        private TMPro.TextMeshPro _promptText;
        private bool _isLocalPlayerNear;

        public HarvestType SeedType => seedType;
        public string SeedName => seedName;
        public float InteractionRadius => interactionRadius;
        public KeyCode PickKey => pickKey;

        public event Action<bool> OnPlayerProximityChanged;

        private void Awake()
        {
            SetupPromptVisual();
        }

        private void Update()
        {
            CheckLocalPlayerProximity();
        }

        private void SetupPromptVisual()
        {
            if (!showPrompt) return;

            Transform existing = transform.Find("SeedPromptAnchor");
            if (existing != null)
            {
                _promptObj = existing.gameObject;
            }
            else
            {
                _promptObj = new GameObject("SeedPromptAnchor");
                _promptObj.transform.SetParent(transform, false);
                _promptObj.transform.localPosition = new Vector3(0f, 1.1f, 0f);
            }

            _promptText = _promptObj.GetComponent<TMPro.TextMeshPro>();
            if (_promptText == null)
            {
                _promptText = _promptObj.AddComponent<TMPro.TextMeshPro>();
            }

            _promptText.fontSize = 2.4f;
            _promptText.alignment = TMPro.TextAlignmentOptions.Center;
            _promptText.color = new Color(1f, 0.95f, 0.4f, 1f);
            _promptText.sortingOrder = 30;
            _promptText.text = $"[{pickKey}] Ambil {seedName}";

            _promptObj.SetActive(false);
        }

        private void CheckLocalPlayerProximity()
        {
            // Cari karakter pemain lokal
            GameObject localPlayer = null;
            var players = FindObjectsOfType<PlayerInventory>();
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i].IsOwner)
                {
                    localPlayer = players[i].gameObject;
                    break;
                }
            }

            if (localPlayer == null)
            {
                if (_isLocalPlayerNear) SetPromptActive(false);
                return;
            }

            float dist = Vector2.Distance(transform.position, localPlayer.transform.position);
            bool isNear = dist <= interactionRadius;

            if (isNear != _isLocalPlayerNear)
            {
                _isLocalPlayerNear = isNear;
                SetPromptActive(_isLocalPlayerNear);
                OnPlayerProximityChanged?.Invoke(_isLocalPlayerNear);
            }
        }

        private void SetPromptActive(bool active)
        {
            if (_promptObj != null)
            {
                _promptObj.SetActive(active);
            }
        }

        /// <summary>
        /// Permintaan pengambilan benih oleh pemain ke Server (Server-Authoritative).
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void ServerRequestSeed(NetworkObject playerNob)
        {
            if (!IsServerStarted) return;
            if (playerNob == null) return;

            // 1. Validasi jarak pemain ke sumber benih
            float dist = Vector2.Distance(transform.position, playerNob.transform.position);
            if (dist > interactionRadius + 1.2f)
            {
                Debug.LogWarning($"<color=orange>[SeedSource]</color> Permintaan benih ditolak: Pemain terlalu jauh ({dist:F2}m > {interactionRadius}m).");
                return;
            }

            // 2. Validasi stok benih
            if (!infiniteSupply && currentStock <= 0)
            {
                Debug.LogWarning($"<color=orange>[SeedSource]</color> Stok '{seedName}' telah habis!");
                TargetRpcNotify(playerNob.Owner, false, $"Stok {seedName} telah habis!");
                return;
            }

            // 3. Validasi inventori pemain
            PlayerInventory inv = playerNob.GetComponent<PlayerInventory>();
            if (inv == null) return;

            // Aturan: Satu pemain hanya membawa 1 jenis item dalam satu waktu
            if (inv.HasItem)
            {
                if (inv.IsHoldingCrop)
                {
                    Debug.Log($"<color=orange>[SeedSource]</color> Pemain {playerNob.OwnerId} sedang membawa hasil panen '{inv.HeldType}'. Setor ke Silo terlebih dahulu.");
                    TargetRpcNotify(playerNob.Owner, false, $"Tangan sedang membawa hasil panen {inv.HeldType}! Setor ke Silo terlebih dahulu.");
                    return;
                }

                if (inv.IsHoldingSeed)
                {
                    if (inv.HeldType != seedType)
                    {
                        Debug.Log($"<color=orange>[SeedSource]</color> Pemain {playerNob.OwnerId} sudah membawa benih lain '{inv.HeldType}'.");
                        TargetRpcNotify(playerNob.Owner, false, $"Tangan sudah membawa benih {inv.HeldType}! Tanam terlebih dahulu.");
                        return;
                    }
                    else
                    {
                        // Sudah membawa benih sejenis, tolak atau pertahankan 1 buah
                        Debug.Log($"<color=orange>[SeedSource]</color> Pemain {playerNob.OwnerId} sudah membawa {seedName}.");
                        TargetRpcNotify(playerNob.Owner, false, $"Tangan sudah memegang {seedName}. Siap ditanam!");
                        return;
                    }
                }
            }

            // 4. Berikan benih ke tangan pemain (Kategori: Seed)
            bool success = inv.ServerSetHeldItem(seedType, 1, ItemCategory.Seed);
            if (success)
            {
                if (!infiniteSupply)
                {
                    currentStock--;
                }

                Debug.Log($"<color=green>[SeedSource]</color> Berhasil memberikan {seedName} x1 ke Pemain {playerNob.OwnerId}.");
                TargetRpcNotify(playerNob.Owner, true, $"Berhasil mengambil 1 {seedName}! Tekan [E] pada lahan garapan untuk menanam.");
            }
        }

        [TargetRpc]
        private void TargetRpcNotify(NetworkConnection target, bool success, string message)
        {
            if (success)
            {
                Debug.Log($"<color=green>[SeedSource]</color> {message}");
            }
            else
            {
                Debug.LogWarning($"<color=orange>[SeedSource]</color> {message}");
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, interactionRadius);
        }

        // ==========================================
        // RUNTIME AUTO-GENERATION & FACTORY
        // ==========================================
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreateSeedSourceInScene()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;

            CheckAndSpawnSource(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            CheckAndSpawnSource(scene);
        }

        private static void CheckAndSpawnSource(UnityEngine.SceneManagement.Scene scene)
        {
            if (scene.name != "SampleScene") return;

            if (FindObjectOfType<SeedSource>() == null)
            {
                Debug.Log("<color=green>[SeedSource]</color> Auto-generating Source Seed Padi in SampleScene at (-2, 2)...");
                CreateSeedSource(new Vector3(-2.0f, 2.0f, 0f), HarvestType.Padi, "Benih Padi");
            }
        }

        public static GameObject CreateSeedSource(Vector3 worldPos, HarvestType type, string name)
        {
            GameObject obj = new GameObject($"SeedSource_{type}");
            obj.transform.position = worldPos;

            // Visual Sprite
            SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
            sr.sprite = GetDefaultBoxSprite();
            sr.sortingOrder = 10;

            // 2D Physics Collider
            BoxCollider2D col = obj.AddComponent<BoxCollider2D>();
            col.size = new Vector2(1.2f, 1.2f);
            col.isTrigger = false;

            // Seed Source Component
            SeedSource source = obj.AddComponent<SeedSource>();
            source.seedType = type;
            source.seedName = name;
            source.infiniteSupply = true;
            source.interactionRadius = 2.2f;

            // Permanent Tag Label: "Kotak Benih Padi"
            GameObject tagObj = new GameObject("NameTag");
            tagObj.transform.SetParent(obj.transform, false);
            tagObj.transform.localPosition = new Vector3(0f, 0.75f, 0f);
            var tagText = tagObj.AddComponent<TMPro.TextMeshPro>();
            tagText.text = $"Kotak {name}";
            tagText.fontSize = 2.2f;
            tagText.alignment = TMPro.TextAlignmentOptions.Center;
            tagText.color = new Color(0.85f, 1f, 0.75f, 1f);
            tagText.sortingOrder = 28;

            return obj;
        }

        private static Sprite _cachedBoxSprite;
        public static Sprite GetDefaultBoxSprite()
        {
            if (_cachedBoxSprite == null)
            {
                Texture2D tex = new Texture2D(24, 24);
                Color border = new Color(0.35f, 0.22f, 0.1f, 1f);
                Color wood = new Color(0.65f, 0.45f, 0.25f, 1f);
                Color seed = new Color(0.4f, 0.85f, 0.25f, 1f);

                for (int y = 0; y < 24; y++)
                {
                    for (int x = 0; x < 24; x++)
                    {
                        if (x == 0 || x == 23 || y == 0 || y == 23)
                            tex.SetPixel(x, y, border);
                        else if (x >= 8 && x <= 15 && y >= 8 && y <= 15)
                            tex.SetPixel(x, y, seed);
                        else
                            tex.SetPixel(x, y, wood);
                    }
                }
                tex.filterMode = FilterMode.Point;
                tex.Apply();
                _cachedBoxSprite = Sprite.Create(tex, new Rect(0, 0, 24, 24), new Vector2(0.5f, 0.5f), 16);
            }
            return _cachedBoxSprite;
        }
    }
}
