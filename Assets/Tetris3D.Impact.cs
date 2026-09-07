using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Random = UnityEngine.Random;
using Object = UnityEngine.Object;

// =====================================================================
//  KUBIKA TOWER - BATCH E (RETUNE CAHAYA) + BATCH F (EFEK BENTURAN)
// ---------------------------------------------------------------------
//  Sumber: KubikaBlast/Assets/Scripts/KubikaLight.cs (E)
//          KubikaBlast/Assets/Scripts/BlastGame.cs   (F)
//
//  File TERPISAH (partial) - ADDITIF. Tidak ada satu baris pun di
//  Tetris3D.cs / Part2 / Part3 / Part4 / Background yang diubah.
//
//  == KENAPA BATCH E ADA ==
//  KubikaBlast sudah pernah melewati persis masalah ini: nilai cahaya
//  dikalibrasi di latar GELAP, lalu latarnya jadi TERANG -> silau, warna blok
//  jadi pucat. Catatan di KubikaLight.cs: intensity 2 -> 1.25 DAN indirect
//  1 -> 0.7 ("kurangi cahaya pantul yang mencuci warna").
//  Tetris3D sudah ikut turun ke 1.25 di Batch A, tapi bounceIntensity BELUM
//  PERNAH DISETEL sama sekali -> masih default Unity 1.0. Sesudah Batch B
//  memasang latar pastel terang, itu jadi sumber silau yang tersisa.
//
//  Anggaran cahaya sebelum file ini:
//    Tetris3D    : Sun 1.25 + Fill 0.30 + ambient 0.30 + indirect 1.00
//    KubikaBlast : Sun 1.25 +          -           + indirect 0.70
//
//  == JEBAKAN SERIALISASI INSPECTOR ==
//  sunIntensity / fillIntensity / bloomThreshold / vignetteAmount adalah field
//  public LAMA: nilainya bisa sudah terkunci di SampleScene, jadi mengubah
//  default di kode TIDAK dijamin berefek. Karena itu semua angka baru di bawah
//  adalah field public BARU (default C# langsung menang), lalu diterapkan ke
//  runtime. Untuk bloom & vignette yang ditulis ulang Part3.Update() setiap
//  frame, yang ditimpa adalah FIELD-nya (bloomThreshold / vignetteAmount)
//  sekali saja - jadi tidak ada tarik-menarik nilai tiap frame.
// =====================================================================

public partial class Tetris3D
{
    // ---------------- BATCH E: TOMBOL PENGATUR (field public BARU) ----------------
    public bool kubikaLightRetune = true;
    public float kubikaSunIntensity = 1.25f;   // sama dengan KubikaBlast
    public float kubikaSunBounce = 0.7f;       // <-- angka yang hilang (indirect multiplier)
    public float kubikaFillLight = 0.18f;      // 0.30 terlalu banyak di latar terang
    public Color kubikaAmbient = new Color(0.22f, 0.22f, 0.25f);
    public float kubikaBloomThreshold = 1.05f; // 0.9 bikin latar pastel sendiri ikut nge-bloom
    [Range(0f, 1f)] public float kubikaVignette = 0.18f;
    public Color kubikaVignetteColor = new Color(0.12f, 0.12f, 0.14f); // ungu gelap -> slate netral

    // ---------------- BATCH F: TOMBOL PENGATUR (field public BARU) ----------------
    public bool kubikaShockwave = true;
    public bool kubikaComboShake = true;
    public bool kubikaHitStop = true;
    [Range(0f, 1f)] public float kubikaShakeStrength = 1f;

    // BlastGame.cs: guncang di-CAP, tidak pernah ditumpuk.
    const float KFX_SHAKE_CAP = 0.85f;
    // Cincin kejut: radius*1.02 -> radius*2.1, tebal vSpace*0.16, 0.42s ease-out.
    const float KFX_RING_DUR = 0.42f;
    const float KFX_RING_FROM = 1.02f;
    const float KFX_RING_TO = 2.10f;
    const float KFX_RING_THICK = 0.16f;
    const float KFX_RING_ALPHA = 0.55f;
    const float KFX_HITSTOP_SCALE = 0.08f;
    const float KFX_HITSTOP_MAX = 0.20f;

    // ---------------- STATE RUNTIME (non-serialisasi) ----------------
    int kfxRetunePass;
    float kfxRetuneNext;
    Light kfxSun, kfxFill;
    Transform kfxRoot;
    float kfxShakeAmt, kfxShakeDur, kfxShakeLeft;
    bool kfxShakeWrote;
    readonly HashSet<int> kfxRinged = new HashSet<int>();
    int kfxPrevLines;
    int kfxPrevStage = int.MinValue;

    // Hit-stop: STATIC supaya pihak lain (menu, dialog item) bisa memeriksa
    // tanpa perlu referensi ke instance, sama seperti BlastGame.HitStopActive.
    public static bool KubikaHitStopActive { get; private set; }
    static float kfxHsEnd, kfxHsPrev, kfxHsScale;

    // ================== LOOP (dipanggil KubikaFxDriver.LateUpdate) ==================
    public void TickKubikaFx()
    {
        KfxRetuneLights();
        KfxTickHitStop();
        KfxDetectClears();
        KfxDetectStage();
        KfxTickShake();
    }

    // ================== BATCH E: RETUNE CAHAYA ==================
    // KubikaLight.cs menerapkan nilainya TIGA kali (Awake, Start, lalu satu frame
    // sesudahnya) karena ada kode lain yang ikut menyentuh lampu sesudah scene
    // dimuat. Pola itu ditiru: 3 sapuan, lalu berhenti total (tidak jalan tiap
    // frame) supaya tidak ada biaya sia-sia di HP.
    void KfxRetuneLights()
    {
        if (!kubikaLightRetune || kfxRetunePass >= 3) return;
        if (cam == null || bloom == null) return;              // SetupScene() belum jalan
        if (Time.unscaledTime < kfxRetuneNext) return;

        kfxRetunePass++;
        kfxRetuneNext = Time.unscaledTime + (kfxRetunePass == 1 ? 0.05f : 0.5f);

        // Cari lampu sekali per sapuan (bukan per frame). Sekaligus matikan lagi
        // directional light lain: KubikaLight.cs mencatat cahaya dobel sebagai
        // penyebab warna blok keruh, dan komponen bisa di-enable ulang pihak lain.
        Light[] all = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            Light l = all[i];
            if (l == null) continue;
            if (l.name == "Sun") { kfxSun = l; continue; }
            if (l.name == "Fill") { kfxFill = l; continue; }
            if (l.type == LightType.Directional) l.enabled = false;
        }

        if (kfxSun != null)
        {
            kfxSun.enabled = true;
            kfxSun.intensity = kubikaSunIntensity;
            kfxSun.bounceIntensity = kubikaSunBounce;   // INI yang belum pernah disetel
            kfxSun.color = Color.white;
            kfxSun.useColorTemperature = true;
            kfxSun.colorTemperature = sunTemperature;   // 5000K, sama dengan KubikaBlast
            kfxSun.shadows = LightShadows.None;
        }
        if (kfxFill != null)
        {
            kfxFill.enabled = true;
            kfxFill.intensity = kubikaFillLight;
            kfxFill.bounceIntensity = 0f;               // isian tidak perlu ikut memantul
            kfxFill.shadows = LightShadows.None;
        }

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = kubikaAmbient;

        // Bloom & vignette: yang ditimpa FIELD-nya, karena Part3.Update() menulis
        // bloom.threshold.value = bloomThreshold tiap frame. Menulis .value saja
        // akan dibatalkan frame berikutnya.
        bloomThreshold = kubikaBloomThreshold;
        vignetteAmount = kubikaVignette;
        if (bloom != null) bloom.threshold.value = kubikaBloomThreshold;
        if (vig != null)
        {
            vig.intensity.value = kubikaVignette;
            // Warna vignette TIDAK ditulis Part3, jadi cukup sekali. Ungu gelap
            // (0.04,0.02,0.10) dulu cocok di latar ungu; di latar pastel Batch B
            // sudutnya terbaca seperti kotor, bukan seperti bayangan.
            vig.color.Override(kubikaVignetteColor);
        }
    }

    // ================== BATCH F: DETEKSI CINCIN PENUH ==================
    // Tetris3D TIDAK punya event OnCleared, dan jumlah 'lines' saja tidak memberi
    // tahu BARIS KE BERAPA yang hancur - padahal cincin kejut butuh ketinggiannya.
    //
    // Jalan keluarnya: FlashClear() menahan baris penuh selama 0.4 detik SEBELUM
    // grid-nya dikosongkan, dan sepanjang itu clearing == true. Jadi baris penuh
    // bisa dibaca langsung dari grid selagi animasi kedipnya berjalan - waktunya
    // malah lebih pas, cincin muncul bersamaan dengan kilatan.
    //
    // Pemindaian hanya jalan saat clearing (24x18 = 432 sel, beberapa frame saja).
    void KfxDetectClears()
    {
        if (grid == null) return;

        // Tiap langkah cascade menambah 'lines' sesudah barisnya dikosongkan.
        // Itu penanda paling murah untuk mengosongkan daftar baris yang sudah
        // diberi cincin - tanpa ini, cincin cascade kedua bisa hilang karena
        // indeks barisnya kebetulan sama dengan yang sudah tercatat.
        if (lines != kfxPrevLines)
        {
            kfxPrevLines = lines;
            kfxRinged.Clear();
        }

        if (!clearing)
        {
            if (kfxRinged.Count > 0) kfxRinged.Clear();
            return;
        }

        List<int> fresh = null;
        for (int r = 0; r < height; r++)
        {
            if (kfxRinged.Contains(r)) continue;
            bool full = true;
            for (int c = 0; c < columns; c++)
                if (grid[c, r] == -1) { full = false; break; }
            if (!full) continue;
            kfxRinged.Add(r);
            if (fresh == null) fresh = new List<int>();
            fresh.Add(r);
        }
        if (fresh == null) return;

        // comboCount baru dinaikkan ResolveBoard() SESUDAH FlashClear() selesai,
        // jadi di titik ini nilainya masih milik clear sebelumnya. comboExpire > 0
        // berarti rentetan masih hidup -> combo yang akan tercatat = comboCount + 1.
        // Ini yang bikin warna cincin & kekuatan guncang sudah benar sejak frame
        // pertama kilatan, bukan terlambat satu clear.
        int combo = comboExpire > 0f ? comboCount + 1 : 1;
        KfxImpact(fresh, combo);
    }

    void KfxImpact(List<int> rows, int combo)
    {
        int n = rows.Count;
        float boost = 1f + Mathf.Clamp01((combo - 1) / 6f) * 0.8f;   // BlastGame.ApplyImpact

        if (kubikaComboShake)
        {
            // Angka dasar sengaja SAMA dengan Part2.FlashClear (0.26 + rows*0.12).
            // Karena penggabungannya MAX (lihat KfxTickShake), clear tunggal tanpa
            // combo terasa persis seperti sebelumnya - yang berubah hanya puncaknya
            // saat rentetan panjang.
            float mag = (0.26f + n * 0.12f) * boost * Mathf.Clamp01(kubikaShakeStrength);
            KfxShake(mag, 0.30f + n * 0.05f);
        }

        if (kubikaShockwave)
            for (int i = 0; i < n; i++) KfxSpawnRing(rows[i], combo);

        // Hit-stop disimpan untuk momen yang memang berat saja. Kalau setiap clear
        // tunggal ikut membekukan waktu, yang terasa bukan "mantap" tapi "nyendat".
        if (kubikaHitStop && (n >= 2 || combo >= 3))
        {
            float sec = 0.05f + n * 0.02f + Mathf.Clamp01((combo - 1) / 6f) * 0.04f;
            KubikaHitStop(sec, KFX_HITSTOP_SCALE);
        }
    }

    // Babak baru (diameter membesar) juga layak dapat benturan. stage cuma naik
    // di StageUp(), jadi polling satu int sudah cukup.
    void KfxDetectStage()
    {
        if (kfxPrevStage == int.MinValue) { kfxPrevStage = stage; return; }
        if (stage == kfxPrevStage) return;
        kfxPrevStage = stage;
        if (kubikaComboShake) KfxShake(0.62f * Mathf.Clamp01(kubikaShakeStrength), 0.5f);
        if (kubikaHitStop) KubikaHitStop(0.12f, 0.10f);
    }

    // ================== GUNCANG KAMERA ==================
    // BlastGame.Shake(): "if (amount > _shake) _shake = min(cap, amount)" - MAX,
    // BUKAN penjumlahan. Guncang yang ditumpuk bikin kamera terbang saat cascade.
    void KfxShake(float amount, float dur)
    {
        amount = Mathf.Min(KFX_SHAKE_CAP, amount);
        if (kfxShakeLeft > 0f && amount <= kfxShakeAmt)
        {
            kfxShakeDur = Mathf.Max(kfxShakeDur, dur);
            kfxShakeLeft = Mathf.Max(kfxShakeLeft, dur);
            return;
        }
        kfxShakeAmt = amount;
        kfxShakeDur = dur;
        kfxShakeLeft = dur;
    }

    // Part3.Update() sudah menulis posisi kamera untuk shakeTime/shakeMag milik
    // Part2. Kita di LateUpdate (sesudah SEMUA Update), jadi tulisan di sini yang
    // terakhir menang. Lapisan lama TIDAK dimatikan - nilainya dibaca lalu
    // digabung MAX, sehingga tidak ada guncang dobel dan Part2 tidak perlu diubah.
    void KfxTickShake()
    {
        if (cam == null) return;

        float mine = 0f;
        if (kfxShakeLeft > 0f)
        {
            // unscaledDeltaTime: selama hit-stop timeScale turun ke 0.08, dan
            // guncangnya justru harus tetap terbaca di layar.
            kfxShakeLeft -= Time.unscaledDeltaTime;
            if (kfxShakeLeft <= 0f) kfxShakeLeft = 0f;
            else mine = kfxShakeAmt * Mathf.Clamp01(kfxShakeLeft / Mathf.Max(0.0001f, kfxShakeDur));
        }

        float legacy = shakeTime > 0f
            ? shakeMag * Mathf.Clamp01(shakeTime / Mathf.Max(0.0001f, shakeDur))
            : 0f;

        float amt = Mathf.Min(KFX_SHAKE_CAP, Mathf.Max(mine, legacy));

        if (amt > 0.0001f)
        {
            // z dibiarkan 0 seperti kode lama: menggeser kamera maju-mundur di
            // tabung sempit ini terasa seperti zoom rusak, bukan benturan.
            cam.transform.position = camBasePos +
                new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), 0f) * amt;
            kfxShakeWrote = true;
        }
        else if (kfxShakeWrote)
        {
            // Hanya sekali saat guncang habis. Kalau posisi kamera ditulis TIAP
            // frame walau tidak ada guncang, kode lain yang menggeser kamera akan
            // ikut dibatalkan tanpa sebab.
            cam.transform.position = camBasePos;
            kfxShakeWrote = false;
        }
    }

    // ================== CINCIN KEJUT ==================
    // Bentuk tabung membuat efek ini pas: satu cincin yang mengembang keluar dari
    // dinding tabung, tepat di ketinggian baris yang hancur.
    Transform KfxEnsureRoot()
    {
        if (kfxRoot != null) return kfxRoot;
        if (spin == null) return null;
        GameObject go = new GameObject("KubikaFx");
        go.transform.SetParent(spin, false);
        kfxRoot = go.transform;
        return kfxRoot;
    }

    static Color KfxGlowColor(int combo)
    {
        if (combo >= 7) return new Color(1f, 0.55f, 0.75f);
        if (combo >= 5) return new Color(1f, 0.72f, 0.35f);
        return new Color(1f, 0.94f, 0.65f);
    }

    void KfxSpawnRing(int row, int combo)
    {
        Transform root = KfxEnsureRoot();
        if (root == null) return;

        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(go.GetComponent<Collider>());
        go.name = "RingShock";
        go.transform.SetParent(root, false);
        // Efek ditaruh di cabang "KubikaFx" sendiri, bukan langsung di bawah spin.
        // Blok papan dilacak lewat array cells[,], jadi ini murni kerapian - tapi
        // KubikaBlast pernah kena masalah FX ikut terhitung sebagai blok asli.
        go.transform.localPosition = new Vector3(0f, row * vSpace, 0f);

        // Cylinder Unity: tinggi 2 unit, radius 0.5 -> skala X/Z = diameter,
        // skala Y = setengah tinggi.
        float d0 = radius * KFX_RING_FROM * 2f;
        float d1 = radius * KFX_RING_TO * 2f;
        float thick = Mathf.Max(0.02f, vSpace * KFX_RING_THICK);
        go.transform.localScale = new Vector3(d0, thick * 0.5f, d0);

        Material m = KfxMakeGlowMat(KfxGlowColor(combo));
        var rend = go.GetComponent<Renderer>();
        if (rend != null) rend.material = m;

        // Jaring pengaman: ClearBoard() memanggil StopAllCoroutines(), jadi animasi
        // di bawah bisa mati di tengah jalan saat pemain menekan ULANG / KE MENU.
        // Destroy berjadwal memastikan tidak ada cincin yang tertinggal membeku.
        Destroy(go, KFX_RING_DUR + 0.25f);
        StartCoroutine(KfxAnimateRing(go.transform, m, d0, d1, thick));
    }

    IEnumerator KfxAnimateRing(Transform tf, Material m, float d0, float d1, float thick)
    {
        float t = 0f;
        while (t < KFX_RING_DUR)
        {
            if (tf == null) yield break;
            // unscaledDeltaTime: Gelembung2 menyetel Time.timeScale = 0 selagi dialog
            // klaim item terbuka, dan cincin tidak boleh menggantung membeku di layar.
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / KFX_RING_DUR);
            float e = 1f - (1f - k) * (1f - k);           // ease-out: cepat di awal
            float d = Mathf.Lerp(d0, d1, e);
            tf.localScale = new Vector3(d, thick * 0.5f * Mathf.Lerp(1f, 0.55f, k), d);
            if (m != null)
            {
                Color c = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : m.color;
                c.a = Mathf.Lerp(KFX_RING_ALPHA, 0f, k);
                m.color = c;
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            }
            yield return null;
        }
        if (tf != null) Destroy(tf.gameObject);
        // Material dibuat saat runtime -> harus dilepas sendiri (catatan
        // _ownedMats di HANDOFF KubikaBlast).
        if (m != null) Destroy(m);
    }

    // Aditif & tidak menulis depth: cincin membaur seperti cahaya, dan bagian yang
    // tertutup blok tetap tertutup karena depth TEST-nya masih jalan.
    Material KfxMakeGlowMat(Color c)
    {
        Shader sh = Shader.Find("Universal Render Pipeline/Unlit");
        if (sh == null) sh = Shader.Find("Unlit/Color");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        Material m = new Material(sh);
        Color cc = new Color(c.r, c.g, c.b, KFX_RING_ALPHA);
        m.color = cc;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", cc);
        if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);   // Transparent
        if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 2f);       // Additive
        if (m.HasProperty("_SrcBlend")) m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (m.HasProperty("_DstBlend")) m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
        if (m.HasProperty("_ZWrite")) m.SetInt("_ZWrite", 0);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 10;
        return m;
    }

    // ================== HIT-STOP ==================
    // Ini bagian paling berbahaya di Batch F: Time.timeScale itu milik bersama.
    // Gelembung2.OpenBubbleClaim() menyetelnya 0 untuk dialog klaim item.
    //
    // Aturan yang dipakai (HANDOFF KubikaBlast pasal 7 - tabel kepemilikan):
    //   1. JANGAN mulai kalau waktu sudah dibekukan pihak lain (timeScale ~ 0).
    //   2. Nilai sebelumnya disimpan, bukan diasumsikan 1.
    //   3. Kalau di tengah hit-stop timeScale berubah jadi bukan nilai kita,
    //      berarti ada pihak lain yang mengambil alih -> kita MUNDUR tanpa
    //      menyentuh apa pun. Dialog item menang, bukan efek visual.
    //   4. Basisnya TIMER, bukan coroutine. Coroutine bisa dibunuh
    //      ClearBoard().StopAllCoroutines() dan game akan tertinggal di
    //      timeScale 0.08 - alias slow motion permanen.
    public void KubikaHitStop(float seconds, float scale)
    {
        if (!kubikaHitStop) return;
        if (KubikaHitStopActive) return;
        if (paused) return;
        if (Time.timeScale <= 0.001f) return;   // dialog / jeda sedang memegang waktu

        seconds = Mathf.Clamp(seconds, 0.01f, KFX_HITSTOP_MAX);
        kfxHsScale = Mathf.Clamp(scale, 0.01f, 1f);
        kfxHsPrev = Time.timeScale;
        kfxHsEnd = Time.unscaledTime + seconds;
        Time.timeScale = kfxHsScale;
        KubikaHitStopActive = true;
    }

    void KfxTickHitStop()
    {
        if (!KubikaHitStopActive) return;
        if (!Mathf.Approximately(Time.timeScale, kfxHsScale))
        {
            KubikaHitStopActive = false;   // pihak lain mengambil alih, jangan diganggu
            return;
        }
        if (Time.unscaledTime < kfxHsEnd) return;
        Time.timeScale = kfxHsPrev <= 0.001f ? 1f : kfxHsPrev;
        KubikaHitStopActive = false;
    }

    // Dipakai driver sebagai jaring pengaman kalau instance game hilang (ganti
    // scene / objek dihancurkan) selagi hit-stop aktif.
    public static void KubikaEndHitStop()
    {
        if (!KubikaHitStopActive) return;
        if (Mathf.Approximately(Time.timeScale, kfxHsScale))
            Time.timeScale = kfxHsPrev <= 0.001f ? 1f : kfxHsPrev;
        KubikaHitStopActive = false;
    }
}

// =====================================================================
//  DRIVER - pola sama dengan KubikaBgDriver (Tetris3D.Background.cs)
// ---------------------------------------------------------------------
//  LateUpdate, dan urutan eksekusi 25100 (SESUDAH KubikaBgDriver 25000):
//  tick ini yang menulis posisi kamera terakhir, jadi hasilnya tidak bisa
//  ditimpa lagi oleh lapisan guncang lama di Part3.Update().
// =====================================================================
[DefaultExecutionOrder(25100)]
public class KubikaFxDriver : MonoBehaviour
{
    Tetris3D game;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (Object.FindFirstObjectByType<KubikaFxDriver>() != null) return;
        var go = new GameObject("KubikaFxDriver");
        DontDestroyOnLoad(go);
        go.AddComponent<KubikaFxDriver>();
    }

    void LateUpdate()
    {
        if (game == null) game = Object.FindFirstObjectByType<Tetris3D>();
        if (game == null)
        {
            // Tidak ada yang bisa mengembalikan timeScale kalau game-nya hilang.
            Tetris3D.KubikaEndHitStop();
            return;
        }
        game.TickKubikaFx();
    }
}
