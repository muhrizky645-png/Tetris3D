# HANDOFF - BATCH M
## Gerbang Combo (Combo Gate) + Perisai Item + Langit-langit Nada Permata

| PEMBARUAN | Tanggal | Isi |
|---|---|---|
| v1.0 | 2026-09-08 | Dokumen dibuat. Batch M 1/4 s.d. 4/4 selesai dipush. |
| v1.1 | 2026-09-09 | **Bagian B DICABUT oleh Batch N.** Gerbang `Time.timeScale` dan penyaring kata pujian dihapus dari kode. Bagian A (langit-langit nada permata) dan Bagian C (perisai item) MASIH BERLAKU. Baca `HANDOFF-BATCH-N.md`. |

> **PERINGATAN - BACA DULU.** Bagian 3 (Gerbang Combo), baris `kubikaGateSlow` / `kubikaGateMaxHold` di bagian 5, poin 3 di bagian 7, dan empat baris pertama tabel di bagian 9 **sudah tidak berlaku**. Semuanya dicabut oleh Batch N pada 2026-09-09 dan diganti jeda nyata di dalam loop cascade (`KbtWaitPraise()`). Bagian 2 (Bagian A) dan bagian 4 (Bagian C) tetap akurat. Sumber kebenaran terbaru: `HANDOFF-BATCH-N.md`.

Commit Batch M:

| # | Commit | Berkas | Isi |
|---|---|---|---|
| 1/4 | `36bcbd3a` | `Assets/Tetris3D.Sfx.cs` | Bagian A - langit-langit nada permata diturunkan |
| 2/4 | `28f364d3` | `Assets/Tetris3D.Beat.cs` (BARU) + `.meta` | Bagian B + C - mesin gerbang combo & perisai item |
| 3/4 | `5debfc89` | `Assets/Tetris3D.Gelembung2.cs` | 4 baris penanda `KbtItemBegin()` / `KbtItemEnd()` |
| 4/4 | (dokumen ini) | `HANDOFF-BATCH-M.md` | Dokumentasi |

Dokumen terkait: `HANDOFF-BATCH-N.md` (pencabut Bagian B), `HANDOFF-BATCH-L.md` (SFX rotasi/kunci/jatuh/permata), `HANDOFF-BATCH-GHI.md` (kata pujian), `HANDOFF-BATCH-EF.md` (hit-stop & cincin), `HANDOFF-BATCH-DJ.md` (musik & jendela combo), `HANDOFF-BATCH-B.md` (latar).

---

## 1. Kenapa Batch M ada

Tiga keluhan dari pemain (kutipan asli):

1. "untuk yg permata kyk ketinggian klo banyak combonya" - bunyi cring permata makin lama makin melengking saat combo panjang.
2. "klo combo nya berurutan dengan cepat, suaranya seperti susul susulan... gmn klo setiap blok yg hancur ke blok yg hancur setelahnya kita freze kan dulu... sampe efek suara pujian habis baru ke cin cin selanjutnya, + ketika itu blok yg turun juga berhenti sejenak".
3. "begitu pun dengan efek item, terutama bom & palu... efek visual item itu kan ada durasinya, ketika blok yg turun masih jalan, eh keburu game over duluan".

Lingkup yang disetujui pemain: A + B + C, dengan izin eksplisit menyisipkan 2 baris (jadinya 4 baris, 2 per coroutine) di `Gelembung2.cs`, dan lama beku dibatasi ~1,1 detik.

**Catatan Batch N:** keluhan nomor 2 ternyata SALAH DITERJEMAHKAN di sini. "Freze kan dulu" berarti **berhenti sungguhan**, bukan gerak lambat. Klarifikasi pemain menyusul pada 2026-09-09. Lihat `HANDOFF-BATCH-N.md` bagian 2b.

---

## 2. Bagian A - langit-langit nada permata (`Tetris3D.Sfx.cs`)

### Diagnosis (angka nyata)

Rumus lama (warisan `KubikaSfx.cs` dari KubikaBlast):

```
pitch = Mathf.Min(1.9f, 1f + i * 0.055f);   // i = nomor butir permata
vol   = Lerp(0.75f, 0.42f, Clamp01(i / KSF_GEM_SPAN));  // KSF_GEM_SPAN = 12
```

- `1.9x` = **+11,2 semitone** (hampir satu oktaf penuh). Terlalu tinggi untuk gelombang sinus murni.
- Langit-langit itu baru tercapai di butir ke-**17**, sedangkan peredupan volume sudah habis di butir ke-**12**.
- Akibatnya butir 12-17 masih naik nada terus tapi sudah di volume paling pelan - persis bagian yang terdengar paling nyaring/menusuk.

### Yang diubah

```csharp
const float KSF_GEM_TOP  = 1.45f;   // dulu hardcode 1.9f
const float KSF_GEM_STEP = 0.038f;  // dulu 0.055f
...
ksfGemSrc.pitch = Mathf.Min(KSF_GEM_TOP, 1f + i * KSF_GEM_STEP);
```

- Langit-langit baru = **+6,4 semitone** (kira-kira interval kuint, masih terdengar naik tapi tidak melengking).
- `1.45 / 0.038 = 11,8` -> langit-langit tercapai tepat di butir ke-**12**, sama dengan titik habisnya peredupan volume. Tangga nada dan tangga volume kini berakhir bersamaan.
- Tidak ada bagian lain di `Sfx.cs` yang berubah.

### Perubahan rencana yang jujur harus dicatat

Di rancangan sempat disebut akan **berhenti** mereset `ksfGemIndex` tiap ledakan. Itu **dibatalkan**: reset tetap dipertahankan, karena reset justru mencegah nada memanjat liar antar-ledakan, dan langit-langit baru sudah cukup menyelesaikan keluhan. Kuantisasi pentatonik (`{0,2,4,7,9}` seperti `ClearCascade` di KubikaBlast) juga **tidak** dikirim - disimpan sebagai tuas cadangan supaya Bagian A tetap satu variabel saja.

---

## 3. Bagian B - Gerbang Combo (`Tetris3D.Beat.cs`) - **DICABUT OLEH BATCH N**

> Seluruh bagian ini sudah **tidak berlaku** sejak commit `a0eeac7c` (2026-09-09). Gerbang `Time.timeScale` dan penyaring tier dihapus dari kode. Dipertahankan di sini sebagai catatan sejarah dan penjelasan kenapa pendekatannya gagal. Penggantinya: `KbtWaitPraise()`, lihat `HANDOFF-BATCH-N.md`.

### Masalah

Saat reaksi berantai, satu link cascade memakan `FlashClear` 0,40 s + `AnimateFall` 0,16 s = **0,56 s**. Padahal klip suara pujian berdurasi 0,758 s (good) sampai 2,560 s (legendary). Jadi link berikutnya selalu memotong kata sebelumnya -> terdengar "susul-susulan".

### Solusi (LAMA): pelambatan waktu, BUKAN pembekuan total

`Time.timeScale = 0` **HARAM** di proyek ini. `FlashClear`, `AnimateFall`, `CascadeGravity`, dan semua `WaitForSeconds` di `BombBlast`/`HammerBlast`/`CoChaChing` memakai waktu **berskala**. Menyetel 0 = hang permanen.

Maka gerbang memakai `Time.timeScale = kubikaGateSlow` (bawaan **0,35**) selama maksimal `kubikaGateMaxHold` (bawaan **1,10** detik, sesuai pilihan pemain).

Efek nyata: jarak antar-link cascade **0,56 s -> sekitar 1,27 s** (1,1 s pada 0,35x menutup 0,385 s waktu berskala; sisa ~0,175 s jalan di 1x).

**Kenapa ini gagal:** karena animasi hancurnya cincin JUGA memakai waktu berskala, memperlambat waktu hanya membuat animasinya lelet - bukan menjeda sesudah animasi selesai. Yang diminta pemain justru animasi normal lalu berhenti sejenak. Batch N mengganti seluruh mekanisme ini dengan `yield` di dalam loop cascade, yang tidak menyentuh `Time.timeScale` sama sekali.

### Antrean suara pujian - `Beat.cs` MENGAMBIL ALIH suara (MASIH BERLAKU)

Ini beda dari rancangan awal dan harus dicatat: `Tetris3D.Praise.cs` **tidak diedit sama sekali**. Sebagai gantinya `Beat.cs` mengambil alih:

- `KbtClaimVoice()` menyetel `kubikaPraiseVoice = false` (diingat di `kbtVoiceForced`) lalu memainkan suaranya sendiri.
- Pemutaran memakai **dua AudioSource bergantian** (`KubikaPraiseVoice1` / `KubikaPraiseVoice2`), jadi ekor kata sebelumnya berdengung menimpa kata berikutnya, bukan terpotong.
- Teks, warna, dan animasi kata pujian tetap milik `Praise.cs`.
- Pengambilalihan dijalankan di `Update()` driver, **bukan** `LateUpdate()`. Semua `Update` berjalan sebelum semua `LateUpdate`, jadi bendera sudah berbalik sebelum `KubikaPraiseDriver` (order 25200) sempat memainkan kata di frame pertama. Kalau ditaruh di `LateUpdate`, kata bisa terputar dua kali.

### Penyaring tingkat - **DIHAPUS DI BATCH N**

> Inilah penyebab laporan "kadang bunyi kadang engga, good nya bunyi awesome nya engga". Fungsi `KbtShouldSpeak()` dihapus seluruhnya di `a0eeac7c`. Sekarang ketujuh kata dibunyikan.

`KbtShouldSpeak(tier)` yang lama:

1. Kalau `kubikaVoiceQueue` mati -> selalu bicara (perilaku lama).
2. Kalau `Time.unscaledTime - kbtSpokenAt < KBT_MIN_GAP` (0,30 s) -> lewati.
3. Kalau `kbtSpokenTier <= 0` -> bicara (pembuka rantai).
4. Kalau tingkatnya LEGENDARY -> selalu bicara.
5. Kalau `tier >= kbtSpokenTier + 2` -> bicara.

Hasil untuk rantai penuh: **GOOD -> AMAZING -> INCREDIBLE -> LEGENDARY** (4 kata, bukan 7). Karena combo naik satu tingkat per cincin, syarat "lompat 2" membuat kata jadi selang-seling sementara TEKS-nya tetap muncul ketujuh-tujuhnya.

### Pelepasan kepemilikan waktu - **SUDAH TIDAK RELEVAN**

> Seluruh mekanisme ini hilang bersama gerbangnya. `Beat.cs` bukan lagi pemilik `Time.timeScale`.

Gerbang hanya menulis `Time.timeScale` kalau nilainya sekarang > 0,001 - jadi tidak pernah menimpa `0` milik klaim gelembung. Saat melepas:

- `timeScale >= 0,999` -> lepas kepemilikan diam-diam.
- `|timeScale - kubikaGateSlow| < 0,05` -> tulis `1f`.
- Selain itu -> hitung `kbtRestoreWait`, dan setelah `KBT_RESTORE_TO` (1,5 s) paksa `1f`, asal hit-stop dan klaim gelembung sedang tidak memegang waktu.

Ini sengaja: kalau kepemilikan dilepas di tengah hit-stop (0,08), hit-stop akan memulihkan ke 0,35 dan tidak ada yang mengembalikan ke 1,0 -> game jalan lambat selamanya.

---

## 4. Bagian C - Perisai Item (`Beat.cs` + 4 baris di `Gelembung2.cs`) - MASIH BERLAKU

### Masalah

Animasi Bom bisa mencapai ~1,5 s dan Palu ~1,1 s sebelum cascade dimulai. Selama fase itu `clearing` masih `false`, jadi `Part3.Update()` tetap menurunkan balok aktif dan tetap bisa mengunci -> game over di tengah animasi penyelamat.

### Solusi: pin timer, bukan pelebaran interval

Selama perisai aktif, tiap `LateUpdate`:

```csharp
fallTimer = 0f;
lockTimer = 0f;
btnSoftDrop = false;
```

Sudah diverifikasi langsung di `Part3.Update()` bahwa `fallTimer` dan `lockTimer` **menghitung NAIK**, jadi menyetelnya ke 0 tiap frame aman dan tidak memicu jatuh tiap frame.

Kenapa bukan menggelembungkan `fallInterval`? Karena `ApplySlow()` melakukan `if (kbSlowTimer <= 0f) kbSlowOrig = fallInterval;` - kalau item Perlambat dipakai saat `fallInterval` sedang digelembungkan, nilai gelembung itu akan terkunci permanen.

Kenapa bukan memaksa `clearing = true`? Karena itu juga akan memblok `HardDrop`, dan membuat `KfxDetectClears()` memindai papan selama fase pra-cascade. Terlalu berisiko.

### 4 baris sisipan di `Gelembung2.cs`

```csharp
// BombBlast(...)
int n = objs.Count;
if (n == 0) yield break;
KbtItemBegin();          // <- BARU, WAJIB setelah guard ini
...
yield return StartCoroutine(ResolveClearsNoSpawn());
KbtItemEnd();            // <- BARU

// HammerBlast(...)
KbToast(...); KbEnsureItemSfx();
KbtItemBegin();          // <- BARU (tidak ada early return di sini)
...
yield return StartCoroutine(ResolveClearsNoSpawn());
KbtItemEnd();            // <- BARU
```

Penempatan di `BombBlast` **kritis**: kalau `KbtItemBegin()` ditaruh sebelum `if (n == 0) yield break;`, penanda akhir tidak akan pernah dieksekusi dan perisai baru lepas lewat watchdog 6 detik.

**Catatan Batch N:** `ResolveClearsNoSpawn()` sekarang punya SATU baris tambahan lagi (jeda antar cincin), jadi total sisipan di file ini menjadi 5 baris kode. Penanda perisai tidak berubah.

### Tiga jalur pelepasan (sengaja berlebih)

1. Penanda `KbtItemEnd()` dipanggil - jalur normal.
2. `clearing` berubah `true` lalu `false` - menangkap kasus coroutine mati karena `ClearBoard()` memanggil `StopAllCoroutines()`.
3. Watchdog `KBT_ITEM_MAX` = 6,00 detik - jaring pengaman terakhir.

---

## 5. Field baru & cara mematikan (revert switch)

Semua ada di komponen `Tetris3D` pada GameObject `Game` di Inspector:

| Field | Bawaan | Fungsi | Status |
|---|---|---|---|
| `kubikaComboGate` | `true` | Batch M: pelambatan antar-clear. Batch N: **jeda nyata** antar cincin | berlaku, arti berubah |
| `kubikaItemShield` | `true` | Matikan -> `KbtItemBegin/End` jadi no-op | berlaku |
| `kubikaVoiceQueue` | `true` | Batch M: penyaring kata. Batch N: hanya kepemilikan suara | berlaku, arti berubah |
| `kubikaGateSlow` | `0.35` | pelambatan gerbang | **DIHAPUS di Batch N** |
| `kubikaGateMaxHold` | `1.10` | lama pelambatan | **DIHAPUS di Batch N** |
| `kubikaPauseCap` | `3.00` | batas atas jeda antar cincin | **BARU di Batch N** |

Dua field `Kubika Gate Slow` dan `Kubika Gate Max Hold` akan **hilang dari Inspector** setelah Batch N. Itu normal - Unity membuang data serialisasi yang tidak punya field lagi.

**JEBAKAN SERIALISASI (penting).** Field `public` baru hanya memakai nilai bawaan kode **sampai `SampleScene.unity` disimpan ulang**. Setelah scene disimpan, nilai yang berlaku adalah yang tersimpan di scene. Jadi kalau nanti nilai bawaan diubah di kode tapi tidak berubah di game, cek Inspector dulu.

Matikan `kubikaComboGate` + `kubikaItemShield` + `kubikaVoiceQueue` = perilaku persis sebelum Batch M, kecuali langit-langit nada permata (itu di `Sfx.cs`, dikendalikan `kubikaSfxRetune` / `kubikaGemChime` dari Batch L).

---

## 6. Yang TIDAK disentuh

- `Tetris3D.Praise.cs` - nol edit.
- `Tetris3D.Part3.cs` - nol edit.
- `Tetris3D.Music.cs` / `BuildMusic()` - musiknya sudah disukai pemain, tidak diutak-atik.
- `Tetris3D.Impact.cs` - hit-stop tetap milik Batch F.

**Catatan Batch N:** `Tetris3D.Part2.cs` yang di Batch M nol edit, di Batch N mendapat **1 baris** sisipan di `ResolveBoard()` (dengan izin eksplisit pemain). `Part3.cs` tetap nol edit.

---

## 7. Risiko & kasus tepi yang masih terbuka

1. **HardDrop saat animasi item.** Selama fase pra-cascade `clearing` masih `false`, jadi menekan spasi / tombol DROP tetap bisa mengunci balok di tengah animasi, dan itu memicu ulang lomba lama `LockPiece() -> ResolveBoard()` vs `ResolveClearsNoSpawn()`. Memblokirnya butuh edit `Part3.cs`. Paparannya sudah jauh berkurang karena baloknya sendiri tidak turun lagi. **(masih terbuka)**
2. **Kepemilikan suara pujian.** Kalau pemain mencentang-mati `kubikaPraiseVoice` secara manual, lalu mematikan `kubikaComboGate` DAN `kubikaVoiceQueue`, `Beat.cs` akan mengembalikan `kubikaPraiseVoice = true`. **(masih terbuka)**
3. ~~**Tiga pemilik `Time.timeScale`**~~ - **TIDAK BERLAKU LAGI.** Sejak Batch N, `Beat.cs` tidak pernah menulis `Time.timeScale`. Pemiliknya tinggal dua: klaim gelembung (0/1) dan hit-stop (0,08).
4. **`PolyGain()` masih macet** (masalah lama, butuh edit `Part3.cs`). **(masih terbuka)**
5. **Klik ekor di `MakeArp` & `MakeGameOverSting`** masih ada (masalah lama, butuh edit `Part3.cs`). **(masih terbuka)**

---

## 8. Titik uji main

> Poin 1, 2, dan 8 sudah digantikan oleh titik uji Batch N. Poin 3-7 tetap berlaku.

1. ~~Combo cepat 4+ link: papan terlihat melambat ~1,1 detik antar-clear.~~ -> Batch N: papan **berhenti**, bukan melambat.
2. ~~Rantai penuh hanya mengeluarkan 4 kata.~~ -> Batch N: **ketujuh kata** terdengar.
3. Bunyi cring permata tidak lagi melengking di combo tinggi.
4. Bom / Palu saat papan hampir penuh: balok aktif menggantung, tidak game over di tengah animasi.
5. Klaim gelembung tepat saat combo berjalan: setelah popup ditutup, `Time.timeScale` kembali ke 1,0.
6. Restart di tengah animasi item: perisai lepas sendiri (uji jalur `ClearBoard`).
7. Hit-stop (clear 2+ baris) bersamaan dengan jeda: waktu tetap pulih ke 1,0.
8. ~~Matikan `kubikaComboGate` -> perilaku lama kembali persis.~~ -> masih benar, tapi artinya sekarang "tanpa jeda", bukan "tanpa pelambatan".

---

## 9. Tuas penyetelan kalau kurang pas

| Keluhan | Tuas | Status |
|---|---|---|
| ~~Beku terasa terlalu lama~~ | ~~`kubikaGateMaxHold` 1,10 -> 0,70~~ | **DIGANTI** `kubikaPauseCap` |
| ~~Beku kurang terasa~~ | ~~`kubikaGateMaxHold` 1,10 -> 1,60~~ | **DIGANTI** `kubikaPauseCap` |
| ~~Pelambatan terlalu kentara~~ | ~~`kubikaGateSlow` 0,35 -> 0,55~~ | **DIHAPUS** |
| ~~Kata pujian terlalu sedikit / banyak~~ | ~~ubah `tier >= kbtSpokenTier + 2`~~ | **DIHAPUS**, semua kata bunyi |
| Jeda kelamaan / kurang | `kubikaPauseCap` (lihat `HANDOFF-BATCH-N.md`) | berlaku |
| Permata masih terlalu tinggi | `KSF_GEM_TOP` 1,45 -> 1,30 | berlaku |
| Permata jadi datar | `KSF_GEM_TOP` 1,45 -> 1,60 | berlaku |
| Ingin nada permata musikal | pakai kuantisasi pentatonik `{0,2,4,7,9}` | berlaku |
