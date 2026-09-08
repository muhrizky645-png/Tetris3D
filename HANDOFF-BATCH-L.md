# HANDOFF — BATCH L : Mesin SFX Baru

**Ruang lingkup:** bunyi **blok mendarat (lock)**, **hard drop**, **rotasi blok**, dan **permata naik satu-per-satu ke HUD**.

---

## PEMBARUAN

| Tanggal | Perubahan | Commit |
|---|---|---|
| 2026-09-08 | Dokumen dibuat. Batch L dikirim: `Assets/Tetris3D.Sfx.cs` + meta. Diverifikasi utuh (blob `a53a3d5e`). | `db5d5ea2` |

**Status:** kode terkirim, **belum diuji main**. Lihat §8.

---

## 1. Kenapa batch ini ada

Keluhan pemilik proyek: *"musiknya sudah oke seperti Tower Blast, cuma SFX seperti blok jatuh, rotasi blok, & ketika permata naik 1-1 ke HUD terlalu jelek jadul nyentrik... masih terlalu kaku, seperti game board jaman dulu."*

Sebab akarnya bukan selera, tapi **dua mesin sintesis yang beda umur hidup bersamaan**:

| Bagian | Mesin | Sejak |
|---|---|---|
| Musik | `BuildMusic()` gaya KubikaBlast (`MakeClip`, voice berlapis) | Batch D (`6c320ef2` / `be242db5`) |
| SFX | **`MakeTone()` lama di `Part3.cs`** | sebelum Batch A |

Jadi musik sudah pindah, SFX belum. Batch L memindahkan SFX ke mesin yang sama gayanya.

---

## 2. Diagnosis — enam cacat nyata di `MakeTone()`

Semua dibaca langsung dari `Assets/Tetris3D.Part3.cs` @ `283e09de` (blob `24868ba5`).

### 2.1 Klik di ekor setiap klip — **ini bug, bukan selera**

`MakeTone` memakai waktu **ternormalisasi** dan peredaman:

```csharp
float t = i / (float)n;          // 0..1, BUKAN detik
float dec = Mathf.Exp(-3.2f * t);
```

Di sampel terakhir `t = 1`, jadi `dec = Exp(-3.2) = 0.041`. Gelombang **dipotong mendadak saat amplitudonya masih 4 %** lalu langsung nol. Lompatan itu = bunyi "tik" kering di **setiap** rotasi, **setiap** lock, **setiap** drop.

`MakeClip()` di `KubikaSfx.cs` punya penjaganya, `MakeTone` tidak:

```csharp
float tail = Mathf.Min(1f, (count - i) / (0.003f * SampleRate));   // fade 3 ms
```

Cacat yang sama juga ada di **`MakeArp`** (`Exp(-3.0f * t)` → berakhir di 5 %) dan **`MakeGameOverSting`**, tapi di sana tertutup ekor dengung 0,34 s sehingga jauh kurang terdengar. **Belum diperbaiki** — lihat §6.

### 2.2 Envelope ternormalisasi membuat durasi tak bermakna

Karena `t` selalu 0..1, bentuk kurva peredaman klip 0,07 s dan klip 0,16 s **identik**. Akibatnya drop 0,16 s meredup secara proporsional lebih lambat dalam detik nyata → terdengar loyo dan mendengung, bukan makin berat. Serangannya juga ikut kacau: `atk = Min(1, t / 0.008f)` = 0,8 % dari klip, yaitu 0,56 ms untuk rotasi tapi 1,28 ms untuk drop.

### 2.3 Sapuan nada menyapu seluruh klip → jadi siulan

```csharp
float f = Mathf.Lerp(freq, freqEnd, t);   // t = 0..1 sepanjang SELURUH klip
```

`sfxDrop` = 300 Hz → 90 Hz rata selama 160 ms. Itu bunyi *slide-whistle* "pyoooong", ciri khas era 1980-an. Benturan sungguhan **runtuh** nadanya di 40–60 ms pertama lalu **ditahan**, seperti `_place` di KubikaSfx:

```csharp
Mathf.Lerp(920f, 430f, Mathf.Clamp01(t / 0.06f))   // perhatikan Clamp01
```

### 2.4 "Vibrato"-nya palsu — sumber kesan *nyentrik*

```csharp
f *= 1f + 0.006f * Mathf.Sin(2f * Mathf.PI * 6f * tt);   // 6 Hz
```

Pada klip 70 ms, 6 Hz hanya menyelesaikan **0,42 putaran** — tidak pernah selesai satu siklus. Jadi itu bukan vibrato, melainkan **detune sembarang yang besarnya berubah tergantung durasi klip**. Pada drop 0,16 s jadi ~1 putaran → goyangan yang terdengar. Dibuang total di Batch L.

### 2.5 Tanpa transien, tanpa sub, tanpa perekat

Semua SFX lama = tumpukan sinus murni `sin(f)*0.8 + sin(2.01f)*0.25 + sin(3f)*0.12`. Tidak ada letupan noise di titik kontak, tidak ada sub-bass untuk bobot, tidak ada soft-clip. Sinus tumpuk = organ elektronik.

### 2.6 Nadanya identik persis setiap kali dipicu

**Koreksi catatan lama:** dulu tercatat bahwa `Sfx()` menaikkan `sfx.pitch` sesuai combo. Di `283e09de` itu **tidak benar** — `Sfx()` hanya melakukan `if (src == sfxLong) src.pitch = 1f;`. Jadi tidak ada bug pitch-bend, tapi juga **nol variasi**. Rotasi bisa berbunyi puluhan kali per menit di frekuensi yang sama; telinga langsung membacanya sebagai mesin. Komentar header di `Part3.cs` yang masih menjelaskan tangga pitch combo sudah **basi**.

---

## 3. Diagnosis — permata naik ke HUD (kasus terburuk)

Dibaca dari `Assets/Tetris3D.Currency.cs` @ `283e09de` (blob `a60fc8de`).

### 3.1 Klipnya turun 900 → 70 Hz

```csharp
curSfxCoinSoft = MakeTone("cur_coin_soft", 900f, 0.10f, 0.25f, 0, 70f);
```

Terjun **3,7 oktaf ke bawah** dalam 100 ms = bunyi "jatuh" kartun, kebalikan dari bunyi **memungut**. Permata seharusnya naik.

### 3.2 Setiap butir berbunyi di nada yang sama

Di `CurTickGems()` fase 3, pemanggilnya:

```csharp
if (anyLanded) CurPlayChaChingSoft();   // tanpa indeks apa pun
```

Animasinya jelas naik satu per satu dan makin cepat (`RISE_DUR 0.32` → `RISE_SPEEDUP 0.82` → `RISE_MIN 0.14`, `RISE_OVERLAP 0.70`), tapi suaranya tidak ikut menanjak. Hasilnya bunyi senapan mesin. `PlayGemTick(index)` di KubikaSfx justru menaikkan nada per butir — itulah yang membuatnya terbaca "sedang mengumpulkan".

### 3.3 Penulisan pitch di AudioSource bersama

```csharp
IEnumerator CoChaChing(AudioClip clip)
{
    KbSfxAt(clip, 1.0f);
    yield return new WaitForSeconds(0.07f);
    KbSfxAt(clip, 1.5f);
    yield return null;
    if (sfx != null) sfx.pitch = 1f;
}

void KbSfxAt(AudioClip c, float pitch)   // di Tetris3D.Gelembung.cs
{
    if (!(soundOn && sfxOn) || sfx == null || c == null) return;
    sfx.pitch = pitch;
    sfx.PlayOneShot(c, sfxVolume);       // catat: melewati PolyGain()
}
```

Di Unity, mengubah `AudioSource.pitch` akan **menggeser nada semua `PlayOneShot` yang masih berbunyi di source itu**. `sfx` adalah source yang sama dengan rotasi/lock/drop. Header `KubikaSfx.cs` menyebut pola ini sebagai biang kerusakan dan menyelesaikannya dengan satu source per peran; Tetris3D baru memindahkan `sfxClear`/`sfxLevelUp` ke `sfxLong`.

Skalanya perlu jujur: `sfx.pitch` kembali ke `1f` pada `yield return null`, jadi jendela pembengkokan **hanya ~1 frame (±16 ms)**. Nyata, tapi bukan cacat terbesar. Yang terbesar tetap §3.1 dan §3.2.

### 3.4 `WaitForSeconds` ber-skala

`CoChaChing` memakai `WaitForSeconds`, **bukan** `WaitForSecondsRealtime`. Saat hit-stop Batch F aktif (`timeScale` turun ke 0,08), jeda 0,07 s menjadi **0,875 detik nyata** — dan hit-stop justru terjadi pada clear besar, saat permata paling banyak muncul. Item Perlambat (`SLOW_MULT 2.5`) juga meregangkannya.

### 3.5 `PolyGain()` bisa mengunci volume

`POLY_WINDOW 0.09f`, `POLY_MAX 8`. Ujinya `now - polyTime > POLY_WINDOW`, tapi `polyTime = now` ditulis ulang pada **setiap** panggilan — jadi rentetan bunyi yang tiap jaraknya < 90 ms tidak pernah mereset pencacah; ia memanjat ke 8 dan gain terkunci di `1/sqrt(8) = 0.354`. Semestinya pencacahnya meluruh terhadap waktu, bukan reset-atau-mentok. **Belum diperbaiki** — lihat §6.

---

## 4. Yang dikerjakan Batch L

**File baru:** `Assets/Tetris3D.Sfx.cs` (+ `.meta`, guid `4d6a1f27b8e34c05a9f2b6d8e1c74350`), commit `db5d5ea2`, blob `a53a3d5e`.

**Additif penuh.** `Tetris3D.cs`, `Part2`, `Part3`, `Part4`, `Currency`, `Gelembung`, `Gelembung2` **tidak diubah satu baris pun.**

### 4.1 Mesin baru

`KsfClip(name, dur, gen)` — dua hal yang tidak dimiliki `MakeTone`:

* `t` dihitung dalam **detik** (`i / rate`), bukan ternormalisasi
* **fade ekor 3 ms** wajib (`KSF_TAIL`) → klip tidak pernah dipotong mendadak

Pembantu: `KsfSine`, `KsfTri`, `KsfNoise`.

### 4.2 Rancangan tiap suara

| Suara | Durasi | Lapisan |
|---|---|---|
| **Rotasi** | 0,055 s | tik noise `Exp(-t·620)` + dua sinus mekanis 1420/2130 Hz + sapuan udara **22 ms pertama saja** + `Exp(-t·78)`. Sengaja pelan — dia yang paling sering berbunyi |
| **Lock** | 0,13 s | noise kontak `Exp(-t·420)` + badan runtuh **232→78 Hz dalam 45 ms lalu ditahan** + sub 62 Hz + ketukan kayu 505/760 Hz + `tanh(mix·1.15)` |
| **Hard drop** | 0,19 s | desau udara 28 ms → benturan mulai di t=24 ms, runtuh **300→58 Hz dalam 50 ms** + sub 52 Hz (lebih panjang) + *crack* + `tanh(mix·1.20)`. Jelas lebih berat dari lock |
| **Bel permata** | 0,20 s | bel dua nada **NAIK C6→E6** (1046,5 → 1318,5 Hz) + kilau oktaf 2093 Hz + *ping* |
| **Cha-ching klaim** | 0,30 s | arpeggio tiga nada **C6–E6–G6** + kilau 3136 Hz |

### 4.3 Variasi tanpa menulis pitch

`KSF_VARIANTS = 4` varian pra-render per suara (detune halus per varian), **digilir tiap frame** oleh driver:

```csharp
sfxRotate = ksfRot[a];  sfxLock = ksfLok[b];  sfxDrop = ksfDrp[c];
```

Sengaja **tidak** memakai `Random.Range` pada `AudioSource.pitch`, karena itu persis penyakit §3.3. `Part2` tetap memanggil `Sfx(sfxRotate)` seperti biasa; yang berubah cuma isi field-nya. Aman terhadap `Sfx()` karena router di sana hanya membandingkan identitas `sfxClear`, `sfxLevelUp`, dan `sfxGameOver` — ketiganya **tidak** digilir.

Urutan per frame: semua `Update()` (bunyi dimainkan) → `LateUpdate()` driver (varian maju). Jadi bunyi berikutnya hampir selalu memakai klip yang berbeda.

### 4.4 Permata: diambil alih tanpa mengedit `Currency.cs`

`curSfxCoin` dan `curSfxCoinSoft` di-assign **malas** (`if (… == null)`), jadi partial baru boleh mengisinya lebih dulu:

| Field | Diisi | Efek |
|---|---|---|
| `curSfxCoinSoft` | klip **bisu** 16 sampel | `CurPlayChaChingSoft()` tetap jalan tapi tak bersuara |
| `curSfxCoin` | klip **bel klaim penuh** | `CoChaChing` yang sudah ada memainkannya 2× (pitch 1,0 lalu 1,5) → justru jadi cha-ching naik yang enak |

Bunyi permata yang benar dimainkan sendiri lewat **deteksi tepi naik `curGemPulse`**:

```csharp
bool rising = (curGemPhase == 2) && curGems3D != null && curGems3D.Count > 0;
if (!rising) ksfGemIndex = 0;
float pulse = curGemPulse;
if (rising && pulse > ksfPrevPulse + 0.0001f) KsfPlayGemTick();
ksfPrevPulse = pulse;
```

`curGemPulse` di-set `0.32f` tepat saat satu permata masuk chip lalu menyusut tiap frame, jadi kenaikan nilainya = ada permata tiba. Karena driver berjalan di `LateUpdate` sesudah `CurTickGems()` pada **frame yang sama**, tidak ada geseran satu frame.

`KsfPlayGemTick()` — tiga hal yang membuatnya terbaca "sedang mengumpulkan":

* nada **naik** per butir: `Min(1.9f, 1f + i * 0.055f)`
* volume **meredup**: `Lerp(0.78f, 0.42f, Clamp01(i / 12f)) * kubikaGemVolume`
* **rate-limit** 35 ms (`KSF_GEM_GAP`) supaya rentetan tidak jadi bubur

Dimainkan di **`ksfGemSrc`, AudioSource milik sendiri** (child dari GameObject `Audio`, nama `KubikaGemAudio`). Inilah yang membuat penulisan pitch di sini aman. Juga menghormati `muteUntil` supaya tidak menabrak sting game over.

---

## 5. Field baru & cara revert

Semua field **BARU**, jadi default kode berlaku (belum pernah ter-serialisasi di `SampleScene`).

| Field | Default | Fungsi |
|---|---|---|
| `kubikaSfxRetune` | `true` | **OFF = kembali total ke SFX lama** |
| `kubikaSfxVariants` | `true` | giliran varian klip (anti "kaku") |
| `kubikaGemChime` | `true` | bel permata + pengambilalihan klip permata |
| `kubikaGemVolume` | `0.85` | `[Range(0, 1.5)]` |

**Revert cepat:** untang centang **Kubika Sfx Retune** pada GameObject `Game` di Inspector, lalu simpan scene. Tidak perlu revert commit.

**Peringatan serialisasi (sama seperti Errors #19):** begitu `SampleScene` disimpan sekali sesudah Batch L masuk, keempat nilai di atas ikut ter-serialisasi. Sesudah itu mengubah default di kode **tidak lagi berpengaruh** — harus lewat Inspector.

**Driver:** `KubikaSfxDriver`, `[DefaultExecutionOrder(25275)]`, bootstrap otomatis via `RuntimeInitializeOnLoadMethod` — **tidak perlu setting scene apa pun.**

Urutan driver sesudah Batch L:

| Driver | Order |
|---|---|
| `KubikaTokoHUD` | −26000 |
| `KubikaBubbleHUD` | −25000 |
| `KubikaBgDriver` | 25000 |
| `KubikaFxDriver` | 25100 |
| `KubikaBalanceDriver` | 25150 |
| `KubikaPraiseDriver` | 25200 |
| `KubikaMusicDriver` | 25250 |
| **`KubikaSfxDriver`** | **25275** |
| `KubikaAudioDebugDriver` | 25300 (harus tetap terakhir) |

Prefiks nama Batch L: **`ksf*` / `KSF_*`** (belum dipakai batch lain).

---

## 6. Sengaja TIDAK diubah

| Hal | Alasan |
|---|---|
| **Musik** (`MakeMusic`, `BuildMusic` Batch D) | pemilik proyek menyatakan sudah oke seperti Tower Blast |
| `sfxClear`, `sfxLevelUp` (`MakeArp`) | tidak dikeluhkan; klik ekornya tertutup dengung 0,34 s |
| `sfxGameOver` (`MakeGameOverSting`) | tidak dikeluhkan |
| `sfxTick`, `sfxDeny` | tidak dikeluhkan (`sfxTick` = detikan revive) |
| SFX item (`kb_bmbl`, `kb_boom`, `kb_ham`, `kb_tick`) | di luar ruang lingkup |
| Suara pujian (mp3 `KubikaVoice/`) | sudah beres |
| **`PolyGain()` §3.5** | perbaikannya menyentuh `Part3.cs`; ditunda agar batch ini tetap additif |
| **Klik ekor `MakeArp`/`MakeGameOverSting` §2.1** | idem |
| **`WaitForSeconds` di `CoChaChing` §3.4** | kini memainkan klip bisu, jadi tak ada artefak terdengar; hanya coroutine yang hidup lebih lama. Tidak mendesak |

---

## 7. Risiko yang belum diverifikasi

1. **Belum pernah dijalankan di Unity.** Batch L belum di-compile maupun didengar. Yang sudah dipastikan: file terkirim utuh sampai penutup kelas.
2. **Keseimbangan volume antar suara masih perkiraan.** Angka `0.90` / `0.92` sesudah `tanh` dan `0.26`–`0.30` pada rotasi disetel dari analisis, bukan dari mendengar. Kalau rotasi terasa masih menonjol, turunkan pengali `body` di `KsfMakeRotate`; kalau lock kurang berat, naikkan amplitudo `sub`.
3. **`sfxVolume` default 0,5** dan `PolyGain()` bisa turun sampai 0,354 (§3.5), jadi rotasi/lock/drop lewat `Sfx()` bisa terdengar lebih pelan dari bel permata — bel permata **tidak** lewat `PolyGain()`. Kalau timpang, setel `kubikaGemVolume`.
4. **Bel permata melewati `PolyGain()`** karena punya source sendiri. Rate-limit 35 ms adalah satu-satunya pengaman. Pada combo besar, cek apakah perlu diperlambat.
5. **Sub-bass 52–62 Hz tidak akan tereproduksi speaker HP**; yang terdengar adalah harmonik keduanya. Di HP mungkin perlu digeser naik ke 70–90 Hz. Uji di perangkat asli, bukan hanya di Editor.
6. **Pembangunan 15 klip terjadi dalam satu frame** saat pertama kali audio siap (± 2 detik audio, ~350 KB). Mestinya < 5 ms, tapi kalau terasa ada hentakan di awal, sebar `KsfBuild()` ke beberapa frame.
7. **Tangga nada permata di-reset saat `curGemPhase != 2`.** Kalau ada burst baru menumpuk di tengah fase naik (`SpawnGemBurst` mengembalikan fase ke 0), tangganya mulai dari bawah lagi. Perilaku ini disengaja, tapi perlu dikonfirmasi enak didengar.

---

## 8. Titik uji main (14) — belum ada yang dilaporkan

1. Rotasi blok: tidak ada lagi "tik" di ekor bunyinya?
2. Rotasi berulang cepat: terdengar bervariasi, bukan seperti mesin?
3. Rotasi: sudah cukup pelan sehingga tidak mengganggu saat main cepat?
4. Rotasi terhalang → masih memakai `sfxDeny` lama (tidak diubah). Terasa nyambung dengan rotasi baru?
5. Blok mendarat normal: terasa seperti **benturan**, bukan "boop"?
6. Hard drop: jelas **lebih berat** dari lock biasa?
7. Hard drop di HP asli (bukan Editor): sub-bass-nya terdengar atau hilang?
8. Permata naik: nadanya **menanjak** butir demi butir?
9. Permata pada combo besar (12+ butir): tidak jadi bubur dan tidak menjerit?
10. Permata: volumenya seimbang terhadap lock/rotasi?
11. Klaim gelembung Permata: cha-ching-nya enak (dua nada naik)?
12. Clear + hit-stop bersamaan dengan permata muncul: tidak ada bunyi yang teregang aneh?
13. Item Perlambat aktif: bunyi permata tetap normal?
14. Game over: sting-nya masih bersih, bel permata tidak menembus `muteUntil`?

---

## 9. Kaitan dokumen lain

| Dokumen | Kaitan |
|---|---|
| `HANDOFF-BATCH-DJ.md` | Batch D = musik & watchdog audio; Batch J = jendela combo. Batch L **tidak** mengubah keduanya |
| `HANDOFF-BATCH-GHI.md` | suara pujian mp3; akar masalah sunyi dulu = volume perangkat kekecilan |
| `HANDOFF-BATCH-EF.md` | hit-stop `timeScale` yang meregangkan `WaitForSeconds` (§3.4) |
| `HANDOFF-BATCH-B.md` | latar & pencahayaan; `blockEmission` masih terbuka |

**Berkas acuan:** `KubikaBlast/Assets/Scripts/KubikaSfx.cs` @ `eeaff57b` (blob `c47c70f5`) — sumber pola `MakeClip`, `PlayGemTick`, dan satu-source-per-peran.
