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

    // Warna latar berganti tiap babak (stage)
    void ApplyStageColors()
    {
        Color top, bottom;
        if (stage == 0) { top = bgTop; bottom = bgBottom; }
        else
        {
            float hue = (0.62f + stage * 0.13f) % 1f;
            top = Color.HSVToRGB(hue, 0.75f, 0.16f);
            bottom = Color.HSVToRGB((hue + 0.08f) % 1f, 0.80f, 0.42f);
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
                else SubmitScore();
            }
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame) RetryGame();
            return;
        }

        // Jendela combo jalan pas main normal (beku saat pause/clear); lewat comboSeconds tanpa clear -> putus
        if (comboExpire > 0f)
        {
            comboExpire -= Time.deltaTime;
            if (comboExpire <= 0f) { comboExpire = 0f; comboCount = 0; }
        }

        var pointer = Pointer.current;
        if (pointer != null)
        {
            if (pointer.press.wasPressedThisFrame) { dragging = true; lastMouseX = pointer.position.ReadValue().x; dragAccum = 0f; hintDone = true; }
            if (pointer.press.wasReleasedThisFrame) dragging = false;
            if (dragging && pointer.press.isPressed)
            {
                float mx = pointer.position.ReadValue().x;
                dragAccum += mx - lastMouseX;
                lastMouseX = mx;
                float stepPx = Screen.width * dragStep;
                int dir = dragReversed ? -1 : 1;
                while (dragAccum >= stepPx) { Move(dir, 0); dragAccum -= stepPx; }
                while (dragAccum <= -stepPx) { Move(-dir, 0); dragAccum += stepPx; }
            }
        }

        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.leftArrowKey.wasPressedThisFrame) { Move(-1, 0); hintDone = true; }
            if (kb.rightArrowKey.wasPressedThisFrame) { Move(1, 0); hintDone = true; }
            if (kb.upArrowKey.wasPressedThisFrame) Rotate();
            if (kb.spaceKey.wasPressedThisFrame) { HardDrop(); return; }
        }

        bool softDrop = (Keyboard.current != null && Keyboard.current.downArrowKey.isPressed) || btnSoftDrop;
        float interval = softDrop ? 0.05f : fallInterval;
        fallTimer += Time.deltaTime;
        if (fallTimer >= interval)
        {
            fallTimer = 0f;
            if (!Move(0, -1)) LockPiece();
        }

        btnSoftDrop = false;
    }

    // ---------- Leaderboard (UGS) ----------
    async void InitUGS()
    {
        try
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
                await UnityServices.InitializeAsync();
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            ugsReady = true;
            if (pendingSubmit) { pendingSubmit = false; SubmitScore(); }
        }
        catch (Exception e) { lbStatus = "UGS: " + e.Message; }
    }

    void LoadProfile()
    {
        profileDone = PlayerPrefs.GetInt("tetris3d_profile", 0) == 1;
        playerName = PlayerPrefs.GetString("tetris3d_name", "");
        playerCountry = PlayerPrefs.GetString("tetris3d_country", DetectCountry());
    }

    void SaveProfile()
    {
        PlayerPrefs.SetInt("tetris3d_profile", 1);
        PlayerPrefs.SetString("tetris3d_name", playerName);
        PlayerPrefs.SetString("tetris3d_country", playerCountry);
        PlayerPrefs.Save();
        profileDone = true;
    }

    // Kirim/ubah nama pemain di UGS (dipakai pas edit profil dari menu), lalu refresh papan
    async void PushName()
    {
        if (!ugsReady || string.IsNullOrEmpty(playerName)) return;
        try { await AuthenticationService.Instance.UpdatePlayerNameAsync(playerName); } catch { }
        LoadRanks();
    }

    string DetectCountry()
    {
        try
        {
            string c = RegionInfo.CurrentRegion.TwoLetterISORegionName.ToUpper();
            for (int i = 0; i < countryCodes.Length; i++)
                if (countryCodes[i] == c) return c;
        }
        catch { }
        return "ID";
    }

    string CountryName(string code)
    {
        for (int i = 0; i < countryCodes.Length; i++)
            if (countryCodes[i] == code) return countryNames[i];
        return code;
    }

    async void SubmitScore()
    {
        if (!ugsReady) { pendingSubmit = true; return; }
        if (submitting) return;
        try
        {
            submitting = true;
            lbStatus = T("sending");
            if (!string.IsNullOrEmpty(playerName))
            {
                try { await AuthenticationService.Instance.UpdatePlayerNameAsync(playerName); } catch { }
            }
            var opt = new AddPlayerScoreOptions { Metadata = new Dictionary<string, object> { { "country", playerCountry } } };
            await LeaderboardsService.Instance.AddPlayerScoreAsync(LB_ID, score, opt);
            lbStatus = "";
            submitting = false;
            homeRanksRequested = false;
        }
        catch (Exception e) { lbStatus = "Err: " + e.Message; submitting = false; }
    }

    async void LoadRanks()
    {
        if (!ugsReady) { lbStatus = T("connecting"); return; }
        try
        {
            ranksLoading = true;
            lbStatus = "";
            var page = await LeaderboardsService.Instance.GetScoresAsync(LB_ID, new GetScoresOptions { Offset = 0, Limit = 50, IncludeMetadata = true });
            ranks.Clear();
            string myId = AuthenticationService.Instance.IsSignedIn ? AuthenticationService.Instance.PlayerId : "";
            foreach (var e in page.Results)
                ranks.Add(new LbEntry { rank = (int)e.Rank + 1, name = e.PlayerName, country = ParseCountry(e.Metadata), score = (long)e.Score, you = e.PlayerId == myId });
            try
            {
                var mine = await LeaderboardsService.Instance.GetPlayerScoreAsync(LB_ID, new GetPlayerScoreOptions { IncludeMetadata = true });
                myRank = (int)mine.Rank + 1;
            }
            catch { myRank = -1; }
            ranksLoading = false;
        }
        catch (Exception e) { lbStatus = "Err: " + e.Message; ranksLoading = false; }
    }

    string ParseCountry(string meta)
    {
        if (string.IsNullOrEmpty(meta)) return "";
        int k = meta.IndexOf("\"country\"");
        if (k < 0) return "";
        int q1 = meta.IndexOf('"', k + 9);
        if (q1 < 0) return "";
        int q2 = meta.IndexOf('"', q1 + 1);
        if (q2 < 0) return "";
        return meta.Substring(q1 + 1, q2 - q1 - 1);
    }

    // ---------- Bahasa ----------
    Lang DetectLang()
    {
        switch (Application.systemLanguage)
        {
            case SystemLanguage.Indonesian: return Lang.ID;
            case SystemLanguage.Spanish: return Lang.ES;
            case SystemLanguage.Portuguese: return Lang.PT;
            case SystemLanguage.French: return Lang.FR;
            default: return Lang.EN;
        }
    }

    string T(string key)
    {
        if (loc != null && loc.TryGetValue(key, out var a))
        {
            int i = (int)lang;
            return (i >= 0 && i < a.Length) ? a[i] : a[0];
        }
        return key;
    }

    void InitLoc()
    {
        loc = new Dictionary<string, string[]>
        {
            { "subtitle",   new[]{ "3D CYLINDER", "3D SILINDER", "CILINDRO 3D", "CILINDRO 3D", "CYLINDRE 3D" } },
            { "record",     new[]{ "BEST", "REKOR", "RÉCORD", "RECORDE", "RECORD" } },
            { "play",       new[]{ "PLAY", "MAIN", "JUGAR", "JOGAR", "JOUER" } },
            { "pressPlay",  new[]{ "Press PLAY to start", "Tekan MAIN untuk mulai", "Pulsa JUGAR para empezar", "Toque JOGAR para começar", "Appuyez sur JOUER" } },
            { "swipeHint",  new[]{ "Swipe = rotate tube", "Geser layar = putar tabung", "Desliza = girar el tubo", "Arraste = girar o tubo", "Glissez = tourner le tube" } },
            { "ctrlHint",   new[]{ "ROTATE  •  DOWN  •  DROP", "ROTASI  •  TURUN  •  JATUH", "GIRAR  •  BAJAR  •  CAER", "GIRAR  •  DESCER  •  SOLTAR", "TOURNER  •  BAS  •  LÂCHER" } },
            { "pause",      new[]{ "PAUSE", "JEDA", "PAUSA", "PAUSA", "PAUSE" } },
            { "resume",     new[]{ "RESUME", "LANJUT", "CONTINUAR", "CONTINUAR", "REPRENDRE" } },
            { "restart",    new[]{ "RESTART", "ULANG", "REINICIAR", "REINICIAR", "RECOMMENCER" } },
            { "mainmenu",   new[]{ "MAIN MENU", "KE MENU", "MENÚ", "MENU", "MENU" } },
            { "sfx",        new[]{ "Sound FX", "Efek Suara (SFX)", "Efectos (SFX)", "Efeitos (SFX)", "Effets (SFX)" } },
            { "music",      new[]{ "Music", "Musik", "Música", "Música", "Musique" } },
            { "score",      new[]{ "SCORE", "SKOR", "PUNTOS", "PONTOS", "SCORE" } },
            { "lines",      new[]{ "Lines", "Baris", "Líneas", "Linhas", "Lignes" } },
            { "lvl",        new[]{ "Lv", "Lv", "Nv", "Nv", "Niv" } },
            { "cols",       new[]{ "Cols", "Kolom", "Cols", "Cols", "Cols" } },
            { "level",      new[]{ "LEVEL", "LEVEL", "NIVEL", "NÍVEL", "NIVEAU" } },
            { "playAgain",  new[]{ "PLAY AGAIN (R)", "MAIN LAGI (R)", "JUGAR OTRA VEZ (R)", "JOGAR DE NOVO (R)", "REJOUER (R)" } },
            { "rotate",     new[]{ "ROTATE", "ROTASI", "GIRAR", "GIRAR", "TOURNER" } },
            { "drop",       new[]{ "DROP", "JATUH", "CAER", "SOLTAR", "LÂCHER" } },
            { "down",       new[]{ "DOWN", "TURUN", "BAJAR", "DESCER", "BAS" } },
            { "next",       new[]{ "NEXT", "BERIKUTNYA", "SIGUIENTE", "PRÓXIMO", "SUIVANT" } },
            { "swipeBig",   new[]{ "SWIPE TO ROTATE THE TUBE", "GESER UNTUK MEMUTAR TABUNG", "DESLIZA PARA GIRAR EL TUBO", "ARRASTE PARA GIRAR O TUBO", "GLISSEZ POUR TOURNER LE TUBE" } },
            { "touchStart", new[]{ "touch screen to start", "sentuh layar buat mulai", "toca para empezar", "toque para começar", "touchez pour commencer" } },
            { "rankings",   new[]{ "RANKINGS", "PERINGKAT", "RANKING", "RANKING", "CLASSEMENT" } },
            { "viewAll",    new[]{ "Tap for all", "Ketuk lihat semua", "Ver todo", "Ver tudo", "Tout voir" } },
            { "setProfile", new[]{ "Set profile", "Buat profil", "Crear perfil", "Criar perfil", "Créer profil" } },
            { "saveProfile", new[]{ "SAVE", "SIMPAN", "GUARDAR", "SALVAR", "ENREGISTRER" } },
            { "profileTitle", new[]{ "YOUR PROFILE", "PROFIL KAMU", "TU PERFIL", "SEU PERFIL", "TON PROFIL" } },
            { "nameLabel",  new[]{ "Name", "Nama", "Nombre", "Nome", "Nom" } },
            { "countryLabel", new[]{ "Country", "Negara", "País", "País", "Pays" } },
            { "submit",     new[]{ "SAVE & SUBMIT", "SIMPAN & KIRIM", "GUARDAR Y ENVIAR", "SALVAR E ENVIAR", "ENREGISTRER" } },
            { "you",        new[]{ "YOU", "KAMU", "TÚ", "VOCÊ", "TOI" } },
            { "loading",    new[]{ "Loading...", "Memuat...", "Cargando...", "Carregando...", "Chargement..." } },
            { "close",      new[]{ "CLOSE", "TUTUP", "CERRAR", "FECHAR", "FERMER" } },
            { "yourRank",   new[]{ "Your rank", "Peringkatmu", "Tu rango", "Sua posição", "Ton rang" } },
            { "unranked",   new[]{ "Not ranked yet", "Belum masuk peringkat", "Sin clasificar", "Sem classificação", "Non classé" } },
            { "enterName",  new[]{ "Enter your name first", "Isi nama dulu", "Escribe tu nombre", "Digite seu nome", "Entre ton nom" } },
            { "connecting", new[]{ "Connecting...", "Menyambung...", "Conectando...", "Conectando...", "Connexion..." } },
            { "noScores",   new[]{ "No scores yet", "Belum ada skor", "Sin puntajes aún", "Sem pontuações ainda", "Aucun score" } },
            { "sending",    new[]{ "Sending...", "Mengirim...", "Enviando...", "Enviando...", "Envoi..." } },
        };
    }

    // Tombol pemilih bahasa (nampil kode aktif; diklik -> daftar 5 bahasa)
    void DrawLangPicker(float x, float y)
    {
        float w = 96f, h = 46f;
        if (Btn3D(new Rect(x, y, w, h), langCodes[(int)lang], new Color(0.32f, 0.38f, 0.60f), false))
            langOpen = !langOpen;
        if (langOpen)
        {
            float lw = 150f, lh = 42f;
            for (int i = 0; i < langNames.Length; i++)
            {
                Rect rr = new Rect(x + w - lw, y + h + 6f + i * (lh + 5f), lw, lh);
                Color fc = i == (int)lang ? new Color(0.20f, 0.82f, 0.46f) : new Color(0.26f, 0.30f, 0.46f);
                if (Btn3D(rr, langNames[i], fc, false))
                {
                    lang = (Lang)i;
                    PlayerPrefs.SetInt("tetris3d_lang", i);
                    PlayerPrefs.Save();
                    langOpen = false;
                }
            }
        }
    }

    // ---------- UI (skor + tombol Android) ----------
    void FillRect(Rect r, Color col)
    {
        Color old = GUI.color;
        GUI.color = col;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = old;
    }

    // Kotak sudut melengkung (rounded) — pakai border-radius bawaan GUI.DrawTexture
    void RoundRect(Rect r, Color col, float radius)
    {
        radius = Mathf.Min(radius, Mathf.Min(r.width, r.height) * 0.5f);
        GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f,
            col, Vector4.zero, new Vector4(radius, radius, radius, radius));
    }

    void GuiText(Rect r, string t, int size, Color col, TextAnchor anchor)
    {
        GUIStyle st = new GUIStyle { fontSize = size, fontStyle = FontStyle.Bold, alignment = anchor };
        st.normal.textColor = new Color(0f, 0f, 0f, 0.65f); // bayangan
        GUI.Label(new Rect(r.x + 2f, r.y + 2f, r.width, r.height), t, st);
        st.normal.textColor = col;                          // teks utama
        GUI.Label(r, t, st);
    }

    // Tombol melengkung — ada efek "mendem" pas ditekan
    bool Btn3D(Rect r, string label, Color face, bool repeat)
    {
        bool held = pointerDown && r.Contains(Event.current.mousePosition);
        float depth = Mathf.Max(7f, r.height * 0.16f);
        float rad = r.height * 0.30f;
        float press = held ? depth * 0.7f : 0f; // muka turun pas ditekan
        Color dark = new Color(face.r * 0.42f, face.g * 0.42f, face.b * 0.42f, 1f);
        Color faceC = held ? new Color(face.r * 0.85f, face.g * 0.85f, face.b * 0.85f, 1f) : face;
        Color light = Color.Lerp(faceC, Color.white, 0.5f);

        // bayangan lembut di bawah
        RoundRect(new Rect(r.x + 2f, r.y + depth + 5f, r.width, r.height - depth), new Color(0f, 0f, 0f, 0.30f), rad);
        // sisi bawah (kesan tebal 3D)
        RoundRect(new Rect(r.x, r.y + depth, r.width, r.height - depth), dark, rad);
        // muka tombol (naik-turun sesuai tekanan)
        Rect fr = new Rect(r.x, r.y + press, r.width, r.height - depth);
        RoundRect(fr, faceC, rad);
        // highlight kilau di atas (redup saat ditekan)
        RoundRect(new Rect(fr.x + rad * 0.5f, fr.y + 3f, fr.width - rad, fr.height * 0.34f),
            new Color(light.r, light.g, light.b, held ? 0.22f : 0.55f), rad * 0.7f);
        // label
        GuiText(new Rect(fr.x, fr.y + fr.height * 0.5f - 15f, fr.width, 30f), label, 22, Color.white, TextAnchor.MiddleCenter);
        return repeat ? GUI.RepeatButton(r, GUIContent.none, GUIStyle.none)
                      : GUI.Button(r, GUIContent.none, GUIStyle.none);
    }

    // Sel mini melengkung buat preview
    void Cell3D(Rect r, Color face)
    {
        float rad = Mathf.Min(r.width, r.height) * 0.26f;
        Color dark = new Color(face.r * 0.5f, face.g * 0.5f, face.b * 0.5f, 1f);
        Color light = Color.Lerp(face, Color.white, 0.5f);
        RoundRect(new Rect(r.x, r.y + r.height * 0.12f, r.width, r.height * 0.88f), dark, rad); // dasar
        RoundRect(new Rect(r.x, r.y, r.width, r.height * 0.88f), face, rad);                    // muka
        RoundRect(new Rect(r.x + rad * 0.5f, r.y + 2f, r.width - rad, r.height * 0.30f),
            new Color(light.r, light.g, light.b, 0.7f), rad * 0.6f);                            // kilau
    }

    // ---- Menu depan (start screen) ----
    void DrawStartMenu()
    {
        float cx = Screen.width / 2f;
        float t = Time.time;

        // Latar gelap
        FillRect(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0.02f, 0.01f, 0.06f, 0.66f));

        // Blok-blok hias melayang
        DrawMenuDeco(t);

        // Judul glow neon + gerak naik-turun halus
        float pulse = 0.75f + 0.25f * Mathf.Sin(t * 2.2f);
        float bob = Mathf.Sin(t * 1.6f) * 6f;
        GlowText(new Rect(0f, Screen.height * 0.16f + bob, Screen.width, 100f), "TETRIS", 88, new Color(0.30f, 0.95f, 1f), pulse);
        GlowText(new Rect(0f, Screen.height * 0.16f + 96f + bob, Screen.width, 76f), T("subtitle"), 52, new Color(1f, 0.82f, 0.28f), pulse);

        // Garis pemisah bercahaya
        float lw = Mathf.Min(Screen.width * 0.5f, 300f);
        RoundRect(new Rect(cx - lw / 2f, Screen.height * 0.16f + 180f + bob, lw, 4f), new Color(0.4f, 0.9f, 1f, 0.55f), 2f);

        // Kartu skor tertinggi + mahkota
        float hw = 300f;
        Rect hiCard = new Rect(cx - hw / 2f, Screen.height * 0.40f, hw, 60f);
        RoundRect(new Rect(hiCard.x - 3f, hiCard.y - 3f, hiCard.width + 6f, hiCard.height + 6f), new Color(1f, 0.8f, 0.2f, 0.22f), 20f); // halo
        RoundRect(hiCard, new Color(0.10f, 0.08f, 0.03f, 0.85f), 18f);
        GuiText(new Rect(hiCard.x + 22f, hiCard.y, 120f, 60f), T("record"), 18, new Color(1f, 0.9f, 0.6f, 0.85f), TextAnchor.MiddleLeft);
        if (crownTex != null)
            GUI.DrawTexture(new Rect(hiCard.x + hw - 152f, hiCard.y + 14f, 36f, 32f), crownTex, ScaleMode.StretchToFill, true, 0f,
                new Color(1f, 0.85f, 0.28f), Vector4.zero, Vector4.zero);
        GuiText(new Rect(hiCard.x + hw - 110f, hiCard.y, 98f, 60f), "" + highScore, 34, new Color(1f, 0.9f, 0.45f), TextAnchor.MiddleLeft);

        // Tombol MAIN berdenyut + halo cahaya
        float bw = Mathf.Min(Screen.width * 0.62f, 340f);
        float grow = 6f * (0.5f + 0.5f * Mathf.Sin(t * 3f));
        Rect btn = new Rect(cx - bw / 2f - grow, Screen.height * 0.53f - grow, bw + grow * 2f, 104f + grow * 2f);
        RoundRect(new Rect(btn.x - 6f, btn.y - 6f, btn.width + 12f, btn.height + 12f), new Color(0.2f, 1f, 0.55f, 0.22f), 30f); // halo
        if (Btn3D(btn, T("play"), new Color(0.20f, 0.82f, 0.46f), false))
            StartGame();

        // ---- Panel leaderboard global (Top 5) — di-tap buka full Top 50 ----
        if (ugsReady && !homeRanksRequested) { homeRanksRequested = true; LoadRanks(); }
        float lbw = Mathf.Min(Screen.width * 0.86f, 460f);
        float lbx = cx - lbw / 2f;
        float lby = Screen.height * 0.61f;
        float hdrH = 44f;
        int showN = Mathf.Min(5, ranks.Count);
        float lbRowH = 50f;
        float bodyH = (!ugsReady || ranksLoading || ranks.Count == 0) ? 66f : showN * (lbRowH + 4f);
        float panelH = hdrH + 8f + bodyH + 10f;

        RoundRect(new Rect(lbx - 3f, lby - 3f, lbw + 6f, panelH + 6f), new Color(1f, 0.82f, 0.25f, 0.18f), 20f);
        RoundRect(new Rect(lbx, lby, lbw, panelH), new Color(0.06f, 0.07f, 0.12f, 0.92f), 18f);
        RoundRect(new Rect(lbx + 10f, lby + 8f, lbw - 20f, hdrH - 6f), new Color(0.95f, 0.75f, 0.15f, 0.16f), 12f);
        if (crownTex != null)
            GUI.DrawTexture(new Rect(lbx + 20f, lby + 12f, 30f, 26f), crownTex, ScaleMode.StretchToFill, true, 0f, new Color(1f, 0.85f, 0.28f), Vector4.zero, Vector4.zero);
        GuiText(new Rect(lbx + 58f, lby + 8f, lbw - 140f, hdrH - 6f), T("rankings"), 20, new Color(1f, 0.9f, 0.55f), TextAnchor.MiddleLeft);
        GuiText(new Rect(lbx + lbw - 104f, lby + 8f, 92f, hdrH - 6f), T("viewAll"), 13, new Color(0.6f, 0.85f, 1f), TextAnchor.MiddleRight);

        float lY = lby + hdrH + 6f;
        if (!ugsReady || ranksLoading) GuiText(new Rect(lbx, lY, lbw, 56f), T("connecting"), 20, new Color(1f, 1f, 1f, 0.8f), TextAnchor.MiddleCenter);
        else if (ranks.Count == 0) GuiText(new Rect(lbx, lY, lbw, 56f), T("noScores"), 20, new Color(1f, 1f, 1f, 0.7f), TextAnchor.MiddleCenter);
        else
        {
            for (int i = 0; i < showN; i++)
            {
                var e = ranks[i];
                Rect rr = new Rect(lbx + 10f, lY + i * (lbRowH + 4f), lbw - 20f, lbRowH);
                Color rowCol = e.you ? new Color(0.20f, 0.55f, 0.42f, 0.95f) : (i < 3 ? new Color(0.18f, 0.16f, 0.28f, 0.95f) : new Color(0.10f, 0.12f, 0.18f, 0.9f));
                RoundRect(rr, rowCol, 10f);
                Color rankCol = i == 0 ? new Color(1f, 0.85f, 0.3f) : i == 1 ? new Color(0.82f, 0.86f, 0.92f) : i == 2 ? new Color(0.88f, 0.58f, 0.32f) : new Color(0.7f, 0.75f, 0.85f);
                GuiText(new Rect(rr.x + 12f, rr.y, 52f, lbRowH), "#" + e.rank, 22, rankCol, TextAnchor.MiddleLeft);
                string nm = string.IsNullOrEmpty(e.name) ? "-" : e.name;
                if (e.you) nm += "  (" + T("you") + ")";
                GuiText(new Rect(rr.x + 64f, rr.y + 5f, rr.width - 180f, 24f), nm, 18, Color.white, TextAnchor.LowerLeft);
                if (!string.IsNullOrEmpty(e.country))
                    GuiText(new Rect(rr.x + 64f, rr.y + 27f, rr.width - 180f, 18f), CountryName(e.country), 12, new Color(0.7f, 0.8f, 1f), TextAnchor.UpperLeft);
                GuiText(new Rect(rr.xMax - 128f, rr.y, 116f, lbRowH), "" + e.score, 22, new Color(0.6f, 1f, 0.75f), TextAnchor.MiddleRight);
            }
        }
        if (GUI.Button(new Rect(lbx, lby, lbw, panelH), GUIContent.none, GUIStyle.none)) { showRanks = true; LoadRanks(); }

        // Hint berkedip (di bawah panel leaderboard)
        float ha = 0.55f + 0.45f * Mathf.Sin(t * 3f);
        GuiText(new Rect(0f, lby + panelH + 14f, Screen.width, 30f), T("pressPlay"), 20, new Color(1f, 1f, 1f, ha), TextAnchor.MiddleCenter);

        // Chip profil (pojok kiri atas) — tap buat edit nama & negara kapan aja
        string pf = string.IsNullOrEmpty(playerName) ? T("setProfile") : (playerName + "  ·  " + playerCountry);
        float pcw = Mathf.Min(Screen.width * 0.52f, 240f);
        if (Btn3D(new Rect(16f, 16f, pcw, 46f), pf, new Color(0.30f, 0.40f, 0.62f), false))
        { editingProfile = true; showProfile = true; countryPicking = false; lbStatus = ""; }

        // Pemilih bahasa (pojok kanan atas)
        DrawLangPicker(Screen.width - 96f - 16f, 16f);
    }

    // Teks dengan efek glow (digambar berlapis karena GUI gak kena Bloom)
    void GlowText(Rect r, string s, int size, Color col, float glow)
    {
        Color g = new Color(col.r, col.g, col.b, 0.16f * glow);
        for (int i = 0; i < 4; i++)
        {
            float o = 2f + i * 1.6f;
            GuiText(new Rect(r.x - o, r.y, r.width, r.height), s, size, g, TextAnchor.MiddleCenter);
            GuiText(new Rect(r.x + o, r.y, r.width, r.height), s, size, g, TextAnchor.MiddleCenter);
            GuiText(new Rect(r.x, r.y - o, r.width, r.height), s, size, g, TextAnchor.MiddleCenter);
            GuiText(new Rect(r.x, r.y + o, r.width, r.height), s, size, g, TextAnchor.MiddleCenter);
        }
        GuiText(r, s, size, col, TextAnchor.MiddleCenter);
    }

    // Blok warna-warni melayang naik di latar menu
    void DrawMenuDeco(float t)
    {
        int n = 7;
        float sz = Mathf.Max(26f, Screen.width * 0.045f);
        for (int i = 0; i < n; i++)
        {
            float fx = (i + 0.5f) / n;
            float px = fx * Screen.width + Mathf.Sin(t * 0.6f + i * 1.3f) * 22f;
            float speed = 38f + (i % 3) * 20f;
            float py = Screen.height - Mathf.Repeat(t * speed + i * 150f, Screen.height + 140f);
            Color c = BlockColor(i % 6);
            c.a = 0.35f;
            RoundRect(new Rect(px - sz / 2f, py, sz, sz), c, sz * 0.28f);
            RoundRect(new Rect(px - sz / 2f + sz * 0.15f, py + sz * 0.12f, sz * 0.7f, sz * 0.28f), new Color(1f, 1f, 1f, 0.15f), sz * 0.14f);
        }
    }

    // ---- Menu jeda (pause) ----
    void DrawPauseMenu()
    {
        float cx = Screen.width / 2f;
        FillRect(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0.02f, 0.01f, 0.06f, 0.72f));
        GlowText(new Rect(0f, Screen.height * 0.16f, Screen.width, 90f), T("pause"), 72, new Color(0.5f, 0.85f, 1f), 1f);

        float bw = Mathf.Min(Screen.width * 0.64f, 360f);
        float bx = cx - bw / 2f;
        float by = Screen.height * 0.30f;
        float bh = 74f, gap = 15f;

        if (Btn3D(new Rect(bx, by, bw, bh), T("resume"), new Color(0.20f, 0.82f, 0.46f), false)) paused = false;
        by += bh + gap;
        if (Btn3D(new Rect(bx, by, bw, bh), T("restart"), new Color(0.95f, 0.70f, 0.20f), false)) RetryGame();
        by += bh + gap;
        if (Btn3D(new Rect(bx, by, bw, bh), T("mainmenu"), new Color(0.88f, 0.35f, 0.42f), false)) GoHome();
        by += bh + gap + 12f;

        // Toggle on/off suara
        sfxOn = DrawToggle(new Rect(bx, by, bw, 58f), T("sfx"), sfxOn);
        by += 58f + gap;
        musicOn = DrawToggle(new Rect(bx, by, bw, 58f), T("music"), musicOn);

        // Pemilih bahasa (pojok kanan atas)
        DrawLangPicker(Screen.width - 96f - 16f, 16f);
    }

    // Baris toggle on/off ala switch
    bool DrawToggle(Rect r, string label, bool value)
    {
        RoundRect(r, new Color(0.10f, 0.12f, 0.18f, 0.92f), 16f);
        GuiText(new Rect(r.x + 18f, r.y, r.width - 110f, r.height), label, 20, Color.white, TextAnchor.MiddleLeft);

        float sw = 70f, sh = 34f;
        Rect track = new Rect(r.xMax - sw - 18f, r.y + (r.height - sh) / 2f, sw, sh);
        RoundRect(track, value ? new Color(0.20f, 0.82f, 0.46f) : new Color(0.35f, 0.35f, 0.40f), sh / 2f);
        float kx = value ? track.xMax - sh + 3f : track.x + 3f;
        RoundRect(new Rect(kx, track.y + 3f, sh - 6f, sh - 6f), Color.white, (sh - 6f) / 2f);
        GuiText(new Rect(track.x + 12f, track.y, track.width - 24f, track.height), value ? "ON" : "OFF", 12, new Color(1f, 1f, 1f, 0.9f), value ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight);

        if (GUI.Button(r, GUIContent.none, GUIStyle.none)) value = !value;
        return value;
    }

    // ---- Layar Buat Profil (nama + negara), muncul saat game over pertama ----
    void DrawProfileScreen()
    {
        float cx = Screen.width / 2f;
        FillRect(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0.02f, 0.01f, 0.06f, 0.93f));
        GlowText(new Rect(0f, Screen.height * 0.09f, Screen.width, 80f), T("profileTitle"), 50, new Color(0.5f, 0.9f, 1f), 1f);

        if (editingProfile && Btn3D(new Rect(16f, 16f, 120f, 48f), T("close"), new Color(0.55f, 0.42f, 0.55f), false))
        { editingProfile = false; showProfile = false; countryPicking = false; lbStatus = ""; }

        float pw = Mathf.Min(Screen.width * 0.82f, 470f);
        float px = cx - pw / 2f;
        float py = Screen.height * 0.20f;

        GuiText(new Rect(px, py, pw, 28f), T("nameLabel"), 22, new Color(0.8f, 0.9f, 1f), TextAnchor.MiddleLeft);
        py += 34f;
        RoundRect(new Rect(px, py, pw, 60f), new Color(0.12f, 0.14f, 0.20f, 0.95f), 14f);
        GUIStyle tf = new GUIStyle(GUI.skin.textField) { fontSize = 26, alignment = TextAnchor.MiddleLeft };
        tf.normal.textColor = Color.white;
        tf.padding = new RectOffset(16, 16, 8, 8);
        playerName = GUI.TextField(new Rect(px, py, pw, 60f), playerName, 16, tf);
        py += 76f;

        GuiText(new Rect(px, py, pw, 28f), T("countryLabel"), 22, new Color(0.8f, 0.9f, 1f), TextAnchor.MiddleLeft);
        py += 34f;
        if (Btn3D(new Rect(px, py, pw, 60f), CountryName(playerCountry) + "  (" + playerCountry + ")", new Color(0.30f, 0.40f, 0.62f), false))
            countryPicking = !countryPicking;
        py += 76f;

        if (countryPicking)
        {
            float listH = Screen.height * 0.44f;
            RoundRect(new Rect(px, py, pw, listH), new Color(0.06f, 0.08f, 0.12f, 0.97f), 12f);
            Rect view = new Rect(px + 6f, py + 6f, pw - 12f, listH - 12f);
            float rowH = 52f;
            Rect content = new Rect(0f, 0f, view.width - 20f, countryCodes.Length * rowH);
            countryScroll = GUI.BeginScrollView(view, countryScroll, content);
            for (int i = 0; i < countryCodes.Length; i++)
            {
                Rect rr = new Rect(0f, i * rowH, content.width, rowH - 6f);
                bool sel = countryCodes[i] == playerCountry;
                RoundRect(rr, sel ? new Color(0.20f, 0.55f, 0.42f, 0.95f) : new Color(0.14f, 0.16f, 0.22f, 0.9f), 10f);
                GuiText(new Rect(rr.x + 14f, rr.y, rr.width - 20f, rr.height), countryNames[i] + "  (" + countryCodes[i] + ")", 20, Color.white, TextAnchor.MiddleLeft);
                if (GUI.Button(rr, GUIContent.none, GUIStyle.none)) { playerCountry = countryCodes[i]; countryPicking = false; }
            }
            GUI.EndScrollView();
            return;
        }

        string submitLabel = editingProfile ? T("saveProfile") : T("submit");
        if (Btn3D(new Rect(px, py, pw, 74f), submitLabel, new Color(0.20f, 0.82f, 0.46f), false))
        {
            if (string.IsNullOrEmpty(playerName.Trim())) { lbStatus = T("enterName"); }
            else if (editingProfile)
            {
                SaveProfile();
                PushName();
                homeRanksRequested = false;
                editingProfile = false;
                showProfile = false;
                lbStatus = "";
            }
            else
            {
                SaveProfile();
                SubmitScore();
                showProfile = false;
                showRanks = true;
                LoadRanks();
            }
        }
        py += 86f;
        if (!string.IsNullOrEmpty(lbStatus))
            GuiText(new Rect(px, py, pw, 30f), lbStatus, 18, new Color(1f, 0.8f, 0.4f), TextAnchor.MiddleCenter);
    }

    // ---- Layar PERINGKAT (Top 10 global + peringkat kamu) ----
    void DrawRanksScreen()
    {
        float cx = Screen.width / 2f;
        FillRect(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0.02f, 0.01f, 0.06f, 0.95f));
        GlowText(new Rect(0f, Screen.height * 0.05f, Screen.width, 80f), T("rankings"), 54, new Color(1f, 0.82f, 0.3f), 1f);

        float pw = Mathf.Min(Screen.width * 0.9f, 540f);
        float px = cx - pw / 2f;
        float py = Screen.height * 0.16f;

        if (ranksLoading) { GuiText(new Rect(0f, Screen.height * 0.45f, Screen.width, 40f), T("loading"), 26, Color.white, TextAnchor.MiddleCenter); }
        else if (!string.IsNullOrEmpty(lbStatus)) { GuiText(new Rect(0f, Screen.height * 0.45f, Screen.width, 40f), lbStatus, 22, new Color(1f, 0.8f, 0.4f), TextAnchor.MiddleCenter); }
        else if (ranks.Count == 0) { GuiText(new Rect(0f, Screen.height * 0.45f, Screen.width, 40f), T("noScores"), 24, new Color(1f, 1f, 1f, 0.8f), TextAnchor.MiddleCenter); }
        else
        {
            float rowH = 56f;
            float listTop = Screen.height * 0.15f;
            float listH = Screen.height * 0.66f;
            Rect view = new Rect(px, listTop, pw, listH);
            Rect content = new Rect(0f, 0f, pw - 16f, ranks.Count * (rowH + 6f));
            ranksScroll = GUI.BeginScrollView(view, ranksScroll, content);
            for (int i = 0; i < ranks.Count; i++)
            {
                var e = ranks[i];
                Rect rr = new Rect(0f, i * (rowH + 6f), content.width, rowH);
                Color rowCol = e.you ? new Color(0.20f, 0.55f, 0.42f, 0.95f) : (i < 3 ? new Color(0.20f, 0.18f, 0.30f, 0.95f) : new Color(0.10f, 0.12f, 0.18f, 0.92f));
                RoundRect(rr, rowCol, 12f);
                Color rankCol = i == 0 ? new Color(1f, 0.85f, 0.3f) : i == 1 ? new Color(0.8f, 0.85f, 0.9f) : i == 2 ? new Color(0.85f, 0.55f, 0.3f) : new Color(0.7f, 0.75f, 0.85f);
                GuiText(new Rect(rr.x + 14f, rr.y, 60f, rowH), "#" + e.rank, 24, rankCol, TextAnchor.MiddleLeft);
                string nm = string.IsNullOrEmpty(e.name) ? "-" : e.name;
                if (e.you) nm += "  (" + T("you") + ")";
                GuiText(new Rect(rr.x + 78f, rr.y + 6f, content.width - 220f, rowH - 24f), nm, 20, Color.white, TextAnchor.LowerLeft);
                if (!string.IsNullOrEmpty(e.country))
                    GuiText(new Rect(rr.x + 78f, rr.y + rowH - 24f, content.width - 220f, 20f), CountryName(e.country), 13, new Color(0.7f, 0.8f, 1f), TextAnchor.UpperLeft);
                GuiText(new Rect(rr.width - 146f, rr.y, 132f, rowH), "" + e.score, 24, new Color(0.6f, 1f, 0.75f), TextAnchor.MiddleRight);
            }
            GUI.EndScrollView();
            string mine = myRank > 0 ? T("yourRank") + "  #" + myRank : T("unranked");
            GuiText(new Rect(px, Screen.height * 0.82f, pw, 34f), mine, 22, new Color(1f, 0.9f, 0.5f), TextAnchor.MiddleCenter);
        }

        float bw = Mathf.Min(Screen.width * 0.6f, 300f);
        if (Btn3D(new Rect(cx - bw / 2f, Screen.height * 0.88f, bw, 66f), T("close"), new Color(0.88f, 0.35f, 0.42f), false))
        { showRanks = false; countryPicking = false; }
    }

    void OnGUI()
    {
        // Lacak status tekan (buat efek tombol) lewat event IMGUI, bukan Input lama
        Event ev = Event.current;
        if (ev.type == EventType.MouseDown) pointerDown = true;
        else if (ev.type == EventType.MouseUp) pointerDown = false;

        // Update rekor tertinggi
        if (score > highScore)
        {
            highScore = score;
            PlayerPrefs.SetInt("tetris3d_hi", highScore);
        }

        // Overlay PERINGKAT (leaderboard global) — bisa dibuka dari menu depan / game over
        if (showRanks) { DrawRanksScreen(); return; }

        // Overlay Buat Profil — muncul saat game over pertama sebelum skor dikirim
        if (showProfile) { DrawProfileScreen(); return; }

        // Menu depan (tampilan pembuka) — gambar & stop di sini sampai tekan MAIN
        if (!started) { DrawStartMenu(); return; }

        // Menu jeda — gambar & stop di sini selagi paused
        if (paused) { DrawPauseMenu(); return; }

        // ---- Papan skor (melengkung & cantik) ----
        float spX = 14f, spY = 12f, spW = 264f, spH = 132f;
        RoundRect(new Rect(spX - 3f, spY - 3f, spW + 6f, spH + 6f), new Color(0.25f, 0.9f, 0.55f, 0.25f), 22f); // glow tepi
        RoundRect(new Rect(spX, spY, spW, spH), new Color(0.06f, 0.08f, 0.12f, 0.90f), 20f);                    // panel utama

        // Baris SKOR TERTINGGI — mahkota BESAR + angka BESAR (tanpa teks "TERTINGGI")
        RoundRect(new Rect(spX + 10f, spY + 10f, spW - 20f, 48f), new Color(0.95f, 0.75f, 0.15f, 0.18f), 14f);
        if (crownTex != null)
            GUI.DrawTexture(new Rect(spX + 20f, spY + 15f, 42f, 38f), crownTex, ScaleMode.StretchToFill, true, 0f,
                new Color(1f, 0.85f, 0.28f), Vector4.zero, Vector4.zero);
        GuiText(new Rect(spX + 72f, spY + 10f, spW - 82f, 48f), "" + highScore, 38, new Color(1f, 0.9f, 0.45f), TextAnchor.MiddleLeft);

        // Aksen kiri + label SKOR + angka (tanpa mahkota)
        RoundRect(new Rect(spX + 10f, spY + 66f, 6f, spH - 78f), new Color(0.20f, 0.85f, 0.48f, 0.95f), 3f);
        GuiText(new Rect(spX + 26f, spY + 64f, spW - 34f, 16f), T("score"), 13, new Color(0.55f, 0.95f, 0.70f), TextAnchor.UpperLeft);
        GuiText(new Rect(spX + 26f, spY + 80f, spW - 34f, 34f), "" + score, 30, Color.white, TextAnchor.UpperLeft);
        GuiText(new Rect(spX + 26f, spY + 112f, spW - 34f, 18f), T("lines") + " " + lines + "   " + T("lvl") + " " + level + "   " + T("cols") + " " + columns, 13, new Color(0.80f, 0.92f, 1f), TextAnchor.UpperLeft);

        // Teks "LEVEL UP!" muncul sebentar tiap naik level
        if (levelUpTime > 0f && !gameOver)
        {
            float la = Mathf.Clamp01(levelUpTime / 1.4f);
            GlowText(new Rect(0f, Screen.height * 0.24f, Screen.width, 84f), T("level") + " " + level + "!", 58, new Color(1f, 0.86f, 0.32f, la), la);
        }

        // Teks "COMBO!" pas cincin keclear berantai (cascade)
        if (comboTime > 0f && !gameOver)
        {
            float ca = Mathf.Clamp01(comboTime / 1.3f);
            GlowText(new Rect(0f, Screen.height * 0.31f, Screen.width, 72f), "COMBO x" + comboShow, 48, new Color(1f, 0.55f, 0.9f, ca), ca);
        }

        if (gameOver)
        {
            if (score > highScore) { highScore = score; PlayerPrefs.SetInt("tetris3d_hi", highScore); PlayerPrefs.Save(); }
            FillRect(new Rect(0f, Screen.height * 0.28f, Screen.width, Screen.height * 0.26f), new Color(0f, 0f, 0f, 0.6f));
            GuiText(new Rect(0f, Screen.height * 0.30f, Screen.width, 90f), "GAME OVER", 70, new Color(1f, 0.35f, 0.35f), TextAnchor.MiddleCenter);
            if (Btn3D(new Rect(Screen.width / 2f - 140f, Screen.height * 0.5f, 280f, 80f), T("playAgain"), new Color(0.20f, 0.80f, 0.45f), false)) RetryGame();
            if (Btn3D(new Rect(Screen.width / 2f - 140f, Screen.height * 0.5f + 92f, 280f, 64f), T("rankings"), new Color(0.30f, 0.55f, 0.95f), false)) { showRanks = true; LoadRanks(); }
            return;
        }

        // Tombol JEDA (atas tengah)
        if (Btn3D(new Rect(Screen.width / 2f - 55f, 16f, 110f, 52f), T("pause"), new Color(0.30f, 0.55f, 0.95f), false)) paused = true;

        float bw = Mathf.Min(Screen.width * 0.16f, 130f);
        float bh = bw;
        float pad = 16f;
        float y = Screen.height - bh - pad;

        if (Btn3D(new Rect(pad, y, bw, bh), T("rotate"), new Color(0.16f, 0.78f, 0.40f), false)) Rotate();
        if (Btn3D(new Rect(Screen.width / 2f - bw / 2f, y, bw, bh), T("drop"), new Color(0.10f, 0.62f, 0.32f), false)) HardDrop();
        if (Btn3D(new Rect(Screen.width - bw - pad, y, bw, bh), T("down"), new Color(0.22f, 0.85f, 0.48f), true)) btnSoftDrop = true; // tahan buat turun cepat (di kanan biar enak dijempol)

        // ---- Kotak preview: bentuk balok BERIKUTNYA (kanan atas) ----
        {
            float pvSize = Mathf.Min(Screen.width * 0.26f, 150f);
            float pvX = Screen.width - pvSize - 16f;
            float pvY = 12f;
            float boxH = pvSize + 40f;
            RoundRect(new Rect(pvX - 11f, pvY - 9f, pvSize + 22f, boxH + 6f), new Color(0.25f, 0.9f, 0.55f, 0.22f), 18f); // glow tepi
            RoundRect(new Rect(pvX - 8f, pvY - 6f, pvSize + 16f, boxH), new Color(0.06f, 0.08f, 0.12f, 0.90f), 16f);       // panel
            RoundRect(new Rect(pvX - 2f, pvY - 1f, pvSize + 4f, 24f), new Color(0.20f, 0.85f, 0.48f, 0.95f), 10f);         // chip judul
            GuiText(new Rect(pvX - 2f, pvY - 1f, pvSize + 4f, 24f), T("next"), 13, new Color(0.03f, 0.12f, 0.06f), TextAnchor.MiddleCenter);

            int[] sp = shapes[nextType];
            int cnt = sp.Length / 2;
            float gridTop = pvY + 32f;
            int minx = 99, maxx = -99, miny = 99, maxy = -99;
            for (int i = 0; i < cnt; i++)
            {
                int gx = sp[i * 2], gy = sp[i * 2 + 1];
                minx = Mathf.Min(minx, gx); maxx = Mathf.Max(maxx, gx);
                miny = Mathf.Min(miny, gy); maxy = Mathf.Max(maxy, gy);
            }
            int w = maxx - minx + 1, h = maxy - miny + 1;
            float cell = pvSize / Mathf.Max(w, h);
            float offX = (pvSize - w * cell) / 2f;
            float offY = (pvSize - h * cell) / 2f;
            float inset = cell * 0.10f;
            Color col = BlockColor(nextType);
            for (int i = 0; i < cnt; i++)
            {
                int gx = sp[i * 2] - minx, gy = sp[i * 2 + 1] - miny;
                float rx = pvX + offX + gx * cell + inset;
                float ry = gridTop + offY + (h - 1 - gy) * cell + inset;
                Cell3D(new Rect(rx, ry, cell - inset * 2f, cell - inset * 2f), col);
            }
        }

        // ---- Tutorial PUTAR TABUNG (besar, di tengah) — hilang setelah sentuhan pertama ----
        if (!hintDone)
        {
            float cxc = Screen.width / 2f;
            float cyc = Screen.height * 0.5f;
            FillRect(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.35f)); // redupin layar

            float tubeW = Mathf.Min(Screen.width * 0.34f, 360f);
            float tubeH = tubeW * 0.32f;
            Rect tube = new Rect(cxc - tubeW / 2f, cyc - tubeH / 2f, tubeW, tubeH);
            FillRect(tube, new Color(0.9f, 0.95f, 1f, 0.95f));
            FillRect(new Rect(tube.x, tube.y, tubeW * 0.06f, tube.height), new Color(0.4f, 0.65f, 0.85f, 0.95f));
            FillRect(new Rect(tube.xMax - tubeW * 0.06f, tube.y, tubeW * 0.06f, tube.height), new Color(0.4f, 0.65f, 0.85f, 0.95f));

            if (triTex != null)
            {
                float ah = tubeH * 1.1f;
                Color oldc = GUI.color;
                GUI.color = new Color(0.2f, 0.85f, 1f, 0.98f);
                GUI.DrawTexture(new Rect(tube.xMax + ah * 0.4f, cyc - ah / 2f, ah, ah), triTex); // panah kanan
                Rect rLeft = new Rect(tube.x - ah * 1.4f, cyc - ah / 2f, ah, ah);
                Matrix4x4 mtx = GUI.matrix;
                GUIUtility.ScaleAroundPivot(new Vector2(-1f, 1f), rLeft.center);
                GUI.DrawTexture(rLeft, triTex); // panah kiri (di-flip)
                GUI.matrix = mtx;
                GUI.color = oldc;
            }

            GuiText(new Rect(0f, cyc + tubeH, Screen.width, 60f), T("swipeBig"), 34, Color.white, TextAnchor.MiddleCenter);
            GuiText(new Rect(0f, cyc + tubeH + 56f, Screen.width, 34f), T("touchStart"), 20, new Color(1f, 1f, 1f, 0.8f), TextAnchor.MiddleCenter);
        }
    }
}