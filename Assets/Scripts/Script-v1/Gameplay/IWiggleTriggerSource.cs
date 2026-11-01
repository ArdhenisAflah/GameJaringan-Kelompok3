using UnityEngine;

namespace Farming
{
    /// <summary>
    /// Kategori entitas yang dapat memicu interaksi pada tanaman.
    /// Memungkinkan sistem diperluas untuk NPC, hewan ternak, dsb di masa depan.
    /// </summary>
    public enum TriggerEntityType
    {
        Player,
        Animal,
        NPC,
        Other
    }

    /// <summary>
    /// Interface modular untuk entitas apapun (Player, Animal, NPC, Pet)
    /// yang dapat memicu animasi goyangan (wiggle) pada tanaman saat melintas.
    /// </summary>
    public interface IWiggleTriggerSource
    {
        TriggerEntityType EntityType { get; }
        Vector2 Velocity { get; }
    }
}
