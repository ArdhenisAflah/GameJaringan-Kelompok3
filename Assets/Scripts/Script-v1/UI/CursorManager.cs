using UnityEngine;

/// <summary>
/// Mengelola kursor kustom berbasis Hardware Cursor (Cursor.SetCursor) dengan dukungan DontDestroyOnLoad.
/// Mengubah kursor menjadi 'click' saat mouse kiri ditekan/ditahan dan kembali ke 'idle' saat dilepas.
/// </summary>
public class CursorManager : MonoBehaviour
{
    public static CursorManager Instance { get; private set; }

    [Header("Tekstur Kursor")]
    [Tooltip("Sprite/Tekstur kursor saat kondisi normal / idle.")]
    [SerializeField] private Texture2D cursorIdle;

    [Tooltip("Sprite/Tekstur kursor saat tombol mouse ditekan.")]
    [SerializeField] private Texture2D cursorClick;

    [Header("Pengaturan")]
    [Tooltip("Titik tumpu klik mouse (biasanya Vector2.zero untuk ujung kiri-atas).")]
    [SerializeField] private Vector2 hotspot = Vector2.zero;

    [Tooltip("Mode kursor: Auto (direkomendasikan) atau ForceSoftware.")]
    [SerializeField] private CursorMode cursorMode = CursorMode.Auto;

    private bool isClicking = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        SetIdleCursor();
    }

    private void Update()
    {
        // Deteksi klik mouse kiri
        if (Input.GetMouseButtonDown(0))
        {
            SetClickCursor();
        }
        else if (Input.GetMouseButtonUp(0))
        {
            SetIdleCursor();
        }
        // Fallback pengaman jika mouse lepas di luar window game
        else if (isClicking && !Input.GetMouseButton(0))
        {
            SetIdleCursor();
        }
    }

    /// <summary>
    /// Mengaktifkan kursor kondisi idle.
    /// </summary>
    public void SetIdleCursor()
    {
        isClicking = false;
        if (cursorIdle != null)
        {
            Cursor.SetCursor(cursorIdle, hotspot, cursorMode);
        }
    }

    /// <summary>
    /// Mengaktifkan kursor kondisi klik.
    /// </summary>
    public void SetClickCursor()
    {
        isClicking = true;
        if (cursorClick != null)
        {
            Cursor.SetCursor(cursorClick, hotspot, cursorMode);
        }
    }

    /// <summary>
    /// Mengembalikan kursor ke default bawaan sistem operasi.
    /// </summary>
    public void ResetToSystemCursor()
    {
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
    }

    private void OnDisable()
    {
        // Opsional: Jika object dinonaktifkan, kembalikan kursor default
        ResetToSystemCursor();
    }
}
