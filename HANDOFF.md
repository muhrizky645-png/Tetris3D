# HANDOFF — KUBIKA TOWER (Tetris3D)

**Dibuat:** 2 September 2026  
**Status:** DAFTAR RENCANA. Belum ada satu baris kode gameplay yang diubah.  
**Repo:** `muhrizky645-png/Tetris3D` — branch `main`

Dokumen ini adalah hasil audit read-only seluruh kode inti, plus daftar perubahan yang ingin dilakukan. Tujuannya supaya pekerjaan bisa dilanjutkan kapan saja tanpa harus mengaudit ulang.

---

## 0. NIAT DESAIN (dari owner — JANGAN dilanggar)

Hal-hal berikut adalah **keputusan desain sadar**, bukan bug. Jangan "diperbaiki".

1. **Game ini endless dan sengaja dibuat agak MUDAH.** Tujuannya mengejar skor tertinggi, bukan menyulitkan pemain. Setiap usulan balance harus condong ke arah mudah, bukan hardcore.

2. **`ClearedRowGravity()` sengaja setengah-setengah.** Blok di bawah cincin yang hancur tetap diam (lubang dibiarkan terjebak), blok di atas cincin jatuh merapat. Ini memang aturan Tetris klasik yang benar.  
   Owner sudah pernah mencoba cascade penuh (semua blok jatuh) dan hasilnya **terlalu mudah** — muncul reaksi berantai yang menghancurkan 3–4 baris sekaligus tanpa direncanakan pemain. **Jangan kembalikan ke cascade penuh.**

3. **Wipe papan di `StageUp()` boleh tetap ada.** Awalnya ini dianggap masalah, tapi karena game-nya endless dan sengaja mudah, wipe itu justru katup relief yang memungkinkan run panjang. Migrasi papan lintas-babak diturunkan jadi **opsional / nanti** (lihat Lampiran A).

4. **`maxColumns = 30` terlalu lebar.** Ini yang ingin diturunkan. Lihat Bagian 2.

---

## 1. URGENT — bukan gameplay, tapi harus ditangani lebih dulu

### 1.1 Keystore ter-commit di repo publik

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

## 2. TUNING DIAMETER (permintaan utama)

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
| **A — REKOMENDASI** | **24** | **2** | 15 → 17 → 19 → 21 → 23 → 24 | level 21 | 5,44 | 38,1 |
| B — paling lega | 21 | 2 | 15 → 17 → 19 → 21 | level 13 | 4,76 | 36,6 |
| C — perubahan minimal | 27 | 3 | 15 → 18 → 21 → 24 → 27 | level 17 | 6,12 | 39,6 |
| sekarang | 30 | 3 | 15 → 18 → 21 → 24 → 27 → 30 | level 21 | 6,80 | 41,1 |

**Kenapa Opsi A:**
- Lebar maksimum turun 20% (30 → 24 slot), jauh lebih mudah dilacak
- `columnsPerStage = 2` menjaga **jumlah stage-up tetap 5** dan diameter maks tetap tercapai di **level 21**, sama seperti sekarang. Jadi pacing progresi tidak berubah sama sekali — hanya titik akhirnya lebih sempit
- Kamera 7% lebih dekat, blok 7% lebih besar di layar
- `maxDiameterLevel` tetap 21, sehingga eskalasi endgame (`EffectiveStoneChance`, baris sampah ganda) berjalan di level yang sama seperti sekarang

Opsi B lebih lega tapi memicu endgame di level 13 — untuk game yang sengaja mudah itu terlalu cepat.

### 2.4 Efek samping yang perlu diputuskan

**Skor per cincin turun.** Rumusnya `columns * cellPoints * rowMult * combo`. Cincin tunggal: 30 x 10 = 300 menjadi 24 x 10 = 240 (turun 20%).

Dua pilihan:
- **Terima saja.** Skor leaderboard lama jadi tidak sebanding dengan yang baru
- **Kompensasi:** `cellPoints` 10 -> 12, sehingga 24 x 12 = 288 (mendekati 300). Skala skor lama tetap kurang lebih sebanding

Rekomendasi: kompensasi dengan `cellPoints = 12`, karena leaderboard `tetris3d_global` sudah punya entri.

**Tidak ada perubahan lain yang diperlukan.** `weirdShapeColumns = 20` tetap terlampaui (kolom mencapai 21 di level 13), dan `weirdShapeLevel = 12` tetap memicu bentuk aneh di level 12 seperti sekarang.

---

## 3. PERBAIKAN CEPAT (kerjakan lebih dulu — semuanya kecil & lokal)

Semua di bawah ini perubahannya beberapa baris, risikonya rendah, dan tidak menyentuh arsitektur.

### F1. CRASH — tombol aktif selagi baris meledak
**File:** `Assets/Tetris3D.Part4.cs` (`OnGUI`)

`Update()` berhenti lebih awal saat `clearing == true`, tapi `OnGUI` tetap menggambar tombol ROTASI / JATUH / TURUN. Selama animasi clear (0,4 dtk + gravitasi 0,16 dtk per rantai, bisa lebih dari 2 dtk saat combo), `active == null`. Tap ROTASI memanggil `Rotate()` -> `RedrawActive()` -> `active.Length` -> **NullReferenceException**. Sama untuk JATUH -> `HardDrop()` -> `Move()`.

**Perbaikan:** nonaktifkan / abaikan ketiga tombol saat `clearing || active == null || gameOver || paused`.  
**Catatan:** `UseBuffFromInv()` di `Toko.cs` sudah punya guard `clearing` — tinggal disamakan.

### F2. Preview NEXT berbohong
**File:** `Assets/Tetris3D.Part2.cs` (`SpawnPiece`)

```
int spins = Random.Range(0, 4);
for (int k = 0; k < spins; k++) ...
```

Balok diputar 0–3 kali secara acak **setelah** `nextType` ditentukan, sementara kotak preview menggambar bentuk dasar tanpa rotasi. Yang muncul tidak pernah dijamin sama dengan yang dilihat pemain.

Efek kedua, lebih penting: ini **membunuh seluruh sistem assist**. `PickNextType()` -> `FindFittingShape()` bekerja keras mencari bentuk **beserta rotasinya** yang muat rapi, lalu rotasinya diacak lagi di sini. Jadi `assistStart = 0.85` praktis tidak berefek, dan ongkos CPU-nya terbuang.

**Perbaikan:** hapus blok rotasi acak itu. Preview jadi jujur DAN assist jadi berfungsi. Karena game ini sengaja mudah, assist yang akhirnya bekerja adalah keuntungan.

### F3. Balok BATU tidak diumumkan
**File:** `Assets/Tetris3D.Part2.cs` (`SpawnPiece`, `PickNextType`)

`curStone` diundi di dalam `SpawnPiece()`, jadi preview tidak pernah bisa menunjukkan bahwa balok berikutnya adalah batu (tidak bisa diputar). Mulai level 18 dengan peluang naik sampai 45%, pemain dapat balok yang tidak bisa diputar tanpa peringatan.

**Perbaikan:** tambah field `nextStone`, undi bersamaan dengan `nextType`, gambar preview dengan `StoneColor()` kalau `nextStone == true`.

### F4. Combo memicu banyak level sekaligus
**File:** `Assets/Tetris3D.Part2.cs` (`RecalcLevel`)

```
while (score >= nextLevelScore && guard++ < 100)
```

Rantai 4 cincin dengan combo x4 di 24 kolom bisa memberi ribuan poin dalam satu event -> naik 3–4 level sekaligus -> `OnLevelUp()` jalan 3–4 kali -> plafon turun 3 baris + 3 baris sampah, atau `StageUp()` berkali-kali. **Pemain dihukum karena bermain bagus.**

**Perbaikan:** batasi maksimal **1 level per event**. Ubah `while` jadi `if`. Sisa skor tetap terhitung untuk level berikutnya karena `nextLevelScore` bersifat akumulatif.

### F5. Pemain terkunci di layar Buat Profil
**File:** `Assets/Tetris3D.Part3.cs` / `Part4.cs` (`DrawProfileScreen`)

Tombol TUTUP hanya muncul kalau `editingProfile == true`. Saat game over pertama, `showProfile = true` dengan `editingProfile = false`, sehingga **tidak ada jalan keluar tanpa mengisi nama**. Dan karena `OnGUI` `return` di titik itu, animasi count-up skor & layar Game Over pertama tidak pernah terlihat. Sesi pertama pemain berakhir dengan formulir, bukan perayaan.

**Perbaikan:** selalu tampilkan tombol TUTUP / NANTI SAJA. Idealnya tunda layar profil sampai setelah animasi skor selesai.

### F6. Layar revive bisa menggantung selamanya
**File:** `Assets/Tetris3D.Extras.cs` (`RequestReviveByAd`)

`reviveTimer = 9999f`. Kalau callback SDK iklan tidak pernah datang (kasus tepi AdMob yang nyata), pemain terkunci di layar revive tanpa jalan keluar.

**Perbaikan:** ganti jadi timeout wajar (~12 dtk) lalu jatuh ke `OnReviveAdUnavailable()`.

### F7. `btnSoftDrop` tidak dibersihkan di jalur early-return
**File:** `Assets/Tetris3D.Part3.cs` (`Update`)

Di-set di `OnGUI`, direset di baris **terakhir** `Update()`. Semua `return` lebih awal (clearing / paused / gameOver / revive) melewatkan reset, sehingga begitu clearing selesai balok langsung menyelonong turun cepat.

**Perbaikan:** reset `btnSoftDrop = false` di awal `Update()`, atau sebelum setiap `return`.

### F8. `sfx.pitch` bocor -> SELURUH SFX jadi sumbang permanen
**File:** `Assets/Tetris3D.Part2.cs` (`ClearBoard`), `Gelembung.cs` (`KbSfxAt`), `Currency.cs` (`CurPlayChaChing`)

`ClearBoard()` memanggil `StopAllCoroutines()`. Kalau coroutine yang sedang memodifikasi `sfx.pitch` (`KbSfxAt`, `CoChaChing`, `BombBlast`, `HammerBlast`) terbunuh sebelum meresetnya, pitch tertinggal di nilai non-1 dan **semua SFX jadi sumbang sampai aplikasi direstart**.

**Perbaikan:** tambahkan `if (sfx != null) sfx.pitch = 1f;` di `ClearBoard()`. Jangka panjang: pakai `AudioSource` terpisah untuk SFX ber-pitch, atau `PlayOneShot` tanpa memutasi pitch global.

### F9. Iklan sudah ditonton tapi Koin tidak masuk
**File:** `Assets/Tetris3D.Part2.cs` (`ClearBoard`), `PetiKoin.cs` (`CoAfterPetiAd`), `Gelembung2.cs`

`StopAllCoroutines()` di `ClearBoard()` juga membunuh `CoAfterPetiAd()` / `CoAfterBubbleDrop()`, sehingga refresh Koin dari server tidak pernah terjadi. **Ini keluhan monetisasi serius** — pemain menonton iklan dan tidak mendapat apa-apa.

**Perbaikan:** jalankan coroutine terkait reward di GameObject/komponen terpisah yang tidak ikut terkena `StopAllCoroutines()` milik `Tetris3D`, atau ganti `StopAllCoroutines()` dengan penghentian selektif berdasarkan handle coroutine.

### F10. Bom & Palu memakai gravitasi yang sudah ditolak
**File:** `Assets/Tetris3D.Gelembung2.cs` (`BombBlast`, `HammerBlast`)

Line clear normal memakai `ClearedRowGravity()` (lubang menetap — desain owner). Tapi Bom dan Palu memakai `CascadeGravity()` (kompaksi penuh per kolom) — **persis mekanik yang owner buang karena terlalu mudah**.

Akibatnya:
- Bom jadi tombol reset papan: hancurkan 50% blok acak, lalu cascade **menghapus semua lubang** yang terkumpul sepanjang run
- Cascade memicu clear berantai yang masuk ke `ResolveClearsNoSpawn()` dan memberi skor, baris, dan combo penuh
- Aturan fisika berubah tanpa pemberitahuan — inilah sumber "tidak bisa diprediksi" yang sesungguhnya

**Perbaikan:** ganti `CascadeGravity()` menjadi `ClearedRowGravity()` di kedua jalur item. Konsisten dengan desain, dan Bom jadi keputusan taktis, bukan tombol ajaib.

### F11. `AddGarbageRow()` bisa menabrak balok aktif
**File:** `Assets/Tetris3D.Part2.cs` (`AddGarbageRow`)

Dari `ResolveBoard()` aman (`active` sudah null). Tapi dari **`ResolveClearsNoSpawn()`** (jalur Bom/Palu) `active` masih hidup — pemain memakai item saat balok sedang jatuh. Tumpukan digeser ke atas menembus balok aktif, semua `Valid()` gagal, balok langsung lock dan menimpa grid. **Papan rusak.**

**Perbaikan:** kalau `active != null`, naikkan `curRow` sebanyak 1 sesudah pergeseran, atau tunda `AddGarbageRow()` sampai balok terkunci.

### F12. `highScore` di-update live saat bermain
**File:** `Assets/Tetris3D.Part4.cs` (`OnGUI`)

`if (score > highScore)` jalan setiap pass `OnGUI` (Layout + Repaint, beberapa kali per frame), termasuk `PlayerPrefs.SetInt`. Akibatnya chip BEST di HUD selalu sama dengan skor sekarang, sehingga **ketegangan "kejar rekormu" hilang total**. Ditambah penulisan `PlayerPrefs` tiap frame.

**Perbaikan:** update `highScore` hanya sekali saat game over. Selama bermain, tampilkan `runBaselineHi` (rekor sebelum run ini dimulai).

### F13. Celah baris sampah tidak selaras antar baris
**File:** `Assets/Tetris3D.Part2.cs` (`AddGarbageRow`)

```
while (gaps.Count < gapN && guard++ < 500) gaps.Add(Random.Range(0, columns));
```

Posisi celah **diacak ulang dari nol setiap baris**. Dua baris sampah dengan celah di posisi berbeda praktis tidak bisa dibersihkan. Itu bukan tangga kesulitan, itu spiral kematian.

**Perbaikan:** simpan `lastGarbageGaps`, dan untuk baris berikutnya gunakan posisi yang sama atau bergeser maksimal 1 kolom. Ini standar di garbage Tetris kompetitif.

---

## 4. TABEL TUNING BALANCE (condong ke MUDAH, sesuai niat desain)

| Field | Sekarang | Usulan | Alasan |
|---|---|---|---|
| `maxColumns` | 30 | **24** | 30 terlalu lebar; 20% lebih sedikit slot untuk dilacak |
| `columnsPerStage` | 3 | **2** | Menjaga 5 stage-up & diameter maks tetap di level 21 |
| `cellPoints` | 10 | **12** | Kompensasi skor akibat cincin lebih sempit (leaderboard tetap sebanding) |
| `garbageGapCount` | 2 | **4** | 2 celah di 24 kolom = spiral kematian; ~1 celah per 6 kolom |
| celah sampah | acak per baris | **selaras antar baris** | Lihat F13 |
| `RecalcLevel` | sampai 100 level/event | **maks 1 level/event** | Lihat F4 |
| `minPlayHeight` | 11 | **12** | Pentomino setinggi 5 butuh ruang manuver |
| `stoneChance` cap | 0,45 | **0,30** | Balok tak bisa diputar; sudah dibantu F3 tapi 45% terlalu keras |
| `stoneStartLevel` | 18 | 18 (tetap) | Sudah pas |
| `assistMin` | 0,15 | **0,30** | Lantai bantuan dinaikkan; selaras dengan niat "agak mudah" |
| `assistStart` | 0,85 | 0,85 (tetap) | Baru benar-benar berfungsi setelah F2 |
| `comboSeconds` | 10 | 10 (tetap) | Combo longgar cocok untuk pengejar skor tertinggi |
| `baseLevelScore` | 600 | **800** | Level 2 sekarang cuma butuh 2 cincin; terlalu cepat |
| `levelStep` | 250 | **300** | Menyesuaikan `baseLevelScore` |
| `fallInterval` | 0,8 tetap | **0,8 -> 0,55 bertahap L1–20** | Sedikit ketegangan di akhir, tetap ramah |

**Catatan `fallInterval`:** sekarang konstan sepanjang game (tertulis eksplisit di header `Tetris3D.cs`). Tidak ada akselerasi sama sekali, dan hard drop selalu tersedia, jadi **nol tekanan waktu**. Semua kesulitan bersifat spasial. Untuk game yang sengaja mudah ini sebenarnya tidak fatal, tapi ramp ringan sampai 0,55 dtk akan memberi sensasi flow tanpa membuatnya sulit. Kalau ragu, ini item paling aman untuk ditunda.

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

**Usulan:** ganti jadi bentuk spasial — radius di satu titik, atau pita 3 kolom penuh. Lebih terbaca, lebih memuaskan, lebih bisa di-balance. Gabungkan dengan F10 (jangan pakai `CascadeGravity`).

### 5.3 Palu sering merugikan

`ApplyHammer()` selalu menghancurkan baris 0 dan 1. Di silinder, baris terbawah justru yang paling penuh dan paling dekat selesai — jadi Palu sering **membuang cincin yang tinggal 1 blok lagi**.

**Usulan:** targetkan 2 baris dengan **lubang terbanyak**, bukan 2 baris terbawah.

### 5.4 Frekuensi iklan terlalu agresif

Gelembung buff tiap 24–40 dtk, gelembung Koin tiap 90 dtk, plus iklan revive, plus Peti Koin. Rata-rata ada tawaran iklan tiap ~30 detik.

Diperburuk oleh `PickBubbleType()`: selama `BUFF_AD_COOLDOWN` (180 dtk) berjalan, **semua** gelembung dipaksa jadi `IT_GEM` — yang juga butuh iklan. Jadi selama 3 menit pemain hanya disodori prompt iklan tanpa variasi hadiah.

**Usulan:** selama cooldown, jangan spawn gelembung sama sekali, atau spawn hadiah kecil yang **gratis** (tanpa iklan). Naikkan `BUBBLE_MIN_GAP` ke 40 dtk.

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

3. **Satu `AudioSource` dipakai bersama dengan `pitch` yang dimutasi global.** Lihat F8. `KbSfxAt()` mengubah `sfx.pitch` dan memengaruhi SFX lain yang sedang berbunyi.

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

---

## 9. PERFORMA (penyebab stutter di Android kelas menengah)

Stutter merusak "rasa memuaskan" lebih dari apa pun, jadi ini bukan item kosmetik.

1. **`GuiText()` menggambar 9 `GUI.Label` per string** (8 outline + 1 isi) dan mengalokasikan `GUIStyle` baru setiap panggilan. `OnGUI` jalan beberapa kali per frame di **empat** MonoBehaviour terpisah (`Tetris3D`, `KubikaCurrencyHUD`, `KubikaBubbleHUD`, `KubikaTokoHUD`). Ini sampah GC tiap frame plus ratusan draw call. **Usulan:** cache `GUIStyle` sebagai field statis; kurangi outline dari 8 arah jadi 4.

2. **`FindFittingShape()` bisa ~180 ribu operasi per spawn** (17 bentuk x 4 rotasi x 24 kolom x `DropRowFor` 18 baris), dijalankan tepat di tengah `ResolveBoard()`. Hitch persis di momen transisi. **Usulan:** batasi jumlah bentuk yang dicoba (misal 5 pertama setelah shuffle), atau hitung profil ketinggian kolom sekali lalu pakai untuk semua kandidat.

3. **`Burst()` membuat GameObject + ParticleSystem baru per sel yang hancur** — di 24 kolom itu 24 particle system per cincin. **Usulan:** satu particle system persisten, panggil `Emit()` dengan posisi berbeda.

4. **Tiga HUD komponen memanggil `FindFirstObjectByType<Tetris3D>()` setiap frame.** **Usulan:** cache referensinya sekali.

5. **`CurrencyTick()` didorong dari `KubikaCurrencyHUD.Update()`** — reward permata bergantung pada komponen eksternal. Kalau komponen itu hilang, tidak ada permata yang diberikan. **Usulan:** pindahkan ke `Tetris3D.Update()`.

---

## 10. LEADERBOARD

1. **Metrik tidak konsisten.** `SubmitScore()` mengirim `score` (run ini), `PushName()` mengirim `highScore`. Kalau leaderboard UGS di-set mode "latest" dan bukan "best", satu run jelek akan **menurunkan** peringkat pemain. **Perbaikan:** selalu kirim `highScore`, dan pastikan mode leaderboard = best score.

2. **`LoadRanks` mengambil `Limit = 50`** tapi label menu berkata "Top 10" / "Top 5". Samakan.

3. **`ParseCountry`** melakukan parsing JSON metadata dengan pencarian indeks string secara naif. Rapuh; pakai parser JSON yang benar.

4. Lihat juga 5.6 — skor dari item merusak integritas peringkat.

---

## 11. URUTAN EKSEKUSI YANG DISARANKAN

**Batch 1 — keamanan (di luar kode game)**
- 1.1 keystore, 1.2 gitignore

**Batch 2 — perbaikan cepat (semuanya kecil, risiko rendah)**
- F1 crash tombol, F2 preview jujur, F3 batu di preview, F4 batas 1 level, F5 tombol keluar profil, F6 timeout revive, F7 `btnSoftDrop`, F8 pitch bocor, F10 gravitasi item, F11 garbage vs balok aktif, F12 highScore live, F13 celah selaras
- F9 (coroutine reward) sedikit lebih besar — boleh dipisah

**Batch 3 — tuning angka (Bagian 2 + Bagian 4)**
- Semuanya perubahan nilai field. **Uji dalam satu sesi bermain penuh** karena saling berinteraksi

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

---

## LAMPIRAN B — Peta file

| File | Isi utama |
|---|---|
| `Tetris3D.cs` | Field & tunable, `Start`, `AllocGrid`, `SetupScene`, `ApplyGeometry`, `ApplyStageColors`, `MakeMat`, `MakeBlock`, `CellLocalPos`, `Wrap`, `shapes`/`boxSize`/`shapeTier`, enum bahasa, field leaderboard |
| `Tetris3D.Part2.cs` | `SpawnPiece`, `PickNextType`, `AllowedShapes`, `FindFittingShape`, `Valid`, `Rotate`, `LockPiece`, `ResolveBoard`, `FlashClear`, `CascadeGravity`, `ClearedRowGravity`, `TooHigh`, `HardDrop`, `RecalcLevel`, `OnLevelUp`, `StageUp`, `AddGarbageRow`, `ClearBoard`, `Shake`, `Burst` |
| `Tetris3D.Part3.cs` | `SetupAudio`, `Sfx`, `MakeTone`/`MakeArp`/`MakeMusic`, `Update()`, UGS & leaderboard, lokalisasi |
| `Tetris3D.Part4.cs` | `GuiText`, `Btn3D`, `DrawStartMenu`, `DrawPauseMenu`, `DrawProfileScreen`, `DrawRanksScreen`, `OnGUI` |
| `Tetris3D.Extras.cs` | Haptic, revive (`RequestReviveByAd`, `ClearTopRowsForRevive`), `DrawGameOverScore`, `DrawSlider`, `Toast` |
| `Tetris3D.Currency.cs` | Permata/Koin, HUD mata uang, animasi burst permata 3 fase, `KubikaCurrencyHUD` |
| `Tetris3D.Gelembung.cs` | Konstanta item, `BubbleTick`, `SpawnBubble`, `PickBubbleType`, ikon item |
| `Tetris3D.Gelembung2.cs` | Panel klaim, `ApplyBuff`, `ResolveClearsNoSpawn`, `ApplyBomb`/`BombBlast`, `ApplyHammer`/`HammerBlast`, `ApplySlow`, `KubikaBubbleHUD` |
| `Tetris3D.Toko.cs` | `TOKO_PRICE`, `DrawTokoShop`, `DrawBuffInv`, `UseBuffFromInv`, `KubikaTokoHUD` |
| `Tetris3D.PetiKoin.cs` | Peti koin berbasis iklan, `KubikaAds` |
| `Tetris3D.UiScale.cs` | `VW`, `VH`, `UiScale`, `ApplyUiScale` |
| `Tetris3D.RoundedBlock.cs` | `RoundedBlockMesh()` |
| `Tetris3D.Saldoku.cs` | Integrasi Saldoku (belum diaudit) |
| `Tetris3D.AdLoading.cs` | Loading iklan (belum diaudit) |
| `Tetris3D.AdsReviveMrec.cs` | Iklan revive & MREC (belum diaudit) |

**Belum diaudit:** `Saldoku.cs`, `AdLoading.cs`, `AdsReviveMrec.cs`. Ketiganya jalur monetisasi, jadi sebaiknya diperiksa sebelum rilis besar berikutnya.
