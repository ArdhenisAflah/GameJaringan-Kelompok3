using UnityEngine;
using FishNet.Object;

public class PlayerAction : NetworkBehaviour
{
    public enum TipeAlat { TanganKosong, Cangkul, AlatSiram }
    
    [Header("Status Tangan")]
    public TipeAlat alatDiTangan = TipeAlat.TanganKosong;

    private void Update()
    {
        if (!IsOwner) return;

        // ==========================================
        // 1. BYPASS DEVELOPER (Jalan Pintas Testing)
        // ==========================================
        
        // Tekan '\' (Backslash) untuk MENCANGKUL paksa
        if (Input.GetKeyDown(KeyCode.Backslash))
        {
            if (LahanManagerTilemap.Instance != null)
            {
                Debug.Log("[Bypass] Mencangkul tanah!");
                LahanManagerTilemap.Instance.MintaCangkul(transform.position);
            }
        }

        // Tekan ']' (Right Bracket) untuk MENYIRAM paksa
        if (Input.GetKeyDown(KeyCode.RightBracket))
        {
            if (LahanManagerTilemap.Instance != null)
            {
                Debug.Log("[Bypass] Menyiram tanah!");
                LahanManagerTilemap.Instance.MintaSiram(transform.position);
            }
        }

        // ==========================================
        // 2. SISTEM NORMAL (Gameplay Asli Nanti)
        // ==========================================
        
        // Tombol E: Ambil/Taruh Barang
        if (Input.GetKeyDown(KeyCode.E))
        {
            if (alatDiTangan == TipeAlat.TanganKosong)
                Debug.Log("Mencoba mengambil alat...");
            else
            {
                Debug.Log($"Meletakkan {alatDiTangan} ke tanah.");
                alatDiTangan = TipeAlat.TanganKosong;
            }
        }

        // Tombol Spasi: Gunakan Alat yang sedang dipegang
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (LahanManagerTilemap.Instance == null) return;

            switch (alatDiTangan)
            {
                case TipeAlat.Cangkul:
                    LahanManagerTilemap.Instance.MintaCangkul(transform.position);
                    break;
                case TipeAlat.AlatSiram:
                    LahanManagerTilemap.Instance.MintaSiram(transform.position);
                    break;
                case TipeAlat.TanganKosong:
                    Debug.Log("Tangan kosong! Tidak bisa berbuat apa-apa.");
                    break;
            }
        }
    }
}