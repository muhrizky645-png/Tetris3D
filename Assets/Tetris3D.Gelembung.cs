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
    const int   BOMB_COLS      = 3;     // lebar area bom (kolom)
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
            if (started && !gameOver && !paused) kbSlowTimer -= dt;
            if (kbSlowTimer <= 0f) { kbSlowTimer = 0f; fallInterval = kbSlowOrig; }
        }
        if ((!started || gameOver) && kbSlowTimer > 0f) { kbSlowTimer = 0f; fallInterval = kbSlowOrig; }

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
        // Gem/Permata DIHILANGKAN dari gelembung. Sisa: Bom, Bersihkan Baris, Perlambat.
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
        "Tiny Fantasy Icons/Explosives/Boom_A",
        "Tiny Fantasy Icons/PowerUps/Bolt_A",
        "Tiny Fantasy Icons/Time/Clock_A",
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
            case IT_LINE: DrawClearRowIcon(r); break;
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

    void DrawClearRowIcon(Rect r)
    {
        int cols = 4;
        float pad = r.width * 0.06f;
        float cellW = (r.width - pad * (cols + 1)) / cols;
        float cellH = r.height * 0.20f;
        float gap = r.height * 0.07f;
        float startY = r.center.y - (cellH * 3f + gap * 2f) / 2f;
        for (int row = 0; row < 3; row++)
        {
            float ry = startY + row * (cellH + gap);
            bool lit = (row == 1);
            for (int c = 0; c < cols; c++)
            {
                float rx = r.x + pad + c * (cellW + pad);
                Color cc = lit ? new Color(1f, 1f, 1f, 0.98f) : new Color(0.28f, 0.55f, 0.75f, 1f);
                RoundRect(new Rect(rx, ry, cellW, cellH), cc, cellW * 0.24f);
            }
        }
        float beamY = startY + (cellH + gap) - cellH * 0.20f;
        RoundRect(new Rect(r.x - r.width * 0.04f, beamY, r.width * 1.08f, cellH * 1.4f), new Color(1f, 1f, 1f, 0.30f), cellH);
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
            case IT_LINE: ApplyClearLine(); break;
            case IT_SLOW: ApplySlow(); break;
            case IT_GEM:
                AddPermata(GEM_BONUS);
                Sfx(sfxClear);
                KbToast("+" + GEM_BONUS + (SalID ? " Permata!" : " Gems!"));
                break;
        }
    }

    void ApplyBomb()
    {
        if (grid == null || cells == null) return;
        int front = Wrap(Mathf.RoundToInt((180f - targetSpin) * columns / 360f));
        int half = BOMB_COLS / 2;
        var targets = new List<Vector2Int>();
        for (int dc = -half; dc <= half; dc++)
        {
            int c = Wrap(front + dc);
            for (int r = 0; r < height; r++)
                if (cells[c, r] != null) targets.Add(new Vector2Int(c, r));
        }
        if (targets.Count == 0) return;
        StartCoroutine(BombBlast(targets));
    }

    // Efek ledakan bom: kotak yang kena membesar & berkedip terang sekilas
    // (mirip animasi cincin hancur), lalu meletus jadi partikel api/percikan
    // sebelum benar-benar hilang. Sebelumnya blok langsung Destroy() tanpa
    // animasi apa pun, jadi terasa seperti tidak ada ledakan sama sekali.
    IEnumerator BombBlast(List<Vector2Int> targets)
    {
        Sfx(sfxClear);
        Shake(0.35f, 0.45f);
        Haptic(60);
        KbToast("BOOM!");

        var objs = new List<Transform>();
        var baseScales = new List<Vector3>();
        var mats = new List<Material>();
        var centers = new List<Vector3>();
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

        float dur = 0.32f;
        float t2 = 0f;
        while (t2 < dur)
        {
            t2 += Time.deltaTime;
            float p = Mathf.Clamp01(t2 / dur);
            float scaleMul = p < 0.5f ? Mathf.Lerp(1f, 1.5f, p / 0.5f) : Mathf.Lerp(1.5f, 0.01f, (p - 0.5f) / 0.5f);
            float flash = p < 0.5f ? Mathf.Lerp(0f, 1f, p / 0.5f) : Mathf.Lerp(1f, 0f, (p - 0.5f) / 0.5f);
            for (int i = 0; i < objs.Count; i++)
            {
                if (objs[i] == null) continue;
                objs[i].localScale = baseScales[i] * scaleMul;
                if (mats[i] != null && mats[i].HasProperty("_EmissionColor"))
                {
                    Color glow = Color.Lerp(new Color(1f, 0.5f, 0.1f), Color.white, flash * 0.5f);
                    mats[i].SetColor("_EmissionColor", glow * (0.8f + flash * 3f));
                }
            }
            yield return null;
        }

        // Partikel ledakan (api & percikan) di tiap kotak yang hancur.
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
        var targets = new List<Vector2Int>();
        for (int c = 0; c < columns; c++)
            if (cells[c, target] != null) targets.Add(new Vector2Int(c, target));
        if (targets.Count == 0) return;
        StartCoroutine(LineBlast(targets));
    }

    // Efek animasi baris hancur (item Bersihkan Baris): kotak yang kena
    // membesar & berkedip terang sekilas lalu meletus jadi partikel,
    // meniru pola animasi ledakan bom, supaya item ini juga terasa ada
    // efeknya (bukan cuma langsung hilang seperti sebelumnya).
    IEnumerator LineBlast(List<Vector2Int> targets)
    {
        Sfx(sfxClear);
        Shake(0.3f, 0.3f);
        Haptic(50);
        KbToast(SalID ? "Baris dibersihkan!" : "Row cleared!");

        var objs = new List<Transform>();
        var baseScales = new List<Vector3>();
        var mats = new List<Material>();
        var centers = new List<Vector3>();
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

        float dur = 0.28f;
        float t2 = 0f;
        while (t2 < dur)
        {
            t2 += Time.deltaTime;
            float p = Mathf.Clamp01(t2 / dur);
            float scaleMul = p < 0.5f ? Mathf.Lerp(1f, 1.35f, p / 0.5f) : Mathf.Lerp(1.35f, 0.01f, (p - 0.5f) / 0.5f);
            float flash = p < 0.5f ? Mathf.Lerp(0f, 1f, p / 0.5f) : Mathf.Lerp(1f, 0f, (p - 0.5f) / 0.5f);
            for (int i = 0; i < objs.Count; i++)
            {
                if (objs[i] == null) continue;
                objs[i].localScale = baseScales[i] * scaleMul;
                if (mats[i] != null && mats[i].HasProperty("_EmissionColor"))
                {
                    Color glow = Color.Lerp(new Color(0.35f, 0.8f, 1f), Color.white, flash * 0.5f);
                    mats[i].SetColor("_EmissionColor", glow * (0.8f + flash * 3f));
                }
            }
            yield return null;
        }

        foreach (var pos in centers)
        {
            Burst(pos, new Color(0.35f, 0.8f, 1f));
            Burst(pos, new Color(0.85f, 0.95f, 1f));
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

    void ApplySlow()
    {
        if (kbSlowTimer <= 0f) kbSlowOrig = fallInterval;
        fallInterval = kbSlowOrig * SLOW_MULT;
        kbSlowTimer = SLOW_SECONDS;
        Sfx(sfxRotate);
        KbToast(SalID ? "Perlambat aktif!" : "Slow active!");
    }

    // ---- indikator timer utk item Perlambat ----
    public bool SlowActive { get { return kbSlowTimer > 0f; } }
    public float SlowSecondsLeft { get { return kbSlowTimer; } }

    // Badge kecil di bagian atas layar yang menunjukkan sisa detik efek
    // Perlambat masih aktif, supaya pemain tahu kapan efeknya akan habis.
    public void DrawSlowTimer()
    {
        if (kbSlowTimer <= 0f) return;
        float h = 56f;
        float w = 190f;
        float x = (VW - w) * 0.5f;
        float y = VH * 0.05f;
        Rect r = new Rect(x, y, w, h);
        RoundRect(new Rect(r.x - 3f, r.y - 3f, r.width + 6f, r.height + 6f), new Color(0.2f, 0.48f, 1f, 0.35f), h * 0.5f);
        RoundRect(r, new Color(0.06f, 0.08f, 0.14f, 0.92f), h * 0.5f);
        float ic = h * 0.72f;
        Rect ir = new Rect(r.x + (h - ic) * 0.5f + 6f, r.y + (h - ic) * 0.5f, ic, ic);
        DrawSlowIcon(ir);
        string txt = Mathf.CeilToInt(kbSlowTimer) + "s";
        Rect tr = new Rect(ir.xMax + 6f, r.y, r.xMax - (ir.xMax + 6f) - 10f, r.height);
        GuiText(tr, txt, 28, new Color(0.85f, 0.93f, 1f), TextAnchor.MiddleLeft);
    }

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
