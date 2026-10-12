namespace GameManagement
{
    /// <summary>
    /// Status siklus permainan (Game State).
    /// Mendukung transisi dari permainan aktif ke Game Over maupun Kemenangan (Victory).
    /// </summary>
    public enum GameState
    {
        Playing,
        GameOver,
        Victory
    }

    /// <summary>
    /// Alasan spesifik mengapa permainan berakhir dengan Game Over.
    /// Memungkinkan diferensiasi pesan dan penanganan di masa depan.
    /// </summary>
    public enum GameOverReason
    {
        FailedOrdersExceeded,
        HostDisconnected,
        Custom
    }
}
