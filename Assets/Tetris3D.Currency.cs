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
//                     (fitur \"Hubungkan Akun\" menyusul).
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
        }
        // game baru: 'lines' balik 0 -> samakan tanpa memberi Permata
        cur_linesSeen = lines;
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

    // Gambar 2 chip mata uang mulai dari (x,y), stack ke bawah.
    public void DrawCurrencyHUD(float x, float y)
    {
        EnsureCurrency();
        float w = 300f, h = 76f, gap = 10f;

        // Permata (selalu tampil)
        DrawCurrencyChip(new Rect(x, y, w, h), new Color(0.62f, 0.35f, 1f), true,
            "Permata", CurShort(cur_permata), true);

        // Koin (mirror SALDOKU; terkunci kalau belum terhubung)
        float y2 = y + h + gap;
        if (!cur_linked)
            DrawCurrencyChip(new Rect(x, y2, w, h), new Color(1f, 0.78f, 0.18f), false,
                "Koin", CurConnect(), false);
        else
            DrawCurrencyChip(new Rect(x, y2, w, h), new Color(1f, 0.78f, 0.18f), false,
                "Koin", CurShort(cur_koin) + (cur_online ? "" : " (offline)"), true);
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

    void DrawGemIcon(Rect r, Color c)
    {
        RoundRect(new Rect(r.x + r.width * 0.15f, r.y + r.height * 0.10f, r.width * 0.70f, r.height * 0.80f),
            c, r.width * 0.28f);
        RoundRect(new Rect(r.x + r.width * 0.30f, r.y + r.height * 0.16f, r.width * 0.40f, r.height * 0.22f),
            new Color(1f, 1f, 1f, 0.55f), r.width * 0.16f);
    }

    void DrawCoinIcon(Rect r, Color c)
    {
        RoundRect(r, new Color(c.r * 0.6f, c.g * 0.5f, c.b * 0.15f, 1f), r.width * 0.5f);
        RoundRect(new Rect(r.x + r.width * 0.12f, r.y + r.height * 0.12f, r.width * 0.76f, r.height * 0.76f),
            c, r.width * 0.5f);
        GuiText(new Rect(r.x, r.y - 1f, r.width, r.height), "K",
            Mathf.RoundToInt(r.height * 0.7f), new Color(0.5f, 0.35f, 0.05f), TextAnchor.MiddleCenter);
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
        // Di bawah papan skor (papan skor: y 12..210). Chip mulai y = 218.
        game.DrawCurrencyHUD(14f, 218f);
    }
}
