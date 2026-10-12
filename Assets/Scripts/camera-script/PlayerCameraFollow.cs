using UnityEngine;

namespace CameraSystem
{
    /// <summary>
    /// Komponen pengatur pergerakan kamera utama agar selalu memusatkan pandangan pada pemain lokal (Owner).
    /// Dapat dipasang langsung pada GameObject Camera atau ditambahkan secara dinamis saat runtime.
    /// </summary>
    public class PlayerCameraFollow : MonoBehaviour
    {
        public static PlayerCameraFollow Instance { get; private set; }

        [Header("Target & Posisi")]
        [Tooltip("Transform objek yang diikuti (karakter pemain lokal).")]
        [SerializeField] private Transform target;

        [Tooltip("Offset posisi kamera terhadap target. Nilai Z default adalah -10 untuk 2D.")]
        [SerializeField] private Vector3 offset = new Vector3(0f, 0f, -10f);

        [Header("Pengaturan Pergerakan")]
        [Tooltip("Jika aktif, kamera bergerak halus (smooth lerp). Jika tidak, kamera selalu mengunci target tepat di tengah secara instan.")]
        [SerializeField] private bool smoothFollow = true;

        [Tooltip("Kecepatan transisi kamera saat smooth follow aktif.")]
        [SerializeField] private float smoothSpeed = 10f;

        [Header("Batas Wilayah / Map Bounds (Opsional)")]
        [Tooltip("Aktifkan jika kamera ingin dibatasi agar tidak melihat ke luar map permainan.")]
        [SerializeField] private bool useBounds = false;
        [SerializeField] private Vector2 minBounds;
        [SerializeField] private Vector2 maxBounds;

        public Transform Target => target;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void LateUpdate()
        {
            if (target == null) return;

            Vector3 desiredPosition = target.position + offset;
            desiredPosition.z = offset.z; // Memastikan jarak Z selalu konsisten untuk kamera 2D

            if (useBounds)
            {
                desiredPosition.x = Mathf.Clamp(desiredPosition.x, minBounds.x, maxBounds.x);
                desiredPosition.y = Mathf.Clamp(desiredPosition.y, minBounds.y, maxBounds.y);
            }

            if (smoothFollow)
            {
                transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
            }
            else
            {
                transform.position = desiredPosition;
            }
        }

        /// <summary>
        /// Mengatur target yang akan difokuskan oleh kamera.
        /// </summary>
        public void SetTarget(Transform newTarget, bool snapImmediately = true)
        {
            target = newTarget;

            if (snapImmediately && target != null)
            {
                SnapToTarget();
            }
        }

        /// <summary>
        /// Menempatkan posisi kamera langsung tepat di tengah target tanpa animasi transisi.
        /// </summary>
        public void SnapToTarget()
        {
            if (target == null) return;

            Vector3 desiredPosition = target.position + offset;
            desiredPosition.z = offset.z;

            if (useBounds)
            {
                desiredPosition.x = Mathf.Clamp(desiredPosition.x, minBounds.x, maxBounds.x);
                desiredPosition.y = Mathf.Clamp(desiredPosition.y, minBounds.y, maxBounds.y);
            }

            transform.position = desiredPosition;
        }

        /// <summary>
        /// Menghapus target fokus kamera.
        /// </summary>
        public void ClearTarget()
        {
            target = null;
        }

        /// <summary>
        /// Helper statis modular untuk menugaskan target ke kamera aktif.
        /// Jika PlayerCameraFollow belum terpasang di kamera utama, method ini akan menambahkannya secara otomatis.
        /// </summary>
        public static void AssignTarget(Transform targetTransform, bool snapImmediately = true)
        {
            if (Instance == null)
            {
                Camera cam = Camera.main;
                if (cam == null)
                {
                    cam = FindObjectOfType<Camera>();
                }

                if (cam != null)
                {
                    PlayerCameraFollow follow = cam.GetComponent<PlayerCameraFollow>();
                    if (follow == null)
                    {
                        follow = cam.gameObject.AddComponent<PlayerCameraFollow>();
                    }
                    Instance = follow;
                }
            }

            if (Instance != null)
            {
                Instance.SetTarget(targetTransform, snapImmediately);
            }
            else
            {
                Debug.LogWarning("[PlayerCameraFollow] Tidak dapat menemukan Main Camera untuk mengikuti pemain!");
            }
        }
    }
}
