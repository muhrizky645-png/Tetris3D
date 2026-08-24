using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// =====================================================================
//  KUBIKA TOWER - SISTEM DUA MATA UANG (Permata & Koin)
// ---------------------------------------------------------------------
//  Ditulis sebagai file TERPISAH supaya TIDAK mengubah file gameplay
//  yang sudah ada (Tetris3D.cs / Part2 / Part3 / Part4). Aman & mudah
//  di-revert.
//
//   * PERMATA (gem) : mata uang IN-GAME murni LOKAL. Dipakai buat buff/
//                     revive. Didapat gratis dari main (line clear/combo).
//                     TIDAK bernilai uang, jadi aman disimpan di device.
//
//   * KOIN          : CERMIN (mirror) saldo poin SALDOKU (1 Koin = 1 poin).
//                     READ-ONLY di game; HANYA server (via AdMob SSV) yang
//                     boleh menambah. Game cuma menampilkan nilai dari
//                     server. Terkunci sampai akun SALDOKU terhubung
//                     (fitur Hubungkan Akun menyusul).
// =====================================================================

public partial class Tetris3D
{
    // ---- kunci PlayerPrefs ----
    const string PP_PERMATA    = "kubika_permata";
    const string PP_KOIN_CACHE = "kubika_koin_cache";
    const string PP_LINKED     = "kubika_linked";

    // ---- aturan perolehan Permata (bebas, tanpa iklan) ----
    const int PERMATA_PER_LINE    = 5;  // permata per baris/cincin yang hancur
    const int PERMATA_COMBO_BONUS = 3;  // bonus per tingkat combo (saat combo >= 2)

    // ---- state ----
    int  cur_permata;    // saldo Permata lokal
    long cur_koin;       // cache saldo Koin (mirror poin SALDOKU) utk tampilan
    bool cur_linked;     // akun SALDOKU sudah terhubung?
    bool cur_online;     // status fetch Koin terakhir sukses (online)?
    bool cur_ready;      // sudah load dari PlayerPrefs?
    int  cur_linesSeen;  // pelacak 'lines' utk beri Permata tanpa ubah logika gameplay

    // ---- akses publik (dipakai HUD & fitur lain) ----
    public int  PermataBalance { get { EnsureCurrency(); return cur_permata; } }
    public long KoinBalance    { get { EnsureCurrency(); return cur_koin; } }
    public bool SaldokuLinked  { get { EnsureCurrency(); return cur_linked; } }

    // Boleh gambar HUD mata uang? (hanya saat MAIN, bukan di menu/overlay)
    public bool CurrencyHudVisible
    {
        get { return started && !paused && !gameOver && !showProfile && !showRanks; }
    }

    void EnsureCurrency()
    {
        if (cur_ready) return;
        cur_permata   = PlayerPrefs.GetInt(PP_PERMATA, 0);
        cur_koin      = PlayerPrefs.GetInt(PP_KOIN_CACHE, 0);
        cur_linked    = PlayerPrefs.GetInt(PP_LINKED, 0) == 1;
        cur_online    = false;
        cur_linesSeen = lines;
        cur_ready     = true;
    }

    public void AddPermata(int amount)
    {
        if (amount <= 0) return;
        EnsureCurrency();
        cur_permata += amount;
        PlayerPrefs.SetInt(PP_PERMATA, cur_permata); // simpan ringan (flush nanti)
    }

    // true kalau saldo cukup & berhasil dipotong
    public bool SpendPermata(int amount)
    {
        EnsureCurrency();
        if (amount <= 0) return true;
        if (cur_permata < amount) return false;
        cur_permata -= amount;
        PlayerPrefs.SetInt(PP_PERMATA, cur_permata);
        PlayerPrefs.Save();
        return true;
    }

    // Dipanggil integrasi server nanti (hasil fetch poin_game_status_apk.php).
    // Game TIDAK boleh mengubah Koin selain lewat data server ini.
    public void SetKoinFromServer(long value, bool online, bool linked)
    {
        EnsureCurrency();
        cur_koin   = value < 0 ? 0 : value;
        cur_online = online;
        cur_linked = linked;
        PlayerPrefs.SetInt(PP_KOIN_CACHE, (int)Mathf.Clamp(cur_koin, 0, int.MaxValue));
        PlayerPrefs.SetInt(PP_LINKED, cur_linked ? 1 : 0);
        PlayerPrefs.Save();
    }

    // Beri Permata dari pertambahan 'lines' TANPA menyentuh logika ResolveBoard.
    // Dipanggil tiap frame oleh komponen HUD terpisah.
    public void CurrencyTick()
    {
        EnsureCurrency();
        if (lines > cur_linesSeen)
        {
            int gain = (lines - cur_linesSeen) * PERMATA_PER_LINE;
            if (comboCount >= 2) gain += comboCount * PERMATA_COMBO_BONUS;
            AddPermata(gain);
            if (gain > 0) SpawnGemBurst(gain);
        }
        // game baru: 'lines' balik 0 -> samakan tanpa memberi Permata
        cur_linesSeen = lines;
        CurTickGems();
    }

    string CurConnect()
    {
        switch (lang)
        {
            case Lang.ID: return "Hubungkan";
            case Lang.ES: return "Conectar";
            case Lang.PT: return "Conectar";
            case Lang.FR: return "Connecter";
            default:      return "Connect";
        }
    }

    // Angka ringkas biar chip gak kepanjangan (1.2K, 3.4M)
    string CurShort(long v)
    {
        if (v >= 1000000) return (v / 1000000f).ToString("0.#") + "M";
        if (v >= 1000)    return (v / 1000f).ToString("0.#") + "K";
        return v.ToString();
    }

    // ---- Versi IN-GAME (ala Block Blast): chip Permata & Koin di baris HUD
    //      atas, sejajar dengan chip skor tertinggi & tombol Jeda.
    //      Posisi diambil dari GetHudRow (Part4.cs). Dipanggil KubikaCurrencyHUD.
    public void DrawCurrencyHUD()
    {
        EnsureCurrency();
        Rect hsRect, gemRect, coinRect, pauseRect;
        GetHudRow(out hsRect, out gemRect, out coinRect, out pauseRect);

        // Denyut ungu di sekeliling chip Permata sesaat setelah permata masuk.
        if (curGemPulse > 0f)
        {
            float pp = Mathf.Clamp01(curGemPulse / 0.32f);
            RoundRect(new Rect(gemRect.x - 7f, gemRect.y - 7f, gemRect.width + 14f, gemRect.height + 14f),
                new Color(0.62f, 0.35f, 1f, 0.55f * pp), 24f);
        }

        // Permata (selalu tampil)
        DrawCurrencyChipCompact(gemRect, new Color(0.62f, 0.35f, 1f), true,
            CurShort(cur_permata), true);

        // Koin (mirror SALDOKU; terkunci kalau belum terhubung)
        if (!cur_linked)
            DrawCurrencyChipCompact(coinRect, new Color(1f, 0.78f, 0.18f), false,
                CurConnect(), false);
        else
            DrawCurrencyChipCompact(coinRect, new Color(1f, 0.78f, 0.18f), false,
                CurShort(cur_koin) + (cur_online ? "" : " (off)"), true);
    }

    // ---- Versi MENU AWAL (berposisi): dua chip ditumpuk vertikal mulai dari
    //      (x, y). Chip Koin bisa di-tap untuk buka overlay SALDOKU; posisinya
    //      cocok dengan KoinChipRect(x, y) di Tetris3D.Saldoku.cs.
    //      Dipanggil KubikaSaldokuUI.
    public void DrawCurrencyHUD(float x, float y)
    {
        EnsureCurrency();
        float w = 300f, h = 76f, gap = 10f;
        string gemName  = (lang == Lang.ID) ? "Permata" : "Gems";
        string coinName = (lang == Lang.ID) ? "Koin" : "Coins";

        // Permata (chip atas)
        DrawCurrencyChip(new Rect(x, y, w, h), new Color(0.62f, 0.35f, 1f), true,
            gemName, CurShort(cur_permata), true);

        // Koin (chip bawah)
        Rect koin = new Rect(x, y + h + gap, w, h);
        if (!cur_linked)
            DrawCurrencyChip(koin, new Color(1f, 0.78f, 0.18f), false,
                coinName, CurConnect(), false);
        else
            DrawCurrencyChip(koin, new Color(1f, 0.78f, 0.18f), false,
                coinName, CurShort(cur_koin) + (cur_online ? "" : " (off)"), true);
    }

    // Chip ringkas (buat baris atas): panel + ikon + nilai (tanpa label nama).
    // Ukuran teks otomatis mengecil biar muat (mis. Hubungkan).
    void DrawCurrencyChipCompact(Rect r, Color accent, bool gem, string value, bool active)
    {
        RoundRect(new Rect(r.x - 3f, r.y - 3f, r.width + 6f, r.height + 6f),
            new Color(accent.r, accent.g, accent.b, 0.22f), 20f);
        RoundRect(r, new Color(0.06f, 0.08f, 0.12f, 0.92f), 18f);

        float ic = r.height - 22f;
        Rect ir = new Rect(r.x + 12f, r.y + 11f, ic, ic);
        if (gem) DrawGemIcon(ir, accent); else DrawCoinIcon(ir, accent);

        float tx = ir.xMax + 10f;
        float tw = r.width - (tx - r.x) - 10f;
        int fs = 30;
        if (uiFont != null)
        {
            GUIStyle ms = new GUIStyle { fontStyle = FontStyle.Bold, font = uiFont };
            while (fs > 14) { ms.fontSize = fs; if (ms.CalcSize(new GUIContent(value)).x <= tw) break; fs -= 2; }
        }
        GuiText(new Rect(tx, r.y, tw, r.height), value, fs,
            active ? Color.white : new Color(1f, 0.85f, 0.5f), TextAnchor.MiddleLeft);
    }

    // Chip: panel melengkung + ikon (gem/coin digambar manual) + label + nilai.
    void DrawCurrencyChip(Rect r, Color accent, bool gem, string name, string value, bool active)
    {
        RoundRect(new Rect(r.x - 3f, r.y - 3f, r.width + 6f, r.height + 6f),
            new Color(accent.r, accent.g, accent.b, 0.22f), 20f);
        RoundRect(r, new Color(0.06f, 0.08f, 0.12f, 0.92f), 18f);

        float ic = r.height - 24f;
        Rect ir = new Rect(r.x + 14f, r.y + 12f, ic, ic);
        if (gem) DrawGemIcon(ir, accent); else DrawCoinIcon(ir, accent);

        float tx = ir.xMax + 14f;
        float tw = r.width - (tx - r.x) - 12f;
        GuiText(new Rect(tx, r.y + 8f, tw, 26f), name, 22,
            new Color(accent.r, accent.g, accent.b, 0.95f), TextAnchor.UpperLeft);
        GuiText(new Rect(tx, r.y + 34f, tw, 40f), value, 34,
            active ? Color.white : new Color(1f, 0.85f, 0.5f), TextAnchor.UpperLeft);
    }

    // ---- ikon mata uang: pakai gambar impor kalau ada, else gambar prosedural ----
    Texture2D gemTex; bool gemTexTried;
    Texture2D coinTex; bool coinTexTried;

    void DrawGemIcon(Rect r, Color c)
    {
        if (!gemTexTried) { gemTex = Resources.Load<Texture2D>("KubikaIcons/Gem_A"); gemTexTried = true; }
        if (gemTex != null) { GUI.DrawTexture(r, gemTex, ScaleMode.ScaleToFit, true); return; }
        RoundRect(new Rect(r.x + r.width * 0.15f, r.y + r.height * 0.10f, r.width * 0.70f, r.height * 0.80f),
            c, r.width * 0.28f);
        RoundRect(new Rect(r.x + r.width * 0.30f, r.y + r.height * 0.16f, r.width * 0.40f, r.height * 0.22f),
            new Color(1f, 1f, 1f, 0.55f), r.width * 0.16f);
    }

    void DrawCoinIcon(Rect r, Color c)
    {
        if (!coinTexTried) { coinTex = Resources.Load<Texture2D>("KubikaIcons/Coin_A"); coinTexTried = true; }
        if (coinTex != null) { GUI.DrawTexture(r, coinTex, ScaleMode.ScaleToFit, true); return; }
        RoundRect(r, new Color(c.r * 0.6f, c.g * 0.5f, c.b * 0.15f, 1f), r.width * 0.5f);
        RoundRect(new Rect(r.x + r.width * 0.12f, r.y + r.height * 0.12f, r.width * 0.76f, r.height * 0.76f),
            c, r.width * 0.5f);
        GuiText(new Rect(r.x, r.y - 1f, r.width, r.height), "K",
            Mathf.RoundToInt(r.height * 0.7f), new Color(0.5f, 0.35f, 0.05f), TextAnchor.MiddleCenter);
    }

    // ============ ANIMASI PERMATA: PECAH DARI BLOCK -> NAIK KE CHIP =========
    // Tiap block yang hancur menghasilkan 1 butir permata (DIBATASI maks
    // CUR_GEM_MAX biar ringan). Alur ala PECAHAN KACA:
    //  1) Permata muncrat sedikit dari block lalu JATUH LURUS KE BAWAH dengan
    //     GRAVITASI NYATA (makin cepat). Sambil jatuh, tiap butiran DIARAHKAN
    //     ke SATU tumpukan RAPAT di paling DASAR (ketinggian sama, berdekatan),
    //     memantul kecil, lalu MENGGEROMBOL (bukan melayang!).
    //  2) DIAM MENGGEROMBOL sejenak di dasar.
    //  3) NAIK BENAR-BENAR SATU PER SATU ke chip Permata di HUD atas, ritme
    //     MAKIN CEPAT. Yang belum giliran tetap DIAM di dasar.
    // Chip berdenyut ungu tiap permata mendarat.
    const int CUR_GEM_MAX = 12; // batas partikel permata biar tidak lag
    struct CurGem { public float x, y, vx, vy, hx, hy, tx, ty, t, dur, delay, rot, rotv, size; public bool hooked; }
    List<CurGem> curGems;
    float curGemPulse;
    bool  curChaQueued;
    AudioClip curSfxCoin;
    List<Vector3> curBurstWorld;   // posisi WORLD sel cincin terakhir yang hancur
    int   curGemPhase;    // 0=jatuh ke dasar, 1=diam menggerombol, 2=naik satu per satu
    float curGemPhaseT;   // timer fase (fase 0-1) & timer global fase 2 (utk delay giliran)

    // Dipanggil dari CurrencyTick saat dapat 'gain' permata dari line clear.
    // Tiap sel cincin yang hancur (posisi direkam CurCaptureRingBurst dari
    // FlashClear) = 1 permata. Permata jatuh ke DASAR seperti pecahan kaca dan
    // menggerombol RAPAT di paling bawah.
    void SpawnGemBurst(int gain)
    {
        if (curGems == null) curGems = new List<CurGem>();

        // Kalau masih ada butiran dari clear sebelumnya yang belum sampai chip,
        // tuntaskan dulu (jangan dicampur) supaya tidak terlihat berantakan.
        if (curGems.Count > 0)
        {
            curGems.Clear();
            curGemPulse = 0.32f;
        }

        // Kumpulkan titik asal dari sel cincin (world -> koordinat UI logis).
        // Tiap sel = 1 block yang hancur -> jadi 1 permata. Ini cuma TITIK LAHIR
        // (tempat pecah); tujuan mendaratnya ditentukan terpisah (tx,ty) di dasar.
        List<Vector2> origins = new List<Vector2>();
        if (curBurstWorld != null)
        {
            for (int i = 0; i < curBurstWorld.Count; i++)
            {
                float ux, uy;
                if (CurWorldToUi(curBurstWorld[i], out ux, out uy))
                    origins.Add(new Vector2(ux, uy));
            }
        }

        // Acak urutan biar sebaran merata di sekeliling cincin (juga saat dipangkas ke maks).
        for (int i = origins.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            Vector2 tmp = origins[i]; origins[i] = origins[j]; origins[j] = tmp;
        }

        // 1 permata per block yang hancur, DIBATASI CUR_GEM_MAX biar tidak lag.
        int n = origins.Count > 0 ? Mathf.Min(origins.Count, CUR_GEM_MAX)
                                  : Mathf.Clamp(2 + gain / 6, 3, 8);

        for (int i = 0; i < n; i++)
        {
            CurGem g = new CurGem();
            if (origins.Count > 0)
            {
                Vector2 o = origins[i % origins.Count];
                g.x = o.x;
                // Titik PECAH dipaksa turun ke area DASAR (dekat pangkal silinder)
                // supaya butiran TIDAK pernah nongol melayang di tengah layar.
                g.y = Mathf.Max(o.y, VH * 0.72f) + Random.Range(-8f, 8f);
            }
            else
            {
                // fallback (posisi cincin tak diketahui): dari dekat dasar.
                g.x = VW * 0.5f + Random.Range(-40f, 40f);
                g.y = VH * 0.74f + Random.Range(-12f, 12f);
            }
            // PECAH seperti kaca lalu JATUH LURUS KE BAWAH karena gravitasi.
            // TIDAK ada dorongan ke atas (biar tidak terkesan melayang). Tiap
            // butiran punya TUJUAN mendarat (tx,ty) = MENGGEROMBOL di DASAR pada
            // SISI KIRI / KANAN silinder (bergantian), lalu menumpuk di sana.
            // Semua permata mendarat di SATU tumpukan RAPAT di paling DASAR (dekat
            // pangkal silinder) dengan KETINGGIAN SAMA & posisi saling BERDEKATAN.
            g.tx = VW * 0.5f + Random.Range(-0.14f, 0.14f) * VW; // kumpul rapat di tengah-bawah
            g.ty = VH * 0.88f + Random.Range(-6f, 6f);           // ketinggian SAMA di paling bawah
            g.vx = Random.Range(-20f, 20f);                              // muncratan samping kecil saja
            g.vy = Random.Range(0f, 60f);                                // HANYA ke bawah (tanpa pop ke atas)
            g.t = 0f;
            g.dur = 0f;
            g.delay = 0f;
            g.rot = Random.Range(0f, 360f);
            g.rotv = Random.Range(-260f, 260f);
            g.size = Random.Range(30f, 44f);
            g.hooked = false;
            curGems.Add(g);
        }

        // Reset timeline: jatuh ke dasar -> diam menggerombol -> naik satu per satu.
        // Combo beruntun mulai ulang dari fase 0.
        curGemPhase = 0;
        curGemPhaseT = 0f;
    }

    // Rekam posisi WORLD sel-sel cincin yang baru hancur (dipanggil FlashClear
    // sebelum sel dihancurkan). Dipakai SpawnGemBurst sebagai titik asal butiran.
    public void CurCaptureRingBurst(List<Transform> ringCells)
    {
        if (curBurstWorld == null) curBurstWorld = new List<Vector3>();
        curBurstWorld.Clear();
        if (ringCells == null) return;
        for (int i = 0; i < ringCells.Count; i++)
            if (ringCells[i] != null) curBurstWorld.Add(ringCells[i].position);
    }

    // Konversi titik dunia -> koordinat UI logis (VW/VH) sesuai ApplyUiScale.
    bool CurWorldToUi(Vector3 world, out float ux, out float uy)
    {
        ux = 0f; uy = 0f;
        if (cam == null) return false;
        Vector3 sp = cam.WorldToScreenPoint(world);
        if (sp.z <= 0f) return false;                 // di belakang kamera
        float sc = UiScale; if (sc <= 0.0001f) sc = 1f;
        ux = sp.x / sc;
        uy = (Screen.height - sp.y) / sc;             // GUI y dihitung dari atas
        return true;
    }

    // Maju-kan animasi permata tiap frame + kurangi denyut chip. Dari CurrencyTick.
    void CurTickGems()
    {
        float dt = Time.unscaledDeltaTime;
        if (dt > 0.033f) dt = 0.033f; // clamp -> fisika stabil walau ada lag spike
        if (curGemPulse > 0f) curGemPulse -= dt;

        if (curGems == null || curGems.Count == 0) return;

        Rect hsRect, gemRect, coinRect, pauseRect;
        GetHudRow(out hsRect, out gemRect, out coinRect, out pauseRect);
        Vector2 target = gemRect.center;

        const float GRAV          = 6000f; // gravitasi KUAT -> jatuh nyata & makin cepat
        const float BOUNCE        = 0.24f; // pantulan kecil saat kena dasar
        const float HOLD_DUR      = 0.55f; // DIAM MENGGEROMBOL di dasar sebelum naik
        const float RISE_DUR      = 0.34f; // durasi naik permata PERTAMA (berikutnya makin cepat)
        const float RISE_SPEEDUP  = 0.86f; // <1 -> tiap permata berikutnya naik lebih cepat
        const float RISE_MIN      = 0.14f; // durasi naik tercepat
        const float RISE_OVERLAP  = 0.85f; // permata berikut baru mulai saat yang skrg 85% sampai

        curGemPhaseT += dt;

        if (curGemPhase == 0)
        {
            // FASE 1: JATUH LURUS ala PECAHAN KACA. Gravitasi menyeret ke bawah;
            // sambil jatuh tiap butiran DIGESER mendekati tumpukan rapat di dasar
            // (tx). Sampai dasar -> memantul kecil -> DIAM menumpuk.
            bool allRest = true;
            for (int i = 0; i < curGems.Count; i++)
            {
                CurGem g = curGems[i];
                g.vy += GRAV * dt;
                g.y  += g.vy * dt;
                g.x  += g.vx * dt;
                float approach = 1f - Mathf.Exp(-5f * dt);   // geser mendatar menuju kolom tumpukan
                if (g.y >= g.ty)
                {
                    g.y = g.ty;
                    if (g.vy > 90f) g.vy = -g.vy * BOUNCE;    // memantul kecil
                    else            g.vy = 0f;                // berhenti
                    g.vx *= 0.4f;
                    approach = 1f - Mathf.Exp(-16f * dt);     // di dasar: rapatkan ke tumpukan
                }
                g.x += (g.tx - g.x) * approach;
                g.rot += g.rotv * dt;
                if (g.y >= g.ty - 0.5f) g.rotv *= Mathf.Clamp01(1f - 4f * dt); // putaran meredam saat mendarat
                curGems[i] = g;
                bool resting = (g.y >= g.ty - 0.5f) && Mathf.Abs(g.vy) < 18f && Mathf.Abs(g.tx - g.x) < 3f;
                if (!resting) allRest = false;
            }
            // lanjut kalau semua sudah diam menumpuk (atau batas waktu aman).
            if (allRest || curGemPhaseT > 1.5f) { curGemPhase = 1; curGemPhaseT = 0f; }
        }
        else if (curGemPhase == 1)
        {
            // FASE 2: DIAM MENGGEROMBOL di dasar (tanpa gerak melayang) sejenak.
            if (curGemPhaseT >= HOLD_DUR)
            {
                // Siapkan NAIK SATU PER SATU dengan jeda MENGECIL (makin cepat).
                // Tiap permata dapat DURASI naik sendiri yang makin singkat
                // (makin cepat). Naiknya BENAR-BENAR satu per satu (lihat fase 3).
                float d = RISE_DUR;
                for (int i = 0; i < curGems.Count; i++)
                {
                    CurGem g = curGems[i];
                    g.hx = g.x; g.hy = g.y;   // titik awal naik (dari dasar)
                    g.dur = d;                // durasi naik permata ini
                    g.t = 0f;
                    g.hooked = false;
                    curGems[i] = g;
                    d *= RISE_SPEEDUP;
                    if (d < RISE_MIN) d = RISE_MIN;
                }
                curGemPhase = 2; curGemPhaseT = 0f;
            }
        }
        else
        {
            // FASE 3: NAIK SATU PER SATU (makin cepat). Yang belum giliran DIAM
            // di dasar; yang naik terbang MELENGKUNG (Bezier) ke chip.
            int done = 0;
            bool anyLanded = false;
            for (int i = 0; i < curGems.Count; i++)
            {
                CurGem g = curGems[i];
                if (g.hooked) { done++; continue; }
                // SATU PER SATU: permata ini baru boleh naik kalau yang SEBELUMNYA
                // sudah mendarat, atau sudah hampir sampai (biar mengalir mulus).
                bool prevReady = (i == 0) || curGems[i - 1].hooked
                    || (curGems[i - 1].t / curGems[i - 1].dur >= RISE_OVERLAP);
                if (!prevReady) { curGems[i] = g; continue; } // tetap DIAM di tumpukan bawah

                g.t += dt;
                float q = Mathf.Clamp01(g.t / g.dur);
                float e = q * q * (3f - 2f * q);
                float u = 1f - e;
                float mx = (g.hx + target.x) * 0.5f;
                float my = (g.hy + target.y) * 0.5f - 34f; // kontrol sedikit di atas -> lintasan naik yang tegas
                g.x = u * u * g.hx + 2f * u * e * mx + e * e * target.x;
                g.y = u * u * g.hy + 2f * u * e * my + e * e * target.y;
                g.size = Mathf.Lerp(g.size, 12f, e);
                g.rot += g.rotv * dt;
                if (q >= 1f)
                {
                    g.hooked = true;
                    curGemPulse = 0.32f; // denyut chip tiap permata mendarat
                    anyLanded = true;
                    done++;
                }
                curGems[i] = g;
            }

            if (anyLanded) CurPlayChaChing(); // ding tiap ada permata mendarat

            if (done >= curGems.Count)
            {
                curGems.Clear();
                curGemPhase = 0; curGemPhaseT = 0f;
            }
        }
    }

    // Gambar butiran permata. Dipanggil KubikaCurrencyHUD.OnGUI (di atas HUD).
    public void DrawGemBurst()
    {
        if (curGems == null || curGems.Count == 0) return;
        Color gc = new Color(0.62f, 0.35f, 1f);
        for (int i = 0; i < curGems.Count; i++)
        {
            CurGem g = curGems[i];
            Rect ir = new Rect(g.x - g.size * 0.5f, g.y - g.size * 0.5f, g.size, g.size);
            Matrix4x4 m = GUI.matrix;
            GUIUtility.RotateAroundPivot(g.rot, new Vector2(g.x, g.y));
            DrawGemIcon(ir, gc);
            GUI.matrix = m;
        }
    }

    // Denyut chip permata + bunyi saat permata mendarat. Juga dipanggil saat
    // klaim gelembung Permata (ApplyBuff IT_GEM).
    public void CurGemChipPulse()
    {
        curGemPulse = 0.32f;
        CurPlayChaChing();
    }

    void CurPlayChaChing()
    {
        if (!(soundOn && sfxOn) || sfx == null) return;
        if (curSfxCoin == null) curSfxCoin = MakeTone("cur_coin", 900f, 0.10f, 0.5f, 0, 70f);
        StartCoroutine(CoChaChing());
    }

    IEnumerator CoChaChing()
    {
        KbSfxAt(curSfxCoin, 1.0f);
        yield return new WaitForSeconds(0.07f);
        KbSfxAt(curSfxCoin, 1.5f);
        yield return null;
        if (sfx != null) sfx.pitch = 1f;
    }
}

// =====================================================================
//  HUD mata uang sebagai KOMPONEN TERPISAH.
//  Dibuat otomatis saat game mulai (RuntimeInitializeOnLoadMethod) jadi
//  TIDAK perlu setting scene & TIDAK perlu mengubah OnGUI yang sudah ada.
// =====================================================================
public class KubikaCurrencyHUD : MonoBehaviour
{
    Tetris3D game;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        var go = new GameObject("KubikaCurrencyHUD");
        DontDestroyOnLoad(go);
        go.AddComponent<KubikaCurrencyHUD>();
    }

    void FindGame()
    {
        if (game == null) game = Object.FindFirstObjectByType<Tetris3D>();
    }

    void Update()
    {
        FindGame();
        if (game != null) game.CurrencyTick();
    }

    void OnGUI()
    {
        FindGame();
        if (game == null || !game.CurrencyHudVisible) return;
        game.ApplyUiScale(); // skala UI responsif (sama dengan base game)
        // Chip permata & koin di baris HUD atas (posisi diambil dari GetHudRow).
        game.DrawCurrencyHUD();
        game.DrawGemBurst();
    }
}
