using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;
using Object = UnityEngine.Object;

public partial class Tetris3D
{
    // ---------- PIECE ----------
    void SpawnPiece()
    {
        curType = nextType;
        nextType = PickNextType();
        curN = boxSize[curType];
        int[] s = shapes[curType];
        int n = s.Length / 2;
        curBox = new Vector2Int[n];
        for (int i = 0; i < n; i++)
            curBox[i] = new Vector2Int(s[i * 2], s[i * 2 + 1]);

        curStone = stoneEnabled && Random.value < EffectiveStoneChance();

        if (!curStone)
        {
            int spins = Random.Range(0, 4);
            for (int k = 0; k < spins; k++)
                for (int i = 0; i < curBox.Length; i++)
                    curBox[i] = new Vector2Int(curBox[i].y, (curN - 1) - curBox[i].x);
        }

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
    int PickNextType()
    {
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
        maxDiameterLevel = -1;
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
}
