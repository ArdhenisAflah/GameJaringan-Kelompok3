using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;

public class PlayerAnimation : NetworkBehaviour
{
    private Animator _animator;
    private SpriteRenderer _spriteRenderer;
    private PlayerMovement _movement;
    private SiloSystem.PlayerInventory _inventory;
    
    private Vector3 _lastPosition;
    private static readonly int IsWalkingHash = Animator.StringToHash("isWalking");
    private static readonly int IsTanamHash = Animator.StringToHash("isTanam");

    // Sinkronisasi arah hadap kiri/kanan saat diam (Idle)
    private readonly SyncVar<bool> _isFacingLeft = new SyncVar<bool>();
    // Sinkronisasi status berjalan (Walk) antar semua pemain di jaringan
    private readonly SyncVar<bool> _isWalking = new SyncVar<bool>();

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _movement = GetComponent<PlayerMovement>();
        _inventory = GetComponent<SiloSystem.PlayerInventory>();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        _lastPosition = transform.position;

        // Listen perubahan flip saat pemain lain membalik badan
        _isFacingLeft.OnChange += (prev, next, asServer) => _spriteRenderer.flipX = next;

        // Listen perubahan status jalan pemain lain untuk animasi mulus bebas flicker
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
    }

    private void Update()
    {
        if (IsOwner)
        {
            // Owner membaca input pergerakan
            bool isMoving = _movement != null && _movement.IsMoving;

            if (_movement != null && _movement.FacingDirection.x != 0)
            {
                bool facingLeft = _movement.FacingDirection.x < 0;
                _spriteRenderer.flipX = facingLeft;
                if (_isFacingLeft.Value != facingLeft)
                {
                    ServerSetFacing(facingLeft);
                }
            }

            if (_isWalking.Value != isMoving)
            {
                ServerSetWalking(isMoving);
            }

            if (_animator != null && _animator.runtimeAnimatorController != null)
            {
                _animator.SetBool(IsWalkingHash, isMoving);
            }

            if (Input.GetKeyDown(KeyCode.E))
            {
                // Jangan picu animasi tanam jika tangan sedang membawa hasil panen (bukan benih)
                if (_inventory == null || !_inventory.IsHoldingCrop)
                {
                    TriggerTanamAnimation();
                }
            }
        }
        else
        {
            // Non-Owner mengandalkan SyncVar yang presisi dari jaringan (tanpa kalkulasi delta position yang jittery)
            _spriteRenderer.flipX = _isFacingLeft.Value;
            if (_animator != null && _animator.runtimeAnimatorController != null)
            {
                _animator.SetBool(IsWalkingHash, _isWalking.Value);
            }
        }
    }

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
}