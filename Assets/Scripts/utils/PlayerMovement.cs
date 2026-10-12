using UnityEngine;
using FishNet.Object;

// Memastikan komponen Rigidbody2D otomatis ditambahkan ke GameObject
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : NetworkBehaviour
{
    [Header("Pengaturan Pergerakan")]
    [Tooltip("Kecepatan berjalan karakter.")]
    [SerializeField] private float moveSpeed = 5f;

    private Rigidbody2D rb;
    private Vector2 movement;

    /// <summary>
    /// Arah hadap terakhir pemain (terkoreksi saat bergerak).
    /// Digunakan oleh sistem aksi (seperti PlayerPlanter) untuk menentukan titik di depan pemain.
    /// Default menghadap ke bawah (Vector2.down).
    /// </summary>
    /// 
    /// // Mengetahui apakah karakter sedang bergerak atau diam
    public bool IsMoving => movement.sqrMagnitude > 0.01f;
    public Vector2 FacingDirection { get; private set; } = Vector2.down;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        
        // Memastikan pengaturan fisika ideal untuk game Top-Down
        rb.gravityScale = 0f; 
        rb.freezeRotation = true; // Mencegah karakter berputar saat menabrak tembok
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
            // Owner lokal menggunakan Dynamic physics untuk pergerakan responsif
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        }
        else
        {
            // Remote player (proxy) menggunakan Kinematic agar fisika lokal tidak melawan
            // interpolasi posisi dari FishNet NetworkTransform (menghilangkan jittering)
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.interpolation = RigidbodyInterpolation2D.None;
            rb.velocity = Vector2.zero;
        }
    }

    private void Update()
    {
        if (!IsOwner) return; // Hanya pemilik objek yang dapat mengontrol pergerakan

        // 1. Mengambil Input (Selalu lakukan di Update agar responsif)
        // GetAxisRaw membuat kontrol lebih "snappy" (langsung jalan/berhenti tanpa efek licin)
        movement.x = Input.GetAxisRaw("Horizontal");
        movement.y = Input.GetAxisRaw("Vertical");

        // Normalisasi vektor agar jalan menyamping (diagonal) tidak lebih cepat dari lurus
        movement = movement.normalized;

        // Memperbarui arah hadap saat ada pergerakan
        if (movement != Vector2.zero)
        {
            FacingDirection = movement;
        }
    }

    private void FixedUpdate()
    {
        if (!IsOwner) return;

        // 2. Mengaplikasikan Pergerakan Fisika (Selalu lakukan di FixedUpdate)
        rb.velocity = movement * moveSpeed;
    }
}