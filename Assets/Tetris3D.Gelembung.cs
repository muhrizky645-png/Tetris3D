using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if KUBIKA_ADMOB
using GoogleMobileAds.Api;
#endif

// =====================================================================
//  KUBIKA TOWER x SALDOKU - GELEMBUNG ITEM DROP
// ---------------------------------------------------------------------
//  File TERPISAH (partial) - ADDITIVE. TIDAK mengubah file gameplay lama
//  maupun Tetris3D.Currency.cs / .Saldoku.cs / .PetiKoin.cs / .Extras.cs.
//
//  Konsep (sesuai permintaan):
//   * Gelembung berisi ITEM jatuh melayang saat MAIN. GRATIS muncul.
//   * Pemain TAP gelembung -> panel klaim. Semua item DIDAPAT SETELAH
//     NONTON IKLAN dulu.
//   * Jenis item:
//       - BUFF (lokal, aman): Bom (hancurkan area blok), Bersihkan 1
//         baris, Perlambat jatuh, Bonus Permata.
//       - KOIN drop -> masuk ke POIN SALDOKU. KOIN tetap READ-ONLY di
//         game: reward dikreditkan SERVER via AdMob SSV. Client hanya
//         refresh saldo (CoRefreshKoin). Pakai KUOTA HARIAN TERPISAH
//         dari Peti Koin (dibedakan server lewat AD UNIT khusus drop).
//
//  CATATAN SERVER (di luar repo, upload manual cPanel):
//   ssv_kubika_callback.php harus mengenali AD UNIT koin-drop lalu kredit
//   poin dengan KUOTA HARIAN sendiri (ref_id idempotent). Buff TIDAK
//   menyentuh server (custom_data kosong -> diabaikan server).
//
//  CATATAN BUILD: sama seperti Peti Koin. Tanpa define KUBIKA_ADMOB,
//  di Editor reward diberi langsung (buat tes) & di perangkat menampilkan
//  "fitur iklan belum aktif".
// =====================================================================

public partial class Tetris3D
{
    // ---- jenis item ----
    const int IT_BOMB = 0, IT_LINE = 1, IT_SLOW = 2, IT_GEM = 3, IT_COIN = 4;

    // ---- parameter (boleh diubah sesuai selera) ----
    const int   BUBBLE_MAX     = 3;
    const float BUBBLE_MIN_GAP = 7f;    // jeda spawn minimum (detik)
    const float BUBBLE_MAX_GAP = 13f;   // jeda spawn maksimum (detik)
    const float BUBBLE_R       = 84f;   // radius gelembung (ruang logis)
    const int   BOMB_COLS      = 3;     // lebar area bom (kolom)
    const int   GEM_BONUS      = 50;    // permata dari item Bonus Permata
    const float SLOW_SECONDS   = 8f;
    const float SLOW_MULT      = 2.5f;

    // ---- state gelembung ----
    class KBubble { public float x, y, vy, drift, phase; public int type; }
    List<KBubble> kbubbles;
    bool  kbInit;
    float kbSpawnTimer;

    // ---- state klaim / iklan ----
    bool   kbClaimOpen;
    int    kbClaimType;
    string kbClaimStatus = "";
    bool   kbAdBusy;
    string kbDropStatus = "";

    // ---- efek tertunda ----
    int    kbPendingBuff = -1;   // buff yang menunggu papan aman utk diterapkan
    float  kbSlowTimer;
    float  kbSlowOrig;

    // ---- toast kecil sendiri ----
    string kbToast = "";
    float  kbToastTime;
    void KbToast(string m) { kbToast = m; kbToastTime = 2.4f; }

    // ---- akses publik utk komponen HUD ----
    public bool BubbleClaimOpen { get { return kbClaimOpen; } }
    public bool BubblesVisible { get { return BubblesActive; } }
    bool BubblesActive
    {
        get
        {
            return started && !paused && !gameOver && !showProfile && !showRanks
                   && !SaldokuOverlayOpen && !kbClaimOpen;
        }
    }

    // ========================= LOOP =========================
    public void BubbleTick()
    {
        if (!kbInit)
        {
            kbubbles = new List<KBubble>();
            kbSpawnTimer = Random.Range(BUBBLE_MIN_GAP, BUBBLE_MAX_GAP);
            kbInit = true;
        }
        float dt = Time.deltaTime;

        if (kbToastTime > 0f) kbToastTime -= Time.unscaledDeltaTime;

        // Amankan: kalau panel klaim kebuka tapi kondisi sudah tak wajar, tutup & normalkan waktu
        if (kbClaimOpen && (!started || gameOver || showProfile || showRanks || SaldokuOverlayOpen))
        {
            kbClaimOpen = false;
            Time.timeScale = 1f;
        }

        // Timer buff Perlambat
        if (kbSlowTimer > 0f)
        {
            if (started && !gameOver && !paused) kbSlowTimer -= dt;
            if (kbSlowTimer <= 0f) { kbSlowTimer = 0f; fallInterval = kbSlowOrig; }
        }
        if ((!started || gameOver) && kbSlowTimer > 0f) { kbSlowTimer = 0f; fallInterval = kbSlowOrig; }

        // Terapkan buff tertunda saat papan aman (bukan sedang clear/pause/game over)
        if (kbPendingBuff >= 0 && started && !gameOver && !clearing && !paused)
        {
            int b = kbPendingBuff; kbPendingBuff = -1;
            ApplyBuff(b);
        }

        // Kalau tidak sedang main: kosongkan gelembung
        if (!started || gameOver) { if (kbubbles.Count > 0) kbubbles.Clear(); return; }

        // Beku saat pause / overlay / klaim kebuka
        if (!BubblesActive) return;

        // Gerakkan gelembung (jatuh + goyang halus)
        for (int i = kbubbles.Count - 1; i >= 0; i--)
        {
            var b = kbubbles[i];
            b.phase += dt;
            b.y += b.vy * dt;
            b.x += Mathf.Sin(b.phase * 1.6f) * b.drift * dt;
            if (b.y > VH * 0.82f) kbubbles.RemoveAt(i);   // lewat area tombol kontrol -> hilang
        }

        // Spawn berkala
        kbSpawnTimer -= dt;
        if (kbSpawnTimer <= 0f && kbubbles.Count < BUBBLE_MAX)
        {
            SpawnBubble();
            kbSpawnTimer = Random.Range(BUBBLE_MIN_GAP, BUBBLE_MAX_GAP);
        }
    }

    void SpawnBubble()
    {
        var b = new KBubble();
        b.x = Random.Range(BUBBLE_R + 20f, Mathf.Max(BUBBLE_R + 40f, VW - BUBBLE_R - 20f));
        b.y = -BUBBLE_R;
        b.vy = Random.Range(70f, 105f);
        b.drift = Random.Range(18f, 42f);
        b.phase = Random.Range(0f, 6.28f);
        b.type = PickBubbleType();
        kbubbles.Add(b);
    }

    // Bobot kemunculan: koin paling langka (butuh akun + kuota server)
    int PickBubbleType()
    {
        int roll = Random.Range(0, 100);
        if (roll < 25) return IT_BOMB;   // 25%
        if (roll < 45) return IT_LINE;   // 20%
        if (roll < 65) return IT_SLOW;   // 20%
        if (roll < 90) return IT_GEM;    // 25%
        return IT_COIN;                  // 10%
    }

    // ========================= GAMBAR =========================
    public void DrawBubbles()
    {
        if (kbubbles == null) return;
        for (int i = 0; i < kbubbles.Count; i++)
        {
            var b = kbubbles[i];
            float d = BUBBLE_R * 2f;
            Rect rr = new Rect(b.x - BUBBLE_R, b.y - BUBBLE_R, d, d);
            DrawOneBubble(rr, b.type);
            if (GUI.Button(rr, GUIContent.none, GUIStyle.none)) { OpenBubbleClaim(b); break; }
        }
        if (kbToastTime > 0f && !string.IsNullOrEmpty(kbToast))
            GuiText(new Rect(0f, VH * 0.14f, VW, 40f), kbToast, 28, new Color(0.8f, 1f, 0.9f), TextAnchor.MiddleCenter);
    }

    void DrawOneBubble(Rect r, int type)
    {
        RoundRect(new Rect(r.x - 4f, r.y - 4f, r.width + 8f, r.height + 8f), new Color(0.6f, 0.85f, 1f, 0.18f), r.width);
        RoundRect(r, new Color(0.55f, 0.8f, 1f, 0.22f), r.width / 2f);
        RoundRect(new Rect(r.x + 2f, r.y + 2f, r.width - 4f, r.height - 4f), new Color(0.85f, 0.95f, 1f, 0.12f), r.width / 2f);
        RoundRect(new Rect(r.x + r.width * 0.22f, r.y + r.height * 0.16f, r.width * 0.26f, r.height * 0.18f), new Color(1f, 1f, 1f, 0.5f), r.width * 0.12f);
        float ic = r.width * 0.52f;
        Rect ir = new Rect(r.center.x - ic / 2f, r.center.y - ic / 2f, ic, ic);
        DrawItemIcon(ir, type);
    }

    void DrawItemIcon(Rect r, int type)
    {
        switch (type)
        {
            case IT_GEM:  DrawGemIcon(r, new Color(0.62f, 0.35f, 1f)); break;
            case IT_COIN: DrawCoinIcon(r, new Color(1f, 0.78f, 0.18f)); break;
            case IT_BOMB: DrawGlyph(r, new Color(0.16f, 0.16f, 0.22f), "B", new Color(1f, 0.5f, 0.4f)); break;
            case IT_LINE: DrawGlyph(r, new Color(0.15f, 0.6f, 0.75f), "=", Color.white); break;
            case IT_SLOW: DrawGlyph(r, new Color(0.2f, 0.45f, 1f), "S", Color.white); break;
        }
    }

    void DrawGlyph(Rect r, Color disc, string ch, Color txt)
    {
        RoundRect(r, disc, r.width / 2f);
        RoundRect(new Rect(r.x + r.width * 0.14f, r.y + r.height * 0.12f, r.width * 0.72f, r.height * 0.26f), new Color(1f, 1f, 1f, 0.18f), r.width * 0.2f);
        GuiText(r, ch, Mathf.RoundToInt(r.height * 0.6f), txt, TextAnchor.MiddleCenter);
    }

    public void DrawBubbleClaim()
    {
        if (!kbClaimOpen) return;
        float sw = VW, sh = VH;
        RoundRect(new Rect(0f, 0f, sw, sh), new Color(0f, 0f, 0f, 0.72f), 0f);
        if (GUI.Button(new Rect(0f, 0f, sw, sh), GUIContent.none, GUIStyle.none)) { /* modal: telan klik luar */ }

        float pw = Mathf.Min(sw * 0.86f, 620f);
        float ph = 540f;
        float px = (sw - pw) * 0.5f;
        float py = (sh - ph) * 0.5f;
        RoundRect(new Rect(px - 4f, py - 4f, pw + 8f, ph + 8f), new Color(0.6f, 0.85f, 1f, 0.45f), 26f);
        RoundRect(new Rect(px, py, pw, ph), new Color(0.06f, 0.08f, 0.12f, 0.98f), 24f);

        float cx = px + 30f, cw = pw - 60f, yy = py + 26f;
        GuiText(new Rect(cx, yy, cw, 44f), SalID ? "Klaim Item" : "Claim Item", 36, Color.white, TextAnchor.UpperCenter);
        yy += 60f;

        float ic = 120f;
        DrawItemIcon(new Rect(px + pw / 2f - ic / 2f, yy, ic, ic), kbClaimType);
        yy += ic + 10f;

        GuiText(new Rect(cx, yy, cw, 36f), BubbleItemName(kbClaimType), 30, new Color(0.9f, 0.95f, 1f), TextAnchor.UpperCenter);
        yy += 42f;
        GuiText(new Rect(cx, yy, cw, 32f), BubbleItemDesc(kbClaimType), 22, new Color(0.75f, 0.8f, 0.9f), TextAnchor.UpperCenter);
        yy += 42f;
        GuiText(new Rect(cx, yy, cw, 28f), SalID ? "Tonton iklan untuk klaim." : "Watch an ad to claim.", 20, new Color(0.62f, 0.7f, 1f), TextAnchor.UpperCenter);
        yy += 38f;

        string st = !string.IsNullOrEmpty(kbClaimStatus) ? kbClaimStatus : kbDropStatus;
        if (!string.IsNullOrEmpty(st))
            GuiText(new Rect(cx, yy, cw, 28f), st, 20, new Color(1f, 0.85f, 0.5f), TextAnchor.UpperCenter);
        yy += 34f;

        float bw = (cw - 16f) * 0.5f;
        string watch = kbAdBusy ? (SalID ? "Memuat iklan..." : "Loading ad...") : (SalID ? "Tonton Iklan" : "Watch Ad");
        if (Btn3D(new Rect(cx, yy, bw, 84f), watch, new Color(1f, 0.62f, 0.12f), false) && !kbAdBusy) ClaimWatchAd();
        if (Btn3D(new Rect(cx + bw + 16f, yy, bw, 84f), SalID ? "Nanti" : "Later", new Color(0.4f, 0.35f, 0.45f), false)) CloseBubbleClaim();
    }

    // ========================= AKSI =========================
    void OpenBubbleClaim(KBubble b)
    {
        if (kbubbles != null) kbubbles.Remove(b);
        kbClaimType = b.type;
        kbClaimOpen = true;
        kbClaimStatus = "";
        kbDropStatus = "";
        Time.timeScale = 0f;   // bekukan papan selama panel klaim terbuka
    }

    void CloseBubbleClaim()
    {
        kbClaimOpen = false;
        kbClaimStatus = "";
        kbDropStatus = "";
        Time.timeScale = 1f;
    }

    void ClaimWatchAd()
    {
        int t = kbClaimType;
        if (t == IT_COIN)
        {
            EnsureCurrency();
            EnsureSaldoku();
            if (!cur_linked || string.IsNullOrEmpty(sal_token))
            {
                kbClaimStatus = SalID ? "Hubungkan akun SALDOKU dulu." : "Link your SALDOKU account first.";
                return; // panel tetap terbuka
            }
            kbClaimStatus = ""; kbDropStatus = "";
            kbClaimOpen = false; Time.timeScale = 1f;
            KubikaExtraAds.Instance.Show(this, KubikaExtraAds.MODE_DROP, sal_token, OnBubbleCoinReward);
        }
        else
        {
            int bt = t;
            kbClaimOpen = false; Time.timeScale = 1f;
            KubikaExtraAds.Instance.Show(this, KubikaExtraAds.MODE_BUFF, null, () => { kbPendingBuff = bt; });
        }
    }

    // ---- dipanggil manajer iklan ----
    public void SetBubbleAdBusy(bool b) { kbAdBusy = b; }
    public string BubbleAdsOffMsg() { return SalID ? "Fitur iklan belum aktif di build ini." : "Ads are not enabled in this build."; }
    public void OnBubbleAdUnavailable(string msg) { kbAdBusy = false; KbToast(msg); }
    public void OnBubbleCoinReward()
    {
        KbToast(SalID ? "Iklan selesai. Menambah koin..." : "Ad done. Crediting coins...");
        StartCoroutine(CoAfterBubbleDrop());
    }
    IEnumerator CoAfterBubbleDrop()
    {
        // Beri jeda supaya callback SSV server sempat masuk, lalu tarik saldo terbaru.
        yield return new WaitForSeconds(2.5f);
        yield return StartCoroutine(CoRefreshKoin(false));
        KbToast(SalID ? "Koin diperbarui!" : "Coins updated!");
    }

    // ========================= BUFF =========================
    void ApplyBuff(int type)
    {
        switch (type)
        {
            case IT_BOMB: ApplyBomb(); break;
            case IT_LINE: ApplyClearLine(); break;
            case IT_SLOW: ApplySlow(); break;
            case IT_GEM:
                AddPermata(GEM_BONUS);
                Sfx(sfxClear);
                KbToast("+" + GEM_BONUS + (SalID ? " Permata!" : " Gems!"));
                break;
        }
    }

    // Bom: hancurkan area beberapa kolom di sekitar kolom depan, lalu gravitasi jatuh.
    void ApplyBomb()
    {
        if (grid == null || cells == null) return;
        int front = Wrap(Mathf.RoundToInt((180f - targetSpin) * columns / 360f));
        int half = BOMB_COLS / 2;
        bool any = false;
        for (int dc = -half; dc <= half; dc++)
        {
            int c = Wrap(front + dc);
            for (int r = 0; r < height; r++)
            {
                if (cells[c, r] != null) { Destroy(cells[c, r]); cells[c, r] = null; any = true; }
                grid[c, r] = -1;
            }
        }
        if (any)
        {
            Sfx(sfxClear);
            Shake(0.35f, 0.4f);
            Haptic(60);
            StartCoroutine(CascadeGravity());
            KbToast("BOOM!");
        }
    }

    // Bersihkan 1 baris: hapus baris TERISI paling bawah, lalu gravitasi jatuh.
    void ApplyClearLine()
    {
        if (grid == null || cells == null) return;
        int target = -1;
        for (int r = 0; r < height; r++)
        {
            bool has = false;
            for (int c = 0; c < columns; c++) if (grid[c, r] != -1) { has = true; break; }
            if (has) { target = r; break; }
        }
        if (target < 0) return;
        for (int c = 0; c < columns; c++)
        {
            if (cells[c, target] != null) { Destroy(cells[c, target]); cells[c, target] = null; }
            grid[c, target] = -1;
        }
        Sfx(sfxClear);
        Shake(0.3f, 0.3f);
        Haptic(50);
        StartCoroutine(CascadeGravity());
        KbToast(SalID ? "Baris dibersihkan!" : "Row cleared!");
    }

    // Perlambat: naikkan interval jatuh sementara.
    void ApplySlow()
    {
        if (kbSlowTimer <= 0f) kbSlowOrig = fallInterval; // simpan nilai asli hanya saat belum aktif
        fallInterval = kbSlowOrig * SLOW_MULT;
        kbSlowTimer = SLOW_SECONDS;
        Sfx(sfxRotate);
        KbToast(SalID ? "Perlambat aktif!" : "Slow active!");
    }

    // ---- teks item ----
    string BubbleItemName(int t)
    {
        switch (t)
        {
            case IT_BOMB: return SalID ? "Bom" : "Bomb";
            case IT_LINE: return SalID ? "Bersihkan Baris" : "Clear Row";
            case IT_SLOW: return SalID ? "Perlambat" : "Slow";
            case IT_GEM:  return SalID ? "Bonus Permata" : "Gem Bonus";
            case IT_COIN: return SalID ? "Koin" : "Coin";
        }
        return "";
    }
    string BubbleItemDesc(int t)
    {
        switch (t)
        {
            case IT_BOMB: return SalID ? "Hancurkan area blok." : "Destroy a block area.";
            case IT_LINE: return SalID ? "Hapus 1 baris penuh." : "Remove one full row.";
            case IT_SLOW: return SalID ? "Balok jatuh lebih lambat." : "Blocks fall slower.";
            case IT_GEM:  return "+" + GEM_BONUS + (SalID ? " Permata." : " Gems.");
            case IT_COIN: return SalID ? "Koin masuk ke poin SALDOKU." : "Coins go to your SALDOKU points.";
        }
        return "";
    }
}

// =====================================================================
//  Komponen HUD terpisah: gambar gelembung saat main + panel klaim.
//  (Pola sama dgn KubikaCurrencyHUD / KubikaSaldokuUI.)
// =====================================================================
[DefaultExecutionOrder(-25000)]
public class KubikaBubbleHUD : MonoBehaviour
{
    Tetris3D game;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        var go = new GameObject("KubikaBubbleHUD");
        DontDestroyOnLoad(go);
        go.AddComponent<KubikaBubbleHUD>();
    }

    void FindGame()
    {
        if (game == null) game = Object.FindFirstObjectByType<Tetris3D>();
    }

    void Update()
    {
        FindGame();
        if (game != null) game.BubbleTick();
    }

    void OnGUI()
    {
        FindGame();
        if (game == null) return;
        game.ApplyUiScale();
        GUI.depth = -800; // di depan HUD utama, di belakang overlay Saldoku (-1000)
        if (game.BubblesVisible) game.DrawBubbles();
        if (game.BubbleClaimOpen) game.DrawBubbleClaim();
    }
}

// =====================================================================
//  Manajer iklan rewarded generik (buff + koin-drop).
//   - MODE_BUFF: reward LOKAL (tanpa SSV). Pakai ulang unit rewarded
//     Peti; custom_data kosong -> server mengabaikan.
//   - MODE_DROP: reward via SERVER (SSV). custom_data = game_token,
//     pakai AD UNIT KHUSUS drop supaya server bisa membedakan dari Peti
//     dan memakai kuota harian tersendiri.
// =====================================================================
public class KubikaExtraAds : MonoBehaviour
{
    public const int MODE_BUFF = 0;
    public const int MODE_DROP = 1;

    // Unit rewarded utk BUFF: pakai ulang unit Peti (buff tak pakai SSV).
    const string AD_UNIT_BUFF = "ca-app-pub-3186700509396792/1410736235";
    // Unit rewarded KHUSUS koin-drop. GANTI dgn unit asli dari AdMob yang
    // SSV-nya diarahkan ke ssv_kubika_callback.php (kuota drop terpisah).
    const string AD_UNIT_DROP = "ca-app-pub-3186700509396792/2222222222";
    const string AD_UNIT_TEST = "ca-app-pub-3940256099942544/5224354917";
    const bool   USE_TEST_ADS = false;

    static KubikaExtraAds _inst;
    public static KubikaExtraAds Instance
    {
        get
        {
            if (_inst == null)
            {
                var go = new GameObject("KubikaExtraAds");
                DontDestroyOnLoad(go);
                _inst = go.AddComponent<KubikaExtraAds>();
            }
            return _inst;
        }
    }

#if KUBIKA_ADMOB
    RewardedAd _ad;
    bool _init, _wantShow, _rewarded;
    Tetris3D _game;
    string _custom, _unit;
    System.Action _cb;

    string Unit(string prod) { return USE_TEST_ADS ? AD_UNIT_TEST : prod; }

    void EnsureInit()
    {
        if (_init) return;
        _init = true;
        MobileAds.Initialize(_ => Load());
    }

    void Load()
    {
        if (_ad != null) { _ad.Destroy(); _ad = null; }
        string u = string.IsNullOrEmpty(_unit) ? Unit(AD_UNIT_BUFF) : _unit;
        RewardedAd.Load(u, new AdRequest(), (ad, err) =>
        {
            if (err != null || ad == null)
            {
                if (_wantShow)
                {
                    _wantShow = false;
                    if (_game != null) { _game.SetBubbleAdBusy(false); _game.OnBubbleAdUnavailable(_game.BubbleAdsOffMsg()); }
                }
                return;
            }
            _ad = ad;
            Hook(_ad);
            if (_wantShow) { _wantShow = false; DoShow(); }
        });
    }

    void Hook(RewardedAd ad)
    {
        ad.OnAdFullScreenContentClosed += () =>
        {
            if (_game != null) _game.SetBubbleAdBusy(false);
            Load(); // preload berikutnya
        };
        ad.OnAdFullScreenContentFailed += (AdError e) =>
        {
            if (_game != null) { _game.SetBubbleAdBusy(false); _game.OnBubbleAdUnavailable(_game.BubbleAdsOffMsg()); }
            Load();
        };
    }

    void DoShow()
    {
        if (_ad == null || !_ad.CanShowAd()) { _wantShow = true; Load(); return; }
        if (!string.IsNullOrEmpty(_custom))
        {
            var ssv = new ServerSideVerificationOptions.Builder().SetCustomData(_custom).Build();
            _ad.SetServerSideVerificationOptions(ssv);
        }
        _rewarded = false;
        _ad.Show(reward => { _rewarded = true; if (_cb != null) _cb(); });
    }

    public void Show(Tetris3D game, int mode, string customData, System.Action onReward)
    {
        _game = game; _custom = customData; _cb = onReward;
        _unit = (mode == MODE_DROP) ? Unit(AD_UNIT_DROP) : Unit(AD_UNIT_BUFF);
        game.SetBubbleAdBusy(true);
        EnsureInit();
        if (_ad != null && _ad.CanShowAd()) DoShow();
        else { _wantShow = true; Load(); }
    }
#else
    public void Show(Tetris3D game, int mode, string customData, System.Action onReward)
    {
#if UNITY_EDITOR
        if (onReward != null) onReward();                 // Editor: beri hadiah langsung buat tes (tanpa AdMob)
#else
        game.OnBubbleAdUnavailable(game.BubbleAdsOffMsg());
#endif
    }
#endif
}
