using UnityEngine;
using SiloSystem;

namespace PlayerVisual
{
    /// <summary>
    /// Komponen modular independen (Single Responsibility) untuk mengontrol parameter
    /// animasi pickup dan membawa barang (holding) pada Animator karakter.
    /// Bekerja secara reaktif mendengarkan perubahan PlayerInventory.heldItem (SyncVar)
    /// sehingga otomatis tersinkronisasi mulus di multiplayer (Host & Client/Observers)
    /// tanpa memerlukan RPC tambahan.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    [RequireComponent(typeof(PlayerInventory))]
    public class PlayerPickupAnimation : MonoBehaviour
    {
        [Header("Animator Parameters")]
        [Tooltip("Nama parameter bool di Animator yang menandakan karakter sedang membawa barang.")]
        [SerializeField] private string isHoldingParameter = "isHolding";

        [Tooltip("Nama parameter trigger di Animator yang memicu animasi gerakan mengambil (pick).")]
        [SerializeField] private string pickTriggerParameter = "pick";

        private Animator _animator;
        private PlayerInventory _inventory;

        private int _isHoldingHash;
        private int _pickTriggerHash;
        private bool _wasHolding;

        private bool _hasHoldingParam;
        private bool _hasPickParam;
        private bool _hasValidatedParams;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _inventory = GetComponent<PlayerInventory>();

            _isHoldingHash = Animator.StringToHash(isHoldingParameter);
            _pickTriggerHash = Animator.StringToHash(pickTriggerParameter);

            ValidateAnimatorParameters();
        }

        /// <summary>
        /// Memvalidasi apakah parameter yang dikonfigurasi benar-benar ada pada Animator Controller.
        /// Mencegah UnityException ("Parameter 'Hash ...' does not exist") yang menyebabkan spam error.
        /// </summary>
        private void ValidateAnimatorParameters()
        {
            _hasHoldingParam = false;
            _hasPickParam = false;

            if (_animator == null || _animator.runtimeAnimatorController == null) return;

            foreach (AnimatorControllerParameter param in _animator.parameters)
            {
                if (param.nameHash == _isHoldingHash && param.type == AnimatorControllerParameterType.Bool)
                {
                    _hasHoldingParam = true;
                }
                else if (param.nameHash == _pickTriggerHash && param.type == AnimatorControllerParameterType.Trigger)
                {
                    _hasPickParam = true;
                }
            }

            if (!_hasValidatedParams)
            {
                if (!_hasHoldingParam)
                {
                    Debug.LogWarning($"[PlayerPickupAnimation] Parameter bool '{isHoldingParameter}' (hash {_isHoldingHash}) tidak ditemukan pada Animator Controller '{_animator.runtimeAnimatorController.name}'. Transisi animasi bawa barang diabaikan sampai parameter ditambahkan ke Animator.", this);
                }

                if (!_hasPickParam)
                {
                    Debug.LogWarning($"[PlayerPickupAnimation] Parameter trigger '{pickTriggerParameter}' (hash {_pickTriggerHash}) tidak ditemukan pada Animator Controller '{_animator.runtimeAnimatorController.name}'. Animasi pick diabaikan sampai parameter ditambahkan ke Animator.", this);
                }

                _hasValidatedParams = true;
            }
        }

        private void Start()
        {
            if (_animator != null && _animator.runtimeAnimatorController != null && (!_hasHoldingParam || !_hasPickParam))
            {
                ValidateAnimatorParameters();
            }

            if (_inventory != null)
            {
                _inventory.OnHeldItemChanged += HandleHeldItemChanged;

                // Evaluasi status awal (misal saat join ke room atau spawn)
                _wasHolding = _inventory.HasItem;
                if (_hasHoldingParam && _animator != null && _animator.runtimeAnimatorController != null)
                {
                    _animator.SetBool(_isHoldingHash, _wasHolding);
                }
            }
        }

        private void OnDestroy()
        {
            if (_inventory != null)
            {
                _inventory.OnHeldItemChanged -= HandleHeldItemChanged;
            }
        }

        /// <summary>
        /// Handler reaktif yang dipicu saat item di PlayerInventory berubah (di Host maupun Observers).
        /// </summary>
        private void HandleHeldItemChanged(HarvestSlot slot)
        {
            bool isHoldingNow = slot.itemType != HarvestType.None && slot.quantity > 0;

            if (_animator != null && _animator.runtimeAnimatorController != null)
            {
                if (_hasHoldingParam)
                {
                    _animator.SetBool(_isHoldingHash, isHoldingNow);
                }

                // Jika sebelumnya tangan kosong dan sekarang memegang item -> picu animasi pick
                if (_hasPickParam && !_wasHolding && isHoldingNow)
                {
                    _animator.ResetTrigger(_pickTriggerHash);
                    _animator.SetTrigger(_pickTriggerHash);
                }
            }

            _wasHolding = isHoldingNow;
        }

        /// <summary>
        /// Watchdog ringan untuk memastikan konsistensi parameter Animator jika terjadi reset state.
        /// </summary>
        private void Update()
        {
            if (!_hasHoldingParam || _animator == null || _inventory == null || _animator.runtimeAnimatorController == null) return;

            bool isHolding = _inventory.HasItem;
            if (_animator.GetBool(_isHoldingHash) != isHolding)
            {
                _animator.SetBool(_isHoldingHash, isHolding);
                _wasHolding = isHolding;
            }
        }

        /// <summary>
        /// Memungkinkan pemicuan animasi pick secara manual dari script luar jika diperlukan.
        /// </summary>
        public void TriggerPick()
        {
            if (_hasPickParam && _animator != null && _animator.runtimeAnimatorController != null)
            {
                _animator.SetTrigger(_pickTriggerHash);
            }
        }
    }
}
