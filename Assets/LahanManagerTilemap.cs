using UnityEngine;
using UnityEngine.Tilemaps;
using FishNet.Connection;
using FishNet.Object;
using System.Collections.Generic;

public enum TipePetak : byte
{
    Rumput = 0,
    Kering = 1,
    Basah = 2
}

public class InfoPetak
{
    public TipePetak tipe = TipePetak.Rumput;

    public bool sudahDicangkul
    {
        get => tipe == TipePetak.Kering || tipe == TipePetak.Basah;
        set
        {
            if (value && tipe == TipePetak.Rumput) tipe = TipePetak.Kering;
            else if (!value) tipe = TipePetak.Rumput;
        }
    }

    public bool sudahDisiram
    {
        get => tipe == TipePetak.Basah;
        set
        {
            if (value) tipe = TipePetak.Basah;
            else if (tipe == TipePetak.Basah) tipe = TipePetak.Kering;
        }
    }

    public GameObject tanaman;
}

public class LahanManagerTilemap : NetworkBehaviour
{
    public static LahanManagerTilemap Instance { get; private set; }

    [Header("Pengaturan Tilemap")]
    [Tooltip("Tilemap dasar untuk rumput (Order in Layer 0 / Sorting Layer Rumput)")]
    public Tilemap tilemapGrass;
    [Tooltip("Layer Tilemap khusus untuk tanah kering (Order in Layer 0 / Sorting Layer Tanah)")]
    public Tilemap tilemapTanah;
    [Tooltip("Layer Tilemap khusus untuk tanah basah disiram (Order in Layer 1 / Sorting Layer TanahBasah)")]
    public Tilemap tilemapBasah;
    [Tooltip("Tilemap untuk rintangan / pohon (Order in Layer 0 / Sorting Layer Pohon)")]
    public Tilemap tilemapTree;
    [Tooltip("Fallback single tilemap untuk backward-compatibility")]
    public Tilemap tilemapLahan;

    [Header("Aset Tile")]
    public TileBase tileRumput;
    public TileBase tileKering; 
    public TileBase tileBasah;  
    public TileBase tileTree;

    [Header("Pengaturan Sorting Layer & Order")]
    [SerializeField] private string grassSortingLayerName = "Rumput";
    [SerializeField] private int grassSortingOrder = 0;
    [SerializeField] private string tanahSortingLayerName = "Tanah";
    [SerializeField] private int tanahSortingOrder = 0;
    [SerializeField] private string basahSortingLayerName = "TanahBasah";
    [SerializeField] private int basahSortingOrder = 1;
    [SerializeField] private string treeSortingLayerName = "Pohon";
    [SerializeField] private int treeSortingOrder = 0;

    private readonly Dictionary<Vector3Int, InfoPetak> dataGrid = new Dictionary<Vector3Int, InfoPetak>();

    public Tilemap PrimaryTilemap => tilemapTanah != null ? tilemapTanah : (tilemapGrass != null ? tilemapGrass : (tilemapLahan != null ? tilemapLahan : tilemapBasah));

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;

        // Auto-discovery Tilemap jika belum terpasang di Inspector
        if (tilemapGrass == null)
        {
            GameObject go = GameObject.Find("TilemapGrass");
            if (go == null) go = GameObject.Find("TilemapRumput");
            if (go != null) tilemapGrass = go.GetComponent<Tilemap>();
        }
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
        if (tilemapTree == null)
        {
            GameObject go = GameObject.Find("TilemapTree");
            if (go == null) go = GameObject.Find("TilemapPohon");
            if (go != null) tilemapTree = go.GetComponent<Tilemap>();
        }
        if (tilemapLahan == null)
        {
            GameObject go = GameObject.Find("TilemapLahan");
            if (go != null) tilemapLahan = go.GetComponent<Tilemap>();
            if (tilemapLahan == null && tilemapTanah == null) tilemapLahan = FindObjectOfType<Tilemap>();
        }

#if UNITY_EDITOR
        if (tileRumput == null)
        {
            tileRumput = UnityEditor.AssetDatabase.LoadAssetAtPath<TileBase>("Assets/TileRumput.asset");
        }
        if (tileKering == null)
        {
            tileKering = UnityEditor.AssetDatabase.LoadAssetAtPath<TileBase>("Assets/TileKering.asset");
        }
        if (tileBasah == null)
        {
            tileBasah = UnityEditor.AssetDatabase.LoadAssetAtPath<TileBase>("Assets/TileBasah.asset");
        }
        if (tileTree == null)
        {
            tileTree = UnityEditor.AssetDatabase.LoadAssetAtPath<TileBase>("Assets/TileTree.asset");
        }
#endif
        ApplySortingConfiguration();
        Ensure2DLightingIncludesAllLayers();
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        ApplySortingConfiguration();
    }

    /// <summary>
    /// Menetapkan sorting layer dan order in layer untuk seluruh tilemap secara konsisten.
    /// </summary>
    public void ApplySortingConfiguration()
    {
        SetTilemapSorting(tilemapGrass, grassSortingLayerName, grassSortingOrder);
        SetTilemapSorting(tilemapTanah, tanahSortingLayerName, tanahSortingOrder);
        SetTilemapSorting(tilemapBasah, basahSortingLayerName, basahSortingOrder);
        SetTilemapSorting(tilemapTree, treeSortingLayerName, treeSortingOrder);
    }

    private void SetTilemapSorting(Tilemap tm, string layerName, int order)
    {
        if (tm == null) return;
        var r = tm.GetComponent<TilemapRenderer>();
        if (r != null)
        {
            if (!string.IsNullOrEmpty(layerName))
            {
                r.sortingLayerName = layerName;
            }
            r.sortingOrder = order;
        }
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

        // Sinkronisasi seluruh kondisi petak tanah kepada client yang baru terhubung (Late-Joiner Sync)
        foreach (KeyValuePair<Vector3Int, InfoPetak> kvp in dataGrid)
        {
            Target_SyncTile(connection, kvp.Key, (byte)kvp.Value.tipe);
        }
    }

    [TargetRpc]
    private void Target_SyncTile(NetworkConnection connection, Vector3Int gridPos, byte tipeByte)
    {
        ApplyTileVisual(gridPos, (TipePetak)tipeByte);
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

    // --- HELPER PENGECEKAN PETAK ---
    public bool HasTileRumput(Vector3 worldPos) => HasTileRumput(WorldToCell(worldPos));
    public bool HasTileRumput(Vector3Int gridPos)
    {
        if (tilemapGrass != null)
        {
            TileBase t = tilemapGrass.GetTile(gridPos);
            if (t != null) return true;
        }
        return false;
    }

    public bool HasTileTree(Vector3 worldPos) => HasTileTree(WorldToCell(worldPos));
    public bool HasTileTree(Vector3Int gridPos)
    {
        if (tilemapTree != null)
        {
            TileBase t = tilemapTree.GetTile(gridPos);
            if (t != null) return true;
        }
        return false;
    }

    public bool IsPetakDicangkul(Vector3 worldPos) => IsPetakDicangkul(WorldToCell(worldPos));
    public bool IsPetakDicangkul(Vector3Int gridPos) => HasTileKering(gridPos);

    public bool HasTileKering(Vector3 worldPos) => HasTileKering(WorldToCell(worldPos));
    public bool HasTileKering(Vector3Int gridPos)
    {
        // 1. Cek dari dictionary state jaringan
        if (dataGrid.TryGetValue(gridPos, out InfoPetak p) && p.sudahDicangkul)
            return true;

        // 2. Cek apakah ada TileKering pada TilemapTanah
        if (tilemapTanah != null)
        {
            TileBase t = tilemapTanah.GetTile(gridPos);
            if (t != null && (tileKering == null || t == tileKering || t.name == "TileKering"))
                return true;
        }

        // 3. Fallback jika menggunakan single tilemap
        if (tilemapLahan != null)
        {
            TileBase t = tilemapLahan.GetTile(gridPos);
            if (t != null && (tileKering == null || t == tileKering || t.name == "TileKering" || t == tileBasah || t.name == "TileBasah"))
                return true;
        }

        return false;
    }

    public bool IsPetakDisiram(Vector3 worldPos) => IsPetakDisiram(WorldToCell(worldPos));
    public bool IsPetakDisiram(Vector3Int gridPos)
    {
        return dataGrid.TryGetValue(gridPos, out InfoPetak p) && p.sudahDisiram;
    }

    public bool CanCangkulAt(Vector3 worldPos) => CanCangkulAt(WorldToCell(worldPos));
    public bool CanCangkulAt(Vector3Int gridPos)
    {
        // 1. Pohon tidak boleh dicangkul
        if (HasTileTree(gridPos)) return false;

        // 2. Jika ada tilemapGrass, hanya area yang beralaskan rumput yang boleh dicangkul
        if (tilemapGrass != null && !HasTileRumput(gridPos)) return false;

        // 3. Petak belum dicangkul (belum TileKering)
        if (HasTileKering(gridPos)) return false;

        // 4. Petak tidak terdapat tanaman
        if (IsPetakDitanam(gridPos)) return false;

        return true;
    }

    public bool CanWaterAt(Vector3 worldPos) => CanWaterAt(WorldToCell(worldPos));
    public bool CanWaterAt(Vector3Int gridPos)
    {
        // Petak harus sudah berupa tanah kering dan belum disiram
        return HasTileKering(gridPos) && !IsPetakDisiram(gridPos);
    }

    public bool CanRestoreRumputAt(Vector3 worldPos) => CanRestoreRumputAt(WorldToCell(worldPos));
    public bool CanRestoreRumputAt(Vector3Int gridPos)
    {
        // Tanah yang sedang dicangkul/disiram dan TIDAK ada tanaman dapat dikembalikan ke rumput
        if (!HasTileKering(gridPos)) return false;
        if (IsPetakDitanam(gridPos)) return false;
        return true;
    }

    public InfoPetak GetOrCreateInfoPetak(Vector3Int gridPos)
    {
        if (!dataGrid.TryGetValue(gridPos, out InfoPetak petak))
        {
            petak = new InfoPetak();
            if (HasTileKering(gridPos))
            {
                petak.tipe = TipePetak.Kering;
            }
            dataGrid.Add(gridPos, petak);
        }
        return petak;
    }

    public bool IsPetakDitanam(Vector3 worldPos) => IsPetakDitanam(WorldToCell(worldPos));
    public bool IsPetakDitanam(Vector3Int gridPos)
    {
        return dataGrid.TryGetValue(gridPos, out InfoPetak p) && p.tanaman != null;
    }

    public void DaftarkanTanaman(Vector3 worldPos, GameObject tanamanObj) => DaftarkanTanaman(WorldToCell(worldPos), tanamanObj);
    public void DaftarkanTanaman(Vector3Int gridPos, GameObject tanamanObj)
    {
        InfoPetak petak = GetOrCreateInfoPetak(gridPos);
        petak.tanaman = tanamanObj;
    }

    public GameObject GetTanaman(Vector3 worldPos) => GetTanaman(WorldToCell(worldPos));
    public GameObject GetTanaman(Vector3Int gridPos)
    {
        return dataGrid.TryGetValue(gridPos, out InfoPetak p) ? p.tanaman : null;
    }

    public void HapusTanaman(Vector3 worldPos) => HapusTanaman(WorldToCell(worldPos));
    public void HapusTanaman(Vector3Int gridPos)
    {
        if (dataGrid.TryGetValue(gridPos, out InfoPetak p))
        {
            p.tanaman = null;
        }
    }

    // --- FITUR CANGKUL (RUMPUT -> KERING) ---
    public void MintaCangkul(Vector3 posisiTarget)
    {
        Vector3Int gridPos = WorldToCell(posisiTarget);
        Server_ProsesCangkul(gridPos);
    }

    [ServerRpc(RequireOwnership = false)]
    private void Server_ProsesCangkul(Vector3Int gridPos)
    {
        if (!CanCangkulAt(gridPos)) return;

        InfoPetak petak = GetOrCreateInfoPetak(gridPos);
        petak.tipe = TipePetak.Kering;

        Observers_UpdateTile(gridPos, (byte)TipePetak.Kering);
    }

    // --- FITUR SIRAM (KERING -> BASAH) ---
    public void MintaSiram(Vector3 posisiTarget)
    {
        Vector3Int gridPos = WorldToCell(posisiTarget);
        Server_ProsesSiram(gridPos);
    }

    [ServerRpc(RequireOwnership = false)]
    private void Server_ProsesSiram(Vector3Int gridPos)
    {
        if (!CanWaterAt(gridPos)) return;

        InfoPetak petak = GetOrCreateInfoPetak(gridPos);
        petak.tipe = TipePetak.Basah;

        Observers_UpdateTile(gridPos, (byte)TipePetak.Basah);
    }

    // --- FITUR UBAH / KEMBALIKAN KE RUMPUT SECARA DINAMIS ---
    public void MintaKembalikanRumput(Vector3 posisiTarget)
    {
        Vector3Int gridPos = WorldToCell(posisiTarget);
        MintaUbahPetak(gridPos, TipePetak.Rumput);
    }

    public void MintaUbahPetak(Vector3Int gridPos, TipePetak tipeBaru)
    {
        Server_ProsesUbahPetak(gridPos, (byte)tipeBaru);
    }

    [ServerRpc(RequireOwnership = false)]
    private void Server_ProsesUbahPetak(Vector3Int gridPos, byte tipeByte)
    {
        TipePetak tipeBaru = (TipePetak)tipeByte;
        if (tipeBaru == TipePetak.Rumput && !CanRestoreRumputAt(gridPos)) return;

        InfoPetak petak = GetOrCreateInfoPetak(gridPos);
        petak.tipe = tipeBaru;

        Observers_UpdateTile(gridPos, tipeByte);
    }

    [ObserversRpc]
    private void Observers_UpdateTile(Vector3Int gridPos, byte tipeByte)
    {
        ApplyTileVisual(gridPos, (TipePetak)tipeByte);
    }

    private void ApplyTileVisual(Vector3Int gridPos, bool statusDisiram)
    {
        ApplyTileVisual(gridPos, statusDisiram ? TipePetak.Basah : TipePetak.Kering);
    }

    private void ApplyTileVisual(Vector3Int gridPos, TipePetak tipe)
    {
        switch (tipe)
        {
            case TipePetak.Rumput:
                if (tilemapTanah != null) tilemapTanah.SetTile(gridPos, null);
                if (tilemapBasah != null) tilemapBasah.SetTile(gridPos, null);
                if (tilemapGrass != null) tilemapGrass.SetTile(gridPos, tileRumput);
                if (tilemapLahan != null) tilemapLahan.SetTile(gridPos, null);
                break;

            case TipePetak.Kering:
                if (tilemapTanah != null && tilemapBasah != null)
                {
                    tilemapTanah.SetTile(gridPos, tileKering);
                    tilemapBasah.SetTile(gridPos, null);
                }
                else if (tilemapLahan != null)
                {
                    tilemapLahan.SetTile(gridPos, tileKering);
                }
                break;

            case TipePetak.Basah:
                if (tilemapTanah != null && tilemapBasah != null)
                {
                    tilemapTanah.SetTile(gridPos, tileKering);
                    tilemapBasah.SetTile(gridPos, tileBasah);
                }
                else if (tilemapLahan != null)
                {
                    tilemapLahan.SetTile(gridPos, tileBasah);
                }
                break;
        }
    }
}