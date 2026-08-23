using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if KUBIKA_ADMOB
using GoogleMobileAds.Api;
#endif

// =====================================================================
//  KUBIKA TOWER x SALDOKU - GELEMBUNG ITEM DROP
// ---------------------------------------------------------------------
//  File TERPISAH (partial) - ADDITIF.
// =====================================================================

public partial class Tetris3D
{
    // ---- jenis item ----
    const int IT_BOMB = 0, IT_LINE = 1, IT_SLOW = 2, IT_GEM = 3, IT_COIN = 4;

    // ---- parameter (boleh diubah sesuai selera) ----
    const int   BUBBLE_MAX     = 3;
    const float BUBBLE_MIN_GAP = 24f;   // jeda spawn minimum (detik) - dibuat lebih jarang
    const float BUBBLE_MAX_GAP = 40f;   // jeda spawn maksimum (detik) - dibuat lebih jarang
    const float BUBBLE_R       = 54f;   // radius gelembung (ruang logis) - diperkecil biar mungil
    const int   BOMB_COLS      = 3;     // (tidak dipakai lagi - bom kini acak 1/2 kotak)
    const int   GEM_BONUS      = 50;    // permata dari item Bonus Permata
    const float SLOW_SECONDS   = 8f;
    const float SLOW_MULT      = 2.5f;
    const float COIN_GAP       = 90f;   // koin muncul ~tiap 1,5 menit (jadwal khusus, buat marketing)
    const float BUFF_AD_COOLDOWN = 180f;// jeda 3 menit antar iklan BUFF (anti-spam AdMob), tanpa batas harian

    // ---- state gelembung ----
    class KBubble { public float x, y, vy, drift, phase; public int type; }
    List<KBubble> kbubbles;
    bool  kbInit;
    float kbSpawnTimer;
    float kbCoinTimer;   // timer khusus koin (marketing)

    // ---- state klaim / iklan ----
    bool   kbClaimOpen;
    int    kbClaimType;
    string kbClaimStatus = "";
    bool   kbAdBusy;
    string kbDropStatus = "";
    float  kbLastBuffAdTime = -9999f;   // waktu (unscaled) terakhir tonton iklan BUFF, utk cooldown

    // ---- efek tertunda ----
    int    kbPendingBuff = -1;
    float  kbSlowTimer;
    float  kbSlowOrig;
    float  kbSlowTickAcc;   // akumulator bunyi detikan saat Perlambat aktif

    // ---- efek suara khusus item (dibuat sekali, lazy) ----
    AudioClip kbSfxBombLit;    // tiap kotak bom menyala (nada naik)
    AudioClip kbSfxBombBoom;   // dentuman saat bom meletus
    AudioClip kbSfxHammerHit;  // hantaman palu tiap tahap cincin
    AudioClip kbSfxSlowTick;   // detikan timer Perlambat

    void KbEnsureItemSfx()
    {
        if (kbSfxBombLit  == null) kbSfxBombLit  = MakeTone("kb_bmbl", 320f, 0.045f, 0.40f, 1, 600f);
        if (kbSfxBombBoom == null) kbSfxBombBoom = MakeTone("kb_boom", 90f,  0.35f,  0.75f, 2, 38f);
        if (kbSfxHammerHit== null) kbSfxHammerHit= MakeTone("kb_ham",  180f, 0.12f,  0.60f, 0, 70f);
        if (kbSfxSlowTick == null) kbSfxSlowTick = MakeTone("kb_tick", 760f, 0.05f,  0.40f, 0, 600f);
    }

    // Mainkan SFX pakai AudioSource bersama dgn pitch tertentu (utk variasi nada).
    // Ingat reset sfx.pitch=1f setelah rangkaian selesai.
    void KbSfxAt(AudioClip c, float pitch)
    {
        if (!(soundOn && sfxOn) || sfx == null || c == null) return;
        sfx.pitch = pitch;
        sfx.PlayOneShot(c, sfxVolume);
    }

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
            kbCoinTimer  = COIN_GAP * 0.5f;
            kbInit = true;
        }
        float dt = Time.deltaTime;

        if (kbToastTime > 0f) kbToastTime -= Time.unscaledDeltaTime;

        if (kbClaimOpen && (!started || gameOver || showProfile || showRanks || SaldokuOverlayOpen))
        {
            kbClaimOpen = false;
            Time.timeScale = 1f;
        }

        if (kbSlowTimer > 0f)
        {
            if (started && !gameOver && !paused)
            {
                kbSlowTimer -= dt;
                // Bunyi detikan tiap 1 detik selagi Perlambat aktif.
                kbSlowTickAcc += dt;
                if (kbSlowTickAcc >= 1f && kbSlowTimer > 0f)
                {
                    kbSlowTickAcc -= 1f;
                    KbEnsureItemSfx();
                    Sfx(kbSfxSlowTick);
                }
            }
            if (kbSlowTimer <= 0f) { kbSlowTimer = 0f; fallInterval = kbSlowOrig; kbSlowTickAcc = 0f; }
        }
        if ((!started || gameOver) && kbSlowTimer > 0f) { kbSlowTimer = 0f; fallInterval = kbSlowOrig; kbSlowTickAcc = 0f; }

        if (kbPendingBuff >= 0 && started && !gameOver && !clearing && !paused)
        {
            int b = kbPendingBuff; kbPendingBuff = -1;
            ApplyBuff(b);
        }

        if (!started || gameOver) { if (kbubbles.Count > 0) kbubbles.Clear(); kbCoinTimer = COIN_GAP * 0.5f; return; }

        if (!BubblesActive) return;

        for (int i = kbubbles.Count - 1; i >= 0; i--)
        {
            var b = kbubbles[i];
            b.phase += dt;
            b.y += b.vy * dt;
            b.x += Mathf.Sin(b.phase * 1.6f) * b.drift * dt;
            if (b.y > VH * 0.82f) kbubbles.RemoveAt(i);
        }

        kbSpawnTimer -= dt;
        if (kbSpawnTimer <= 0f && kbubbles.Count < BUBBLE_MAX)
        {
            SpawnBubble();
            kbSpawnTimer = Random.Range(BUBBLE_MIN_GAP, BUBBLE_MAX_GAP);
        }

        // Koin punya JADWAL SENDIRI biar lebih sering muncul (~tiap 1,5 menit)
        // untuk mendorong pemain klaim -> nonton iklan -> poin SALDOKU (marketing).
        kbCoinTimer -= dt;
        if (kbCoinTimer <= 0f)
        {
            if (kbubbles.Count < BUBBLE_MAX + 1) SpawnCoinBubble();
            kbCoinTimer = COIN_GAP;
        }
    }

    void SpawnBubble()
    {
        var b = new KBubble();
        // Muncul MELAYANG di PINGGIR (kiri/kanan) - jangan di tengah supaya tak
        // menutupi menara balok. Pilih salah satu sisi secara acak.
        bool left = Random.value < 0.5f;
        float band = Mathf.Max(BUBBLE_R + 8f, VW * 0.22f);   // lebar area pinggir
        if (left) b.x = Random.Range(BUBBLE_R + 6f, band);
        else      b.x = Random.Range(VW - band, VW - BUBBLE_R - 6f);
        b.y = -BUBBLE_R;
        b.vy = Random.Range(70f, 105f);
        b.drift = Random.Range(8f, 20f);    // goyang lebih kecil biar tetap di pinggir
        b.phase = Random.Range(0f, 6.28f);
        b.type = PickBubbleType();
        kbubbles.Add(b);
    }

    // Gelembung KOIN (dipanggil oleh jadwal koin khusus).
    void SpawnCoinBubble()
    {
        var b = new KBubble();
        bool left = Random.value < 0.5f;
        float band = Mathf.Max(BUBBLE_R + 8f, VW * 0.22f);
        if (left) b.x = Random.Range(BUBBLE_R + 6f, band);
        else      b.x = Random.Range(VW - band, VW - BUBBLE_R - 6f);
        b.y = -BUBBLE_R;
        b.vy = Random.Range(70f, 105f);
        b.drift = Random.Range(8f, 20f);
        b.phase = Random.Range(0f, 6.28f);
        b.type = IT_COIN;
        kbubbles.Add(b);
    }

    int PickBubbleType()
    {
        // Koin TIDAK di sini - koin punya jadwal sendiri (SpawnCoinBubble).
        // Gem/Permata DIHILANGKAN dari gelembung. Sisa: Bom, Palu, Perlambat.
        int roll = Random.Range(0, 100);
        if (roll < 34) return IT_BOMB;
        if (roll < 67) return IT_LINE;
        return IT_SLOW;
    }

    // Sisa cooldown iklan buff (detik). 0 kalau sudah boleh nonton.
    float BuffAdCooldownLeft()
    {
        float left = BUFF_AD_COOLDOWN - (Time.unscaledTime - kbLastBuffAdTime);
        return left > 0f ? left : 0f;
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

    static readonly string[] KB_ICON_PATH = new string[]
    {
        "KubikaIcons/Boom_A",
        "KubikaIcons/Hammer_A",
        "KubikaIcons/Clock_A",
        "Tiny Fantasy Icons/Gems/Gems_Large_Diamond",
        "Tiny Fantasy Icons/Coins/Coins_Large_Gold",
    };
    static Dictionary<int, Texture2D> kbIconCache;
    static HashSet<int> kbIconMissing;

    Texture2D KbItemTex(int type)
    {
        if (type < 0 || type >= KB_ICON_PATH.Length) return null;
        if (kbIconCache == null) kbIconCache = new Dictionary<int, Texture2D>();
        if (kbIconMissing == null) kbIconMissing = new HashSet<int>();
        Texture2D tex;
        if (kbIconCache.TryGetValue(type, out tex)) return tex;
        if (kbIconMissing.Contains(type)) return null;
        tex = Resources.Load<Texture2D>(KB_ICON_PATH[type]);
        if (tex != null) kbIconCache[type] = tex;
        else kbIconMissing.Add(type);
        return tex;
    }

    void DrawItemIcon(Rect r, int type)
    {
        Texture2D tex = KbItemTex(type);
        if (tex != null)
        {
            Color prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.30f);
            GUI.DrawTexture(new Rect(r.x + 2f, r.y + 3f, r.width, r.height), tex, ScaleMode.ScaleToFit, true);
            GUI.color = prev;
            GUI.DrawTexture(r, tex, ScaleMode.ScaleToFit, true);
            return;
        }

        switch (type)
        {
            case IT_GEM:  DrawGemIcon(r, new Color(0.62f, 0.35f, 1f)); break;
            case IT_COIN: DrawCoinIcon(r, new Color(1f, 0.78f, 0.18f)); break;
            case IT_BOMB: DrawBombIcon(r); break;
            case IT_LINE: DrawHammerIcon(r); break;
            case IT_SLOW: DrawSlowIcon(r); break;
        }
    }

    void DrawRectRot(Rect r, Color col, float radius, float deg, Vector2 pivot)
    {
        Matrix4x4 m = GUI.matrix;
        GUIUtility.RotateAroundPivot(deg, pivot);
        RoundRect(r, col, radius);
        GUI.matrix = m;
    }

    void DrawBombIcon(Rect r)
    {
        float d = r.width * 0.70f;
        Rect body = new Rect(r.center.x - d / 2f, r.yMax - d, d, d);
        RoundRect(new Rect(body.x - 3f, body.y - 3f, body.width + 6f, body.height + 6f), new Color(0f, 0f, 0f, 0.35f), body.width);
        RoundRect(body, new Color(0.13f, 0.13f, 0.19f, 1f), d / 2f);
        RoundRect(new Rect(body.x + d * 0.18f, body.y + d * 0.14f, d * 0.34f, d * 0.24f), new Color(1f, 1f, 1f, 0.40f), d * 0.20f);
        float capW = d * 0.26f;
        Rect cap = new Rect(body.center.x - capW / 2f, body.y - capW * 0.45f, capW, capW * 0.6f);
        RoundRect(cap, new Color(0.85f, 0.70f, 0.30f, 1f), capW * 0.20f);
        float fw = r.width * 0.10f;
        float fTop = r.y + r.height * 0.10f;
        Rect fuse = new Rect(cap.center.x - fw / 2f, fTop, fw, cap.y - fTop + 2f);
        DrawRectRot(fuse, new Color(0.62f, 0.46f, 0.26f, 1f), fw * 0.5f, 16f, new Vector2(cap.center.x, cap.y));
        float sp = r.width * 0.22f;
        Rect spark = new Rect(cap.center.x + r.width * 0.02f, fTop - sp * 0.35f, sp, sp);
        RoundRect(spark, new Color(1f, 0.55f, 0.15f, 1f), sp / 2f);
        RoundRect(new Rect(spark.x + sp * 0.28f, spark.y + sp * 0.28f, sp * 0.44f, sp * 0.44f), new Color(1f, 0.95f, 0.6f, 1f), sp * 0.22f);
    }

    // Ikon PALU (pengganti ikon laser). Kepala besi + gagang kayu, dimiringkan.
    void DrawHammerIcon(Rect r)
    {
        Vector2 ctr = r.center;
        float deg = -32f;

        // Gagang kayu.
        float hw = r.width * 0.13f;
        float hh = r.height * 0.64f;
        Rect handle = new Rect(ctr.x - hw / 2f, ctr.y - hh * 0.12f, hw, hh);
        DrawRectRot(new Rect(handle.x - 2f, handle.y - 2f, handle.width + 4f, handle.height + 4f), new Color(0f, 0f, 0f, 0.32f), hw * 0.5f, deg, ctr);
        DrawRectRot(handle, new Color(0.62f, 0.42f, 0.24f, 1f), hw * 0.45f, deg, ctr);

        // Kepala palu (besi).
        float headW = r.width * 0.60f;
        float headH = r.height * 0.30f;
        Rect head = new Rect(ctr.x - headW / 2f, r.y + r.height * 0.14f, headW, headH);
        DrawRectRot(new Rect(head.x - 3f, head.y - 3f, head.width + 6f, head.height + 6f), new Color(0f, 0f, 0f, 0.35f), headH * 0.30f, deg, ctr);
        DrawRectRot(head, new Color(0.70f, 0.74f, 0.82f, 1f), headH * 0.26f, deg, ctr);
        DrawRectRot(new Rect(head.x + headW * 0.10f, head.y + headH * 0.16f, headW * 0.32f, headH * 0.24f), new Color(1f, 1f, 1f, 0.5f), headH * 0.20f, deg, ctr);
    }

    void DrawSlowIcon(Rect r)
    {
        float d = r.width * 0.84f;
        Vector2 ctr = r.center;
        Rect face = new Rect(ctr.x - d / 2f, ctr.y - d / 2f, d, d);
        RoundRect(face, new Color(0.20f, 0.48f, 1f, 1f), d / 2f);
        float inD = d * 0.76f;
        RoundRect(new Rect(ctr.x - inD / 2f, ctr.y - inD / 2f, inD, inD), new Color(0.93f, 0.96f, 1f, 1f), inD / 2f);
        float knob = d * 0.14f;
        RoundRect(new Rect(ctr.x - knob / 2f, face.y - knob * 0.55f, knob, knob * 0.6f), new Color(0.20f, 0.48f, 1f, 1f), knob * 0.25f);
        float hw = d * 0.075f;
        Color hand = new Color(0.14f, 0.19f, 0.30f, 1f);
        DrawRectRot(new Rect(ctr.x - hw / 2f, ctr.y - inD * 0.30f, hw, inD * 0.30f), hand, hw * 0.5f, 0f, ctr);
        DrawRectRot(new Rect(ctr.x - hw / 2f, ctr.y - inD * 0.38f, hw, inD * 0.38f), hand, hw * 0.5f, 115f, ctr);
        RoundRect(new Rect(ctr.x - hw, ctr.y - hw, hw * 2f, hw * 2f), hand, hw);
    }

    public void DrawBubbleClaim()
    {
        if (!kbClaimOpen) return;
        float sw = VW, sh = VH;
        RoundRect(new Rect(0f, 0f, sw, sh), new Color(0f, 0f, 0f, 0.72f), 0f);

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
        // Cooldown 3 menit HANYA berlaku utk buff (bukan koin).
        float cdLeft = (kbClaimType != IT_COIN) ? BuffAdCooldownLeft() : 0f;
        string watch;
        Color watchCol;
        if (cdLeft > 0f)
        {
            watch = (SalID ? "Tunggu " : "Wait ") + Mathf.CeilToInt(cdLeft) + "s";
            watchCol = new Color(0.4f, 0.4f, 0.45f);
        }
        else
        {
            watch = kbAdBusy ? (SalID ? "Memuat iklan..." : "Loading ad...") : (SalID ? "Tonton Iklan" : "Watch Ad");
            watchCol = new Color(1f, 0.62f, 0.12f);
        }
        bool doWatch = Btn3D(new Rect(cx, yy, bw, 84f), watch, watchCol, false);
        bool doLater = Btn3D(new Rect(cx + bw + 16f, yy, bw, 84f), SalID ? "Nanti" : "Later", new Color(0.4f, 0.35f, 0.45f), false);

        GUI.Button(new Rect(0f, 0f, sw, sh), GUIContent.none, GUIStyle.none);

        if (doWatch && !kbAdBusy && cdLeft <= 0f) ClaimWatchAd();
        if (doLater) CloseBubbleClaim();
    }

    // ========================= AKSI =========================
    void OpenBubbleClaim(KBubble b)
    {
        if (kbubbles != null) kbubbles.Remove(b);
        kbClaimType = b.type;
        kbClaimOpen = true;
        kbClaimStatus = "";
        kbDropStatus = "";
        Time.timeScale = 0f;
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
                return;
            }
            kbClaimStatus = ""; kbDropStatus = "";
            kbClaimOpen = false; Time.timeScale = 1f;
            KubikaExtraAds.Instance.Show(this, KubikaExtraAds.MODE_DROP, sal_token, OnBubbleCoinReward);
        }
        else
        {
            // Cooldown 3 menit antar iklan buff (anti-spam AdMob), tanpa batas harian.
            float cd = BuffAdCooldownLeft();
            if (cd > 0f)
            {
                kbClaimStatus = (SalID ? "Tunggu " : "Wait ") + Mathf.CeilToInt(cd) + (SalID ? " detik lagi." : "s more.");
                return;
            }
            int bt = t;
            kbClaimOpen = false; Time.timeScale = 1f;
            KubikaExtraAds.Instance.Show(this, KubikaExtraAds.MODE_BUFF, null, () => { kbLastBuffAdTime = Time.unscaledTime; kbPendingBuff = bt; });
        }
    }

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
            case IT_LINE: ApplyHammer(); break;
            case IT_SLOW: ApplySlow(); break;
            case IT_GEM:
                AddPermata(GEM_BONUS);
                Sfx(sfxClear);
                KbToast("+" + GEM_BONUS + (SalID ? " Permata!" : " Gems!"));
                break;
        }
    }

    // ---- util: kumpulkan transform/skala/material/pusat dari daftar sel ----
    void GatherCells(List<Vector2Int> targets, out List<Transform> objs, out List<Vector3> baseScales, out List<Material> mats, out List<Vector3> centers)
    {
        objs = new List<Transform>();
        baseScales = new List<Vector3>();
        mats = new List<Material>();
        centers = new List<Vector3>();
        foreach (var t in targets)
        {
            GameObject go = cells[t.x, t.y];
            if (go == null) continue;
            objs.Add(go.transform);
            baseScales.Add(go.transform.localScale);
            centers.Add(go.transform.position);
            var rend = go.GetComponent<Renderer>();
            mats.Add(rend != null ? rend.material : null);
        }
    }

    // ========================= BOM (acak 1/2 kotak) =========================
    // Bom kini memilih ACAK setengah dari semua kotak yang SUDAH menumpuk
    // (balok yg sedang jatuh ada di array 'active', jadi otomatis tak ikut).
    void ApplyBomb()
    {
        if (grid == null || cells == null) return;

        var all = new List<Vector2Int>();
        for (int c = 0; c < columns; c++)
            for (int r = 0; r < height; r++)
                if (cells[c, r] != null) all.Add(new Vector2Int(c, r));
        if (all.Count == 0) return;

        // Acak urutan (Fisher-Yates) -> ambil separuh.
        for (int i = all.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            var tmp = all[i]; all[i] = all[j]; all[j] = tmp;
        }
        int take = Mathf.Max(1, all.Count / 2);
        var targets = all.GetRange(0, take);

        StartCoroutine(BombBlast(targets));
    }

    // Efek bom: tiap kotak target menyala kilatan PUTIH satu per satu (urut acak
    // karena target sudah diacak) sambil berbunyi (nada naik), sampai semua
    // menyala; jeda sekejap; lalu meletus bersamaan jadi partikel & hilang.
    IEnumerator BombBlast(List<Vector2Int> targets)
    {
        KbToast("BOOM!");
        KbEnsureItemSfx();

        List<Transform> objs; List<Vector3> baseScales; List<Material> mats; List<Vector3> centers;
        GatherCells(targets, out objs, out baseScales, out mats, out centers);
        int n = objs.Count;
        if (n == 0) yield break;

        // Fase 1: menyala satu per satu (tiap nyala ada bunyi, nada makin naik).
        float totalLightDur = Mathf.Clamp(n * 0.045f, 0.25f, 0.90f);
        float perStep = totalLightDur / n;
        for (int i = 0; i < n; i++)
        {
            KbSfxAt(kbSfxBombLit, 1f + i * 0.03f);
            if (objs[i] != null) objs[i].localScale = baseScales[i] * 1.20f;
            if (mats[i] != null && mats[i].HasProperty("_EmissionColor"))
                mats[i].SetColor("_EmissionColor", new Color(1f, 0.97f, 0.75f) * 3.4f);
            yield return new WaitForSeconds(perStep);
        }
        if (sfx != null) sfx.pitch = 1f;

        // Jeda sekejap saat semua sudah menyala penuh, sebelum meletus bareng.
        Shake(0.10f, 0.14f);
        yield return new WaitForSeconds(0.14f);

        // Fase 2: mengecil cepat bersamaan sambil meletus (dentuman).
        KbSfxAt(kbSfxBombBoom, 1f);
        if (sfx != null) sfx.pitch = 1f;
        float dur = 0.20f;
        float t2 = 0f;
        while (t2 < dur)
        {
            t2 += Time.deltaTime;
            float p = Mathf.Clamp01(t2 / dur);
            float scaleMul = Mathf.Lerp(1.20f, 0.01f, p);
            for (int i = 0; i < n; i++)
            {
                if (objs[i] == null) continue;
                objs[i].localScale = baseScales[i] * scaleMul;
            }
            yield return null;
        }

        Shake(0.35f, 0.45f);
        Haptic(60);

        foreach (var pos in centers)
        {
            Burst(pos, new Color(1f, 0.55f, 0.15f));
            Burst(pos, new Color(1f, 0.85f, 0.35f));
        }

        foreach (var tr in objs)
            if (tr != null) Destroy(tr.gameObject);

        foreach (var t in targets)
        {
            if (t.x >= 0 && t.x < columns && t.y >= 0 && t.y < height)
            {
                cells[t.x, t.y] = null;
                grid[t.x, t.y] = -1;
            }
        }

        StartCoroutine(CascadeGravity());
    }

    // ========================= PALU (2 baris terbawah) =========================
    // Mengganti item "laser". Palu menghancurkan 2 baris PALING BAWAH (r=0 & r=1).
    // Visual: cincin menyala ATAS dulu lalu BAWAH, bergantian beberapa siklus
    // (tiap tahap ada bunyi hantaman), baru keduanya hancur bersamaan.
    void ApplyHammer()
    {
        if (grid == null || cells == null) return;

        var topRow = new List<Vector2Int>();   // baris atas dari dua baris bawah (r=1)
        var botRow = new List<Vector2Int>();   // baris paling bawah (r=0)
        for (int c = 0; c < columns; c++)
        {
            if (height > 1 && cells[c, 1] != null) topRow.Add(new Vector2Int(c, 1));
            if (cells[c, 0] != null) botRow.Add(new Vector2Int(c, 0));
        }
        if (topRow.Count == 0 && botRow.Count == 0) return;
        StartCoroutine(HammerBlast(topRow, botRow));
    }

    void SetRingGlow(List<Transform> objs, List<Vector3> baseScales, List<Material> mats, Color col, bool on)
    {
        for (int i = 0; i < objs.Count; i++)
        {
            if (objs[i] != null) objs[i].localScale = baseScales[i] * (on ? 1.18f : 1f);
            if (mats[i] != null && mats[i].HasProperty("_EmissionColor"))
                mats[i].SetColor("_EmissionColor", on ? col * 3.2f : col * 0.4f);
        }
    }

    void ScaleRing(List<Transform> objs, List<Vector3> baseScales, float mul)
    {
        for (int i = 0; i < objs.Count; i++)
            if (objs[i] != null) objs[i].localScale = baseScales[i] * mul;
    }

    IEnumerator HammerBlast(List<Vector2Int> topRow, List<Vector2Int> botRow)
    {
        KbToast(SalID ? "Palu menghantam!" : "Hammer smash!");
        KbEnsureItemSfx();

        List<Transform> topT, botT;
        List<Vector3> topS, botS, topC, botC;
        List<Material> topM, botM;
        GatherCells(topRow, out topT, out topS, out topM, out topC);
        GatherCells(botRow, out botT, out botS, out botM, out botC);

        Color ringCol = new Color(1f, 0.85f, 0.35f);

        // Cincin nyala ATAS dulu baru BAWAH, bergantian.
        int cycles = 2;
        for (int cyc = 0; cyc < cycles; cyc++)
        {
            KbSfxAt(kbSfxHammerHit, 1.15f);
            SetRingGlow(topT, topS, topM, ringCol, true);
            SetRingGlow(botT, botS, botM, ringCol, false);
            Shake(0.06f, 0.10f);
            yield return new WaitForSeconds(0.16f);

            KbSfxAt(kbSfxHammerHit, 0.85f);
            SetRingGlow(topT, topS, topM, ringCol, false);
            SetRingGlow(botT, botS, botM, ringCol, true);
            Shake(0.06f, 0.10f);
            yield return new WaitForSeconds(0.16f);
        }
        if (sfx != null) sfx.pitch = 1f;

        // Nyalakan dua-duanya sebelum hancur.
        SetRingGlow(topT, topS, topM, ringCol, true);
        SetRingGlow(botT, botS, botM, ringCol, true);
        yield return new WaitForSeconds(0.10f);

        // Hantam: mengecil cepat + partikel.
        KbSfxAt(kbSfxHammerHit, 0.65f);
        if (sfx != null) sfx.pitch = 1f;
        Shake(0.40f, 0.50f);
        Haptic(70);
        float dur = 0.18f;
        float t2 = 0f;
        while (t2 < dur)
        {
            t2 += Time.deltaTime;
            float p = Mathf.Clamp01(t2 / dur);
            float mul = Mathf.Lerp(1.18f, 0.01f, p);
            ScaleRing(topT, topS, mul);
            ScaleRing(botT, botS, mul);
            yield return null;
        }

        foreach (var pos in topC) Burst(pos, ringCol);
        foreach (var pos in botC) Burst(pos, new Color(1f, 0.6f, 0.2f));

        foreach (var tr in topT) if (tr != null) Destroy(tr.gameObject);
        foreach (var tr in botT) if (tr != null) Destroy(tr.gameObject);

        // Kosongkan grid baris 0 & 1.
        for (int c = 0; c < columns; c++)
        {
            if (height > 1) { cells[c, 1] = null; grid[c, 1] = -1; }
            cells[c, 0] = null; grid[c, 0] = -1;
        }

        StartCoroutine(CascadeGravity());
    }

    void ApplySlow()
    {
        if (kbSlowTimer <= 0f) kbSlowOrig = fallInterval;
        fallInterval = kbSlowOrig * SLOW_MULT;
        kbSlowTimer = SLOW_SECONDS;
        kbSlowTickAcc = 0f;
        Sfx(sfxRotate);
        KbToast(SalID ? "Perlambat aktif!" : "Slow active!");
    }

    // ---- indikator timer utk item Perlambat ----
    public bool SlowActive { get { return kbSlowTimer > 0f; } }
    public float SlowSecondsLeft { get { return kbSlowTimer; } }

    // Vignette biru redup berdenyut di tepi layar selagi Perlambat aktif -
    // pembeda visual yang jelas (bukan cuma angka detik) bahwa game sedang
    // dalam mode lambat.
    public void DrawSlowVignette()
    {
        if (kbSlowTimer <= 0f) return;
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3.0f);
        float a = Mathf.Lerp(0.05f, 0.16f, pulse);
        Color c = new Color(0.22f, 0.52f, 1f, a);
        float edge = Mathf.Max(18f, VW * 0.035f);
        FillRect(new Rect(0f, 0f, VW, edge), c);          // atas
        FillRect(new Rect(0f, VH - edge, VW, edge), c);   // bawah
        FillRect(new Rect(0f, 0f, edge, VH), c);          // kiri
        FillRect(new Rect(VW - edge, 0f, edge, VH), c);   // kanan
    }

    // Badge kecil yang menunjukkan sisa detik efek Perlambat masih aktif.
    // Diposisikan tepat di bawah kotak preview "balok berikutnya" (pojok
    // kanan) supaya tidak menabrak baris HUD atas (skor/permata/koin/jeda).
    // Berdenyut halus (glow ring) biar makin jelas beda dari badge biasa.
    public void DrawSlowTimer()
    {
        if (kbSlowTimer <= 0f) return;

        Rect hsRect, gemRect, coinRect, pauseRect;
        GetHudRow(out hsRect, out gemRect, out coinRect, out pauseRect);

        float pvSize = Mathf.Min(VW * 0.22f, 132f);
        float pvX = VW - pvSize - 14f;
        float pvY = pauseRect.yMax + 14f;
        float boxH = pvSize + 40f;
        float boxBottom = (pvY - 6f) + boxH;

        float w = pvSize + 22f;
        float h = 50f;
        float x = pvX - 11f;
        float y = boxBottom + 10f;
        Rect r = new Rect(x, y, w, h);

        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f);
        RoundRect(new Rect(r.x - 3f, r.y - 3f, r.width + 6f, r.height + 6f), new Color(0.2f, 0.48f, 1f, 0.22f + 0.28f * pulse), h * 0.5f);
        RoundRect(r, new Color(0.06f, 0.08f, 0.14f, 0.92f), h * 0.5f);
        float ic = h * 0.72f;
        Rect ir = new Rect(r.x + (h - ic) * 0.5f + 4f, r.y + (h - ic) * 0.5f, ic, ic);
        DrawSlowIcon(ir);
        string txt = Mathf.CeilToInt(kbSlowTimer) + "s";
        Rect tr = new Rect(ir.xMax + 4f, r.y, r.xMax - (ir.xMax + 4f) - 8f, r.height);
        GuiText(tr, txt, 24, new Color(0.85f, 0.93f, 1f), TextAnchor.MiddleCenter);
    }

    string BubbleItemName(int t)
    {
        switch (t)
        {
            case IT_BOMB: return SalID ? "Bom" : "Bomb";
            case IT_LINE: return SalID ? "Palu" : "Hammer";
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
            case IT_BOMB: return SalID ? "Hancurkan separuh kotak secara acak." : "Destroy half of the blocks at random.";
            case IT_LINE: return SalID ? "Hancurkan 2 baris terbawah." : "Smash the bottom 2 rows.";
            case IT_SLOW: return SalID ? "Balok jatuh lebih lambat." : "Blocks fall slower.";
            case IT_GEM:  return "+" + GEM_BONUS + (SalID ? " Permata." : " Gems.");
            case IT_COIN: return SalID ? "Koin masuk ke poin SALDOKU." : "Coins go to your SALDOKU points.";
        }
        return "";
    }
}

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
        GUI.depth = -800;
        if (game.SlowActive) game.DrawSlowVignette();
        if (game.BubblesVisible) game.DrawBubbles();
        if (game.BubbleClaimOpen) game.DrawBubbleClaim();
        if (game.SlowActive && !game.BubbleClaimOpen) game.DrawSlowTimer();
    }
}

public class KubikaExtraAds : MonoBehaviour
{
    public const int MODE_BUFF = 0;
    public const int MODE_DROP = 1;

    const string AD_UNIT_BUFF = "ca-app-pub-3186700509396792/1410736235";
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
            Load();
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
        if (onReward != null) onReward();
#else
        game.OnBubbleAdUnavailable(game.BubbleAdsOffMsg());
#endif
    }
#endif
}
