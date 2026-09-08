using UnityEngine;

// =====================================================================
//  KUBIKA TOWER - BATCH M : IRAMA COMBO & TAMENG ITEM
// ---------------------------------------------------------------------
//  File TERPISAH (partial) - ADDITIF. TIDAK mengubah Tetris3D.cs, Part2,
//  Part3, Part4, Currency, Praise, maupun Sfx. Satu-satunya file lain yang
//  disentuh Batch M adalah Gelembung2.cs, dan hanya 2 baris penanda per
//  coroutine item (KbtItemBegin / KbtItemEnd) -- tanpa mengubah logika
//  apa pun di dalamnya.
//
//  DUA KELUHAN YANG DIKERJAKAN DI SINI
//  ---------------------------------------------------------------------
//  (B) "kalau combonya berurutan cepat, suaranya seperti susul-susulan"
//  (C) "efek visual item ada durasinya, tapi balok masih jalan, keburu
//      game over duluan" (terutama Bom & Palu, yang dipakai justru saat
//      keadaan darurat)
//
//  ANGKA YANG MENJELASKAN (B)
//  ---------------------------------------------------------------------
//  Satu mata rantai cascade di ResolveBoard() = 0,56 detik:
//      FlashClear 0,40 s  +  AnimateFall 0,16 s
//  lalu loop langsung balik ke FindFullRows() untuk baris penuh berikutnya.
//
//  Durasi kata pujiannya (hasil ukur file mp3 di Resources/KubikaVoice):
//      good 0,76 | awesome 1,36 | amazing 1,62 | fantastic 1,96
//      incredible 1,93 | unstoppable 1,36 | LEGENDARY 2,56
//
//  Jadi mata rantai berikutnya datang di detik 0,56 sementara katanya masih
//  butuh 1,4-2,6 detik lagi. Dan KprPlayVoice() di Praise.cs hanya punya
//  SATU AudioSource, sehingga kprVoice.Play() MEMOTONG kata sebelumnya di
//  tengah suku kata. Jadi biang keroknya adalah JARAKNYA, bukan bunyinya.
//
//  TEMUAN PENTING: di jalur combo normal TIDAK ADA balok yang sedang jatuh.
//  LockPiece() menyetel active = null lalu memulai ResolveBoard(), dan
//  SpawnPiece() baru dipanggil di baris terakhir. Ditambah Update() punya
//  "if (clearing) return;". Artinya membekukan papan di antara clear TIDAK
//  merugikan pemain sama sekali -- tidak ada input atau balok yang hilang.
//
//  KENAPA BUKAN Time.timeScale = 0 (ini jebakan paling gampang dimasuki)
//  ---------------------------------------------------------------------
//  FlashClear, AnimateFall, ClearedRowGravity, CascadeGravity, dan SEMUA
//  WaitForSeconds di BombBlast/HammerBlast memakai waktu TERSKALA. Kalau
//  timeScale dinolkan, coroutine itu tidak pernah maju -> papan tidak
//  pernah selesai membersihkan diri -> GAME MENGGANTUNG PERMANEN.
//  Maka "freeze" di sini = PELAMBATAN (0,35x), bukan penghentian.
//
//  Yang enaknya: hampir semua efek yang ingin tetap lincah sudah memakai
//  waktu TAK TERSKALA -> teks pujian (TickKubikaPraise), guncangan &
//  cincin gelombang (KfxTickShake/KfxAnimateRing), animasi permata
//  (CurTickGems), dan bel permata (KsfPlayGemTick). Jadi pelambatan ini
//  hanya menyentuh PAPAN, bukan tampilan efeknya.
//
//  CARA MEMBATALKAN (per bagian, tanpa menyentuh kode)
//  ---------------------------------------------------------------------
//      Kubika Combo Gate  = OFF -> papan tidak pernah diperlambat
//      Kubika Item Shield = OFF -> balok jalan lagi selama animasi item
//      Kubika Voice Queue = OFF -> penyaring tier mati (semua kata dibunyikan)
//  Kalau Combo Gate DAN Voice Queue dua-duanya OFF, kepemilikan suara
//  dikembalikan ke Praise.cs seperti sebelum Batch M.
// =====================================================================

public partial class Tetris3D
{
    // ---------- sakelar (SEMUA field BARU -> default kode ini berlaku sampai
    //            SampleScene disimpan ulang; lihat catatan serialisasi di doc) ----------
    public bool kubikaComboGate  = true;   // perlambat papan di antara clear
    public bool kubikaItemShield = true;   // tahan balok selama animasi item
    public bool kubikaVoiceQueue = true;   // saring kata pujian (anti monolog)
    [Range(0.15f, 1f)]   public float kubikaGateSlow    = 0.35f;  // 1 = tanpa efek
    [Range(0.20f, 2.5f)] public float kubikaGateMaxHold = 1.10f;  // detik (tak terskala)

    // ---------- konstanta ----------
    const float KBT_MIN_GAP    = 0.30f;   // jarak minimum antar kata pujian
    const float KBT_ITEM_MAX   = 6.00f;   // batas keras tameng item (watchdog)
    const float KBT_RESTORE_TO = 1.50f;   // batas sabar sebelum paksa timeScale = 1
    const float KBT_WORD_FALLBACK = 0.95f; // dipakai kalau panjang klip tak diketahui

    const string KBT_VOICE_DIR = "KubikaVoice/";
    // Urutan WAJIB sama dengan KPR_FILES di Praise.cs (tier 1..7).
    static readonly string[] KBT_FILES =
    {
        "good", "awesome", "amazing", "fantastic", "incredible", "unstoppable", "legendary"
    };

    // ---------- state ----------
    float kbtPrevComboTime;      // pendeteksi tepi naik comboTime (pola prev* biasa)
    float kbtHoldLeft;           // sisa tahanan papan, detik TAK TERSKALA
    bool  kbtOwnsTime;           // true = timeScale sedang dipegang file ini
    float kbtRestoreWait;        // berapa lama gagal mengembalikan timeScale
    bool  kbtItemOpen;           // penanda dari BombBlast/HammerBlast
    float kbtItemLeft;           // watchdog tameng item
    bool  kbtItemSawClearing;    // sudah melihat clearing == true sesudah item?
    bool  kbtVoiceForced;        // apakah kita yang mematikan kubikaPraiseVoice
    bool  kbtPrevOver;

    AudioSource[] kbtSrc;        // DUA source bergilir -> kata lama tidak terpotong
    int   kbtSrcIdx;
    AudioClip[] kbtClips;
    bool[] kbtTried;
    int   kbtSpokenTier;
    float kbtSpokenAt;
    float kbtVoiceLen;

    // =================================================================
    //  PENANDA ITEM (dipanggil dari Gelembung2.cs, 2 baris per coroutine)
    // =================================================================
    // Sengaja publik & sangat bodoh: hanya menyetel penanda. Semua keputusan
    // ada di KbtTick, jadi kalau sakelarnya dimatikan, penanda ini tetap
    // tercatat tapi tidak berefek apa pun.
    public void KbtItemBegin()
    {
        kbtItemOpen = true;
        kbtItemLeft = KBT_ITEM_MAX;
        kbtItemSawClearing = false;
    }

    public void KbtItemEnd()
    {
        kbtItemOpen = false;
        kbtItemLeft = 0f;
        kbtItemSawClearing = false;
    }

    // =================================================================
    //  SUARA PUJIAN (diambil alih dari Praise.cs)
    // =================================================================
    // Kenapa diambil alih, bukan mengedit Praise.cs: KprPlayVoice() hanya
    // punya satu AudioSource, dan satu source berarti Play() SELALU memotong
    // klip yang sedang berbunyi. Yang dibutuhkan justru sebaliknya -- ekor
    // kata lama dibiarkan berdering menimpa kata baru. Itu mustahil dengan
    // satu source, jadi di sini dipakai DUA yang dipakai bergilir.
    //
    // Teks pujian, warna, skala, dan animasinya TETAP milik Praise.cs. Yang
    // dipindah cuma pemutar suaranya.
    void KbtEnsureVoice()
    {
        if (kbtClips == null)
        {
            kbtClips = new AudioClip[KBT_FILES.Length];
            kbtTried = new bool[KBT_FILES.Length];
        }
        if (kbtSrc != null && kbtSrc.Length == 2 && kbtSrc[0] != null && kbtSrc[1] != null) return;

        kbtSrc = new AudioSource[2];
        for (int i = 0; i < 2; i++)
        {
            GameObject go = new GameObject("KubikaPraiseVoice" + (i + 1));
            go.transform.SetParent(transform, false);
            AudioSource a = go.AddComponent<AudioSource>();
            a.playOnAwake   = false;
            a.loop          = false;
            a.spatialBlend  = 0f;   // 2D penuh
            a.pitch         = 1f;   // JANGAN pernah diubah: ini suara manusia
            a.priority      = 64;
            kbtSrc[i] = a;
        }
    }

    // Penyaring tier. Tanpa ini, rantai combo panjang membunyikan KETUJUH kata
    // berurutan; dengan jarak sekitar 1,2 detik itu jadi monolog 8 detik yang
    // menutupi seluruh permainan.
    //
    // Aturannya: kata pertama rantai selalu dibunyikan, sesudah itu hanya kalau
    // tier melompat minimal 2 tingkat, dan LEGENDARY selalu dibunyikan. Hasil
    // untuk rantai penuh: GOOD -> AMAZING -> INCREDIBLE -> LEGENDARY (4 kata,
    // bukan 7), jadi tiap kata punya ruang bernapas dan tingkatannya tetap
    // terasa menanjak.
    bool KbtShouldSpeak(int tier)
    {
        if (!kubikaVoiceQueue) return true;
        if (Time.unscaledTime - kbtSpokenAt < KBT_MIN_GAP) return false;  // anti senapan mesin
        if (kbtSpokenTier <= 0) return true;                              // kata pembuka rantai
        if (tier >= KBT_FILES.Length) return true;                        // LEGENDARY selalu
        if (tier >= kbtSpokenTier + 2) return true;                       // lompat >= 2 tingkat
        return false;
    }

    void KbtSpeak(int tier)
    {
        if (!(soundOn && sfxOn)) return;
        if (Time.unscaledTime < muteUntil) return;   // hormati jeda sting game over

        KbtEnsureVoice();

        int i = Mathf.Clamp(tier - 1, 0, KBT_FILES.Length - 1);
        if (!kbtTried[i])
        {
            kbtTried[i] = true;   // sekali gagal, jangan coba tiap combo
            kbtClips[i] = Resources.Load<AudioClip>(KBT_VOICE_DIR + KBT_FILES[i]);
        }
        AudioClip c = kbtClips[i];
        if (c == null) return;

        kbtSrcIdx = 1 - kbtSrcIdx;                  // GILIR -> kata lama tidak dipotong
        AudioSource a = kbtSrc[kbtSrcIdx];
        if (a == null) return;

        a.mute   = false;
        a.volume = Mathf.Clamp01(sfxVolume * Mathf.Clamp(kubikaVoiceVolume, 0f, 1.5f));
        a.clip   = c;
        a.Play();

        kbtSpokenTier = tier;
        kbtSpokenAt   = Time.unscaledTime;
        kbtVoiceLen   = c.length;
    }

    void KbtStopVoice()
    {
        if (kbtSrc == null) return;
        for (int i = 0; i < kbtSrc.Length; i++)
            if (kbtSrc[i] != null) kbtSrc[i].Stop();
    }

    // Dipanggil dari Update() driver, BUKAN LateUpdate. Alasannya: semua Update
    // berjalan sebelum semua LateUpdate, jadi kepemilikan suara sudah berpindah
    // sebelum KubikaPraiseDriver (25200) sempat memutar kata di frame yang sama.
    // Tanpa ini, frame pertama bisa membunyikan kata DUA KALI.
    public void KbtClaimVoice()
    {
        bool own = kubikaComboGate || kubikaVoiceQueue;
        if (own)
        {
            if (kubikaPraiseVoice) { kubikaPraiseVoice = false; kbtVoiceForced = true; }
        }
        else if (kbtVoiceForced)
        {
            kubikaPraiseVoice = true;
            kbtVoiceForced = false;
        }
    }

    // =================================================================
    //  DENYUT UTAMA (LateUpdate, sesudah Part3.Update pada frame yang sama)
    // =================================================================
    public void KbtTick()
    {
        float dt = Time.unscaledDeltaTime;
        bool alive = started && !paused && !gameOver;
        bool own = kubikaComboGate || kubikaVoiceQueue;

        // ---------- 1. rantai combo putus -> lupakan tier ----------
        // comboCount = 0 saat jendela combo habis, dan = 1 pada clear pertama.
        if (comboCount <= 1) kbtSpokenTier = 0;

        // ---------- 2. tepi naik comboTime = ADA kata pujian baru ----------
        // Pola yang sama dipakai TickKubikaPraise (kprPrevComboTime). Kedua
        // pendeteksi berdiri sendiri, jadi teks tetap muncul walau suaranya
        // disaring di sini.
        float ct = comboTime;
        if (alive && ct > kbtPrevComboTime + 0.0001f)
        {
            int tier = Mathf.Clamp(comboShow - 1, 1, KBT_FILES.Length);
            if (own && KbtShouldSpeak(tier)) KbtSpeak(tier);

            if (kubikaComboGate)
            {
                // Tahan papan selama sisa kata yang masih berbunyi, DIBATASI
                // kubikaGateMaxHold. Batas ini penting: LEGENDARY 2,56 detik
                // kalau ditahan penuh akan terasa seperti game-nya nge-hang.
                // Dengan batas 1,1 detik, ekor katanya dibiarkan berdering
                // menimpa mata rantai berikutnya -- dan itu tidak lagi jadi
                // masalah karena sekarang ada dua source bergilir.
                float len  = kbtVoiceLen > 0f ? kbtVoiceLen : KBT_WORD_FALLBACK;
                float left = Mathf.Max(0f, (kbtSpokenAt + len) - Time.unscaledTime);
                float cap  = Mathf.Max(0.20f, kubikaGateMaxHold);
                kbtHoldLeft = Mathf.Min(Mathf.Max(kbtHoldLeft, left), cap);
            }
        }
        kbtPrevComboTime = ct;

        // ---------- 3. game over -> senyap & lepas ----------
        // Aturan lama dipertahankan: game over langsung membungkam pujian.
        if (gameOver && !kbtPrevOver) { KbtStopVoice(); kbtHoldLeft = 0f; }
        kbtPrevOver = gameOver;
        if (!alive) kbtHoldLeft = 0f;

        // ---------- 4. TAMENG ITEM (bagian C) ----------
        // Pelepasan punya DUA jalur supaya tidak pernah nyangkut:
        //   * penanda KbtItemEnd() dari akhir coroutine (jalur normal)
        //   * begitu clearing sempat true lalu kembali false -> cascade item
        //     sudah selesai (ResolveClearsNoSpawn menyetel clearing di ujung
        //     BombBlast/HammerBlast). Jalur ini tetap bekerja walau coroutine
        //     dibunuh StopAllCoroutines() oleh ClearBoard().
        //   * ditambah watchdog KBT_ITEM_MAX sebagai jaring terakhir.
        if (kbtItemLeft > 0f)
        {
            kbtItemLeft -= dt;
            if (clearing) kbtItemSawClearing = true;
            else if (kbtItemSawClearing) KbtItemEnd();
        }
        if (!started || gameOver) KbtItemEnd();

        bool shield = kubikaItemShield && kbtItemLeft > 0f && alive;
        if (shield)
        {
            // Part3.Update() menghitung KEDUA timer NAIK:
            //     grounded  -> fallTimer = 0; lockTimer += dt; kunci saat >= LOCK_DELAY
            //     melayang  -> lockTimer = 0; fallTimer += dt; jatuh saat >= interval
            // Jadi menahan keduanya di 0 setiap LateUpdate = balok menggantung
            // dan TIDAK PERNAH terkunci, tanpa risiko "jatuh tiap frame".
            //
            // Sengaja TIDAK menaikkan fallInterval seperti ApplySlow(): kalau
            // item Perlambat aktif di tengah tahanan, ApplySlow menyimpan
            // kbSlowOrig = fallInterval yang sudah digelembungkan -> pemain
            // kena perlambatan permanen sesudahnya.
            if (active != null)
            {
                fallTimer = 0f;
                lockTimer = 0f;
            }
            // Tombol TURUN juga dikunci; kalau tidak, menahan tombol saat
            // animasi item tetap menyeret balok ke bawah dengan interval 0,05 s.
            btnSoftDrop = false;
        }

        // ---------- 5. GATE WAKTU (bagian B) ----------
        if (kbtHoldLeft > 0f) kbtHoldLeft -= dt;

        float slow = Mathf.Clamp(kubikaGateSlow, 0.15f, 1f);

        // Syarat clearing itu yang membuat pelambatan HANYA terjadi di antara
        // clear (saat ResolveBoard berjalan), bukan saat pemain sedang bermain.
        // Tameng item dan gate ini tujuannya BERLAWANAN -- gate memperlambat
        // animasi, tameng justru ingin animasi item jalan penuh -- jadi
        // keduanya dikunci agar tidak pernah aktif bersamaan.
        bool wantGate = kubikaComboGate
                     && kbtHoldLeft > 0f
                     && clearing
                     && alive
                     && !shield
                     && !KubikaHitStopActive   // hit-stop Batch F pemilik lain timeScale
                     && !BubbleClaimOpen;      // klaim gelembung menyetel timeScale = 0

        if (wantGate)
        {
            // Jangan pernah menimpa timeScale = 0 milik orang lain.
            if (Time.timeScale > 0.001f)
            {
                Time.timeScale = slow;
                kbtOwnsTime = true;
                kbtRestoreWait = 0f;
            }
        }
        else if (kbtOwnsTime)
        {
            // Aturan kepemilikan meniru KubikaHitStop() di Impact.cs: hanya
            // kembalikan kalau nilainya MASIH milik kita, dan jangan pernah
            // melepas kepemilikan tanpa benar-benar mengembalikannya (kalau
            // dilepas mentah-mentah, hit-stop yang mengambil alih akan
            // memulihkan ke 0.35 dan tidak ada lagi yang mengembalikan ke 1).
            float ts = Time.timeScale;
            if (ts >= 0.999f)
            {
                kbtOwnsTime = false; kbtRestoreWait = 0f;              // sudah normal
            }
            else if (Mathf.Abs(ts - slow) < 0.05f)
            {
                Time.timeScale = 1f; kbtOwnsTime = false; kbtRestoreWait = 0f;
            }
            else
            {
                kbtRestoreWait += dt;
                if (kbtRestoreWait > KBT_RESTORE_TO && !BubbleClaimOpen && !KubikaHitStopActive)
                {
                    Time.timeScale = 1f; kbtOwnsTime = false; kbtRestoreWait = 0f;
                }
            }
        }
    }

    // Jaring pengaman statis, dipakai driver kalau instance game hilang
    // (ganti scene / dihancurkan) sementara timeScale masih tertahan.
    // Pola yang sama dengan KubikaEndHitStop() di Impact.cs.
    public static void KubikaEndBeatGate()
    {
        if (Time.timeScale > 0.001f && Time.timeScale < 0.999f) Time.timeScale = 1f;
    }
}

// =====================================================================
//  Driver. Urutan 25210 = SESUDAH KubikaPraiseDriver (25200) supaya
//  comboShow/comboTime pada frame ini sudah final, dan SEBELUM
//  KubikaMusicDriver (25250) / KubikaSfxDriver (25275).
// =====================================================================
[DefaultExecutionOrder(25210)]
public class KubikaBeatDriver : MonoBehaviour
{
    Tetris3D game;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        GameObject go = new GameObject("KubikaBeatDriver");
        DontDestroyOnLoad(go);
        go.AddComponent<KubikaBeatDriver>();
    }

    void Find()
    {
        if (game == null) game = UnityEngine.Object.FindFirstObjectByType<Tetris3D>();
    }

    // Update: semua Update berjalan sebelum semua LateUpdate, jadi kepemilikan
    // suara berpindah sebelum Praise (25200) sempat memutar kata.
    void Update()
    {
        Find();
        if (game != null) game.KbtClaimVoice();
    }

    void LateUpdate()
    {
        Find();
        if (game != null) game.KbtTick();
        else Tetris3D.KubikaEndBeatGate();
    }
}
