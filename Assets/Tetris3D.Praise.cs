using UnityEngine;

// =====================================================================
//  KUBIKA TOWER - BATCH H: KATA PUJIAN + SUARA (GOOD! .. LEGENDARY!!)
// ---------------------------------------------------------------------
//  Sumber angka & daftar kata: KubikaBlast/Assets/Scripts/KubikaHud.cs
//
//  File TERPISAH (partial) - ADDITIF. Tidak ada satu baris pun di
//  Tetris3D.cs / Part2 / Part3 / Part4 yang diubah.
//
//  == KENAPA TIDAK MENYALIN KubikaHud.cs ==
//  KubikaHud.cs membangun Canvas + Text uGUI dan mencari field privat
//  BlastUI lewat REFLECTION. Tetris3D sama sekali tidak memakai uGUI -
//  seluruh HUD-nya IMGUI (OnGUI + GuiText/GlowText di Part4). Jadi yang
//  diambil dari KubikaBlast hanya DATA-nya: 7 kata, 7 warna, dan kurva
//  animasinya. Penggambarannya ditulis ulang memakai GlowText milik
//  Part4 supaya gaya teksnya (font Thaleah + outline 8 arah) identik
//  dengan LEVEL UP! dan COMBO x yang sudah ada.
//
//  == KENAPA COCOK PERSIS ==
//  Part2.ResolveBoard() sudah menyetel comboShow = comboCount saat
//  comboCount >= 2. Dengan cap combo 8 dari Batch G, comboShow ada di
//  rentang 2..8 -> tier = comboShow - 1 = 1..7, yaitu TEPAT tujuh kata
//  pujian. GOOD! di combo 2, LEGENDARY!! pas di combo maksimum.
// =====================================================================

public partial class Tetris3D
{
    // ---------------- TOMBOL PENGATUR (field public BARU) ----------------
    public bool kubikaPraise = true;        // teks pujian
    public bool kubikaPraiseVoice = true;   // suara pujian
    [Range(0f, 1.5f)] public float kubikaVoiceVolume = 1f;

    // KubikaHud.AnimatePraise: dur 0.95 s.
    const float KPR_DUR = 0.95f;

    // Folder di Resources tempat 7 file mp3 diletakkan (tanpa ekstensi saat load).
    const string KPR_VOICE_DIR = "KubikaVoice/";

    // Tier 1..7 -> indeks 0..6. Daftar & warna sama dengan KubikaHud.PraiseFor/PraiseColor.
    static readonly string[] KPR_WORDS =
    { "GOOD!", "AWESOME!!", "AMAZING!!", "FANTASTIC!!", "INCREDIBLE!!", "UNSTOPPABLE!!", "LEGENDARY!!" };

    // Nama file suara HARUS sama dengan ini (huruf kecil, di Resources/KubikaVoice/).
    static readonly string[] KPR_FILES =
    { "good", "awesome", "amazing", "fantastic", "incredible", "unstoppable", "legendary" };

    static readonly Color[] KPR_COLORS =
    {
        new Color(0.55f, 0.95f, 0.55f),   // GOOD!         hijau
        new Color(0.35f, 0.80f, 1.00f),   // AWESOME!!     biru
        new Color(1.00f, 0.85f, 0.30f),   // AMAZING!!     kuning
        new Color(1.00f, 0.60f, 0.25f),   // FANTASTIC!!   jingga
        new Color(1.00f, 0.45f, 0.55f),   // INCREDIBLE!!  merah muda
        new Color(0.80f, 0.50f, 1.00f),   // UNSTOPPABLE!! ungu
        new Color(1.00f, 0.90f, 0.40f),   // LEGENDARY!!   emas
    };

    // ---------------- STATE RUNTIME ----------------
    float kprTime;              // sisa waktu animasi
    int kprTier;                // 1..7
    float kprPrevComboTime;     // pencacah tepi SENDIRI (jangan pakai prevLines milik file lain)
    bool kprPrevGameOver;
    AudioSource kprVoice;
    AudioClip[] kprClips;
    bool[] kprClipTried;

    // ================== LOOP (dipanggil KubikaPraiseDriver.LateUpdate) ==================
    public void TickKubikaPraise()
    {
        // DETEKSI COMBO BARU tanpa mengubah Part2:
        // Part2.ResolveBoard() menyetel comboTime = 1.3f setiap clear dengan
        // combo >= 2. Nilai itu hanya bisa MELOMPAT NAIK di saat clear (di luar
        // itu Part3.Update() terus menguranginya), jadi "comboTime naik" adalah
        // penanda clear-dengan-combo yang tepat dan tidak bisa keliru.
        float ct = comboTime;
        if (ct > kprPrevComboTime + 0.0001f && started && !gameOver && !paused)
            KprFire(comboShow);
        kprPrevComboTime = ct;

        // unscaledDeltaTime: hit-stop Batch F menurunkan timeScale ke 0.08, dan
        // teks pujian tidak boleh ikut melambat.
        if (kprTime > 0f)
        {
            kprTime -= Time.unscaledDeltaTime;
            if (kprTime < 0f) kprTime = 0f;
        }

        // Catatan KubikaBlast pasal 10: PERIKSA GAME OVER SEBELUM PERAYAAN.
        // Kalau balok terakhir mengunci sekaligus mengakhiri permainan, pujian
        // harus langsung dibungkam supaya bunyi game over berdiri sendiri.
        if (gameOver && !kprPrevGameOver)
        {
            kprTime = 0f;
            KprStopVoice();
        }
        kprPrevGameOver = gameOver;

        // Jeda / kembali ke menu -> tidak ada pujian yang menggantung di layar.
        if (!started || paused) kprTime = 0f;
    }

    void KprFire(int combo)
    {
        if (!kubikaPraise) return;
        int tier = Mathf.Clamp(combo - 1, 1, KPR_WORDS.Length);
        kprTier = tier;
        kprTime = KPR_DUR;
        KprPlayVoice(tier);
    }

    // ================== SUARA ==================
    // AudioSource SENDIRI, tidak menumpang sfx / sfxLong / music.
    //
    // Ini aturan keras dari HANDOFF KubikaBlast pasal 10: peran yang mengubah
    // pitch TIDAK BOLEH berbagi AudioSource dengan klip panjang. Part2.Sfx()
    // menaikkan sfx.pitch mengikuti combo (dan Part2.ClearBoard() harus
    // meresetnya ke 1 - lihat catatan F8). Kalau suara "LEGENDARY!!" ikut
    // menumpang sumber itu, kata-katanya akan terdengar melengking makin
    // tinggi seiring combo. Sumber ini pitch-nya dipaku 1 dan tidak pernah
    // disentuh siapa pun.
    void KprEnsureVoice()
    {
        if (kprClips == null) { kprClips = new AudioClip[KPR_FILES.Length]; kprClipTried = new bool[KPR_FILES.Length]; }
        if (kprVoice != null) return;
        kprVoice = gameObject.AddComponent<AudioSource>();
        kprVoice.playOnAwake = false;
        kprVoice.loop = false;
        kprVoice.spatialBlend = 0f;   // 2D
        kprVoice.pitch = 1f;          // JANGAN pernah diubah
        kprVoice.priority = 64;
    }

    void KprPlayVoice(int tier)
    {
        if (!kubikaPraiseVoice) return;
        if (!sfxOn) return;           // ikut toggle SUARA di menu jeda
        KprEnsureVoice();

        int i = Mathf.Clamp(tier - 1, 0, KPR_FILES.Length - 1);
        if (!kprClipTried[i])
        {
            // Dimuat SAAT DIPAKAI, bukan saat mulai: file suara tier tinggi
            // (LEGENDARY) mungkin tidak pernah terpakai di satu sesi.
            kprClips[i] = Resources.Load<AudioClip>(KPR_VOICE_DIR + KPR_FILES[i]);
            kprClipTried[i] = true;
        }
        AudioClip c = kprClips[i];
        // Kalau file mp3-nya belum ada di Resources/KubikaVoice/, TEKS tetap
        // jalan normal - fitur ini tidak boleh mati hanya karena aset kurang.
        if (c == null) return;

        kprVoice.volume = Mathf.Clamp01(sfxVolume * Mathf.Clamp(kubikaVoiceVolume, 0f, 1.5f));
        kprVoice.clip = c;
        // Satu sumber = suara lama otomatis dipotong suara baru. Ini memang yang
        // diinginkan: saat cascade cepat, "AMAZING" harus MENGGANTI "GOOD", bukan
        // menumpuk jadi dua orang berbicara bersamaan.
        kprVoice.Play();
    }

    void KprStopVoice()
    {
        if (kprVoice != null && kprVoice.isPlaying) kprVoice.Stop();
    }

    // ================== GAMBAR (dipanggil KubikaPraiseDriver.OnGUI) ==================
    public void DrawKubikaPraiseGui()
    {
        if (!kubikaPraise || kprTime <= 0f) return;
        if (!started || paused || gameOver) return;
        // Semua overlay yang menutupi layar: pujian tidak boleh menembusnya.
        if (showProfile || showRanks || tokoOpen) return;
        if (BubbleClaimOpen || SaldokuOverlayOpen) return;

        int tier = Mathf.Clamp(kprTier, 1, KPR_WORDS.Length);
        string word = KPR_WORDS[tier - 1];

        // Kurva animasi KubikaHud.AnimatePraise: melonjak melewati 1.0 lalu
        // mengendap (overshoot), baru memudar sambil naik.
        float k = 1f - Mathf.Clamp01(kprTime / KPR_DUR);        // 0 -> 1
        float scale = k < 0.16f ? Mathf.Lerp(0.4f, 1.18f, k / 0.16f)
                    : k < 0.28f ? Mathf.Lerp(1.18f, 1f, (k - 0.16f) / 0.12f)
                    : 1f;
        float alpha = k < 0.62f ? 1f : Mathf.Clamp01(1f - (k - 0.62f) / 0.38f);
        float rise = Mathf.Lerp(0f, 50f, k);

        Color col = KPR_COLORS[tier - 1];
        col.a = alpha;

        // Makin tinggi tier makin besar hurufnya (KubikaHud memakai satu ukuran,
        // tapi di sini COMBO x juga ikut membesar - biar dua-duanya senada).
        int size = 78 + tier * 6;

        // VH * 0.46 = DI BAWAH teks COMBO x (Part4 menggambarnya di VH*0.31
        // dengan tinggi 150, jadi berakhir sekitar VH*0.40). Naiknya dibatasi
        // 50 px supaya ujung animasinya tetap tidak menyentuh teks COMBO.
        Rect r = new Rect(0f, VH * 0.46f - rise, VW, 120f);

        Matrix4x4 old = GUI.matrix;
        GUIUtility.ScaleAroundPivot(new Vector2(scale, scale), r.center);
        // GlowText milik Part4: lapisan cahaya + teks beroutline 8 arah.
        // TIDAK ada GUI.Button di sini sama sekali - pujian tidak boleh
        // mencuri sentuhan pemain (catatan KubikaBubbleHUD, HANDOFF pasal 5.5).
        GlowText(r, word, size, col, alpha);
        GUI.matrix = old;
    }
}

// =====================================================================
//  DRIVER PUJIAN
// ---------------------------------------------------------------------
//  Punya OnGUI sendiri supaya Part4.OnGUI() tidak perlu disentuh.
//
//  GUI.depth: di IMGUI, angka LEBIH KECIL digambar LEBIH DEPAN.
//  KubikaTokoHUD memakai -900 (paling depan, karena tombolnya harus bisa
//  di-tap). Pujian memakai -500: di DEPAN HUD utama (depth 0) tapi di
//  BELAKANG panel toko, jadi tidak pernah menutupi kontrol yang bisa
//  disentuh.
//
//  Execution order 25200 = sesudah KubikaBalanceDriver (25150), supaya
//  cap combo Batch G sudah diterapkan ke comboShow sebelum tier pujian
//  dibaca. Tanpa urutan itu, combo 9 akan meminta kata ke-8 yang tidak ada.
// =====================================================================
[DefaultExecutionOrder(25200)]
public class KubikaPraiseDriver : MonoBehaviour
{
    Tetris3D game;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (Object.FindFirstObjectByType<KubikaPraiseDriver>() != null) return;
        var go = new GameObject("KubikaPraiseDriver");
        DontDestroyOnLoad(go);
        go.AddComponent<KubikaPraiseDriver>();
    }

    void FindGame()
    {
        if (game == null) game = Object.FindFirstObjectByType<Tetris3D>();
    }

    void LateUpdate()
    {
        FindGame();
        if (game == null) return;
        game.TickKubikaPraise();
    }

    void OnGUI()
    {
        if (Tetris3D.AdFullscreenShowing) return;   // iklan fullscreen -> HUD off
        FindGame();
        if (game == null) return;
        game.ApplyUiScale();                        // wajib: semua UI di ruang logis 720
        GUI.depth = -500;
        game.DrawKubikaPraiseGui();
    }
}
