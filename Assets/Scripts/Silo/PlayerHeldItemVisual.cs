using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace SiloSystem
{
    /// <summary>
    /// Komponen modular pada Player untuk menampilkan visual hasil panen di atas kepala karakter.
    /// Objek visual menjadi child GameObject dari Player sehingga posisinya relatif terhadap pemain saat bergerak.
    /// Bekerja secara reaktif mendengarkan PlayerInventory.OnHeldItemChanged (dan SyncVar heldItem).
    /// </summary>
    public class PlayerHeldItemVisual : MonoBehaviour
    {
        [Header("Pengaturan Posisi Anchor")]
        [Tooltip("Offset relatif objek hasil panen di atas kepala karakter.")]
        [SerializeField] private Vector3 headOffset = new Vector3(0f, 0.9f, 0f);

        [Header("Animasi Mengambang (Hover)")]
        [SerializeField] private bool enableHover = true;
        [SerializeField] private float hoverAmplitude = 0.05f;
        [SerializeField] private float hoverFrequency = 4f;

        [Header("Referensi Komponen")]
        [SerializeField] private PlayerInventory playerInventory;
        [SerializeField] private List<HarvestItemData> itemDatabase = new();

        private GameObject _anchorObj;
        private SpriteRenderer _itemSpriteRenderer;
        private TextMeshPro _quantityText;
        private Vector3 _initialLocalPos;
        private HarvestType _currentType = HarvestType.None;

        private void Awake()
        {
            if (playerInventory == null)
            {
                playerInventory = GetComponent<PlayerInventory>();
            }

            SetupVisualAnchor();
            LoadItemDatabaseIfNeeded();
        }

        private void Start()
        {
            if (playerInventory != null)
            {
                playerInventory.OnHeldItemChanged += HandleHeldItemChanged;
                // Inisialisasi tampilan awal sesuai status heldItem saat ini
                HandleHeldItemChanged(playerInventory.heldItem.Value);
            }
        }

        private void OnDestroy()
        {
            if (playerInventory != null)
            {
                playerInventory.OnHeldItemChanged -= HandleHeldItemChanged;
            }
        }

        private void Update()
        {
            // Efek melayang halus (hover bobbing) jika ada barang yang sedang dipegang
            if (enableHover && _anchorObj != null && _anchorObj.activeSelf)
            {
                float hoverY = Mathf.Sin(Time.time * hoverFrequency) * hoverAmplitude;
                _anchorObj.transform.localPosition = _initialLocalPos + new Vector3(0f, hoverY, 0f);
            }
        }

        private void SetupVisualAnchor()
        {
            if (_anchorObj == null)
            {
                Transform existing = transform.Find("HeldItemAnchor");
                if (existing != null)
                {
                    _anchorObj = existing.gameObject;
                }
                else
                {
                    _anchorObj = new GameObject("HeldItemAnchor");
                    _anchorObj.transform.SetParent(transform, false);
                }
            }

            _initialLocalPos = headOffset;
            _anchorObj.transform.localPosition = _initialLocalPos;
            _anchorObj.transform.localRotation = Quaternion.identity;
            _anchorObj.transform.localScale = new Vector3(0.85f, 0.85f, 1f);

            // Sprite Renderer
            _itemSpriteRenderer = _anchorObj.GetComponent<SpriteRenderer>();
            if (_itemSpriteRenderer == null)
            {
                _itemSpriteRenderer = _anchorObj.AddComponent<SpriteRenderer>();
            }
            _itemSpriteRenderer.sortingOrder = 25; // Di atas kepala player

            // TextMeshPro Quantity Tag (Kecil di samping ikon)
            Transform textChild = _anchorObj.transform.Find("QuantityTag");
            GameObject textObj;
            if (textChild != null)
            {
                textObj = textChild.gameObject;
            }
            else
            {
                textObj = new GameObject("QuantityTag");
                textObj.transform.SetParent(_anchorObj.transform, false);
            }

            textObj.transform.localPosition = new Vector3(0.35f, -0.1f, 0f);
            _quantityText = textObj.GetComponent<TextMeshPro>();
            if (_quantityText == null)
            {
                _quantityText = textObj.AddComponent<TextMeshPro>();
            }
            _quantityText.fontSize = 2.5f;
            _quantityText.alignment = TextAlignmentOptions.Left;
            _quantityText.color = new Color(1f, 0.95f, 0.4f, 1f);
            _quantityText.sortingOrder = 26;

            _anchorObj.SetActive(false);
        }

        private void HandleHeldItemChanged(HarvestSlot slot)
        {
            if (slot.itemType == HarvestType.None || slot.quantity <= 0)
            {
                ClearVisual();
            }
            else
            {
                DisplayItem(slot.itemType, slot.quantity);
            }
        }

        public void DisplayItem(HarvestType type, int quantity)
        {
            if (_anchorObj == null) SetupVisualAnchor();

            _currentType = type;
            _anchorObj.SetActive(true);

            // Tentukan Sprite dari item database atau fallback visual
            Sprite cropSprite = GetSpriteForType(type);
            if (_itemSpriteRenderer != null)
            {
                _itemSpriteRenderer.sprite = cropSprite;

                // Jika sprite belum memiliki tekstur spesifik, berikan warna penanda sesuai jenis hasil panen
                if (cropSprite == null)
                {
                    _itemSpriteRenderer.sprite = GetDefaultBoxSprite();
                    _itemSpriteRenderer.color = GetColorForType(type);
                }
                else
                {
                    _itemSpriteRenderer.color = Color.white;
                }
            }

            if (_quantityText != null)
            {
                _quantityText.text = quantity > 1 ? $"x{quantity}" : "";
            }
        }

        public void ClearVisual()
        {
            _currentType = HarvestType.None;
            if (_anchorObj != null)
            {
                _anchorObj.SetActive(false);
            }
        }

        private Sprite GetSpriteForType(HarvestType type)
        {
            for (int i = 0; i < itemDatabase.Count; i++)
            {
                if (itemDatabase[i] != null && itemDatabase[i].harvestType == type && itemDatabase[i].icon != null)
                {
                    return itemDatabase[i].icon;
                }
            }

#if UNITY_EDITOR
            // Fallback Editor: ambil sprite matang dari SpriteSheet/Padi_animated-Sheet jika Padi
            if (type == HarvestType.Padi)
            {
                string path = "Assets/SpriteSheet/Padi_animated-Sheet.png";
                var assets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path);
                for (int i = 0; i < assets.Length; i++)
                {
                    if (assets[i] is Sprite s && (s.name.Contains("3") || s.name.Contains("2")))
                    {
                        return s;
                    }
                }
            }
#endif

            return null;
        }

        private static Sprite _cachedDefaultSprite;
        private Sprite GetDefaultBoxSprite()
        {
            if (_cachedDefaultSprite == null)
            {
                Texture2D tex = new Texture2D(16, 16);
                Color[] colors = new Color[16 * 16];
                for (int i = 0; i < colors.Length; i++) colors[i] = Color.white;
                tex.SetPixels(colors);
                tex.Apply();
                _cachedDefaultSprite = Sprite.Create(tex, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), 16);
            }
            return _cachedDefaultSprite;
        }

        private Color GetColorForType(HarvestType type)
        {
            return type switch
            {
                HarvestType.Padi => new Color(0.95f, 0.85f, 0.3f, 1f),    // Kuning emas
                HarvestType.Jagung => new Color(1f, 0.65f, 0.15f, 1f),    // Oranye jagung
                HarvestType.Gandum => new Color(0.85f, 0.75f, 0.45f, 1f),  // Gandum cerah
                HarvestType.Wortel => new Color(1f, 0.45f, 0.1f, 1f),     // Merah oranye wortel
                HarvestType.Tomat => new Color(0.9f, 0.2f, 0.2f, 1f),      // Merah tomat
                HarvestType.Kentang => new Color(0.7f, 0.55f, 0.35f, 1f),  // Cokelat kentang
                _ => Color.white
            };
        }

        private void LoadItemDatabaseIfNeeded()
        {
            if (itemDatabase.Count > 0) return;

            var loaded = Resources.LoadAll<HarvestItemData>("HarvestItems");
            if (loaded != null && loaded.Length > 0)
            {
                itemDatabase.AddRange(loaded);
            }

#if UNITY_EDITOR
            if (itemDatabase.Count == 0)
            {
                string[] guids = UnityEditor.AssetDatabase.FindAssets("t:HarvestItemData", new[] { "Assets/Data/HarvestItems" });
                for (int i = 0; i < guids.Length; i++)
                {
                    string p = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[i]);
                    HarvestItemData item = UnityEditor.AssetDatabase.LoadAssetAtPath<HarvestItemData>(p);
                    if (item != null && !itemDatabase.Contains(item))
                    {
                        itemDatabase.Add(item);
                    }
                }
            }
#endif
        }
    }
}
