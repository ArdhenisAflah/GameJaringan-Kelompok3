using System;
using UnityEngine;
using FishNet.Object;
using Utility;

/// <summary>
/// Controller pusat pergerakan dan kontrol fisik karakter pemain di multiplayer.
/// Mengadopsi arsitektur event-driven sehingga sistem visual/animasi hanya berperan
/// sebagai pendengar (listener) tanpa kopling langsung ke logika fisika.
/// Mewarisi IWiggleTriggerSource untuk integrasi interaksi tanaman.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : NetworkBehaviour, Farming.IWiggleTriggerSource
{
    [Header("Pengaturan Pergerakan")]
    [Tooltip("Kecepatan berjalan karakter.")]
    [SerializeField] private float moveSpeed = 5f;

    private Rigidbody2D rb;
    private PlayerInput _playerInput;
    private Vector2 _currentMoveInput;
    private Vector2 _facingDirection = Vector2.down;
    private bool _isFacingLeftLast;

    #region Event Broadcasters (Animation & Listeners)

    /// <summary>
    /// Terpicu saat pemain mulai bergerak atau berhenti bergerak. (isMoving)
    /// </summary>
    public event Action<bool> OnMoveStateChanged;

    /// <summary>
    /// Terpicu saat arah hadap kiri/kanan karakter berubah. (isFacingLeft)
    /// </summary>
    public event Action<bool> OnFacingChanged;

    /// <summary>
    /// Terpicu setiap ada pembaruan vektor arah hadap (FacingDirection).
    /// </summary>
    public event Action<Vector2> OnFacingDirectionChanged;

    #endregion

    #region Properties

    public Farming.TriggerEntityType EntityType => Farming.TriggerEntityType.Player;
    public Vector2 Velocity => rb != null ? rb.velocity : Vector2.zero;

    public bool IsMoving => _currentMoveInput.sqrMagnitude > 0.01f;
    public Vector2 FacingDirection => _facingDirection;
    public bool IsFacingLeft => _facingDirection.x < 0;

    /// <summary>
    /// Status penguncian pergerakan (misalnya saat animasi aksi menyiram/mencangkul sedang berlangsung).
    /// </summary>
    public bool IsMovementLocked { get; private set; }

    /// <summary>
    /// Komponen pembaca input pemain terpusat.
    /// </summary>
    public PlayerInput InputSource => _playerInput;

    #endregion

    /// <summary>
    /// Mengunci atau membuka kunci pergerakan pemain. Saat dikunci, velocity Rigidbody dihentikan seketika.
    /// </summary>
    public void SetMovementLocked(bool isLocked)
    {
        IsMovementLocked = isLocked;
        if (isLocked)
        {
            _currentMoveInput = Vector2.zero;
            if (rb != null)
            {
                rb.velocity = Vector2.zero;
            }
            OnMoveStateChanged?.Invoke(false);
        }
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        _playerInput = gameObject.GetOrAddComponent<PlayerInput>();

        // Pengaturan fisika ideal Top-Down 2D
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        ApplyPhysicsAuthority();
    }

    public override void OnOwnershipClient(FishNet.Connection.NetworkConnection prevOwner)
    {
        base.OnOwnershipClient(prevOwner);
        ApplyPhysicsAuthority();
    }

    private void ApplyPhysicsAuthority()
    {
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        if (rb == null) return;

        if (IsOwner)
        {
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        }
        else
        {
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.interpolation = RigidbodyInterpolation2D.None;
            rb.velocity = Vector2.zero;
        }
    }

    private void Update()
    {
        if (!IsOwner) return;

        if (IsMovementLocked)
        {
            if (_currentMoveInput != Vector2.zero)
            {
                _currentMoveInput = Vector2.zero;
                OnMoveStateChanged?.Invoke(false);
            }
            return;
        }

        // Ambil input pergerakan dari utility PlayerInput
        Vector2 prevInput = _currentMoveInput;
        _currentMoveInput = _playerInput != null ? _playerInput.MoveInput : Vector2.zero;

        bool wasMoving = prevInput.sqrMagnitude > 0.01f;
        bool nowMoving = _currentMoveInput.sqrMagnitude > 0.01f;

        if (wasMoving != nowMoving)
        {
            OnMoveStateChanged?.Invoke(nowMoving);
        }

        // Memperbarui arah hadap saat bergerak
        if (_currentMoveInput != Vector2.zero)
        {
            _facingDirection = _currentMoveInput;
            OnFacingDirectionChanged?.Invoke(_facingDirection);

            // Jika ada komponen horizontal (A atau D), cek apakah arah hadap horizontal berubah
            if (Mathf.Abs(_currentMoveInput.x) > 0.01f)
            {
                bool facingLeft = _currentMoveInput.x < 0f;
                if (_isFacingLeftLast != facingLeft)
                {
                    _isFacingLeftLast = facingLeft;
                    OnFacingChanged?.Invoke(facingLeft);
                }
            }
        }
    }

    private void FixedUpdate()
    {
        if (!IsOwner) return;

        if (IsMovementLocked)
        {
            rb.velocity = Vector2.zero;
            return;
        }

        rb.velocity = _currentMoveInput * moveSpeed;
    }
}
