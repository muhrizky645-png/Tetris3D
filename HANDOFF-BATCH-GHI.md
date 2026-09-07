# HANDOFF - BATCH G + H + I

Referensi: **KubikaBlast** (repo privat). Satu konsep, beda model - angkanya dipinjam,
bukan dikira-kira.

| Batch | Isi | Sumber KubikaBlast | File baru di Tetris3D |
|---|---|---|---|
| **G** | Cap combo 8, pengali 0.35/tingkat, level dari baris | `BlastCore.cs` | `Assets/Tetris3D.Balance.cs` |
| **H** | 7 kata pujian + 7 suara | `KubikaHud.cs` | `Assets/Tetris3D.Praise.cs` |
| **I** | targetFrameRate / vSync / sleepTimeout | `KubikaPerf.cs` | `Assets/Tetris3D.Balance.cs` |

`Tetris3D.cs`, `Part2`, `Part3`, `Part4` **tidak diubah satu baris pun**.

---

## 1. LANGKAH MANUAL - WAJIB (suara pujian)

Tujuh file mp3 **harus** diletakkan sendiri di Unity:

```
Assets/Resources/KubikaVoice/good.mp3
Assets/Resources/KubikaVoice/awesome.mp3
Assets/Resources/KubikaVoice/amazing.mp3
Assets/Resources/KubikaVoice/fantastic.mp3
Assets/Resources/KubikaVoice/incredible.mp3
Assets/Resources/KubikaVoice/unstoppable.mp3
Assets/Resources/KubikaVoice/legendary.mp3
```

Nama file **harus huruf kecil dan tepat seperti di atas** - `KPR_FILES` di
`Tetris3D.Praise.cs` memuat file berdasarkan nama ini (`Resources.Load` tanpa
ekstensi). Folder `KubikaVoice` juga harus ada di bawah `Resources`, sejajar
dengan `KubikaIcons` yang sudah dipakai ikon-ikon item.

Setelan Import yang disarankan (klik file di Unity -> Inspector):

| Setelan | Nilai | Alasan |
|---|---|---|
| Load Type | Decompress On Load | klip pendek, hindari jeda saat pertama diputar |
| Compression Format | Vorbis (atau ADPCM) | kualitas per ukuran lebih baik dari MP3 di Android |
| Quality | ~70% | suara ucapan tidak butuh lebih |
| Force To Mono | centang | sumbernya memang sudah mono |
| Preload Audio Data | centang | 7 klip x ~20 KB, aman |

**Kalau file mp3-nya belum dipasang, game tetap normal**: teks pujian tampil
seperti biasa, hanya tanpa suara. `KprPlayVoice()` keluar diam-diam kalau
`Resources.Load` mengembalikan null - fitur tidak boleh mati karena aset kurang.

### Yang sudah dilakukan ke file audionya

File aslinya kenyaringannya tidak rata - selisih **3,1 LUFS**:

| File | Sebelum | Sesudah |
|---|---|---|
| good | -13,8 LUFS / TP -2,6 dBTP | -16 LUFS / TP -1,5 dBTP |
| awesome | -12,9 | -16 |
| amazing | -13,2 | -16 |
| fantastic | -11,7 | -16 |
| incredible | -11,0 | -16 |
| unstoppable | **-10,7** | -16 |
| legendary | -11,8 | -16 |

Kalau dibiarkan, `unstoppable` terdengar paling keras dan `good` paling pelan -
padahal justru tingkat yang lebih tinggi yang harus terasa lebih megah, dan itu
mestinya datang dari isi suaranya, bukan dari ketidaksengajaan mastering.
Semua disetel ke **-16 LUFS / true peak -1,5 dBTP**, senyap di kedua ujung
dibuang (supaya suara mulai tepat sedetik dengan teksnya muncul), tetap
mono 44,1 kHz, MP3 128 kbps. Durasi sesudah trim: good 0,76 s - legendary 2,56 s.

---

## 2. Batch H - kata pujian

Tujuh tingkat, sama dengan `KubikaHud.PraiseFor` / `PraiseColor`:

| Tier | Combo | Kata | Warna |
|---|---|---|---|
| 1 | 2 | GOOD! | hijau (0.55, 0.95, 0.55) |
| 2 | 3 | AWESOME!! | biru (0.35, 0.80, 1.00) |
| 3 | 4 | AMAZING!! | kuning (1.00, 0.85, 0.30) |
| 4 | 5 | FANTASTIC!! | jingga (1.00, 0.60, 0.25) |
| 5 | 6 | INCREDIBLE!! | merah muda (1.00, 0.45, 0.55) |
| 6 | 7 | UNSTOPPABLE!! | ungu (0.80, 0.50, 1.00) |
| 7 | 8 | LEGENDARY!! | emas (1.00, 0.90, 0.40) |

Dengan cap combo 8 dari Batch G, `comboShow` berada di rentang 2..8, jadi
`tier = comboShow - 1` = 1..7 - **tepat tujuh kata**. GOOD! di combo 2,
LEGENDARY!! pas di combo maksimum. Bukan kebetulan: itu memang alasan
KubikaBlast memakai cap 8.

### Kenapa `KubikaHud.cs` tidak disalin

`KubikaHud.cs` membangun `Canvas` + `Text` uGUI dan mencari field privat
`BlastUI` lewat **reflection**. Tetris3D tidak memakai uGUI sama sekali -
seluruh HUD-nya IMGUI (`OnGUI` + `GuiText`/`GlowText` di `Part4`). Jadi yang
diambil hanya DATA-nya (7 kata, 7 warna, kurva animasi); penggambarannya
ditulis ulang memakai `GlowText` milik `Part4`, supaya gaya teksnya - font
Thaleah + outline 8 arah - identik dengan LEVEL UP! dan COMBO x yang sudah ada.

### Animasi & posisi

Kurva `KubikaHud.AnimatePraise`: durasi 0,95 s, melonjak ke 1,18x lalu
mengendap ke 1,0x, memudar sesudah 62%, naik 50 px.

Digambar di `VH * 0.46`, yaitu **di bawah** teks COMBO x (Part4 menaruhnya di
`VH*0.31` dengan tinggi 150, berakhir sekitar `VH*0.40`). Naiknya dibatasi
50 px supaya ujung animasinya tetap tidak menyentuh teks COMBO.

### Deteksi tanpa mengubah Part2

Tidak ada event di Tetris3D. `Part2.ResolveBoard()` menyetel `comboTime = 1.3f`
setiap clear dengan combo >= 2, dan `Part3.Update()` terus menguranginya. Jadi
**"comboTime melompat naik"** hanya bisa terjadi pada saat clear - penanda yang
tepat dan tidak bisa keliru. Pencacah tepinya milik sendiri (`kprPrevComboTime`),
tidak berbagi dengan `prevLines` / `kbgPrevLines` / `kfxPrevLines`.

### Suara: AudioSource sendiri

Aturan keras dari HANDOFF KubikaBlast pasal 10: peran yang mengubah pitch tidak
boleh berbagi AudioSource dengan klip panjang. `Part2.Sfx()` **menaikkan
sfx.pitch mengikuti combo** (dan `ClearBoard()` harus meresetnya - catatan F8).
Kalau suara pujian menumpang sumber itu, "LEGENDARY!!" akan terdengar melengking
makin tinggi seiring combo. Karena itu dibuat `AudioSource` baru dengan
`pitch = 1f` yang tidak pernah disentuh siapa pun.

Satu sumber juga berarti suara lama otomatis dipotong suara baru - dan itu
memang yang diinginkan: saat cascade cepat, "AMAZING" harus MENGGANTI "GOOD",
bukan menumpuk jadi dua orang bicara bersamaan.

Ikut toggle **SUARA** di menu jeda (`sfxOn`) dan volume `sfxVolume`.
Saat game over, pujian langsung dibungkam (`KprStopVoice`) - catatan pasal 10:
periksa game over SEBELUM perayaan, bunyi game over harus berdiri sendiri.

---

## 3. Batch G - ekonomi

### Masalahnya

`Part2.ResolveBoard()`:
```csharp
pts = columns * cellPoints * rowMult * Mathf.Max(1, comboCount);   // TANPA BATAS
```
Combo 12 = pengali 12x. Satu rentetan beruntung mengalahkan seluruh sisa
permainan, dan papan peringkat jadi lomba "siapa paling beruntung sekali",
bukan siapa paling konsisten.

### Sesudah

```
cap  = 8            (BlastCore.COMBO_CAP)
step = 0.35         (BlastCore.COMBO_STEP)
pengali = 1 + (min(combo, 8) - 1) * 0.35   ->  maksimum 3.45x
```

| Combo | Pengali lama | Pengali baru |
|---|---|---|
| 1 | 1,00x | 1,00x |
| 2 | 2,00x | 1,35x |
| 4 | 4,00x | 2,05x |
| 8 | 8,00x | **3,45x (cap)** |
| 12 | 12,00x | 3,45x |

Penghitung combonya juga dicap, jadi HUD berhenti di "COMBO x8" dan tier pujian
berhenti tepat di LEGENDARY!!.

### Kenapa `Part2.cs` tetap tidak perlu diubah

Rumus lamanya bisa dihitung ulang dari luar: jumlah baris yang hancur =
pertambahan `lines` pada frame itu, dan `comboCount` terlihat apa adanya. Jadi
**selisih** antara rumus lama dan rumus bercap dikoreksi langsung ke `score`.
Hasil akhirnya identik dengan mengedit rumusnya, tanpa menulis ulang file 34 KB
yang penuh perbaikan F1-F13.

Koreksi hanya berjalan saat `comboTime` melompat naik (combo >= 2). Untuk
combo 1, rumus lama dan baru memberi angka yang **sama** (pengali 1), jadi tidak
ada yang perlu dikoreksi - dan itu sekaligus membuat clear dari **item**
(Bom/Palu, yang lewat `ResolveClearsNoSpawn()` dan tidak menyentuh combo) tidak
pernah ikut terkoreksi salah.

### Level dari baris (12 baris / level)

Level lama murni dari skor. Karena pengali combo sekarang dicap, skor tumbuh
lebih tenang, jadi ditambahkan lantai yang pasti: `LINES_PER_LEVEL = 12`.

**Caranya tidak langsung, dan itu disengaja.** `OnLevelUp()` bisa memicu
`StageUp()` yang memanggil `DestroyBoardObjects()`. Part2 hanya pernah
menjalankannya di satu titik aman: papan tenang, `active == null`, di ujung
`ResolveBoard()`. Memanggilnya dari `LateUpdate` berarti papan bisa dihapus
selagi balok masih jatuh atau selagi coroutine clear berjalan -> balok aktif
hilang / NullReference. Jadi yang dilakukan hanya **menurunkan ambang**
(`nextLevelScore`), lalu `RecalcLevel()` milik Part2 yang mengeksekusinya di
waktu yang sudah terbukti aman.

Konsekuensi yang disengaja: level dari baris berlaku pada clear **berikutnya**
(tertunda satu clear), bukan seketika. Aturan F4 - maksimal naik satu level per
clear - tetap utuh.

### PERINGATAN: papan peringkat

Skor sekarang tumbuh **jauh lebih lambat** pada combo tinggi. Skor lama di
leaderboard `tetris3d_global` dibuat dengan pengali tanpa batas, jadi **skor
lama dan baru tidak lagi sebanding**. Pilihan: biarkan (rekor lama jadi tembok
yang hampir mustahil), atau ganti `LB_ID` ke papan baru mis. `tetris3d_v2`.
Keputusan ini belum diambil - `LB_ID` masih `tetris3d_global`.

Kalau ingin membatalkan seluruh Batch G tanpa menghapus file: matikan
`kubikaEconomy` di Inspector.

---

## 4. Batch I - FPS

`Start()` di `Tetris3D.cs` hanya menyetel `Screen.orientation`. Tidak ada satu
file pun yang menyentuh frame rate, jadi HP kelas menengah berjalan di default
platform yang bisa saja 30 fps.

```
QualitySettings.vSyncCount = 0;              // WAJIB dulu, kalau vSync nyala targetFrameRate diabaikan
Application.targetFrameRate = 60;
Screen.sleepTimeout = SleepTimeout.NeverSleep;
```

Sudah diaudit tidak ada yang menyetelnya di: `Tetris3D.cs`, `Part2`, `Part4`,
`Toko`, `UiScale`. Belum diaudit: `Extras`, `Currency`, `Gelembung`,
`Gelembung2`, `AdLoading`, `AdsReviveMrec`, `PetiKoin`, `Saldoku`, `Part3`.
Karena itu penerapannya **3 sapuan lalu berhenti total**, bukan tiap frame -
kalau ternyata ada file lain yang memilikinya, file itu yang menang. Tabel
kepemilikan tidak boleh diperebutkan tiap frame.

---

## 5. Tombol pengatur baru (semua field public BARU, default sudah benar)

| Field | Default | Fungsi |
|---|---|---|
| `kubikaPraise` | `true` | teks pujian |
| `kubikaPraiseVoice` | `true` | suara pujian |
| `kubikaVoiceVolume` | `1` | pengali di atas `sfxVolume` (0..1.5) |
| `kubikaEconomy` | `true` | matikan = balik ke skor lama tanpa batas |
| `kubikaComboCap` | `8` | batas combo |
| `kubikaComboStep` | `0.35` | tambahan pengali per tingkat combo |
| `kubikaLinesPerLevel` | `12` | 0 = matikan level-dari-baris |
| `kubikaPerf` | `true` | terapkan FPS/vSync |
| `kubikaFps` | `60` | 30..120 |

---

## 6. Urutan eksekusi (lengkap sesudah Batch B-I)

| Driver | Order | Tugas |
|---|---|---|
| `KubikaTokoHUD` | -26000 | toko + inventaris buff (GUI.depth -900, paling depan) |
| `KubikaBgDriver` | 25000 | latar + gelembung + denyut bloom (Batch B) |
| `KubikaFxDriver` | 25100 | cahaya, cincin, guncang, hit-stop (Batch E+F) |
| `KubikaBalanceDriver` | **25150** | cap combo, koreksi skor, level dari baris, FPS |
| `KubikaPraiseDriver` | **25200** | kata pujian + suara (GUI.depth -500) |

Urutan 25150 sebelum 25200 itu **wajib**: cap combo harus sudah diterapkan ke
`comboShow` sebelum tier kata pujian dibaca. Tanpa itu, combo 9 akan meminta
kata ke-8 yang tidak ada.

GUI.depth: di IMGUI angka **lebih kecil digambar lebih depan**. Pujian di -500 =
di depan HUD utama (0) tapi di belakang panel toko (-900), jadi tidak pernah
menutupi kontrol yang bisa disentuh. Pujian juga **tidak menggambar GUI.Button
sama sekali**, jadi tidak bisa mencuri sentuhan pemain (catatan
`KubikaBubbleHUD`, HANDOFF pasal 5.5).

---

## 7. Cek saat main

### Batch H
1. Combo 2 -> **GOOD!** hijau muncul di bawah teks COMBO x2, tidak bertumpuk.
2. Kata & warnanya naik bertingkat sampai **LEGENDARY!!** emas di combo 8.
3. Combo 9, 10, 12 -> tetap LEGENDARY!!, HUD tetap "COMBO x8".
4. Suaranya mulai **bersamaan** dengan teks (bukan terlambat) - bukti trim senyap bekerja.
5. Kenyaringan ketujuh suara terasa **rata**; GOOD! tidak lebih pelan dari UNSTOPPABLE!!.
6. Cascade cepat: suara lama **terpotong**, tidak pernah dua suara bertumpuk.
7. Combo panjang: suaranya **tidak** ikut melengking makin tinggi (bukti pitch terpisah dari `Sfx()`).
8. Toggle **SUARA** off di menu jeda -> pujian bisu, teks tetap jalan.
9. Balok terakhir memicu game over -> pujian **langsung** hilang & bisu, bunyi game over sendirian.
10. Buka **TOKO** / dialog klaim item -> pujian tidak menembus panel.
11. Tap tombol PUTAR/JATUH tepat saat pujian muncul -> tombol tetap responsif.

### Batch G
12. Combo 8 skornya jauh lebih kecil dari sebelumnya - ini **memang** perubahannya.
13. Clear tunggal tanpa combo: skornya **sama persis** seperti sebelum Batch G.
14. Pakai item **Bom/Palu** -> skornya tidak berubah aneh (koreksi tidak ikut campur).
15. Level tetap naik walau main tanpa combo besar (12 baris / level, berlaku di clear berikutnya).
16. Naik level tetap maksimal satu tingkat per clear.

### Batch I
17. Gerakan terasa 60 fps di HP yang sebelumnya 30.
18. Layar tidak mati sendiri saat lama di menu.

---

## 8. Sisa yang belum dikerjakan

- **Batch D** - musik loop 96 BPM (`BuildMusic`) dari KubikaBlast. Belum diminta.
- **Keputusan `LB_ID`** - lihat peringatan papan peringkat di bagian 3.
- **Uji main Batch B** (8 poin di `HANDOFF-BATCH-B.md`) dan **Batch E+F**
  (13 poin di `HANDOFF-BATCH-EF.md`) masih belum dijalankan.
