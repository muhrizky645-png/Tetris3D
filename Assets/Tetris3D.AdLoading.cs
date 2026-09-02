using System.Collections.Generic;
using UnityEngine;

// =====================================================================
//  KUBIKA TOWER - EFEK LOADING IKLAN + ANIMASI KOIN + PETI + GESER MREC
// ---------------------------------------------------------------------
//  File TERPISAH (partial class + komponen). Semua additive, tidak
//  mengubah file gameplay lama. Berisi 4 hal:
//
//   1) EFEK LOADING IKLAN (semua iklan)
//      Sesudah klik "Tonton Iklan" ada jeda saat iklan DIMUAT. Dulu game
//      tetap jalan (balok jatuh) padahal layar diam -> terkesan nge-bug.
//      Sekarang: selama iklan diminta tapi BELUM tampil fullscreen,
//      tampilkan overlay "Memuat iklan..." + BEKUKAN game (timeScale=0),
//      lalu normal lagi setelah iklan selesai. Dideteksi otomatis dari
//      flag sibuk yang sudah ada (kbAdBusy / petiBusy / reviveAdPending)
//      jadi tidak perlu mengubah manajer iklan mana pun.
//
//   2) ANIMASI KOIN MASUK (mirip permata)
//      Saat saldo Koin BERTAMBAH selagi main, koin-koin terbang ke chip
//      Koin di HUD + chip berdenyut, supaya pemain yakin koin masuk.
//
//   3) PETI BESAR beranimasi - overlay SALDOKU.
//      OPSI B: gambar sprite peti Royal (SPR_Royal_Close / SPR_Royal_Open)
//      LANGSUNG via GUI.DrawTexture (anti-gagal di URP, tanpa kamera/RT).
//      Ada efek idle (napas, goyang, getar, halo, kilau) + ganti ke sprite
//      terbuka saat peti didapat. Kalau sprite tidak ada -> fallback peti
//      gambar-kode lama.
//
//   4) GESER UI supaya tidak ketutup banner MREC (300x250) di layar
//      JEDA / GAME OVER / Revive.
// =====================================================================

public partial class Tetris3D
{
    // ------------------------------------------------------------------
    //  (1) LOADING IKLAN
    // ------------------------------------------------------------------
    // true = iklan sudah diminta tapi belum tampil fullscreen (masih memuat).
    public bool AdLoadingActive
    {
        get { return (kbAdBusy || petiBusy || reviveAdPending) && !AdFullscreenShowing; }
    }

    // Overlay "Memuat iklan..." : latar gelap + spinner titik berputar.
    public void DrawAdLoadingOverlay()
    {
        float sw = VW, sh = VH;
        FillRect(new Rect(0f, 0f, sw, sh), new Color(0f, 0f, 0f, 0.82f));

        float cx = sw * 0.5f;
        float cy = sh * 0.44f;
        float R = Mathf.Min(sw, sh) * 0.085f;
        float dot = Mathf.Max(8f, R * 0.18f);
        int n = 8;
        float tt = Time.unscaledTime * 1.5f;
        for (int i = 0; i < n; i++)
        {
            float ang = (i / (float)n) * Mathf.PI * 2f - Mathf.PI * 0.5f;
            float px = cx + Mathf.Cos(ang) * R;
            float py = cy + Mathf.Sin(ang) * R;
            float phase = Mathf.Repeat(tt - i / (float)n, 1f);
            float a = 0.15f + 0.85f * (1f - phase);
            RoundRect(new Rect(px - dot * 0.5f, py - dot * 0.5f, dot, dot),
                new Color(1f, 0.78f, 0.2f, a), dot * 0.5f);
        }

        string msg = (lang == Lang.ID) ? "Memuat iklan..." : "Loading ad...";
        GuiText(new Rect(0f, cy + R + 26f, sw, 44f), msg, 28, Color.white, TextAnchor.MiddleCenter);
    }

    // ------------------------------------------------------------------
    //  (2) ANIMASI KOIN MASUK
    // ------------------------------------------------------------------
    struct KbCoinFly { public Vector2 from; public float t; public float dur; public float delay; public float size; }
    List<KbCoinFly> kbCoins;
    float kbCoinPulse;
    long kbPrevKoinObserved = -1;

    // Pantau saldo Koin tiap frame: kalau bertambah SELAGI MAIN & terhubung,
    // munculkan animasi koin terbang. Tidak animasi saat pertama load / di menu.
    public void CoinFlyObserve()
    {
        EnsureCurrency();
        long k = cur_koin;
        if (kbPrevKoinObserved >= 0 && k > kbPrevKoinObserved && cur_linked && CurrencyHudVisible)
            SpawnCoinFly((int)Mathf.Min(k - kbPrevKoinObserved, 100000));
        kbPrevKoinObserved = k;
    }

    // Munculkan koin-koin terbang dari tengah-bawah layar menuju chip Koin.
    public void SpawnCoinFly(int amount)
    {
        if (kbCoins == null) kbCoins = new List<KbCoinFly>();
        int n = Mathf.Clamp(6 + amount / 60, 6, 14);
        for (int i = 0; i < n; i++)
        {
            KbCoinFly c = new KbCoinFly();
            c.from = new Vector2(VW * 0.5f + Random.Range(-VW * 0.16f, VW * 0.16f),
                                 VH * 0.60f + Random.Range(-VH * 0.05f, VH * 0.07f));
            c.delay = i * 0.055f + Random.Range(0f, 0.03f);
            c.dur = Random.Range(0.5f, 0.72f);
            c.t = 0f;
            c.size = Mathf.Clamp(VW * 0.055f, 28f, 52f) * Random.Range(0.85f, 1.15f);
            kbCoins.Add(c);
        }
        while (kbCoins.Count > 28) kbCoins.RemoveAt(0);
    }

    // Maju-kan animasi koin tiap frame (unscaled supaya jalan walau beku).
    public void CoinFlyTick()
    {
        if (kbCoinPulse > 0f) kbCoinPulse -= Time.unscaledDeltaTime;
        if (kbCoins == null || kbCoins.Count == 0) return;
        float dt = Time.unscaledDeltaTime; if (dt > 0.033f) dt = 0.033f;
        for (int i = kbCoins.Count - 1; i >= 0; i--)
        {
            KbCoinFly c = kbCoins[i];
            if (c.delay > 0f) { c.delay -= dt; kbCoins[i] = c; continue; }
            c.t += dt;
            if (c.t >= c.dur)
            {
                kbCoinPulse = 0.34f;
                CurPlayChaChing();
                kbCoins.RemoveAt(i);
                continue;
            }
            kbCoins[i] = c;
        }
    }

    // Gambar koin terbang + denyut di chip Koin. Dipanggil KubikaCoinFlyHUD.
    public void DrawCoinFlyOverlay()
    {
        Rect hs, gem, coin, pause;
        GetHudRow(out hs, out gem, out coin, out pause);

        if (kbCoinPulse > 0f)
        {
            float cp = Mathf.Clamp01(kbCoinPulse / 0.34f);
            RoundRect(new Rect(coin.x - 7f, coin.y - 7f, coin.width + 14f, coin.height + 14f),
                new Color(1f, 0.78f, 0.2f, 0.55f * cp), 24f);
        }

        if (kbCoins == null || kbCoins.Count == 0) return;
        Vector2 target = coin.center;
        for (int i = 0; i < kbCoins.Count; i++)
        {
            KbCoinFly c = kbCoins[i];
            if (c.delay > 0f) continue;
            float q = c.dur > 0f ? Mathf.Clamp01(c.t / c.dur) : 1f;
            float e = q * q * (3f - 2f * q);
            Vector2 mid = (c.from + target) * 0.5f + new Vector2(0f, -Mathf.Abs(c.from.y - target.y) * 0.28f);
            Vector2 p = Vector2.Lerp(Vector2.Lerp(c.from, mid, e), Vector2.Lerp(mid, target, e), e);
            float s = Mathf.Lerp(c.size, c.size * 0.4f, e);
            DrawCoinIcon(new Rect(p.x - s * 0.5f, p.y - s * 0.5f, s, s), new Color(1f, 0.78f, 0.18f));
        }
    }

    // ------------------------------------------------------------------
    //  (3) PETI BESAR + ANIMASI - overlay SALDOKU
    // ------------------------------------------------------------------
    const float PETI_OPEN_DUR = 3f;
    // Skala tampilan peti terhadap kotak dasar (baseR). Naikkan utk peti
    // lebih besar, turunkan utk lebih kecil.
    const float PETI_VIEW_SCALE = 4f;
    float petiOpenAnimEnd = 0f;

    public int PetiProgress { get { return peti_progress; } }
    public void TriggerPetiOpenAnim() { petiOpenAnimEnd = Time.unscaledTime + PETI_OPEN_DUR; }

    // OPSI B: gambar sprite peti Royal LANGSUNG via GUI.DrawTexture. Sprite
    // diambil dari Resources (SPR_Royal_Close / SPR_Royal_Open) oleh
    // KubikaPetiChest3D. Anti-gagal di URP karena tidak pakai kamera/RT.
    // Kalau sprite tidak tersedia -> fallback peti gambar-kode lama.
    public void DrawPetiChest(Rect baseR)
    {
        bool opening = Time.unscaledTime < petiOpenAnimEnd;
        Texture tex = KubikaPetiChest3D.Report(opening);
        if (tex == null) { DrawPetiChestProcedural(baseR); return; }
        if (Event.current != null && Event.current.type != EventType.Repaint) return;

        float prog01 = iklanPerPeti > 0 ? Mathf.Clamp01(peti_progress / (float)iklanPerPeti) : 0f;
        float openP = opening
            ? Mathf.Clamp01((PETI_OPEN_DUR - (petiOpenAnimEnd - Time.unscaledTime)) / 0.45f)
            : 0f;
        float tt = Time.unscaledTime;

        // --- Animasi IDLE: SELALU bergerak (napas + goyang halus) ---
        float breathe = Mathf.Sin(tt * 2.3f) * (baseR.height * 0.02f);
        float sway = Mathf.Sin(tt * 1.4f) * 3f;

        // --- Getar makin kencang mendekati penuh + ekstra saat terbuka ---
        float shakeAmp = Mathf.Lerp(0f, 4f, prog01) + (opening ? 8f : 0f);
        float shakeX = Mathf.Sin(tt * 26f) * shakeAmp;
        float shakeY = Mathf.Abs(Mathf.Sin(tt * 31f)) * shakeAmp * 0.22f;

        // --- Goyang rotasi (idle halus + guncang saat terbuka) ---
        float idleRot = Mathf.Sin(tt * 1.9f) * 1.6f + (opening ? Mathf.Sin(tt * 28f) * 2.2f : 0f);

        // Ukuran gambar peti (skala terhadap kotak dasar), berpusat di posisi peti.
        float side = baseR.height * PETI_VIEW_SCALE;
        Vector2 c = new Vector2(baseR.center.x + sway + shakeX, baseR.center.y - breathe - shakeY);
        Rect drawR = new Rect(c.x - side * 0.5f, c.y - side * 0.5f, side, side);

        Matrix4x4 baseM = GUI.matrix;
        GUIUtility.RotateAroundPivot(idleRot, new Vector2(drawR.center.x, drawR.yMax - side * 0.14f));

        // halo cahaya lembut berdenyut, terang saat terbuka
        {
            float glowA = 0.08f + (opening
                ? 0.4f * openP * (0.6f + 0.4f * Mathf.Sin(tt * 8f))
                : 0.05f * (0.5f + 0.5f * Mathf.Sin(tt * 2.5f)));
            RoundRect(new Rect(drawR.x + side * 0.14f, drawR.y + side * 0.20f, side * 0.72f, side * 0.62f),
                new Color(1f, 0.9f, 0.45f, glowA), side * 0.3f);
        }

        // gambar sprite peti Royal (alpha-blend supaya latar transparan)
        GUI.DrawTexture(drawR, tex, ScaleMode.ScaleToFit, true);

        // kilau (sparkle): idle sedikit + lembut, terbuka banyak + terang
        {
            int ns = opening ? 6 : 3;
            float baseA = opening ? 0.85f * openP : 0.45f;
            for (int i = 0; i < ns; i++)
            {
                float a = (i / (float)ns) * Mathf.PI * 2f + tt * 1.4f;
                float rad = side * (0.18f + 0.1f * Mathf.Sin(tt * 3f + i * 1.7f));
                float sx = drawR.center.x + Mathf.Cos(a) * rad;
                float sy = drawR.center.y - side * 0.05f + Mathf.Sin(a) * rad * 0.65f;
                float ss = Mathf.Max(6f, side * 0.04f) * (0.55f + 0.45f * Mathf.Sin(tt * 6f + i));
                float twk = 0.45f + 0.55f * Mathf.Sin(tt * 5f + i * 2f);
                DrawSparkle(new Vector2(sx, sy), ss, new Color(1f, 0.96f, 0.65f, baseA * twk));
            }
        }

        GUI.matrix = baseM;
    }

    // Gambar peti (chest) blocky emas - FALLBACK gambar-kode.
    // baseR = posisi & ukuran dasar (tanpa getar). SELALU beranimasi: idle
    // (napas naik-turun + goyang + kedut + rotasi + halo + kilau kecil) walau
    // peti masih 0/5, getar makin kencang mendekati penuh, lalu terbuka +
    // kilau penuh saat 1 peti didapat.
    void DrawPetiChestProcedural(Rect baseR)
    {
        float prog01 = iklanPerPeti > 0 ? Mathf.Clamp01(peti_progress / (float)iklanPerPeti) : 0f;
        bool opening = Time.unscaledTime < petiOpenAnimEnd;
        float openP = opening
            ? Mathf.Clamp01((PETI_OPEN_DUR - (petiOpenAnimEnd - Time.unscaledTime)) / 0.45f)
            : 0f;

        float tt = Time.unscaledTime;

        // --- Animasi IDLE: SELALU bergerak walau peti masih 0/5 ---
        float breathe = Mathf.Sin(tt * 2.3f) * (baseR.height * 0.03f);   // napas naik-turun
        float sway = Mathf.Sin(tt * 1.4f) * 3f;                          // geser kiri-kanan halus
        float twitchP = Mathf.Repeat(tt, 1.8f);                          // kedut berkala tiap ~1.8 dtk
        float twitch = (twitchP < 0.30f) ? Mathf.Sin(twitchP * 70f) * 4f * (1f - twitchP / 0.30f) : 0f;

        // --- Getar makin kencang mendekati penuh + ekstra kuat saat terbuka ---
        float shakeAmp = Mathf.Lerp(0f, 5f, prog01) + (opening ? 9f : 0f);
        float shakeX = Mathf.Sin(tt * 26f) * shakeAmp;
        float shakeY = Mathf.Abs(Mathf.Sin(tt * 31f)) * shakeAmp * 0.22f;

        // --- Goyang rotasi (idle halus + guncang saat terbuka) ---
        float idleRot = Mathf.Sin(tt * 1.9f) * 2.2f + (opening ? Mathf.Sin(tt * 28f) * 2.5f : 0f);

        Rect r = new Rect(baseR.x + sway + twitch + shakeX, baseR.y - breathe - shakeY, baseR.width, baseR.height);

        Matrix4x4 baseM = GUI.matrix;
        GUIUtility.RotateAroundPivot(idleRot, new Vector2(r.center.x, r.yMax));

        float w = r.width, h = r.height;
        Color wood  = new Color(0.60f, 0.40f, 0.18f);
        Color woodD = new Color(0.42f, 0.27f, 0.11f);
        Color woodL = new Color(0.72f, 0.50f, 0.24f);
        Color gold  = new Color(1f, 0.82f, 0.28f);
        Color goldD = new Color(0.80f, 0.58f, 0.14f);

        // halo cahaya: lembut berdenyut selalu, terang saat terbuka
        {
            float glowA = 0.10f + (opening
                ? 0.4f * openP * (0.6f + 0.4f * Mathf.Sin(tt * 8f))
                : 0.06f * (0.5f + 0.5f * Mathf.Sin(tt * 2.5f)));
            RoundRect(new Rect(r.x - w * 0.18f, r.y - h * 0.22f, w * 1.36f, h * 1.4f),
                new Color(1f, 0.9f, 0.45f, glowA), w * 0.4f);
        }

        // ---- badan peti ----
        float bodyH = h * 0.60f;
        Rect body = new Rect(r.x, r.yMax - bodyH, w, bodyH);
        RoundRect(body, wood, w * 0.10f);
        RoundRect(new Rect(body.x, body.yMax - bodyH * 0.20f, w, bodyH * 0.20f), goldD, w * 0.08f);
        RoundRect(new Rect(r.x + w * 0.15f, body.y + 4f, w * 0.08f, bodyH - 8f), woodD, 4f);
        RoundRect(new Rect(r.x + w * 0.77f, body.y + 4f, w * 0.08f, bodyH - 8f), woodD, 4f);
        RoundRect(new Rect(body.x, body.y, w * 0.05f, bodyH), goldD, 4f);
        RoundRect(new Rect(body.xMax - w * 0.05f, body.y, w * 0.05f, bodyH), goldD, 4f);

        // cahaya isi peti (emas) muncul saat terbuka
        if (opening)
            RoundRect(new Rect(body.x + w * 0.12f, body.y - h * 0.06f, w * 0.76f, h * 0.16f),
                new Color(1f, 0.92f, 0.5f, 0.85f * openP), w * 0.06f);

        // ---- tutup peti, terangkat + miring saat terbuka ----
        float lidH = h * 0.44f;
        float lift = openP * lidH * 0.85f;
        float tilt = openP * -16f;
        Rect lid = new Rect(r.x, body.y - lidH + 6f - lift, w, lidH);
        Matrix4x4 oldM = GUI.matrix;
        if (opening) GUIUtility.RotateAroundPivot(tilt, new Vector2(lid.x + 4f, lid.yMax));
        RoundRect(lid, woodL, w * 0.12f);
        RoundRect(new Rect(lid.x, lid.y, w, lidH * 0.55f), wood, w * 0.12f);
        RoundRect(new Rect(lid.x, lid.yMax - lidH * 0.22f, w, lidH * 0.22f), goldD, 4f);
        RoundRect(new Rect(lid.x, lid.y, w * 0.05f, lidH), goldD, 4f);
        RoundRect(new Rect(lid.xMax - w * 0.05f, lid.y, w * 0.05f, lidH), goldD, 4f);

        // kilau kecil menyapu tutup (glint) - jalan terus biar tidak terlihat mati
        {
            float gsweep = Mathf.Repeat(tt * 0.5f, 1.4f) / 1.4f;   // 0..1 menyapu
            float gx = lid.x + gsweep * w;
            float ga = 0.35f * Mathf.Sin(gsweep * Mathf.PI);       // muncul-hilang halus
            if (ga > 0.01f)
                RoundRect(new Rect(gx - w * 0.05f, lid.y + 3f, w * 0.10f, lidH * 0.5f),
                    new Color(1f, 1f, 0.9f, ga), w * 0.05f);
        }
        GUI.matrix = oldM;

        // ---- gembok emas di depan ----
        float lockS = w * 0.17f;
        float lockY = body.y - lockS * 0.30f;
        RoundRect(new Rect(r.x + w * 0.5f - lockS * 0.5f, lockY, lockS, lockS), gold, lockS * 0.26f);
        RoundRect(new Rect(r.x + w * 0.5f - lockS * 0.14f, lockY + lockS * 0.30f, lockS * 0.28f, lockS * 0.42f), goldD, 3f);

        // ---- kilau (sparkle): idle sedikit + lembut, terbuka banyak + terang ----
        {
            int ns = opening ? 6 : 3;
            float baseA = opening ? 0.85f * openP : 0.5f;
            for (int i = 0; i < ns; i++)
            {
                float a = (i / (float)ns) * Mathf.PI * 2f + tt * 1.4f;
                float rad = w * (0.30f + 0.16f * Mathf.Sin(tt * 3f + i * 1.7f));
                float sx = r.center.x + Mathf.Cos(a) * rad;
                float sy = (lid.y + lidH * 0.2f) + Mathf.Sin(a) * rad * 0.65f;
                float ss = Mathf.Max(6f, w * 0.06f) * (0.55f + 0.45f * Mathf.Sin(tt * 6f + i));
                float twk = 0.45f + 0.55f * Mathf.Sin(tt * 5f + i * 2f);
                DrawSparkle(new Vector2(sx, sy), ss, new Color(1f, 0.96f, 0.65f, baseA * twk));
            }
        }

        GUI.matrix = baseM;
    }

    void DrawSparkle(Vector2 c, float s, Color col)
    {
        RoundRect(new Rect(c.x - s * 0.10f, c.y - s * 0.5f, s * 0.20f, s), col, s * 0.08f);
        RoundRect(new Rect(c.x - s * 0.5f, c.y - s * 0.10f, s, s * 0.20f), col, s * 0.08f);
    }

    // ------------------------------------------------------------------
    //  (4) GESER UI supaya tidak ketutup MREC (banner 300x250 di atas)
    // ------------------------------------------------------------------
    public float MrecUiShift()
    {
#if KUBIKA_ADMOB
        if (!MrecShouldShow) return 0f;
        float scale = UiScale <= 0f ? 1f : UiScale;
        float dpi = Screen.dpi <= 1f ? 160f : Screen.dpi;
        float px = 250f * (dpi / 160f);                 // MREC tinggi 250dp
        px = Mathf.Clamp(px, Screen.height * 0.10f, Screen.height * 0.5f);
        float mrecBottom = px / scale + 22f;            // + margin bawah MREC
        float refTop = VH * 0.27f;                      // judul paling atas ketiga layar
        return Mathf.Max(0f, mrecBottom - refTop);
#else
        return 0f;
#endif
    }
}

// =====================================================================
//  Gerbang iklan: bekukan game selama loading/iklan + gambar overlay loading.
// =====================================================================
[DefaultExecutionOrder(-31000)]
public class KubikaAdGate : MonoBehaviour
{
    Tetris3D game;
    bool tsHeld;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        var go = new GameObject("KubikaAdGate");
        DontDestroyOnLoad(go);
        go.AddComponent<KubikaAdGate>();
    }

    void FindGame()
    {
        if (game == null) game = Object.FindFirstObjectByType<Tetris3D>();
    }

    void Update()
    {
        FindGame();
        bool loading = game != null && game.AdLoadingActive;
        bool adActive = loading || Tetris3D.AdFullscreenShowing;

        if (adActive)
        {
            tsHeld = true;
            if (Time.timeScale != 0f) Time.timeScale = 0f; // bekukan balok jatuh
        }
        else if (tsHeld)
        {
            tsHeld = false;
            Time.timeScale = 1f; // lanjut normal setelah iklan selesai
        }
    }

    void OnGUI()
    {
        FindGame();
        if (game == null || !game.AdLoadingActive) return; // hanya saat memuat (bukan saat iklan fullscreen)
        GUI.depth = -20000;
        game.ApplyUiScale();
        game.DrawAdLoadingOverlay();
    }
}

// =====================================================================
//  HUD animasi koin masuk: pantau saldo Koin -> koin terbang ke chip.
// =====================================================================
[DefaultExecutionOrder(-780)]
public class KubikaCoinFlyHUD : MonoBehaviour
{
    Tetris3D game;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        var go = new GameObject("KubikaCoinFlyHUD");
        DontDestroyOnLoad(go);
        go.AddComponent<KubikaCoinFlyHUD>();
    }

    void FindGame()
    {
        if (game == null) game = Object.FindFirstObjectByType<Tetris3D>();
    }

    void Update()
    {
        FindGame();
        if (game == null) return;
        game.CoinFlyObserve();
        game.CoinFlyTick();
    }

    void OnGUI()
    {
        if (Tetris3D.AdFullscreenShowing) return;
        FindGame();
        if (game == null || !game.CurrencyHudVisible) return;
        GUI.depth = -900;
        game.ApplyUiScale();
        game.DrawCoinFlyOverlay();
    }
}

// =====================================================================
//  Pemantau peti: kalau progress peti TURUN (berarti 1 peti didapat),
//  picu animasi peti terbuka + kilau di overlay SALDOKU.
// =====================================================================
public class KubikaPetiWatcher : MonoBehaviour
{
    Tetris3D game;
    int prev = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        var go = new GameObject("KubikaPetiWatcher");
        DontDestroyOnLoad(go);
        go.AddComponent<KubikaPetiWatcher>();
    }

    void Update()
    {
        if (game == null) game = Object.FindFirstObjectByType<Tetris3D>();
        if (game == null) return;
        int p = game.PetiProgress;
        if (prev >= 0 && p < prev) game.TriggerPetiOpenAnim();
        prev = p;
    }
}

// =====================================================================
//  (OPSI B) PETI SPRITE ROYAL
//  Muat sprite peti Royal (SPR_Royal_Close / SPR_Royal_Open) dari Resources
//  saat start, lalu balikkan Texture-nya lewat Report(opening) untuk
//  digambar LANGSUNG via GUI.DrawTexture di overlay SALDOKU (DrawPetiChest).
//  Cara ini anti-gagal di URP (tidak pakai kamera/RenderTexture) dan
//  layering IMGUI tetap benar. Kalau sprite tidak ada -> Report balikkan
//  null -> pemanggil pakai fallback peti gambar-kode.
// =====================================================================
[DefaultExecutionOrder(-760)]
public class KubikaPetiChest3D : MonoBehaviour
{
    static KubikaPetiChest3D I;

    const string SPR_CLOSE = "Modern 2D Animated Chests Pack_FREE Demo/Chests/Royal/Sprites/SPR_Royal_Close";
    const string SPR_OPEN  = "Modern 2D Animated Chests Pack_FREE Demo/Chests/Royal/Sprites/SPR_Royal_Open";

    Texture texClose;
    Texture texOpen;
    bool ok;
    bool triedBuild;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        var go = new GameObject("KubikaPetiChest3D");
        DontDestroyOnLoad(go);
        go.AddComponent<KubikaPetiChest3D>();
    }

    void Awake()
    {
        I = this;
        Build();
    }

    void Build()
    {
        if (triedBuild) return;
        triedBuild = true;

        // Sprite di-import sebagai Single sprite -> ambil tekstur sumbernya.
        var sClose = Resources.Load<Sprite>(SPR_CLOSE);
        var sOpen  = Resources.Load<Sprite>(SPR_OPEN);
        texClose = sClose != null ? sClose.texture : Resources.Load<Texture2D>(SPR_CLOSE) as Texture;
        texOpen  = sOpen  != null ? sOpen.texture  : Resources.Load<Texture2D>(SPR_OPEN) as Texture;
        if (texOpen == null) texOpen = texClose;    // aman kalau sprite terbuka tak ada
        ok = texClose != null;                       // -> fallback peti kode kalau gagal
    }

    // Dipanggil dari OnGUI (DrawPetiChest). Balikkan tekstur peti sesuai
    // status: terbuka -> sprite Open, selain itu -> sprite Close.
    // return null kalau sprite tidak tersedia -> pemanggil pakai fallback.
    public static Texture Report(bool opening)
    {
        if (I == null || !I.ok) return null;
        return (opening && I.texOpen != null) ? I.texOpen : I.texClose;
    }
}
