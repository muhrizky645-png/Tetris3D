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

public class Tetris3D : MonoBehaviour
{
    [Header("Bentuk tabung")]
    public int startColumns = 12;   // kolom awal (kecil = gampang bikin cincin)
    public int maxColumns = 24;     // batas kolom saat diameter membesar
    public int columnsPerStage = 3; // tambahan kolom tiap babak baru
    public int height = 22;
    public float radius = 3.4f;     // radius awal (ikut membesar tiap babak)
    public float vSpace = 1.35f;

    [Header("Kecepatan jatuh (tetap sepanjang game)")]
    public float fallInterval = 0.8f;

    [Header("Skor & level")]
    public int cellPoints = 10;      // poin per kotak (skor cincin = jumlah kolom x ini x combo)
    public int baseLevelScore = 300; // skor buat naik ke level 2
    public int levelStep = 200;      // tiap level, syarat naik nambah segini (berjenjang)
    public float comboSeconds = 10f; // jendela combo: clear lagi dalam sekian detik -> pengali naik

    [Header("Tantangan (tangga kesulitan)")]
    public int levelsPerStage = 3;      // tiap sekian level -> babak baru (diameter membesar)
    public int ceilingDropPerLevel = 1; // plafon turun sekian baris tiap naik level
    public int minPlayHeight = 8;       // plafon paling rendah
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
    public float bloomIntensity = 0.7f;
    public float bloomThreshold = 0.9f;
    public float bloomScatter = 0.6f;
    public float emissionStrength = 0.5f;
    public float vignetteAmount = 0.28f;

    [Header("Suara")]
    public bool soundOn = true;
    public bool sfxOn = true;
    public bool musicOn = true;
    public float sfxVolume = 0.5f;
    public float musicVolume = 0.22f;

    [Header("Gameplay")]
    public bool ghostPiece = true;

    int columns;
    float baseRadius;
    int[,] grid;
    GameObject[,] cells;
    int killLine;
    int stage;
    int nextLevelScore;
    bool stoneEnabled;

    readonly int[] boxSize = { 4, 2, 3, 3, 3, 3, 3, 3, 2, 3, 3, 3, 2 };
    readonly int[][] shapes = new int[][]
    {
        new int[]{0,2, 1,2, 2,2, 3,2},
        new int[]{0,0, 1,0, 0,1, 1,1},
        new int[]{1,2, 0,1, 1,1, 2,1},
        new int[]{1,2, 2,2, 0,1, 1,1},
        new int[]{0,2, 1,2, 1,1, 2,1},
        new int[]{0,2, 0,1, 1,1, 2,1},
        new int[]{2,2, 0,1, 1,1, 2,1},
        new int[]{0,1, 1,1, 2,1},
        new int[]{0,1, 0,0, 1,0},
        new int[]{1,2, 0,1, 1,1, 2,1, 1,0},
        new int[]{0,2, 1,2, 2,2, 1,1, 1,0},
        new int[]{0,2, 1,2, 0,1, 1,1, 0,0},
        new int[]{0,0, 1,0},
    };

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
    static readonly string[] langNames = { "English", "Indonesia", "Español", "Português", "Français" };
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
        uiFont = Resources.Load<Font>("Thaleah_PixelFont/ThaleahFat");
        baseRadius = radius;
        columns = Mathf.Max(3, startColumns);
        AllocGrid();
        killLine = height;
        nextLevelScore = baseLevelScore;
        SetupScene();
        nextType = Random.Range(0, shapes.Length);
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
        lt.intensity = 1.15f;
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
        crownTex = MakeCrownTex(64);

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
        if (cam != null)
        {
            cam.transform.position = new Vector3(0f, camY, -dist);
            cam.transform.LookAt(new Vector3(0f, centerY, 0f));
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

    // Warna latar berganti tiap babak (stage) — dibikin cerah & menyenangkan
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
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.9f);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.9f);
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
        g.GetComponent<Renderer>().material = MakeMat(stone ? StoneColor() : BlockColor(type));
        return g;
    }

    void PlaceObj(GameObject g, int col, int row)
    {
        g.transform.localPosition = CellLocalPos(col, row);
        g.transform.localRotation = CellLocalRot(col);
    }

    int Wrap(int c) { c %= columns; if (c < 0) c += columns; return c; }

    // ---------- PIECE ----------
    void SpawnPiece()
    {
        curType = nextType;
        nextType = Random.Range(0, shapes.Length);
        curN = boxSize[curType];
        int[] s = shapes[curType];
        int n = s.Length / 2;
        curBox = new Vector2Int[n];
        for (int i = 0; i < n; i++)
            curBox[i] = new Vector2Int(s[i * 2], s[i * 2 + 1]);

        curStone = stoneEnabled && Random.value < stoneChance;

        if (!curStone)
        {
            int spins = Random.Range(0, 4);
            for (int k = 0; k < spins; k++)
                for (int i = 0; i < curBox.Length; i++)
                    curBox[i] = new Vector2Int(curBox[i].y, (curN - 1) - curBox[i].x);
        }

        int frontCol = Wrap(Mathf.RoundToInt((180f - spinDeg) * columns / 360f));
        curCol = frontCol;
        curRow = height - curN;

        if (!Valid(curBox, curCol, curRow))
        {
            gameOver = true;
            Sfx(sfxGameOver);
            return;
        }

        active = new GameObject[curBox.Length];
        for (int i = 0; i < active.Length; i++)
            active[i] = MakeBlock(curType, curStone);

        if (ghostPiece)
        {
            ghost = new GameObject[curBox.Length];
            Color gc = curStone ? StoneColor() : BlockColor(curType);
            for (int i = 0; i < ghost.Length; i++)
                ghost[i] = MakeGhostBlock(gc);
        }

        RedrawActive();
        UpdateTargetSpin();
    }

    bool Valid(Vector2Int[] box, int col, int row)
    {
        for (int i = 0; i < box.Length; i++)
        {
            int r = row + box[i].y;
            int c = Wrap(col + box[i].x);
            if (r < 0) return false;
            if (r < height && grid[c, r] != -1) return false;
        }
        return true;
    }

    void RedrawActive()
    {
        for (int i = 0; i < active.Length; i++)
        {
            if (active[i] == null) continue;
            int c = Wrap(curCol + curBox[i].x);
            int r = curRow + curBox[i].y;
            PlaceObj(active[i], c, r);
        }
        if (ghost != null)
        {
            int gr = GhostRow();
            bool show = gr < curRow;
            for (int i = 0; i < ghost.Length; i++)
            {
                if (ghost[i] == null) continue;
                ghost[i].SetActive(show);
                if (show)
                {
                    int c = Wrap(curCol + curBox[i].x);
                    int r = gr + curBox[i].y;
                    PlaceObj(ghost[i], c, r);
                }
            }
        }
    }

    int GhostRow()
    {
        int r = curRow;
        while (Valid(curBox, curCol, r - 1)) r--;
        return r;
    }

    GameObject MakeGhostBlock(Color c)
    {
        GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(g.GetComponent<Collider>());
        g.transform.SetParent(spin);
        g.transform.localScale = blockScale * 0.96f;
        g.GetComponent<Renderer>().material = MakeGhostMat(c);
        return g;
    }

    Material MakeGhostMat(Color c)
    {
        Shader sh = Shader.Find("Universal Render Pipeline/Unlit");
        if (sh == null) sh = Shader.Find("Unlit/Transparent");
        Material m = new Material(sh);
        Color gc = new Color(c.r, c.g, c.b, 0.22f);
        m.color = gc;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", gc);
        if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
        if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 0f);
        if (m.HasProperty("_SrcBlend")) m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (m.HasProperty("_DstBlend")) m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (m.HasProperty("_ZWrite")) m.SetInt("_ZWrite", 0);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        return m;
    }

    void DestroyGhost()
    {
        if (ghost == null) return;
        for (int i = 0; i < ghost.Length; i++)
            if (ghost[i] != null) Destroy(ghost[i]);
        ghost = null;
    }

    bool Move(int dCol, int dRow)
    {
        if (Valid(curBox, curCol + dCol, curRow + dRow))
        {
            curCol += dCol;
            curRow += dRow;
            RedrawActive();
            if (dCol != 0) UpdateTargetSpin();
            return true;
        }
        return false;
    }

    void Rotate()
    {
        if (curStone) return; // balok batu gak bisa diputar
        Vector2Int[] nb = new Vector2Int[curBox.Length];
        for (int i = 0; i < curBox.Length; i++)
            nb[i] = new Vector2Int(curBox[i].y, (curN - 1) - curBox[i].x);
        if (Valid(nb, curCol, curRow))
        {
            curBox = nb;
            RedrawActive();
            Sfx(sfxRotate);
        }
    }

    void LockPiece()
    {
        Sfx(sfxLock);
        DestroyGhost();
        for (int i = 0; i < active.Length; i++)
        {
            int c = Wrap(curCol + curBox[i].x);
            int r = curRow + curBox[i].y;
            if (r >= 0 && r < height)
            {
                grid[c, r] = curType;
                cells[c, r] = active[i];
                active[i] = null;
            }
            else if (active[i] != null)
            {
                Destroy(active[i]);
                active[i] = null;
            }
        }
        active = null;
        StartCoroutine(ResolveBoard());
    }

    List<int> FindFullRows()
    {
        var res = new List<int>();
        for (int r = 0; r < height; r++)
        {
            bool full = true;
            for (int c = 0; c < columns; c++)
                if (grid[c, r] == -1) { full = false; break; }
            if (full) res.Add(r);
        }
        return res;
    }

    // Hancurin cincin -> gravitasi jatuh per kotak -> cek cincin baru (combo berantai)
    IEnumerator ResolveBoard()
    {
        clearing = true;
        while (true)
        {
            var full = FindFullRows();
            if (full.Count == 0) break;
            yield return StartCoroutine(FlashClear(full));

            // COMBO: tiap cincin hancur dalam <comboSeconds> detik dari yang sebelumnya -> streak naik
            if (comboExpire > 0f) comboCount++; else comboCount = 1;
            comboExpire = comboSeconds;
            if (comboCount >= 2) { comboShow = comboCount; comboTime = 1.3f; }

            float rowMult = full.Count <= 1 ? 1f : full.Count == 2 ? 2.5f : full.Count == 3 ? 4.5f : 7f + (full.Count - 4) * 2f;
            int pts = Mathf.RoundToInt(columns * cellPoints * rowMult * Mathf.Max(1, comboCount));
            score += pts;
            lines += full.Count;

            yield return StartCoroutine(CascadeGravity());
        }

        RecalcLevel();
        clearing = false;

        if (gameOver || TooHigh())
        {
            gameOver = true;
            Sfx(sfxGameOver);
            yield break;
        }
        SpawnPiece();
    }

    // Animasi cincin hancur (kedip + partikel), lalu kosongin barisnya
    IEnumerator FlashClear(List<int> rows)
    {
        Shake(0.30f + rows.Count * 0.05f, 0.26f + rows.Count * 0.12f);
        Sfx(sfxClear);

        var objs = new List<Transform>();
        var baseScales = new List<Vector3>();
        var mats = new List<Material>();
        foreach (int r in rows)
            for (int c = 0; c < columns; c++)
            {
                if (cells[c, r] == null) continue;
                objs.Add(cells[c, r].transform);
                baseScales.Add(cells[c, r].transform.localScale);
                var rend = cells[c, r].GetComponent<Renderer>();
                mats.Add(rend != null ? rend.material : null);
            }

        float dur = 0.4f;
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / dur);
            float scaleMul = p < 0.4f ? Mathf.Lerp(1f, 1.35f, p / 0.4f) : Mathf.Lerp(1.35f, 0.02f, (p - 0.4f) / 0.6f);
            float flash = p < 0.4f ? Mathf.Lerp(0f, 1f, p / 0.4f) : Mathf.Lerp(1f, 0f, (p - 0.4f) / 0.6f);
            for (int i = 0; i < objs.Count; i++)
            {
                if (objs[i] == null) continue;
                objs[i].localScale = baseScales[i] * scaleMul;
                if (mats[i] != null && mats[i].HasProperty("_EmissionColor"))
                {
                    Color bc = mats[i].HasProperty("_BaseColor") ? mats[i].GetColor("_BaseColor") : mats[i].color;
                    Color glow = Color.Lerp(bc, Color.white, flash);
                    mats[i].SetColor("_EmissionColor", glow * (0.6f + flash * 2.5f));
                }
            }
            yield return null;
        }

        for (int i = 0; i < objs.Count; i++)
        {
            if (objs[i] == null) continue;
            Color bc = mats[i] != null ? (mats[i].HasProperty("_BaseColor") ? mats[i].GetColor("_BaseColor") : mats[i].color) : Color.white;
            Burst(objs[i].position, bc);
        }
        foreach (var tr in objs)
            if (tr != null) Destroy(tr.gameObject);

        foreach (int r in rows)
            for (int c = 0; c < columns; c++) { grid[c, r] = -1; cells[c, r] = null; }
    }

    // Gravitasi cascade: tiap kotak jatuh sendiri ngisi ruang kosong di kolomnya
    IEnumerator CascadeGravity()
    {
        var movers = new List<Transform>();
        var fromY = new List<float>();
        var toY = new List<float>();

        for (int c = 0; c < columns; c++)
        {
            int write = 0;
            for (int r = 0; r < height; r++)
            {
                if (grid[c, r] == -1) continue;
                if (r != write)
                {
                    grid[c, write] = grid[c, r];
                    cells[c, write] = cells[c, r];
                    grid[c, r] = -1;
                    cells[c, r] = null;
                    GameObject go = cells[c, write];
                    if (go != null)
                    {
                        movers.Add(go.transform);
                        fromY.Add(go.transform.localPosition.y);
                        toY.Add(CellLocalPos(c, write).y);
                    }
                }
                write++;
            }
        }

        if (movers.Count == 0) yield break;

        float dur = 0.16f;
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / dur);
            for (int i = 0; i < movers.Count; i++)
            {
                if (movers[i] == null) continue;
                Vector3 lp = movers[i].localPosition;
                lp.y = Mathf.Lerp(fromY[i], toY[i], p);
                movers[i].localPosition = lp;
            }
            yield return null;
        }
        for (int i = 0; i < movers.Count; i++)
        {
            if (movers[i] == null) continue;
            Vector3 lp = movers[i].localPosition;
            lp.y = toY[i];
            movers[i].localPosition = lp;
        }
    }

    bool TooHigh()
    {
        for (int c = 0; c < columns; c++)
            for (int r = killLine; r < height; r++)
                if (grid[c, r] != -1) return true;
        return false;
    }

    void HardDrop()
    {
        while (Move(0, -1)) { }
        Sfx(sfxDrop);
        LockPiece();
    }

    // Naik level dari skor, syarat makin gede tiap level (berjenjang)
    void RecalcLevel()
    {
        int guard = 0;
        while (score >= nextLevelScore && guard++ < 100)
        {
            level++;
            OnLevelUp();
            nextLevelScore += baseLevelScore + (level - 1) * levelStep;
        }
    }

    // Efek tiap naik level = tangga kesulitan
    void OnLevelUp()
    {
        levelUpTime = 1.4f;
        Sfx(sfxLevelUp);
        bool stageLevel = ((level - 1) % Mathf.Max(1, levelsPerStage)) == 0;
        if (stageLevel && columns < maxColumns)
        {
            StageUp();
        }
        else
        {
            killLine = Mathf.Max(minPlayHeight, killLine - ceilingDropPerLevel);
            if (columns >= maxColumns) AddGarbageRow();
        }
        stoneEnabled = level >= stoneStartLevel;
    }

    // Babak baru: bersihin papan, diameter membesar, plafon reset, warna latar ganti
    void StageUp()
    {
        DestroyBoardObjects();
        columns = Mathf.Min(maxColumns, columns + columnsPerStage);
        AllocGrid();
        killLine = height;
        stage++;
        ApplyGeometry();
        ApplyStageColors();
        Shake(0.45f, 0.5f);
        Sfx(sfxClear);
    }

    void DestroyBoardObjects()
    {
        if (cells != null)
            for (int c = 0; c < cells.GetLength(0); c++)
                for (int r = 0; r < cells.GetLength(1); r++)
                    if (cells[c, r] != null) { Destroy(cells[c, r]); cells[c, r] = null; }
        if (active != null)
            for (int i = 0; i < active.Length; i++)
                if (active[i] != null) Destroy(active[i]);
        active = null;
        DestroyGhost();
    }

    // Baris sampah naik dari bawah (fase diameter maksimum)
    void AddGarbageRow()
    {
        for (int c = 0; c < columns; c++)
            if (cells[c, height - 1] != null) Destroy(cells[c, height - 1]);

        for (int r = height - 1; r >= 1; r--)
            for (int c = 0; c < columns; c++)
            {
                grid[c, r] = grid[c, r - 1];
                cells[c, r] = cells[c, r - 1];
                if (cells[c, r] != null) PlaceObj(cells[c, r], c, r);
            }

        var gaps = new HashSet<int>();
        int gapN = Mathf.Clamp(garbageGapCount, 1, columns - 1);
        int guard = 0;
        while (gaps.Count < gapN && guard++ < 500) gaps.Add(Random.Range(0, columns));

        for (int c = 0; c < columns; c++)
        {
            if (gaps.Contains(c)) { grid[c, 0] = -1; cells[c, 0] = null; }
            else
            {
                grid[c, 0] = 0;
                GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(g.GetComponent<Collider>());
                g.transform.SetParent(spin);
                g.transform.localScale = blockScale;
                g.GetComponent<Renderer>().material = MakeMat(new Color(0.42f, 0.44f, 0.5f));
                PlaceObj(g, c, 0);
                cells[c, 0] = g;
            }
        }
    }

    void StartGame()
    {
        if (started) return;
        started = true;
        SpawnPiece();
    }

    // Bersihin papan & reset semua (retry / ke menu) tanpa reload scene
    void ClearBoard()
    {
        StopAllCoroutines();
        DestroyBoardObjects();
        columns = Mathf.Max(3, startColumns);
        radius = baseRadius;
        AllocGrid();
        stage = 0;
        killLine = height;
        stoneEnabled = false;
        score = 0; lines = 0; level = 1;
        nextLevelScore = baseLevelScore;
        fallTimer = 0f; gameOver = false; clearing = false;
        levelUpTime = 0f; comboTime = 0f;
        comboCount = 0; comboExpire = 0f;
        gameOverHandled = false; showProfile = false; showRanks = false; editingProfile = false;
        ApplyGeometry();
        ApplyStageColors();
    }

    void RetryGame()
    {
        ClearBoard();
        paused = false;
        started = true;
        nextType = Random.Range(0, shapes.Length);
        SpawnPiece();
    }

    void GoHome()
    {
        ClearBoard();
        paused = false;
        started = false;
        nextType = Random.Range(0, shapes.Length);
    }

    // ---------- EFEK VISUAL ----------
    void Shake(float dur, float mag)
    {
        shakeDur = dur;
        shakeTime = dur;
        shakeMag = mag;
    }

    void Burst(Vector3 worldPos, Color c)
    {
        GameObject pg = new GameObject("Burst");
        pg.transform.position = worldPos;
        var ps = pg.AddComponent<ParticleSystem>();
        ps.Stop();
        var main = ps.main;
        main.duration = 0.6f;
        main.loop = false;
        main.startLifetime = 0.55f;
        main.startSpeed = 4.5f;
        main.startSize = 0.28f;
        main.startColor = c;
        main.gravityModifier = 0.6f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 60;
        var em = ps.emission;
        em.rateOverTime = 0f;
        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Sphere;
        sh.radius = 0.15f;
        var rend = ps.GetComponent<ParticleSystemRenderer>();
        if (particleMat != null) rend.material = particleMat;
        ps.Emit(16);
        Destroy(pg, 1.0f);
    }

    // ---------- AUDIO ----------
    void SetupAudio()
    {
        if (Object.FindFirstObjectByType<AudioListener>() == null && cam != null)
            cam.gameObject.AddComponent<AudioListener>();

        GameObject ag = new GameObject("Audio");
        ag.transform.SetParent(transform);
        sfx = ag.AddComponent<AudioSource>();
        sfx.playOnAwake = false;
        music = ag.AddComponent<AudioSource>();
        music.playOnAwake = false;
        music.loop = true;

        sfxRotate = MakeTone("rot", 720f, 0.07f, 0.42f, 0, 1010f);
        sfxLock = MakeTone("lock", 200f, 0.12f, 0.62f, 0, 120f);
        sfxDrop = MakeTone("drop", 300f, 0.16f, 0.62f, 1, 90f);
        sfxClear = MakeArp("clr", new float[] { 523.25f, 659.25f, 783.99f, 1046.50f }, 0.085f, 0.52f);
        sfxGameOver = MakeArp("go", new float[] { 523.25f, 440.00f, 349.23f, 261.63f }, 0.18f, 0.55f);
        sfxLevelUp = MakeArp("lvl", new float[] { 659.25f, 830.61f, 987.77f, 1318.51f }, 0.10f, 0.5f);

        musicClip = MakeMusic();
        music.clip = musicClip;
        music.volume = musicVolume;
        if (soundOn && musicOn) music.Play();
    }

    void Sfx(AudioClip c)
    {
        if (soundOn && sfxOn && sfx != null && c != null) sfx.PlayOneShot(c, sfxVolume);
    }

    AudioClip MakeTone(string name, float freq, float dur, float vol, int wave, float freqEnd)
    {
        int rate = 44100;
        int n = Mathf.Max(1, (int)(rate * dur));
        float[] data = new float[n];
        float phase = 0f, phase2 = 0f, phase3 = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)n;
            float tt = i / (float)rate;
            float f = Mathf.Lerp(freq, freqEnd <= 0f ? freq : freqEnd, t);
            f *= 1f + 0.006f * Mathf.Sin(2f * Mathf.PI * 6f * tt); // vibrato halus biar hidup
            phase  += 2f * Mathf.PI * f / rate;
            phase2 += 2f * Mathf.PI * f * 2.01f / rate;
            phase3 += 2f * Mathf.PI * f * 3.0f / rate;
            float s;
            if (wave == 1) s = Mathf.Sign(Mathf.Sin(phase)) * 0.6f + Mathf.Sin(phase2) * 0.2f;                       // square + harmonik
            else if (wave == 2) s = (Mathf.PingPong(phase / Mathf.PI, 1f) * 2f - 1f) * 0.7f + Mathf.Sin(phase3) * 0.2f; // triangle + harmonik ganjil
            else s = Mathf.Sin(phase) * 0.8f + Mathf.Sin(phase2) * 0.25f + Mathf.Sin(phase3) * 0.12f;                  // sine bertumpuk (lebih tebal)
            float atk = Mathf.Min(1f, t / 0.008f);   // serangan cepat = punchy
            float dec = Mathf.Exp(-3.2f * t);        // peluruhan eksponensial
            data[i] = Mathf.Clamp(s * atk * dec * vol, -1f, 1f);
        }
        AudioClip clip = AudioClip.Create(name, n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    // Arpeggio pendek (nada berurutan) buat clear / level up / game over yang lebih "jos"
    AudioClip MakeArp(string name, float[] freqs, float noteDur, float vol)
    {
        int rate = 44100;
        int nPer = Mathf.Max(1, (int)(rate * noteDur));
        int total = nPer * freqs.Length;
        float[] data = new float[total];
        for (int k = 0; k < freqs.Length; k++)
        {
            float f = freqs[k];
            for (int i = 0; i < nPer; i++)
            {
                float t = i / (float)nPer;
                float tt = i / (float)rate;
                float s = Mathf.Sin(2f * Mathf.PI * f * tt) * 0.7f
                        + Mathf.Sin(2f * Mathf.PI * f * 2.01f * tt) * 0.25f
                        + Mathf.Sin(2f * Mathf.PI * f * 3f * tt) * 0.12f;
                float env = Mathf.Min(1f, t / 0.01f) * Mathf.Exp(-3.5f * t);
                data[k * nPer + i] = Mathf.Clamp(s * env * vol, -1f, 1f);
            }
        }
        AudioClip clip = AudioClip.Create(name, total, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    AudioClip MakeMusic()
    {
        int rate = 44100;
        float noteDur = 0.22f;
        float[] scale = { 261.63f, 293.66f, 329.63f, 392.00f, 440.00f, 523.25f };
        int[] seq = { 0, 2, 4, 2, 1, 3, 5, 3, 0, 2, 4, 5, 4, 2, 1, 0 };
        int nPer = (int)(rate * noteDur);
        int total = nPer * seq.Length;
        float[] data = new float[total];
        for (int k = 0; k < seq.Length; k++)
        {
            float f = scale[seq[k]];
            float bass = f * 0.5f;
            for (int i = 0; i < nPer; i++)
            {
                float t = i / (float)nPer;
                float env = Mathf.Min(1f, t / 0.05f) * Mathf.Pow(1f - t, 0.6f);
                float tt = i / (float)rate;
                float s = Mathf.Sin(2f * Mathf.PI * f * tt) * 0.6f + Mathf.Sin(2f * Mathf.PI * bass * tt) * 0.4f;
                data[k * nPer + i] = s * env * 0.5f;
            }
        }
        AudioClip clip = AudioClip.Create("music", total, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    void UpdateTargetSpin()
    {
        float centerCol = curCol + (curN - 1) * 0.5f;
        targetSpin = 180f - 360f * centerCol / columns;
    }

    // ---------- LOOP ----------
    void Update()
    {
        if (levelUpTime > 0f) levelUpTime -= Time.deltaTime;
        if (comboTime > 0f) comboTime -= Time.deltaTime;

        if (bloom != null)
        {
            bloom.intensity.value = bloomIntensity;
            bloom.threshold.value = bloomThreshold;
            bloom.scatter.value = bloomScatter;
        }
        if (vig != null) vig.intensity.value = vignetteAmount;

        if (music != null)
        {
            music.volume = musicVolume;
            bool wantMusic = soundOn && musicOn;
            if (wantMusic && !music.isPlaying) music.Play();
            else if (!wantMusic && music.isPlaying) music.Pause();
        }

        if (spin != null)
        {
            spinDeg = Mathf.LerpAngle(spinDeg, targetSpin, 12f * Time.deltaTime);
            spin.localRotation = Quaternion.Euler(0f, spinDeg, 0f);
        }

        if (shakeTime > 0f && cam != null)
        {
            shakeTime -= Time.deltaTime;
            float amt = shakeMag * Mathf.Clamp01(shakeTime / Mathf.Max(0.0001f, shakeDur));
            if (shakeTime > 0f)
                cam.transform.position = camBasePos + new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), 0f) * amt;
            else
                cam.transform.position = camBasePos;
        }

        if (killRingTf != null)
        {
            bool showRing = started && !gameOver && killLine < height;
            killRingTf.gameObject.SetActive(showRing);
            if (showRing)
            {
                killRingTf.localScale = new Vector3(radius * 2.1f, 0.05f, radius * 2.1f);
                killRingTf.localPosition = new Vector3(0f, (killLine - 0.5f) * vSpace, 0f);
            }
        }

        if (!started)
        {
            spinDeg += Time.deltaTime * 16f;
            targetSpin = spinDeg;
            if (spin != null) spin.localRotation = Quaternion.Euler(0f, spinDeg, 0f);

            var kbMenu = Keyboard.current;
            if (kbMenu != null && (kbMenu.spaceKey.wasPressedThisFrame || kbMenu.enterKey.wasPressedThisFrame)) StartGame();
            return;
        }

        var kbPause = Keyboard.current;
        if (kbPause != null && (kbPause.escapeKey.wasPressedThisFrame || kbPause.pKey.wasPressedThisFrame)) paused = !paused;
        if (paused) return;

        if (clearing) return;

        if (gameOver)
        {
            if (!gameOverHandled)
            {
                gameOverHandled = true;
                if (!profileDone) showProfile = true;
                else Submit