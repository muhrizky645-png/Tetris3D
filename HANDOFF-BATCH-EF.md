# HANDOFF - BATCH E + F

Referensi: **KubikaBlast** (repo privat, `main`). Kedua game satu konsep, beda model,
jadi angka-angkanya dipinjam langsung, bukan dikira-kira.

- Batch E sumbernya `KubikaBlast/Assets/Scripts/KubikaLight.cs`
- Batch F sumbernya `KubikaBlast/Assets/Scripts/BlastGame.cs`

Semua kode ada di **satu file baru**: `Assets/Tetris3D.Impact.cs`.
`Tetris3D.cs`, `Part2`, `Part3`, `Part4`, dan `Background` **tidak diubah satu baris pun**.

**Dieksekusi:** 7 September 2026, commit `375e2dd6`.
**Diperbarui:** 8 September 2026 - lihat kotak PEMBARUAN di bawah.

---

## PEMBARUAN 8 September 2026

| Klaim lama di dokumen ini | Keadaan sebenarnya |
|---|---|
| Bagian 5: "Kata pujian = Batch H, belum dikerjakan" | **Selesai** - `bcd0f5b6`, `Assets/Tetris3D.Praise.cs` |
| Bagian 5: "Ekonomi = Batch G" | **Selesai** - `bcd0f5b6`, `Assets/Tetris3D.Balance.cs`. Ternyata `Part2.cs` **tidak perlu** disentuh sama sekali (koreksi skor dari luar) |
| Bagian 5: "KubikaPerf = Batch I" | **Selesai** - `bcd0f5b6`, ikut di `Balance.cs`. Audit `targetFrameRate` **belum tuntas** (9 file belum diperiksa) |
| Bagian 1: retune cahaya selesai | Benar, **kecuali `blockEmission`** yang tidak ikut disetel |

Dokumen batch lanjutannya: `HANDOFF-BATCH-GHI.md` (G, H, I) dan
`HANDOFF-BATCH-DJ.md` (D, J, alat ukur audio).

---

## 1. Batch E - retune cahaya untuk latar TERANG

Batch B mengganti latar jadi pastel terang, tapi angka cahaya masih kalibrasi
latar gelap. KubikaBlast sudah pernah melewati masalah yang sama persis dan
solusinya tercatat di `KubikaLight.cs`: intensity `2 -> 1.25` **dan** indirect
`1 -> 0.7` ("kurangi cahaya pantul yang mencuci warna").

Tetris3D sudah ikut turun ke 1.25 di Batch A, tapi `bounceIntensity` **belum
pernah disetel sama sekali** -> masih default Unity **1.0**. Itu sumber silau
yang tersisa.

| | KubikaBlast | Tetris3D (sebelum) | Tetris3D (sekarang) |
|---|---|---|---|
| Sun intensity | 1.25 | 1.25 | 1.25 |
| Sun bounce / indirect | **0.7** | **tidak disetel (1.0)** | **0.7** |
| Temperature | 5000 K | 5000 K | 5000 K |
| Fill light | tidak ada | 0.30 | **0.18** |
| Ambient (flat) | tidak disentuh | 0.30, 0.30, 0.34 | **0.22, 0.22, 0.25** |
| Bloom threshold | tidak ada bloom | 0.9 | **1.05** |
| Vignette | tidak ada | 0.28, ungu gelap | **0.18, slate netral** |
| Emisi blok | - | 0.14 | **0.14 (TIDAK disetel)** |

Alasan dua angka bloom/vignette (temuan baru, bukan dari KubikaBlast):

- **Threshold 0.9 terlalu rendah.** Preset bawah Batch B luminance-nya sekitar
  0.86, dan kilatan sesudah clear menariknya ke ~0.92. Artinya **latar itu
  sendiri** yang mulai nge-bloom, bukan bloknya. 1.05 menyisakan ruang.
- **Vignette ungu gelap** `(0.04, 0.02, 0.10)` dulu cocok karena latarnya ungu.
  Di atas pastel, sudut layar terbaca seperti **kotor**, bukan seperti bayangan.

> **SISA YANG BELUM DIKERJAKAN: `blockEmission` (0.14).** `HANDOFF-BATCH-B.md`
> bagian 6 meminta tiga hal ditinjau di atas latar terang: `sunIntensity`,
> `blockEmission`, dan keterbacaan HUD. Batch E menyelesaikan yang pertama
> (lewat `bounceIntensity`, yang ternyata biang keroknya) tapi **tidak menyentuh
> emisi blok**. Di latar pastel, emisi rendah bisa membuat blok kurang "pop".
> Ini satu-satunya angka Batch A yang masih menunggu.

### Cara penerapan (penting)

`sunIntensity` / `fillIntensity` / `bloomThreshold` / `vignetteAmount` adalah
field public **lama** - nilainya bisa sudah terkunci di `SampleScene`, jadi
mengubah default di kode tidak dijamin berefek. Karena itu:

- semua angka baru adalah field public **BARU** (`kubikaSunBounce`, dll) -> default C# menang;
- `Part3.Update()` menulis `bloom.threshold.value = bloomThreshold` **tiap frame**,
  jadi yang ditimpa adalah **field-nya**, sekali saja. Tidak ada tarik-menarik per frame.
- Warna vignette tidak ditulis Part3, jadi cukup `Override()` sekali.

Penerapan dijalankan **3 sapuan** lalu berhenti total (0.05 s dan 0.5 s), meniru
`KubikaLight.Apply()` yang dipanggil di `Awake`, `Start`, dan satu frame
sesudahnya. Tiap sapuan sekalian mematikan directional light lain (cahaya dobel
= warna blok keruh).

> **CATATAN 8 September 2026.** Pola "timpa field lama dari field baru" ini
> kemudian dipakai lagi di Batch J untuk `comboSeconds` (field lama, nilai scene
> 10 dtk) yang ditimpa `kubikaComboWindow` (field baru, 20 dtk). Bedanya: di
> sini cukup **sekali** karena `Part3` membaca ulang field-nya tiap frame,
> sedangkan `comboSeconds` dibaca hanya saat clear, jadi di sana timpanya
> dipasang sebagai pemeriksaan `!=` yang menyembuhkan diri sendiri. Lihat
> `HANDOFF-BATCH-DJ.md` bagian 1.

---

## 2. Batch F - cincin kejut, guncang sadar-combo, hit-stop

### 2.1 Cincin kejut

Angka dari `BlastGame.SpawnRingShock` / `AnimateShock`:
radius `1.02x -> 2.1x`, tebal `vSpace * 0.16`, durasi `0.42 s`, ease-out
`1-(1-k)^2`, alpha `0.55 -> 0`, material additive.
Warna ikut combo: `<5` = (1, 0.94, 0.65) - `>=5` = (1, 0.72, 0.35) - `>=7` = (1, 0.55, 0.75).

**Masalahnya: cincin butuh tahu BARIS KE BERAPA yang hancur, dan Tetris3D tidak
punya event.** Jumlah `lines` saja tidak cukup.

Jalan keluarnya: `FlashClear()` menahan baris penuh **0.4 detik sebelum**
grid-nya dikosongkan, dan sepanjang itu `clearing == true`. Jadi baris penuh
dibaca langsung dari `grid` selagi kilatannya berjalan - waktunya malah lebih
pas, cincin muncul bersamaan dengan kilatan. Pemindaian (24x18 sel) hanya jalan
saat `clearing`.

Daftar baris yang sudah diberi cincin dikosongkan setiap `lines` berubah, supaya
cincin **cascade kedua** tidak hilang hanya karena indeks barisnya kebetulan
sama dengan yang sudah tercatat.

> **RISIKO YANG BELUM DIVERIFIKASI (dicatat 8 September 2026).** Ketergantungan
> pada `clearing == true` punya satu titik lemah: clear yang dipicu **item**
> (Bom dan Palu) berjalan lewat `ResolveClearsNoSpawn()` di `Gelembung2.cs`,
> bukan lewat `FlashClear()`. Kalau jalur itu tidak menyetel `clearing`, maka
> pemindai cincin tidak pernah jalan dan **Bom/Palu tidak mengeluarkan cincin
> sama sekali**. Belum diuji. Kalau terbukti, perbaikannya bukan memaksa
> `clearing = true` dari luar (itu akan mengacaukan penjaga di `Part3`),
> melainkan memberi pemindai pemicu keduanya sendiri.

### 2.2 Guncang sadar-combo

`Shake(dur, mag)` yang lama **menimpa** nilai lama dan **mengabaikan combo
sepenuhnya** - clear tunggal terasa identik dengan combo 8.

Sekarang, mengikuti `BlastGame.ApplyImpact`:
`boost = 1 + clamp01((combo-1)/6) * 0.8` (maks 1.8x), digabung **MAX bukan
dijumlahkan**, dengan cap `0.85`.

Angka dasarnya sengaja **sama** dengan `Part2.FlashClear` (`0.26 + rows*0.12`).
Karena penggabungannya MAX, **clear tunggal tanpa combo terasa persis seperti
sebelumnya** - yang berubah hanya puncaknya saat rentetan panjang. Lapisan lama
di `Part3.Update()` tidak dimatikan, hanya dibaca lalu digabung, jadi tidak ada
guncang dobel.

`comboCount` baru dinaikkan `ResolveBoard()` **sesudah** `FlashClear()` selesai,
jadi saat kilatan combo yang dipakai = `comboExpire > 0 ? comboCount + 1 : 1`.
Tanpa koreksi ini, warna cincin & kekuatan guncang selalu terlambat satu clear.

> **CATATAN:** rumus `comboExpire > 0 ? comboCount + 1 : 1` itu kini bergantung
> pada jendela combo yang **20 detik** (Batch J), bukan 10 detik lagi. Efeknya
> hanya menguntungkan: `comboExpire` lebih lama positif, jadi warna cincin dan
> kekuatan guncang lebih sering terbaca sebagai combo lanjutan.

Babak baru (`stage` naik) juga dapat guncang 0.62 + hit-stop 0.12 s.

### 2.3 Hit-stop - bagian paling berbahaya

`Time.timeScale` itu milik bersama. `Gelembung2.OpenBubbleClaim()` menyetelnya
**0** untuk dialog klaim item. Aturan yang dipakai (HANDOFF KubikaBlast pasal 7,
tabel kepemilikan):

1. **Jangan mulai** kalau waktu sudah dibekukan pihak lain (`timeScale <= 0.001`) atau `paused`.
2. Nilai sebelumnya **disimpan**, bukan diasumsikan 1.
3. Kalau di tengah hit-stop `timeScale` berubah jadi bukan nilai kita, berarti
   ada yang mengambil alih -> kita **mundur tanpa menyentuh apa pun**. Dialog
   item menang, bukan efek visual.
4. Basisnya **TIMER, bukan coroutine.** `ClearBoard()` memanggil
   `StopAllCoroutines()`; coroutine hit-stop yang mati di tengah akan
   meninggalkan game di `timeScale 0.08` alias **slow motion permanen**.
5. `KubikaHitStopActive` static + `KubikaEndHitStop()` sebagai jaring pengaman
   kalau instance game hilang.

Hit-stop hanya dipicu saat `rows >= 2` **atau** `combo >= 3`. Kalau setiap clear
tunggal ikut membekukan waktu, yang terasa bukan "mantap" tapi "nyendat".
Durasi 0.05 + rows*0.02 + boost, dibatasi keras 0.20 s, scale 0.08.

> **POLA YANG TERULANG.** Bahaya nomor 4 di atas - koroutin mati kena
> `StopAllCoroutines()` lalu meninggalkan state global rusak - **benar-benar
> terjadi** di tempat lain: `CoGameOverSting()` di `Part3.cs` bisa meninggalkan
> volume AudioSource di 0 alias game bisu permanen. Itulah yang kemudian
> memaksa lahirnya `KmuTickAudioWatchdog()` di Batch D. Jadi aturannya bukan
> cuma untuk hit-stop: **apa pun yang mengubah state global dari dalam koroutin
> wajib punya jalur pemulihan berbasis timer atau watchdog**, karena
> `ClearBoard()` bisa memotongnya kapan saja.

---

## 3. Tombol pengatur baru (semua field public BARU, default sudah benar)

| Field | Default | Fungsi |
|---|---|---|
| `kubikaLightRetune` | `true` | matikan = balik ke cahaya Batch A |
| `kubikaSunIntensity` | `1.25` | key light |
| `kubikaSunBounce` | `0.7` | **indirect multiplier - inti Batch E** |
| `kubikaFillLight` | `0.18` | isian sisi belakang |
| `kubikaAmbient` | `0.22, 0.22, 0.25` | ambient flat |
| `kubikaBloomThreshold` | `1.05` | naikkan lagi kalau latar masih nge-bloom |
| `kubikaVignette` | `0.18` | kekuatan vignette |
| `kubikaVignetteColor` | `0.12, 0.12, 0.14` | slate netral |
| `kubikaShockwave` | `true` | cincin kejut |
| `kubikaComboShake` | `true` | guncang sadar-combo |
| `kubikaHitStop` | `true` | hit-stop |
| `kubikaShakeStrength` | `1` | 0..1, pengali guncang |

---

## 4. Urutan eksekusi

| Driver | Order | Tugas |
|---|---|---|
| `KubikaBgDriver` | 25000 | latar + gelembung + denyut bloom (Batch B) |
| `KubikaFxDriver` | **25100** | retune cahaya, cincin, guncang, hit-stop |

Keduanya `LateUpdate` dan bootstrap sendiri (`AfterSceneLoad` +
`DontDestroyOnLoad`), jadi `SetupScene()` tidak perlu memasang apa pun.
`KubikaFxDriver` sengaja **sesudah** driver latar: tick ini yang menulis posisi
kamera terakhir, sehingga tidak bisa ditimpa lagi lapisan guncang lama.

Urutan lengkap sesudah Batch J ada di `HANDOFF-BATCH-DJ.md` bagian 5
(delapan driver, dari `KubikaTokoHUD` -26000 sampai `KubikaAudioDebugDriver` 25300).

---

## 5. Yang SENGAJA tidak dikerjakan di batch ini

- Batch F versi KubikaBlast juga punya `SpawnColumnShock` dan `AnimateFx`
  (kubus mati yang berpencar). Tetris3D sudah punya `Burst()` 16 partikel di
  `Part2.FlashClear`, jadi menambah keduanya cuma bikin ramai.
  **Status: tetap tidak diambil**, dan ini keputusan desain, bukan pekerjaan tertunda.

Tiga item di bawah dulu tercatat "belum dikerjakan" - **ketiganya sudah selesai**
di commit `bcd0f5b6`, dok `HANDOFF-BATCH-GHI.md`:

- ~~Kata pujian GOOD! -> LEGENDARY!! = **Batch H**~~ -> selesai,
  `Assets/Tetris3D.Praise.cs`. Dugaan di dokumen ini benar: `KubikaHud.cs`
  memang **tidak bisa dicopot mentah** karena mencari field privat `BlastUI`
  lewat *reflection* dan membangun `Canvas`/`Text`, sedangkan Tetris3D pakai
  IMGUI. Yang diambil hanya datanya (7 kata, 7 warna, kurva animasi);
  penggambarannya ditulis ulang memakai `GlowText` milik `Part4`.
- ~~Ekonomi (level dari baris, cap combo 8) = **Batch G**~~ -> selesai,
  `Assets/Tetris3D.Balance.cs`. **Dugaan di dokumen ini SALAH:** ternyata
  `Part2.cs` **tidak perlu disentuh sama sekali**. Rumus skor lamanya bisa
  dihitung ulang dari luar (jumlah baris = pertambahan `lines`, `comboCount`
  terlihat apa adanya), jadi cukup **selisihnya** yang dikoreksi ke `score`.
  Peringatan soal leaderboard tetap berlaku, dan keputusannya sudah diambil:
  `LB_ID` **tetap `tetris3d_global`**.
- ~~`KubikaPerf` (FPS/vSync) = **Batch I**~~ -> selesai, ikut di `Balance.cs`.
  Audit yang diminta dokumen ini baru **sebagian**: `Tetris3D.cs`, `Part2`,
  `Part4`, `Toko`, `UiScale` sudah bersih. **Belum diaudit:** `Extras`,
  `Currency`, `Gelembung`, `Gelembung2`, `AdLoading`, `AdsReviveMrec`,
  `PetiKoin`, `Saldoku`, `Part3`. Karena itu penerapannya dibuat 3 sapuan lalu
  berhenti - kalau ada file lain yang memiliki `targetFrameRate`, file itu yang
  menang.

---

## 6. Cek saat main (Batch E + F)

> **STATUS: BELUM DIJALANKAN.** Tiga belas poin di bawah belum pernah
> dilaporkan hasilnya. Yang paling berharga: **poin 10 dan 11** (kedua-duanya
> menguji apakah `timeScale` bisa tertinggal rusak), lalu **poin 6** yang
> sekaligus menjawab risiko cincin-lewat-item di bagian 2.1.

1. Warna blok terasa lebih **pekat**, bukan pucat - itu efek indirect 1.0 -> 0.7.
2. Latar pastel **tidak** ikut bercahaya sendiri; yang nge-bloom cuma blok & kilatan.
3. Sudut layar tidak terasa kotor keunguan lagi.
4. Clear 1 baris tanpa combo: guncangnya **sama seperti sebelumnya** (bukti MAX bekerja).
5. Combo panjang: guncang jelas lebih keras, tapi kamera tidak terbang (cap 0.85).
6. Cincin muncul **tepat di ketinggian baris** yang hancur, dan ada satu cincin
   per baris saat clear ganda. **Sekalian uji pakai Bom dan Palu** - kalau di
   situ tidak ada cincin sama sekali, dugaan di bagian 2.1 terbukti.
7. Warna cincin berubah di combo 5 dan 7 (kuning -> jingga -> pink).
8. Cascade (clear berantai) tetap mengeluarkan cincin di langkah kedua & ketiga.
9. Clear ganda / combo 3+ terasa "nyendat" sepersekian detik, lalu normal lagi.
10. **Buka dialog klaim item tepat sesudah clear besar** - waktu harus berhenti
    penuh dan **tidak** tertinggal di slow motion sesudah dialog ditutup.
11. Tekan **ULANG / KE MENU** tepat saat cincin masih mengembang - tidak ada
    cincin yang tertinggal membeku, dan kecepatan game normal.
12. Naik babak: guncang lebih besar + hit-stop, latar tidak kembali ungu.
13. Console 0 error.
