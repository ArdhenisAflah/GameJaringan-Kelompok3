# 📚 KNOWLEDGE BASE & ARSITEKTUR PROYEK (GameJaringan-Kelompok3)

Dokumen ini adalah panduan referensi utama mengenai arsitektur jaringan **FishNet**, alur input pemain, sistem penanaman (Planting), serta panduan langkah demi langkah (Step-by-Step Setup Guideline) untuk menambahkan fitur baru ke dalam proyek ini.

---

## 1. Konsep Inti Jaringan FishNet (Networking Core)

Game ini menggunakan arsitektur **Server-Authoritative** (Server sebagai pemegang otoritas tunggal atas keadaan game).

### 🔑 Aturan Utama:
1. **Client TIDAK PERNAH memunculkan objek jaringan secara mandiri**:
   - Jika Client memanggil `Instantiate()` sendiri, objek tersebut hanya ada di layar dia (tidak tersinkron dan akan menyebabkan error/desinkronisasi).
   - Semua objek multiplayer harus di-spawn oleh **Server** menggunakan `ServerManager.Spawn(gameObject)`.
2. **`IsOwner`**:
   - Properti bawaan `NetworkBehaviour` di FishNet.
   - Bernilai `true` HANYA pada komputer/klien yang memiliki kontrol atas karakter tersebut.
   - Digunakan untuk membatasi pembacaan input (keyboard/mouse) agar pemain tidak mengendalikan karakter milik pemain lain.
3. **`[ServerRpc]` (Remote Procedure Call ke Server)**:
   - Method yang dipanggil oleh Client, tetapi dijalankan di Server.
   - Digunakan untuk mengirim "niat" pemain (misal: "Saya ingin menanam di titik (X, Y)").
   - Server bertugas memvalidasi permintaan tersebut (Anti-Cheat) sebelum mengeksekusinya.
4. **`[SyncVar]`**:
   - Variabel yang nilainya disinkronkan otomatis dari Server ke semua Client.
   - Client tidak boleh mengubah nilai `SyncVar` secara langsung; hanya Server yang berhak mengubahnya.

---

## 2. Diagram Alur Aksi Pemain (Planting Flow)

```
[ Pemain Lokal (IsOwner) ]
       │
       ▼ (Tekan Tombol 'E')
[ PlayerPlanter.cs ] ──(IPlacementStrategy: Free / Grid)──> Hitung Posisi Target
       │
       ▼ (Kirim ServerRpc)
[ Server (Authority) ]
       │
       ├─► 1. Validasi Jarak (Apakah dekat dengan pemain?)
       ├─► 2. Validasi Objek (Apakah titik sudah tertutup tembok/tanaman lain?)
       │
       ▼ (Jika Valid)
[ Instantiate(plantPrefab) ]
       │
       ▼
[ ServerManager.Spawn(plant) ] ──(Replikasi FishNet)──► [ Muncul di Semua Klien ]
```

---

## 3. Struktur Arsitektur Modular

Untuk menjaga kode tetap bersih (*Clean Architecture*) dan mengikuti prinsip *Single Responsibility Principle* (SRP), setiap fungsi dipisahkan ke dalam komponennya masing-masing:

```
Assets/Scripts/
├── utils/
│   └── PlayerMovement.cs          # Mengurus pergerakan WASD & FacingDirection
└── Farming/
    ├── PlayerPlanter.cs           # Mengurus input tanam, validasi, & ServerRpc
    ├── Plant.cs                   # NetworkBehaviour untuk state tanaman (SyncVar)
    └── Placement/
        └── IPlacementStrategy.cs  # Interface modular penempatan (Free vs. Grid)
```

### Mengapa Memakai `IPlacementStrategy`?
Sistem penempatan menggunakan **Strategy Pattern**:
- **`FreePlacementStrategy`**: Meletakkan objek tepat di depan karakter sesuai arah hadap dan jarak bebas.
- **`GridPlacementStrategy`**: Meletakkan objek dengan membulatkan posisi ke petak koordinat grid terdekat (gaya *Stardew Valley*).
- **Fleksibel**: Kamu bisa mengganti mode penempatan kapan saja langsung dari Inspector Unity melalui dropdown `Placement Type` tanpa perlu mengubah kode.

---

## 4. Panduan Setup di Unity Editor (Step-by-Step Guideline)

Ikuti langkah-langkah berikut di Unity Editor untuk mengaktifkan fitur penanaman:

### Langkah 1: Membuat Prefab Tanaman (`Plant Prefab`)
1. Di Unity Hierarchy, klik kanan -> **2D Object** -> **Sprites** -> **Circle** (atau sprite tanaman apa saja).
2. Beri nama GameObject tersebut: `PlantPrefab`.
3. Di Inspector `PlantPrefab`, tambahkan komponen berikut:
   - **`NetworkObject`** (Komponen wajib FishNet agar bisa disinkronkan).
   - **`Plant`** (`Assets/Scripts/Farming/Plant.cs`).
   - **`Circle Collider 2D`** (Opsional: centang `Is Trigger` atau atur Layer-nya ke layer khusus, misal `Obstacle` atau `Plant`).
4. Tarik GameObject `PlantPrefab` dari Hierarchy ke folder Project (misal ke `Assets/Prefabs/`) untuk menjadikannya Prefab.
5. Hapus `PlantPrefab` yang ada di Hierarchy.

### Langkah 2: Mendaftarkan Prefab ke FishNet
FishNet wajib mengetahui daftar prefab yang boleh di-spawn di jaringan:
1. Cari asset **`DefaultPrefabObjects`** di folder `Assets/` (atau klik menu atas: **FishNet** -> **Configuration** -> **Refresh Default Prefabs**).
2. Pastikan `PlantPrefab` yang baru kamu buat sudah terdaftar di dalam list prefab tersebut.

### Langkah 3: Memasang `PlayerPlanter` pada Karakter Pemain
1. Buka prefab pemain di `Assets/Player.prefab`.
2. Klik tombol **Add Component** di Inspector, lalu ketik dan pilih: **`Player Planter`**.
3. Atur parameternya:
   - **Plant Key**: `E` (atau tombol keyboard lain yang kamu inginkan).
   - **Placement Type**: Pilih `Grid` (jika ingin snap ke petak) atau `Free` (jika ingin bebas).
   - **Reach Distance**: `1` (jarak jangkauan di depan karakter).
   - **Grid Size**: `1` (ukuran petak jika memilih Grid).
   - **Plant Prefab**: Drag asset `PlantPrefab` dari Langkah 1 ke kolom ini.
   - **Blocked Layers**: Centang layer yang tidak boleh ditanami (misal: Layer tembok atau layer tanaman agar tidak saling bertumpuk).
4. Save / Simpan Prefab `Player.prefab`.

---

## 5. Cara Menambahkan Fitur Input Baru di Masa Depan (Blueprint Template)

Jika di kemudian hari kamu ingin menambahkan aksi baru (misalnya: **Menyiram Tanaman / Water**, **Menyerang / Attack**, atau **Menebang / Chop**), jangan masukkan ke `PlayerMovement.cs`. Buatlah script komponen baru pada Player prefab menggunakan template berikut:

### Template Script Aksi Baru (`PlayerActionTemplate.cs`)

```csharp
using UnityEngine;
using FishNet.Object;

public class PlayerActionTemplate : NetworkBehaviour
{
    [Header("Input")]
    [SerializeField] private KeyCode actionKey = KeyCode.F;

    private PlayerMovement _playerMovement;

    private void Awake()
    {
        _playerMovement = GetComponent<PlayerMovement>();
    }

    private void Update()
    {
        // 1. HANYA klien pemilik yang membaca input
        if (!IsOwner) return;

        if (Input.GetKeyDown(actionKey))
        {
            Vector2 facing = _playerMovement.FacingDirection;
            ServerExecuteAction(transform.position, facing);
        }
    }

    // 2. Kirim permintaan ke Server
    [ServerRpc]
    private void ServerExecuteAction(Vector2 origin, Vector2 direction)
    {
        // 3. Server memvalidasi aturan game
        // ... (Contoh: periksa cooldown, periksa stamina, hit-check) ...

        // 4. Jalankan efek di Server / panggil ObserversRpc jika butuh animasi di semua klien
        Debug.Log($"Aksi dijalankan oleh pemain {OwnerId} menghadap {direction}");
    }
}
```

### Keuntungan Arsitektur Ini:
1. **Modular**: Menambah atau menghapus fitur semudah menambah atau menghapus komponen di Inspector.
2. **Aman dari Cheat**: Klien tidak bisa memalsukan spawn atau posisi karena Server selalu memvalidasi jarak dan kondisi.
3. **Mudah Di-debug**: Error pada sistem tanam tidak akan merusak sistem gerak, dan sebaliknya.

