using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Leaderboards;
using Unity.Services.Leaderboards.Models;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Random = UnityEngine.Random;
using Object = UnityEngine.Object;

public partial class Tetris3D : MonoBehaviour
{
    [Header("Bentuk tabung")]
    public int startColumns = 15;   // kolom awal (kecil = gampang bikin cincin)
    public int maxColumns = 30;     // batas kolom saat diameter membesar
    public int columnsPerStage = 3; // tambahan kolom tiap babak baru
    public int height = 18;
    public float radius = 3.4f;     // radius awal (ikut membesar tiap babak)
    public float vSpace = 1.35f;

    [Header("Kecepatan jatuh (tetap sepanjang game)")]
    public float fallInterval = 0.8f;

    [Header("Skor & level")]
    public int cellPoints = 10;      // poin per kotak (skor cincin = jumlah kolom x ini x combo)
    public int baseLevelScore = 600; // skor buat naik ke level 2
    public int levelStep = 250;      // tiap level, syarat naik nambah segini (berjenjang)
    public float comboSeconds = 10f; // jendela combo: clear lagi dalam sekian detik -> pengali naik

    [Header("Tantangan (tangga kesulitan)")]
    public int levelsPerStage = 4;      // tiap sekian level -> babak baru (diameter membesar)
    public int ceilingDropPerLevel = 1; // plafon turun sekian baris tiap naik level
    public int minPlayHeight = 11;      // plafon paling rendah
    public int garbageGapCount = 2;     // lubang di baris sampah (fase diameter maks)
    public int stoneStartLevel = 18;    // mulai level ini muncul balok batu (gak bisa diputar)
    [Range(0f, 1f)] public float stoneChance = 0.25f;

    [Header("Tampilan blok")]
    public float blockFill = 0.92f;
    public float blockDepth = 0.55f;

    [Header("Kamera & background")]
    public float cameraZoom = 1.02f;
    public float cameraAngle = 8f;
    public Color backgroundColor = new Color(0.06f, 0.07f, 0.12f);

    [Header("Kontrol geser")]
    public float dragStep = 0.05f;
    public bool dragReversed = false;

    [Header("Warna & latar")]
    public bool rainbowColors = true;
    public Color bgTop = new Color(0.05f, 0.04f, 0.14f);
    public Color bgBottom = new Color(0.24f, 0.07f, 0.38f);
    public Color[] palette = new Color[]
    {
        new Color(0.00f, 0.90f, 1.00f),
        new Color(1.00f, 0.85f, 0.10f),
        new Color(0.75f, 0.20f, 1.00f),
        new Color(0.20f, 1.00f, 0.45f),
        new Color(1.00f, 0.25f, 0.35f),
        new Color(0.25f, 0.50f, 1.00f),
        new Color(1.00f, 0.55f, 0.10f),
        new Color(1.00f, 0.30f, 0.75f),
        new Color(0.55f, 1.00f, 0.20f),
        new Color(0.10f, 1.00f, 0.85f),
        new Color(0.85f, 0.40f, 1.00f),
        new Color(1.00f, 0.75f, 0.30f),
    };

    [Header("Efek cahaya (atur kalau kesilauan)")]
    public float bloomIntensity = 1f;
    public float bloomThreshold = 0.9f;
    public float bloomScatter = 0.4f;
    public float emissionStrength = 0.75f;
    public float vignetteAmount = 0.28f;

    [Header("Suara")]
    public bool soundOn = true;
    public bool sfxOn = true;
    public bool musicOn = true;
    public float sfxVolume = 0.5f;
    public float musicVolume = 0.22f;

    [Header("Gameplay")]
    public bool ghostPiece = true;

    [Header("Progresi bentuk balok")]
    public int mediumShapeLevel = 3;      // mulai level ini: bentuk sedang (pentomino rapi) ikut muncul
    public int weirdShapeColumns = 20;    // mulai kolom sebanyak ini: bentuk aneh muncul
    public int weirdShapeLevel = 12;      // atau mulai level ini: bentuk aneh muncul
    [Range(0f, 1f)] public float assistStart = 0.85f;   // peluang balok pas di celah (level 1)
    public float assistDecayPerLevel = 0.05f;           // bantuan berkurang tiap naik level
    [Range(0f, 1f)] public float assistMin = 0.15f;     // batas bawah bantuan

    int columns;
    float baseRadius;
    int[,] grid;
    GameObject[,] cells;
    int killLine;
    int stage;
    int maxDiameterLevel = -1;  // level saat diameter mentok maks (buat eskalasi endgame)
    int nextLevelScore;
    bool stoneEnabled;

    readonly int[] boxSize = { 2, 3, 3, 2, 2, 3, 3, 3, 3, 5, 4, 3, 3, 3, 3, 4, 4 };
    readonly int[][] shapes = new int[][]
    {
        new int[]{0,0, 1,0},                    // 0  Domino (tier 0)
        new int[]{0,0, 1,0, 2,0},               // 1  Garis-3 (tier 0)
        new int[]{0,0, 0,1, 0,2},               // 2  Garis-3 tegak (tier 0)
        new int[]{0,0, 1,0, 0,1},               // 3  Sudut-3 / L kecil (tier 0)
        new int[]{0,0, 1,0, 1,1},               // 4  Sudut-3 / L kecil cermin (tier 0)
        new int[]{0,2, 1,2, 2,2, 1,1, 1,0},     // 5  Pentomino T (tier 1)
        new int[]{0,1, 2,1, 0,0, 1,0, 2,0},     // 6  Pentomino U (tier 1)
        new int[]{0,2, 0,1, 0,0, 1,0, 2,0},     // 7  Pentomino V (tier 1)
        new int[]{1,2, 0,1, 1,1, 2,1, 1,0},     // 8  Pentomino X / plus (tier 1)
        new int[]{0,0, 1,0, 2,0, 3,0, 4,0},     // 9  Garis-5 (tier 1)
        new int[]{0,3, 0,2, 0,1, 0,0, 1,0},     // 10 L-panjang (tier 1)
        new int[]{0,2, 1,2, 0,1, 1,1, 0,0},     // 11 Pentomino P (tier 2 - aneh)
        new int[]{1,2, 2,2, 0,1, 1,1, 1,0},     // 12 Pentomino F (tier 2 - aneh)
        new int[]{0,2, 0,1, 1,1, 1,0, 2,0},     // 13 Pentomino W (tier 2 - aneh)
        new int[]{0,2, 1,2, 1,1, 1,0, 2,0},     // 14 Pentomino Z (tier 2 - aneh)
        new int[]{1,3, 0,2, 1,2, 1,1, 1,0},     // 15 Pentomino Y (tier 2 - aneh)
        new int[]{1,3, 1,2, 0,1, 1,1, 0,0},     // 16 Pentomino N (tier 2 - aneh)
    };

    // Tingkat bentuk: 0 = sederhana (level awal), 1 = sedang, 2 = aneh (kolom 20+ / level tinggi)
    readonly int[] shapeTier = { 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 2, 2 };

    int curType, curN, nextType;
    Vector2Int[] curBox;
    int curCol;
    int curRow;
    bool curStone;
    GameObject[] active;
    GameObject[] ghost;

    float fallTimer;
    int score, lines, level = 1;
    float levelUpTime;
    int comboShow;
    float comboTime;
    int comboCount;      // streak combo berbasis waktu (naik tiap clear dalam jendela)
    float comboExpire;   // sisa detik sebelum combo putus
    bool gameOver;
    bool paused;
    bool started;
    bool hintDone;
    bool btnSoftDrop;
    bool clearing;

    Texture2D triTex;
    Texture2D crownTex;
    int highScore;
    bool pointerDown;
    Font uiFont;

    // ---------- Bahasa (localization) ----------
    public enum Lang { EN, ID, ES, PT, FR }
    Lang lang = Lang.EN;
    bool langOpen;
    static readonly string[] langCodes = { "EN", "ID", "ES", "PT", "FR" };
    static readonly string[] langNames = { "English", "Indonesia", "Espa\u00f1ol", "Portugu\u00eas", "Fran\u00e7ais" };
    Dictionary<string, string[]> loc;

    // ---------- Leaderboard (Unity Gaming Services) ----------
    const string LB_ID = "tetris3d_global";
    bool ugsReady;
    bool submitting;
    bool profileDone;
    bool showProfile;      // layar Buat Profil (muncul saat game over pertama)
    bool showRanks;        // layar PERINGKAT (Top 10 global)
    bool pendingSubmit;    // skor nunggu dikirim setelah UGS siap
    bool gameOverHandled;  // biar profil/submit cuma sekali tiap game over
    bool ranksLoading;
    bool countryPicking;
    int myRank = -1;
    string playerName = "";
    string playerCountry = "ID";
    string lbStatus = "";
    Vector2 countryScroll;
    Vector2 ranksScroll;
    bool homeRanksRequested;
    bool editingProfile;
    struct LbEntry { public int rank; public string name; public string country; public long score; public bool you; }
    List<LbEntry> ranks = new List<LbEntry>();
    static readonly string[] countryCodes = { "ID", "US", "GB", "IN", "BR", "JP", "KR", "CN", "DE", "FR", "ES", "IT", "RU", "CA", "AU", "MX", "AR", "NL", "TR", "SA", "AE", "EG", "ZA", "NG", "PH", "TH", "VN", "MY", "SG", "PK", "BD", "PL", "SE", "NO", "FI", "DK", "PT", "GR", "UA", "RO", "CZ", "HU", "AT", "CH", "BE", "IE", "NZ", "CL", "CO", "PE" };
    static readonly string[] countryNames = { "Indonesia", "United States", "United Kingdom", "India", "Brazil", "Japan", "South Korea", "China", "Germany", "France", "Spain", "Italy", "Russia", "Canada", "Australia", "Mexico", "Argentina", "Netherlands", "Turkey", "Saudi Arabia", "UAE", "Egypt", "South Africa", "Nigeria", "Philippines", "Thailand", "Vietnam", "Malaysia", "Singapore", "Pakistan", "Bangladesh", "Poland", "Sweden", "Norway", "Finland", "Denmark", "Portugal", "Greece", "Ukraine", "Romania", "Czechia", "Hungary", "Austria", "Switzerland", "Belgium", "Ireland", "New Zealand", "Chile", "Colombia", "Peru" };

    Transform spin;
    float spinDeg, targetSpin;
    Camera cam;
    Vector3 blockScale;
    Vector3 camBasePos;
    float shakeTime, shakeDur, shakeMag;
    Material particleMat;
    Bloom bloom;
    Vignette vig;
    AudioSource sfx, music;
    AudioClip sfxRotate, sfxLock, sfxDrop, sfxClear, sfxGameOver, sfxLevelUp, musicClip;

    Transform coreTf;
    Transform bgTf;
    Material bgMat;
    Transform killRingTf;
    Material killRingMat;

    bool dragging;
    float lastMouseX, dragAccum;

    void Start()
    {
        Screen.orientation = ScreenOrientation.Portrait;
        InitLoc();
        if (PlayerPrefs.HasKey("tetris3d_lang")) lang = (Lang)PlayerPrefs.GetInt("tetris3d_lang");
        else lang = DetectLang();
        highScore = PlayerPrefs.GetInt("tetris3d_hi", 0);
        uiFont = Resources.Load<Font>("FPSFont/FPS Gaming Font/Square-Black");
        baseRadius = radius;
        columns = Mathf.Max(3, startColumns);
        AllocGrid();
        killLine = height;
        nextLevelScore = baseLevelScore;
        SetupScene();
        nextType = PickNextType();
        LoadProfile();
        InitUGS();
    }

    void AllocGrid()
    {
        grid = new int[columns, height];
        cells = new GameObject[columns, height];
        for (int c = 0; c < columns; c++)
            for (int r = 0; r < height; r++)
                grid[c, r] = -1;
    }

    // ---------- SCENE ----------
    void SetupScene()
    {
        spin = new GameObject("Tower").transform;
        spin.position = Vector3.zero;

        cam = Camera.main;
        if (cam == null)
        {
            GameObject cg = new GameObject("Main Camera");
            cg.tag = "MainCamera";
            cam = cg.AddComponent<Camera>();
        }
        cam.transform.SetParent(null);
        cam.fieldOfView = 55f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = bgTop;

        var camData = cam.GetUniversalAdditionalCameraData();
        if (camData != null) camData.renderPostProcessing = true;
        GameObject volGO = new GameObject("Global Volume");
        Volume vol = volGO.AddComponent<Volume>();
        vol.isGlobal = true;
        vol.priority = 1f;
        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
        vol.profile = profile;
        bloom = profile.Add<Bloom>(true);
        bloom.intensity.Override(bloomIntensity);
        bloom.threshold.Override(bloomThreshold);
        bloom.scatter.Override(bloomScatter);
        bloom.tint.Override(new Color(1f, 0.92f, 1f));
        vig = profile.Add<Vignette>(true);
        vig.intensity.Override(vignetteAmount);
        vig.smoothness.Override(0.7f);
        vig.color.Override(new Color(0.04f, 0.02f, 0.10f));

        Shader psh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (psh == null) psh = Shader.Find("Particles/Standard Unlit");
        if (psh == null) psh = Shader.Find("Sprites/Default");
        particleMat = new Material(psh);

        GameObject bg = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(bg.GetComponent<Collider>());
        bg.name = "BgGradient";
        bg.transform.SetParent(cam.transform);
        bg.transform.localRotation = Quaternion.identity;
        Shader us = Shader.Find("Universal Render Pipeline/Unlit");
        if (us == null) us = Shader.Find("Unlit/Texture");
        bgMat = new Material(us);
        bg.GetComponent<Renderer>().material = bgMat;
        bgTf = bg.transform;

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.25f, 0.22f, 0.35f);

        GameObject sun = new GameObject("Sun");
        Light lt = sun.AddComponent<Light>();
        lt.type = LightType.Directional;
        lt.intensity = 0.9f;
        lt.color = new Color(1f, 0.96f, 0.9f);
        sun.transform.rotation = Quaternion.Euler(35f, 20f, 0f);

        GameObject fill = new GameObject("Fill");
        Light fl = fill.AddComponent<Light>();
        fl.type = LightType.Directional;
        fl.intensity = 0.5f;
        fl.color = new Color(0.4f, 0.5f, 1f);
        fill.transform.rotation = Quaternion.Euler(-10f, 210f, 0f);

        GameObject core = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(core.GetComponent<Collider>());
        core.transform.SetParent(spin);
        Material coreMat = MakeMat(new Color(0.10f, 0.10f, 0.18f));
        if (coreMat.HasProperty("_EmissionColor")) coreMat.SetColor("_EmissionColor", new Color(0.06f, 0.05f, 0.14f));
        core.GetComponent<Renderer>().material = coreMat;
        coreTf = core.transform;

        GameObject kr = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(kr.GetComponent<Collider>());
        kr.name = "KillRing";
        kr.transform.SetParent(spin);
        killRingMat = MakeUnlitTransparent(new Color(1f, 0.2f, 0.25f, 0.35f));
        kr.GetComponent<Renderer>().material = killRingMat;
        killRingTf = kr.transform;
        kr.SetActive(false);

        triTex = MakeTriTex(32);
        crownTex = Resources.Load<Texture2D>("KubikaIcons/Crown_A");
        if (crownTex == null) crownTex = MakeCrownTex(64);

        SetupAudio();

        ApplyGeometry();
        ApplyStageColors();
    }

    // Skala ulang blok, kamera, inti, latar sesuai columns/radius sekarang
    void ApplyGeometry()
    {
        radius = baseRadius * columns / Mathf.Max(1, startColumns);
        float arc = 2f * Mathf.PI * radius / columns;
        vSpace = arc;                                     // tinggi baris = lebar arc -> kotak SELALU persegi di semua diameter
        blockScale = new Vector3(arc * blockFill, arc * blockFill, blockDepth);

        float towerH = height * vSpace;
        float centerY = towerH * 0.5f;
        float dist = towerH * cameraZoom + radius * 2.2f;
        float camY = centerY + Mathf.Tan(cameraAngle * Mathf.Deg2Rad) * dist;
        float aimY = centerY - towerH * 0.06f; // arahin kamera agak ke bawah biar menara naik & ada jarak dari tombol
        if (cam != null)
        {
            cam.transform.position = new Vector3(0f, camY, -dist);
            cam.transform.LookAt(new Vector3(0f, aimY, 0f));
            camBasePos = cam.transform.position;
        }
        if (bgTf != null)
        {
            float zBg = dist + 120f;
            bgTf.localPosition = new Vector3(0f, 0f, zBg);
            float bgScale = zBg * 3f;
            bgTf.localScale = new Vector3(bgScale, bgScale, 1f);
        }
        if (coreTf != null)
        {
            coreTf.localPosition = new Vector3(0f, towerH * 0.5f - vSpace * 0.5f, 0f);
            coreTf.localScale = new Vector3(radius * 1.15f, towerH * 0.5f, radius * 1.15f);
        }
    }

    // Warna latar berganti tiap babak (stage) - dibikin cerah & menyenangkan
    void ApplyStageColors()
    {
        Color top, bottom;
        if (stage == 0) { top = new Color(0.20f, 0.48f, 0.88f); bottom = new Color(0.88f, 0.46f, 0.82f); }
        else
        {
            float hue = (0.62f + stage * 0.13f) % 1f;
            top = Color.HSVToRGB(hue, 0.52f, 0.62f);
            bottom = Color.HSVToRGB((hue + 0.08f) % 1f, 0.58f, 0.90f);
        }
        Texture2D grad = MakeGradientTex(top, bottom, 256);
        if (bgMat != null)
        {
            bgMat.mainTexture = grad;
            if (bgMat.HasProperty("_BaseMap")) bgMat.SetTexture("_BaseMap", grad);
            if (bgMat.HasProperty("_BaseColor")) bgMat.SetColor("_BaseColor", Color.white);
        }
        if (cam != null) cam.backgroundColor = top;
    }

    Texture2D MakeGradientTex(Color top, Color bottom, int h)
    {
        Texture2D t = new Texture2D(1, h, TextureFormat.RGBA32, false);
        for (int y = 0; y < h; y++)
        {
            float f = y / (float)(h - 1);
            t.SetPixel(0, y, Color.Lerp(bottom, top, f));
        }
        t.wrapMode = TextureWrapMode.Clamp;
        t.Apply();
        return t;
    }

    Texture2D MakeTriTex(int size)
    {
        Texture2D t = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color32[] px = new Color32[size * size];
        float cy = (size - 1) / 2f;
        for (int yy = 0; yy < size; yy++)
            for (int xx = 0; xx < size; xx++)
            {
                float halfH = (1f - xx / (float)(size - 1)) * cy;
                bool inside = Mathf.Abs(yy - cy) <= halfH;
                px[yy * size + xx] = inside ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
            }
        t.SetPixels32(px);
        t.Apply();
        t.filterMode = FilterMode.Bilinear;
        return t;
    }

    Texture2D MakeCrownTex(int s)
    {
        Texture2D t = new Texture2D(s, s, TextureFormat.RGBA32, false);
        Color32[] px = new Color32[s * s];
        float baseTop = 0.42f;
        for (int yy = 0; yy < s; yy++)
            for (int xx = 0; xx < s; xx++)
            {
                float fx = xx / (float)(s - 1);
                float fy = yy / (float)(s - 1);
                bool on;
                if (fx <= 0.08f || fx >= 0.92f) on = false;
                else if (fy <= baseTop) on = true;
                else
                {
                    float p1 = Mathf.Clamp01(1f - Mathf.Abs(fx - 0.22f) / 0.20f);
                    float p2 = Mathf.Clamp01(1f - Mathf.Abs(fx - 0.50f) / 0.20f);
                    float p3 = Mathf.Clamp01(1f - Mathf.Abs(fx - 0.78f) / 0.20f);
                    float peak = Mathf.Max(p1, Mathf.Max(p2, p3));
                    float top = baseTop + (1f - baseTop) * peak;
                    on = fy <= top;
                }
                px[yy * s + xx] = on ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
            }
        t.SetPixels32(px);
        t.Apply();
        t.filterMode = FilterMode.Bilinear;
        return t;
    }

    Material MakeMat(Color c)
    {
        Shader sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh == null) sh = Shader.Find("Standard");
        Material m = new Material(sh);
        m.color = c;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.35f);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.35f);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0.35f);
        m.EnableKeyword("_EMISSION");
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", c * emissionStrength);
        return m;
    }

    Material MakeUnlitTransparent(Color c)
    {
        Shader sh = Shader.Find("Universal Render Pipeline/Unlit");
        if (sh == null) sh = Shader.Find("Unlit/Transparent");
        Material m = new Material(sh);
        m.color = c;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
        if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 0f);
        if (m.HasProperty("_SrcBlend")) m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (m.HasProperty("_DstBlend")) m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (m.HasProperty("_ZWrite")) m.SetInt("_ZWrite", 0);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        return m;
    }

    float AngleOf(int col) { return 2f * Mathf.PI * col / columns; }

    Vector3 CellLocalPos(int col, int row)
    {
        float a = AngleOf(col);
        return new Vector3(radius * Mathf.Sin(a), row * vSpace, radius * Mathf.Cos(a));
    }

    Quaternion CellLocalRot(int col)
    {
        float a = AngleOf(col);
        Vector3 outward = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
        return Quaternion.LookRotation(outward, Vector3.up);
    }

    Color BlockColor(int type)
    {
        if (rainbowColors) return Color.HSVToRGB((type * 0.11f) % 1f, 0.85f, 1f);
        return palette[type % palette.Length];
    }

    Color StoneColor() { return new Color(0.55f, 0.56f, 0.62f); }

    GameObject MakeBlock(int type, bool stone)
    {
        GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(g.GetComponent<Collider>());
        g.transform.SetParent(spin);
        g.transform.localScale = blockScale;
        var mf = g.GetComponent<MeshFilter>();
        if (mf != null) mf.sharedMesh = RoundedBlockMesh();   // sudut & tepi membulat ala BlockBlast
        g.GetComponent<Renderer>().material = MakeMat(stone ? StoneColor() : BlockColor(type));
        return g;
    }

    void PlaceObj(GameObject g, int col, int row)
    {
        g.transform.localPosition = CellLocalPos(col, row);
        g.transform.localRotation = CellLocalRot(col);
    }

    int Wrap(int c) { c %= columns; if (c < 0) c += columns; return c; }
}
