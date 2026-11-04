using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NPCMainMenuCharacter : MonoBehaviour
{
    [Header("Komponen Karakter")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Animator animator;

    [Header("Kecepatan & Pergerakan")]
    [Tooltip("Kecepatan berjalan karakter")]
    [SerializeField] private float moveSpeed = 2.0f;

    [Tooltip("Toleransi jarak untuk menganggap sudah sampai di titik tujuan")]
    [SerializeField] private float arriveDistance = 0.05f;

    [Tooltip("Centang jika orientasi sprite asli menghadap ke kiri")]
    [SerializeField] private bool invertFlip = false;

    [Header("Batasan Area Lahan Hijau (Wander Bounds)")]
    [Tooltip("Batas bawah-kiri area lahan")]
    [SerializeField] private Vector2 farmAreaMin = new Vector2(-4.0f, -12.5f);

    [Tooltip("Batas atas-kanan area lahan")]
    [SerializeField] private Vector2 farmAreaMax = new Vector2(6.0f, -10.0f);

    [Tooltip("Opsional: Jika ada Collider2D area lahan, batas akan otomatis diambil dari collider ini")]
    [SerializeField] private Collider2D farmAreaCollider;

    [Header("Zona Terlarang (Area Title EDEN FARM)")]
    [Tooltip("Transform dari Title EDEN FARM (posisi di sekitar Y: -9.5)")]
    [SerializeField] private Transform titleTransform;

    [Tooltip("Ukuran radius area terlarang di sekitar title agar karakter tidak masuk")]
    [SerializeField] private Vector2 titleForbiddenRadius = new Vector2(3.5f, 1.2f);

    [Tooltip("Opsional: Jika ada Collider2D khusus pada Title, akan langsung digunakan sebagai zona terlarang")]
    [SerializeField] private Collider2D titleCollider;

    [Header("Daftar Animasi & Aktivitas saat Berhenti")]
    [Tooltip("Nama parameter boolean berjalan di Animator")]
    [SerializeField] private string walkingParamName = "isWalking";

    [Tooltip("Daftar nama state animasi saat karakter diam/berkebun")]
    [SerializeField] private List<string> idleActionAnimations = new List<string>
    {
        "Idle",
        "Menanam",
        "Panen",
        "AmbilBenih",
        "pick"
    };

    [Header("Waktu Diam (Wait Time)")]
    [Tooltip("Waktu diam minimum saat sampai di titik tujuan (detik)")]
    [SerializeField] private float minWaitTime = 2.0f;

    [Tooltip("Waktu diam maksimum saat sampai di titik tujuan (detik)")]
    [SerializeField] private float maxWaitTime = 4.5f;

    [Header("Status")]
    [SerializeField] private bool isActivated = false;

    private Vector3 _currentWaypoint;
    private Coroutine _wanderRoutine;
    private int _walkingParamHash;
    private bool _hasWalkingParam = false;

    private void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        if (animator == null)
            animator = GetComponent<Animator>();

        if (animator != null && !string.IsNullOrEmpty(walkingParamName))
        {
            _walkingParamHash = Animator.StringToHash(walkingParamName);
            // Cek apakah parameter ada di controller
            foreach (var param in animator.parameters)
            {
                if (param.name == walkingParamName)
                {
                    _hasWalkingParam = true;
                    break;
                }
            }
        }
    }

    private void Start()
    {
        if (isActivated)
        {
            ActivateCharacter();
        }
    }

    /// <summary>
    /// Mengaktifkan karakter untuk mulai berjalan mencari waypoint acak di lahan
    /// Dapat dipanggil dari UnityEvent (onMainMenuReady) atau Spawner
    /// </summary>
    [ContextMenu("Activate Character")]
    public void ActivateCharacter()
    {
        isActivated = true;

        if (_wanderRoutine != null)
        {
            StopCoroutine(_wanderRoutine);
        }

        _wanderRoutine = StartCoroutine(WanderRoutine());
    }

    /// <summary>
    /// Menghentikan aktivitas karakter jika diperlukan
    /// </summary>
    public void DeactivateCharacter()
    {
        isActivated = false;

        if (_wanderRoutine != null)
        {
            StopCoroutine(_wanderRoutine);
            _wanderRoutine = null;
        }

        SetWalking(false);
        PlayAnimation("Idle");
    }

    private IEnumerator WanderRoutine()
    {
        while (isActivated)
        {
            // 1. Tentukan titik waypoint baru yang valid (di lahan dan di luar area title)
            _currentWaypoint = GetValidRandomWaypoint();

            // 2. Berjalan menuju titik waypoint
            SetWalking(true);
            UpdateFlipDirection(_currentWaypoint.x);

            while (Vector2.Distance(transform.position, _currentWaypoint) > arriveDistance)
            {
                transform.position = Vector3.MoveTowards(transform.position, _currentWaypoint, moveSpeed * Time.deltaTime);
                UpdateFlipDirection(_currentWaypoint.x);
                yield return null;
            }

            // Sampai di tujuan
            transform.position = _currentWaypoint;
            SetWalking(false);

            // 3. Mainkan aksi animasi acak (Idle, Menanam, Panen, AmbilBenih, dll.)
            PlayRandomIdleAction();

            // 4. Diam beberapa detik sebelum mencari titik baru
            float waitDuration = Random.Range(minWaitTime, maxWaitTime);
            yield return new WaitForSeconds(waitDuration);
        }
    }

    /// <summary>
    /// Mencari koordinat acak di dalam lahan yang dipastikan di luar area Title
    /// </summary>
    public Vector3 GetValidRandomWaypoint()
    {
        Vector2 min = farmAreaMin;
        Vector2 max = farmAreaMax;

        if (farmAreaCollider != null)
        {
            Bounds b = farmAreaCollider.bounds;
            min = new Vector2(b.min.x, b.min.y);
            max = new Vector2(b.max.x, b.max.y);
        }

        Vector3 candidate = transform.position;
        int maxAttempts = 25;

        for (int i = 0; i < maxAttempts; i++)
        {
            float randX = Random.Range(min.x, max.x);
            float randY = Random.Range(min.y, max.y);
            candidate = new Vector3(randX, randY, transform.position.z);

            if (!IsInsideTitleArea(candidate))
            {
                return candidate;
            }
        }

        return candidate;
    }

    /// <summary>
    /// Memeriksa apakah suatu koordinat berada di dalam area terlarang Title
    /// </summary>
    private bool IsInsideTitleArea(Vector3 point)
    {
        // 1. Cek via Title Collider jika ada
        if (titleCollider != null)
        {
            return titleCollider.bounds.Contains(point);
        }

        // 2. Cek via Title Transform dan forbidden radius
        if (titleTransform != null)
        {
            Vector3 titlePos = titleTransform.position;
            float dx = Mathf.Abs(point.x - titlePos.x);
            float dy = Mathf.Abs(point.y - titlePos.y);

            if (dx <= titleForbiddenRadius.x && dy <= titleForbiddenRadius.y)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Membalikkan badan karakter (Flip X) sesuai arah berjalan
    /// </summary>
    private void UpdateFlipDirection(float targetX)
    {
        if (spriteRenderer == null) return;

        float diff = targetX - transform.position.x;
        if (Mathf.Abs(diff) > 0.01f)
        {
            bool faceLeft = diff < 0f;
            spriteRenderer.flipX = invertFlip ? !faceLeft : faceLeft;
        }
    }

    private void SetWalking(bool isWalking)
    {
        if (animator != null && _hasWalkingParam)
        {
            animator.SetBool(_walkingParamHash, isWalking);
        }
    }

    private void PlayRandomIdleAction()
    {
        if (animator == null || idleActionAnimations == null || idleActionAnimations.Count == 0) return;

        int randIndex = Random.Range(0, idleActionAnimations.Count);
        string animName = idleActionAnimations[randIndex];

        PlayAnimation(animName);
    }

    private void PlayAnimation(string animName)
    {
        if (animator == null || string.IsNullOrEmpty(animName)) return;

        // Mainkan state animasi langsung di Animator
        if (animator.HasState(0, Animator.StringToHash(animName)))
        {
            animator.Play(animName, 0, 0f);
        }
    }

    private void OnDrawGizmosSelected()
    {
        // Gambar batas lahan hijau (Hijau)
        Gizmos.color = new Color(0.2f, 0.9f, 0.3f, 0.4f);
        Vector2 min = farmAreaMin;
        Vector2 max = farmAreaMax;
        if (farmAreaCollider != null)
        {
            Bounds b = farmAreaCollider.bounds;
            min = b.min;
            max = b.max;
        }
        Vector3 center = new Vector3((min.x + max.x) * 0.5f, (min.y + max.y) * 0.5f, 0f);
        Vector3 size = new Vector3(max.x - min.x, max.y - min.y, 0.1f);
        Gizmos.DrawWireCube(center, size);

        // Gambar zona terlarang Title (Merah)
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.5f);
        if (titleCollider != null)
        {
            Gizmos.DrawWireCube(titleCollider.bounds.center, titleCollider.bounds.size);
        }
        else if (titleTransform != null)
        {
            Gizmos.DrawWireCube(titleTransform.position, new Vector3(titleForbiddenRadius.x * 2f, titleForbiddenRadius.y * 2f, 0.1f));
        }

        // Gambar titik tujuan saat ini jika sedang berjalan
        if (Application.isPlaying && isActivated)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, _currentWaypoint);
            Gizmos.DrawSphere(_currentWaypoint, 0.15f);
        }
    }
}
