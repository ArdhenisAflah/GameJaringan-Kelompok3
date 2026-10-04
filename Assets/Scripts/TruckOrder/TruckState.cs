namespace TruckOrder
{
    /// <summary>
    /// State machine untuk perilaku kendaraan truk pengangkut order.
    /// </summary>
    public enum TruckState
    {
        Arriving,  // Sedang melaju menuju slot antrean yang ditugaskan
        Waiting,   // Sedang parkir diam di antrean (slot > 0) menunggu giliran
        Active,    // Berada di loading spot (slot 0), order aktif, timer berjalan
        Filled,    // Order berhasil dipenuhi 100%, sprite terisi, jeda singkat
        Departing, // Berangkat lurus keluar setelah pesanan selesai
        Escaping   // Berangkat kabur lurus keluar karena timer habis (Failed)
    }
}
