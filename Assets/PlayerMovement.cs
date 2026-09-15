using UnityEngine;

// Memastikan komponen Rigidbody2D otomatis ditambahkan ke GameObject
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Pengaturan Pergerakan")]
    [Tooltip("Kecepatan berjalan karakter.")]
    [SerializeField] private float moveSpeed = 5f;

    private Rigidbody2D rb;
    private Vector2 movement;

    private void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        
        // Memastikan pengaturan fisika ideal untuk game Top-Down
        rb.gravityScale = 0f; 
        rb.freezeRotation = true; // Mencegah karakter berputar saat menabrak tembok
    }

    private void Update()
    {
        // 1. Mengambil Input (Selalu lakukan di Update agar responsif)
        // GetAxisRaw membuat kontrol lebih "snappy" (langsung jalan/berhenti tanpa efek licin)
        movement.x = Input.GetAxisRaw("Horizontal");
        movement.y = Input.GetAxisRaw("Vertical");

        // Normalisasi vektor agar jalan menyamping (diagonal) tidak lebih cepat dari lurus
        movement = movement.normalized;
    }

    private void FixedUpdate()
    {
        // 2. Mengaplikasikan Pergerakan Fisika (Selalu lakukan di FixedUpdate)
        rb.velocity = movement * moveSpeed;
    }
}