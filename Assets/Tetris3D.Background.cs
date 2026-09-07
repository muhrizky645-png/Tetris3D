using UnityEngine;
using UnityEngine.Rendering.Universal;

// =====================================================================
//  KUBIKA TOWER - BATCH B: LATAR GRADIEN + GELEMBUNG
// ---------------------------------------------------------------------
//  Adopsi dari KubikaBlast/Assets/Scripts/BlastBackground.cs.
//  Spek lengkapnya ada di HANDOFF-BATCH-B.md.
//
//  File TERPISAH (partial) - ADDITIF. Tidak ada satu baris pun di
//  Tetris3D.cs / Part2 / Part3 / Part4 yang diubah.
//
//  == Bagian 0 dokumen: KONFLIK BgGradient ==
//  Tetris3D SUDAH punya quad "BgGradient" (dibuat SetupScene(), di-parent ke
//  kamera, pakai bgMat). BlastBackground juga bikin quad bernama sama. Kalau
//  di-port mentah hasilnya DUA quad bertumpuk + z-fighting. Di sini dipakai
//  "cara minimal": quad & material yang sudah ada DIPAKAI ULANG, yang ditukar
//  hanya isi teksturnya. Tidak ada quad latar kedua.
//
//  == Bagian 1 dokumen: TIDAK ADA EVENT ==
//  Tetris3D tidak punya OnCleared/OnLevelUp. Semua reaksi di sini POLLING per
//  frame, mengikuti pola prevLines/prevGameOver milik Extras.cs.
//
//  == Bagian 5 dokumen: JEBAKAN SERIALISASI INSPECTOR ==
//  bgTop/bgBottom sudah ter-serialize di SampleScene, jadi mengubah defaultnya
//  di kode TIDAK berefek. Karena itu semua tombol pengatur di bawah adalah
//  field public BARU: belum ada di scene -> default C# langsung menang, tanpa
//  perlu membuka Inspector sama sekali.
// =====================================================================

public partial class Tetris3D
{
    // ---------------- TOMBOL PENGATUR (field public BARU) ----------------
    // Set kubikaBackground = false untuk kembali ke gradien stage yang lama.
    public bool kubikaBackground = true;
    public bool kubikaBgBubbles = true;
    public int kubikaBgPreset = -1;            // -1 = ikut level, 0..5 = kunci satu preset
    public float kubikaBubbleRate = 9f;        // emisi dasar gelembung (per detik)
    [Range(0f, 1f)] public float kubikaBgFlash = 0.55f;
    public bool kubikaBgReactToClears = true;

    // ---------------- PRESET GRADIEN (BlastBackground.cs) ----------------
    // Aturan desain: tidak ada channel >= 0.90, saturasi rendah-sedang, kontras
    // atas-bawah sempit, dan ATAS lebih gelap dari bawah supaya HUD putih (LEVEL,
    // skor) tetap terbaca karena tidak punya kartu di belakangnya.
    static readonly Color[] KbgTop =
    {
        new Color(0.52f, 0.68f, 0.80f),   // 1. biru kabut
        new Color(0.64f, 0.62f, 0.80f),   // 2. lilac lembut
        new Color(0.50f, 0.72f, 0.70f),   // 3. sage teal
        new Color(0.84f, 0.68f, 0.58f),   // 4. terracotta lembut
        new Color(0.58f, 0.74f, 0.60f),   // 5. hijau daun muda
        new Color(0.84f, 0.66f, 0.68f),   // 6. rose berdebu
    };
    static readonly Color[] KbgBot =
    {
        new Color(0.86f, 0.84f, 0.76f),   // 1. pasir lembut
        new Color(0.86f, 0.82f, 0.85f),   // 2. blush kelabu
        new Color(0.82f, 0.86f, 0.78f),   // 3. sage pucat
        new Color(0.87f, 0.83f, 0.74f),   // 4. linen hangat
        new Color(0.80f, 0.86f, 0.80f),   // 5. mint kelabu
        new Color(0.84f, 0.82f, 0.88f),   // 6. lavender kelabu
    };

    // Di latar TERANG, gelembung putih tidak kelihatan. Dipakai tint gelap supaya
    // terbaca sebagai bayangan lembut yang naik.
    static readonly Color KBG_TINT = new Color(0.22f, 0.30f, 0.46f);
    // alpha akhir = startColor.a * colorOverLifetime.a. Kurva alpha di bawah
    // sengaja dibuat plateau = 1 (murni fade masuk/keluar) supaya angka ini
    // benar-benar jadi opacity puncak, tidak dikalikan dua kali.
    const float KBG_ALPHA = 0.24f;
    const float KBG_TINT_KEEP = 0.85f;
    // ApplyGeometry() menaruh quad latar di zBg = dist + 120f. Offset itu dipakai
    // untuk membaca balik jarak kamera tanpa mengubah ApplyGeometry().
    const float KBG_QUAD_OFFSET = 120f;

    // ---------------- STATE RUNTIME (non-serialisasi) ----------------
    Texture2D kbgGradTex;
    Texture2D kbgDotTex;
    Color[] kbgPixels;
    ParticleSystem kbgPs;
    Color kbgCurTop = new Color(0.52f, 0.68f, 0.80f);
    Color kbgCurBot = new Color(0.86f, 0.84f, 0.76f);
    Color kbgTgtTop = new Color(0.52f, 0.68f, 0.80f);
    Color kbgTgtBot = new Color(0.86f, 0.84f, 0.76f);
    float kbgFlash;
    float kbgLayoutZ = float.MinValue;
    int kbgLastLevel = int.MinValue;
    int kbgPrevLines;

    int KbgPresetIndex(int lv)
    {
        int n = KbgTop.Length;
        if (kubikaBgPreset >= 0) return kubikaBgPreset % n;
        int i = (lv - 1) % n;
        if (i < 0) i += n;
        return i;
    }

    static bool KbgFar(Color a, Color b)
    {
        return Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) > 0.006f;
    }

    // ================== LOOP (dipanggil KubikaBgDriver.LateUpdate) ==================
    public void TickKubikaBackground()
    {
        if (!kubikaBackground) return;
        if (bgMat == null || cam == null) return;   // SetupScene() belum jalan

        // Gelembung: dibuat sekali, lalu ditata ulang tiap kali diameter tabung
        // berubah (ApplyGeometry menggeser bgTf, jadi z-nya dipakai sebagai penanda).
        KbgEnsureBubbles();
        KbgLayoutBubbles();

        // ---- POLLING 1: ganti preset saat naik level ----
        if (level != kbgLastLevel)
        {
            kbgLastLevel = level;
            int idx = KbgPresetIndex(level);
            kbgTgtTop = KbgTop[idx];
            kbgTgtBot = KbgBot[idx];
        }

        // ---- POLLING 2: deteksi tepi 'lines' = pengganti event OnCleared ----
        // Sengaja pakai penghitung sendiri (kbgPrevLines), BUKAN prevLines milik
        // Extras.cs, supaya haptic di Part3.Update() tidak terganggu.
        if (lines > kbgPrevLines)
        {
            if (kubikaBgReactToClears)
            {
                int gained = lines - kbgPrevLines;
                float c = Mathf.Clamp01((comboCount - 1) / 7f);
                kbgFlash = Mathf.Min(1f, kbgFlash + 0.45f + c * 0.55f);
                if (kbgPs != null) kbgPs.Emit(Mathf.Clamp(gained * 4, 2, 14));
            }
        }
        kbgPrevLines = lines;

        // ---- Peluruhan warna & kilatan ----
        // unscaledDeltaTime, BUKAN deltaTime: Gelembung2 menyetel Time.timeScale = 0
        // selagi dialog klaim item terbuka, dan latar tidak boleh ikut membeku.
        bool dirty = false;

        if (KbgFar(kbgCurTop, kbgTgtTop) || KbgFar(kbgCurBot, kbgTgtBot))
        {
            float k = Time.unscaledDeltaTime * 2f;
            kbgCurTop = Color.Lerp(kbgCurTop, kbgTgtTop, k);
            kbgCurBot = Color.Lerp(kbgCurBot, kbgTgtBot, k);
            dirty = true;
        }

        if (kbgFlash > 0.001f)
        {
            kbgFlash = Mathf.MoveTowards(kbgFlash, 0f, Time.unscaledDeltaTime * 1.7f);
            dirty = true;
        }

        // Rebut balik kendali kalau ApplyStageColors() baru saja menimpa bgMat saat
        // naik babak. Ini yang membuat Tetris3D.cs tidak perlu diubah sama sekali.
        if (kbgGradTex == null || bgMat.mainTexture != kbgGradTex) dirty = true;

        if (dirty) ApplyKubikaBackground();

        // ---- Denyut bloom dijahit ke Bloom yang SUDAH ADA (bukan Volume kedua) ----
        // Part3.Update() menulis bloom.intensity.value tiap frame; kita di LateUpdate
        // sehingga nilai ini yang terakhir menang.
        if (bloom != null && kbgFlash > 0.001f)
            bloom.intensity.value = bloomIntensity + kbgFlash * Mathf.Clamp01(kubikaBgFlash) * 1.2f;

        // ---- POLLING 3: emisi gelembung naik selama combo ----
        if (kbgPs != null)
        {
            var em = kbgPs.emission;
            float boost = 1f + Mathf.Clamp01((comboCount - 1) / 6f) * 1.6f;
            em.rateOverTime = kubikaBubbleRate * boost;
        }
    }

    // ================== GAMBAR GRADIEN ==================
    void ApplyKubikaBackground()
    {
        // Kilatan sesudah clear. Targetnya BUKAN putih murni - kilatan putih di atas
        // latar terang terasa seperti lampu blitz. Cukup ditarik sedikit ke krem
        // pucat, bobot atas & bawah beda supaya terasa "denyut", bukan silau.
        float b = kbgFlash * Mathf.Clamp01(kubikaBgFlash);
        Color top = Color.Lerp(kbgCurTop, new Color(0.92f, 0.91f, 0.86f), b * 0.30f);
        Color bot = Color.Lerp(kbgCurBot, new Color(0.92f, 0.91f, 0.88f), b * 0.16f);

        if (kbgGradTex == null)
        {
            kbgGradTex = new Texture2D(4, 128, TextureFormat.RGBA32, false);
            kbgGradTex.wrapMode = TextureWrapMode.Clamp;
            kbgGradTex.filterMode = FilterMode.Bilinear;
        }

        int w = kbgGradTex.width, h = kbgGradTex.height;
        // Buffer dipakai ulang: fungsi ini jalan tiap frame selama kilatan meluruh,
        // jadi alokasi baru tiap kali akan bikin sampah GC di HP.
        if (kbgPixels == null || kbgPixels.Length != w * h) kbgPixels = new Color[w * h];
        for (int y = 0; y < h; y++)
        {
            float t = (float)y / (h - 1);
            Color c = Color.Lerp(bot, top, t);
            int row = y * w;
            for (int x = 0; x < w; x++) kbgPixels[row + x] = c;
        }
        kbgGradTex.SetPixels(kbgPixels);
        kbgGradTex.Apply();

        bgMat.mainTexture = kbgGradTex;
        if (bgMat.HasProperty("_BaseMap")) bgMat.SetTexture("_BaseMap", kbgGradTex);
        if (bgMat.HasProperty("_BaseColor")) bgMat.SetColor("_BaseColor", Color.white);

        if (cam != null) cam.backgroundColor = bot;

        if (kbgPs != null)
        {
            var main = kbgPs.main;
            // Ambil sedikit nuansa warna preset, tapi tint gelapnya tetap dominan
            // supaya gelembung tidak menyatu dengan latar.
            Color pc = Color.Lerp(top, KBG_TINT, KBG_TINT_KEEP);
            pc.a = KBG_ALPHA;
            main.startColor = pc;
        }
    }

    // ================== GELEMBUNG ==================
    // Jarak kamera dibaca balik dari quad latar (ApplyGeometry: zBg = dist + 120),
    // supaya ApplyGeometry() tidak perlu diubah.
    float KbgCamDist()
    {
        if (bgTf == null) return 0f;
        return Mathf.Max(1f, bgTf.localPosition.z - KBG_QUAD_OFFSET);
    }

    void KbgEnsureBubbles()
    {
        if (!kubikaBgBubbles || kbgPs != null || cam == null || bgTf == null) return;

        GameObject pgo = new GameObject("BgBubbles");
        pgo.transform.SetParent(cam.transform, false);
        pgo.transform.localRotation = Quaternion.identity;

        kbgPs = pgo.AddComponent<ParticleSystem>();
        kbgPs.Stop();

        var main = kbgPs.main;
        main.loop = true;
        main.startLifetime = 8f;
        main.startSpeed = 0f;
        main.maxParticles = 120;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startColor = new Color(KBG_TINT.r, KBG_TINT.g, KBG_TINT.b, KBG_ALPHA);

        var emission = kbgPs.emission;
        emission.rateOverTime = kubikaBubbleRate;

        var shape = kbgPs.shape;
        shape.shapeType = ParticleSystemShapeType.Box;

        var vel = kbgPs.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.Local;
        vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        // Kurva ini MURNI fade masuk/keluar; plateau-nya 1 supaya tidak mengurangi
        // KBG_ALPHA untuk kedua kalinya.
        var colOverLife = kbgPs.colorOverLifetime;
        colOverLife.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.20f),
                new GradientAlphaKey(1f, 0.80f),
                new GradientAlphaKey(0f, 1f),
            });
        colOverLife.color = grad;

        var psr = pgo.GetComponent<ParticleSystemRenderer>();
        Shader pshader = Shader.Find("Sprites/Default");
        if (pshader == null) pshader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        Material pmat = new Material(pshader);
        pmat.mainTexture = KbgSoftDot();
        psr.material = pmat;
        psr.sortingOrder = -10;   // selalu di belakang menara

        // Murni dekoratif: tidak ada collider, tidak ada GUI.Button -> tidak mungkin
        // mencuri tap seperti gelembung item (poin 5.5 HANDOFF.md).

        KbgLayoutBubbles();
        kbgPs.Play();
    }

    // Ditata ulang otomatis tiap kali diameter tabung membesar (ApplyGeometry
    // menggeser bgTf, jadi perubahan z-nya jadi penanda).
    void KbgLayoutBubbles()
    {
        if (kbgPs == null || cam == null || bgTf == null) return;

        float bgz = bgTf.localPosition.z;
        if (Mathf.Abs(bgz - kbgLayoutZ) < 0.01f) return;
        kbgLayoutZ = bgz;

        // Di BELAKANG menara (jarak kamera + sisa radius), tapi masih jauh di depan
        // quad latar yang duduk di dist + 120.
        float pdist = KbgCamDist() + Mathf.Max(radius * 4f, 12f);
        float ph = 2f * pdist * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float pw = ph * Mathf.Max(1f, cam.aspect);

        kbgPs.transform.localPosition = new Vector3(0f, -ph * 0.5f, pdist);

        var main = kbgPs.main;
        main.startSize = new ParticleSystem.MinMaxCurve(ph * 0.03f, ph * 0.08f);

        var shape = kbgPs.shape;
        shape.scale = new Vector3(pw, 0.1f, 0.1f);

        var vel = kbgPs.velocityOverLifetime;
        vel.y = new ParticleSystem.MinMaxCurve(ph * 0.06f, ph * 0.12f);
        vel.x = new ParticleSystem.MinMaxCurve(-ph * 0.01f, ph * 0.01f);
    }

    // Titik lembut 64x64 buatan (tidak perlu aset).
    Texture2D KbgSoftDot()
    {
        if (kbgDotTex != null) return kbgDotTex;
        int s = 64;
        kbgDotTex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        kbgDotTex.wrapMode = TextureWrapMode.Clamp;
        float c = (s - 1) * 0.5f;
        var px = new Color32[s * s];
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                float a = Mathf.Clamp01(1f - d);
                a = a * a;
                px[y * s + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        kbgDotTex.SetPixels32(px);
        kbgDotTex.Apply();
        return kbgDotTex;
    }
}

// =====================================================================
//  DRIVER - pola sama persis dengan KubikaBubbleHUD di Tetris3D.Gelembung2.cs
// ---------------------------------------------------------------------
//  Bootstrap otomatis, jadi SetupScene() tidak perlu diubah untuk memasang
//  apa pun, dan Part3.Update() tidak perlu diubah untuk memanggil tick.
//
//  LateUpdate, BUKAN Update: Part3.Update() menulis bloom.intensity.value tiap
//  frame. Urutan eksekusi antar-MonoBehaviour tidak terdefinisi, jadi kalau
//  tick latar jalan di Update, denyut bloom kilatan bisa ketimpa separuh waktu.
//  LateUpdate dijamin berjalan sesudah SEMUA Update.
// =====================================================================
[DefaultExecutionOrder(25000)]
public class KubikaBgDriver : MonoBehaviour
{
    Tetris3D game;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (Object.FindFirstObjectByType<KubikaBgDriver>() != null) return;
        var go = new GameObject("KubikaBgDriver");
        DontDestroyOnLoad(go);
        go.AddComponent<KubikaBgDriver>();
    }

    void LateUpdate()
    {
        if (game == null) game = Object.FindFirstObjectByType<Tetris3D>();
        if (game != null) game.TickKubikaBackground();
    }
}
