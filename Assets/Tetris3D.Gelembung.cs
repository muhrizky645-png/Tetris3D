using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if KUBIKA_ADMOB
using GoogleMobileAds.Api;
#endif

// =====================================================================
//  KUBIKA TOWER x SALDOKU - GELEMBUNG ITEM DROP (bagian 1)
// ---------------------------------------------------------------------
//  File TERPISAH (partial) - ADDITIF. Lanjutannya di Tetris3D.Gelembung2.cs
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

        TickRewardAnims();

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
            AddBuffReward(b);
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
        // Saat cooldown iklan buff aktif (3 menit sesudah nonton iklan buff),
        // gelembung buff (Bom/Palu/Perlambat) BERHENTI muncul; hanya Permata
        // yang keluar dari jalur ini. Sesudah cooldown habis, buff muncul lagi.
        if (BuffAdCooldownLeft() > 0f) return IT_GEM;

        int roll = Random.Range(0, 100);
        if (roll < 28) return IT_BOMB;
        if (roll < 54) return IT_LINE;
        if (roll < 76) return IT_SLOW;
        return IT_GEM;   // 24% Permata (klaim = nonton iklan -> +GEM_BONUS)
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
        "KubikaIcons/Gem_A",
        "KubikaIcons/Coin_A",
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
}
