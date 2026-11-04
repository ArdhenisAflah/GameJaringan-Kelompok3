using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;

/// <summary>
/// Komponen murni listener/receiver animasi karakter (Single Responsibility).
/// Bertanggung jawab atas visual SpriteRenderer (FlipX) dan Animator parameters.
/// Menerima sinyal event dari PlayerController (movement/facing) dan PlayerAction/PlayerPlanter
/// tanpa membaca Input langsung secara hardcoded.
/// </summary>
public class PlayerAnimation : NetworkBehaviour
{
    private Animator _animator;
    private SpriteRenderer _spriteRenderer;
    private PlayerController _controller;
    private SiloSystem.PlayerInventory _inventory;

    private static readonly int IsWalkingHash = Animator.StringToHash("isWalking");
    private static readonly int IsTanamHash = Animator.StringToHash("isTanam");
    private static readonly int IsWateringHash = Animator.StringToHash("isWatering");
    private static readonly int IsDiggingHash = Animator.StringToHash("isDigging");

    // Sinkronisasi arah hadap kiri/kanan saat diam (Idle)
    private readonly SyncVar<bool> _isFacingLeft = new SyncVar<bool>();
    // Sinkronisasi status berjalan (Walk) antar semua pemain di jaringan
    private readonly SyncVar<bool> _isWalking = new SyncVar<bool>();

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _controller = GetComponent<PlayerController>();
        _inventory = GetComponent<SiloSystem.PlayerInventory>();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        // 1. Sinkronisasi Remote Players (Non-Owner) via SyncVar
        _isFacingLeft.OnChange += (prev, next, asServer) => _spriteRenderer.flipX = next;
        _isWalking.OnChange += (prev, next, asServer) =>
        {
            if (!IsOwner && _animator != null && _animator.runtimeAnimatorController != null)
            {
                _animator.SetBool(IsWalkingHash, next);
            }
        };

        // Inisialisasi awal saat client join ke room yang sedang berjalan
        _spriteRenderer.flipX = _isFacingLeft.Value;
        if (!IsOwner && _animator != null && _animator.runtimeAnimatorController != null)
        {
            _animator.SetBool(IsWalkingHash, _isWalking.Value);
        }

        // 2. Berlangganan Event dari PlayerController (Owner)
        if (IsOwner && _controller != null)
        {
            _controller.OnMoveStateChanged += HandleMoveStateChanged;
            _controller.OnFacingChanged += HandleFacingChanged;
        }
    }

    public override void OnStopClient()
    {
        base.OnStopClient();

        // Unsubscribe untuk mencegah memory leak
        if (_controller != null)
        {
            _controller.OnMoveStateChanged -= HandleMoveStateChanged;
            _controller.OnFacingChanged -= HandleFacingChanged;
        }
    }

    #region Event Handlers dari PlayerController

    private void HandleMoveStateChanged(bool isMoving)
    {
        if (_animator != null && _animator.runtimeAnimatorController != null)
        {
            _animator.SetBool(IsWalkingHash, isMoving);
        }

        if (_isWalking.Value != isMoving)
        {
            ServerSetWalking(isMoving);
        }
    }

    private void HandleFacingChanged(bool facingLeft)
    {
        if (_spriteRenderer != null)
        {
            _spriteRenderer.flipX = facingLeft;
        }

        if (_isFacingLeft.Value != facingLeft)
        {
            ServerSetFacing(facingLeft);
        }
    }

    #endregion

    private void Update()
    {
        // Hanya untuk validasi input tanam jika ada input utility
        if (IsOwner && _controller != null && _controller.InputSource != null)
        {
            if (_controller.InputSource.IsPlantPressed)
            {
                // Jangan picu animasi tanam jika tangan sedang membawa hasil panen (bukan benih)
                if (_inventory == null || !_inventory.IsHoldingCrop)
                {
                    TriggerTanamAnimation();
                }
            }
        }
    }

    #region Network Server RPCs

    [ServerRpc]
    private void ServerSetFacing(bool facingLeft)
    {
        _isFacingLeft.Value = facingLeft;
    }

    [ServerRpc]
    private void ServerSetWalking(bool isWalking)
    {
        _isWalking.Value = isWalking;
    }

    #endregion

    #region Trigger Aksi Animasi (Tanam, Siram, Cangkul)

    public void TriggerTanamAnimation()
    {
        PlayTanam();
        if (IsClientInitialized)
        {
            ServerPlayTanam();
        }
    }

    private void PlayTanam()
    {
        if (_animator != null && _animator.runtimeAnimatorController != null)
        {
            _animator.SetTrigger(IsTanamHash);
        }
    }

    [ServerRpc]
    private void ServerPlayTanam()
    {
        ObserversPlayTanam();
    }

    [ObserversRpc(ExcludeOwner = true)]
    private void ObserversPlayTanam()
    {
        PlayTanam();
    }

    /// <summary>
    /// Memutar animasi menyiram tanaman secara lokal dan menyinkronkannya ke seluruh client.
    /// </summary>
    public void TriggerWateringAnimation()
    {
        PlayWatering();
        if (IsClientInitialized)
        {
            ServerPlayWatering();
        }
    }

    private void PlayWatering()
    {
        if (_animator != null && _animator.runtimeAnimatorController != null)
        {
            _animator.SetTrigger(IsWateringHash);
        }
    }

    [ServerRpc]
    private void ServerPlayWatering()
    {
        ObserversPlayWatering();
    }

    [ObserversRpc(ExcludeOwner = true)]
    private void ObserversPlayWatering()
    {
        PlayWatering();
    }

    /// <summary>
    /// Memutar animasi mencangkul tanah secara lokal dan menyinkronkannya ke seluruh client.
    /// </summary>
    public void TriggerDiggingAnimation()
    {
        PlayDigging();
        if (IsClientInitialized)
        {
            ServerPlayDigging();
        }
    }

    private void PlayDigging()
    {
        if (_animator != null && _animator.runtimeAnimatorController != null)
        {
            _animator.SetTrigger(IsDiggingHash);
        }
    }

    [ServerRpc]
    private void ServerPlayDigging()
    {
        ObserversPlayDigging();
    }

    [ObserversRpc(ExcludeOwner = true)]
    private void ObserversPlayDigging()
    {
        PlayDigging();
    }

    #endregion
}