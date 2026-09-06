# HANDOFF — Batch B: Background + Gelembung (adopsi KubikaBlast / "towerblast")

**Dibuat:** 6 September 2026  
**Status:** RENCANA — **belum dieksekusi.** Ditulis lebih dulu atas permintaan owner karena limit hampir habis, supaya bisa dikerjakan kapan saja tanpa audit ulang.  
**Sumber:** `muhrizky645-png/KubikaBlast` → `Assets/Scripts/BlastBackground.cs`  
**Dokumen induk:** `HANDOFF.md`. Dokumen ini pelengkapnya, sama seperti `SETUP-TROUBLESHOOTING.md`. Setelah Batch B dikerjakan, isinya bisa dilebur ke `HANDOFF.md`.

---

## Status adopsi KubikaBlast secara keseluruhan

Owner minta memindahkan **background + SFX + pencahayaan + warna blok** dari KubikaBlast ("towerblast") ke Tetris3D. Dipecah jadi 4 batch:

| Batch | Isi | Status | Commit |
|---|---|---|---|
| A | Pencahayaan (Sun/Fill, 5000K, matikan directional bawaan scene) + warna & emisi blok (palet 5 warna) | **SELESAI** | `198f2432` (`Tetris3D.cs`) |
| B | Background gradien + partikel gelembung | **DOKUMEN INI — belum dikerjakan** | — |
| C | Arsitektur SFX (source per-peran, sting game over, kompensasi polifoni `1/√n`, musik fade) | **SELESAI** | `b3f8c678` (`Part3.cs`) |
| D | Loop musik 96 BPM (`BuildMusic`) | Belum diminta owner | — |

Owner memilih **A & C** dulu. Batch B ini ditunda dan didokumentasikan. Batch D belum diminta.

---

## 0. PERINGATAN PALING PENTING — konflik `BgGradient`

Tetris3D **sudah** punya latar bergradien sendiri. Di `SetupScene()` (`Tetris3D.cs`) ada quad bernama **`BgGradient`** yang:

- di-parent **ke kamera**,
- memakai material `bgMat` dengan warna `bgTop` / `bgBottom` (default `(0.05,0.04,0.14)` → `(0.24,0.07,0.38)`, ungu gelap),
- ditempatkan di `zBg = dist + 120f`, skala `bgScale = zBg * 3f`.

BlastBackground di KubikaBlast **juga** membuat quad `BgGradient` yang di-parent ke kamera. Kalau di-port mentah-mentah, jadi **dua quad latar bertumpuk** → saling menutup / z-fighting, dan yang menang tergantung jarak. 

**WAJIB: ganti latar yang sudah ada, JANGAN tambah quad kedua.** Dua cara aman:

1. **Cara minimal (disarankan untuk pertama):** biarkan quad `BgGradient` + `bgMat` yang sudah ada, cukup **ganti isi teksturnya** dengan gradien 6-preset KubikaBlast (lihat Bagian 2). Paling sedikit menyentuh geometri yang sudah jalan.
2. **Cara penuh:** hapus blok pembuat `BgGradient` lama di `SetupScene()`, ganti dengan tekstur gradien 4×128 dan sistem partikel gelembung ala BlastBackground.

Apa pun caranya, pastikan hanya ADA SATU quad latar.

---

## 1. Tetris3D TIDAK punya event — semua hook harus POLLING

BlastBackground di KubikaBlast bereaksi ke event: emisi gelembung naik saat combo, dan ada kilatan (flash) latar sesudah clear. **Tetris3D tidak punya event sama sekali** (`OnCleared` / `OnLevelUp` tidak ada). Sistem lain yang butuh reaksi — misalnya `Extras.cs` — melakukannya dengan **polling edge** di `Update()`: menyimpan `prevLines` / `prevGameOver` lalu membandingkan tiap frame.

Port Batch B **harus ikut pola polling itu**, bukan event:

- **Boost emisi gelembung saat combo:** baca `comboCount` tiap frame, hitung `boost = 1 + clamp01((comboCount - 1) / 6) * 1.6`.
- **Flash latar sesudah clear:** deteksi tepi kenaikan `lines` (`lines > prevLines`) tiap frame, lalu picu flash. Jangan cari callback clear — tidak ada.

Taruh polling ini di `Part3.Update()` bareng polling yang sudah ada, atau di komponen HUD latar terpisah yang meng-cache referensi `Tetris3D` sekali (jangan `FindFirstObjectByType` tiap frame — lihat Bagian 9 `HANDOFF.md`).

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

---

## 4. Flash sesudah clear + denyut bloom

Dari `BlastBackground.cs`, dipicu saat clear (di Tetris3D: polling tepi `lines > prevLines`):

- Warna flash latar: atas → `(0.92, 0.91, 0.86)` pada kecerahan `b * 0.30`; bawah → `(0.92, 0.91, 0.88)` pada `b * 0.16`.
- `bloomStrength = 0.55` saat flash, lalu `_bloom` meluruh `1.7`/dtk.
- Warna latar kembali ke preset dengan lerp `Time.deltaTime * 2`.
- `Far()` threshold `0.006` (ambang berhenti lerp).

Tetris3D sudah punya `Global Volume` dengan Bloom (lihat Batch A: tint `(1,0.92,1)`, Vignette). Denyut bloom flash ini bisa dijahit ke Bloom yang sudah ada, **bukan** bikin Volume kedua. Sinkronkan lewat `Update()` yang sudah menyetel bloom/vignette.

---

## 5. Rencana implementasi + jebakan Inspector

**Di mana:** paling bersih sebagai bagian dari `SetupScene()` (ganti `BgGradient`) + polling di `Part3.Update()`. Kalau mau terisolasi, buat partial baru `Tetris3D.Background.cs` (pola sama seperti `Extras`, `Gelembung2`) supaya tidak perlu menulis ulang `Tetris3D.cs` penuh.

**JEBAKAN SERIALISASI INSPECTOR (kritis).** Semua field `public` pada `Tetris3D` **diserialisasi ke `SampleScene`** pada GameObject `Game`, dan **nilai scene menang atas default C#**. Artinya:

- `bgTop` dan `bgBottom` **sudah punya nilai tersimpan di scene** (ungu gelap). Mengubah defaultnya di kode **tidak akan berefek** pada scene yang ada.
- **Solusi yang sudah terbukti di Batch A:** JANGAN ubah field lama. Tambah **field `public` BARU** (mis. `kubikaBackground = true`, `bgPreset = -1` untuk acak, `bubbleEmission = 9f`). Field baru belum ada di scene, jadi default C#-nya langsung aktif **tanpa perlu buka Inspector**. Ini persis trik yang dipakai `kubikaPalette` / `sunIntensity` di Batch A.
- Logika: `if (kubikaBackground) { pakai preset + gelembung baru } else { pakai bgTop/bgBottom lama }`. Field lama tetap utuh sebagai jalan mundur.

**Field non-serialisasi** (state runtime seperti timer flash, referensi partikel) tidak butuh langkah Inspector.

---

## 6. Interaksi dengan Batch A (WAJIB dibaca sebelum eksekusi)

Angka cahaya KubikaBlast di Batch A **sengaja diturunkan** (`sunIntensity = 1.25`, `fillIntensity = 0.30`) karena di KubikaBlast latarnya **terang**. Saat ini di Tetris3D latarnya masih gelap, jadi cahaya itu terbaca lebih terang dari seharusnya.

**Begitu Batch B mendaratkan latar terang, keseimbangan cahaya berubah lagi.** Sesudah Batch B:
- Cek ulang `sunIntensity` — mungkin sudah pas, atau malah perlu naik sedikit karena latar terang "memakan" kontras blok.
- Cek `blockEmission` (0.14) — di latar terang, emisi rendah bisa jadi kurang "pop".
- Cek keterbacaan HUD putih di atas latar terang (aturan "atas lebih gelap" di Bagian 2 sudah dirancang untuk ini).

Urutan uji yang benar: pull Batch B → main → baru sentuh angka cahaya kalau perlu.

---

## 7. Pengujian sesudah eksekusi

1. **Hanya ada SATU latar** — tidak ada z-fighting / kedip antara dua quad (peringatan Bagian 0).
2. **Gelembung di belakang papan** (`sortingOrder = -10`), tidak menutupi menara, tidak menyerap tap.
3. **Boost combo** — main sampai combo tinggi, pastikan emisi gelembung naik (bukti polling `comboCount` jalan).
4. **Flash sesudah clear** — clear baris, pastikan latar berkilat lalu meluruh mulus (bukti polling tepi `lines` jalan).
5. **Keterbacaan** — HUD putih + blok masih jelas di atas latar terang; kalau silau, turunkan `sunIntensity` / sesuaikan preset (Bagian 6).
6. **Console 0 error** sesudah pull (Batch A + C terakhir tercatat 0/0/0).

---

## 8. Ringkas: yang tersisa sesudah Batch B

- **Batch D** (musik 96 BPM `BuildMusic`) — belum diminta.
- **`Columns Per Stage` → 2** di Inspector (satu-satunya langkah Inspector yang masih menggantung dari tuning diameter — lihat `HANDOFF.md` Bagian 0.5).
- **F9** — satu-satunya perbaikan cepat yang tersisa.
- FX KubikaBlast lain yang bisa dicuri nanti (dari `BlastGame.cs`): `HitStop`, camera shake MAX-bukan-jumlah, shockwave rings, `AnimateFx` 1.22×→1.85×, `_fxRoot` terpisah.
