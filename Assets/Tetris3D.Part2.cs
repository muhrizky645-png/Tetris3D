using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;
using Object = UnityEngine.Object;

public partial class Tetris3D
{
    // F3: apakah balok BERIKUTNYA (yang tampil di kotak NEXT) berupa balok BATU.
    // Diundi bareng bentuknya di PickNextType(), bukan lagi saat balok muncul,
    // supaya kotak pratinjau bisa menggambarnya abu-abu sejak sekarang.
    bool nextStone;

    // F11: jumlah baris sampah yang TERTUNDA karena saat level naik ternyata masih
    // ada balok yang sedang jatuh. Diterapkan di awal SpawnPiece() berikutnya,
    // yaitu saat papan sudah tenang. Lihat AddGarbageRow().
    int pendingGarbage;

    // ---------- LOCK DELAY ----------
    // Dulu balok langsung terkunci begitu tick jatuh berikutnya gagal turun, jadi
    // TIDAK ADA jendela penyesuaian terakhir sama sekali. Padahal kontrol utama
    // game ini adalah geser layar yang sifatnya analog: pemain sering baru sadar
    // posisinya meleset satu kolom tepat saat balok mendarat, dan sudah terlambat.
    // Sekarang balok yang menyentuh tumpukan menunggu LOCK_DELAY detik dulu.
    //
    // Hitungannya di-reset tiap kali pemain benar-benar menggeser atau memutar
    // (lihat TouchLockDelay), tapi dibatasi LOCK_MAX_RESETS supaya balok tidak
    // bisa ditahan melayang selamanya dengan menggeser bolak-balik.
    //
    // HARD DROP tidak lewat jalur ini sama sekali - HardDrop() memanggil
    // LockPiece() langsung, jadi menjatuhkan balok tetap terasa tegas & instan.
    const float LOCK_DELAY = 0.5f;
    const int LOCK_MAX_RESETS = 15;
    float lockTimer;
    int lockResets;

    // Bunyi "tidak bisa" untuk rotasi yang gagal. Dideklarasikan di sini (bukan di
    // Tetris3D.cs) supaya penambahan ini tidak menuntut file itu ikut ditulis ulang;
    // isinya dibuat di Part3.SetupAudio(). Kelas ini partial, jadi field-nya sama.
    AudioClip sfxDeny;

    // Urutan percobaan WALL KICK saat rotasi terhalang. Indeks 0 = rotasi di tempat.
    // Ini SILINDER, jadi tidak ada dinding kiri/kanan - yang menghalangi selalu
    // tumpukan blok, dan Wrap() sudah mengurus kolom yang melingkar. Offset +-2
    // dipakai karena game ini punya bentuk selebar 5 (pentomino), yang sering butuh
    // ruang lebih dari satu kolom untuk berputar. Tiga offset terakhir mengangkat
    // balok satu baris, untuk kasus terjepit di permukaan yang tidak rata.
    static readonly int[] kickCol = { 0, -1, 1, -2, 2, 0, -1, 1 };
    static readonly int[] kickRow = { 0, 0, 0, 0, 0, 1, 1, 1 };

    // ---------- PIECE ----------
    void SpawnPiece()
    {
        // F11: terapkan baris sampah yang tertunda SEKARANG, selagi papan tenang
        // (balok lama sudah terkunci, balok baru belum dibuat). Lihat AddGarbageRow().
        while (pendingGarbage > 0)
        {
            pendingGarbage--;
            AddGarbageRow();
        }

        // F11: sampah yang baru naik bisa saja langsung melewati garis mati. Dulu
        // pemeriksaan ini selalu terjadi SESUDAH AddGarbageRow() (di ResolveBoard),
        // jadi diulang di sini biar urutannya tetap sama setelah penundaan.
        if (TooHigh())
        {
            gameOver = true;
            Sfx(sfxGameOver);
            return;
        }

        curType = nextType;

        // F3: sifat BATU sudah diundi bareng bentuknya waktu pratinjau digambar,
        // jadi di sini tinggal dipakai. Harus disalin SEBELUM PickNextType() di
        // baris bawah, karena fungsi itu menimpa nextStone dengan undian baru.
        curStone = nextStone;

        nextType = PickNextType();
        curN = boxSize[curType];
        int[] s = shapes[curType];
        int n = s.Length / 2;
        curBox = new Vector2Int[n];
        for (int i = 0; i < n; i++)
            curBox[i] = new Vector2Int(s[i * 2], s[i * 2 + 1]);

        // F2: TIDAK ada rotasi acak saat balok muncul. Dulu balok diputar 0-3 kali
        // secara acak, padahal kotak NEXT menggambar bentuk pada orientasi dasar,
        // sehingga preview jadi tidak jujur dan bantuan (assist) yang sudah cari
        // celah pas ikut rusak. Sekarang balok muncul persis seperti di pratinjau.

        // Kolom depan diambil dari sudut ISTIRAHAT (targetSpin), bukan sudut animasi (spinDeg),
        // biar balok baru selalu muncul pas di tengah walau tabung masih berputar.
        int frontCol = Wrap(Mathf.RoundToInt((180f - targetSpin) * columns / 360f));
        // Pusatkan KOTAK balok di kolom depan (bukan tepi kiri) -> titik jatuh di tengah &
        // tabung tidak geser sendiri tiap balok baru muncul.
        curCol = Wrap(frontCol - (curN - 1) / 2);
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

        // Balok baru selalu mulai dengan jatah lock delay penuh.
        lockTimer = 0f;
        lockResets = 0;

        RedrawActive();
        UpdateTargetSpin();
    }

    // Peluang balok BATU efektif: naik bertahap saat sudah di diameter maksimum (endgame),
    // biar makin menantang & skor tidak gampang mentok. Dibatasi 0.45.
    float EffectiveStoneChance()
    {
        float ch = stoneChance;
        if (columns >= maxColumns && maxDiameterLevel > 0)
        {
            int past = Mathf.Max(0, level - maxDiameterLevel);
            ch = Mathf.Min(0.45f, stoneChance + past * 0.02f);
        }
        return ch;
    }

    // ---------- PEMILIHAN BENTUK CERDAS (progresif + "tempat rahasia") ----------
    // Pilih tipe balok berikutnya: bentuk menyesuaikan kemajuan & condong ke balok yang ada tempatnya
    //
    // F3: fungsi ini SEKALIAN mengundi apakah balok berikutnya berupa balok BATU
    // (nextStone). Dulu undian batu terjadi di SpawnPiece(), yaitu SESUDAH kotak
    // pratinjau NEXT digambar, sehingga pratinjau tidak pernah bisa memberi tahu
    // pemain bahwa balok berikutnya tidak bisa diputar. Karena PickNextType()
    // dipanggil dari Start(), SpawnPiece(), RetryGame(), dan GoHome(), menaruh
    // undiannya di sini bikin nextStone selalu ikut ter-update di semua jalur.
    int PickNextType()
    {
        nextStone = stoneEnabled && Random.value < EffectiveStoneChance();

        List<int> pool = AllowedShapes();
        float assist = Mathf.Max(assistMin, assistStart - (level - 1) * assistDecayPerLevel);
        if (Random.value < assist)
        {
            int fit = FindFittingShape(pool);
            if (fit >= 0) return fit;
        }
        return pool[Random.Range(0, pool.Count)];
    }

    // Bentuk yang boleh muncul sesuai level & jumlah kolom (awal sederhana, makin tinggi makin aneh)
    List<int> AllowedShapes()
    {
        bool medium = level >= mediumShapeLevel || columns > startColumns;
        bool weird = columns >= weirdShapeColumns || level >= weirdShapeLevel;
        var pool = new List<int>();
        for (int i = 0; i < shapes.Length; i++)
        {
            int tier = i < shapeTier.Length ? shapeTier[i] : 0;
            if (tier == 0 || (tier == 1 && medium) || (tier == 2 && weird)) pool.Add(i);
        }
        if (pool.Count == 0) pool.Add(0);
        return pool;
    }

    // Cari (acak) bentuk dari pool yang bisa mendarat rapi di celah sekarang -> tempatnya sudah ada, tapi dirahasiakan
    int FindFittingShape(List<int> pool)
    {
        var order = new List<int>(pool);
        for (int i = order.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            int tmp = order[i]; order[i] = order[j]; order[j] = tmp;
        }
        for (int k = 0; k < order.Count; k++)
            if (ShapeFitsSomewhere(order[k])) return order[k];
        return -1;
    }

    // True kalau bentuk ini (di salah satu rotasi & kolom) bisa jatuh menempel tanpa nyisain lubang di bawahnya
    bool ShapeFitsSomewhere(int type)
    {
        int n = boxSize[type];
        int[] s = shapes[type];
        int cnt = s.Length / 2;
        Vector2Int[] box = new Vector2Int[cnt];
        for (int i = 0; i < cnt; i++) box[i] = new Vector2Int(s[i * 2], s[i * 2 + 1]);
        for (int rot = 0; rot < 4; rot++)
        {
            for (int col = 0; col < columns; col++)
            {
                int row = DropRowFor(box, col);
                if (row < -900) continue;
                if (LandsFlush(box, col, row)) return true;
            }
            Vector2Int[] nb = new Vector2Int[cnt];
            for (int i = 0; i < cnt; i++) nb[i] = new Vector2Int(box[i].y, (n - 1) - box[i].x);
            box = nb;
        }
        return false;
    }

    // Baris terendah tempat bentuk berhenti jatuh di kolom tsb (-1000 kalau tak ada tempat)
    int DropRowFor(Vector2Int[] box, int col)
    {
        for (int r = 0; r < height; r++)
            if (Valid(box, col, r) && !Valid(box, col, r - 1)) return r;
        return -1000;
    }

    // Menempel rapi: sel terendah tiap kolom balok punya tumpuan (lantai / blok terisi) tepat di bawahnya
    bool LandsFlush(Vector2Int[] box, int col, int row)
    {
        var lowest = new Dictionary<int, int>();
        for (int i = 0; i < box.Length; i++)
        {
            int c = Wrap(col + box[i].x);
            int r = row + box[i].y;
            if (!lowest.ContainsKey(c) || r < lowest[c]) lowest[c] = r;
        }
        foreach (var kv in lowest)
        {
            int c = kv.Key, r = kv.Value;
            if (r <= 0) continue;
            if (r - 1 >= height) return false;
            if (grid[c, r - 1] == -1) return false;
        }
        return true;
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
            // Gerakan yang berhasil memperpanjang jendela lock delay (kalau balok
            // memang sedang menyentuh tumpukan). Aman dipanggil di semua kondisi:
            // TouchLockDelay() langsung keluar kalau balok belum mendarat.
            TouchLockDelay();
            return true;
        }
        return false;
    }

    // Reset hitung mundur lock delay karena pemain baru saja melakukan sesuatu
    // (menggeser atau memutar) selagi balok sudah menyentuh tumpukan.
    //
    // lockTimer <= 0f berarti balok belum mendarat, jadi tidak ada apa-apa untuk
    // di-reset - ini juga yang membuat Move(0,-1) dari gravitasi biasa tidak
    // menghabiskan jatah reset.
    //
    // Batas LOCK_MAX_RESETS penting: tanpa itu pemain bisa menahan satu balok
    // melayang tanpa batas dengan menggeser bolak-balik, dan game endless jadi
    // tidak pernah benar-benar menekan.
    void TouchLockDelay()
    {
        if (lockTimer <= 0f) return;
        if (lockResets >= LOCK_MAX_RESETS) return;
        lockResets++;
        lockTimer = 0f;
    }

    void Rotate()
    {
        // Balok BATU memang tidak bisa diputar. Dulu fungsi ini langsung return
        // tanpa tanda apa pun, jadi pemain mengira tombolnya rusak. Getaran singkat
        // memberi tahu "tombolmu terbaca, baloknya saja yang memang kaku".
        if (curStone) { Haptic(15); return; }
        if (active == null || curBox == null) return;

        Vector2Int[] nb = new Vector2Int[curBox.Length];
        for (int i = 0; i < curBox.Length; i++)
            nb[i] = new Vector2Int(curBox[i].y, (curN - 1) - curBox[i].x);

        // WALL KICK: dulu hanya orientasi di tempat yang dicoba, dan kalau terhalang
        // rotasi GAGAL DIAM-DIAM. Sekarang beberapa offset dicoba berurutan, dari
        // yang paling dekat ke yang paling jauh, jadi balok yang mepet tumpukan
        // masih bisa diputar dengan menggeser sedikit.
        //
        // curCol sengaja TIDAK di-Wrap di sini, mengikuti konvensi Move() yang juga
        // membiarkan curCol melewati batas - Valid() dan RedrawActive() sudah
        // memanggil Wrap() sendiri di setiap sel.
        for (int k = 0; k < kickCol.Length; k++)
        {
            int nc = curCol + kickCol[k];
            int nr = curRow + kickRow[k];
            if (!Valid(nb, nc, nr)) continue;

            curBox = nb;
            curCol = nc;
            curRow = nr;
            RedrawActive();
            Sfx(sfxRotate);
            // Rotasi yang berhasil juga memperpanjang jendela lock delay, supaya
            // pemain bisa membetulkan orientasi tepat sebelum balok terkunci.
            TouchLockDelay();
            return;
        }

        // Semua percobaan buntu. Beri tanda, jangan diam saja.
        Sfx(sfxDeny);
        Haptic(15);
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

            yield return StartCoroutine(ClearedRowGravity(full));
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

        // Rekam posisi WORLD sel cincin yang akan hancur -> dipakai animasi butiran
        // permata biar muncul MENYEBAR di sekitar cincin, lalu ditarik naik ke chip
        // Permata. Lihat Tetris3D.Currency.cs (CurCaptureRingBurst / SpawnGemBurst).
        CurCaptureRingBurst(objs);

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

    // Gravitasi cascade PENUH: tiap kotak jatuh mengisi ruang kosong di kolomnya.
    // Dipakai efek item (Bom/Palu) yang menyisakan lubang acak & harus dirapatkan.
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

        yield return StartCoroutine(AnimateFall(movers, fromY, toY));
    }

    // Gravitasi clear cincin: blok di BAWAH cincin TETAP DIAM (celah kejebak dibiarkan),
    // sedangkan blok di ATAS cincin JATUH menumpuk rapat di atas tumpukan bawah
    // (atau sampai dasar kalau di bawahnya kosong), sehingga ruang kosong yang bisa
    // dijangkau blok dari atas akan terisi. Baris cincin sudah dikosongkan di FlashClear.
    IEnumerator ClearedRowGravity(List<int> clearedRows)
    {
        // Baris cincin TERENDAH = batas: di bawahnya diam, di atasnya jatuh.
        int minC = height;
        foreach (int r in clearedRows) if (r < minC) minC = r;
        if (minC >= height) yield break;

        var movers = new List<Transform>();
        var fromY = new List<float>();
        var toY = new List<float>();

        for (int c = 0; c < columns; c++)
        {
            // Titik pijak = tepat di atas blok TERAKHIR yang ada di bawah cincin.
            // Kalau di bawah cincin kosong semua -> blok atas jatuh sampai dasar (0).
            int landBase = 0;
            for (int r = 0; r < minC; r++)
                if (grid[c, r] != -1) landBase = r + 1;

            // Kumpulkan blok di ATAS cincin (dari bawah ke atas). Baris cincin sudah kosong.
            var upGrid = new List<int>();
            var upObj = new List<GameObject>();
            for (int r = minC; r < height; r++)
            {
                if (grid[c, r] == -1) continue;
                upGrid.Add(grid[c, r]);
                upObj.Add(cells[c, r]);
            }

            // Kosongkan mulai titik pijak ke atas (posisi lama blok atas + ruang di bawah cincin).
            // Baris di bawah titik pijak TIDAK disentuh -> blok bawah tetap nempel.
            for (int r = landBase; r < height; r++) { grid[c, r] = -1; cells[c, r] = null; }

            // Tumpuk rapat blok atas mulai dari titik pijak (mengisi ruang kosong yang terjangkau).
            for (int i = 0; i < upGrid.Count; i++)
            {
                int w = landBase + i;
                grid[c, w] = upGrid[i];
                cells[c, w] = upObj[i];
                GameObject go = upObj[i];
                if (go != null)
                {
                    movers.Add(go.transform);
                    fromY.Add(go.transform.localPosition.y);
                    toY.Add(CellLocalPos(c, w).y);
                }
            }
        }

        yield return StartCoroutine(AnimateFall(movers, fromY, toY));
    }

    // Animasi jatuh bersama (lerp 0.16 dtk) untuk daftar balok yang bergeser turun.
    IEnumerator AnimateFall(List<Transform> movers, List<float> fromY, List<float> toY)
    {
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
        // Hard drop sengaja MELEWATI lock delay: pemain sudah menyatakan niatnya
        // dengan tegas, jadi jendela penyesuaian justru akan terasa lamban.
        while (Move(0, -1)) { }
        Sfx(sfxDrop);
        LockPiece();
    }

    // Naik level dari skor, syarat makin gede tiap level (berjenjang)
    void RecalcLevel()
    {
        // F4: maksimal NAIK SATU level per kejadian clear. Dulu memakai while, jadi
        // satu combo besar bisa melompati beberapa level sekaligus dan memanggil
        // OnLevelUp() berkali-kali dalam satu frame -> StageUp (papan dibersihkan)
        // atau baris sampah bertumpuk sekaligus, terasa tidak adil & bikin kaget.
        // Sisa kelebihan skor tetap tersimpan di nextLevelScore, jadi level
        // berikutnya menyusul pada clear sesudahnya.
        if (score >= nextLevelScore)
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
            if (columns >= maxColumns)
            {
                AddGarbageRow();
                // Eskalasi endgame: baris sampah GANDA tiap 4 level setelah diameter mentok.
                if (maxDiameterLevel > 0)
                {
                    int past = level - maxDiameterLevel;
                    if (past > 0 && past % 4 == 0) AddGarbageRow();
                }
            }
        }
        stoneEnabled = level >= stoneStartLevel;
    }

    // Babak baru: bersihin papan, diameter membesar, plafon reset, warna latar ganti
    void StageUp()
    {
        DestroyBoardObjects();
        columns = Mathf.Min(maxColumns, columns + columnsPerStage);
        if (columns >= maxColumns && maxDiameterLevel < 0) maxDiameterLevel = level;
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

    // F13: lubang baris sampah yang dipakai TERAKHIR kali. Baris sampah berikutnya
    // menurunkan lubangnya dari sini (digeser paling jauh 1 kolom) supaya lubang
    // antar baris SEJAJAR dan membentuk lorong yang bisa ditembus balok.
    List<int> lastGarbageGaps;

    // Baris sampah naik dari bawah (fase diameter maksimum)
    void AddGarbageRow()
    {
        // F11: JANGAN dorong papan naik selagi masih ada balok yang jatuh.
        // Fungsi ini menggeser SELURUH isi grid naik satu baris, tapi balok aktif
        // (curRow/curCol beserta GameObject-nya) tidak ikut digeser. Lewat jalur
        // normal ini aman, karena baris sampah dipicu dari ResolveBoard() saat
        // balok sudah terkunci (active == null). TAPI clear akibat ITEM
        // (Bom/Palu/Garis) lewat ResolveClearsNoSpawn() memanggil RecalcLevel()
        // selagi balok masih jatuh -> balok aktif bisa mendadak tumpang tindih
        // dengan tumpukan yang baru naik, atau hilang tertelan saat dikunci.
        // Jadi barisnya ditunda dulu, lalu diterapkan di awal SpawnPiece().
        if (active != null) { pendingGarbage++; return; }

        for (int c = 0; c < columns; c++)
            if (cells[c, height - 1] != null) Destroy(cells[c, height - 1]);

        for (int r = height - 1; r >= 1; r--)
            for (int c = 0; c < columns; c++)
            {
                grid[c, r] = grid[c, r - 1];
                cells[c, r] = cells[c, r - 1];
                if (cells[c, r] != null) PlaceObj(cells[c, r], c, r);
            }

        // F13: lubang TIDAK lagi diacak dari nol tiap baris. Dengan garbageGapCount = 2
        // di 24 kolom, peluang lubang baris baru sejajar dengan baris sebelumnya sangat
        // kecil -> tumpukan sampah jadi tembok berlubang selang-seling yang mustahil
        // ditembus balok apa pun. Itu terasa TIDAK ADIL, bukan sulit. Sekarang lubang
        // diturunkan dari baris sampah sebelumnya dan digeser maksimal 1 kolom, jadi
        // terbentuk lorong menyambung ke atas: pemain masih harus mengarahkan balok,
        // tapi jalannya selalu ada.
        int gapN = Mathf.Clamp(garbageGapCount, 1, columns - 1);
        var gaps = new HashSet<int>();

        if (lastGarbageGaps != null && lastGarbageGaps.Count > 0)
        {
            for (int i = 0; i < lastGarbageGaps.Count && gaps.Count < gapN; i++)
                gaps.Add(Wrap(lastGarbageGaps[i] + Random.Range(-1, 2)));
        }

        // Tambal sisa kuota: baris sampah PERTAMA tiap sesi (lastGarbageGaps masih
        // kosong), atau kalau ada lubang yang bertabrakan setelah digeser dan dibuang
        // HashSet. Loop lama tetap dipakai sebagai fallback acak.
        int guard = 0;
        while (gaps.Count < gapN && guard++ < 500) gaps.Add(Random.Range(0, columns));

        // Simpan pola lubang ini buat baris sampah berikutnya.
        if (lastGarbageGaps == null) lastGarbageGaps = new List<int>();
        lastGarbageGaps.Clear();
        foreach (int g in gaps) lastGarbageGaps.Add(g);

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
        // F8: reset pitch SFX ke normal. Sfx() menaikkan pitch mengikuti combo;
        // kalau game berakhir / di-retry saat pitch masih tinggi, semua suara
        // sesudahnya ikut melengking sampai combo berikutnya menormalkannya.
        if (sfx != null) sfx.pitch = 1f;
        DestroyBoardObjects();
        columns = Mathf.Max(3, startColumns);
        radius = baseRadius;
        AllocGrid();
        stage = 0;
        maxDiameterLevel = -1;
        killLine = height;
        stoneEnabled = false;
        score = 0; lines = 0; level = 1;
        nextLevelScore = baseLevelScore;
        fallTimer = 0f; gameOver = false; clearing = false;
        levelUpTime = 0f; comboTime = 0f;
        comboCount = 0; comboExpire = 0f;
        gameOverHandled = false; showProfile = false; showRanks = false; editingProfile = false;
        // F13: buang pola lubang baris sampah sesi sebelumnya, biar lorongnya tidak
        // terbawa ke sesi baru (retry / ke menu).
        if (lastGarbageGaps != null) lastGarbageGaps.Clear();
        // F11: buang sisa baris sampah tertunda dari sesi sebelumnya, biar sesi baru
        // tidak langsung kena sampah warisan.
        pendingGarbage = 0;
        // F3: batalkan juga undian BATU buat balok berikutnya (stoneEnabled sudah
        // di-reset false di atas). PickNextType() sesudah ini akan mengundi ulang.
        nextStone = false;
        // Lock delay: sesi baru mulai bersih, jatah reset penuh.
        lockTimer = 0f;
        lockResets = 0;
        ApplyGeometry();
        ApplyStageColors();
    }

    void RetryGame()
    {
        ClearBoard();
        paused = false;
        started = true;
        nextType = PickNextType();
        SpawnPiece();
    }

    void GoHome()
    {
        ClearBoard();
        paused = false;
        started = false;
        nextType = PickNextType();
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
        main.duration = 0.5f;
        main.loop = false;
        main.startLifetime = 0.42f;
        main.startSpeed = 2.1f;
        main.startSize = 0.22f;
        main.startColor = c;
        main.gravityModifier = 0.8f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 32;
        var em = ps.emission;
        em.rateOverTime = 0f;
        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Sphere;
        sh.radius = 0.12f;
        var rend = ps.GetComponent<ParticleSystemRenderer>();
        if (particleMat != null) rend.material = particleMat;
        ps.Emit(16);
        Destroy(pg, 0.9f);
    }
}
