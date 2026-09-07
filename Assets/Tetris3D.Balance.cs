using UnityEngine;

// =====================================================================
//  KUBIKA TOWER - BATCH G (EKONOMI) + BATCH I (FPS)
// ---------------------------------------------------------------------
//  Sumber: KubikaBlast/Assets/Scripts/BlastCore.cs  (G)
//          KubikaBlast/Assets/Scripts/KubikaPerf.cs (I)
//
//  File TERPISAH (partial) - ADDITIF. Tidak ada satu baris pun di
//  Tetris3D.cs / Part2 / Part3 / Part4 yang diubah.
//
//  == MASALAH YANG DIPERBAIKI ==
//  Part2.ResolveBoard() menghitung skor begini:
//      pts = columns * cellPoints * rowMult * Mathf.Max(1, comboCount)
//  comboCount TIDAK ADA BATASNYA. Combo 12 = pengali 12x. KubikaBlast
//  sudah pernah membuang model itu dan menggantinya dengan:
//      COMBO_CAP = 8, COMBO_STEP = 0.35  ->  pengali maksimum 3.45x
//  Alasannya bukan sekadar angka: pengali linear tak terbatas membuat
//  satu rentetan beruntung mengalahkan seluruh sisa permainan, dan
//  papan peringkat jadi lomba "siapa paling beruntung sekali", bukan
//  siapa paling konsisten.
//
//  == KENAPA TIDAK PERLU MENGUBAH Part2.cs ==
//  Rumus lamanya bisa dihitung ulang dari LUAR: jumlah baris yang hancur
//  = pertambahan 'lines' pada frame itu, dan comboCount terlihat apa
//  adanya. Jadi selisih antara rumus lama dan rumus bercap dikoreksi
//  langsung ke 'score'. Hasil akhirnya identik dengan mengedit rumusnya,
//  tanpa menulis ulang file 34 KB yang penuh perbaikan F1-F13.
// =====================================================================

public partial class Tetris3D
{
    // ---------------- BATCH G: TOMBOL PENGATUR (field public BARU) ----------------
    public bool kubikaEconomy = true;
    public int kubikaComboCap = 8;                             // BlastCore.COMBO_CAP
    [Range(0f, 1f)] public float kubikaComboStep = 0.35f;      // BlastCore.COMBO_STEP
    public int kubikaLinesPerLevel = 12;                       // BlastCore.LINES_PER_LEVEL

    // ---------------- BATCH J: JENDELA COMBO (field public BARU) ----------------
    // Jeda (detik) antar penghancuran cincin/baris yang membuat rentetan kata
    // pujian GOOD! -> LEGENDARY!! terus menyambung. Ditimpa ke comboSeconds saat
    // runtime (lihat TickKubikaBalance: kenapa harus ditimpa, bukan ganti default).
    [Range(1f, 60f)] public float kubikaComboWindow = 20f;

    // ---------------- BATCH I: TOMBOL PENGATUR ----------------
    public bool kubikaPerf = true;
    public int kubikaFps = 60;

    // ---------------- STATE RUNTIME ----------------
    int keqPrevLines;             // pencacah tepi SENDIRI
    float keqPrevComboTime;
    bool keqSeeded;
    int keqPerfPass;
    float keqPerfNext;

    // ================== LOOP (dipanggil KubikaBalanceDriver.LateUpdate) ==================
    public void TickKubikaBalance()
    {
        KeqTickPerf();

        // ---- JENDELA COMBO (jarak kata pujian) ----
        // comboSeconds adalah field LAMA yang sudah ter-serialize di SampleScene
        // (nilai scene = 10 detik). Mengganti default-nya di Tetris3D.cs TIDAK
        // berpengaruh - nilai scene selalu menang. Jadi ditimpa dari sini supaya
        // nilai yang diminta (kubikaComboWindow) benar-benar berlaku di game.
        if (kubikaComboWindow > 0f && comboSeconds != kubikaComboWindow)
            comboSeconds = kubikaComboWindow;

        if (!keqSeeded)
        {
            // Tick pertama hanya menanam nilai awal, jangan sampai dianggap kejadian.
            keqPrevLines = lines;
            keqPrevComboTime = comboTime;
            keqSeeded = true;
            return;
        }

        int delta = lines - keqPrevLines;
        float ct = comboTime;
        bool comboEdge = ct > keqPrevComboTime + 0.0001f;
        keqPrevLines = lines;
        keqPrevComboTime = ct;

        if (!kubikaEconomy) return;

        int cap = Mathf.Max(2, kubikaComboCap);

        // ---- KOREKSI SKOR ----
        // Hanya saat comboTime melompat naik, yaitu tepat sesudah
        // Part2.ResolveBoard() menambah skor untuk clear dengan combo >= 2.
        // Untuk combo 1, rumus lama (pengali 1) dan rumus baru (1 + 0*step = 1)
        // memberi angka yang SAMA, jadi tidak ada yang perlu dikoreksi - dan
        // itu sekaligus membuat clear dari ITEM (Bom/Palu, yang lewat jalur
        // lain dan tidak menyentuh combo) tidak pernah ikut terkoreksi salah.
        if (comboEdge && delta > 0 && !gameOver)
        {
            float rowMult = delta <= 1 ? 1f
                          : delta == 2 ? 2.5f
                          : delta == 3 ? 4.5f
                          : 7f + (delta - 4) * 2f;              // sama dengan Part2

            int oldCombo = Mathf.Max(1, comboCount);            // yang DIPAKAI Part2 tadi
            int newCombo = Mathf.Min(oldCombo, cap);
            float newMult = 1f + (newCombo - 1) * Mathf.Max(0f, kubikaComboStep);

            int ptsOld = Mathf.RoundToInt(columns * cellPoints * rowMult * oldCombo);
            int ptsNew = Mathf.RoundToInt(columns * cellPoints * rowMult * newMult);

            score += ptsNew - ptsOld;
            if (score < 0) score = 0;
        }

        // ---- CAP PENGHITUNG COMBO ----
        // Dilakukan SESUDAH koreksi di atas, supaya ptsOld masih memakai angka
        // asli yang tadi dipakai Part2. Setelah dicap, HUD berhenti di
        // "COMBO x8" dan tier pujian berhenti tepat di LEGENDARY!! (tier 7).
        if (comboCount > cap) comboCount = cap;
        if (comboShow > cap) comboShow = cap;

        // ---- LEVEL DARI BARIS ----
        // Level lama murni dari SKOR (Part2.RecalcLevel). Karena pengali combo
        // sekarang dicap, skor tumbuh lebih tenang dan pemain yang rajin clear
        // tanpa combo besar bisa mandek levelnya. KubikaBlast memakai
        // LINES_PER_LEVEL = 12 sebagai lantai yang pasti.
        //
        // PENTING - kenapa lewat nextLevelScore, bukan memanggil OnLevelUp()
        // sendiri: OnLevelUp() bisa memicu StageUp() yang memanggil
        // DestroyBoardObjects(). Part2 hanya pernah menjalankannya di satu titik
        // aman (papan tenang, active == null, di ujung ResolveBoard). Memanggilnya
        // dari LateUpdate berarti papan bisa dihapus selagi balok masih jatuh atau
        // selagi coroutine clear berjalan -> balok aktif hilang / NullReference.
        // Jadi yang dilakukan hanya MENURUNKAN ambang skor, lalu RecalcLevel()
        // milik Part2 yang mengeksekusinya di waktu yang sudah terbukti aman.
        // Konsekuensi yang disengaja: level dari baris berlaku pada clear
        // BERIKUTNYA (tertunda satu clear), bukan seketika.
        if (kubikaLinesPerLevel > 0 && !gameOver && started)
        {
            int need = level * Mathf.Max(1, kubikaLinesPerLevel);
            if (lines >= need && nextLevelScore > score) nextLevelScore = score;
        }
    }

    // ================== BATCH I: FPS ==================
    // KubikaPerf.cs di KubikaBlast adalah SATU-SATUNYA pemilik targetFrameRate
    // (HANDOFF pasal 7, tabel kepemilikan). Di Tetris3D, Start() hanya menyetel
    // Screen.orientation - tidak ada yang menyentuh frame rate sama sekali,
    // jadi HP kelas menengah berjalan di default platform yang bisa saja 30.
    //
    // Dijalankan 3 sapuan lalu BERHENTI TOTAL, bukan tiap frame. Kalau kelak ada
    // file lain yang menyetel targetFrameRate sesudah ini, file itu yang menang -
    // dan itu memang benar: tabel kepemilikan tidak boleh diperebutkan tiap frame.
    void KeqTickPerf()
    {
        if (!kubikaPerf || keqPerfPass >= 3) return;
        if (Time.unscaledTime < keqPerfNext) return;
        keqPerfPass++;
        keqPerfNext = Time.unscaledTime + (keqPerfPass == 1 ? 0.05f : 0.5f);

        int fps = Mathf.Clamp(kubikaFps, 30, 120);
        // vSyncCount HARUS 0 lebih dulu: selama vSync menyala, Unity mengabaikan
        // targetFrameRate sepenuhnya.
        QualitySettings.vSyncCount = 0;
        if (Application.targetFrameRate != fps) Application.targetFrameRate = fps;
        // Game ini bisa dilihat lama tanpa disentuh (menu, layar peringkat).
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
    }
}

// =====================================================================
//  DRIVER EKONOMI - order 25150
//  Sesudah KubikaFxDriver (25100), SEBELUM KubikaPraiseDriver (25200):
//  cap combo harus sudah diterapkan ke comboShow sebelum tier kata
//  pujian dibaca di file Praise.
// =====================================================================
[DefaultExecutionOrder(25150)]
public class KubikaBalanceDriver : MonoBehaviour
{
    Tetris3D game;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (Object.FindFirstObjectByType<KubikaBalanceDriver>() != null) return;
        var go = new GameObject("KubikaBalanceDriver");
        DontDestroyOnLoad(go);
        go.AddComponent<KubikaBalanceDriver>();
    }

    void LateUpdate()
    {
        if (game == null) game = Object.FindFirstObjectByType<Tetris3D>();
        if (game == null) return;
        game.TickKubikaBalance();
    }
}
