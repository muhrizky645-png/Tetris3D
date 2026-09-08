using UnityEngine;

// =====================================================================
//  KUBIKA TOWER - BATCH N : JEDA NYATA ANTAR CINCIN & TAMENG ITEM
// ---------------------------------------------------------------------
//  File TERPISAH (partial) - ADDITIF. TIDAK mengubah Tetris3D.cs, Part3,
//  Part4, Currency, Praise, maupun Sfx. File lain yang disentuh hanya:
//      * Part2.cs      -> 1 baris yield di dalam loop ResolveBoard()
//      * Gelembung2.cs -> 1 baris yield di ResolveClearsNoSpawn()
//                         + 2 baris penanda per coroutine item (Batch M)
//
//  RIWAYAT
//  ---------------------------------------------------------------------
//  Batch M (versi lama): memperlambat Time.timeScale ke 0,35x maksimal
//      1,10 detik di antara clear, DAN menyaring kata pujian supaya rantai
//      panjang tidak jadi monolog.
//  Batch N (file ini) : kedua-duanya DIBONGKAR. Lihat alasannya di bawah.
//
//  KENAPA PENDEKATAN BATCH M SALAH
//  ---------------------------------------------------------------------
//  1. Penyaring tier bikin suara terasa RUSAK, bukan rapi. Aturannya
//     "tier harus melompat minimal 2 tingkat", dan karena combo naik satu
//     per satu hasilnya selang-seling: GOOD bunyi, AWESOME senyap,
//     AMAZING bunyi, FANTASTIC senyap... Padahal TEKS-nya tetap muncul
//     ketujuh-tujuhnya, jadi mata dan telinga tidak sinkron. Laporan
//     pemain: "kadang bunyi kadang engga, itu kenapa?"
//
//  2. Pelambatan timeScale tidak pernah bisa memberi "jeda". FlashClear
//     (0,40 s), AnimateFall (0,16 s), ClearedRowGravity, CascadeGravity,
//     dan SEMUA WaitForSeconds di BombBlast/HammerBlast memakai waktu
//     TERSKALA. Memperlambat waktu = membuat animasi hancurnya ikut
//     LELET, bukan menjeda SESUDAH animasi selesai. Dan timeScale = 0
//     jauh lebih parah: coroutine itu tidak akan pernah maju, papan tidak
//     pernah selesai membersihkan diri -> GAME MENGGANTUNG PERMANEN.
//
//  YANG DIMINTA PEMAIN (kutipan)
//  ---------------------------------------------------------------------
//  "biarkan ketika satu cincin hancur, game seperti terjeda, sampai efek
//   suara pujian + visualnya selesai, trus lanjut ke combo selanjutnya.
//   itu kasus ketika cincin hancur 1-1, soalnya kadang ketika 1 cincin
//   penuh, yg atasnya kan turun, trus bisa hancurin 1 cincin lagi, kadang
//   bisa beruntun sampe 3x"
//
//  CARA BATCH N: JEDA DI DALAM LOOP, BUKAN PELAMBATAN WAKTU
//  ---------------------------------------------------------------------
//  ResolveBoard() itu sebuah loop:
//      cari baris penuh -> FlashClear -> skor -> ClearedRowGravity -> ulangi
//  Tempat jeda yang benar ada di UJUNG tiap putaran, dan itu cuma bisa
//  dicapai dengan menyisipkan satu yield di sana:
//
//      yield return StartCoroutine(KbtWaitPraise());
//
//  Keunggulannya, dan ini yang membuat seluruh gate timeScale dibuang:
//    * Time.timeScale TIDAK PERNAH disentuh -> nol risiko menggantung.
//    * Selagi menunggu, clearing masih true, dan Part3.Update() sudah punya
//      "if (clearing) return;" di awal. Jadi balok yang jatuh, lock delay,
//      input, dan hitung mundur combo SEMUA berhenti sendiri. Itu jeda
//      sungguhan, bukan gerak lambat.
//    * Animasi hancurnya cincin tetap kecepatan normal (tidak lelet).
//    * Karena tiap kata dijamin selesai sebelum cincin berikutnya hancur,
//      penyaring kata jadi TIDAK PERLU -> ketujuh kata dibunyikan lagi.
//    * Efek yang harus tetap lincah memang sudah memakai waktu TAK
//      TERSKALA (teks pujian, guncangan, cincin gelombang, animasi permata,
//      bel permata), jadi selama jeda semuanya tetap berjalan mulus.
//
//  Panjang jeda = panjang klip suaranya (minimal 0,95 s = durasi animasi
//  teks KPR_DUR) + 0,10 s napas, dibatasi kubikaPauseCap. Default cap 3,0 s
//  sengaja lebih besar dari kata terpanjang (LEGENDARY 2,56 s) supaya
//  "penuh sampai kata habis" benar-benar terpenuhi.
//
//  DURASI KATA (hasil ukur mp3 di Resources/KubikaVoice)
//      good 0,76 | awesome 1,36 | amazing 1,62 | fantastic 1,96
//      incredible 1,93 | unstoppable 1,36 | LEGENDARY 2,56
//  Rantai 3x (kasus yang disebut pemain) = sekitar 3-4 detik jeda total.
//
//  KENAPA SUARANYA DIAMBIL ALIH DARI Praise.cs
//  ---------------------------------------------------------------------
//  KprPlayVoice() hanya punya SATU AudioSource, dan satu source berarti
//  Play() selalu MEMOTONG klip yang sedang berbunyi. Di sini dipakai DUA
//  source bergilir, jadi ekor kata lama dibiarkan berdering. Teks pujian,
//  warna, skala, dan animasinya TETAP milik Praise.cs.
//
//  CARA MEMBATALKAN (per bagian, tanpa menyentuh kode)
//  ---------------------------------------------------------------------
//      Kubika Combo Gate  = OFF -> jeda antar cincin mati (papan lanjut terus)
//      Kubika Item Shield = OFF -> balok jalan lagi selama animasi item
//      Kubika Pause Cap         -> batas atas jeda, detik
//  Kalau Combo Gate DAN Voice Queue dua-duanya OFF, kepemilikan suara
//  dikembalikan ke Praise.cs seperti sebelum Batch M.
// =====================================================================

public partial class Tetris3D
{
    // ---------- sakelar (SEMUA field BARU -> default kode ini berlaku sampai
    //            SampleScene disimpan ulang; lihat catatan serialisasi di doc) ----------
    public bool kubikaComboGate  = true;   // jeda papan di antara clear
    public bool kubikaItemShield = true;   // tahan balok selama animasi item
    public bool kubikaVoiceQueue = true;   // pemutar suara dipegang file ini
    // Batas atas jeda. 3,0 s > LEGENDARY (2,56 s) -> kata terpanjang lolos utuh.
    // Turunkan ke 1,8 atau 1,3 kalau jedanya terasa kelamaan.
    [Range(0.20f, 4f)] public float kubikaPauseCap = 3.00f;

    // ---------- konstanta ----------
    const float KBT_ITEM_MAX = 6.00f;   // batas keras tameng item (watchdog)
    const float KBT_BREATH   = 0.10f;   // napas sesudah kata habis
    const float KBT_WAIT_MAX = 4.00f;   // jaring terakhir: jeda tak boleh lewat ini
    const float KBT_WORD_FALLBACK = 0.95f; // = KPR_DUR, durasi animasi teks pujian

    const string KBT_VOICE_DIR = "KubikaVoice/";
    // Urutan WAJIB sama dengan KPR_FILES di Praise.cs (tier 1..7).
    static readonly string[] KBT_FILES =
    {
        "good", "awesome", "amazing", "fantastic", "incredible", "unstoppable", "legendary"
    };

    // ---------- state ----------
    float kbtPrevComboTime;      // pendeteksi tepi naik comboTime (pola prev* biasa)
    float kbtWordAt;             // kapan kata terakhir mulai, waktu TAK TERSKALA
    float kbtWordLen;            // berapa lama papan harus menunggu kata itu
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
    float kbtVoiceLen;

    // =================================================================
    //  JEDA ANTAR CINCIN (bagian B) - di-yield dari loop cascade
    // =================================================================
    // Disisipkan di DUA tempat, masing-masing satu baris:
    //   Part2.ResolveBoard()               -> combo normal
    //   Gelembung2.ResolveClearsNoSpawn()  -> cascade Bom/Palu/Garis
    //
    // Ditaruh di ATAS loop (sesudah "kalau tidak ada baris penuh, keluar"),
    // BUKAN di bawah. Dua alasan:
    //   * pada cincin PERTAMA belum ada kata untuk ditunggu, jadi fungsi ini
    //     langsung selesai -> hancurnya cincin pertama tetap instan;
    //   * sesudah cincin TERAKHIR loop sudah keluar lewat break, jadi
    //     SpawnPiece() tidak pernah tertunda oleh jeda.
    //
    // Aman terhadap semua kondisi tepi:
    //   * pakai waktu TAK TERSKALA -> tidak peduli hit-stop / item Perlambat;
    //   * kalau pemain menekan Pause atau membuka klaim gelembung
    //     (timeScale = 0), hitungan jedanya DIBEKUKAN, tidak habis diam-diam;
    //   * ClearBoard() memanggil StopAllCoroutines() -> coroutine ini mati
    //     bersama ResolveBoard, tidak ada yang nyangkut;
    //   * ada batas keras KBT_WAIT_MAX.
    //
    // Sengaja mengembalikan System.Collections.IEnumerator dengan nama penuh
    // supaya file ini tidak perlu "using System.Collections;".
    public System.Collections.IEnumerator KbtWaitPraise()
    {
        if (!kubikaComboGate) yield break;
        if (!started || gameOver) yield break;
        if (kbtWordAt <= 0f || kbtWordLen <= 0f) yield break;

        float cap = Mathf.Max(0.20f, kubikaPauseCap);
        float end = kbtWordAt + Mathf.Min(kbtWordLen, cap);
        float guard = Time.unscaledTime + KBT_WAIT_MAX;

        while (true)
        {
            if (!started || gameOver) yield break;
            if (Time.unscaledTime >= guard) yield break;

            // Papan sedang dibekukan orang lain -> tunda hitungan, jangan
            // biarkan jedanya habis selagi pemain menatap menu.
            if (paused || BubbleClaimOpen)
            {
                end += Time.unscaledDeltaTime;
                guard += Time.unscaledDeltaTime;
                yield return null;
                continue;
            }

            if (Time.unscaledTime >= end) yield break;
            yield return null;
        }
    }

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

    // TIDAK ADA LAGI PENYARING TIER.
    //
    // Batch M punya KbtShouldSpeak() yang mensyaratkan tier melompat >= 2
    // tingkat. Itu dibuat waktu papan masih jalan terus dan kata-kata saling
    // menimpa. Sekarang papan benar-benar menunggu tiap kata habis, jadi
    // penyaringnya bukan cuma tidak perlu -- dia justru merusak, karena teks
    // muncul tanpa suara. Fungsi itu dihapus, bukan dimatikan lewat sakelar,
    // supaya nilai lama yang mungkin sudah tersimpan di scene tidak bisa
    // menghidupkannya kembali.
    //
    // Mengembalikan true kalau klipnya benar-benar diputar -> pemanggil pakai
    // itu untuk menentukan panjang jeda.
    bool KbtSpeak(int tier)
    {
        if (!(soundOn && sfxOn)) return false;
        if (Time.unscaledTime < muteUntil) return false;   // hormati jeda sting game over

        KbtEnsureVoice();

        int i = Mathf.Clamp(tier - 1, 0, KBT_FILES.Length - 1);
        if (!kbtTried[i])
        {
            kbtTried[i] = true;   // sekali gagal, jangan coba tiap combo
            kbtClips[i] = Resources.Load<AudioClip>(KBT_VOICE_DIR + KBT_FILES[i]);
        }
        AudioClip c = kbtClips[i];
        if (c == null) return false;

        kbtSrcIdx = 1 - kbtSrcIdx;                  // GILIR -> kata lama tidak dipotong
        AudioSource a = kbtSrc[kbtSrcIdx];
        if (a == null) return false;

        a.mute   = false;
        a.volume = Mathf.Clamp01(sfxVolume * Mathf.Clamp(kubikaVoiceVolume, 0f, 1.5f));
        a.clip   = c;
        a.Play();

        kbtSpokenTier = tier;
        kbtVoiceLen   = c.length;
        return true;
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

        // ---------- 1. rantai combo putus -> lupakan tier & kata ----------
        // comboCount = 0 saat jendela combo habis, dan = 1 pada clear pertama.
        // kbtWordAt dinolkan juga supaya cincin pertama rantai baru tidak
        // menunggu sisa kata dari rantai sebelumnya.
        if (comboCount <= 1)
        {
            kbtSpokenTier = 0;
            kbtWordAt = 0f;
            kbtWordLen = 0f;
        }

        // ---------- 2. tepi naik comboTime = ADA kata pujian baru ----------
        // Pola yang sama dipakai TickKubikaPraise (kprPrevComboTime). Kedua
        // pendeteksi berdiri sendiri, jadi teks tetap muncul walau suaranya
        // dimatikan lewat sakelar.
        float ct = comboTime;
        if (alive && ct > kbtPrevComboTime + 0.0001f)
        {
            int tier = Mathf.Clamp(comboShow - 1, 1, KBT_FILES.Length);
            bool played = own && KbtSpeak(tier);

            // Panjang jeda: klip suaranya kalau memang berbunyi, kalau tidak
            // (suara dimatikan / file hilang) tetap pakai 0,95 s supaya animasi
            // TEKS pujian juga kebagian ruang -- pemain minta "suara + visual".
            float len = (played && kbtVoiceLen > 0f) ? kbtVoiceLen : KBT_WORD_FALLBACK;
            kbtWordAt  = Time.unscaledTime;
            kbtWordLen = Mathf.Max(len, KBT_WORD_FALLBACK) + KBT_BREATH;
        }
        kbtPrevComboTime = ct;

        // ---------- 3. game over -> senyap & lepas ----------
        // Aturan lama dipertahankan: game over langsung membungkam pujian.
        if (gameOver && !kbtPrevOver)
        {
            KbtStopVoice();
            kbtWordAt = 0f;
            kbtWordLen = 0f;
        }
        kbtPrevOver = gameOver;

        // ---------- 4. TAMENG ITEM (bagian C, TIDAK diubah dari Batch M) ----------
        // Pelepasan punya TIGA jalur supaya tidak pernah nyangkut:
        //   * penanda KbtItemEnd() dari akhir coroutine (jalur normal)
        //   * begitu clearing sempat true lalu kembali false -> cascade item
        //     sudah selesai (ResolveClearsNoSpawn menyetel clearing di ujung
        //     BombBlast/HammerBlast). Jalur ini tetap bekerja walau coroutine
        //     dibunuh StopAllCoroutines() oleh ClearBoard().
        //   * watchdog KBT_ITEM_MAX sebagai jaring terakhir.
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

        // ---------- 5. Time.timeScale: TIDAK DISENTUH SAMA SEKALI ----------
        // Seluruh gate pelambatan Batch M (kbtHoldLeft / kbtOwnsTime /
        // kbtRestoreWait / kubikaGateSlow / kubikaGateMaxHold) DIHAPUS.
        // Jeda sekarang dikerjakan KbtWaitPraise() dari dalam loop cascade,
        // jadi file ini bukan lagi salah satu pemilik timeScale. Yang tersisa
        // cuma dua: klaim gelembung (0/1) dan hit-stop Batch F (0,08).
    }

    // Jaring pengaman statis, dipakai driver kalau instance game hilang
    // (ganti scene / dihancurkan) sementara timeScale masih tertahan oleh
    // pemilik lain yang ikut hilang. Pola yang sama dengan KubikaEndHitStop().
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
