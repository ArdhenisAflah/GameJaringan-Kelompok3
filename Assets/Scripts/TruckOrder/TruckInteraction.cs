using System;
using UnityEngine;
using TMPro;
using SiloSystem;

namespace TruckOrder
{
    /// <summary>
    /// Menangani deteksi jangkauan pemain lokal dan input tombol 'F' untuk
    /// menyetorkan Padi ke truk pesanan yang sedang aktif di loading spot.
    /// </summary>
    public class TruckInteraction : MonoBehaviour
    {
        [Header("Jangkauan & Input")]
        [Tooltip("Jarak maksimal interaksi pemain dengan truk aktif.")]
        [SerializeField] private float interactionRadius = 2.5f;

        [Tooltip("Tombol untuk memasukkan Padi ke truk pesanan.")]
        [SerializeField] private KeyCode interactKey = KeyCode.F;

        [Header("Visual Prompt")]
        [Tooltip("Tampilkan prompt mengambang saat pemain berada dalam jangkauan.")]
        [SerializeField] private bool showPrompt = true;

        [SerializeField] private Vector3 promptOffset = new Vector3(-1.2f, 0f, 0f);

        private GameObject _promptObj;
        private TextMeshPro _promptText;
        private bool _isPlayerInRange = false;

        public event Action<bool> OnPlayerProximityChanged;

        private void Awake()
        {
            SetupPromptVisual();
        }

        private void SetupPromptVisual()
        {
            if (!showPrompt) return;

            Transform existing = transform.Find("TruckPromptAnchor");
            if (existing != null)
            {
                _promptObj = existing.gameObject;
            }
            else
            {
                _promptObj = new GameObject("TruckPromptAnchor");
                _promptObj.transform.SetParent(transform, false);
                _promptObj.transform.localPosition = promptOffset;
            }

            _promptText = _promptObj.GetComponent<TextMeshPro>();
            if (_promptText == null)
            {
                _promptText = _promptObj.AddComponent<TextMeshPro>();
            }

            _promptText.fontSize = 2.4f;
            _promptText.alignment = TextAlignmentOptions.Center;
            _promptText.color = new Color(1f, 0.95f, 0.4f, 1f);
            
            // Pastikan prompt teks berada di layer Karakter dengan order tinggi
            var textRenderer = _promptObj.GetComponent<MeshRenderer>();
            if (textRenderer != null)
            {
                textRenderer.sortingLayerName = "Karakter";
                textRenderer.sortingOrder = 25;
            }
            _promptText.sortingOrder = 25;
            _promptText.text = $"[{interactKey}] Masukkan Padi";

            _promptObj.SetActive(false);
        }

        private void Update()
        {
            // Hanya aktif saat OrderSystem memiliki pesanan aktif
            if (OrderSystem.Instance == null || !OrderSystem.Instance.HasActiveOrder)
            {
                if (_isPlayerInRange) SetPromptActive(false);
                return;
            }

            PlayerInventory localPlayer = GetLocalPlayerInventory();
            if (localPlayer == null)
            {
                if (_isPlayerInRange) SetPromptActive(false);
                return;
            }

            // Hitung jarak ke truk aktif atau posisi loading spot
            Vector3 targetCenter = OrderSystem.Instance.ActiveTruck != null
                ? OrderSystem.Instance.ActiveTruck.transform.position
                : transform.position;

            float dist = Vector2.Distance(targetCenter, localPlayer.transform.position);
            bool inRange = dist <= interactionRadius;

            if (inRange != _isPlayerInRange)
            {
                _isPlayerInRange = inRange;
                SetPromptActive(_isPlayerInRange);
                OnPlayerProximityChanged?.Invoke(_isPlayerInRange);
            }

            if (_isPlayerInRange)
            {
                UpdatePromptText(localPlayer);

                if (Input.GetKeyDown(interactKey))
                {
                    ExecuteInsert(localPlayer);
                }
            }
        }

        private void UpdatePromptText(PlayerInventory player)
        {
            if (_promptText == null) return;

            HarvestType neededType = (OrderSystem.Instance != null && OrderSystem.Instance.ActiveOrder != null)
                ? OrderSystem.Instance.ActiveOrder.ItemType
                : HarvestType.Padi;

            if (player != null && player.HasItem && player.HeldType == neededType && player.IsHoldingCrop)
            {
                _promptText.text = $"[{interactKey}] Masukkan {neededType} ({player.HeldQuantity} di tangan)";
                _promptText.color = new Color(0.6f, 1f, 0.5f, 1f); // Hijau siap setor
            }
            else
            {
                _promptText.text = $"Bawa {neededType} ke Truk";
                _promptText.color = new Color(1f, 0.75f, 0.3f, 1f); // Oranye peringatan
            }
        }

        private void ExecuteInsert(PlayerInventory player)
        {
            if (OrderSystem.Instance == null) return;

            bool success = OrderSystem.Instance.TryInsertItem(player, out int addedAmount, out string feedback);
            if (success)
            {
                Debug.Log($"<color=green>[TruckInteraction]</color> {feedback}");
            }
            else
            {
                Debug.LogWarning($"<color=orange>[TruckInteraction]</color> {feedback}");
            }
        }

        private void SetPromptActive(bool active)
        {
            _isPlayerInRange = active;
            if (_promptObj != null)
            {
                _promptObj.SetActive(active);
            }
        }

        private PlayerInventory GetLocalPlayerInventory()
        {
            var players = FindObjectsOfType<PlayerInventory>();
            for (int i = 0; i < players.Length; i++)
            {
                // Prioritaskan IsOwner jika dalam multiplayer FishNet
                if (players[i].IsOwner)
                {
                    return players[i];
                }
            }

            // Fallback untuk single-player / testing
            return players.Length > 0 ? players[0] : null;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, interactionRadius);
        }
    }
}
