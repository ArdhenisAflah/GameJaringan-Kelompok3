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


    public override void OnStartNetwork()
    {
        base.OnStartNetwork();

        // Memberi warna berbeda pada karakter milik sendiri agar mudah dikenali saat testing
        if (base.Owner.IsLocalClient)
        {
            GetComponent<Renderer>().material.color = Color.green; // Player Lokal = Hijau
        }
        else
        {
            GetComponent<Renderer>().material.color = Color.red;   // Player Lain = Merah
        }
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        
        // Memastikan pengaturan fisika ideal untuk game Top-Down
        rb.gravityScale = 0f; 
        rb.freezeRotation = true; // Mencegah karakter berputar saat menabrak tembok
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
    }

    private void FixedUpdate()
    {
        if (!IsOwner) return;

        // 2. Mengaplikasikan Pergerakan Fisika (Selalu lakukan di FixedUpdate)
        rb.velocity = movement * moveSpeed;
    }
}