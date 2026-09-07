# HANDOFF — Batch B: Background + Gelembung (adopsi KubikaBlast / "towerblast")

**Dibuat:** 6 September 2026  
**Dieksekusi:** 7 September 2026  
**Status:** **SELESAI** — commit `dc01bd29` (`Assets/Tetris3D.Background.cs`). Catatan eksekusi & penyimpangan dari rencana ada di **Bagian 9**.  
**Sumber:** `muhrizky645-png/KubikaBlast` → `Assets/Scripts/BlastBackground.cs`  
**Dokumen induk:** `HANDOFF.md`. Dokumen ini pelengkapnya, sama seperti `SETUP-TROUBLESHOOTING.md`. Sekarang Batch B sudah dikerjakan, isinya bisa dilebur ke `HANDOFF.md`.

---

## Status adopsi KubikaBlast secara keseluruhan

Owner minta memindahkan **background + SFX + pencahayaan + warna blok** dari KubikaBlast ("towerblast") ke Tetris3D. Dipecah jadi 4 batch:

| Batch | Isi | Status | Commit |
|---|---|---|---|
| A | Pencahayaan (Sun/Fill, 5000K, matikan directional bawaan scene) + warna & emisi blok (palet 5 warna) | **SELESAI** | `198f2432` (`Tetris3D.cs`) |
| B | Background gradien + partikel gelembung | **SELESAI** | `dc01bd29` (`Tetris3D.Background.cs`) |
| C | Arsitektur SFX (source per-peran, sting game over, kompensasi polifoni `1/√n`, musik fade) | **SELESAI** | `b3f8c678` (`Part3.cs`) |
| D | Loop musik 96 BPM (`BuildMusic`) | Belum diminta owner | — |

A, B, dan C sudah mendarat. Batch D belum diminta.

---

## 0. PERINGATAN PALING PENTING — konflik `BgGradient`

> **SUDAH TERATASI di `dc01bd29`.** Yang dipakai adalah **Cara 1 (minimal)** di bawah: quad `BgGradient` + `bgMat` lama dipakai ulang, hanya isi teksturnya yang ditukar. **Tidak ada quad latar kedua yang dibuat.** Bagian ini dipertahankan sebagai catatan alasan, bukan lagi risiko terbuka.

Tetris3D **sudah** punya latar bergradien sendiri. Di `SetupScene()` (`Tetris3D.cs`) ada quad bernama **`BgGradient`** yang:

- di-parent **ke kamera**,
- memakai material `bgMat` dengan warna `bgTop` / `bgBottom` (default `(0.05,0.04,0.14)` → `(0.24,0.07,0.38)`, ungu gelap),
- ditempatkan di `zBg = dist + 120f`, skala `bgScale = zBg * 3f`.

BlastBackground di KubikaBlast **juga** membuat quad `BgGradient` yang di-parent ke kamera. Kalau di-port mentah-mentah, jadi **dua quad latar bertumpuk** → saling menutup / z-fighting, dan yang menang tergantung jarak. 

**WAJIB: ganti latar yang sudah ada, JANGAN tambah quad kedua.** Dua cara aman:

1. **Cara minimal (disarankan untuk pertama):** biarkan quad `BgGradient` + `bgMat` yang sudah ada, cukup **ganti isi teksturnya** dengan gradien 6-preset KubikaBlast (lihat Bagian 2). Paling sedikit menyentuh geometri yang sudah jalan. ← **INI YANG DIPAKAI**
2. **Cara penuh:** hapus blok pembuat `BgGradient` lama di `SetupScene()`, ganti dengan tekstur gradien 4×128 dan sistem partikel gelembung ala BlastBackground.

Apa pun caranya, pastikan hanya ADA SATU quad latar.

---

## 1. Tetris3D TIDAK punya event — semua hook harus POLLING

BlastBackground di KubikaBlast bereaksi ke event: emisi gelembung naik saat combo, dan ada kilatan (flash) latar sesudah clear. **Tetris3D tidak punya event sama sekali** (`OnCleared` / `OnLevelUp` tidak ada). Sistem lain yang butuh reaksi — misalnya `Extras.cs` — melakukannya dengan **polling edge** di `Update()`: menyimpan `prevLines` / `prevGameOver` lalu membandingkan tiap frame.

Port Batch B **harus ikut pola polling itu**, bukan event:

- **Boost emisi gelembung saat combo:** baca `comboCount` tiap frame, hitung `boost = 1 + clamp01((comboCount - 1) / 6) * 1.6`.
- **Flash latar sesudah clear:** deteksi tepi kenaikan `lines` (`lines > prevLines`) tiap frame, lalu picu flash. Jangan cari callback clear — tidak ada.

Taruh polling ini di `Part3.Update()` bareng polling yang sudah ada, atau di komponen HUD latar terpisah yang meng-cache referensi `Tetris3D` sekali (jangan `FindFirstObjectByType` tiap frame — lihat Bagian 9 `HANDOFF.md`).

> **CARA EKSEKUSI:** dipakai penghitung tepi **sendiri** (`kbgPrevLines`), **bukan** menumpang `prevLines` milik `Extras.cs`. Alasannya `prevLines` sudah dikonsumsi haptic di `Part3.Update()` (`if (lines > prevLines) Haptic(30); prevLines = lines;`) — kalau ikut dipakai, siapa pun yang jalan lebih dulu akan menelan tepi itu dan yang belakangan tidak pernah kebagian. Dua penghitung terpisah = dua reaksi independen, tidak saling makan.

---

## 2. Spek gradien (dari `BlastBackground.cs`)

6 pasang preset warna (atas/bawah). Aturan desainnya: **tidak ada channel ≥ 0.90**, saturasi rendah-sedang, kontras atas-bawah sempit, dan **atas lebih gelap dari bawah** supaya HUD putih tetap terbaca.

**TopColors[6]:**
```
(0.52, 0.68, 0.80)
(0.64, 0.62, 0.80)
(0.50, 0.72, 0.70)
(0.84, 0.68, 0.58)
(0.58, 0.74, 0.60)
(0.84, 0.66, 0.68)
```

**BottomColors[6]:**
```
(0.86, 0.84, 0.76)
(0.86, 0.82, 0.85)
(0.82, 0.86, 0.78)
(0.87, 0.83, 0.74)
(0.80, 0.86, 0.80)
(0.84, 0.82, 0.88)
```

- Tekstur gradien **4×128** (lebar 4, tinggi 128), di-`Bilinear`/`Clamp`.
- Quad di-parent ke kamera pada `gdist = clamp(far * 0.5, 20, 500)` (`far` = `Camera.farClipPlane`). Di Tetris3D, quad lama pakai `zBg = dist + 120f` — kalau pakai cara minimal, pertahankan penempatan lama, cukup tukar tekstur.
- Pilih preset acak per sesi, atau ikut `stage` supaya selaras dengan `ApplyStageColors()`.

> **CARA EKSEKUSI:** preset **ikut `level`** (`(level - 1) % 6`), sama seperti KubikaBlast, bukan acak per sesi. Penempatan quad lama (`zBg = dist + 120f`) dipertahankan utuh. Bisa dikunci ke satu preset lewat `kubikaBgPreset = 0..5`; `-1` = ikut level.

> **CATATAN:** preset KubikaBlast ini TERANG. Latar Tetris3D sekarang gelap (ungu). Perubahan ini mengubah nuansa total game — memang itu tujuannya (mengejar look KubikaBlast), tapi lihat Bagian 6: pencahayaan Batch A perlu ditinjau ulang setelah latar jadi terang.

---

## 3. Partikel gelembung

Parameter dari `BlastBackground.cs`:

| Parameter | Nilai |
|---|---|
| `BubbleTint` | `(0.22, 0.30, 0.46)` |
| `BUBBLE_ALPHA` | `0.24` |
| `BUBBLE_TINT_KEEP` | `0.85` |
| `startLifetime` | `8` dtk |
| `maxParticles` | `120` |
| `_baseEmission` | `9` /dtk |
| Boost emisi | `1 + clamp01((Combo - 1) / 6) * 1.6` |
| `sortingOrder` | `-10` (di belakang papan) |
| Tekstur | `SoftDot()` 64×64 (titik lembut buatan) |

- Sistem partikel tunggal, `sortingOrder = -10` supaya selalu di belakang menara.
- Emisi dasar 9/dtk, naik saat combo lewat boost di atas (di-polling — Bagian 1).
- **Waspada tap-steal:** poin 5.5 di `HANDOFF.md` sudah mencatat gelembung item (`KubikaBubbleHUD`) mencuri tap. Gelembung latar ini **murni dekoratif** (partikel, bukan tombol), jadi seharusnya aman — pastikan tidak diberi collider / tidak menyerap input.

> **CARA EKSEKUSI:** semua nilai di tabel dipakai apa adanya. Objeknya `BgBubbles`, tanpa collider dan tanpa `GUI.Button`, jadi tidak mungkin mencuri tap. Penempatan & penskalaan: lihat Bagian 9.

---

## 4. Flash sesudah clear + denyut bloom

Dari `BlastBackground.cs`, dipicu saat clear (di Tetris3D: polling tepi `lines > prevLines`):

- Warna flash latar: atas → `(0.92, 0.91, 0.86)` pada kecerahan `b * 0.30`; bawah → `(0.92, 0.91, 0.88)` pada `b * 0.16`.
- `bloomStrength = 0.55` saat flash, lalu `_bloom` meluruh `1.7`/dtk.
- Warna latar kembali ke preset dengan lerp `Time.deltaTime * 2`.
- `Far()` threshold `0.006` (ambang berhenti lerp).

Tetris3D sudah punya `Global Volume` dengan Bloom (lihat Batch A: tint `(1,0.92,1)`, Vignette). Denyut bloom flash ini bisa dijahit ke Bloom yang sudah ada, **bukan** bikin Volume kedua. Sinkronkan lewat `Update()` yang sudah menyetel bloom/vignette.

> **CARA EKSEKUSI:** dijahit ke Bloom yang sudah ada, **tidak ada Volume kedua**. Kekuatan kilatan diatur `kubikaBgFlash` (default `0.55`). Soal kenapa dijahit dari `LateUpdate` dan bukan `Update`, lihat Bagian 9.

---

## 5. Rencana implementasi + jebakan Inspector

**Di mana:** paling bersih sebagai bagian dari `SetupScene()` (ganti `BgGradient`) + polling di `Part3.Update()`. Kalau mau terisolasi, buat partial baru `Tetris3D.Background.cs` (pola sama seperti `Extras`, `Gelembung2`) supaya tidak perlu menulis ulang `Tetris3D.cs` penuh.

> **CARA EKSEKUSI: dipilih jalur TERISOLASI.** Seluruh Batch B ada di satu file baru `Assets/Tetris3D.Background.cs`. **Nol baris** di `Tetris3D.cs`, `Part2.cs`, `Part3.cs`, `Part4.cs` yang diubah — jadi tidak ada risiko merusak 58 KB kode yang sudah jalan. Caranya: driver kecil `KubikaBgDriver` (pola **sama persis** dengan `KubikaBubbleHUD` di `Gelembung2.cs`) melakukan bootstrap sendiri lewat `[RuntimeInitializeOnLoadMethod]`, meng-cache referensi `Tetris3D` sekali, lalu memanggil `TickKubikaBackground()`. Init-nya lazy sehingga `SetupScene()` tidak perlu memasang apa pun.

**JEBAKAN SERIALISASI INSPECTOR (kritis).** Semua field `public` pada `Tetris3D` **diserialisasi ke `SampleScene`** pada GameObject `Game`, dan **nilai scene menang atas default C#**. Artinya:

- `bgTop` dan `bgBottom` **sudah punya nilai tersimpan di scene** (ungu gelap). Mengubah defaultnya di kode **tidak akan berefek** pada scene yang ada.
- **Solusi yang sudah terbukti di Batch A:** JANGAN ubah field lama. Tambah **field `public` BARU** (mis. `kubikaBackground = true`, `bgPreset = -1` untuk acak, `bubbleEmission = 9f`). Field baru belum ada di scene, jadi default C#-nya langsung aktif **tanpa perlu buka Inspector**. Ini persis trik yang dipakai `kubikaPalette` / `sunIntensity` di Batch A.
- Logika: `if (kubikaBackground) { pakai preset + gelembung baru } else { pakai bgTop/bgBottom lama }`. Field lama tetap utuh sebagai jalan mundur.

**Field non-serialisasi** (state runtime seperti timer flash, referensi partikel) tidak butuh langkah Inspector.

> **CARA EKSEKUSI:** trik field baru dipatuhi. `bgTop`/`bgBottom` **tidak disentuh sama sekali**, jadi **tidak ada langkah Inspector untuk Batch B**. Field baru yang ditambahkan:
>
> | Field | Default | Fungsi |
> |---|---|---|
> | `kubikaBackground` | `true` | `false` = balik ke gradien stage lama (`bgTop`/`bgBottom`) |
> | `kubikaBgBubbles` | `true` | `false` = gradien saja, tanpa gelembung |
> | `kubikaBgPreset` | `-1` | `-1` ikut level, `0..5` kunci satu preset |
> | `kubikaBubbleRate` | `9f` | emisi dasar gelembung /dtk |
> | `kubikaBgFlash` | `0.55f` | kekuatan kilatan sesudah clear |
> | `kubikaBgReactToClears` | `true` | matikan kilatan + burst tanpa mematikan gelembung |
>
> Nama sengaja diberi awalan `kubika`/`kbg` supaya tidak bentrok dengan `kb*` / `BUBBLE_*` milik `Gelembung.cs` (gelembung ITEM — sistem yang sama sekali berbeda) yang berada di partial `Tetris3D` yang sama.

---

## 6. Interaksi dengan Batch A (WAJIB dibaca sebelum eksekusi)

Angka cahaya KubikaBlast di Batch A **sengaja diturunkan** (`sunIntensity = 1.25`, `fillIntensity = 0.30`) karena di KubikaBlast latarnya **terang**. Saat ini di Tetris3D latarnya masih gelap, jadi cahaya itu terbaca lebih terang dari seharusnya.

**Begitu Batch B mendaratkan latar terang, keseimbangan cahaya berubah lagi.** Sesudah Batch B:
- Cek ulang `sunIntensity` — mungkin sudah pas, atau malah perlu naik sedikit karena latar terang "memakan" kontras blok.
- Cek `blockEmission` (0.14) — di latar terang, emisi rendah bisa jadi kurang "pop".
- Cek keterbacaan HUD putih di atas latar terang (aturan "atas lebih gelap" di Bagian 2 sudah dirancang untuk ini).

Urutan uji yang benar: pull Batch B → main → baru sentuh angka cahaya kalau perlu.

> **STATUS: MASIH TERBUKA.** Batch B sudah mendarat, jadi langkah retune cahaya ini **sekarang giliran berikutnya**. Ketiga angka (`sunIntensity`, `blockEmission`, keterbacaan HUD) belum ditinjau ulang di atas latar terang.

---

## 7. Pengujian sesudah eksekusi

1. **Hanya ada SATU latar** — tidak ada z-fighting / kedip antara dua quad (peringatan Bagian 0).
2. **Gelembung di belakang papan** (`sortingOrder = -10`), tidak menutupi menara, tidak menyerap tap.
3. **Boost combo** — main sampai combo tinggi, pastikan emisi gelembung naik (bukti polling `comboCount` jalan).
4. **Flash sesudah clear** — clear baris, pastikan latar berkilat lalu meluruh mulus (bukti polling tepi `lines` jalan).
5. **Keterbacaan** — HUD putih + blok masih jelas di atas latar terang; kalau silau, turunkan `sunIntensity` / sesuaikan preset (Bagian 6).
6. **Console 0 error** sesudah pull (Batch A + C terakhir tercatat 0/0/0).

Tambahan sesudah eksekusi:

7. **Naik babak tidak mengembalikan latar ungu.** `ApplyStageColors()` masih hidup dan masih menulis `bgMat` saat naik babak; latar Kubika merebutnya balik di frame yang sama (Bagian 9). Kalau sempat terlihat kedip ungu satu frame saat naik babak, itu titik yang harus diperiksa.
8. **Latar tetap hidup saat dialog klaim item terbuka** — dialog itu menyetel `Time.timeScale = 0` (Bagian 9).

---

## 8. Ringkas: yang tersisa sesudah Batch B

- **Retune cahaya Batch A di atas latar terang** — Bagian 6, sekarang jadi giliran berikutnya.
- **Batch D** (musik 96 BPM `BuildMusic`) — belum diminta.
- **F9** — satu-satunya perbaikan cepat yang tersisa.
- FX KubikaBlast lain yang bisa dicuri nanti (dari `BlastGame.cs`): `HitStop`, camera shake MAX-bukan-jumlah, shockwave rings, `AnimateFx` 1.22×→1.85×, `_fxRoot` terpisah.

> Baris **`Columns Per Stage` → 2** dihapus dari daftar ini: owner mengonfirmasi langkah Inspector tuning diameter (`Cell Points = 12`, `Max Columns = 24`, `Columns Per Stage = 2`) **sudah dikerjakan**. Tidak ada lagi langkah Inspector yang menggantung.

---

## 9. Catatan eksekusi — keputusan yang TIDAK ada di rencana

Lima hal ini muncul saat implementasi dan tidak terpikirkan waktu dokumen ini ditulis. Semuanya penting kalau nanti kode ini disentuh lagi.

**9.1 `LateUpdate`, bukan `Update`, untuk denyut bloom.**  
`Part3.Update()` menulis `bloom.intensity.value = bloomIntensity` **tiap frame**. Urutan eksekusi antar-MonoBehaviour di Unity tidak terdefinisi secara default, jadi kalau tick latar jalan di `Update()`, denyut bloom kilatan akan ketimpa di sebagian frame — kilatannya jadi kedip tak beraturan, dan bug seperti ini sangat sulit dilacak karena tergantung urutan yang bisa berubah. `LateUpdate` dijamin berjalan **sesudah semua** `Update`, jadi nilai denyut selalu yang terakhir menang. Driver juga diberi `[DefaultExecutionOrder(25000)]` sebagai lapis pengaman kedua.

**9.2 `unscaledDeltaTime`, bukan `deltaTime`.**  
Rencana menyebut lerp warna `Time.deltaTime * 2`. Itu tidak aman di Tetris3D: `Gelembung2.cs` menyetel **`Time.timeScale = 0`** selama dialog klaim item terbuka (`OpenBubbleClaim`). Dengan `deltaTime`, animasi latar ikut membeku total di situ. Dipakai `unscaledDeltaTime` untuk lerp warna maupun peluruhan kilatan, jadi latar tetap hidup saat dialog/jeda.

**9.3 Merebut balik `bgMat` dari `ApplyStageColors()` tanpa mengubah `Tetris3D.cs`.**  
`ApplyStageColors()` masih dipanggil saat naik babak dan masih menulis tekstur gradien lamanya ke `bgMat`. Karena diputuskan tidak menyentuh `Tetris3D.cs` (Bagian 5), tidak ada `return` awal yang bisa dipasang di sana. Solusinya satu baris di tick: `if (kbgGradTex == null || bgMat.mainTexture != kbgGradTex) dirty = true;`. Begitu `ApplyStageColors()` menimpa, cek itu langsung menandai dirty dan latar Kubika terpasang lagi di frame yang sama. Baris ini sekaligus jadi jalur init lazy-nya (saat awal `kbgGradTex` masih `null`).

**9.4 Penempatan gelembung tanpa mengubah `ApplyGeometry()`.**  
Gelembung harus di **belakang** menara tapi **di depan** quad latar. Jarak kamera (`dist`) cuma variabel lokal di `ApplyGeometry()`, tidak disimpan ke field. Daripada mengubah `ApplyGeometry()`, jaraknya dibaca balik dari quad latar: `dist = bgTf.localPosition.z - 120f` (karena `ApplyGeometry` menaruhnya di `zBg = dist + 120f`; konstanta 120 diikat di `KBG_QUAD_OFFSET`). Gelembung lalu ditaruh di `pdist = dist + max(radius * 4, 12)`. Bonusnya: perubahan `bgTf.localPosition.z` juga jadi **penanda** bahwa diameter tabung membesar, sehingga tata letak gelembung (`startSize`, `shape.scale`, kecepatan) dihitung ulang otomatis tiap naik babak. Contoh angka di babak awal: `dist ≈ 33.6`, menara di `33.6`, gelembung di `47.2`, quad latar di `153.6` — urutannya benar.

**9.5 Plateau kurva alpha gelembung harus 1, bukan `KBG_ALPHA`.**  
Alpha akhir partikel = `startColor.a × colorOverLifetime.alpha`. Kalau plateau `colorOverLifetime` diisi `0.24` padahal `startColor.a` juga sudah `0.24`, hasilnya `0.0576` — gelembung praktis tidak kelihatan di latar terang. Kurvanya dibuat `0 → 1 → 1 → 0` (murni fade masuk/keluar) supaya `KBG_ALPHA` benar-benar jadi opacity puncak dan tidak dikalikan dua kali. Buffer piksel gradien (`kbgPixels`) juga dipakai ulang antar frame karena fungsi gambar itu jalan tiap frame selama kilatan meluruh — alokasi baru tiap kali akan bikin sampah GC di HP.
