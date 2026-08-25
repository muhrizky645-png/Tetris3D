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

    // ============ ANIMASI PERMATA DARI BLOCK -> BAR ATAS ====================
    // Tiap block yang hancur memunculkan 1 objek permata tepat di posisi block
    // itu di DUNIA 3D. Objeknya pakai GAMBAR/ASSET permata yang SAMA dengan ikon
    // Permata di HUD (KubikaIcons/Gem_A) sebagai SPRITE billboard (selalu
    // menghadap kamera) -> jadi bentuknya MIRIP ikon. Kalau asset tidak ada,
    // otomatis fallback ke mesh kristal 3D prosedural. Alur ala PECAHAN KACA:
    //  1) Muncrat dari block lalu JATUH (gravitasi dunia) lalu MENYEBAR SETENGAH
    //     MELINGKAR mengelilingi DEPAN tabung: menutupi sisi KIRI, DEPAN, dan
    //     KANAN, TAPI TIDAK di belakang. Merata sepanjang busur dengan jari-jari
    //     & tinggi beragam biar tetap terlihat menyebar (bukan garis tipis).
    //  2) DIAM sejenak.
    //  3) NAIK SATU PER SATU (makin cepat) melengkung menuju chip Permata di
    //     bar atas, sambil mengecil, lalu masuk (chip berdenyut).
    //  Kalau ADA burst BARU sebelum yang lama naik semua -> DITUMPUK (tidak
    //  reset ke chip), semua turun-kumpul lagi lalu naik bareng. Dibatasi
    //  CUR_GEM_TOTAL biar tetap ringan.
    const int CUR_GEM_MAX   = 12; // batas objek permata per burst biar tidak lag
    const int CUR_GEM_TOTAL = 24; // batas TOTAL objek permata di layar (tumpukan)

    struct CurGem3D
    {
        public Transform tf;       // objek permata di DUNIA
        public Vector3 vel;        // kecepatan dunia (fase jatuh)
        public Vector3 rest;       // titik mendarat/menggerombol di dasar
        public Vector3 riseFrom;   // posisi saat mulai naik
        public Vector3 baseScale;  // skala awal (buat mengecil saat naik)
        public float t;            // progres naik (0..1 * dur)
        public float dur;          // durasi naik permata ini (kecil = cepat)
        public float spinV;        // kecepatan putar visual (kilau, mode mesh)
        public bool arrived;       // sudah sampai chip
    }

    List<CurGem3D> curGems3D;
    float curGemPulse;
    bool  curChaQueued;
    AudioClip curSfxCoin;
    AudioClip curSfxCoinSoft;   // versi -50% volume utk bunyi permata NAIK satu-per-satu (biar tak berisik saat combo)
    List<Vector3> curBurstWorld;   // posisi WORLD block cincin terakhir yang hancur
    int   curGemPhase;    // 0=jatuh ke dasar, 1=diam menggerombol, 2=naik satu per satu
    float curGemPhaseT;   // timer fase

    static Mesh kbGemMesh;
    Material curGemMat;
    Sprite   curGemSprite;      // gambar permata (asset ikon) utk mode billboard
    bool     curGemSpriteTried; // sudah coba load asset gambar permata?

    // Mesh kristal (octahedron memanjang, faset datar biar berkilau saat kena
    // cahaya & bloom). Dibuat SEKALI lalu dipakai bersama semua permata.
    static Mesh GemMesh()
    {
        if (kbGemMesh == null) kbGemMesh = BuildGem(0.62f, 0.95f);
        return kbGemMesh;
    }

    // r = jari-jari khatulistiwa, h = setengah tinggi (mahkota atas & paviliun
    // bawah). Tiap faset punya vertex sendiri -> normal datar (flat shaded).
    static Mesh BuildGem(float r, float h)
    {
        Vector3 top = new Vector3(0f,  h, 0f);
        Vector3 bot = new Vector3(0f, -h, 0f);
        Vector3[] e = {
            new Vector3( r, 0f, 0f),
            new Vector3(0f, 0f,  r),
            new Vector3(-r, 0f, 0f),
            new Vector3(0f, 0f, -r),
        };
        var verts = new List<Vector3>();
        var norms = new List<Vector3>();
        var tris  = new List<int>();
        for (int i = 0; i < 4; i++)
        {
            Vector3 a = e[i];
            Vector3 b = e[(i + 1) % 4];
            AddGemFace(verts, norms, tris, top, a, b); // mahkota atas
            AddGemFace(verts, norms, tris, bot, b, a); // paviliun bawah
        }
        var m = new Mesh();
        m.name = "KubikaGem";
        m.SetVertices(verts);
        m.SetNormals(norms);
        m.SetTriangles(tris, 0);
        m.RecalculateBounds();
        m.RecalculateTangents();
        return m;
    }

    static void AddGemFace(List<Vector3> verts, List<Vector3> norms, List<int> tris,
        Vector3 a, Vector3 b, Vector3 c)
    {
        int bi = verts.Count;
        Vector3 nrm = Vector3.Cross(b - a, c - a).normalized;
        verts.Add(a); verts.Add(b); verts.Add(c);
        norms.Add(nrm); norms.Add(nrm); norms.Add(nrm);
        tris.Add(bi); tris.Add(bi + 1); tris.Add(bi + 2);
    }

    // Buat satu objek permata di posisi dunia tertentu.
    // Kalau ADA asset gambar permata (KubikaIcons/Gem_A -- sama dg ikon HUD),
    // pakai SpriteRenderer billboard supaya bentuknya SAMA PERSIS dengan ikon.
    // Kalau asset TIDAK ada -> fallback ke mesh kristal 3D prosedural.
    Transform CurMakeGem(Vector3 worldPos, float size)
    {
        if (!curGemSpriteTried)
        {
            curGemSpriteTried = true;
            Texture2D t = Resources.Load<Texture2D>("KubikaIcons/Gem_A");
            if (t != null)
                curGemSprite = Sprite.Create(t, new Rect(0f, 0f, t.width, t.height),
                    new Vector2(0.5f, 0.5f), 100f);
        }

        GameObject g = new GameObject("Gem");
        if (curGemSprite != null)
        {
            // ---- MODE SPRITE: pakai gambar permata (mirip ikon) ----
            var sr = g.AddComponent<SpriteRenderer>();
            sr.sprite = curGemSprite;
            sr.color = Color.white;
            // Skala supaya lebar sprite kira-kira 2x 'size' (sepadan mesh lama).
            float px = Mathf.Max(curGemSprite.bounds.size.x, curGemSprite.bounds.size.y);
            float s = (px > 0.0001f) ? (size * 2.0f / px) : size;
            g.transform.localScale = Vector3.one * s;
        }
        else
        {
            // ---- MODE MESH 3D: fallback kalau asset gambar tidak ada ----
            if (curGemMat == null)
            {
                curGemMat = MakeMat(new Color(0.62f, 0.35f, 1f)); // ungu (URP Lit + emissive)
                if (curGemMat.HasProperty("_Metallic"))   curGemMat.SetFloat("_Metallic", 0.2f);
                if (curGemMat.HasProperty("_Smoothness")) curGemMat.SetFloat("_Smoothness", 0.95f);
                if (curGemMat.HasProperty("_EmissionColor"))
                    curGemMat.SetColor("_EmissionColor", new Color(0.62f, 0.35f, 1f) * 1.4f);
            }
            var mf = g.AddComponent<MeshFilter>();
            mf.sharedMesh = GemMesh();
            var mr = g.AddComponent<MeshRenderer>();
            mr.sharedMaterial = curGemMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            g.transform.localScale = Vector3.one * size;
            g.transform.localRotation = Random.rotation;
        }
        g.transform.position = worldPos;
        return g.transform;
    }

    // Orientasi permata tiap frame: kalau mode SPRITE -> billboard menghadap
    // kamera (gambar selalu tampak penuh, sama seperti ikon). Kalau mode MESH 3D
    // -> berputar biasa biar berkilau.
    void CurSpinOrFace(Transform tf, float spinY, float spinZ, float dt)
    {
        if (tf == null) return;
        if (curGemSprite != null)
        {
            if (cam != null)
            {
                Vector3 fwd = tf.position - cam.transform.position;
                if (fwd.sqrMagnitude > 0.0001f)
                    tf.rotation = Quaternion.LookRotation(fwd.normalized, Vector3.up);
            }
        }
        else
        {
            tf.Rotate(0f, spinY * dt, spinZ * dt, Space.Self);
        }
    }

    // Dipanggil dari CurrencyTick saat dapat 'gain' permata dari line clear.
    // Tiap sel cincin yang hancur (posisi WORLD direkam CurCaptureRingBurst dari
    // FlashClear) = 1 permata yang lahir tepat di posisi block itu.
    void SpawnGemBurst(int gain)
    {
        if (curGems3D == null) curGems3D = new List<CurGem3D>();

        // Buang permata yang SUDAH sampai chip / objeknya sudah hilang.
        for (int i = curGems3D.Count - 1; i >= 0; i--)
        {
            if (curGems3D[i].arrived || curGems3D[i].tf == null)
            {
                if (curGems3D[i].tf != null) Destroy(curGems3D[i].tf.gameObject);
                curGems3D.RemoveAt(i);
            }
        }

        // Permata lama yang MASIH ada TIDAK dibuang -> DITUMPUK dengan burst baru.
        // Kembalikan ke fase jatuh/menggerombol (reset skala kalau tadi sempat
        // mengecil saat naik) supaya semua permata naik BARENG nanti, bukan reset.
        for (int i = 0; i < curGems3D.Count; i++)
        {
            CurGem3D g = curGems3D[i];
            if (g.tf != null) g.tf.localScale = g.baseScale;
            g.arrived = false; g.t = 0f; g.dur = 0f;
            curGems3D[i] = g;
        }

        // Titik lahir = posisi WORLD block yang hancur (1 block = 1 permata).
        List<Vector3> origins = new List<Vector3>();
        if (curBurstWorld != null) origins.AddRange(curBurstWorld);
        // Acak biar sebaran merata saat dipangkas ke maks.
        for (int i = origins.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            Vector3 tmp = origins[i]; origins[i] = origins[j]; origins[j] = tmp;
        }

        int n = origins.Count > 0 ? Mathf.Min(origins.Count, CUR_GEM_MAX)
                                  : Mathf.Clamp(2 + gain / 6, 3, 8);

        float size   = Mathf.Max(0.1f, blockScale.x * 0.25f); // lebih kecil dari block
        float baseY  = vSpace * 0.6f;    // dasar tabung (dekat pangkal)
        float frontZ = -radius * 0.95f;  // sisi DEPAN (menghadap kamera = -Z)
        // Sebaran SETENGAH MELINGKAR mengelilingi DEPAN tabung: menutupi sisi
        // KIRI, DEPAN, dan KANAN, TAPI TIDAK di belakang. Sudut busur
        // -arcHalf..+arcHalf (a=0 -> tepat DEPAN/-Z, a=-90 -> kiri, a=+90 ->
        // kanan). Biar terlihat MENYEBAR (bukan garis tipis) dipakai beberapa
        // \"cincin\" (band) jari-jari makin ke luar + jitter sudut/jari-jari/tinggi.
        float ringBase  = radius * 1.05f;  // jari-jari cincin TERDALAM (tepat di luar dinding tabung)
        float ringStep  = radius * 0.30f;  // jarak antar cincin ke LUAR
        int   ringBands = 3;               // jumlah cincin biar sebaran terlihat tebal
        float arcHalf   = 82f;             // setengah sudut busur (derajat); <90 supaya TAK sampai belakang
        float angJitter = 7f;              // jitter sudut (derajat) biar organik

        for (int i = 0; i < n; i++)
        {
            Vector3 birth = origins.Count > 0
                ? origins[i % origins.Count]
                : new Vector3(Random.Range(-ringBase, ringBase), baseY + 4f * vSpace, frontZ);

            CurGem3D g = new CurGem3D();
            g.tf = CurMakeGem(birth, size);
            g.baseScale = g.tf.localScale;

            // Muncrat PECAH: dorongan keluar dari pusat tabung + sedikit ke atas.
            Vector3 outward = new Vector3(birth.x, 0f, birth.z);
            if (outward.sqrMagnitude < 0.01f)
                outward = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f));
            outward = outward.normalized;
            g.vel = outward * Random.Range(1.5f, 3.5f)
                  + Vector3.up * Random.Range(1.5f, 4.5f)
                  + new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f));

            g.spinV = Random.Range(-220f, 220f);
            g.t = 0f; g.dur = 0f; g.arrived = false;
            curGems3D.Add(g);
        }

        // Batas TOTAL supaya tetap ringan: buang permata TERTUA kalau kebanyakan.
        while (curGems3D.Count > CUR_GEM_TOTAL)
        {
            if (curGems3D[0].tf != null) Destroy(curGems3D[0].tf.gameObject);
            curGems3D.RemoveAt(0);
        }

        // ---- SUSUN sebaran SETENGAH MELINGKAR (depan + samping) SEMUA permata ----
        // Permata disebar RATA sepanjang busur -arcHalf..+arcHalf (kiri -> depan ->
        // kanan), TIDAK ada di belakang. Tiap permata dapat sudut merata + jitter,
        // dan salah satu dari beberapa \"cincin\" (band) jari-jari -> jadi menyebar
        // tebal & posisinya beda-beda (organik), bukan garis tipis.
        int count = curGems3D.Count;
        for (int i = 0; i < count; i++)
        {
            CurGem3D g = curGems3D[i];

            float frac = (count > 1) ? (float)i / (count - 1) : 0.5f;
            float angDeg = Mathf.Lerp(-arcHalf, arcHalf, frac) + Random.Range(-angJitter, angJitter);
            float ang = angDeg * Mathf.Deg2Rad;

            int band = i % ringBands;                          // pilih cincin (kedalaman) bergiliran
            float rr = ringBase + band * ringStep
                     + Random.Range(-size * 0.20f, size * 0.20f);

            float x =  Mathf.Sin(ang) * rr;    // a=0 -> tengah (x=0), a=+/-90 -> samping penuh
            float z = -Mathf.Cos(ang) * rr;    // a=0 -> DEPAN (-Z); cos>0 utk |a|<90 -> TAK PERNAH +Z (belakang)

            g.rest = new Vector3(
                x,
                baseY + Random.Range(-0.06f, 0.06f) * vSpace,
                z);
            curGems3D[i] = g;
        }

        // Mulai/ulang animasi dari fase JATUH untuk SEMUA permata (lama + baru)
        // -> menumpuk mulus, tidak nge-reset lompat ke chip.
        curGemPulse = 0.32f;
        curGemPhase = 0;
        curGemPhaseT = 0f;
    }

    // Rekam posisi WORLD sel-sel cincin yang baru hancur (dipanggil FlashClear
    // sebelum sel dihancurkan). Dipakai SpawnGemBurst sebagai titik lahir permata.
    public void CurCaptureRingBurst(List<Transform> ringCells)
    {
        if (curBurstWorld == null) curBurstWorld = new List<Vector3>();
        curBurstWorld.Clear();
        if (ringCells == null) return;
        for (int i = 0; i < ringCells.Count; i++)
            if (ringCells[i] != null) curBurstWorld.Add(ringCells[i].position);
    }

    // Titik UI logis (VW/VH, dari GetHudRow) -> titik DUNIA di depan kamera pada
    // kedalaman 'depth'. Dipakai supaya permata naik tepat ke chip Permata.
    Vector3 CurUiToWorld(Vector2 uiPoint, float depth)
    {
        if (cam == null) return Vector3.zero;
        float sc = UiScale; if (sc <= 0.0001f) sc = 1f;
        Vector3 sp = new Vector3(uiPoint.x * sc, Screen.height - uiPoint.y * sc, depth);
        return cam.ScreenToWorldPoint(sp);
    }

    // Maju-kan animasi permata tiap frame + kurangi denyut chip. Dari CurrencyTick.
    void CurTickGems()
    {
        float dt = Time.unscaledDeltaTime;
        if (dt > 0.033f) dt = 0.033f; // clamp -> stabil walau ada lag spike
        if (curGemPulse > 0f) curGemPulse -= dt;

        if (curGems3D == null || curGems3D.Count == 0) return;

        // Target NAIK = titik dunia yang jatuh tepat di chip Permata (bar atas).
        Rect hsRect, gemRect, coinRect, pauseRect;
        GetHudRow(out hsRect, out gemRect, out coinRect, out pauseRect);
        float depth = (cam != null ? Vector3.Distance(cam.transform.position, Vector3.zero) : 30f) * 0.55f;
        Vector3 target = CurUiToWorld(gemRect.center, depth);

        const float GRAV         = 55f;   // gravitasi dunia (jatuh nyata)
        const float BOUNCE       = 0.22f; // pantulan kecil saat kena dasar
        const float HOLD_DUR     = 0.25f; // diam menggerombol sebelum naik
        const float RISE_DUR     = 0.32f; // durasi naik permata PERTAMA
        const float RISE_SPEEDUP = 0.82f; // <1 -> tiap permata berikutnya lebih cepat
        const float RISE_MIN     = 0.14f; // durasi naik tercepat
        const float RISE_OVERLAP = 0.70f; // berikutnya mulai saat yang skrg 70% sampai

        curGemPhaseT += dt;

        if (curGemPhase == 0)
        {
            // FASE 1: JATUH ala PECAHAN KACA + ditarik ke gerombolan dasar.
            bool allRest = true;
            for (int i = 0; i < curGems3D.Count; i++)
            {
                CurGem3D g = curGems3D[i];
                if (g.tf == null) { curGems3D[i] = g; continue; }
                g.vel.y -= GRAV * dt;
                Vector3 p = g.tf.position + g.vel * dt;
                float ax = 1f - Mathf.Exp(-4f * dt);       // tarik mendatar ke titik gerombol
                if (p.y <= g.rest.y)
                {
                    p.y = g.rest.y;
                    if (g.vel.y < -1.2f) g.vel.y = -g.vel.y * BOUNCE; else g.vel.y = 0f;
                    g.vel.x *= 0.5f; g.vel.z *= 0.5f;
                    ax = 1f - Mathf.Exp(-14f * dt);        // di dasar: rapatkan cepat
                }
                p.x += (g.rest.x - p.x) * ax;
                p.z += (g.rest.z - p.z) * ax;
                g.tf.position = p;
                CurSpinOrFace(g.tf, g.spinV, g.spinV * 0.5f, dt);
                bool resting = (p.y <= g.rest.y + 0.02f) && Mathf.Abs(g.vel.y) < 0.6f
                    && new Vector2(g.rest.x - p.x, g.rest.z - p.z).sqrMagnitude < 0.04f;
                if (!resting) allRest = false;
                curGems3D[i] = g;
            }
            if (allRest || curGemPhaseT > 1.6f) { curGemPhase = 1; curGemPhaseT = 0f; }
        }
        else if (curGemPhase == 1)
        {
            // FASE 2: DIAM MENGGEROMBOL (cuma berputar pelan) sejenak.
            for (int i = 0; i < curGems3D.Count; i++)
            {
                CurGem3D g = curGems3D[i];
                CurSpinOrFace(g.tf, g.spinV, 0f, dt);
                curGems3D[i] = g;
            }
            if (curGemPhaseT >= HOLD_DUR)
            {
                // Siapkan naik: tiap permata dapat DURASI makin singkat (makin cepat).
                float d = RISE_DUR;
                for (int i = 0; i < curGems3D.Count; i++)
                {
                    CurGem3D g = curGems3D[i];
                    if (g.tf != null) g.riseFrom = g.tf.position;
                    g.dur = d; g.t = 0f; g.arrived = false;
                    curGems3D[i] = g;
                    d *= RISE_SPEEDUP; if (d < RISE_MIN) d = RISE_MIN;
                }
                curGemPhase = 2; curGemPhaseT = 0f;
            }
        }
        else
        {
            // FASE 3: NAIK SATU PER SATU (makin cepat) melengkung ke chip.
            int done = 0;
            bool anyLanded = false;
            for (int i = 0; i < curGems3D.Count; i++)
            {
                CurGem3D g = curGems3D[i];
                if (g.arrived) { done++; continue; }
                if (g.tf == null) { g.arrived = true; done++; curGems3D[i] = g; continue; }

                // SATU PER SATU: baru naik kalau yang SEBELUMNYA sudah/hampir sampai.
                bool prevReady = (i == 0) || curGems3D[i - 1].arrived
                    || (curGems3D[i - 1].dur > 0f
                        && curGems3D[i - 1].t / curGems3D[i - 1].dur >= RISE_OVERLAP);
                if (!prevReady) { curGems3D[i] = g; continue; } // tetap DIAM di dasar

                g.t += dt;
                float q = Mathf.Clamp01(g.t / g.dur);
                float e = q * q * (3f - 2f * q);
                // Bezier kuadratik: lengkung naik yang tegas.
                Vector3 mid = (g.riseFrom + target) * 0.5f
                    + Vector3.up * (Vector3.Distance(g.riseFrom, target) * 0.18f);
                Vector3 pos = Vector3.Lerp(Vector3.Lerp(g.riseFrom, mid, e),
                                           Vector3.Lerp(mid, target, e), e);
                g.tf.position = pos;
                g.tf.localScale = Vector3.Lerp(g.baseScale, g.baseScale * 0.12f, e); // mengecil -> masuk chip
                CurSpinOrFace(g.tf, g.spinV * 2f, 0f, dt);
                if (q >= 1f)
                {
                    g.arrived = true;
                    if (g.tf != null) Destroy(g.tf.gameObject);
                    curGemPulse = 0.32f; // denyut chip tiap permata masuk
                    anyLanded = true;
                    done++;
                }
                curGems3D[i] = g;
            }

            if (anyLanded) CurPlayChaChingSoft(); // volume -50% biar tak berisik saat combo (naik satu-per-satu)

            if (done >= curGems3D.Count)
            {
                for (int i = 0; i < curGems3D.Count; i++)
                    if (curGems3D[i].tf != null) Destroy(curGems3D[i].tf.gameObject);
                curGems3D.Clear();
                curGemPhase = 0; curGemPhaseT = 0f;
            }
        }
    }

    // Permata kini objek dunia asli (dirender kamera), jadi TIDAK perlu digambar
    // di OnGUI lagi. Disisakan sebagai no-op agar pemanggil lama tetap aman.
    public void DrawGemBurst() { }

    // Denyut chip permata + bunyi saat permata masuk. Juga dipanggil saat klaim
    // gelembung Permata (ApplyBuff IT_GEM).
    public void CurGemChipPulse()
    {
        curGemPulse = 0.32f;
        CurPlayChaChing();
    }

    void CurPlayChaChing()      // versi NORMAL (koin masuk / klaim gelembung)
    {
        if (!(soundOn && sfxOn) || sfx == null) return;
        if (curSfxCoin == null) curSfxCoin = MakeTone("cur_coin", 900f, 0.10f, 0.5f, 0, 70f);
        StartCoroutine(CoChaChing(curSfxCoin));
    }

    // Versi LEBIH PELAN (volume -50%, vol 0.25 vs 0.5): dipakai saat permata NAIK
    // satu-per-satu, karena saat combo besar bunyinya bertubi-tubi & jadi berisik.
    void CurPlayChaChingSoft()
    {
        if (!(soundOn && sfxOn) || sfx == null) return;
        if (curSfxCoinSoft == null) curSfxCoinSoft = MakeTone("cur_coin_soft", 900f, 0.10f, 0.25f, 0, 70f);
        StartCoroutine(CoChaChing(curSfxCoinSoft));
    }

    IEnumerator CoChaChing(AudioClip clip)
    {
        KbSfxAt(clip, 1.0f);
        yield return new WaitForSeconds(0.07f);
        KbSfxAt(clip, 1.5f);
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
        if (game != null) game.CurrencyTick(); // animasi permata jalan tiap frame
    }

    void OnGUI()
    {
        if (Tetris3D.AdFullscreenShowing) return; // iklan fullscreen -> HUD off (iklan di depan)
        FindGame();
        if (game == null || !game.CurrencyHudVisible) return;
        game.ApplyUiScale(); // skala UI responsif (sama dengan base game)
        // Chip permata & koin di baris HUD atas (posisi diambil dari GetHudRow).
        game.DrawCurrencyHUD();
    }
}
