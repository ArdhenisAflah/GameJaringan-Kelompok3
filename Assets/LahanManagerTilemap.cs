using UnityEngine;
using UnityEngine.Tilemaps;
using FishNet.Connection;
using FishNet.Object;
using System.Collections.Generic;

public class InfoPetak
{
    public bool sudahDicangkul;
    public bool sudahDisiram;
    public GameObject tanaman;
}

public class LahanManagerTilemap : NetworkBehaviour
{
    public static LahanManagerTilemap Instance { get; private set; }

    [Header("Pengaturan Tilemap")]
    [Tooltip("Layer Tilemap khusus untuk tanah kering (Order in Layer 0 / Sorting Layer Tanah)")]
    public Tilemap tilemapTanah;
    [Tooltip("Layer Tilemap khusus untuk tanah basah disiram (Order in Layer 1 / Sorting Layer TanahBasah)")]
    public Tilemap tilemapBasah;
    [Tooltip("Fallback single tilemap untuk backward-compatibility")]
    public Tilemap tilemapLahan;

    public TileBase tileKering; 
    public TileBase tileBasah;  

    private readonly Dictionary<Vector3Int, InfoPetak> dataGrid = new Dictionary<Vector3Int, InfoPetak>();

    public Tilemap PrimaryTilemap => tilemapTanah != null ? tilemapTanah : (tilemapLahan != null ? tilemapLahan : tilemapBasah);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;

        // Auto-discovery Tilemap jika belum terpasang di Inspector
        if (tilemapTanah == null)
        {
            GameObject go = GameObject.Find("TilemapTanah");
            if (go != null) tilemapTanah = go.GetComponent<Tilemap>();
        }
        if (tilemapBasah == null)
        {
            GameObject go = GameObject.Find("TilemapBasah");
            if (go != null) tilemapBasah = go.GetComponent<Tilemap>();
        }
        if (tilemapLahan == null)
        {
            GameObject go = GameObject.Find("TilemapLahan");
            if (go != null) tilemapLahan = go.GetComponent<Tilemap>();
            if (tilemapLahan == null && tilemapTanah == null) tilemapLahan = FindObjectOfType<Tilemap>();
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
        Ensure2DLightingIncludesAllLayers();
    }

    private void Ensure2DLightingIncludesAllLayers()
    {
        var lights = FindObjectsOfType<UnityEngine.Rendering.Universal.Light2D>();
        if (lights == null || lights.Length == 0) return;

        int[] allLayers = System.Array.ConvertAll(SortingLayer.layers, l => l.id);
        var field = typeof(UnityEngine.Rendering.Universal.Light2D).GetField("m_ApplyToSortingLayers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field == null) return;

        foreach (var light in lights)
        {
            if (light.lightType == UnityEngine.Rendering.Universal.Light2D.LightType.Global)
            {
                int[] current = field.GetValue(light) as int[];
                if (current == null || current.Length < allLayers.Length)
                {
                    field.SetValue(light, allLayers);
                }
            }
        }
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
        ApplyTileVisual(gridPos, statusDisiram);
    }

    public Vector3Int WorldToCell(Vector3 worldPos)
    {
        Tilemap tm = PrimaryTilemap;
        if (tm != null) return tm.WorldToCell(worldPos);
        return new Vector3Int(Mathf.FloorToInt(worldPos.x), Mathf.FloorToInt(worldPos.y), 0);
    }

    public Vector3 GetCellCenterWorld(Vector3Int cellPos)
    {
        Tilemap tm = PrimaryTilemap;
        if (tm != null) return tm.GetCellCenterWorld(cellPos);
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

    public InfoPetak GetOrCreateInfoPetak(Vector3Int gridPos)
    {
        if (!dataGrid.TryGetValue(gridPos, out InfoPetak petak))
        {
            petak = new InfoPetak();
            dataGrid.Add(gridPos, petak);
        }
        return petak;
    }

    public bool IsPetakDitanam(Vector3 worldPos)
    {
        Vector3Int gridPos = WorldToCell(worldPos);
        return IsPetakDitanam(gridPos);
    }

    public bool IsPetakDitanam(Vector3Int gridPos)
    {
        return dataGrid.TryGetValue(gridPos, out InfoPetak p) && p.tanaman != null;
    }

    public void DaftarkanTanaman(Vector3 worldPos, GameObject tanamanObj)
    {
        Vector3Int gridPos = WorldToCell(worldPos);
        DaftarkanTanaman(gridPos, tanamanObj);
    }

    public void DaftarkanTanaman(Vector3Int gridPos, GameObject tanamanObj)
    {
        InfoPetak petak = GetOrCreateInfoPetak(gridPos);
        petak.tanaman = tanamanObj;
    }

    public void HapusTanaman(Vector3 worldPos)
    {
        Vector3Int gridPos = WorldToCell(worldPos);
        HapusTanaman(gridPos);
    }

    public void HapusTanaman(Vector3Int gridPos)
    {
        if (dataGrid.TryGetValue(gridPos, out InfoPetak p))
        {
            p.tanaman = null;
        }
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
        ApplyTileVisual(gridPos, statusDisiram);
    }

    private void ApplyTileVisual(Vector3Int gridPos, bool statusDisiram)
    {
        if (tilemapTanah != null && tilemapBasah != null)
        {
            // Layer 1 (Order 0): Tanah Kering selalu ada sebagai dasar petak garapan
            tilemapTanah.SetTile(gridPos, tileKering);
            // Layer 2 (Order 1): Tanah Basah berada di atas tanah kering jika sudah disiram
            tilemapBasah.SetTile(gridPos, statusDisiram ? tileBasah : null);
        }
        else if (tilemapLahan != null)
        {
            tilemapLahan.SetTile(gridPos, statusDisiram ? tileBasah : tileKering);
        }
    }
}