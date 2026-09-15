using UnityEngine;

namespace Farming.Placement
{
    /// <summary>
    /// Tipe penempatan objek (bebas vs grid).
    /// Dapat dipilih langsung melalui Unity Inspector.
    /// </summary>
    public enum PlacementType
    {
        Free,
        Grid
    }

    /// <summary>
    /// Interface modular untuk menghitung posisi penempatan objek di dunia game.
    /// Memungkinkan penggantian logika penempatan (Free, Grid, Hexagon, dll) tanpa mengubah script aksi pemain.
    /// </summary>
    public interface IPlacementStrategy
    {
        /// <summary>
        /// Menghitung koordinat target penempatan objek.
        /// </summary>
        /// <param name="origin">Posisi awal (misal: posisi pemain saat ini).</param>
        /// <param name="facingDirection">Arah hadap pemain (vektor satuan).</param>
        /// <param name="reachDistance">Jarak jangkauan ke depan pemain.</param>
        /// <param name="gridSize">Ukuran tiap petak sel grid (jika menggunakan grid).</param>
        /// <returns>Koordinat Vector2 target penempatan.</returns>
        Vector2 CalculatePosition(Vector2 origin, Vector2 facingDirection, float reachDistance, float gridSize = 1f);
    }

    /// <summary>
    /// Strategi Penempatan Bebas: Menempatkan objek tepat di depan pemain sejauh reachDistance.
    /// Cocok untuk game action RPG atau non-tile game.
    /// </summary>
    public class FreePlacementStrategy : IPlacementStrategy
    {
        public Vector2 CalculatePosition(Vector2 origin, Vector2 facingDirection, float reachDistance, float gridSize = 1f)
        {
            Vector2 dir = facingDirection != Vector2.zero ? facingDirection.normalized : Vector2.down;
            return origin + (dir * reachDistance);
        }
    }

    /// <summary>
    /// Strategi Penempatan Grid: Menghitung titik di depan pemain lalu membulatkannya ke koordinat grid terdekat.
    /// Cocok untuk game simulasi pertanian bergaya Stardew Valley atau Harvest Moon.
    /// </summary>
    public class GridPlacementStrategy : IPlacementStrategy
    {
        public Vector2 CalculatePosition(Vector2 origin, Vector2 facingDirection, float reachDistance, float gridSize = 1f)
        {
            Vector2 dir = facingDirection != Vector2.zero ? facingDirection.normalized : Vector2.down;
            Vector2 rawTarget = origin + (dir * reachDistance);

            if (gridSize <= 0.0001f) gridSize = 1f;

            float snappedX = Mathf.Round(rawTarget.x / gridSize) * gridSize;
            float snappedY = Mathf.Round(rawTarget.y / gridSize) * gridSize;

            return new Vector2(snappedX, snappedY);
        }
    }

    /// <summary>
    /// Factory helper untuk menyediakan instance strategi penempatan berdasarkan enum PlacementType.
    /// </summary>
    public static class PlacementStrategyFactory
    {
        private static readonly FreePlacementStrategy _free = new FreePlacementStrategy();
        private static readonly GridPlacementStrategy _grid = new GridPlacementStrategy();

        public static IPlacementStrategy GetStrategy(PlacementType type)
        {
            switch (type)
            {
                case PlacementType.Grid:
                    return _grid;
                case PlacementType.Free:
                default:
                    return _free;
            }
        }
    }
}

