# HANDOFF - BATCH N
## Jeda NYATA antar cincin + semua kata pujian dibunyikan lagi

| PEMBARUAN | Tanggal | Isi |
|---|---|---|
| v1.0 | 2026-09-09 | Dokumen dibuat. Batch N 1/4 s.d. 4/4 selesai dipush. |

Commit Batch N:

| # | Commit | Berkas | Isi |
|---|---|---|---|
| 1/4 | `a0eeac7c` | `Assets/Tetris3D.Beat.cs` | Tulis ulang: `KbtWaitPraise()` masuk, gerbang timeScale + penyaring tier keluar |
| 2/4 | `5ad949be` | `Assets/Tetris3D.Part2.cs` | 1 baris `yield` di loop `ResolveBoard()` (+17/-0) |
| 3/4 | `9e736c8d` | `Assets/Tetris3D.Gelembung2.cs` | 1 baris `yield` di loop `ResolveClearsNoSpawn()` (+9/-0) |
| 4/4 | (dokumen ini) | `HANDOFF-BATCH-N.md` + baris PEMBARUAN di `HANDOFF-BATCH-M.md` | Dokumentasi |

Dokumen terkait: `HANDOFF-BATCH-M.md` (Bagian B-nya DICABUT oleh dokumen ini; Bagian A & C masih berlaku), `HANDOFF-BATCH-L.md` (SFX), `HANDOFF-BATCH-GHI.md` (kata pujian), `HANDOFF-BATCH-EF.md` (hit-stop), `HANDOFF-BATCH-DJ.md` (musik & jendela combo), `HANDOFF-BATCH-B.md` (latar).

---

## 1. Dua keluhan yang memicu Batch N

**Keluhan 1 (kutipan):** "sekarang kadang bunyi kadang engga, good nya bunyi awesome nya engga, amazing bunyi, incrediblenya engga dst. itu kenapa?"

**Keluhan 2 (kutipan):** "biarkan ketika satu cincin hancur, game seperti terjeda, sampai efek suara pujian + visualnya selesai, trus lanjut ke combo selanjutnya. itu kasus ketika cincin hancur 1-1, soalnya kadang ketika 1 cincin penuh, yg atasnya kan turun, trus bisa hancurin 1 cincin lagi, kadang bisa beruntun sampe 3x"

Keduanya berujung ke satu akar yang sama: **Batch M salah membaca kata "freeze"**, lalu menutupi akibatnya dengan penyaring suara.

---

## 2. Kesalahan rancangan Batch M, ditulis terbuka

### 2a. Penyaring tier = penyebab "kadang bunyi kadang engga"

Itu **bukan bug**, itu perilaku yang sengaja dikirim di Batch M lewat `KbtShouldSpeak()`:

```csharp
if (tier >= kbtSpokenTier + 2) return true;   // harus melompat 2 tingkat
return false;
```

Karena combo naik **satu** tingkat per cincin, syarat "lompat 2" tidak pernah terpenuhi di cincin berikutnya. Hasilnya selang-seling:

| Combo | Kata di layar | Suara (Batch M) | Suara (Batch N) |
|---|---|---|---|
| 2 | GOOD! | bunyi | bunyi |
| 3 | AWESOME!! | **senyap** | bunyi |
| 4 | AMAZING!! | bunyi | bunyi |
| 5 | FANTASTIC!! | **senyap** | bunyi |
| 6 | INCREDIBLE!! | bunyi | bunyi |
| 7 | UNSTOPPABLE!! | **senyap** | bunyi |
| 8 | LEGENDARY!! | bunyi | bunyi |

Teksnya tetap muncul ketujuh-tujuhnya (itu milik `Praise.cs`, tidak disaring), jadi mata dan telinga tidak sinkron - persis yang dilaporkan.

Lebih parah lagi: penyaring itu sebenarnya sudah **tidak diperlukan sejak Batch M itu sendiri**. Masalah asli "kata terpotong" sudah diselesaikan oleh dua AudioSource bergantian. Penyaring ditambahkan untuk gejala yang sudah sembuh. **Pelajaran: jangan menambah penyaring untuk gejala yang sudah diobati oleh perbaikan sebenarnya.**

Di Batch N fungsi `KbtShouldSpeak()` **dihapus seluruhnya**, bukan sekadar dimatikan lewat sakelar, supaya nilai `kubikaVoiceQueue` yang mungkin sudah tersimpan di scene tidak bisa menghidupkannya kembali.

### 2b. Pelambatan waktu tidak pernah bisa jadi "jeda"

Batch M menerjemahkan "freeze" jadi `Time.timeScale = 0.35f`. Itu salah secara mendasar, dan alasannya bisa dibaca dari kode:

| Coroutine | Sumber waktu |
|---|---|
| `FlashClear` (0,40 s) | **terskala** |
| `AnimateFall` (0,16 s) | **terskala** |
| `ClearedRowGravity` / `CascadeGravity` | **terskala** |
| `BombBlast` / `HammerBlast` (`WaitForSeconds`) | **terskala** |

Semua animasi hancurnya cincin memakai waktu terskala. Jadi memperlambat waktu = **membuat animasinya lelet**, bukan menjeda **sesudah** animasi selesai. Yang diminta pemain justru sebaliknya: animasi tetap normal, lalu berhenti sejenak.

Dan `timeScale = 0` lebih parah: coroutine di atas tidak akan pernah maju, papan tidak pernah selesai membersihkan diri, `SpawnPiece()` tidak pernah dipanggil - **game menggantung permanen**. Itu sebabnya Batch M memilih 0,35 dan bukan 0.

---

## 3. Cara Batch N: satu `yield` di dalam loop cascade

`ResolveBoard()` itu sebuah loop:

```
clearing = true;
while (true) {
    full = FindFullRows();
    if (full.Count == 0) break;
    >>> DI SINI <<<
    FlashClear(full);        // 0,40 s
    ...skor & combo...
    ClearedRowGravity(full); // 0,16 s
}
RecalcLevel(); clearing = false; SpawnPiece();
```

Satu baris disisipkan di tanda `>>> <<<`:

```csharp
yield return StartCoroutine(KbtWaitPraise());
```

### Kenapa ini jeda sungguhan, tanpa menyentuh `Time.timeScale`

Selama menunggu, `clearing` masih `true`. `Part3.Update()` sudah punya `if (clearing) return;` di awal. Jadi yang berhenti **dengan sendirinya**:

- balok yang jatuh (`fallTimer`),
- lock delay (`lockTimer`),
- input geser/putar/hard drop,
- hitung mundur `comboExpire`.

Sementara yang **tetap jalan mulus** karena memang memakai waktu tak terskala: animasi teks pujian, guncangan layar, cincin gelombang, animasi butiran permata, dan suara pujiannya sendiri. Persis kombinasi yang diminta: "sampai efek suara pujian **+ visualnya** selesai".

Karena `Time.timeScale` tidak pernah ditulis, **risiko hang sama dengan nol**. `Beat.cs` bukan lagi salah satu pemilik `timeScale`; sekarang tinggal dua: klaim gelembung (0/1) dan hit-stop Batch F (0,08).

### Kenapa `yield`-nya di ATAS loop, bukan di bawah

Ini bagian yang paling gampang salah kalau nanti ada yang merapikan kode ini:

- **Cincin pertama tidak menunggu.** Saat putaran pertama, `kbtWordAt + kbtWordLen` sudah lewat (belum ada kata untuk rantai ini), jadi `KbtWaitPraise()` langsung `yield break`. Hancurnya cincin pertama tetap terasa instan.
- **Cincin terakhir tidak menunda spawn.** Sesudah cincin terakhir, loop keluar lewat `break` di atas - jeda tidak pernah dieksekusi lagi. Kalau `yield`-nya ditaruh di bawah `ClearedRowGravity`, setiap rantai akan berakhir dengan jeda mati sebelum balok berikutnya muncul.

Hasil untuk rantai 3x yang disebut pemain: **hancur - jeda - hancur - jeda - hancur - lanjut**, bukan "jeda - hancur - jeda - hancur - jeda".

### `KbtWaitPraise()` selengkapnya

```csharp
public System.Collections.IEnumerator KbtWaitPraise()
{
    if (!kubikaComboGate) yield break;
    if (!started || gameOver) yield break;
    if (kbtWordAt <= 0f || kbtWordLen <= 0f) yield break;

    float cap   = Mathf.Max(0.20f, kubikaPauseCap);
    float end   = kbtWordAt + Mathf.Min(kbtWordLen, cap);
    float guard = Time.unscaledTime + KBT_WAIT_MAX;   // 4,0 s

    while (true)
    {
        if (!started || gameOver) yield break;
        if (Time.unscaledTime >= guard) yield break;
        if (paused || BubbleClaimOpen)                 // dibekukan orang lain
        { end += Time.unscaledDeltaTime; guard += Time.unscaledDeltaTime; yield return null; continue; }
        if (Time.unscaledTime >= end) yield break;
        yield return null;
    }
}
```

Empat pengaman yang sengaja ditumpuk:

1. **Waktu tak terskala** -> tidak terpengaruh hit-stop maupun item Perlambat.
2. **Pause / klaim gelembung** -> hitungan jeda ditunda, tidak habis diam-diam selagi pemain menatap menu.
3. **`ClearBoard()` memanggil `StopAllCoroutines()`** -> coroutine ini mati bersama `ResolveBoard`, tidak ada yang nyangkut saat restart.
4. **`KBT_WAIT_MAX` 4,0 detik** -> jaring terakhir kalau ada keadaan yang tidak terpikirkan.

### Panjang jeda

Ditetapkan sekali per kata, di tepi naik `comboTime`:

```csharp
float len = (played && kbtVoiceLen > 0f) ? kbtVoiceLen : KBT_WORD_FALLBACK;  // 0,95 s
kbtWordAt  = Time.unscaledTime;
kbtWordLen = Mathf.Max(len, KBT_WORD_FALLBACK) + KBT_BREATH;                 // + 0,10 s
```

`KBT_WORD_FALLBACK` = 0,95 s = `KPR_DUR`, durasi animasi teks pujian. Jadi walaupun suaranya dimatikan atau filenya hilang, **visualnya tetap kebagian ruang** - sesuai permintaan "suara **+** visual".

| Kata | Klip | Jeda nyata (+0,10 s napas) |
|---|---|---|
| GOOD | 0,758 s | 1,06 s (naik ke 0,95 dulu) |
| AWESOME | 1,358 s | 1,46 s |
| AMAZING | 1,620 s | 1,72 s |
| FANTASTIC | 1,959 s | 2,06 s |
| INCREDIBLE | 1,933 s | 2,03 s |
| UNSTOPPABLE | 1,358 s | 1,46 s |
| LEGENDARY | 2,560 s | 2,66 s |

---

## 4. Jalur item ikut dirapikan

Baris `yield` yang sama disisipkan di `ResolveClearsNoSpawn()` (cascade akibat Bom / Palu / Garis). Tanpa itu, jalur item akan tetap terasa susul-susulan sementara jalur combo normal sudah rapi.

Bonus yang datang gratis: selama jeda, `clearing` masih `true`, dan di jalur item balok aktif bisa saja belum terkunci -> `Part3.Update()` berhenti di awal, jadi baloknya ikut diam. Ini **memperkuat** perisai item Bagian C dari Batch M, tidak menggantikannya (perisai tetap dibutuhkan untuk fase **pra-cascade**, saat animasi Bom/Palu berjalan dan `clearing` masih `false`).

---

## 5. Yang dihapus dari `Beat.cs`

| Dihapus | Alasan |
|---|---|
| `KbtShouldSpeak()` | penyebab "kadang bunyi kadang engga" |
| blok gerbang `Time.timeScale` | diganti jeda nyata |
| `kubikaGateSlow` | field gerbang, tidak dipakai lagi |
| `kubikaGateMaxHold` | field gerbang, tidak dipakai lagi |
| `kbtHoldLeft`, `kbtOwnsTime`, `kbtRestoreWait` | state kepemilikan waktu |
| `KBT_RESTORE_TO` | pemulihan timeScale |
| `KBT_MIN_GAP` | jeda minimal antar kata, milik penyaring |
| `kbtSpokenAt` | hanya dipakai penyaring |

**Dua field akan HILANG dari Inspector: `Kubika Gate Slow` dan `Kubika Gate Max Hold`. Itu normal.** Unity membuang data serialisasi yang tidak punya field lagi.

Yang **tidak** diubah: perisai item (Bagian C), `KbtItemBegin/End`, dua AudioSource bergilir, `KbtClaimVoice()` dari `Update()` driver, penghentian suara saat game over, dan urutan eksekusi driver 25210.

---

## 6. Field baru & cara mematikan (revert switch)

| Field | Bawaan | Fungsi |
|---|---|---|
| `kubikaComboGate` | `true` | Matikan -> tidak ada jeda antar cincin, papan lanjut terus |
| `kubikaItemShield` | `true` | Matikan -> `KbtItemBegin/End` jadi no-op |
| `kubikaVoiceQueue` | `true` | Sekarang hanya ikut menentukan kepemilikan suara |
| `kubikaPauseCap` | `3.00` | **BARU.** Batas atas jeda, dalam detik |

**Kenapa namanya `kubikaPauseCap` dan bukan memakai ulang `kubikaGateMaxHold`?** Karena `kubikaGateMaxHold` bawaannya `1.10`, dan kalau `SampleScene.unity` sudah pernah disimpan sesudah Batch M, nilai `1.10` itu ikut tersimpan. Memakai ulang nama lama berarti jeda diam-diam terpotong di 1,1 detik - LEGENDARY (2,56 s) akan terputus, padahal pilihan pemain jelas "penuh sampai kata habis". Nama baru = pasti memakai bawaan kode.

**JEBAKAN SERIALISASI (masih berlaku).** `kubikaPauseCap` baru memakai nilai bawaan kode **sampai `SampleScene.unity` disimpan ulang**. Sesudah itu yang berlaku adalah nilai di scene. Kalau nanti bawaannya diubah di kode tapi tidak berubah di game, cek Inspector dulu.

---

## 7. Risiko & kasus tepi yang masih terbuka

1. **Rantai panjang = jeda menumpuk.** Rantai 3x memakan sekitar 1,06 + 1,46 + 1,72 = **~4,2 detik** total jeda (jeda pertama tidak ada, jadi praktisnya ~2,5-3,2 detik terasa). Rantai 7x bisa lebih dari 10 detik. Kalau terasa lamban, turunkan `kubikaPauseCap` ke 1,5 - itu satu-satunya tuas yang dibutuhkan.
2. **Jeda "warisan" antar balok.** Jendela combo sekarang 20 detik, jadi rantai bertahan lintas balok. Kalau pemain menjatuhkan balok berikutnya **sangat cepat** (kurang dari ~2 detik) dan langsung membuat cincin, cincin itu akan menunggu sisa kata sebelumnya dulu. Secara logika konsisten (kata lama memang masih berbunyi), tapi bisa terasa seperti tersendat. Kalau mengganggu, laporkan - perbaikannya kecil.
3. **HardDrop saat animasi item.** Masih terbuka, sama seperti Batch M. Fase pra-cascade `clearing` masih `false`, jadi menekan DROP bisa memicu lomba `LockPiece() -> ResolveBoard()` vs `ResolveClearsNoSpawn()`. Butuh edit `Part3.cs`.
4. **Kepemilikan suara pujian.** Kalau `kubikaPraiseVoice` dimatikan manual lalu `kubikaComboGate` DAN `kubikaVoiceQueue` dimatikan, `Beat.cs` mengembalikannya ke `true`.
5. **`PolyGain()` masih macet** dan **klik ekor di `MakeArp` / `MakeGameOverSting`** - dua masalah lama, keduanya butuh edit `Part3.cs`.

---

## 8. Titik uji main

1. Cincin hancur 1-1 beruntun: papan benar-benar **berhenti mati** sesudah tiap cincin sampai katanya habis, lalu lanjut. Bukan gerak lambat.
2. Rantai panjang: **ketujuh kata terdengar** - GOOD, AWESOME, AMAZING, FANTASTIC, INCREDIBLE, UNSTOPPABLE, LEGENDARY.
3. Rantai 3x: total jeda terasa wajar. Kalau tidak, turunkan `kubikaPauseCap`.
4. Cincin terakhir: balok berikutnya muncul **tanpa** jeda tambahan.
5. Buka klaim gelembung tepat di tengah jeda: sesudah ditutup, jeda melanjutkan sisanya dan `Time.timeScale` kembali 1,0.
6. Tekan Pause di tengah jeda: hitungan jeda ikut berhenti.
7. Restart / ke menu di tengah jeda (`ClearBoard` -> `StopAllCoroutines`): tidak ada kondisi nyangkut, papan langsung bersih.
8. Bom / Palu saat papan hampir penuh: balok tetap menggantung (perisai Bagian C), dan cascade-nya kini ikut berjeda.
9. Matikan `kubikaComboGate`: cascade instan seperti sebelum Batch M/N.

---

## 9. Tuas penyetelan kalau kurang pas

| Keluhan | Tuas |
|---|---|
| Jeda kelamaan | `kubikaPauseCap` 3,0 -> 1,5 (LEGENDARY ikut terpotong di 1,5 s) |
| Jeda kurang terasa | naikkan `KBT_BREATH` 0,10 -> 0,25 di `Beat.cs` |
| Ingin jeda hanya untuk kata besar | naikkan `KBT_WORD_FALLBACK` atau beri syarat `comboShow >= 4` di `KbtWaitPraise()` |
| Tidak mau ada jeda sama sekali | `kubikaComboGate` = OFF |
| Permata masih terlalu tinggi | `KSF_GEM_TOP` 1,45 -> 1,30 (Batch M Bagian A) |
| Balok masih jalan saat animasi item | `kubikaItemShield` pastikan ON (Batch M Bagian C) |
