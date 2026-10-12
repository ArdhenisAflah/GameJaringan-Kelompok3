namespace TruckOrder
{
    /// <summary>
    /// Status siklus hidup pesanan (Order).
    /// Hanya OrderSystem yang berhak mengubah status ini.
    /// </summary>
    public enum OrderStatus
    {
        Pending,   // Truk masih di antrean, belum tiba di loading spot
        Active,    // Truk sedang di loading spot dan aktif menerima item
        Completed, // Kebutuhan item telah terpenuhi sepenuhnya
        Failed     // Waktu tunggu (timer sabar) habis sebelum order terpenuhi
    }
}
