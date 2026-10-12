using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using FishNet;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using TruckOrder;

namespace GameManagement
{
    /// <summary>
    /// Pengelola status global permainan (Game State Manager).
    /// Mengontrol siklus Playing, GameOver, dan Victory secara tersinkronisasi (Server-Authoritative).
    /// Mendeteksi kegagalan pesanan truk secara dinamis dan mengeksekusi graceful shutdown
    /// untuk mengeluarkan Host dan Client kembali ke Lobby saat Game Over.
    /// </summary>
    public class GameStateManager : NetworkBehaviour
    {
        public static GameStateManager Instance { get; private set; }

        [Header("Pengaturan Game Over")]
        [Tooltip("Batas maksimal pesanan truk yang boleh gagal sebelum memicu Game Over.")]
        [SerializeField] private int maxFailedOrders = 3;

        [Tooltip("Jeda waktu (detik) menampilkan layar Game Over sebelum server dimatikan dan pemain dikeluarkan.")]
        [SerializeField] private float shutdownDelay = 5.0f;

        [Tooltip("Nama scene tujuan saat pemain dikeluarkan dari room.")]
        [SerializeField] private string lobbySceneName = "Lobby";

        [Header("Pengaturan Kemenangan (Win Condition - Extensible)")]
        [Tooltip("Aktifkan evaluasi kondisi menang di masa depan.")]
        [SerializeField] private bool enableWinCondition = false;

        [Tooltip("Target jumlah pesanan yang harus dipenuhi untuk menang.")]
        [SerializeField] private int targetCompletedOrders = 10;

        // FishNet Synchronized Variables
        public readonly SyncVar<GameState> CurrentState = new(GameState.Playing);
        public readonly SyncVar<int> FailedOrderCount = new(0);
        public readonly SyncVar<int> CompletedOrderCount = new(0);

        // Public Events untuk UI dan Audio/SFX
        public event Action<GameState> OnGameStateChanged;
        public event Action<int, int> OnFailedOrderCountChanged; // current, max
        public event Action<int, int> OnCompletedOrderCountChanged; // current, target
        public event Action<GameOverReason> OnGameOverTriggered;
        public event Action OnVictoryTriggered;
        public event Action<float> OnShutdownCountdownTick; // remaining seconds

        public int MaxFailedOrders => maxFailedOrders;
        public float ShutdownDelay => shutdownDelay;
        public bool IsGameOver => CurrentState.Value == GameState.GameOver;
        public bool IsVictory => CurrentState.Value == GameState.Victory;

        private Coroutine _shutdownCoroutine;
        private bool _isShuttingDown = false;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            CurrentState.OnChange += HandleCurrentStateChanged;
            FailedOrderCount.OnChange += HandleFailedOrderCountChanged;
            CompletedOrderCount.OnChange += HandleCompletedOrderCountChanged;
        }

        private void Start()
        {
            BindOrderSystemEvents();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            // Reset state saat server aktif
            CurrentState.Value = GameState.Playing;
            FailedOrderCount.Value = 0;
            CompletedOrderCount.Value = 0;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            CurrentState.OnChange -= HandleCurrentStateChanged;
            FailedOrderCount.OnChange -= HandleFailedOrderCountChanged;
            CompletedOrderCount.OnChange -= HandleCompletedOrderCountChanged;

            UnbindOrderSystemEvents();
        }

        // ==========================================
        // 1. ORDER SYSTEM EVENT BINDINGS
        // ==========================================
        private void BindOrderSystemEvents()
        {
            if (OrderSystem.Instance != null)
            {
                OrderSystem.Instance.OnOrderFailed += HandleOrderFailed;
                OrderSystem.Instance.OnOrderCompleted += HandleOrderCompleted;
            }
        }

        private void UnbindOrderSystemEvents()
        {
            if (OrderSystem.Instance != null)
            {
                OrderSystem.Instance.OnOrderFailed -= HandleOrderFailed;
                OrderSystem.Instance.OnOrderCompleted -= HandleOrderCompleted;
            }
        }

        private void HandleOrderFailed(Order order)
        {
            // Izinkan evaluasi jika di Server FishNet, atau fallback offline di editor
            bool isServer = IsServerStarted || !InstanceFinder.IsClientStarted;
            if (!isServer) return;
            if (CurrentState.Value != GameState.Playing) return;

            int newFailedCount = FailedOrderCount.Value + 1;
            FailedOrderCount.Value = newFailedCount;

            Debug.Log($"<color=orange>[GameStateManager]</color> Order gagal dicatat! Total gagal: {newFailedCount}/{maxFailedOrders}");

            if (newFailedCount >= maxFailedOrders)
            {
                TriggerGameOver(GameOverReason.FailedOrdersExceeded);
            }
        }

        private void HandleOrderCompleted(Order order)
        {
            bool isServer = IsServerStarted || !InstanceFinder.IsClientStarted;
            if (!isServer) return;
            if (CurrentState.Value != GameState.Playing) return;

            int newCompletedCount = CompletedOrderCount.Value + 1;
            CompletedOrderCount.Value = newCompletedCount;

            Debug.Log($"<color=green>[GameStateManager]</color> Order sukses dicatat! Total selesai: {newCompletedCount}/{targetCompletedOrders}");

            if (enableWinCondition && newCompletedCount >= targetCompletedOrders)
            {
                TriggerVictory();
            }
        }

        // ==========================================
        // 2. STATE TRANSITIONS & SYNCVAR HANDLERS
        // ==========================================
        private void HandleCurrentStateChanged(GameState prev, GameState next, bool asServer)
        {
            OnGameStateChanged?.Invoke(next);

            if (next == GameState.GameOver)
            {
                OnGameOverTriggered?.Invoke(GameOverReason.FailedOrdersExceeded);
            }
            else if (next == GameState.Victory)
            {
                OnVictoryTriggered?.Invoke();
            }
        }

        private void HandleFailedOrderCountChanged(int prev, int next, bool asServer)
        {
            OnFailedOrderCountChanged?.Invoke(next, maxFailedOrders);
        }

        private void HandleCompletedOrderCountChanged(int prev, int next, bool asServer)
        {
            OnCompletedOrderCountChanged?.Invoke(next, targetCompletedOrders);
        }

        // ==========================================
        // 3. GAME OVER LOGIC & SHUTDOWN SEQUENCE
        // ==========================================
        /// <summary>
        /// Memicu Game Over (Server-Authoritative).
        /// </summary>
        public void TriggerGameOver(GameOverReason reason)
        {
            if (CurrentState.Value == GameState.GameOver) return;

            CurrentState.Value = GameState.GameOver;
            Debug.LogError($"<color=red>[GameStateManager] GAME OVER dipicu! Alasan: {reason}</color>");

            // Siarkan ke seluruh client
            if (IsServerStarted)
            {
                RpcGameOver(reason);
            }
            else
            {
                // Fallback offline
                OnGameOverTriggered?.Invoke(reason);
            }

            if (_shutdownCoroutine == null)
            {
                _shutdownCoroutine = StartCoroutine(GracefulShutdownRoutine());
            }
        }

        [ObserversRpc]
        private void RpcGameOver(GameOverReason reason)
        {
            OnGameOverTriggered?.Invoke(reason);

            // Jika client (bukan host), jalankan timer countdown lokal juga sebagai safety net
            if (!IsServerStarted && _shutdownCoroutine == null)
            {
                _shutdownCoroutine = StartCoroutine(ClientCountdownWatchdogRoutine());
            }
        }

        /// <summary>
        /// Memicu Kemenangan (Win Condition - Server-Authoritative).
        /// </summary>
        public void TriggerVictory()
        {
            if (CurrentState.Value != GameState.Playing) return;

            CurrentState.Value = GameState.Victory;
            Debug.Log("<color=green>[GameStateManager] VICTORY dipicu! Target pesanan terpenuhi!</color>");

            if (IsServerStarted)
            {
                RpcVictory();
            }
            else
            {
                OnVictoryTriggered?.Invoke();
            }
        }

        [ObserversRpc]
        private void RpcVictory()
        {
            OnVictoryTriggered?.Invoke();
        }

        [ObserversRpc]
        private void RpcShutdownCountdownTick(float remaining)
        {
            OnShutdownCountdownTick?.Invoke(remaining);
        }

        /// <summary>
        /// Coroutine Server untuk memberikan buffer tampilan Game Over sebelum mematikan server.
        /// </summary>
        private IEnumerator GracefulShutdownRoutine()
        {
            float elapsed = 0f;
            while (elapsed < shutdownDelay)
            {
                float remaining = Mathf.Max(0f, shutdownDelay - elapsed);
                OnShutdownCountdownTick?.Invoke(remaining);

                if (IsServerStarted)
                {
                    RpcShutdownCountdownTick(remaining);
                }

                yield return new WaitForSeconds(1.0f);
                elapsed += 1.0f;
            }

            OnShutdownCountdownTick?.Invoke(0f);
            ExecuteServerShutdown();
        }

        /// <summary>
        /// Watchdog klien jika koneksi terputus mendadak atau server terlambat menutup socket.
        /// </summary>
        private IEnumerator ClientCountdownWatchdogRoutine()
        {
            float elapsed = 0f;
            while (elapsed < shutdownDelay + 1.0f)
            {
                float remaining = Mathf.Max(0f, shutdownDelay - elapsed);
                OnShutdownCountdownTick?.Invoke(remaining);
                yield return new WaitForSeconds(1.0f);
                elapsed += 1.0f;
            }

            // Jika belum kembali ke Lobby, paksa kembali
            ExecuteClientLocalEject();
        }

        /// <summary>
        /// Menutup server, menghapus UGS cloud lobby, dan mengarahkan Host kembali ke Lobby.
        /// </summary>
        private async void ExecuteServerShutdown()
        {
            if (_isShuttingDown) return;
            _isShuttingDown = true;

            Debug.Log("<color=yellow>[GameStateManager]</color> Mengeksekusi shutdown server dan membersihkan lobby...");

            try
            {
                // 1. Bersihkan UGS Lobby jika ada
                if (lobby.Instance != null)
                {
                    await lobby.Instance.LeaveLobby();
                }

                // 2. Hentikan koneksi server FishNet (fallback jika belum dihentikan)
                if (InstanceFinder.NetworkManager != null && InstanceFinder.IsServerStarted)
                {
                    InstanceFinder.ServerManager.StopConnection(true);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[GameStateManager] Peringatan saat shutdown: {ex.Message}");
            }
            finally
            {
                // 3. Muat scene Lobby
                if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != lobbySceneName)
                {
                    UnityEngine.SceneManagement.SceneManager.LoadScene(lobbySceneName);
                }
            }
        }

        /// <summary>
        /// Ejeksi lokal di sisi client.
        /// </summary>
        private async void ExecuteClientLocalEject()
        {
            if (_isShuttingDown) return;
            _isShuttingDown = true;

            Debug.Log("<color=yellow>[GameStateManager]</color> Ejeksi client kembali ke Lobby...");

            try
            {
                if (lobby.Instance != null)
                {
                    await lobby.Instance.LeaveLobby();
                }

                if (InstanceFinder.NetworkManager != null && InstanceFinder.IsClientStarted)
                {
                    InstanceFinder.ClientManager.StopConnection();
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[GameStateManager] Peringatan saat client eject: {ex.Message}");
            }
            finally
            {
                if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != lobbySceneName)
                {
                    UnityEngine.SceneManagement.SceneManager.LoadScene(lobbySceneName);
                }
            }
        }

        // ==========================================
        // 4. DYNAMIC CONFIGURATION & CHEATS/TESTING
        // ==========================================
        /// <summary>
        /// Mengatur batas maksimal pesanan gagal secara dinamis.
        /// </summary>
        public void SetMaxFailedOrders(int max)
        {
            maxFailedOrders = Mathf.Max(1, max);
            OnFailedOrderCountChanged?.Invoke(FailedOrderCount.Value, maxFailedOrders);
            Debug.Log($"<color=cyan>[GameStateManager]</color> Batas gagal order diubah menjadi: {maxFailedOrders}");
        }

        /// <summary>
        /// API manual untuk memicu Game Over (keperluan testing).
        /// </summary>
        public void ServerManualTriggerGameOver(GameOverReason reason = GameOverReason.Custom)
        {
            TriggerGameOver(reason);
        }

        /// <summary>
        /// API manual untuk memicu Victory (keperluan testing).
        /// </summary>
        public void ServerManualTriggerVictory()
        {
            TriggerVictory();
        }
    }
}
