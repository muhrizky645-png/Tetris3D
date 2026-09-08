using UnityEngine;

// =====================================================================
//  KUBIKA TOWER - BATCH L : MESIN SFX BARU
//  (blok jatuh / hard drop / rotasi blok / permata naik ke HUD)
// ---------------------------------------------------------------------
//  File TERPISAH (partial) - ADDITIF. TIDAK mengubah Tetris3D.cs, Part2,
//  Part3, Part4, Currency, maupun Gelembung. Semua penggantian dilakukan
//  saat RUNTIME oleh KubikaSfxDriver, jadi gampang di-revert: matikan
//  saja centang "Kubika Sfx Retune" di Inspector.
//
//  LATAR: musik game sudah pindah ke mesin sintesis baru (Batch D, dari
//  BuildMusic milik KubikaBlast), tapi SFX masih dibangun MakeTone() lama
//  di Part3.cs. Itu sebabnya musiknya terasa modern sementara efeknya
//  terasa seperti board game jadul. File ini memindahkan SFX ke mesin
//  yang sama gayanya dengan MakeClip() di KubikaBlast.
//
//  ENAM MASALAH NYATA DI MakeTone() YANG DIPERBAIKI DI SINI
//  ---------------------------------------------------------------------
//  1) KLIK DI EKOR SETIAP KLIP (ini bug, bukan selera).
//     MakeTone memakai waktu TERNORMALISASI (t = i/n) dengan peredaman
//     dec = Exp(-3.2f * t). Di sampel TERAKHIR nilainya Exp(-3.2) =
//     0.041, jadi gelombang dipotong mendadak saat amplitudonya masih 4%
//     lalu langsung nol. Lompatan itu = bunyi "tik" kering di SETIAP
//     rotasi, SETIAP lock, SETIAP drop. KsfClip() di bawah memakai fade
//     ekor 3 ms (KSF_TAIL), penjaga yang sama seperti MakeClip():
//         tail = Min(1, (n - i) / (0.003f * rate))
//
//  2) ENVELOPE TERNORMALISASI BIKIN DURASI TAK BERMAKNA.
//     Karena t selalu 0..1, bentuk peredaman klip 0.07 s dan klip 0.16 s
//     IDENTIK -> yang panjang terdengar lambat & mendengung, bukan makin
//     berat. Di file ini SEMUA envelope memakai DETIK sungguhan.
//
//  3) SAPUAN NADA MENYAPU SELURUH KLIP -> JADI SIULAN.
//     sfxDrop lama = 300 Hz -> 90 Hz dilerp rata sepanjang 160 ms; itu
//     bunyi slide-whistle "pyoooong". Benturan sungguhan RUNTUH nadanya
//     di 45-50 ms pertama lalu DITAHAN. Di sini dipakai pola
//     Lerp(f0, f1, Clamp01(t / 0.045f)) -- perhatikan Clamp01-nya.
//
//  4) "VIBRATO"-NYA PALSU (sumber kesan nyentrik).
//     f *= 1 + 0.006f * Sin(2*PI*6*t) pada klip 70 ms hanya menyelesaikan
//     0,42 PUTARAN. Jadi itu bukan vibrato, melainkan detune sembarang
//     yang besarnya berubah-ubah tergantung durasi klip. Dibuang total.
//
//  5) TANPA TRANSIEN, TANPA SUB, TANPA PEREKAT.
//     Semua SFX lama = tumpukan sinus murni (0.8 / 0.25 / 0.12) = organ
//     elektronik. Di sini tiap benturan punya letupan noise 2-5 ms, badan
//     yang runtuh, sub-bass untuk bobot, dan soft-clip tanh sebagai
//     perekat lapisan.
//
//  6) NADANYA IDENTIK PERSIS SETIAP KALI DIPICU.
//     Rotasi bisa berbunyi puluhan kali per menit di frekuensi yang sama;
//     telinga langsung membacanya sebagai mesin. Solusi di sini: KSF_VARIANTS
//     klip pra-render yang DIGILIR tiap frame. Sengaja TIDAK memakai
//     random AudioSource.pitch, karena menulis pitch akan MEMBENGKOKKAN
//     semua PlayOneShot yang masih berbunyi di source yang sama -- itu
//     justru penyakit yang dicatat KubikaSfx sebagai biang kerusakan.
//
//  ---------------------------------------------------------------------
//  PEMBARUAN BATCH M - NADA PERMATA TERLALU TINGGI SAAT COMBO PANJANG
//  ---------------------------------------------------------------------
//  Keluhan: "permata kayak ketinggian kalau banyak combonya".
//  Tangga nada lama = Min(1.9f, 1 + i * 0.055f):
//     * plafon 1.9 itu +11,2 semitone (hampir satu oktaf penuh) di atas
//       C6 yang sudah tinggi -> masuk wilayah yang terdengar MENJERIT
//     * plafon baru tercapai di butir ke-17, padahal peredupan volume
//       (KSF_GEM_SPAN = 12) sudah habis di butir ke-12. Jadi ada rentang
//       butir 12-17 yang nadanya masih naik sementara volumenya sudah
//       mentok pelan -> justru bagian paling melengking & paling aneh.
//  Sekarang = Min(KSF_GEM_TOP, 1 + i * KSF_GEM_STEP) dengan plafon 1.45
//  (+6,4 semitone, kira-kira kuint) dan langkah 0.038. Plafon tercapai
//  tepat di butir ke-12, jadi tangga nada dan peredupan volume berakhir
//  di titik yang SAMA. Rasa "sedang mengumpulkan" tetap ada karena naik
//  12 langkah, tapi puncaknya tidak lagi menusuk.
//
//  Kalau nanti ingin lebih musikal lagi, ada tuas lanjutan di
//  HANDOFF-BATCH-M.md: kuantisasi pentatonik {0,2,4,7,9} seperti
//  ClearCascade milik KubikaBlast. Sengaja BELUM dipasang supaya
//  perubahan batch ini tetap satu variabel yang mudah dinilai.
// =====================================================================

public partial class Tetris3D
{
    // ---------- sakelar (SEMUA field BARU -> default kode ini berlaku) ----------
    public bool kubikaSfxRetune   = true;  // OFF = kembali ke SFX lama
    public bool kubikaSfxVariants = true;  // giliran varian klip (anti "kaku")
    public bool kubikaGemChime    = true;  // bel permata naik ke HUD
    [Range(0f, 1.5f)] public float kubikaGemVolume = 0.85f;

    // ---------- konstanta ----------
    const int   KSF_RATE     = 44100;
    const int   KSF_VARIANTS = 4;
    const float KSF_TAIL     = 0.003f;   // fade ekor 3 ms = anti-klik
    const float KSF_GEM_GAP  = 0.035f;   // rate-limit bel permata
    const float KSF_GEM_SPAN = 12f;      // butir ke-12 = volume terendah
    const float KSF_GEM_TOP  = 1.45f;    // BATCH M: plafon nada bel (dulu 1.9f)
    const float KSF_GEM_STEP = 0.038f;   // BATCH M: langkah nada per butir (dulu 0.055f)

    // ---------- state ----------
    bool  ksfReady;
    float ksfRetry;
    AudioClip[] ksfRot, ksfLok, ksfDrp;
    AudioClip   ksfGemClip, ksfGemClaimClip, ksfSilent;
    int   ksfVarIdx;
    AudioSource ksfGemSrc;
    float ksfGemLast;
    float ksfPrevPulse;
    int   ksfGemIndex;

    // ================= PABRIK GELOMBANG =================
    // Catatan: semua generator dijalankan SEKALI saat bake, jadi pemakaian
    // UnityEngine.Random di dalamnya tidak membebani frame.

    static float KsfSine(float f, float t)
    {
        return Mathf.Sin(2f * Mathf.PI * f * t);
    }

    static float KsfTri(float f, float t)
    {
        float p = f * t;
        p -= Mathf.Floor(p);
        return 4f * Mathf.Abs(p - 0.5f) - 1f;
    }

    static float KsfNoise()
    {
        return UnityEngine.Random.value * 2f - 1f;
    }

    // Bikin klip mono. DUA hal penting yang tidak dimiliki MakeTone:
    //   * t dihitung dalam DETIK (i / rate), bukan ternormalisasi (i / n)
    //   * ada fade ekor 3 ms supaya klip TIDAK pernah dipotong mendadak
    AudioClip KsfClip(string name, float dur, System.Func<float, float> gen)
    {
        int n = Mathf.Max(16, Mathf.RoundToInt(KSF_RATE * dur));
        float tailN = Mathf.Max(1f, KSF_TAIL * KSF_RATE);
        float[] data = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)KSF_RATE;
            float tail = Mathf.Min(1f, (n - i) / tailN);
            data[i] = Mathf.Clamp(gen(t) * tail, -1f, 1f);
        }
        AudioClip c = AudioClip.Create(name, n, 1, KSF_RATE, false);
        c.SetData(data, 0);
        return c;
    }

    // Klip bisu 16 sampel: dipakai untuk MEMBUNGKAM bunyi permata lama
    // tanpa perlu mengedit Tetris3D.Currency.cs.
    AudioClip KsfMakeSilent()
    {
        AudioClip c = AudioClip.Create("ksf_silent", 16, 1, KSF_RATE, false);
        c.SetData(new float[16], 0);
        return c;
    }

    // ================= ROTASI BLOK =================
    // Rotasi adalah suara yang PALING SERING berbunyi, jadi dia harus jadi
    // yang paling tidak menonjol: pendek (55 ms), pelan, dan MEKANIS --
    // terdengar seperti mekanisme, bukan seperti nada musik. Bunyi lama
    // 720 -> 1010 Hz sinus murni selama 70 ms terdengar seperti "bleep".
    AudioClip KsfMakeRotate(int v)
    {
        float det = 1f + (v - (KSF_VARIANTS - 1) * 0.5f) * 0.035f;
        float hiA = 1420f * det;
        float hiB = 2130f * det;
        return KsfClip("ksf_rot" + v, 0.055f, t =>
        {
            float open  = 1f - Mathf.Exp(-t * 2600f);
            float tick  = KsfNoise() * Mathf.Exp(-t * 620f) * 0.30f;
            // sapuan udara SINGKAT (22 ms pertama saja), lalu diam di nada
            float sweep = Mathf.Lerp(0.88f, 1f, Mathf.Clamp01(t / 0.022f));
            float body  = KsfSine(hiA * sweep, t) * 0.26f
                        + KsfTri(hiB * sweep, t) * 0.10f;
            return (tick + body * open) * Mathf.Exp(-t * 78f);
        });
    }

    // ================= BLOK MENDARAT (LOCK) =================
    // Nada runtuh 232 -> 78 Hz dalam 45 ms lalu DITAHAN, plus ketukan kayu
    // ~505 Hz, sub-bass 62 Hz untuk bobot, dan letupan noise sebagai titik
    // kontak. tanh merekatkan lapisan-lapisannya.
    AudioClip KsfMakeLock(int v)
    {
        float det = 1f + (v - (KSF_VARIANTS - 1) * 0.5f) * 0.045f;
        float f0 = 232f * det, f1 = 78f * det;
        return KsfClip("ksf_lock" + v, 0.13f, t =>
        {
            float open  = 1f - Mathf.Exp(-t * 900f);
            float f     = Mathf.Lerp(f0, f1, Mathf.Clamp01(t / 0.045f));
            float body  = (KsfSine(f, t) * 0.85f + KsfTri(f * 0.5f, t) * 0.16f)
                        * Mathf.Exp(-t * 26f) * open;
            float sub   = KsfSine(62f * det, t) * Mathf.Exp(-t * 19f) * 0.55f;
            float knock = (KsfSine(505f * det, t) * 0.30f
                        +  KsfTri(760f * det, t) * 0.12f) * Mathf.Exp(-t * 96f);
            float hit   = KsfNoise() * Mathf.Exp(-t * 420f) * 0.26f;
            return (float)System.Math.Tanh((body + sub + knock + hit) * 1.15f) * 0.90f;
        });
    }

    // ================= HARD DROP =================
    // Harus jelas LEBIH BERAT dari lock biasa supaya kedua aksi terbedakan.
    // Bunyi lama justru memakai gelombang KOTAK 300 -> 90 Hz = seperti laser
    // retro. Di sini: desau udara 28 ms, lalu benturan dengan nada runtuh
    // 300 -> 58 Hz dan sub 52 Hz yang lebih panjang.
    AudioClip KsfMakeDrop(int v)
    {
        float det = 1f + (v - (KSF_VARIANTS - 1) * 0.5f) * 0.040f;
        float f0 = 300f * det, f1 = 58f * det;
        return KsfClip("ksf_drop" + v, 0.19f, t =>
        {
            float pre    = Mathf.Clamp01(1f - t / 0.028f);
            float whoosh = KsfNoise() * pre * pre * 0.20f;

            float ti    = Mathf.Max(0f, t - 0.024f);   // benturan setelah desau
            float open  = 1f - Mathf.Exp(-ti * 1100f);
            float f     = Mathf.Lerp(f0, f1, Mathf.Clamp01(ti / 0.050f));
            float body  = (KsfSine(f, ti) * 0.95f + KsfTri(f * 0.5f, ti) * 0.20f)
                        * Mathf.Exp(-ti * 21f) * open;
            float sub   = KsfSine(52f * det, ti) * Mathf.Exp(-ti * 15f) * 0.70f;
            float crack = KsfNoise() * Mathf.Exp(-ti * 300f) * 0.30f;
            return (float)System.Math.Tanh((whoosh + body + sub + crack) * 1.20f) * 0.92f;
        });
    }

    // ================= PERMATA NAIK KE HUD =================
    // Bunyi lama: MakeTone("cur_coin_soft", 900f, 0.10f, 0.25f, 0, 70f)
    // -- yaitu 900 Hz TERJUN ke 70 Hz dalam 100 ms. Itu penurunan 3,7 oktaf,
    // bunyi "jatuh" kartun; kebalikan dari bunyi MEMUNGUT. Dan karena
    // CurPlayChaChingSoft() dipanggil tanpa indeks, setiap butir berbunyi
    // di nada yang SAMA -> terdengar seperti senapan mesin.
    //
    // Ganti: bel bening dua nada NAIK C6 -> E6 plus kilau satu oktaf di
    // atasnya, dimainkan dengan TANGGA NADA per butir (lihat KsfPlayGemTick).
    AudioClip KsfMakeGem()
    {
        const float N1 = 1046.5f;   // C6
        const float N2 = 1318.5f;   // E6
        const float STEP = 0.055f;
        return KsfClip("ksf_gem", 0.20f, t =>
        {
            float f  = (t < STEP) ? N1 : N2;
            float lt = (t < STEP) ? t : t - STEP;
            float bell = (KsfSine(f, lt) * 0.52f
                        + KsfSine(f * 2f, lt) * 0.18f
                        + KsfSine(f * 3.01f, lt) * 0.07f) * Mathf.Exp(-lt * 17f);
            float shimmer = KsfSine(2093f, t) * 0.10f * Mathf.Exp(-t * 13f);
            float ping    = KsfNoise() * Mathf.Exp(-t * 900f) * 0.10f;
            return (bell + shimmer + ping) * (1f - Mathf.Exp(-t * 1500f));
        });
    }

    // Versi PENUH untuk klaim gelembung Permata (kejadian tunggal & jarang).
    // Klip ini sengaja dipasang ke curSfxCoin, jadi coroutine CoChaChing yang
    // sudah ada memainkannya dua kali (pitch 1.0 lalu 1.5) -> justru menjadi
    // "cha-ching" naik yang enak, tanpa perlu mengubah Currency.cs.
    AudioClip KsfMakeGemClaim()
    {
        float[] arp = { 1046.5f, 1318.5f, 1568.0f };   // C6 - E6 - G6
        const float STEP = 0.060f;
        return KsfClip("ksf_gem_claim", 0.30f, t =>
        {
            int i = (int)(t / STEP);
            if (i > arp.Length - 1) i = arp.Length - 1;
            float lt = t - i * STEP;
            float f  = arp[i];
            float bell = (KsfSine(f, lt) * 0.50f
                        + KsfSine(f * 2f, lt) * 0.20f
                        + KsfSine(f * 3.01f, lt) * 0.08f) * Mathf.Exp(-lt * 14f);
            float shimmer = KsfSine(3136f, t) * 0.09f * Mathf.Exp(-t * 10f);
            return (bell + shimmer) * (1f - Mathf.Exp(-t * 1400f)) * 0.95f;
        });
    }

    // Bel permata dengan TANGGA NADA. Dipanggil dari KsfTick saat mendeteksi
    // tepi naik curGemPulse selagi curGemPhase == 2 (fase naik satu per satu).
    //
    // Tiga hal yang membuatnya terbaca sebagai "sedang mengumpulkan":
    //   * nada NAIK per butir: 1 + i * KSF_GEM_STEP, dibatasi KSF_GEM_TOP
    //     (BATCH M: 1 + i * 0.038 dengan plafon 1.45; dulu 0.055 / 1.9)
    //   * volume MEREDUP per butir supaya combo besar tidak menjerit
    //   * rate-limit 35 ms supaya rentetan tidak jadi bubur
    // Dimainkan di ksfGemSrc, AudioSource MILIK SENDIRI -- inilah yang bikin
    // penulisan pitch di sini aman dan tidak membengkokkan rotasi/lock/drop.
    void KsfPlayGemTick()
    {
        if (!kubikaGemChime || ksfGemSrc == null || ksfGemClip == null) return;
        if (!(soundOn && sfxOn)) return;
        if (Time.unscaledTime < muteUntil) return;   // hormati jeda sting game over

        float now = Time.unscaledTime;
        if (now - ksfGemLast < KSF_GEM_GAP) return;
        ksfGemLast = now;

        int i = ksfGemIndex;
        ksfGemIndex = i + 1;

        // BATCH M: plafon & langkah dipindah ke konstanta supaya satu tempat saja
        // yang perlu disetel kalau nadanya masih terasa terlalu tinggi/rendah.
        ksfGemSrc.pitch = Mathf.Min(KSF_GEM_TOP, 1f + i * KSF_GEM_STEP);
        float lvl = Mathf.Lerp(0.78f, 0.42f, Mathf.Clamp01(i / KSF_GEM_SPAN))
                  * kubikaGemVolume;
        // sfxVolume default 0.5 -> dinormalkan supaya slider tetap berpengaruh
        // tanpa membuat bel jadi setengah volume.
        float g = (sfxVolume <= 0f) ? 0f : Mathf.Clamp01(lvl * (sfxVolume / 0.5f));
        if (g > 0f) ksfGemSrc.PlayOneShot(ksfGemClip, g);
    }

    // ================= PEMASANGAN =================
    void KsfBuild()
    {
        ksfRot = new AudioClip[KSF_VARIANTS];
        ksfLok = new AudioClip[KSF_VARIANTS];
        ksfDrp = new AudioClip[KSF_VARIANTS];
        for (int v = 0; v < KSF_VARIANTS; v++)
        {
            ksfRot[v] = KsfMakeRotate(v);
            ksfLok[v] = KsfMakeLock(v);
            ksfDrp[v] = KsfMakeDrop(v);
        }
        ksfGemClip      = KsfMakeGem();
        ksfGemClaimClip = KsfMakeGemClaim();
        ksfSilent       = KsfMakeSilent();

        sfxRotate = ksfRot[0];
        sfxLock   = ksfLok[0];
        sfxDrop   = ksfDrp[0];

        // AudioSource khusus permata. WAJIB terpisah: menulis pitch di sebuah
        // AudioSource akan menggeser nada SEMUA PlayOneShot yang masih
        // berbunyi di source itu.
        if (ksfGemSrc == null && sfx != null)
        {
            GameObject go = new GameObject("KubikaGemAudio");
            go.transform.SetParent(sfx.transform, false);
            ksfGemSrc = go.AddComponent<AudioSource>();
            ksfGemSrc.playOnAwake   = false;
            ksfGemSrc.spatialBlend  = 0f;
            ksfGemSrc.volume        = 1f;
        }

        ksfReady = true;
    }

    // Dipanggil KubikaSfxDriver tiap LateUpdate (sesudah semua Update, jadi
    // sesudah Part3.Update dan sesudah CurTickGems pada frame yang sama).
    public void KsfTick()
    {
        if (!kubikaSfxRetune) return;

        if (!ksfReady)
        {
            // tunggu SetupAudio() selesai membangun klip aslinya
            if (sfx == null || sfxRotate == null || sfxLock == null || sfxDrop == null) return;
            if (Time.unscaledTime < ksfRetry) return;
            ksfRetry = Time.unscaledTime + 0.05f;
            KsfBuild();
            return;
        }

        // ---- giliran varian: variasi tanpa menyentuh pitch sama sekali ----
        if (kubikaSfxVariants)
        {
            ksfVarIdx++;
            if (ksfVarIdx >= KSF_VARIANTS * 3) ksfVarIdx = 0;
            int a = ksfVarIdx % KSF_VARIANTS;
            int b = (ksfVarIdx * 3 + 1) % KSF_VARIANTS;
            int c = (ksfVarIdx * 5 + 2) % KSF_VARIANTS;
            sfxRotate = ksfRot[a];
            sfxLock   = ksfLok[b];
            sfxDrop   = ksfDrp[c];
        }

        // ---- ambil alih bunyi permata TANPA mengedit Currency.cs ----
        // curSfxCoinSoft di-isi klip BISU: CurPlayChaChingSoft() tetap jalan
        // seperti biasa tapi tidak mengeluarkan bunyi, dan bel yang benar
        // dimainkan di bawah lewat deteksi tepi naik curGemPulse.
        // curSfxCoin di-isi klip bel PENUH: coroutine CoChaChing yang sudah
        // ada memainkannya 2x naik -> cha-ching klaim gelembung jadi bagus.
        if (kubikaGemChime)
        {
            if (ksfSilent != null && curSfxCoinSoft != ksfSilent)
                curSfxCoinSoft = ksfSilent;
            if (ksfGemClaimClip != null && curSfxCoin != ksfGemClaimClip)
                curSfxCoin = ksfGemClaimClip;
        }

        // ---- tangga nada permata: reset saat bukan fase naik ----
        bool rising = (curGemPhase == 2) && curGems3D != null && curGems3D.Count > 0;
        if (!rising) ksfGemIndex = 0;

        // curGemPulse di-set 0.32f tepat saat SATU permata masuk chip, lalu
        // menyusut tiap frame -> kenaikan nilainya = ada permata yang tiba.
        float pulse = curGemPulse;
        if (rising && pulse > ksfPrevPulse + 0.0001f) KsfPlayGemTick();
        ksfPrevPulse = pulse;
    }
}

// =====================================================================
//  Driver. Urutan 25275 = di antara KubikaMusicDriver (25250) dan
//  KubikaAudioDebugDriver (25300), supaya laporan audio tetap yang
//  terakhir membaca keadaan.
// =====================================================================
[DefaultExecutionOrder(25275)]
public class KubikaSfxDriver : MonoBehaviour
{
    Tetris3D game;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        GameObject go = new GameObject("KubikaSfxDriver");
        DontDestroyOnLoad(go);
        go.AddComponent<KubikaSfxDriver>();
    }

    void LateUpdate()
    {
        if (game == null) game = UnityEngine.Object.FindFirstObjectByType<Tetris3D>();
        if (game != null) game.KsfTick();
    }
}
