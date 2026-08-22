using UnityEngine;

// =====================================================================
//  KUBIKA TOWER x SALDOKU - TOKO (SHOP) + INVENTARIS BUFF
// ---------------------------------------------------------------------
//  File TERPISAH (partial) - ADDITIVE. Tidak mengubah file gameplay.
//
//   * TOKO : beli item buff pakai PERMATA (mata uang lokal). Isinya
//            item2 yang ada di gelembung KECUALI koin. Item Bonus
//            Permata juga tidak dijual (karena itu mata uangnya sendiri),
//            jadi yang dijual = 3 buff yang bisa dipakai saat main:
//            Bom, Bersihkan Baris, Perlambat.
//
//   * INVENTARIS BUFF : saat MAIN muncul 3 slot (ikon + jumlah, mis.
//            bom x0/x5). Tap slot buat pakai buff langsung (tanpa iklan,
//            karena sudah dibeli pakai permata).
// =====================================================================

public partial class Tetris3D
{
    // ---- simpanan inventaris (index = IT_BOMB/IT_LINE/IT_SLOW = 0/1/2) ----
    const string PP_BUFF_INV = "kubika_buffinv";
    int[]  tokoInv;
    bool   tokoReady;
    bool   tokoOpen;
    string tokoStatus = "";

    // Harga tiap buff (dalam Permata). Urutan: Bom, Bersihkan Baris, Perlambat.
    // Sengaja MAHAL (499) supaya pemain lebih terdorong nonton iklan.
    static readonly int[] TOKO_PRICE = new int[] { 499, 499, 499 };

    void EnsureToko()
    {
        if (tokoReady) return;
        tokoInv = new int[3];
        string s = PlayerPrefs.GetString(PP_BUFF_INV, "");
        if (!string.IsNullOrEmpty(s))
        {
            string[] parts = s.Split(',');
            for (int i = 0; i < 3 && i < parts.Length; i++) int.TryParse(parts[i], out tokoInv[i]);
        }
        tokoReady = true;
    }

    void SaveToko()
    {
        EnsureToko();
        PlayerPrefs.SetString(PP_BUFF_INV, tokoInv[0] + "," + tokoInv[1] + "," + tokoInv[2]);
        PlayerPrefs.Save();
    }

    // ---- akses publik utk komponen HUD terpisah (KubikaTokoHUD) ----
    public bool TokoOpen { get { return tokoOpen; } }

    // Tombol TOKO hanya di menu depan (bukan saat main / overlay lain).
    // Disembunyikan saat dropdown bahasa (langOpen) kebuka biar tidak tabrakan.
    public bool TokoButtonVisible
    {
        get { return !started && !showProfile && !showRanks && !SaldokuOverlayOpen && !tokoOpen && !langOpen; }
    }

    // Inventaris buff hanya tampil saat MAIN.
    public bool BuffInvVisible
    {
        get
        {
            return started && !paused && !gameOver && !showProfile && !showRanks
                   && !SaldokuOverlayOpen && !tokoOpen && !BubbleClaimOpen;
        }
    }

    public void OpenToko() { EnsureToko(); tokoOpen = true; tokoStatus = ""; }
    void CloseToko() { tokoOpen = false; tokoStatus = ""; }

    // ---- tombol pembuka TOKO: PAS DI BAWAH pemilih bahasa (pojok kanan atas) ----
    // Pemilih bahasa: DrawLangPicker(VW-112, 16), ukuran 96x46, tepi kanan = VW-16.
    // Tombol TOKO diselaraskan rata-kanan tepat di bawahnya.
    public void DrawTokoButton()
    {
        float bw = Mathf.Min(VW * 0.42f, 150f);
        float bh = 52f;
        float bx = VW - 16f - bw;        // rata kanan, tepi sama dengan pemilih bahasa
        float by = 16f + 46f + 12f;      // tepat di bawah pemilih bahasa
        Rect r = new Rect(bx, by, bw, bh);
        RoundRect(new Rect(r.x - 5f, r.y - 5f, r.width + 10f, r.height + 10f), new Color(0.85f, 0.45f, 0.95f, 0.22f), 18f);
        if (Btn3D(r, SalID ? "TOKO" : "SHOP", new Color(0.72f, 0.40f, 0.95f), false)) OpenToko();
    }

    // ---- panel TOKO ----
    public void DrawTokoShop()
    {
        EnsureToko();
        EnsureCurrency();
        float sw = VW, sh = VH;
        RoundRect(new Rect(0f, 0f, sw, sh), new Color(0f, 0f, 0f, 0.80f), 0f);

        float pw = Mathf.Min(sw * 0.9f, 640f);
        float ph = Mathf.Min(sh * 0.86f, 760f);
        float px = (sw - pw) * 0.5f, py = (sh - ph) * 0.5f;
        RoundRect(new Rect(px - 4f, py - 4f, pw + 8f, ph + 8f), new Color(0.85f, 0.45f, 0.95f, 0.45f), 26f);
        RoundRect(new Rect(px, py, pw, ph), new Color(0.06f, 0.08f, 0.12f, 0.98f), 24f);

        float cx = px + 30f, cw = pw - 60f, yy = py + 26f;
        GuiText(new Rect(cx, yy, cw, 48f), SalID ? "TOKO" : "SHOP", 40, Color.white, TextAnchor.UpperCenter);
        yy += 66f;

        // Saldo permata
        RoundRect(new Rect(cx, yy, cw, 56f), new Color(0.12f, 0.10f, 0.20f, 0.95f), 14f);
        DrawGemIcon(new Rect(cx + 14f, yy + 12f, 32f, 32f), new Color(0.62f, 0.35f, 1f));
        GuiText(new Rect(cx + 56f, yy, cw - 70f, 56f), (SalID ? "Permata: " : "Gems: ") + cur_permata, 26, new Color(0.85f, 0.8f, 1f), TextAnchor.MiddleLeft);
        yy += 72f;

        int[] types = { IT_BOMB, IT_LINE, IT_SLOW };
        float rowH = 104f, gap = 12f;
        for (int i = 0; i < 3; i++)
        {
            Rect rr = new Rect(cx, yy, cw, rowH);
            RoundRect(rr, new Color(0.10f, 0.12f, 0.18f, 0.95f), 16f);
            DrawItemIcon(new Rect(rr.x + 14f, rr.y + 14f, rowH - 28f, rowH - 28f), types[i]);
            float tx = rr.x + rowH;
            GuiText(new Rect(tx, rr.y + 12f, cw - rowH - 190f, 34f), BubbleItemName(types[i]), 26, Color.white, TextAnchor.LowerLeft);
            GuiText(new Rect(tx, rr.y + 52f, cw - rowH - 190f, 28f), (SalID ? "Punya: x" : "Owned: x") + tokoInv[i], 20, new Color(0.7f, 0.85f, 1f), TextAnchor.UpperLeft);
            float bwid = 172f;
            Rect br = new Rect(rr.xMax - bwid - 14f, rr.y + (rowH - 72f) / 2f, bwid, 72f);
            bool afford = cur_permata >= TOKO_PRICE[i];
            if (Btn3D(br, TOKO_PRICE[i] + (SalID ? " Permata" : " Gems"), afford ? new Color(0.20f, 0.72f, 0.42f) : new Color(0.40f, 0.38f, 0.44f), false))
            {
                if (SpendPermata(TOKO_PRICE[i]))
                {
                    tokoInv[i]++; SaveToko(); Sfx(sfxClear);
                    tokoStatus = (SalID ? "Dibeli: " : "Bought: ") + BubbleItemName(types[i]) + " (x" + tokoInv[i] + ")";
                }
                else tokoStatus = SalID ? "Permata kurang." : "Not enough gems.";
            }
            yy += rowH + gap;
        }

        yy += 4f;
        if (!string.IsNullOrEmpty(tokoStatus))
            GuiText(new Rect(cx, yy, cw, 30f), tokoStatus, 20, new Color(1f, 0.85f, 0.5f), TextAnchor.UpperCenter);

        GuiText(new Rect(cx, py + ph - 150f, cw, 26f), SalID ? "Pakai buff dengan tap ikonnya saat main." : "Tap the icon in-game to use a buff.", 18, new Color(0.65f, 0.72f, 0.85f), TextAnchor.UpperCenter);

        float clw = Mathf.Min(cw * 0.6f, 300f);
        bool doClose = Btn3D(new Rect(px + pw / 2f - clw / 2f, py + ph - 92f, clw, 72f), SalID ? "Tutup" : "Close", new Color(0.88f, 0.35f, 0.42f), false);

        // Swallow terakhir biar klik di luar kontrol tidak tembus ke belakang.
        GUI.Button(new Rect(0f, 0f, sw, sh), GUIContent.none, GUIStyle.none);

        if (doClose) CloseToko();
    }

    // ---- inventaris buff saat MAIN (3 slot di sisi kiri-tengah) ----
    public void DrawBuffInv()
    {
        EnsureToko();
        int[] types = { IT_BOMB, IT_LINE, IT_SLOW };
        float slot = Mathf.Min(VW * 0.15f, 96f);
        float gap = 14f;
        float totalH = 3f * slot + 2f * gap;
        float sx = 12f;
        float sy = VH * 0.5f - totalH * 0.5f;
        for (int i = 0; i < 3; i++)
        {
            Rect rr = new Rect(sx, sy + i * (slot + gap), slot, slot);
            bool has = tokoInv[i] > 0;
            RoundRect(new Rect(rr.x - 3f, rr.y - 3f, rr.width + 6f, rr.height + 6f), new Color(0.5f, 0.8f, 1f, has ? 0.30f : 0.10f), 18f);
            RoundRect(rr, new Color(0.06f, 0.08f, 0.12f, has ? 0.92f : 0.55f), 16f);

            Color prev = GUI.color;
            if (!has) GUI.color = new Color(1f, 1f, 1f, 0.35f);
            DrawItemIcon(new Rect(rr.x + rr.width * 0.16f, rr.y + rr.height * 0.08f, rr.width * 0.68f, rr.height * 0.68f), types[i]);
            GUI.color = prev;

            Rect badge = new Rect(rr.xMax - 42f, rr.yMax - 34f, 40f, 30f);
            RoundRect(badge, new Color(0.10f, 0.10f, 0.15f, 0.95f), 9f);
            GuiText(badge, "x" + tokoInv[i], 20, has ? new Color(1f, 0.95f, 0.6f) : new Color(0.8f, 0.8f, 0.85f, 0.7f), TextAnchor.MiddleCenter);

            if (has && GUI.Button(rr, GUIContent.none, GUIStyle.none)) UseBuffFromInv(i);
        }
    }

    void UseBuffFromInv(int i)
    {
        EnsureToko();
        if (i < 0 || i > 2) return;
        if (tokoInv[i] <= 0) return;
        if (!started || gameOver || paused || clearing) return;
        tokoInv[i]--;
        SaveToko();
        ApplyBuff(i);   // i == IT_BOMB/IT_LINE/IT_SLOW (0/1/2)
        Haptic(40);
    }
}

// =====================================================================
//  HUD TOKO sebagai KOMPONEN TERPISAH (auto-bootstrap, tanpa ubah scene).
//  Execution order paling awal + GUI.depth paling depan supaya tombol
//  TOKO, panel toko, dan slot buff selalu bisa di-tap.
// =====================================================================
[DefaultExecutionOrder(-26000)]
public class KubikaTokoHUD : MonoBehaviour
{
    Tetris3D game;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        var go = new GameObject("KubikaTokoHUD");
        DontDestroyOnLoad(go);
        go.AddComponent<KubikaTokoHUD>();
    }

    void FindGame()
    {
        if (game == null) game = Object.FindFirstObjectByType<Tetris3D>();
    }

    void OnGUI()
    {
        FindGame();
        if (game == null) return;
        game.ApplyUiScale();
        GUI.depth = -900;
        if (game.TokoOpen) { game.DrawTokoShop(); return; }
        if (game.TokoButtonVisible) game.DrawTokoButton();
        if (game.BuffInvVisible) game.DrawBuffInv();
    }
}
