using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace SiloSystem.UI
{
    /// <summary>
    /// Komponen untuk satu kartu / box jenis item di UI Silo ("satu item satu box").
    /// Menampilkan ikon, nama, deskripsi jumlah tersimpan, serta tombol aksi Ambil/Setor.
    /// </summary>
    public class SiloItemCard : MonoBehaviour
    {
        [Header("Referensi UI Elemen")]
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI itemNameText;
        [SerializeField] private TextMeshProUGUI quantityText;
        [SerializeField] private Button pickButton;
        [SerializeField] private Button depositButton;
        [SerializeField] private TextMeshProUGUI pickButtonText;
        [SerializeField] private TextMeshProUGUI depositButtonText;

        private HarvestType _itemType;
        private Action<HarvestType> _onPickCallback;
        private Action<HarvestType> _onDepositCallback;

        public HarvestType ItemType => _itemType;

        private void Awake()
        {
            AutoDiscoverChildReferences();

            if (pickButton != null)
            {
                pickButton.onClick.AddListener(HandlePickClicked);
            }

            if (depositButton != null)
            {
                depositButton.onClick.AddListener(HandleDepositClicked);
            }
        }

        public void AutoDiscoverChildReferences()
        {
            if (itemNameText == null) itemNameText = transform.Find("NameText")?.GetComponent<TextMeshProUGUI>();
            if (quantityText == null) quantityText = transform.Find("QuantityText")?.GetComponent<TextMeshProUGUI>();
            if (iconImage == null) iconImage = transform.Find("IconImage")?.GetComponent<Image>();

            if (pickButton == null)
            {
                Transform pb = transform.Find("PickButton");
                if (pb != null)
                {
                    pickButton = pb.GetComponent<Button>();
                    pickButtonText = pb.GetComponentInChildren<TextMeshProUGUI>();
                }
            }

            if (depositButton == null)
            {
                Transform db = transform.Find("DepositButton");
                if (db != null)
                {
                    depositButton = db.GetComponent<Button>();
                    depositButtonText = db.GetComponentInChildren<TextMeshProUGUI>();
                }
            }
        }

        private void OnDestroy()
        {
            if (pickButton != null)
            {
                pickButton.onClick.RemoveListener(HandlePickClicked);
            }

            if (depositButton != null)
            {
                depositButton.onClick.RemoveListener(HandleDepositClicked);
            }
        }

        /// <summary>
        /// Menginisialisasi data pada kartu item ini.
        /// </summary>
        public void Configure(
            HarvestType type,
            int quantity,
            Sprite icon,
            string displayName,
            bool canDeposit,
            Action<HarvestType> onPick,
            Action<HarvestType> onDeposit)
        {
            _itemType = type;
            _onPickCallback = onPick;
            _onDepositCallback = onDeposit;

            if (itemNameText != null)
            {
                itemNameText.text = !string.IsNullOrEmpty(displayName) ? displayName : type.ToString();
            }

            if (quantityText != null)
            {
                quantityText.text = $"Tersimpan: {quantity} unit";
            }

            if (iconImage != null)
            {
                if (icon != null)
                {
                    iconImage.sprite = icon;
                    iconImage.color = Color.white;
                }
                else
                {
                    // Fallback visual jika icon belum dipasang
                    iconImage.color = GetFallbackColor(type);
                }
            }

            // Tombol Ambil aktif jika stok di silo > 0
            if (pickButton != null)
            {
                pickButton.interactable = quantity > 0;
            }

            // Tombol Setor aktif jika pemain memegang item sejenis
            if (depositButton != null)
            {
                depositButton.interactable = canDeposit;
            }
        }

        private void HandlePickClicked()
        {
            _onPickCallback?.Invoke(_itemType);
        }

        private void HandleDepositClicked()
        {
            _onDepositCallback?.Invoke(_itemType);
        }

        private Color GetFallbackColor(HarvestType type)
        {
            return type switch
            {
                HarvestType.Padi => new Color(0.85f, 0.75f, 0.25f, 1f), // Kuning padi
                HarvestType.Jagung => new Color(0.95f, 0.85f, 0.15f, 1f), // Kuning cerah
                HarvestType.Gandum => new Color(0.8f, 0.65f, 0.4f, 1f), // Coklat gandum
                HarvestType.Wortel => new Color(0.95f, 0.5f, 0.1f, 1f), // Oranye
                HarvestType.Tomat => new Color(0.9f, 0.2f, 0.2f, 1f), // Merah
                _ => new Color(0.6f, 0.6f, 0.6f, 1f)
            };
        }
    }
}
