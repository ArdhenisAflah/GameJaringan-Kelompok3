using UnityEngine;
using UnityEngine.Tilemaps;
using FishNet.Object;
using System.Collections.Generic;

public class InfoPetak
{
    public bool sudahDicangkul;
    public bool sudahDisiram;
}

public class LahanManagerTilemap : NetworkBehaviour
{
    public static LahanManagerTilemap Instance;

    [Header("Pengaturan Tilemap")]
    public Tilemap tilemapLahan;
    public TileBase tileKering; 
    public TileBase tileBasah;  

    private Dictionary<Vector3Int, InfoPetak> dataGrid = new Dictionary<Vector3Int, InfoPetak>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    // --- FITUR CANGKUL ---
    public void MintaCangkul(Vector3 posisiPemain)
    {
        Vector3Int gridPos = tilemapLahan.WorldToCell(posisiPemain);
        Server_ProsesCangkul(gridPos);
    }

    [ServerRpc(RequireOwnership = false)]
    private void Server_ProsesCangkul(Vector3Int gridPos)
    {
        if (!dataGrid.ContainsKey(gridPos))
        {
            InfoPetak petakBaru = new InfoPetak();
            petakBaru.sudahDicangkul = true;
            petakBaru.sudahDisiram = false; 

            dataGrid.Add(gridPos, petakBaru);
            Observers_UpdateTile(gridPos, false); 
        }
    }

    // --- FITUR SIRAM ---
    public void MintaSiram(Vector3 posisiPemain)
    {
        Vector3Int gridPos = tilemapLahan.WorldToCell(posisiPemain);
        Server_ProsesSiram(gridPos);
    }

    [ServerRpc(RequireOwnership = false)]
    private void Server_ProsesSiram(Vector3Int gridPos)
    {
        if (dataGrid.TryGetValue(gridPos, out InfoPetak petak))
        {
            if (!petak.sudahDisiram)
            {
                petak.sudahDisiram = true; 
                Observers_UpdateTile(gridPos, true); 
            }
        }
    }

    [ObserversRpc]
    private void Observers_UpdateTile(Vector3Int gridPos, bool statusDisiram)
    {
        if (statusDisiram) tilemapLahan.SetTile(gridPos, tileBasah);
        else tilemapLahan.SetTile(gridPos, tileKering);
    }
}