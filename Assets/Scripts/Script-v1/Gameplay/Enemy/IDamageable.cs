using FishNet.Object;

namespace EnemySystem
{
    /// <summary>
    /// Interface untuk entitas musuh atau objek yang dapat menerima hit/damage dari pemain.
    /// </summary>
    public interface IDamageable
    {
        int CurrentHealth { get; }
        int MaxHealth { get; }
        bool IsDead { get; }

        /// <summary>
        /// Mengurangi health dan memicu efek hit/damage.
        /// </summary>
        /// <param name="amount">Jumlah damage.</param>
        /// <param name="attacker">NetworkObject penyerang (opsional).</param>
        void TakeDamage(int amount, NetworkObject attacker = null);
    }
}
