using UnityEngine;

namespace Farming.Timer
{
    /// <summary>
    /// Interface modular untuk menghitung timer pertumbuhan tanaman.
    /// Mendukung kustomisasi durasi per stage, pengganda kecepatan (multiplier),
    /// serta fitur pause/resume tanpa terikat langsung ke MonoBehaviour.
    /// </summary>
    public interface IGrowthTimer
    {
        /// <summary>
        /// Durasi total (dalam detik) untuk stage pertumbuhan saat ini.
        /// </summary>
        float TargetDuration { get; }

        /// <summary>
        /// Waktu yang telah berlalu pada stage saat ini (dalam detik).
        /// </summary>
        float ElapsedTime { get; }

        /// <summary>
        /// Sisa waktu untuk menyelesaikan stage saat ini (dalam detik).
        /// </summary>
        float RemainingTime { get; }

        /// <summary>
        /// Progres pertumbuhan stage saat ini dalam skala 0.0 hingga 1.0.
        /// </summary>
        float ProgressRatio { get; }

        /// <summary>
        /// Pengganda kecepatan pertumbuhan (default = 1.0).
        /// Contoh: 1.5x jika disiram, 2.0x jika diberi pupuk.
        /// </summary>
        float SpeedMultiplier { get; set; }

        /// <summary>
        /// Menandakan apakah timer sedang dipause/dihentikan sementara.
        /// </summary>
        bool IsPaused { get; set; }

        /// <summary>
        /// Menandakan apakah timer pada stage saat ini sudah selesai/mencapai durasi target.
        /// </summary>
        bool IsFinished { get; }

        /// <summary>
        /// Mengupdate timer berdasarkan delta time dan pengganda kecepatan.
        /// </summary>
        /// <param name="deltaTime">Waktu per frame (contoh: Time.deltaTime).</param>
        void Tick(float deltaTime);

        /// <summary>
        /// Mereset timer dan menentukan durasi baru untuk stage berikutnya.
        /// </summary>
        /// <param name="newDuration">Durasi baru dalam detik.</param>
        void ResetTimer(float newDuration);

        /// <summary>
        /// Mengatur ulang progres timer langsung ke nilai tertentu.
        /// </summary>
        void SetProgressRatio(float ratio);

        /// <summary>
        /// Menghentikan sementara timer pertumbuhan.
        /// </summary>
        void Pause();

        /// <summary>
        /// Melanjutkan timer pertumbuhan yang sedang dipause.
        /// </summary>
        void Resume();
    }
}
