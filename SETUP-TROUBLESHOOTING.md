# SETUP & TROUBLESHOOTING — KUBIKA TOWER (Tetris3D)

**Dibuat:** 2 September 2026
**Pendamping:** `HANDOFF.md` (daftar perubahan gameplay yang direncanakan)
**Status:** Dokumen diagnosis. Belum ada file `.cs` yang diubah.

Dokumen ini terpisah dari `HANDOFF.md` karena isinya bukan "perubahan yang ingin dilakukan", melainkan **hal-hal yang membuat proyek tidak bisa dibuka atau di-build sama sekali**. Kerjakan yang di sini lebih dulu — tanpa ini, tidak ada item di `HANDOFF.md` yang bisa dites.

---

## MASALAH 1 — BLOKIR TOTAL: `Tetris3D.AdLoading.cs` tidak bisa dikompilasi

### Gejala

Saat membuka proyek, Unity menampilkan dialog:

> **Enter Safe Mode?**
> The project you are opening contains compilation errors.

Console menunjukkan **30 error**, semuanya di `Assets/Tetris3D.AdLoading.cs`, dengan kode error:

```
CS1056: Unexpected character '\'
CS1010: Newline in constant
CS1525: Invalid expression term '"'
CS1003: Syntax error, ':' expected
CS1002: ; expected
CS1026: ) expected
```

### Penyebab

**Semua tanda kutip di file itu ter-escape.** Isi file yang tersimpan berbentuk `\"` (backslash + kutip) padahal seharusnya `"` saja.

Baris 68, apa adanya di repo:

```csharp
string msg = (lang == Lang.ID) ? \"Memuat iklan...\" : \"Loading ad...\";
```

Seharusnya:

```csharp
string msg = (lang == Lang.ID) ? "Memuat iklan..." : "Loading ad...";
```

Backlash bukan escape yang sah di luar string literal, jadi compiler menolaknya (`CS1056`), lalu menganggap kutip berikutnya membuka string yang tidak pernah ditutup sampai akhir baris (`CS1010`). Satu backslash memicu 3–6 error. Enam lokasi rusak menghasilkan 30 error.

Verifikasi kolomnya cocok: di baris 68, hitung 8 spasi indentasi + `string`(9–14) + `msg`(16–18) + `=`(20) + `(lang`(22–26) + `==`(28–29) + `Lang.ID)`(31–38) + `?`(40) → backslash tepat di **kolom 42**. Console melaporkan `(68,42)`. Cocok persis.

### Lokasi yang rusak

| Baris | Kolom | Kode |
|---|---|---|
| 68 | 42 | `? \"Memuat iklan...\" : \"Loading ad...\"` |
| 392 | 33 | `new GameObject(\"KubikaAdGate\")` |
| 441 | 33 | `new GameObject(\"KubikaCoinFlyHUD\")` |
| 482 | 33 | `new GameObject(\"KubikaPetiWatcher\")` |
| 511 | 30 | `const string SPR_CLOSE = \"...\"` |
| 512 | 30 | `const string SPR_OPEN  = \"...\"` |
| 523 | 33 | `new GameObject(\"KubikaPetiChest3D\")` |

Pola kolomnya konsisten: 33 = tepat setelah `GameObject(`, 30 = tepat setelah `= ` pada deklarasi `const string`.

Ada juga `\"` di komentar baris 11, 14, dan 45. Itu **tidak** menyebabkan error (compiler mengabaikan isi komentar), tapi ikut terkoreksi oleh perbaikan di bawah.

### Perbaikan

Buka `Assets/Tetris3D.AdLoading.cs`, lalu Find & Replace **hanya di file itu**:

```
Find:     \"
Replace:  "
```

**Matikan mode Regex** dulu (ikon `.*` di kotak pencarian VS Code), supaya backslash dibaca sebagai karakter biasa.

Replace menyeluruh **aman untuk file ini** — sudah diverifikasi tidak ada satu pun string di dalamnya yang memang membutuhkan kutip ter-escape (tidak ada `\\n`, tidak ada kutip di dalam kutip).

Setelah simpan: kembali ke Unity → menu **Safe Mode → Exit Safe Mode**, atau tekan `Ctrl+R`.

### Yang BUKAN penyebabnya

Dua hipotesis yang sudah diperiksa dan **dinyatakan salah**:

1. **Bukan karena migrasi disk / ganti Windows.** Error leksikal seperti ini murni soal karakter di dalam teks file. Memindahkan file antar drive tidak menyuntikkan backslash.

2. **Bukan karena AdMob dinonaktifkan.** Keenam baris yang error berada **di luar** blok `#if KUBIKA_ADMOB`. Satu-satunya `#if` di file ini ada di `MrecUiShift()` paling bawah, dan blok itu punya `#else` yang benar. Menyalakan atau mematikan scripting define tidak mengubah isi file `.cs`, dan tidak berpengaruh ke keenam baris tersebut. Lihat Masalah 3 untuk verifikasi lengkap perilaku saat iklan dimatikan.

### Catatan penting: kerusakan ini ada di repo

File ini **tersimpan rusak di GitHub**, bukan hanya di satu mesin. Artinya:

- Clone ulang **tidak** menyelesaikan masalah — malah membawa versi rusak ke mesin baru
- Siapa pun yang meng-clone repo ini akan langsung masuk Safe Mode
- Kalau ada salinan lama di disk lain yang masih utuh, bandingkan file itu

Kemungkinan besar file ini pernah ditulis atau ditempel lewat perantara yang meng-escape kutip (misalnya payload JSON), lalu ter-commit apa adanya.

**Setelah diperbaiki lokal, commit perbaikannya** supaya jebakan ini tidak terulang.

### Status audit file lain

Seluruh file `.cs` di `Assets/` sudah diperiksa untuk kerusakan yang sama. **Hanya `Tetris3D.AdLoading.cs` yang terkena.** Berikut yang sudah dikonfirmasi bersih:

`Tetris3D.cs`, `Part2.cs`, `Part3.cs`, `Part4.cs`, `Extras.cs`, `Currency.cs`, `Gelembung.cs`, `Gelembung2.cs`, `Toko.cs`, `PetiKoin.cs`, `Saldoku.cs`, `AdsReviveMrec.cs`, `UiScale.cs`

Console juga mengonfirmasi ini: 30 error, semuanya menunjuk satu file.

> **Peringatan:** error sintaks menghentikan compiler sebelum tahap semantik. Jadi setelah kutip diperbaiki, **mungkin muncul error baru** yang sebelumnya tersembunyi. Kalau itu terjadi, lihat Masalah 3.

---

## MASALAH 2 — RANJAU: Git LFS belum diunduh

### Kenapa ini penting

`.gitattributes` di repo mengatur tipe file berikut lewat Git LFS:

```
*.dll   lfs      *.png   lfs      *.wav   lfs
*.pdb   lfs      *.jpg   lfs      *.mp3   lfs
*.so    lfs      *.psd   lfs      *.ttf   lfs
*.apk   lfs      *.tga   lfs      *.otf   lfs
```

Kalau repo di-clone di mesin yang belum ada Git LFS, semua file itu turun sebagai **pointer teks ~130 byte**, bukan file aslinya.

Bukti nyata di repo saat ini — isi `Assets/GoogleMobileAds/GoogleMobileAds.dll`:

```
version https://git-lfs.github.com/spec/v1
oid sha256:ed42f9ba2fea3307a2a0aec4fb55d0bbd58ffcbe0a9f48df8189e9338d592394
size 45568
```

Ukuran aslinya **45.568 byte**, yang tersimpan hanya **130 byte**. Kesepuluh DLL AdMob bernasib sama (129–131 byte).

### Akibat kalau LFS tidak diunduh

| Aset | Gejala |
|---|---|
| DLL AdMob | `CS0246: namespace 'GoogleMobileAds' could not be found` — **hanya kalau define `KUBIKA_ADMOB` aktif** |
| Font `FPSFont/FPS Gaming Font/Square-Black` | Semua teks HUD jatuh ke font default Unity |
| `KubikaIcons/*` (Crown, Hand, Gem, Coin, Boom, Hammer, Clock) | Ikon jadi kotak putih atau hilang |
| Sprite peti Royal | `KubikaPetiChest3D.Report()` balikkan null → fallback ke peti gambar-kode |
| `Assets/logo/` | Logo hilang |

Catatan: sprite peti punya fallback yang benar (`DrawPetiChestProcedural`), jadi itu tidak fatal — hanya tampil beda.

### Perbaikan

```bash
cd <folder-proyek>
git lfs install
git lfs pull
```

Verifikasi — ini langkah yang menentukan:

```bash
# Windows CMD
dir Assets\GoogleMobileAds\GoogleMobileAds.dll
```

Harus terbaca **45.568 byte**. Kalau masih 130 byte, LFS belum jalan.

### Kalau kuota LFS habis

GitHub gratis memberi 1 GB bandwidth LFS per bulan. Kalau `git lfs pull` gagal karena kuota, jalan pintasnya: unduh **Google Mobile Ads Unity Plugin v11.4.0** langsung dari GitHub releases resmi Google, lalu impor `.unitypackage`-nya menimpa `Assets/GoogleMobileAds`.

Versi itu tercatat di `Assets/GoogleMobileAds/GoogleMobileAds_version-11.4.0_manifest.txt`. **Pakai versi yang sama** supaya `.meta` dan GUID tidak berubah.

---

## MASALAH 3 — Perilaku saat iklan dinonaktifkan (masa review Play Store)

### Konteks

Selama proses review Play Store (~14 hari), reviewer meminta iklan dimatikan dulu. Caranya: hapus `KUBIKA_ADMOB` dari **Project Settings → Player → Scripting Define Symbols (Android)**.

### Kabar baik: mematikan define itu AMAN

Sudah diverifikasi file per file. Arsitekturnya benar — **semua state disimpan di luar `#if`, hanya panggilan SDK yang diselubungi, dan setiap kelas punya stub `#else`**:

| Kelas / anggota | File | Jalur `#else` |
|---|---|---|
| `KubikaAds.ShowPetiAd` | `PetiKoin.cs` | `game.OnPetiAdUnavailable(game.PetiAdsOffMsg())` |
| `KubikaReviveAds.Show` | `AdsReviveMrec.cs` | Editor → `onReward()`; device → `OnReviveAdUnavailable()` |
| `KubikaExtraAds.Show` | `Gelembung2.cs` | Editor → `onReward()`; device → `OnBubbleAdUnavailable()` |
| `KubikaMrec.ShowMrec` / `HideMrec` | `AdsReviveMrec.cs` | kosong (no-op) |
| `Tetris3D.MrecUiShift()` | `AdLoading.cs` | `return 0f` |

Field state yang dipakai `AdLoadingActive` semuanya dideklarasikan **di luar** guard, jadi tidak ada `CS0103`:

- `petiBusy` → `PetiKoin.cs`, di dalam `partial class Tetris3D`, tanpa `#if`
- `reviveAdPending` → `Extras.cs`, tanpa `#if`
- `kbAdBusy` → diakses lewat `SetBubbleAdBusy()` di `Gelembung2.cs`, tanpa `#if`

Juga tidak ada risiko game membeku: `KubikaAdGate` menyetel `Time.timeScale = 0f` selama `AdLoadingActive`, tapi dengan iklan mati, jalur `#else` tidak pernah menyetel flag busy jadi `true`, dan `OnPetiAdUnavailable` / `OnBubbleAdUnavailable` selalu mereset ke `false`.

### Yang perlu diperhatikan

**1. Di Editor, hadiah diberikan GRATIS.**

```csharp
#else
    public void Show(...)
    {
#if UNITY_EDITOR
        if (onReward != null) onReward();   // langsung dapat hadiah
#else
        game.OnBubbleAdUnavailable(...);
#endif
    }
#endif
```

Berlaku untuk `KubikaExtraAds` (buff Bom/Palu/Perlambat) dan `KubikaReviveAds` (revive). Jadi **menguji di Editor tidak mencerminkan perilaku di perangkat**. Untuk memvalidasi build reviewer, harus tes di device atau build Android sungguhan.

**2. Gelembung tetap muncul walau tidak bisa diklaim.**

Ini yang paling perlu ditangani sebelum menyerahkan build ke reviewer. `BubbleTick()` tetap memunculkan gelembung tiap `BUBBLE_MIN_GAP`–`BUBBLE_MAX_GAP` (24–40 detik) tanpa memeriksa apakah iklan aktif. Reviewer akan melihat gelembung, menyentuhnya, membuka panel klaim, menekan "Tonton Iklan", lalu hanya mendapat toast:

> "Fitur iklan belum aktif di build ini."

Berulang setiap ~30 detik. Kesannya fitur rusak, bukan fitur dimatikan.

**Usulan:** bungkus spawn gelembung dengan guard define, atau tambahkan flag `adsEnabled` yang dibaca `BubbleTick()`, `PetiKoinBtn()`, dan tawaran revive. Dengan begitu build "iklan mati" tampil bersih, bukan tampak bug.

Hal yang sama berlaku untuk tombol **Peti Koin** di overlay SALDOKU dan tombol **Tonton Iklan** di layar revive.

**3. Cek DLL sebelum menyalakan kembali define.**

Selama `KUBIKA_ADMOB` mati, DLL AdMob tidak ikut dikompilasi — jadi Masalah 2 **tidak terlihat**. Begitu define dinyalakan lagi setelah review lolos, `CS0246` bisa muncul mendadak. Jalankan `git lfs pull` dan verifikasi ukuran DLL **sebelum** menyalakan define.

---

## CHECKLIST CLONE BARU

Urutkan seperti ini di mesin baru:

```bash
# 1. Pastikan Git LFS ada SEBELUM clone
git lfs install

# 2. Clone
git clone https://github.com/muhrizky645-png/Tetris3D.git
cd Tetris3D

# 3. Tarik file LFS (kalau clone dilakukan tanpa LFS)
git lfs pull

# 4. Verifikasi DLL utuh — harus 45.568 byte
dir Assets\GoogleMobileAds\GoogleMobileAds.dll
```

Lalu, **sebelum membuka Unity**:

- [ ] Cek `Assets/Tetris3D.AdLoading.cs` — pastikan tidak ada `\"` (lihat Masalah 1). Kalau perbaikannya belum ter-commit, lakukan Find & Replace dulu.
- [ ] Pastikan versi Unity cocok. `Packages/manifest.json` memakai URP 17.5.0, `com.unity.ai.inference`, dan `com.unity.ai.assistant` → **Unity 6.2**.
- [ ] Pastikan ada koneksi internet saat pertama membuka, supaya Package Manager bisa me-resolve paket.

Baru buka Unity.

Kalau tetap masuk Safe Mode: buka Console (`Ctrl+Shift+C`), aktifkan filter Error, dan baca **error paling atas**. Error pertama yang menentukan — sisanya biasanya efek berantai.

| Isi error | Artinya |
|---|---|
| `CS1056` / `CS1010` / `CS1525` | Masalah 1 — kutip ter-escape |
| `CS0246 ... 'GoogleMobileAds'` | Masalah 2 — Git LFS belum ditarik |
| `CS0246 ... 'Unity.Services'` | Paket gagal di-resolve — cek internet, buka Package Manager |
| `CS0103 ... 'kbAdBusy'` dsb. | Tidak diharapkan — semua flag sudah di luar `#if` (lihat Masalah 3) |

---

## RINGKASAN PRIORITAS

| # | Masalah | Dampak | Usaha |
|---|---|---|---|
| 1 | Kutip ter-escape di `AdLoading.cs` | **Proyek tidak bisa dibuka** | Satu Find & Replace |
| 2 | Git LFS belum ditarik | DLL, font, ikon hilang | Satu perintah |
| 3 | Gelembung tetap muncul saat iklan mati | Tampak bug di mata reviewer | Satu guard |

Masalah 1 harus lebih dulu — tanpa itu Unity tidak akan terbuka. Masalah 3 sebaiknya selesai sebelum build diserahkan ke reviewer Play Store.
