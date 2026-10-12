---
name: unity-runtime-ui
description: >-
  WAJIB digunakan setiap kali menangani, membuat, atau memodifikasi UI Unity, Canvas, HUD, dan popup modal.
  Menegakkan aturan Controller Canvas yang selalu aktif di mana hanya child panel yang dinonaktifkan (SetActive(false)),
  serta mewajibkan implementasi runtime UI otomatis mandiri tanpa bergantung pada klik menu Editor Tools Unity.
---

# Unity Runtime UI & Modal Lifecycle Pattern

Skill ini memandu AI agent dalam merancang, menulis, dan memelihara sistem UI di Unity (termasuk multiplayer FishNet/Netcode). 
Pola ini mengatasi dua kegagalan umum di Unity:
1. **Lifecycle Freeze pada Inactive Object**: Deaktivasi GameObject controller sebelum frame pertama mematikan method `Start()`, `Update()`, event subscription, dan instance singleton.
2. **Ketergantungan Menu Editor**: Mengharuskan developer mengeklik tombol menu `[MenuItem("Tools/...")]` manual sebelum UI bisa berfungsi di scene.

---

## 1. Aturan Emas Arsitektur UI (Controller Always-Active)

### A. Root Canvas / UI Controller HARUS Selalu Aktif
- Komponen Controller UI (misal `SiloUI`, `InventoryUI`, `ShopUI`) **DILARANG KERAS** ditempelkan langsung pada GameObject panel pop-up yang di-toggle `SetActive(false)`.
- Tempatkan komponen Controller pada root `Canvas` (atau GameObject manager khusus) yang **SELALU AKTIF (`activeSelf == true`)**.
- Variabel modal panel dijadikan referensi tersendiri (misal: `[SerializeField] private GameObject panelRoot`).

### B. Hanya Child Panel yang Dinonaktifkan (`panelRoot.SetActive(false)`)
- Saat menutup UI (`CloseUI()`):
  ```csharp
  if (panelRoot != null) panelRoot.SetActive(false);
  ```
- Saat membuka UI (`OpenUI()`):
  ```csharp
  if (panelRoot != null) panelRoot.SetActive(true);
  ```
- Properti `IsOpen` membaca status `panelRoot`:
  ```csharp
  public bool IsOpen => panelRoot != null && panelRoot.activeSelf;
  ```
- Dengan cara ini:
  - `Awake()`, `Start()`, `Update()`, dan Coroutine pada script Controller **tetap berjalan**.
  - Singleton `Instance` **tidak pernah hilang atau bernilai null**.
  - Event listener jaringan/gameplay **tidak terlepas atau gagal berlangganan**.

---

## 2. Zero-Click / Self-Contained Runtime UI Generation

**DILARANG** memaksa user/developer mengeklik menu Editor (seperti `Tools -> Setup Scene UI`) agar UI dapat muncul. Semua sistem UI wajib memiliki auto-instantiation runtime yang mandiri:

### A. Inisialisasi Otomatis via `SceneManager.sceneLoaded`
Gunakan hook `SceneManager.sceneLoaded` untuk menangani transisi antar-scene (misal dari scene `Lobby` ke `Gameplay/SampleScene`):

```csharp
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
private static void InitSceneLoadedHook()
{
    UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
}

private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
{
    // Jika scene membutuhkan UI tertentu dan belum ada instance di hierarki
    if (FindObjectOfType<TargetStorageOrTrigger>() != null && Instance == null)
    {
        CreateRuntimeCanvasUI();
    }
}
```

### B. Fallback Lazy-Creation Saat Tombol Interaksi Ditekan
Pada script interaksi gameplay (misal `SiloInteractionTrigger.cs`, `NpcInteraction.cs`), selalu sediakan direct trigger bridge:

```csharp
public void OpenInteraction()
{
    var ui = MyUIController.Instance;
    if (ui == null)
    {
        ui = FindObjectOfType<MyUIController>(true);
    }
    if (ui == null)
    {
        ui = MyUIController.CreateRuntimeCanvasUI(); // Buat saat itu juga jika belum ada
    }

    if (ui != null)
    {
        ui.OpenUI();
    }
}
```

### C. Standar Komponen `CreateRuntimeCanvasUI()`
Builder runtime wajib melengkapi elemen dasar berikut secara programatik:
1. **EventSystem**: Jika `FindObjectOfType<EventSystem>() == null`, buat GameObject `EventSystem` lengkap dengan `EventSystem` & `StandaloneInputModule`.
2. **Canvas**:
   - `renderMode = RenderMode.ScreenSpaceOverlay;`
   - `sortingOrder = 100;` (atau nilai tinggi agar tidak tertutup objek world/kamera 2D).
3. **CanvasScaler**:
   - `uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;`
   - `referenceResolution = new Vector2(1920, 1080);`
4. **GraphicRaycaster**: Wajib terpasang pada Canvas agar tombol dapat menerima event klik/pointer.
5. **Panel Modal**: Dibuat sebagai anak Canvas, dan di akhir builder diset `panelObj.SetActive(false)`.

---

## 3. Kompatibilitas Delegasi & Event (`Action`)

Ketika method `OpenUI` atau `CloseUI` disambungkan ke event delegate bawaan (seperti `Action` tanpa parameter):
- Sediakan overload parameterless:
  ```csharp
  public void OpenUI()
  {
      OpenUI(null, null);
  }

  public void OpenUI(ContextData data, TriggerSource source)
  {
      // Logika pembukaan UI dengan konteks
  }
  ```
- Hindari hanya mengandalkan method dengan parameter opsional `(data = null)` untuk event bertipe `Action`, karena compiler C# menolak pencocokan signature parameter opsional dengan `Action`.

---

## 4. Checklist Saat Membuat UI Baru di Codebase

- [ ] Controller berada di root Canvas yang selalu aktif.
- [ ] Panel pop-up / modal tersimpan di `panelRoot` dan hanya `panelRoot` yang di-`SetActive(false/true)`.
- [ ] Ada fungsi `CreateRuntimeCanvasUI()` mandiri yang otomatis memasang EventSystem, Canvas (sortingOrder tinggi), Scaler, dan GraphicRaycaster.
- [ ] Memiliki hook transisi scene `SceneManager.sceneLoaded` (`BeforeSceneLoad`).
- [ ] Script interaksi gameplay memanggil `UI.Instance.OpenUI()` secara langsung dengan fallback builder.
- [ ] Tidak ada kewajiban klik menu editor Unity untuk menguji fungsi UI.
