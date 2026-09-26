using UnityEngine;
using UnityEngine.Tilemaps;
using FishNet.Connection;
using FishNet.Object;
using System.Collections.Generic;

public class InfoPetak
{
    public bool sudahDicangkul;
    public bool sudahDisiram;
}

public class LahanManagerTilemap : NetworkBehaviour
{
    public static LahanManagerTilemap Instance { get; private set; }

    [Header("Pengaturan Tilemap")]
    public Tilemap tilemapLahan;
    public TileBase tileKering; 
    public TileBase tileBasah;  

    private readonly Dictionary<Vector3Int, InfoPetak> dataGrid = new Dictionary<Vector3Int, InfoPetak>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;

        // Auto-discovery Tilemap jika belum terpasang di Inspector
        if (tilemapLahan == null)
        {
            GameObject go = GameObject.Find("TilemapLahan");
            if (go != null) tilemapLahan = go.GetComponent<Tilemap>();
            if (tilemapLahan == null) tilemapLahan = FindObjectOfType<Tilemap>();
        }

#if UNITY_EDITOR
        if (tileKering == null)
        {
            tileKering = UnityEditor.AssetDatabase.LoadAssetAtPath<TileBase>("Assets/TileKering.asset");
        }
        if (tileBasah == null)
        {
            tileBasah = UnityEditor.AssetDatabase.LoadAssetAtPath<TileBase>("Assets/TileBasah.asset");
        }
#endif
    }

    public override void OnSpawnServer(NetworkConnection connection)
    {
        base.OnSpawnServer(connection);

        // Sinkronisasi seluruh kondisi petak tanah yang sudah ada kepada client yang baru terhubung (Late-Joiner Sync)
        foreach (KeyValuePair<Vector3Int, InfoPetak> kvp in dataGrid)
        {
            Target_SyncTile(connection, kvp.Key, kvp.Value.sudahDisiram);
        }
    }

    [TargetRpc]
    private void Target_SyncTile(NetworkConnection connection, Vector3Int gridPos, bool statusDisiram)
    {
        if (tilemapLahan != null)
        {
            tilemapLahan.SetTile(gridPos, statusDisiram ? tileBasah : tileKering);
        }
    }

    public Vector3Int WorldToCell(Vector3 worldPos)
    {
        if (tilemapLahan != null) return tilemapLahan.WorldToCell(worldPos);
        return new Vector3Int(Mathf.FloorToInt(worldPos.x), Mathf.FloorToInt(worldPos.y), 0);
    }

    public Vector3 GetCellCenterWorld(Vector3Int cellPos)
    {
        if (tilemapLahan != null) return tilemapLahan.GetCellCenterWorld(cellPos);
        return new Vector3(cellPos.x + 0.5f, cellPos.y + 0.5f, 0f);
    }

    public bool IsPetakDicangkul(Vector3 worldPos)
    {
        Vector3Int gridPos = WorldToCell(worldPos);
        return dataGrid.TryGetValue(gridPos, out InfoPetak p) && p.sudahDicangkul;
    }

    public bool IsPetakDisiram(Vector3 worldPos)
    {
        Vector3Int gridPos = WorldToCell(worldPos);
        return dataGrid.TryGetValue(gridPos, out InfoPetak p) && p.sudahDisiram;
    }

    // --- FITUR CANGKUL ---
    public void MintaCangkul(Vector3 posisiTarget)
    {
        Vector3Int gridPos = WorldToCell(posisiTarget);
        Server_ProsesCangkul(gridPos);
    }

    [ServerRpc(RequireOwnership = false)]
    private void Server_ProsesCangkul(Vector3Int gridPos)
    {
        if (!dataGrid.ContainsKey(gridPos))
        {
            InfoPetak petakBaru = new InfoPetak
            {
                sudahDicangkul = true,
                sudahDisiram = false
            };

            dataGrid.Add(gridPos, petakBaru);
            Observers_UpdateTile(gridPos, false); 
        }
    }

    // --- FITUR SIRAM ---
    public void MintaSiram(Vector3 posisiTarget)
    {
        Vector3Int gridPos = WorldToCell(posisiTarget);
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
        if (tilemapLahan == null) return;
        tilemapLahan.SetTile(gridPos, statusDisiram ? tileBasah : tileKering);
    }
}