using UnityEngine;

// =====================================================================
//  KUBIKA TOWER - PEMILIH FONT UI (Fatality FPS Gaming Font)
// ---------------------------------------------------------------------
//  File TERPISAH (partial) - ADDITIVE, tidak mengubah file gameplay inti.
//
//  Tujuan: mengganti font UI game ke font "Square-Black" (Fatality FPS
//  Gaming Font) TANPA perlu mengedit Tetris3D.cs.
//
//  Strategi (urut dari paling andal):
//    0. Muat LANGSUNG lewat path pasti di dalam Resources:
//         Assets/Resources/FPSFont/FPS Gaming Font/Square-Black.ttf
//       -> path Resources = "FPSFont/FPS Gaming Font/Square-Black"
//    1. Kalau gagal, scan semua font di Resources & cari nama yang
//       mengandung kata kunci (square/fatal/fps/gaming).
//    2. Kalau tetap gagal, pakai font lama (ThaleahFat).
//    3. Cadangan terakhir: font apa pun yang ada.
//
//  Komponen KubikaFontApplier di bawah akan menimpa uiFont tiap frame
//  selama beberapa detik pertama, jadi font default dari Start() ketimpa.
// =====================================================================

public partial class Tetris3D
{
    Font _fontOverrideCache;
    bool _fontOverridePicked;

    public void ApplyUiFontOverride()
    {
        if (!_fontOverridePicked)
        {
            _fontOverrideCache = PickUiFont();
            _fontOverridePicked = true;
            Debug.Log("[KubikaFont] Font terpilih: " +
                (_fontOverrideCache != null ? _fontOverrideCache.name : "NULL (tidak ketemu)"));
        }
        if (_fontOverrideCache != null) uiFont = _fontOverrideCache;
    }

    Font PickUiFont()
    {
        // 0) Muat LANGSUNG lewat path pasti (paling andal, tanpa ekstensi).
        string[] paths =
        {
            "FPSFont/FPS Gaming Font/Square-Black",
            "FPS Gaming Font/Square-Black",
            "FPSFont/Square-Black",
            "Square-Black",
        };
        foreach (string p in paths)
        {
            Font direct = Resources.Load<Font>(p);
            if (direct != null) return direct;
        }

        // 1) Scan semua font di Resources, cocokkan nama dgn kata kunci.
        string[] keys = { "square", "fatal", "fps", "gaming" };
        Font[] all = Resources.LoadAll<Font>("");
        if (all != null)
        {
            foreach (Font f in all)
            {
                if (f == null) continue;
                string n = f.name.ToLowerInvariant();
                foreach (string k in keys)
                    if (n.Contains(k)) return f;
            }
        }

        // 2) Cadangan: font lama (Thaleah) - biar game tetap normal.
        Font fb = Resources.Load<Font>("ThaleahFat_TTF");
        if (fb != null) return fb;

        // 3) Cadangan terakhir: font apa pun yang tersedia.
        if (all != null && all.Length > 0) return all[0];
        return null;
    }
}

// ---------------------------------------------------------------------
//  Bootstrap: cari Tetris3D setelah scene load, lalu terapkan font tiap
//  frame selama 5 detik pertama (cukup untuk menimpa font dari Start()).
// ---------------------------------------------------------------------
[DefaultExecutionOrder(-25000)]
public class KubikaFontApplier : MonoBehaviour
{
    Tetris3D game;
    float t;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        var go = new GameObject("KubikaFontApplier");
        DontDestroyOnLoad(go);
        go.AddComponent<KubikaFontApplier>();
    }

    void Update()
    {
        t += Time.unscaledDeltaTime;
        if (t > 5f) { enabled = false; return; }
        if (game == null) game = Object.FindFirstObjectByType<Tetris3D>();
        if (game == null) return;
        game.ApplyUiFontOverride();
    }
}
