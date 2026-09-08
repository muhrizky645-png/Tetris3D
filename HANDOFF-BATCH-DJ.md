# HANDOFF - BATCH D + J (+ alat ukur audio)

**Dibuat:** 8 September 2026
**Status:** Batch D **SELESAI**, Batch J **SELESAI**

Dokumen ini menutup lubang dokumentasi: Batch D dan Batch J sudah mendarat di
`main` tapi belum pernah punya dokumen sendiri, dan alat ukur audio
(`Tetris3D.AudioDebug.cs`) juga belum tercatat di mana pun.

| Batch | Isi | File | Commit |
|---|---|---|---|
| **D** | Musik loop 96 BPM + watchdog audio | `Assets/Tetris3D.Music.cs` | `6c320ef2`, diperbaiki `be242db5` |
| **J** | Jendela combo 20 detik | `Assets/Tetris3D.Balance.cs` | `8bab6db2` |
| - | Laporan audio (alat ukur) | `Assets/Tetris3D.AudioDebug.cs` | `0e4e8dea`, dimatikan di Batch K |

`Tetris3D.cs`, `Part2`, `Part3`, `Part4` **tidak diubah satu baris pun** oleh
ketiganya.

---

## 1. Batch J - jendela combo (jarak kata pujian)

### Yang diminta

"Jarak kata pujian untuk menghancurkan cincin, good - legendary" = berapa lama
pemain masih boleh menghancurkan cincin berikutnya sebelum rentetan pujian
putus dan balik lagi ke GOOD!. Diminta **20 detik**.

### Salah sasaran dulu, dan kenapa

Permintaan pertama berbunyi "cooldown waktunya jadi 20 detik" dan langsung
diterjemahkan jadi `BUFF_AD_COOLDOWN` di `Gelembung.cs` (commit `04331bef`).
Itu **salah** - yang dimaksud jendela combo. `04331bef` sudah **dibatalkan**
di `8bab6db2` (kembali ke `180f` alias 3 menit).

Pelajarannya: di game ini kata "cooldown" bisa menunjuk minimal tiga timer yang
berbeda (`BUFF_AD_COOLDOWN`, `COIN_GAP`, `comboSeconds`). Kalau permintaannya
hanya menyebut angka tanpa nama field, **tanyakan gejalanya di layar** dulu.

### Rantai sebabnya

```
Part2.ResolveBoard()   ->  comboExpire = comboSeconds   (setiap clear)
Part3.Update()         ->  comboExpire dikurangi tiap frame; 0 = combo reset
Part2.ResolveBoard()   ->  comboTime = 1.3f             (clear dgn combo >= 2)
Praise.TickKubikaPraise() -> comboTime melompat naik = picu KprFire(comboShow)
Praise.KprFire()       ->  tier = Clamp(combo - 1, 1, 7) = GOOD! .. LEGENDARY!!
```

Jadi yang mengatur "jarak"-nya adalah **`comboSeconds`**, bukan `KPR_DUR`
(0,95 s = lama kata pujian tampil di layar) dan bukan `comboTime` (1,3 s =
umur teks COMBO x di HUD).

### Kenapa tidak cukup mengganti angkanya di `Tetris3D.cs`

`comboSeconds` itu `public float comboSeconds = 10f;` - field **LAMA**, jadi
nilainya sudah ter-serialize di `SampleScene` pada GameObject `Game`. Nilai
scene **mengalahkan** default C#. Mengganti `10f` jadi `20f` di `Tetris3D.cs`
tidak akan berefek apa pun pada scene yang sudah ada.

Solusinya sama seperti seluruh batch sebelumnya: **field public BARU** +
timpa saat runtime.

| Field | Default | Fungsi |
|---|---|---|
| `kubikaComboWindow` | `20f` (rentang 1..60) | jendela combo; `0` = jangan timpa, pakai nilai scene |

```csharp
// di TickKubikaBalance(), tepat sesudah KeqTickPerf()
if (kubikaComboWindow > 0f && comboSeconds != kubikaComboWindow)
    comboSeconds = kubikaComboWindow;
```

Ditaruh **sebelum** blok `if (!keqSeeded)` supaya sudah berlaku sejak tick
pertama, bukan tick kedua. Perbandingan `!=` membuatnya menulis hanya saat
perlu, dan sekaligus **menyembuhkan sendiri** kalau kelak ada pihak lain yang
mengubah `comboSeconds` di tengah permainan. Saat ini tidak ada satu pun file
lain yang menulis field itu, jadi tidak ada tarik-menarik.

### Yang TIDAK berubah

Cap combo tetap **8**, jadi LEGENDARY!! masih kata terakhir. Jendela yang lebih
panjang membuat LEGENDARY!! lebih **mudah dicapai**, bukan menambah kata baru
di atasnya. Kalau nanti terasa terlalu mudah, turunkan `kubikaComboWindow`,
jangan naikkan `kubikaComboCap` - menaikkan cap akan meminta kata ke-8 yang
tidak ada di `KPR_WORDS`.

---

## 2. Batch D - musik + watchdog audio

Sumber: `BuildMusic` di KubikaBlast (loop 96 BPM). Seluruhnya di
`Assets/Tetris3D.Music.cs`, driver `KubikaMusicDriver` order **25250**
(sesudah Praise 25200, sebelum AudioDebug 25300).

### Bagian yang paling penting: `KmuTickAudioWatchdog()`

Ini sebenarnya perbaikan bug, bukan fitur musik.

`CoGameOverSting()` di `Part3.cs` menurunkan volume AudioSource lalu
mengembalikannya di ujung koroutin. Masalahnya `ClearBoard()` memanggil
`StopAllCoroutines()`. Kalau pemain menekan **ULANG** tepat saat sting game over
sedang berjalan, koroutin itu mati di tengah dan volume **tertinggal di 0** -
seluruh game jadi bisu permanen sampai aplikasi ditutup. Gejalanya sangat mirip
"bug audio acak" dan hampir mustahil dilacak dari laporan pemain.

Watchdog ini memeriksa keadaan audio tiap frame dan memulihkannya. Sekalian juga
menegakkan `kprVoice.mute = !(soundOn && sfxOn)`, karena `KprPlayVoice()`
sendiri tidak memeriksa `soundOn` - tanpa ini, suara pujian tetap bunyi walau
tombol SUARA sudah dimatikan di menu jeda.

Tombol pengatur: **`kubikaAudioHeal`** (matikan = watchdog berhenti, bug
volume-nol bisa kembali).

### Pelajaran CS0104 - jangan diulangi

Commit `6c320ef2` **gagal compile**:

```
Assets\Tetris3D.Music.cs(256,13): error CS0104: 'Object' is an ambiguous
reference between 'UnityEngine.Object' and 'object'
```

Penyebabnya `using System;` di kepala file. Begitu `System` ikut diimpor, nama
pendek `Object` jadi ambigu antara `UnityEngine.Object` dan alias `object`
milik C#. Bootstrap driver di SEMUA file Kubika memakai
`Object.FindFirstObjectByType<...>()`, jadi polanya langsung meledak.

Diperbaiki di `be242db5` dengan tiga langkah:
1. buang `using System;`
2. `Func<...>` -> `System.Func<...>`
3. `Object.` -> `UnityEngine.Object.` di kedua lokasi

**Aturan untuk file Kubika berikutnya:** jangan pakai `using System;`. Tulis
`System.Text.StringBuilder`, `System.Func`, `System.Collections.Generic`
lengkap. `Tetris3D.AudioDebug.cs` sengaja mengikuti aturan ini.

---

## 3. Suara pujian - cerita lengkapnya

### Pemasangan mp3 (SUDAH dikerjakan owner)

```
Assets/Resources/KubikaVoice/good.mp3
Assets/Resources/KubikaVoice/awesome.mp3
Assets/Resources/KubikaVoice/amazing.mp3
Assets/Resources/KubikaVoice/fantastic.mp3
Assets/Resources/KubikaVoice/incredible.mp3
Assets/Resources/KubikaVoice/unstoppable.mp3
Assets/Resources/KubikaVoice/legendary.mp3
```

Nama file harus huruf kecil dan persis seperti di atas (`KPR_FILES` memuatnya
lewat `Resources.Load` tanpa ekstensi). Setelan import yang disarankan:
Decompress On Load, Vorbis ~70%, Force To Mono, Preload Audio Data.

### Penyebab sebenarnya "pujian bisu" - volume perangkat

Gejalanya: teks GOOD!..LEGENDARY!! muncul, suaranya tidak ada. Dugaan awal
ada dua - mp3 belum terpasang, atau `soundOn`/`sfxOn` mati di scene. **Dua-duanya
salah.** Penyebabnya volume di sisi perangkat yang kekecilan.

Ini titik buta yang perlu diingat: `AudioListener.volume` di dalam game bisa
menunjukkan `1` sementara volume media HP-nya nol. Laporan audio tidak bisa
melihat ke sana. Jadi untuk keluhan "tidak ada suara" berikutnya, **cek volume
perangkat lebih dulu** sebelum membongkar aset atau kode.

### Pelajaran pemrosesan mp3 - dua kesalahan nyata

Kedua kesalahan ini benar-benar terjadi dan sempat mengirim file rusak:

1. **`loudnorm` satu lintasan pada klip pendek.** Filter itu butuh jendela
   minimal ~3 detik untuk mengukur, sedangkan klip pujian 0,76-2,56 detik.
   Hasilnya gain ngawur dan file praktis senyap. **Obatnya dua lintasan
   dengan `linear=true`**: lintasan pertama mengukur, lintasan kedua menerapkan
   angka hasil ukur.
2. **Melaporkan hasil lintasan ANALISIS sebagai hasil akhir.**
   `loudnorm print_format=summary` mencetak angka **input**, bukan output. Angka
   itu sempat dilaporkan seolah-olah bukti file sudah benar - padahal filenya
   senyap. **Aturan: jangan pernah melaporkan angka audio dari lintasan
   analisis. Ukur artefak akhirnya** (`volumedetect` / `ffprobe`). Tanda bahaya
   tambahan: mp3 hasil olah yang ukurannya menyusut lebih dari 20% patut
   dicurigai.

Hasil akhir yang benar - ketujuh klip disetel ke **-16 LUFS / true peak
-1,5 dBTP**, senyap di kedua ujung dibuang, mono 44,1 kHz, MP3 128 kbps:

| File | Durasi | Puncak |
|---|---|---|
| good | 0,758 s | -4,8 dB |
| awesome | 1,358 s | -4,0 dB |
| amazing | 1,620 s | -5,2 dB |
| fantastic | 1,959 s | -5,2 dB |
| incredible | 1,933 s | -6,6 dB |
| unstoppable | 1,358 s | -6,7 dB |
| legendary | 2,560 s | -5,5 dB |

> **Alarm palsu yang dicabut:** `awesome.mp3` dan `unstoppable.mp3` sama-sama
> 22.194 byte / 1,358367 s, dan itu sempat dicurigai sebagai file duplikat.
> MD5-nya berbeda. Penjelasannya: MP3 128 kbps CBR memaketkan audio ke frame
> 1152 sampel, dan kedua klip kebetulan pas 52 frame. Ukuran identik pada CBR
> **bukan** bukti duplikasi.

---

## 4. Alat ukur audio (`Tetris3D.AudioDebug.cs`)

Dibuat karena `Part3.Sfx()` dan `KprPlayVoice()` sama-sama `return` diam-diam
saat gagal. Itu benar untuk rilis, tapi tidak bisa didiagnosis. File ini
mengubah kegagalan senyap menjadi satu blok laporan yang bisa dibaca, **tanpa
mengubah satu pun perilaku rilis**.

Satu hal yang layak diingat dari temuannya: `DrawKubikaPraiseGui()` **tidak
memeriksa tombol suara sama sekali**. Jadi teks pujian yang muncul BUKAN bukti
`soundOn`/`sfxOn` menyala - teks dan suara dua jalur terpisah.

Laporan dibagi 5 bagian: tombol suara, AudioListener & global, AudioSource,
klip prosedural `Part3`, dan 7 mp3 pujian (OK/HILANG). Baris berawalan
`>>> PENYEBAB:` menandai temuan yang menjelaskan kebisuan.

**Status: DIMATIKAN** (`kubikaAudioReport = false`) sejak Batch K, karena
urusannya sudah tuntas. Filenya sengaja tidak dihapus - menyalakan satu centang
lebih cepat daripada menulis ulang alat ukurnya.

> **Jebakan Inspector:** field ini sempat hidup dengan default `true`. Kalau
> `SampleScene` pernah disimpan sesudah commit `0e4e8dea`, nilai `true` sudah
> ter-serialize dan akan mengalahkan default `false`. Kalau laporannya masih
> muncul: GameObject `Game` -> komponen `Tetris3D` -> hilangkan centang
> **Kubika Audio Report** -> simpan scene.

---

## 5. Urutan eksekusi (lengkap, sesudah Batch D + J + K)

| Driver | Order | Tugas |
|---|---|---|
| `KubikaTokoHUD` | -26000 | toko + inventaris buff (GUI.depth -900) |
| `KubikaBubbleHUD` | -25000 | gelembung item (GUI.depth -800) |
| `KubikaBgDriver` | 25000 | latar + gelembung latar + denyut bloom (B) |
| `KubikaFxDriver` | 25100 | cahaya, cincin kejut, guncang, hit-stop (E+F) |
| `KubikaBalanceDriver` | 25150 | cap combo, koreksi skor, level dari baris, FPS, **jendela combo (J)** |
| `KubikaPraiseDriver` | 25200 | kata pujian + suara (GUI.depth -500) |
| `KubikaMusicDriver` | 25250 | musik 96 BPM + watchdog audio (D) |
| `KubikaAudioDebugDriver` | 25300 | laporan audio (mati secara default) |

Dua urutan yang **wajib** dijaga:
- **25150 sebelum 25200** - cap combo harus sudah diterapkan ke `comboShow`
  sebelum tier kata pujian dibaca.
- **25300 paling belakang** - laporan harus membaca keadaan audio SESUDAH semua
  pihak selesai menyentuhnya di frame itu, termasuk watchdog di 25250.

---

## 6. Cek saat main (Batch D + J)

### Batch J
1. Combo 2 -> GOOD!. Tunggu **sekitar 15 detik** lalu clear lagi -> harus
   **AWESOME!!**, bukan kembali ke GOOD! (bukti jendela 20 detik berlaku).
2. Tunggu lebih dari 20 detik -> clear berikutnya kembali ke GOOD!.
3. HUD COMBO tetap berhenti di x8 dan kata terakhir tetap LEGENDARY!!.
4. Cek di Inspector: `Kubika Combo Window` terbaca **20**. Kalau di sana masih
   10, berarti yang dilihat field lama `Combo Seconds` - keduanya ada, dan yang
   menang saat runtime adalah `kubikaComboWindow`.

### Batch D
5. Musik berjalan dan loop-nya menyambung tanpa jeda terdengar.
6. Toggle MUSIK di menu jeda -> musik berhenti, SFX tetap jalan.
7. **Uji watchdog (paling penting):** mainkan sampai game over, lalu tekan
   **ULANG** tepat saat bunyi sting game over masih berjalan. Sesudah restart,
   semua SFX harus **tetap terdengar normal**. Kalau bisu, watchdog gagal -
   periksa `kubikaAudioHeal`.
8. Matikan tombol SUARA di menu jeda -> suara pujian juga ikut bisu (bukan cuma
   SFX biasa), teks pujian tetap tampil.
9. Console 0 error.

---

## 7. Sisa yang masih terbuka

- **`blockEmission` masih 0,14** - satu-satunya angka Batch A yang belum pernah
  ditinjau di atas latar pastel terang Batch B. Batch E menyetel cahaya, bloom,
  dan vignette, tapi **tidak** menyentuh emisi blok.
- **Audit `targetFrameRate` belum tuntas.** Batch I baru mengaudit `Tetris3D.cs`,
  `Part2`, `Part4`, `Toko`, `UiScale`. Belum diaudit: `Extras`, `Currency`,
  `Gelembung`, `Gelembung2`, `AdLoading`, `AdsReviveMrec`, `PetiKoin`,
  `Saldoku`, `Part3`.
- **Cincin kejut saat clear lewat item (Bom/Palu) belum diverifikasi.** Jalur
  `ResolveClearsNoSpawn()` diduga tidak menyetel `clearing`, sedangkan pemindai
  cincin Batch F hanya jalan saat `clearing == true`. Kalau dugaan ini benar,
  Bom dan Palu tidak mengeluarkan cincin sama sekali.
- **`LB_ID` tetap `tetris3d_global`** - keputusan owner 8 September 2026: pakai
  yang ada dulu, tidak reset ke `tetris3d_v2`. Konsekuensinya rekor lama (dibuat
  dengan pengali combo tanpa batas) jadi tembok yang hampir mustahil dilewati.
- **F9** - perbaikan cepat terakhir, isinya masih belum ditentukan.
- **Uji main** yang belum pernah dilaporkan: Batch B 8 poin, Batch E+F 13 poin,
  Batch G/H/I 18 poin, plus 9 poin di dokumen ini.
