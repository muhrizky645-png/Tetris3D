# HANDOFF — KUBIKA TOWER (Tetris3D)

**Dibuat:** 2 September 2026  
**Diperbarui:** 4 September 2026  
**Status:** Bagian 2 & 3 SUDAH DIEKSEKUSI. F1–F13 selesai kecuali F9.  
**Repo:** `muhrizky645-png/Tetris3D` — branch `main`

Dokumen ini adalah hasil audit read-only seluruh kode inti, plus daftar perubahan yang ingin dilakukan. Tujuannya supaya pekerjaan bisa dilanjutkan kapan saja tanpa harus mengaudit ulang.

Bagian 3 (perbaikan cepat) dan Bagian 2 (tuning diameter) **sudah dikerjakan** — lihat **Bagian 0.5** untuk status, SHA commit, dan beberapa koreksi penting terhadap isi dokumen ini sendiri. Bagian 4 ke bawah masih rencana.

Dokumen pendamping: **`SETUP-TROUBLESHOOTING.md`** (masalah Unity / Safe Mode / LFS / build review).

---

## 0. NIAT DESAIN (dari owner — JANGAN dilanggar)

Hal-hal berikut adalah **keputusan desain sadar**, bukan bug. Jangan "diperbaiki".

1. **Game ini endless dan sengaja dibuat agak MUDAH.** Tujuannya mengejar skor tertinggi, bukan menyulitkan pemain. Setiap usulan balance harus condong ke arah mudah, bukan hardcore.

2. **`ClearedRowGravity()` sengaja setengah-setengah.** Blok di bawah cincin yang hancur tetap diam (lubang dibiarkan terjebak), blok di atas cincin jatuh merapat. Ini memang aturan Tetris klasik yang benar.  
   Owner sudah pernah mencoba cascade penuh (semua blok jatuh) dan hasilnya **terlalu mudah** — muncul reaksi berantai yang menghancurkan 3–4 baris sekaligus tanpa direncanakan pemain. **Jangan kembalikan ke cascade penuh.**

3. **Wipe papan di `StageUp()` boleh tetap ada.** Awalnya ini dianggap masalah, tapi karena game-nya endless dan sengaja mudah, wipe itu justru katup relief yang memungkinkan run panjang. Migrasi papan lintas-babak diturunkan jadi **opsional / nanti** (lihat Lampiran A).

4. **`maxColumns = 30` terlalu lebar.** Sudah diturunkan ke 24 — lihat Bagian 2 dan 0.5.

---

## 0.5 STATUS EKSEKUSI (per 4 September 2026)

### LANGKAH MANUAL YANG BELUM DIKONFIRMASI — BACA INI DULU

Tiga tunable di bawah sudah diubah di kode, **tapi kemungkinan besar belum aktif di game.** Semua tunable di `Tetris3D.cs` adalah field `public` pada MonoBehaviour, sehingga Unity **menserialisasi nilainya ke dalam `SampleScene`** pada GameObject `Game`. **Nilai yang tersimpan di scene menang atas nilai default di C#.** Mengubah angka di kode saja tidak berefek apa-apa pada scene yang sudah ada.

Buka `SampleScene` -> pilih GameObject **`Game`** -> ubah di Inspector -> **Ctrl+S**:

| Header di Inspector | Field | Nilai baru |
|---|---|---|
| Skor & level | Cell Points | **12** |
| Bentuk tabung | Max Columns | **24** |
| Bentuk tabung | Columns Per Stage | **2** |

**JANGAN** pakai klik-kanan -> **Reset** pada komponen. Itu mengembalikan SELURUH field ke default dan menghapus setelan lain yang sudah dirapikan.

Field privat (`lastGarbageGaps`, `nextStone`, `pendingGarbage`, `runBaselineHi`) tidak diserialisasi, jadi F3, F5, F10, F11, F12, dan F13 **tidak butuh langkah Inspector** — begitu di-pull langsung aktif.

Jebakan yang sama berlaku untuk setiap usulan di Bagian 4 yang menyentuh field `public`.

### Perbaikan cepat — status

| # | Ringkas | File | Status | Commit |
|---|---|---|---|---|
| F1 | Guard `ctrlReady` untuk tombol saat clearing | `Part4.cs` | SELESAI | `47f1ac7` |
| F2 | Hapus rotasi acak di `SpawnPiece` | `Part2.cs` | SELESAI | `f2991bf` |
| F3 | Batu diundi di `PickNextType` + pratinjau abu-abu | `Part2.cs` + `Part4.cs` | SELESAI | `a863a9a` + `9971c22` |
| F4 | `RecalcLevel` `while` -> `if` (maks 1 level/event) | `Part2.cs` | SELESAI | `f2991bf` |
| F5 | Tombol TUTUP selalu tampil di layar profil | `Part4.cs` | SELESAI | `9971c22` |
| F6 | Watchdog 12 dtk untuk iklan revive | `Extras.cs` + `Part3.cs` | SELESAI | `a9f49ee` + `c586597` |
| F7 | `btnSoftDrop` dikonsumsi di awal `Update()` | `Part3.cs` | SELESAI | `c586597` |
| F8 | `sfx.pitch = 1f` di `ClearBoard()` | `Part2.cs` | SELESAI | `f2991bf` |
| F9 | Coroutine reward mati oleh `StopAllCoroutines()` | `Part2.cs` dll | **BELUM** | — |
| F10 | `ResolveClearsNoSpawn` -> `ClearedRowGravity` | `Gelembung2.cs` | SELESAI | `886e830` |
| F11 | `AddGarbageRow` ditunda saat balok masih aktif | `Part2.cs` | SELESAI | `a863a9a` |
| F12 | `highScore` tidak lagi ditulis tiap frame | `Part4.cs` | SELESAI | `877bcd3` |
| F13 | Celah baris sampah selaras ±1 kolom | `Part2.cs` | SELESAI | `0bcb13e` |

**F9 satu-satunya yang tersisa**, dan memang paling besar dari ke-13 — butuh memindahkan coroutine reward ke komponen terpisah supaya tidak ikut mati oleh `StopAllCoroutines()` milik `Tetris3D`.

**Catatan lintas-file.** F3 dan F6 masing-masing tersebar di dua file. Sempat terjadi hampir-celaka pada F6: `TickReviveAdWatchdog()` sudah ada di `Extras.cs` tapi belum ada yang memanggilnya, dan C# **tidak memberi peringatan** untuk method privat yang tak terpakai — jadi perbaikannya diam-diam tidak aktif. Kalau sebuah perbaikan menyentuh helper di satu file dan pemanggilnya di file lain, **dorong keduanya sebelum menyatakan selesai.**

### Tuning yang sudah diterapkan

| Field | Lama | Baru | Commit |
|---|---|---|---|
| `maxColumns` | 30 | **24** | `05b8a3f` |
| `columnsPerStage` | 3 | **2** | `05b8a3f` |
| `cellPoints` | 10 | **12** | `adf4a07` |

Sisa Bagian 4 belum dikerjakan.

Perbaikan lain di luar daftar F: **`Tetris3D.AdLoading.cs`** tadinya tidak bisa dikompilasi sama sekali (30 error, seluruh proyek masuk Safe Mode). Sudah diperbaiki — lihat Lampiran B dan `SETUP-TROUBLESHOOTING.md`.

---

### KOREKSI 1 — pesan commit `05b8a3fa` SALAH

Pesan commit `05b8a3fa` menulis *"level 17 lewat kolom, bukan level 13"* untuk bentuk aneh. **Itu keliru.** Pesan commit tidak bisa diubah setelah didorong, jadi koreksinya dicatat di sini.

Bentuk aneh dipicu oleh `weirdShapeColumns = 20` **atau** `weirdShapeLevel = 12`, mana pun yang tercapai lebih dulu.

| | Progresi kolom | `columns >= 20` di | `level >= 12` di | Bentuk aneh mulai |
|---|---|---|---|---|
| Lama (30/3) | 15 -> 18(L5) -> 21(L9) | **L9** | L12 | **L9** |
| Baru (24/2) | 15 -> 17(L5) -> 19(L9) -> 21(L13) | L13 | **L12** | **L12** |

Jadi bentuk aneh bergeser **L9 -> L12**, yaitu **3 level lebih lambat** — bukan 8 level seperti yang tersirat di pesan commit. Kodenya sendiri benar; hanya narasinya yang salah.

Konsekuensi yang penting untuk pekerjaan berikutnya: setelah tuning ini **`weirdShapeLevel` yang jadi pengikat**, bukan lagi `weirdShapeColumns`. Kalau ingin bentuk aneh datang lebih lambat, naikkan `weirdShapeLevel` 12 -> 14. Mengutak-atik `weirdShapeColumns` sekarang tidak akan terasa. (Keduanya field `public` — butuh langkah Inspector.)

### KOREKSI 2 — cakupan F10 jauh lebih sempit dari yang ditulis semula

Teks F10 di Bagian 3 menyuruh mengganti `CascadeGravity()` di `BombBlast` **dan** `HammerBlast`. Setelah kodenya benar-benar dibaca, itu **salah, dan akan merusak game kalau diterapkan mentah-mentah**:

- **`BombBlast` wajib tetap memakai `CascadeGravity()`.** Bom menghancurkan sekitar 50% sel secara acak di seluruh papan, jadi lubangnya tersebar ke mana-mana. `ClearedRowGravity()` hanya mengerti satu batas baris; kalau dipakai di sini, blok akan **menggantung di udara**.
- **`HammerBlast` tidak ada bedanya.** Palu selalu menghabisi baris 0 dan 1, sehingga `landBase` selalu 0 dan kedua fungsi memberi hasil yang identik. Menggantinya sia-sia.
- **Yang benar-benar tidak konsisten adalah `ResolveClearsNoSpawn()`** — jalur clear beruntun akibat item. Hanya satu baris di situ yang diubah.

Pelajarannya: catatan hasil audit bisa terlalu kasar. **Baca kodenya lagi sebelum mempercayai catatan lama.**

### Catatan F12 — ada harganya

Rekor sekarang **hanya ditulis saat game over**. Kalau aplikasi ditutup paksa di tengah run yang sedang memecahkan rekor, rekor itu hilang. Ini harga yang dibayar untuk mengembalikan ketegangan "kejar rekormu" — dulu chip BEST di HUD selalu sama persis dengan skor berjalan, jadi rekor tidak pernah terasa dikejar. Mudah dibalik kalau ternyata tidak disukai.

### Catatan F3 — keputusan implementasi

Undian batu ditaruh sebagai **efek samping di dalam `PickNextType()`**, bukan lewat helper `RollNext()` yang baru. Alasannya `PickNextType()` sudah dipanggil dari `Start()`, `SpawnPiece()`, `RetryGame()`, dan `GoHome()`, sehingga keempat jalur otomatis memperbarui `nextStone` tanpa perlu menyentuh `Tetris3D.cs`. Efek samping ini ditulis di komentar tepat di atas method-nya.

**Urutan di `SpawnPiece()` kritis:** `curStone = nextStone;` harus dijalankan **sebelum** `nextType = PickNextType();`, karena `PickNextType()` langsung menimpa `nextStone` dengan undian berikutnya.

### Catatan F6 — belum bisa diuji

Watchdog revive tidak bisa diuji selama define `KUBIKA_ADMOB` mati, karena jalur gagal langsung dipanggil seketika tanpa menunggu. Uji setelah iklan dihidupkan lagi pasca-review Play Store.

### Yang belum diverifikasi

Semua perubahan didorong lewat GitHub API, yang mengharuskan **menulis ulang isi file secara penuh** setiap kali. Ukuran byte tiap file sudah dicek dan selisihnya wajar, tapi **belum ada satu pun yang benar-benar dikompilasi**. Setelah `git pull`, buka Unity dan periksa Console sebelum melanjutkan pekerjaan lain.

Ini bukan kekhawatiran teoretis: bug asli di `AdLoading.cs` yang membuat proyek masuk Safe Mode persis lahir dari penulisan ulang file yang escaping-nya rusak.

---

## 1. URGENT — bukan gameplay, tapi harus ditangani lebih dulu

### 1.1 Keystore ter-commit di repo publik

**BELUM DIKERJAKAN.** Butuh perintah git lokal, tidak bisa lewat API.

`Keystore/kubika-upload.keystore` (2.696 byte) ada di dalam repo, dan repo ini **publik**. Itu upload key Play Store dan bisa diunduh siapa saja. Kalau password-nya juga tertulis di `CODEMAGIC_SETUP.md`, keystore harus dianggap bocor total.

Langkah:
- Hapus dari **histori git**, bukan cuma `git rm` (butuh `git filter-repo` atau BFG)
- Tambahkan `Keystore/` dan `*.keystore` ke `.gitignore`
- Ajukan **upload key reset** di Google Play Console
- Pindahkan keystore + password ke environment variable / encrypted secret di Codemagic

### 1.2 Folder sampah build ikut ter-commit

Tambahkan ke `.gitignore`:

```
Keystore/
*.keystore
*_BurstDebugInformation_DoNotShip/
*_BackUpThisFolder_ButDontShipItWithYourGame/
```

---

## 2. TUNING DIAMETER (permintaan utama) — SUDAH DITERAPKAN (Opsi A)

### 2.1 Kenapa 30 terasa terlalu lebar

Bukan soal ukuran blok. Dari `ApplyGeometry()`:

```
radius = baseRadius * columns / startColumns;
arc    = 2 * PI * radius / columns;
```

Disubstitusikan, `columns` saling menghilang:

```
arc = 2 * PI * baseRadius / startColumns   ->  KONSTAN
```

Jadi `arc`, `vSpace`, dan `blockScale` **tidak pernah berubah** di semua babak. Blok berukuran sama persis di 15 kolom maupun 30 kolom. Yang berubah hanya sudut tiap sel dan radius; kamera mundur karena `dist` mengandung `radius * 2.2f`.

Masalah 30 kolom yang sebenarnya:
- Pemain harus melacak **30 slot diskrit** dengan wrap-around, secara mental
- Kapan pun hanya sekitar 39% lingkaran (~140 derajat) yang terbaca di layar; sisanya di belakang tabung
- Satu cincin butuh ~8 balok ditempatkan tanpa menyisakan lubang, melintasi batas sambungan
- Kamera paling jauh (`dist` ~41,1) sehingga blok tampak paling kecil

### 2.2 Level terjadinya stage-up

Dari `OnLevelUp()`: `stageLevel = ((level - 1) % levelsPerStage) == 0` dengan `levelsPerStage = 4`.  
Jadi stage-up terjadi di **level 5, 9, 13, 17, 21**.

### 2.3 Opsi setelan

| Opsi | maxColumns | columnsPerStage | Progresi kolom | Diameter maks tercapai | radius maks | dist kamera |
|---|---|---|---|---|---|---|
| **A — DIPILIH & DITERAPKAN** | **24** | **2** | 15 → 17 → 19 → 21 → 23 → 24 | level 21 | 5,44 | 38,1 |
| B — paling lega | 21 | 2 | 15 → 17 → 19 → 21 | level 13 | 4,76 | 36,6 |
| C — perubahan minimal | 27 | 3 | 15 → 18 → 21 → 24 → 27 | level 17 | 6,12 | 39,6 |
| lama | 30 | 3 | 15 → 18 → 21 → 24 → 27 → 30 | level 21 | 6,80 | 41,1 |

**Kenapa Opsi A:**
- Lebar maksimum turun 20% (30 → 24 slot), jauh lebih mudah dilacak
- `columnsPerStage = 2` menjaga **jumlah stage-up tetap 5** dan diameter maks tetap tercapai di **level 21**, sama seperti sebelumnya. Jadi pacing progresi tidak berubah sama sekali — hanya titik akhirnya lebih sempit
- Kamera 7% lebih dekat, blok 7% lebih besar di layar
- `maxDiameterLevel` tetap 21, sehingga eskalasi endgame (`EffectiveStoneChance`, baris sampah ganda) berjalan di level yang sama seperti sebelumnya

Opsi B lebih lega tapi memicu endgame di level 13 — untuk game yang sengaja mudah itu terlalu cepat.

### 2.4 Efek samping

**Skor per cincin turun.** Rumusnya `columns * cellPoints * rowMult * combo`. Cincin tunggal: 30 x 10 = 300 menjadi 24 x 10 = 240 (turun 20%).

Sudah dikompensasi dengan **`cellPoints` 10 -> 12**, sehingga 24 x 12 = 288 (mendekati 300) dan skala skor lama di leaderboard `tetris3d_global` tetap kurang lebih sebanding.

**Efek ke bentuk aneh.** `weirdShapeColumns = 20` sekarang baru terlampaui di level 13 (saat kolom mencapai 21), sehingga `weirdShapeLevel = 12` yang memicu lebih dulu. Bentuk aneh bergeser dari **level 9 ke level 12**. Lihat KOREKSI 1 di Bagian 0.5 — pesan commit `05b8a3fa` salah menuliskan angka ini.

---

## 3. PERBAIKAN CEPAT — SELESAI kecuali F9

Semua di bawah ini perubahannya beberapa baris, risikonya rendah, dan tidak menyentuh arsitektur. Status ringkas ada di tabel Bagian 0.5.

### F1. SELESAI — CRASH: tombol aktif selagi baris meledak
**File:** `Assets/Tetris3D.Part4.cs` (`OnGUI`) · commit `47f1ac7`

`Update()` berhenti lebih awal saat `clearing == true`, tapi `OnGUI` tetap menggambar tombol ROTASI / JATUH / TURUN. Selama animasi clear (0,4 dtk + gravitasi 0,16 dtk per rantai, bisa lebih dari 2 dtk saat combo), `active == null`. Tap ROTASI memanggil `Rotate()` -> `RedrawActive()` -> `active.Length` -> **NullReferenceException**. Sama untuk JATUH -> `HardDrop()` -> `Move()`.

**Dikerjakan:** `bool ctrlReady = !clearing && active != null && !gameOver && !paused;` lalu `&& ctrlReady` ditambahkan sesudah tiap pemanggilan `Btn3D(...)`.

### F2. SELESAI — Preview NEXT berbohong
**File:** `Assets/Tetris3D.Part2.cs` (`SpawnPiece`) · commit `f2991bf`

```
int spins = Random.Range(0, 4);
for (int k = 0; k < spins; k++) ...
```

Balok diputar 0–3 kali secara acak **setelah** `nextType` ditentukan, sementara kotak preview menggambar bentuk dasar tanpa rotasi. Yang muncul tidak pernah dijamin sama dengan yang dilihat pemain.

Efek kedua, lebih penting: ini **membunuh seluruh sistem assist**. `PickNextType()` -> `FindFittingShape()` bekerja keras mencari bentuk **beserta rotasinya** yang muat rapi, lalu rotasinya diacak lagi di sini. Jadi `assistStart = 0.85` praktis tidak berefek, dan ongkos CPU-nya terbuang.

**Dikerjakan:** blok rotasi acak dihapus. Preview jadi jujur DAN assist jadi berfungsi.

### F3. SELESAI — Balok BATU tidak diumumkan
**File:** `Assets/Tetris3D.Part2.cs` + `Part4.cs` · commit `a863a9a` + `9971c22`

`curStone` diundi di dalam `SpawnPiece()`, jadi preview tidak pernah bisa menunjukkan bahwa balok berikutnya adalah batu (tidak bisa diputar). Mulai level 18 dengan peluang naik sampai 45%, pemain dapat balok yang tidak bisa diputar tanpa peringatan.

**Dikerjakan:** field `nextStone` diundi di `PickNextType()`; kotak NEXT menggambar sel dengan `StoneColor()` dan chip judulnya ikut abu-abu. Lihat catatan implementasi di Bagian 0.5 — urutan baris di `SpawnPiece()` kritis.

### F4. SELESAI — Combo memicu banyak level sekaligus
**File:** `Assets/Tetris3D.Part2.cs` (`RecalcLevel`) · commit `f2991bf`

```
while (score >= nextLevelScore && guard++ < 100)
```

Rantai 4 cincin dengan combo x4 di 24 kolom bisa memberi ribuan poin dalam satu event -> naik 3–4 level sekaligus -> `OnLevelUp()` jalan 3–4 kali -> plafon turun 3 baris + 3 baris sampah, atau `StageUp()` berkali-kali. **Pemain dihukum karena bermain bagus.**

**Dikerjakan:** `while` diganti `if`, maksimal 1 level per event. Sisa skor tetap terhitung untuk level berikutnya karena `nextLevelScore` bersifat akumulatif.

### F5. SELESAI — Pemain terkunci di layar Buat Profil
**File:** `Assets/Tetris3D.Part4.cs` (`DrawProfileScreen`) · commit `9971c22`

Tombol TUTUP hanya muncul kalau `editingProfile == true`. Saat game over pertama, `showProfile = true` dengan `editingProfile = false`, sehingga **tidak ada jalan keluar tanpa mengisi nama**. Dan karena `OnGUI` `return` di titik itu, animasi count-up skor & layar Game Over pertama tidak pernah terlihat. Sesi pertama pemain berakhir dengan formulir, bukan perayaan.

**Dikerjakan:** guard `editingProfile &&` dihapus, tombol TUTUP selalu digambar. Aman karena `gameOverHandled` sudah `true` di titik itu, jadi menutup layar profil jatuh ke layar GAME OVER normal dan profil masih bisa dibuat lain kali lewat chip di menu.

**Belum dikerjakan (opsional):** menunda layar profil sampai animasi skor selesai.

### F6. SELESAI — Layar revive bisa menggantung selamanya
**File:** `Assets/Tetris3D.Extras.cs` + `Part3.cs` · commit `a9f49ee` + `c586597`

`reviveTimer = 9999f`. Kalau callback SDK iklan tidak pernah datang (kasus tepi AdMob yang nyata), pemain terkunci di layar revive tanpa jalan keluar.

**Dikerjakan:** `TickReviveAdWatchdog()` dengan `REVIVE_AD_TIMEOUT = 12f`, dipanggil dari `Part3.Update()` di dalam `if (reviveOffer)`. Memakai `Time.unscaledDeltaTime`, bukan `deltaTime`, karena iklan sering menyetel `timeScale = 0`. Setelah timeout jatuh ke `OnReviveAdUnavailable()`.

### F7. SELESAI — `btnSoftDrop` tidak dibersihkan di jalur early-return
**File:** `Assets/Tetris3D.Part3.cs` (`Update`) · commit `c586597`

Di-set di `OnGUI`, direset di baris **terakhir** `Update()`. Semua `return` lebih awal (clearing / paused / gameOver / revive) melewatkan reset, sehingga begitu clearing selesai balok langsung menyelonong turun cepat.

**Dikerjakan:** flag dikonsumsi di awal `Update()` (`bool softDropHeld = btnSoftDrop; btnSoftDrop = false;`) dan reset di baris terakhir dihapus. Urutan frame-nya Update -> render -> OnGUI, jadi `OnGUI` memang menyetel flag untuk `Update` frame berikutnya.

### F8. SELESAI — `sfx.pitch` bocor -> SELURUH SFX jadi sumbang permanen
**File:** `Assets/Tetris3D.Part2.cs` (`ClearBoard`) · commit `f2991bf`

`ClearBoard()` memanggil `StopAllCoroutines()`. Kalau coroutine yang sedang memodifikasi `sfx.pitch` (`KbSfxAt`, `CoChaChing`, `BombBlast`, `HammerBlast`) terbunuh sebelum meresetnya, pitch tertinggal di nilai non-1 dan **semua SFX jadi sumbang sampai aplikasi direstart**.

**Dikerjakan:** `if (sfx != null) sfx.pitch = 1f;` di `ClearBoard()`.

**Jangka panjang (belum):** pakai `AudioSource` terpisah untuk SFX ber-pitch, atau `PlayOneShot` tanpa memutasi pitch global.

### F9. BELUM — Iklan sudah ditonton tapi Koin tidak masuk
**File:** `Assets/Tetris3D.Part2.cs` (`ClearBoard`), `PetiKoin.cs` (`CoAfterPetiAd`), `Gelembung2.cs`

`StopAllCoroutines()` di `ClearBoard()` juga membunuh `CoAfterPetiAd()` / `CoAfterBubbleDrop()`, sehingga refresh Koin dari server tidak pernah terjadi. **Ini keluhan monetisasi serius** — pemain menonton iklan dan tidak mendapat apa-apa.

**Rencana:** jalankan coroutine terkait reward di GameObject/komponen terpisah yang tidak ikut terkena `StopAllCoroutines()` milik `Tetris3D`, atau ganti `StopAllCoroutines()` dengan penghentian selektif berdasarkan handle coroutine.

### F10. SELESAI (cakupan diperbaiki) — gravitasi item tidak konsisten
**File:** `Assets/Tetris3D.Gelembung2.cs` (`ResolveClearsNoSpawn`) · commit `886e830`

Line clear normal memakai `ClearedRowGravity()` (lubang menetap — desain owner). Tapi clear beruntun akibat item memakai `CascadeGravity()` (kompaksi penuh per kolom) — **persis mekanik yang owner buang karena terlalu mudah**. Cascade memicu clear berantai yang masuk ke `ResolveClearsNoSpawn()` dan memberi skor, baris, dan combo penuh.

**Dikerjakan:** satu baris di `ResolveClearsNoSpawn()`, `CascadeGravity()` -> `ClearedRowGravity(full)`.

**PENTING:** `BombBlast` dan `HammerBlast` sengaja **tidak** diubah. Lihat KOREKSI 2 di Bagian 0.5 untuk alasannya — mengubahnya akan membuat blok menggantung di udara.

### F11. SELESAI — `AddGarbageRow()` bisa menabrak balok aktif
**File:** `Assets/Tetris3D.Part2.cs` (`AddGarbageRow`, `SpawnPiece`) · commit `a863a9a`

Dari `ResolveBoard()` aman (`active` sudah null). Tapi dari **`ResolveClearsNoSpawn()`** (jalur Bom/Palu) `active` masih hidup — pemain memakai item saat balok sedang jatuh. Tumpukan digeser ke atas menembus balok aktif, semua `Valid()` gagal, balok langsung lock dan menimpa grid. **Papan rusak.**

**Dikerjakan:** `AddGarbageRow()` diawali `if (active != null) { pendingGarbage++; return; }`, dan antrean dikuras di awal `SpawnPiece()`. Pemeriksaan `TooHigh()` ikut dipindah ke `SpawnPiece()` supaya urutan cek kalah tetap sama seperti sebelumnya (dulu selalu dicek SESUDAH baris sampah ditambahkan).

`pendingGarbage` tidak perlu direset di `StageUp()`, karena `AddGarbageRow` hanya jalan saat `columns >= maxColumns` — dan di titik itu `StageUp()` tidak mungkin jalan lagi.

### F12. SELESAI — `highScore` di-update live saat bermain
**File:** `Assets/Tetris3D.Part4.cs` (`OnGUI`) · commit `877bcd3`

`if (score > highScore)` jalan setiap pass `OnGUI` (Layout + Repaint, beberapa kali per frame), termasuk `PlayerPrefs.SetInt`. Akibatnya chip BEST di HUD selalu sama dengan skor sekarang, sehingga **ketegangan "kejar rekormu" hilang total**. Ditambah penulisan `PlayerPrefs` tiap frame.

**Dikerjakan:** blok update live dihapus; blok game over jadi satu-satunya penulis `highScore`. HUD memakai `int hudHi = gameOver ? runBaselineHi : highScore;`. Ada konsekuensinya — lihat catatan di Bagian 0.5.

### F13. SELESAI — Celah baris sampah tidak selaras antar baris
**File:** `Assets/Tetris3D.Part2.cs` (`AddGarbageRow`) · commit `0bcb13e`

```
while (gaps.Count < gapN && guard++ < 500) gaps.Add(Random.Range(0, columns));
```

Posisi celah **diacak ulang dari nol setiap baris**. Dua baris sampah dengan celah di posisi berbeda praktis tidak bisa dibersihkan. Itu bukan tangga kesulitan, itu spiral kematian.

**Dikerjakan:** field `lastGarbageGaps` menyimpan posisi celah baris sebelumnya; baris berikutnya memakai posisi yang sama digeser maksimal 1 kolom (`Random.Range(-1, 2)` lalu `Wrap`). Hasilnya celah membentuk kanal diagonal yang bisa ditembus, bukan dinding papan catur. Ini standar di garbage Tetris kompetitif.

---

## 4. TABEL TUNING BALANCE (condong ke MUDAH, sesuai niat desain)

| Field | Sekarang | Usulan | Alasan |
|---|---|---|---|
| `maxColumns` | 30 | **24 — SUDAH** | 30 terlalu lebar; 20% lebih sedikit slot untuk dilacak |
| `columnsPerStage` | 3 | **2 — SUDAH** | Menjaga 5 stage-up & diameter maks tetap di level 21 |
| `cellPoints` | 10 | **12 — SUDAH** | Kompensasi skor akibat cincin lebih sempit (leaderboard tetap sebanding) |
| `garbageGapCount` | 2 | **4** | 2 celah di 24 kolom = spiral kematian; ~1 celah per 6 kolom |
| celah sampah | acak per baris | **selaras — SUDAH** | Lihat F13 |
| `RecalcLevel` | sampai 100 level/event | **maks 1 — SUDAH** | Lihat F4 |
| `minPlayHeight` | 11 | **12** | Pentomino setinggi 5 butuh ruang manuver |
| `stoneChance` cap | 0,45 | **0,30** | Balok tak bisa diputar; sudah dibantu F3 tapi 45% terlalu keras |
| `stoneStartLevel` | 18 | 18 (tetap) | Sudah pas |
| `assistMin` | 0,15 | **0,30** | Lantai bantuan dinaikkan; selaras dengan niat "agak mudah" |
| `assistStart` | 0,85 | 0,85 (tetap) | Baru benar-benar berfungsi setelah F2 |
| `comboSeconds` | 10 | 10 (tetap) | Combo longgar cocok untuk pengejar skor tertinggi |
| `baseLevelScore` | 600 | **800** | Level 2 sekarang cuma butuh 2 cincin; terlalu cepat |
| `levelStep` | 250 | **300** | Menyesuaikan `baseLevelScore` |
| `fallInterval` | 0,8 tetap | **0,8 -> 0,55 bertahap L1–20** | Sedikit ketegangan di akhir, tetap ramah |
| `weirdShapeLevel` | 12 | **14** | Pengikat baru setelah tuning diameter — lihat KOREKSI 1 |

**Catatan `fallInterval`:** sekarang konstan sepanjang game (tertulis eksplisit di header `Tetris3D.cs`). Tidak ada akselerasi sama sekali, dan hard drop selalu tersedia, jadi **nol tekanan waktu**. Semua kesulitan bersifat spasial. Untuk game yang sengaja mudah ini sebenarnya tidak fatal, tapi ramp ringan sampai 0,55 dtk akan memberi sensasi flow tanpa membuatnya sulit. Kalau ragu, ini item paling aman untuk ditunda.

**Ingat:** semua field di tabel ini `public`, jadi setiap perubahan butuh langkah Inspector — lihat Bagian 0.5.

---

## 5. ITEM & EKONOMI

### 5.1 Toko praktis tidak terjangkau

`PERMATA_PER_LINE = 5`, `TOKO_PRICE = { 600 bom, 400 palu, 200 slow }`.  
Satu Bom = sekitar **120 baris**. Gelembung Permata dari iklan memberi `GEM_BONUS = 50`, jadi **12 iklan per Bom**. Toko jadi hiasan; jalur nyata satu-satunya adalah iklan. Permata juga tidak punya sink lain (tidak ada skin, tema, atau continue).

**Usulan:**

| Field | Sekarang | Usulan |
|---|---|---|
| `PERMATA_PER_LINE` | 5 | **8** |
| `PERMATA_COMBO_BONUS` | 3 | **5** |
| `TOKO_PRICE` bom | 600 | **300** |
| `TOKO_PRICE` palu | 400 | **200** |
| `TOKO_PRICE` slow | 200 | **120** |

Hasilnya Bom jadi sekitar 38 baris — masih terasa sebagai pencapaian, tapi bisa dicapai murni dari bermain.

### 5.2 Bom terlalu kuat DAN tidak terbaca

Menghancurkan **50% seluruh blok secara acak**. Karena acak per-sel, hasilnya sering **lebih buruk**: papan penuh lubang satu-satuan yang mustahil ditutup. Item ini kadang jadi anti-item.

**Usulan:** ganti jadi bentuk spasial — radius di satu titik, atau pita 3 kolom penuh. Lebih terbaca, lebih memuaskan, lebih bisa di-balance.

**Catatan:** kalau bentuk Bom diubah jadi spasial dan lubangnya tidak lagi tersebar acak, barulah `ClearedRowGravity()` mungkin masuk akal di `BombBlast`. Selama masih acak per-sel, `CascadeGravity()` wajib dipertahankan (KOREKSI 2, Bagian 0.5).

### 5.3 Palu sering merugikan

`ApplyHammer()` selalu menghancurkan baris 0 dan 1. Di silinder, baris terbawah justru yang paling penuh dan paling dekat selesai — jadi Palu sering **membuang cincin yang tinggal 1 blok lagi**.

**Usulan:** targetkan 2 baris dengan **lubang terbanyak**, bukan 2 baris terbawah.

**Catatan:** kalau ini dikerjakan, `landBase` tidak lagi selalu 0, sehingga pilihan gravitasi di `HammerBlast` **jadi berarti** dan harus ditinjau ulang.

### 5.4 Frekuensi iklan terlalu agresif

Gelembung buff tiap 24–40 dtk, gelembung Koin tiap 90 dtk, plus iklan revive, plus Peti Koin. Rata-rata ada tawaran iklan tiap ~30 detik.

Diperburuk oleh `PickBubbleType()`: selama `BUFF_AD_COOLDOWN` (180 dtk) berjalan, **semua** gelembung dipaksa jadi `IT_GEM` — yang juga butuh iklan. Jadi selama 3 menit pemain hanya disodori prompt iklan tanpa variasi hadiah.

**Usulan:** selama cooldown, jangan spawn gelembung sama sekali, atau spawn hadiah kecil yang **gratis** (tanpa iklan). Naikkan `BUBBLE_MIN_GAP` ke 40 dtk.

**MENDESAK untuk build review.** Selama `KUBIKA_ADMOB` mati, `BubbleTick()` tetap memunculkan gelembung tapi tidak ada iklan yang bisa diputar — reviewer Play Store melihat tawaran "Tonton Iklan" yang tidak bisa diklaim tiap ~30 detik. Butuh flag `adsEnabled` yang diperiksa di `BubbleTick()`, `PetiKoinBtn()`, dan tawaran revive. Lihat `SETUP-TROUBLESHOOTING.md` Masalah 3.

### 5.5 Gelembung mencuri tap & membekukan game

`KubikaBubbleHUD` memakai `GUI.depth = -800` (di depan segalanya) dan gelembung melintasi area slot inventaris buff (kiri-tengah) serta panel NEXT (kanan). Salah tap saat menggeser tabung membuka modal iklan mendadak di tengah drop. `OpenBubbleClaim` juga menyetel `Time.timeScale = 0f`, dan `kbClaimOpen` tidak memeriksa `paused` sehingga menu Jeda bisa tergambar bertumpuk.

**Usulan:** batasi area spawn gelembung ke zona aman (atas-tengah), dan jangan pakai `Time.timeScale = 0` — cukup hentikan `fallTimer`.

### 5.6 Item = beli skor pakai iklan

`ResolveClearsNoSpawn()` memberi `score`, `lines`, dan `comboCount` penuh dari clear hasil item. Jadi Bom/Palu hasil iklan langsung mengonversi iklan menjadi poin leaderboard global.

**Usulan:** clear dari item tidak menambah `score` (atau hanya 25%), dan tidak menaikkan `comboCount`. Tetap menambah `lines` dan Permata.

---

## 6. GAME FEEL (dampak terbesar ke "rasa memuaskan")

Diurutkan berdasarkan rasio dampak terhadap usaha.

1. **Lock delay** — belum ada sama sekali. Begitu balok tidak bisa turun di tick berikutnya, langsung terkunci. Tidak ada jendela penyesuaian terakhir. Standar modern: ~0,5 dtk, reset saat digerakkan, maksimal ~15 reset. Tanpa ini, kontrol swipe yang analog terasa menghukum. **Ini item nomor satu.**

2. **Wall kick** — `Rotate()` mencoba satu orientasi; kalau tertutup, gagal **diam-diam**: tanpa suara, tanpa getar, tanpa apa pun. Pemain menekan tombol dan tidak terjadi apa-apa. Minimal butuh kick +/-1 kolom dan +/-1 baris, plus SFX/haptic saat gagal.

3. **Hold piece** — belum ada. Di game dengan pentomino dan balok batu, hold adalah katup pengaman yang menghilangkan sebagian besar frustrasi. Sangat selaras dengan niat "agak mudah".

4. **7-bag randomizer** — sekarang murni `Random.Range`, sehingga terjadi kekeringan bentuk. Di silinder yang butuh 24 kolom terisi penuh, kekeringan bentuk sangat mematikan.

5. **Danger state** — tidak ada apa pun saat tumpukan mendekati `killLine`: musik tidak berubah, tidak ada denyut, `killRingTf` hanya silinder merah statis alpha 0,35. **Ketegangan adalah setengah dari kesenangan Tetris.**

6. **Juice hard drop** — `while (Move(0,-1)) {}` lalu satu nada; balok teleport. Yang hilang: garis jejak di jalur jatuh, debu benturan, squash-and-stretch saat mendarat, dan **freeze-frame ~40 ms** (trik termurah & paling ampuh untuk kesan berbobot).

7. **Tidak ada bonus skor soft drop / hard drop** — menghilangkan insentif bermain agresif.

8. **Tidak ada teks "Single / Double / Triple"** dan tidak ada perayaan perfect clear.

---

## 7. AUDIO

1. **Musik loop 16 nada (~3,5 dtk), sinus murni + bass, tanpa perkusi, `loop = true` selamanya.** Akan menyiksa dalam 2 menit. **Prioritas nomor satu di audio.** Butuh loop lebih panjang (minimal 30 dtk) dan idealnya sampel asli, bukan sintesis runtime.

2. **Combo tidak punya pitch-ladder.** `sfxClear` selalu nada yang sama persis, jadi combo x7 terdengar identik dengan combo x1. Naikkan 1 semitone per tingkat combo — **ini kesempatan paling besar yang terlewat**, dan ongkosnya hampir nol.

3. **Satu `AudioSource` dipakai bersama dengan `pitch` yang dimutasi global.** Lihat F8. `KbSfxAt()` mengubah `sfx.pitch` dan memengaruhi SFX lain yang sedang berbunyi. F8 hanya menambal gejalanya di `ClearBoard()`, akar masalahnya masih ada.

4. **Penumpukan audio di momen terbaik.** Combo besar = `sfxClear` + `CurPlayChaChingSoft()` hingga 24 kali (masing-masing dua bunyi berjarak 0,07 dtk) + `sfxLevelUp` + tick Perlambat. Hasilnya bubur. Tidak ada ducking, tidak ada batas jumlah suara bersamaan.

5. **`StageUp()` memakai `Sfx(sfxClear)`** — momen paling besar di game memakai suara yang paling sering terdengar. Butuh fanfare sendiri.

6. **SFX yang belum ada:** soft drop, balok menyentuh permukaan (beda dari lock), rotasi gagal, baris sampah naik, peringatan bahaya.

7. **Tick Perlambat berbunyi tiap detik selama 8 detik** sambil border layar berkedip. Item bantuan tapi terasa seperti alarm.

---

## 8. VISUAL

1. **Palet warna saling tabrakan.** `BlockColor(type)` = `HSV(type * 0.11)`. Dengan 17 tipe, hue melewati 1,0 dan **membungkus**: tipe 0 (hue 0,00) dan tipe 9 (hue 0,99) praktis warna yang sama. Pemain tidak bisa membedakan bentuk lewat warna. **Perbaikan:** pakai `palette[]` yang sudah ada (12 warna terpilih) alih-alih HSV generatif, atau kurangi pengali hue ke ~0,055 agar 17 tipe tersebar dalam satu putaran penuh.

2. **Bloom membuat papan sulit dibaca.** `emissionStrength = 0.75` di **setiap** blok, `bloomIntensity = 1`, `threshold = 0.9`. Seluruh menara berpendar merata sehingga topologi permukaan — hal terpenting yang harus dibaca pemain — jadi kabur. **Usulan:** `emissionStrength` 0,75 -> 0,45, `bloomThreshold` 0,9 -> 1,1.

3. **Ghost piece lemah.** Kubus alpha 0,22, unlit, `ZWrite = 0` (sorting tidak stabil), dan warnanya sama dengan blok sehingga menyatu dengan tumpukan. **Usulan:** ubah jadi outline/wireframe, atau beri warna putih tetap dengan alpha lebih tinggi.

4. **`killRingTf` adalah silinder padat setinggi 0,05**, bukan cincin — ia menutupi isi tabung di belakangnya. **Usulan:** torus tipis atau garis shader yang berdenyut saat bahaya.

5. **"COMBO xN" digambar font size 100+ melintang di tengah layar selama 1,3 dtk** — persis menutupi menara di momen pemain paling perlu melihat papan. **Usulan:** pindah ke sisi atas, perkecil, perpendek jadi 0,8 dtk.

6. **Animasi permata di-reset saat combo beruntun.** `SpawnGemBurst()` mengembalikan **semua** permata lama ke fase 0, sehingga permata yang hampir sampai chip ditarik balik ke bawah. Di momen paling seru, animasinya justru terlihat kacau. **Usulan:** jangan reset yang sedang berjalan; antrekan burst baru secara independen. Perpendek `HOLD_DUR` 0,25 -> 0,12.

7. **Kebocoran resource.** `MakeGradientTex()` bikin `Texture2D` baru tiap `StageUp` tanpa menghapus yang lama. `MakeBlock()` / `MakeGhostBlock()` / `AddGarbageRow()` bikin **Material baru per kubus** — di 24 x 18 itu ratusan material unik, nol batching, plus churn GC tiap clear. **Usulan:** cache satu material per tipe warna (17 + batu + garbage + ghost) dan pakai `sharedMaterial`. Untuk `FlashClear` yang butuh emisi per-blok, pakai `MaterialPropertyBlock`.

**Pratinjau NEXT (baru, dari F3):** kotak NEXT sekarang menggambar balok batu dengan `StoneColor()` (abu-abu) dan chip judulnya ikut abu-abu. Kalau `palette[]` jadi dipakai di poin 1, pastikan warna batu tetap jelas berbeda dari 17 warna blok biasa.

---

## 9. PERFORMA (penyebab stutter di Android kelas menengah)

Stutter merusak "rasa memuaskan" lebih dari apa pun, jadi ini bukan item kosmetik.

1. **`GuiText()` menggambar 9 `GUI.Label` per string** (8 outline + 1 isi) dan mengalokasikan `GUIStyle` baru setiap panggilan. `OnGUI` jalan beberapa kali per frame di **empat** MonoBehaviour terpisah (`Tetris3D`, `KubikaCurrencyHUD`, `KubikaBubbleHUD`, `KubikaTokoHUD`). Ini sampah GC tiap frame plus ratusan draw call. **Usulan:** cache `GUIStyle` sebagai field statis; kurangi outline dari 8 arah jadi 4.

2. **`FindFittingShape()` bisa ~180 ribu operasi per spawn** (17 bentuk x 4 rotasi x 24 kolom x `DropRowFor` 18 baris), dijalankan tepat di tengah `ResolveBoard()`. Hitch persis di momen transisi. **Usulan:** batasi jumlah bentuk yang dicoba (misal 5 pertama setelah shuffle), atau hitung profil ketinggian kolom sekali lalu pakai untuk semua kandidat.

   **Catatan pasca-F2:** biaya ini sekarang **benar-benar terpakai** (dulu hasilnya dibuang oleh rotasi acak), jadi optimasinya jadi lebih berharga, bukan kurang.

3. **`Burst()` membuat GameObject + ParticleSystem baru per sel yang hancur** — di 24 kolom itu 24 particle system per cincin. **Usulan:** satu particle system persisten, panggil `Emit()` dengan posisi berbeda.

4. **Tiga HUD komponen memanggil `FindFirstObjectByType<Tetris3D>()` setiap frame.** **Usulan:** cache referensinya sekali.

5. **`CurrencyTick()` didorong dari `KubikaCurrencyHUD.Update()`** — reward permata bergantung pada komponen eksternal. Kalau komponen itu hilang, tidak ada permata yang diberikan. **Usulan:** pindahkan ke `Tetris3D.Update()`.

---

## 10. LEADERBOARD

1. **Metrik tidak konsisten.** `SubmitScore()` mengirim `score` (run ini), `PushName()` mengirim `highScore`. Kalau leaderboard UGS di-set mode "latest" dan bukan "best", satu run jelek akan **menurunkan** peringkat pemain. **Perbaikan:** selalu kirim `highScore`, dan pastikan mode leaderboard = best score.

   **Catatan pasca-F12:** `highScore` sekarang baru diperbarui saat game over. Urutannya di blok game over perlu dicek — pastikan `SubmitScore()` jalan **sesudah** `highScore` di-commit, bukan sebelum.

2. **`LoadRanks` mengambil `Limit = 50`** tapi label menu berkata "Top 10" / "Top 5". Samakan.

3. **`ParseCountry`** melakukan parsing JSON metadata dengan pencarian indeks string secara naif. Rapuh; pakai parser JSON yang benar.

4. Lihat juga 5.6 — skor dari item merusak integritas peringkat.

---

## 11. URUTAN EKSEKUSI YANG DISARANKAN

**Batch 1 — keamanan (di luar kode game)** — BELUM
- 1.1 keystore, 1.2 gitignore. Butuh perintah git lokal.

**Batch 2 — perbaikan cepat** — SELESAI kecuali F9
- F1, F2, F3, F4, F5, F6, F7, F8, F10, F11, F12, F13 sudah didorong. Lihat tabel Bagian 0.5.
- F9 (coroutine reward) tersisa — paling besar dari ke-13.

**Batch 2b — build review (BARU, mendesak)** — BELUM
- Flag `adsEnabled` supaya build tanpa AdMob tidak menampilkan tawaran iklan yang tidak bisa diklaim. Lihat 5.4.

**Batch 3 — tuning angka (Bagian 2 + Bagian 4)** — SEBAGIAN
- `maxColumns`, `columnsPerStage`, `cellPoints` sudah diubah di kode. **Langkah Inspector belum dikonfirmasi** — lihat Bagian 0.5.
- Sisanya belum. **Uji dalam satu sesi bermain penuh** karena saling berinteraksi.

**Batch 4 — item & ekonomi (Bagian 5)**

**Batch 5 — game feel (Bagian 6) + audio (Bagian 7)**
- Lock delay, wall kick, hold piece, danger state. Ini pekerjaan fitur, bukan perbaikan

**Batch 6 — visual (Bagian 8) + performa (Bagian 9) + leaderboard (Bagian 10)**

---

## LAMPIRAN A — Migrasi papan lintas-babak (DITUNDA, opsional)

Disimpan untuk referensi. **Bukan prioritas** karena wipe papan selaras dengan niat desain endless-mudah (lihat Bagian 0 poin 3).

Kalau nanti ingin papan bertahan saat diameter membesar, ini yang perlu diketahui:

**Kenapa versi naif crash.** `StageUp()` melakukan `DestroyBoardObjects()` lalu menaikkan `columns` lalu `AllocGrid()`. Kalau dua langkah pembersihan itu dihapus, `columns` jadi 17 sementara `grid` masih `int[15, 18]`. `Wrap(c)` mengembalikan 0–16, lalu `grid[c, r]` dengan `c >= 15` melempar **`IndexOutOfRangeException`** di frame berikutnya. Loop `c < columns` di `Valid()`, `FindFullRows()`, `TooHigh()`, dan `CascadeGravity()` semuanya terkena.

**Kabar baiknya.** Karena `arc` konstan (lihat 2.1), `vSpace` dan `blockScale` tidak berubah antar babak. Untuk setiap blok yang dipertahankan: baris `r` tetap, `localPosition.y` tetap, `localScale` tetap. Hanya sudut dan radius yang berubah, dan `PlaceObj(go, colBaru, r)` sudah menghitung keduanya secara otomatis. Tidak perlu rebuild mesh atau hitung ulang skala.

**Resep migrasi:**
1. Salin `grid` dan `cells` ke array sementara berukuran `columns` lama
2. Naikkan `columns`, panggil `AllocGrid()` (array baru & bersih — inilah yang mencegah out-of-range)
3. Petakan kolom lama -> kolom baru
4. Tulis balik, lalu panggil `PlaceObj()` per sel yang terisi
5. `ApplyGeometry()` + `ApplyStageColors()` seperti sekarang

**Langkah 3 adalah pertanyaan desain, bukan teknis.** Sebarkan celah baru secara merata (untuk 15 -> 17, dua celah di sekitar kolom 5 dan 11). Ini aman: `StageUp()` dipanggil dari `RecalcLevel()` yang jalan **setelah** loop `while` di `ResolveBoard()` selesai, jadi pada titik itu `FindFullRows()` dijamin kosong dan tidak ada cincin utuh yang jadi bolong.

**Jangan pakai pembulatan** `round(c * newCols / oldCols)` untuk pemetaan — `Mathf.RoundToInt` memakai banker's rounding, sehingga nilai `.5` bisa membulat ke slot yang sama dan dua kolom lama bertabrakan. Tentukan posisi celah secara eksplisit lalu geser kolom lama berurutan ke slot yang tersisa (dijamin injektif).

**Kebal crash permanen (opsional, 1 baris):** alokasikan `grid = new int[maxColumns, height]` sejak awal dan pakai `columns` hanya sebagai jumlah kolom aktif. Array tidak pernah dialokasi ulang, sehingga `IndexOutOfRangeException` tidak mungkin terjadi apa pun urutan eksekusinya. Ongkosnya 24 x 18 int = ~1,7 KB sekali. Ini juga menutup risiko lain: **`StageUp()` tidak memanggil `StopAllCoroutines()`**, jadi `CascadeGravity` / `AnimateFall` yang masih berjalan bisa memegang indeks kolom lama.

**Alternatif termurah tanpa migrasi:** tunda eksekusi `StageUp()` sampai papan memang sedang rendah (tumpukan tertinggi <= 2 baris). Kode `StageUp()` tidak berubah sedikit pun, nol risiko crash, tapi wipe-nya tidak lagi mengampuni apa pun.

**Bonus kalau papan dipertahankan:** karena `vSpace` dan `blockScale` konstan, radius dan sudut tiap blok bisa di-lerp selama ~0,6 dtk. Secara visual menara pemain **melebar di depan matanya**. `AnimateFall()` sudah punya polanya (kumpulkan movers, lerp, snap di akhir) — cukup versi yang me-lerp posisi penuh, bukan hanya `y`.

**Catatan pasca-F11:** kalau migrasi ini jadi dikerjakan, ingat `pendingGarbage` sekarang bisa berisi antrean baris sampah. Saat ini aman karena `AddGarbageRow` hanya jalan di `columns >= maxColumns` (titik di mana `StageUp` tidak mungkin jalan lagi), tapi asumsi itu ikut runtuh kalau aturan stage-up berubah.

---

## LAMPIRAN B — Peta file

| File | Isi utama |
|---|---|
| `Tetris3D.cs` | Field & tunable, `Start`, `AllocGrid`, `SetupScene`, `ApplyGeometry`, `ApplyStageColors`, `MakeMat`, `MakeBlock`, `CellLocalPos`, `Wrap`, `shapes`/`boxSize`/`shapeTier`, enum bahasa, field leaderboard |
| `Tetris3D.Part2.cs` | `SpawnPiece`, `PickNextType`, `AllowedShapes`, `FindFittingShape`, `Valid`, `Rotate`, `LockPiece`, `ResolveBoard`, `FlashClear`, `CascadeGravity`, `ClearedRowGravity`, `TooHigh`, `HardDrop`, `RecalcLevel`, `OnLevelUp`, `StageUp`, `AddGarbageRow`, `ClearBoard`, `Shake`, `Burst`; field `nextStone`, `pendingGarbage`, `lastGarbageGaps` |
| `Tetris3D.Part3.cs` | `SetupAudio`, `Sfx`, `MakeTone`/`MakeArp`/`MakeMusic`, `Update()`, UGS & leaderboard, lokalisasi (`InitLoc`) |
| `Tetris3D.Part4.cs` | `GuiText`, `Btn3D`, `DrawStartMenu`, `DrawPauseMenu`, `DrawProfileScreen`, `DrawRanksScreen`, `GetHudRow`, `OnGUI` |
| `Tetris3D.Extras.cs` | Haptic, revive (`RequestReviveByAd`, `TickReviveAdWatchdog`, `ClearTopRowsForRevive`), `DrawGameOverScore`, `DrawSlider`, `Toast` |
| `Tetris3D.Currency.cs` | Permata/Koin, HUD mata uang, animasi burst permata 3 fase, `KubikaCurrencyHUD` |
| `Tetris3D.Gelembung.cs` | Konstanta item, `BubbleTick`, `SpawnBubble`, `PickBubbleType`, ikon item |
| `Tetris3D.Gelembung2.cs` | Panel klaim, `ApplyBuff`, `ResolveClearsNoSpawn`, `ApplyBomb`/`BombBlast`, `ApplyHammer`/`HammerBlast`, `ApplySlow`, `KubikaBubbleHUD` |
| `Tetris3D.Toko.cs` | `TOKO_PRICE`, `DrawTokoShop`, `DrawBuffInv`, `UseBuffFromInv`, `KubikaTokoHUD` |
| `Tetris3D.PetiKoin.cs` | Peti koin berbasis iklan, `KubikaAds` |
| `Tetris3D.UiScale.cs` | `VW`, `VH`, `UiScale`, `ApplyUiScale` |
| `Tetris3D.RoundedBlock.cs` | `RoundedBlockMesh()` — satu-satunya file yang belum dibaca |
| `Tetris3D.Saldoku.cs` | Integrasi Saldoku |
| `Tetris3D.AdLoading.cs` | Loading iklan, `KubikaAdGate`, `KubikaCoinFlyHUD`, `KubikaPetiWatcher` |
| `Tetris3D.AdsReviveMrec.cs` | Iklan revive & MREC, `KubikaMrecDriver` |

### Catatan `AdLoading.cs` — bug yang pernah melumpuhkan proyek

File ini pernah berisi **30 error kompilasi** yang membuat seluruh proyek masuk Safe Mode. Penyebabnya bukan migrasi disk dan bukan AdMob yang dimatikan, melainkan **tanda kutip yang ter-escape berlebihan** (`\"teks\"` alih-alih `"teks"`) di 8 lokasi. Backslash ilegal di luar string -> `CS1056`, kutip yang menyusul membuka string tak tertutup -> `CS1010`, dan sisanya efek beruntun (`CS1525`, `CS1003`, `CS1002`, `CS1026`).

Sudah diperbaiki (23.648 -> 23.626 byte). **Ini persis risiko yang muncul tiap kali file ditulis ulang lewat API** — lihat peringatan di akhir Bagian 0.5.

### Status audit

Seluruh file di atas sudah dibaca kecuali **`RoundedBlock.cs`**. `Saldoku.cs`, `AdLoading.cs`, `AdsReviveMrec.cs`, dan `PetiKoin.cs` — yang dulu ditandai belum diaudit — sekarang sudah diperiksa, termasuk perilakunya saat define `KUBIKA_ADMOB` dimatikan. Semua jalur `#else`-nya aman; satu-satunya masalah nyata saat iklan mati adalah UX gelembung di poin 5.4.
