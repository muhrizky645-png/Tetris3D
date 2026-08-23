using UnityEngine;

// =====================================================================
//  KUBIKA TOWER - PEMILIH FONT UI (Fatality FPS Gaming Font)
// ---------------------------------------------------------------------
//  File TERPISAH (partial) - ADDITIVE, tidak mengubah file gameplay inti.
//
//  Tujuan: mengganti font UI game ke "Fatality FPS Gaming Font" TANPA
//  perlu mengedit Tetris3D.cs. Font dicari OTOMATIS di folder Resources
//  lewat nama yang mengandung salah satu kata kunci: "fatal", "fps",
//  atau "gaming" (huruf besar/kecil bebas). Kalau font itu belum ada,
//  otomatis jatuh ke font lama (ThaleahFat) supaya game tetap normal.
//
//  == CARA MENGAKTIFKAN (WAJIB) ==
//    1. Taruh file font-nya di dalam folder "Resources" (boleh di subfolder,
//       mis. Assets/Resources/FPSFont/...). Ini SUDAH benar.
//    2. Commit & push file .ttf + semua .meta-nya ke GitHub.
//    3. Selesai - font otomatis kepakai di semua teks & tombol.
// =====================================================================

public partial class Tetris3D
{
    Font _fontOverrideCache;
    bool _fontOverridePicked;

    // Dipanggil komponen bootstrap di bawah, setelah scene siap.
    // Aman dipanggil berkali-kali: pencarian font hanya dilakukan sekali.
    public void ApplyUiFontOverride()
    {
        if (!_fontOverridePicked)
        {
            _fontOverrideCache = PickUiFont();
            _fontOverridePicked = true;
        }
        if (_fontOverrideCache != null) uiFont = _fontOverrideCache;
    }

    Font PickUiFont()
    {
        // 1) Utamakan font gaming baru kalau sudah ada di Resources.
        //    Cari nama file font yang mengandung "fatal"/"fps"/"gaming".
        Font[] all = Resources.LoadAll<Font>("");
        if (all != null)
        {
            foreach (Font f in all)
            {
                if (f == null) continue;
                string n = f.name.ToLowerInvariant();
                if (n.Contains("fatal") || n.Contains("fps") || n.Contains("gaming"))
                    return f;
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
//  Bootstrap: cari Tetris3D setelah scene load, lalu terapkan font.
//  Tidak perlu mengubah scene / prefab apa pun. Berhenti otomatis
//  setelah 3 detik (cukup untuk menimpa font default dari Start()).
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
        if (t > 3f) { enabled = false; return; }   // cukup 3 detik lalu berhenti
        if (game == null) game = Object.FindFirstObjectByType<Tetris3D>();
        if (game == null) return;
        game.ApplyUiFontOverride();
    }
}
