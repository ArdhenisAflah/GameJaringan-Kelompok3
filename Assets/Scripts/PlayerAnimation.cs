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
    }

    private void Update()
    {
        bool isMoving;

        if (IsOwner)
        {
            // Owner membaca input pergerakan
            Vector2 dir = _movement != null ? _movement.FacingDirection : Vector2.zero;
            isMoving = _movement.IsMoving;

            if (_movement.FacingDirection.x != 0)
            {
                bool facingLeft = _movement.FacingDirection.x < 0;
                _spriteRenderer.flipX = facingLeft;
                if (_isFacingLeft.Value != facingLeft)
                {
                    ServerSetFacing(facingLeft);
                }
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
            // Non-Owner mendeteksi gerakan dari perpindahan NetworkTransform
            float speed = (transform.position - _lastPosition).magnitude / Mathf.Max(Time.deltaTime, 0.001f);
            isMoving = speed > 0.05f;
            _spriteRenderer.flipX = _isFacingLeft.Value;
        }

        _lastPosition = transform.position;
        if (_animator != null && _animator.runtimeAnimatorController != null)
        {
            _animator.SetBool(IsWalkingHash, isMoving);
        }
    }

    [ServerRpc]
    private void ServerSetFacing(bool facingLeft)
    {
        _isFacingLeft.Value = facingLeft;
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