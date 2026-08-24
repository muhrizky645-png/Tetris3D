using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if KUBIKA_ADMOB
using GoogleMobileAds.Api;
#endif

// =====================================================================
//  KUBIKA TOWER x SALDOKU - GELEMBUNG ITEM DROP (bagian 2)
// ---------------------------------------------------------------------
//  Lanjutan dari Tetris3D.Gelembung.cs (partial yang sama).
// =====================================================================

public partial class Tetris3D
{
    public void DrawBubbleClaim()
    {
        if (!kbClaimOpen) return;
        float sw = VW, sh = VH;
        RoundRect(new Rect(0f, 0f, sw, sh), new Color(0f, 0f, 0f, 0.72f), 0f);

        float pw = Mathf.Min(sw * 0.86f, 620f);
        float ph = 540f;
        float px = (sw - pw) * 0.5f;
        float py = (sh - ph) * 0.5f;
        RoundRect(new Rect(px - 4f, py - 4f, pw + 8f, ph + 8f), new Color(0.6f, 0.85f, 1f, 0.45f), 26f);
        RoundRect(new Rect(px, py, pw, ph), new Color(0.06f, 0.08f, 0.12f, 0.98f), 24f);

        float cx = px + 30f, cw = pw - 60f, yy = py + 26f;
        GuiText(new Rect(cx, yy, cw, 44f), SalID ? "Klaim Item" : "Claim Item", 36, Color.white, TextAnchor.UpperCenter);
        yy += 60f;

        float ic = 120f;
        DrawItemIcon(new Rect(px + pw / 2f - ic / 2f, yy, ic, ic), kbClaimType);
        yy += ic + 10f;

        GuiText(new Rect(cx, yy, cw, 36f), BubbleItemName(kbClaimType), 30, new Color(0.9f, 0.95f, 1f), TextAnchor.UpperCenter);
        yy += 42f;
        GuiText(new Rect(cx, yy, cw, 32f), BubbleItemDesc(kbClaimType), 22, new Color(0.75f, 0.8f, 0.9f), TextAnchor.UpperCenter);
        yy += 42f;
        GuiText(new Rect(cx, yy, cw, 28f), SalID ? "Tonton iklan untuk klaim." : "Watch an ad to claim.", 20, new Color(0.62f, 0.7f, 1f), TextAnchor.UpperCenter);
        yy += 38f;

        string st = !string.IsNullOrEmpty(kbClaimStatus) ? kbClaimStatus : kbDropStatus;
        if (!string.IsNullOrEmpty(st))
            GuiText(new Rect(cx, yy, cw, 28f), st, 20, new Color(1f, 0.85f, 0.5f), TextAnchor.UpperCenter);
        yy += 34f;

        float bw = (cw - 16f) * 0.5f;
        // Tidak ada lagi hitung mundur di tampilan. Cooldown 3 menit diproses di
        // balik layar: selama cooldown, gelembung BUFF berhenti muncul & hanya
        // Permata yang keluar (lihat PickBubbleType). Jadi tombol selalu siap.
        string watch = kbAdBusy ? (SalID ? "Memuat iklan..." : "Loading ad...") : (SalID ? "Tonton Iklan" : "Watch Ad");
        Color watchCol = new Color(1f, 0.62f, 0.12f);
        bool doWatch = Btn3D(new Rect(cx, yy, bw, 84f), watch, watchCol, false);
        bool doLater = Btn3D(new Rect(cx + bw + 16f, yy, bw, 84f), SalID ? "Nanti" : "Later", new Color(0.4f, 0.35f, 0.45f), false);

        GUI.Button(new Rect(0f, 0f, sw, sh), GUIContent.none, GUIStyle.none);

        if (doWatch && !kbAdBusy) ClaimWatchAd();
        if (doLater) CloseBubbleClaim();
    }

    // ========================= AKSI =========================
    void OpenBubbleClaim(KBubble b)
    {
        if (kbubbles != null) kbubbles.Remove(b);
        kbClaimType = b.type;
        kbClaimOpen = true;
        kbClaimStatus = "";
        kbDropStatus = "";
        Time.timeScale = 0f;
    }

    void CloseBubbleClaim()
    {
        kbClaimOpen = false;
        kbClaimStatus = "";
        kbDropStatus = "";
        Time.timeScale = 1f;
    }

    void ClaimWatchAd()
    {
        int t = kbClaimType;
        if (t == IT_COIN)
        {
            EnsureCurrency();
            EnsureSaldoku();
            if (!cur_linked || string.IsNullOrEmpty(sal_token))
            {
                kbClaimStatus = SalID ? "Hubungkan akun SALDOKU dulu." : "Link your SALDOKU account first.";
                return;
            }
            kbClaimStatus = ""; kbDropStatus = "";
            kbClaimOpen = false; Time.timeScale = 1f;
            KubikaExtraAds.Instance.Show(this, KubikaExtraAds.MODE_DROP, sal_token, OnBubbleCoinReward);
        }
        else
        {
            // Tanpa hitung mundur di UI. Cooldown 3 menit hanya menahan kemunculan
            // gelembung BUFF (lihat PickBubbleType). Permata (IT_GEM) TIDAK memicu
            // cooldown, jadi hanya Bom/Palu/Perlambat yang menyetel timer.
            int bt = t;
            kbClaimOpen = false; Time.timeScale = 1f;
            KubikaExtraAds.Instance.Show(this, KubikaExtraAds.MODE_BUFF, null, () => { if (bt <= IT_SLOW) kbLastBuffAdTime = Time.unscaledTime; kbPendingBuff = bt; });
        }
    }

    public void SetBubbleAdBusy(bool b) { kbAdBusy = b; }
    public string BubbleAdsOffMsg() { return SalID ? "Fitur iklan belum aktif di build ini." : "Ads are not enabled in this build."; }
    public void OnBubbleAdUnavailable(string msg) { kbAdBusy = false; KbToast(msg); }
    public void OnBubbleCoinReward()
    {
        KbToast(SalID ? "Iklan selesai. Menambah koin..." : "Ad done. Crediting coins...");
        StartCoroutine(CoAfterBubbleDrop());
    }
    IEnumerator CoAfterBubbleDrop()
    {
        yield return new WaitForSeconds(2.5f);
        yield return StartCoroutine(CoRefreshKoin(false));
        KbToast(SalID ? "Koin diperbarui!" : "Coins updated!");
    }

    // ========================= BUFF =========================
    void ApplyBuff(int type)
    {
        switch (type)
        {
            case IT_BOMB: ApplyBomb(); break;
            case IT_LINE: ApplyHammer(); break;
            case IT_SLOW: ApplySlow(); break;
            case IT_GEM:
                AddPermata(GEM_BONUS);
                Sfx(sfxClear);
                CurGemChipPulse();
                KbToast("+" + GEM_BONUS + (SalID ? " Permata!" : " Gems!"));
                break;
        }
    }

    // ================== HADIAH ITEM -> INVENTORY + ANIMASI ==================
    // Item hasil klaim (tonton iklan) TIDAK langsung dipakai, tapi MASUK ke
    // inventaris (tokoInv). Animasi: ikon membesar di tengah layar lalu terbang
    // mengecil ke slot inventaris kiri, diikuti teks "+1". Saat item dipakai
    // dari inventaris muncul "-1". Digambar oleh KubikaTokoHUD (paling depan)
    // supaya teks +1/-1 tidak ketiban bingkai inventaris.

    struct KbReward { public int type; public int slot; public float t; public float dur; }
    struct KbPlus  { public int slot; public float t; public int delta; }
    List<KbReward> kbRewards;
    List<KbPlus>   kbPlusOnes;

    // Rect slot inventaris buff ke-i. Rumus harus sama dgn DrawBuffInv (Toko).
    Rect KbBuffSlotRect(int i)
    {
        float slot = Mathf.Min(VW * 0.15f, 96f);
        float gap = 14f;
        float totalH = 3f * slot + 2f * gap;
        float sx = 12f;
        float sy = VH * 0.5f - totalH * 0.5f;
        return new Rect(sx, sy + i * (slot + gap), slot, slot);
    }

    // Dipanggil setelah iklan buff selesai (ganti pemakaian langsung).
    void AddBuffReward(int type)
    {
        // Hanya buff (Bom/Palu/Perlambat = 0/1/2) yang masuk inventaris.
        if (type < 0 || type > 2) { ApplyBuff(type); return; }
        EnsureToko();
        tokoInv[type]++;
        SaveToko();
        Sfx(sfxClear);
        if (kbRewards == null) kbRewards = new List<KbReward>();
        kbRewards.Add(new KbReward { type = type, slot = type, t = 0f, dur = 1.0f });
    }

    // Dipanggil saat pemain MEMAKAI item dari inventaris (tap slot) supaya
    // muncul teks "-1" melompat di slot tsb (kebalikan dari "+1").
    public void KbUsePopup(int slot)
    {
        if (slot < 0 || slot > 2) return;
        if (kbPlusOnes == null) kbPlusOnes = new List<KbPlus>();
        kbPlusOnes.Add(new KbPlus { slot = slot, t = 0f, delta = -1 });
    }

    // Maju-kan timer animasi tiap frame (dipanggil dari BubbleTick).
    void TickRewardAnims()
    {
        float dt = Time.unscaledDeltaTime;
        if (kbRewards != null)
        {
            for (int i = kbRewards.Count - 1; i >= 0; i--)
            {
                var a = kbRewards[i];
                a.t += dt;
                kbRewards[i] = a;
                if (a.t >= a.dur)
                {
                    if (kbPlusOnes == null) kbPlusOnes = new List<KbPlus>();
                    kbPlusOnes.Add(new KbPlus { slot = a.slot, t = 0f, delta = 1 });
                    Sfx(sfxLevelUp);
                    Haptic(30);
                    kbRewards.RemoveAt(i);
                }
            }
        }
        if (kbPlusOnes != null)
        {
            for (int i = kbPlusOnes.Count - 1; i >= 0; i--)
            {
                var p = kbPlusOnes[i];
                p.t += dt;
                kbPlusOnes[i] = p;
                if (p.t >= 1.0f) kbPlusOnes.RemoveAt(i);
            }
        }
    }

    // Gambar animasi hadiah (ikon terbang + teks +1/-1). Dipanggil dari HUD Toko
    // (paling depan). Ikon TANPA bingkai/glow -> yang terlihat cuma item-nya.
    public void DrawRewardAnims()
    {
        if (kbRewards != null && kbRewards.Count > 0)
        {
            Vector2 scrCtr = new Vector2(VW * 0.5f, VH * 0.42f);
            float bigSize = Mathf.Min(VW, VH) * 0.34f;
            for (int i = 0; i < kbRewards.Count; i++)
            {
                var a = kbRewards[i];
                float p = a.dur > 0f ? Mathf.Clamp01(a.t / a.dur) : 1f;
                Rect slot = KbBuffSlotRect(a.slot);
                float smallSize = slot.width * 0.72f;
                Vector2 pos; float size;
                if (p < 0.32f)
                {
                    float q = p / 0.32f;
                    float pop = 1f - Mathf.Pow(1f - q, 3f);
                    size = bigSize * Mathf.LerpUnclamped(0.2f, 1.08f, pop);
                    pos = scrCtr;
                }
                else
                {
                    float q = (p - 0.32f) / 0.68f;
                    float e = q * q * (3f - 2f * q);
                    pos = Vector2.LerpUnclamped(scrCtr, slot.center, e);
                    size = Mathf.Lerp(bigSize * 1.08f, smallSize, e);
                }
                Rect ir = new Rect(pos.x - size * 0.5f, pos.y - size * 0.5f, size, size);
                DrawItemIcon(ir, a.type);
            }
        }
        if (kbPlusOnes != null && kbPlusOnes.Count > 0)
        {
            for (int i = 0; i < kbPlusOnes.Count; i++)
            {
                var pl = kbPlusOnes[i];
                float p = Mathf.Clamp01(pl.t / 1.0f);
                Rect slot = KbBuffSlotRect(pl.slot);
                float rise = 52f * p;
                float alpha = 1f - p;
                float bump = 1f - Mathf.Abs(0.5f - Mathf.Min(p, 0.5f)) * 2f;
                int size = Mathf.RoundToInt(30f + 14f * bump);
                Rect tr = new Rect(slot.center.x - 60f, slot.y - 10f - rise, 120f, 44f);
                string ptxt = pl.delta >= 0 ? "+" + pl.delta : pl.delta.ToString();
                Color pcol = pl.delta >= 0 ? new Color(1f, 0.95f, 0.55f, alpha) : new Color(1f, 0.55f, 0.5f, alpha);
                GuiText(tr, ptxt, size, pcol, TextAnchor.MiddleCenter);
            }
        }
    }

    // Setelah item meng-cascade papan: cek & hancurkan baris yg JADI penuh SAAT
    // ITU JUGA (tanpa menunggu balok aktif mendarat), termasuk reaksi berantai.
    IEnumerator ResolveClearsNoSpawn()
    {
        clearing = true;
        while (true)
        {
            var full = FindFullRows();
            if (full.Count == 0) break;
            yield return StartCoroutine(FlashClear(full));

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
        // Kalau item memicu naik babak (StageUp), papan di-reset & balok aktif
        // hilang -> spawn balok baru supaya game tetap jalan.
        if (!gameOver && active == null) SpawnPiece();
        clearing = false;
    }

    // ---- util: kumpulkan transform/skala/material/pusat dari daftar sel ----
    void GatherCells(List<Vector2Int> targets, out List<Transform> objs, out List<Vector3> baseScales, out List<Material> mats, out List<Vector3> centers)
    {
        objs = new List<Transform>();
        baseScales = new List<Vector3>();
        mats = new List<Material>();
        centers = new List<Vector3>();
        foreach (var t in targets)
        {
            GameObject go = cells[t.x, t.y];
            if (go == null) continue;
            objs.Add(go.transform);
            baseScales.Add(go.transform.localScale);
            centers.Add(go.transform.position);
            var rend = go.GetComponent<Renderer>();
            mats.Add(rend != null ? rend.material : null);
        }
    }

    // ========================= BOM (acak 1/2 kotak) =========================
    void ApplyBomb()
    {
        if (grid == null || cells == null) return;

        var all = new List<Vector2Int>();
        for (int c = 0; c < columns; c++)
            for (int r = 0; r < height; r++)
                if (cells[c, r] != null) all.Add(new Vector2Int(c, r));
        if (all.Count == 0) return;

        for (int i = all.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            var tmp = all[i]; all[i] = all[j]; all[j] = tmp;
        }
        int take = Mathf.Max(1, all.Count / 2);
        var targets = all.GetRange(0, take);

        StartCoroutine(BombBlast(targets));
    }

    IEnumerator BombBlast(List<Vector2Int> targets)
    {
        KbToast("BOOM!");
        KbEnsureItemSfx();

        List<Transform> objs; List<Vector3> baseScales; List<Material> mats; List<Vector3> centers;
        GatherCells(targets, out objs, out baseScales, out mats, out centers);
        int n = objs.Count;
        if (n == 0) yield break;

        float totalLightDur = Mathf.Clamp(n * 0.045f, 0.25f, 0.90f);
        float perStep = totalLightDur / n;
        for (int i = 0; i < n; i++)
        {
            KbSfxAt(kbSfxBombLit, 1f + i * 0.03f);
            if (objs[i] != null) objs[i].localScale = baseScales[i] * 1.20f;
            if (mats[i] != null && mats[i].HasProperty("_EmissionColor"))
                mats[i].SetColor("_EmissionColor", new Color(1f, 0.97f, 0.75f) * 3.4f);
            yield return new WaitForSeconds(perStep);
        }
        if (sfx != null) sfx.pitch = 1f;

        Shake(0.10f, 0.14f);
        yield return new WaitForSeconds(0.14f);

        KbSfxAt(kbSfxBombBoom, 1f);
        if (sfx != null) sfx.pitch = 1f;
        float dur = 0.20f;
        float t2 = 0f;
        while (t2 < dur)
        {
            t2 += Time.deltaTime;
            float p = Mathf.Clamp01(t2 / dur);
            float scaleMul = Mathf.Lerp(1.20f, 0.01f, p);
            for (int i = 0; i < n; i++)
            {
                if (objs[i] == null) continue;
                objs[i].localScale = baseScales[i] * scaleMul;
            }
            yield return null;
        }

        Shake(0.35f, 0.45f);
        Haptic(60);

        foreach (var pos in centers)
        {
            Burst(pos, new Color(1f, 0.55f, 0.15f));
            Burst(pos, new Color(1f, 0.85f, 0.35f));
        }

        foreach (var tr in objs)
            if (tr != null) Destroy(tr.gameObject);

        foreach (var t in targets)
        {
            if (t.x >= 0 && t.x < columns && t.y >= 0 && t.y < height)
            {
                cells[t.x, t.y] = null;
                grid[t.x, t.y] = -1;
            }
        }

        yield return StartCoroutine(CascadeGravity());
        yield return StartCoroutine(ResolveClearsNoSpawn());
    }

    // ========================= PALU (2 baris terbawah) =========================
    void ApplyHammer()
    {
        if (grid == null || cells == null) return;

        var topRow = new List<Vector2Int>();
        var botRow = new List<Vector2Int>();
        for (int c = 0; c < columns; c++)
        {
            if (height > 1 && cells[c, 1] != null) topRow.Add(new Vector2Int(c, 1));
            if (cells[c, 0] != null) botRow.Add(new Vector2Int(c, 0));
        }
        if (topRow.Count == 0 && botRow.Count == 0) return;
        StartCoroutine(HammerBlast(topRow, botRow));
    }

    void SetRingGlow(List<Transform> objs, List<Vector3> baseScales, List<Material> mats, Color col, bool on)
    {
        for (int i = 0; i < objs.Count; i++)
        {
            if (objs[i] != null) objs[i].localScale = baseScales[i] * (on ? 1.18f : 1f);
            if (mats[i] != null && mats[i].HasProperty("_EmissionColor"))
                mats[i].SetColor("_EmissionColor", on ? col * 3.2f : col * 0.4f);
        }
    }

    void ScaleRing(List<Transform> objs, List<Vector3> baseScales, float mul)
    {
        for (int i = 0; i < objs.Count; i++)
            if (objs[i] != null) objs[i].localScale = baseScales[i] * mul;
    }

    IEnumerator HammerBlast(List<Vector2Int> topRow, List<Vector2Int> botRow)
    {
        KbToast(SalID ? "Palu menghantam!" : "Hammer smash!");
        KbEnsureItemSfx();

        List<Transform> topT, botT;
        List<Vector3> topS, botS, topC, botC;
        List<Material> topM, botM;
        GatherCells(topRow, out topT, out topS, out topM, out topC);
        GatherCells(botRow, out botT, out botS, out botM, out botC);

        Color ringCol = new Color(1f, 0.85f, 0.35f);

        int cycles = 2;
        for (int cyc = 0; cyc < cycles; cyc++)
        {
            KbSfxAt(kbSfxHammerHit, 1.15f);
            SetRingGlow(topT, topS, topM, ringCol, true);
            SetRingGlow(botT, botS, botM, ringCol, false);
            Shake(0.06f, 0.10f);
            yield return new WaitForSeconds(0.16f);

            KbSfxAt(kbSfxHammerHit, 0.85f);
            SetRingGlow(topT, topS, topM, ringCol, false);
            SetRingGlow(botT, botS, botM, ringCol, true);
            Shake(0.06f, 0.10f);
            yield return new WaitForSeconds(0.16f);
        }
        if (sfx != null) sfx.pitch = 1f;

        SetRingGlow(topT, topS, topM, ringCol, true);
        SetRingGlow(botT, botS, botM, ringCol, true);
        yield return new WaitForSeconds(0.10f);

        KbSfxAt(kbSfxHammerHit, 0.65f);
        if (sfx != null) sfx.pitch = 1f;
        Shake(0.40f, 0.50f);
        Haptic(70);
        float dur = 0.18f;
        float t2 = 0f;
        while (t2 < dur)
        {
            t2 += Time.deltaTime;
            float p = Mathf.Clamp01(t2 / dur);
            float mul = Mathf.Lerp(1.18f, 0.01f, p);
            ScaleRing(topT, topS, mul);
            ScaleRing(botT, botS, mul);
            yield return null;
        }

        foreach (var pos in topC) Burst(pos, ringCol);
        foreach (var pos in botC) Burst(pos, new Color(1f, 0.6f, 0.2f));

        foreach (var tr in topT) if (tr != null) Destroy(tr.gameObject);
        foreach (var tr in botT) if (tr != null) Destroy(tr.gameObject);

        for (int c = 0; c < columns; c++)
        {
            if (height > 1) { cells[c, 1] = null; grid[c, 1] = -1; }
            cells[c, 0] = null; grid[c, 0] = -1;
        }

        yield return StartCoroutine(CascadeGravity());
        yield return StartCoroutine(ResolveClearsNoSpawn());
    }

    void ApplySlow()
    {
        if (kbSlowTimer <= 0f) kbSlowOrig = fallInterval;
        fallInterval = kbSlowOrig * SLOW_MULT;
        kbSlowTimer = SLOW_SECONDS;
        kbSlowTickAcc = 0f;
        Sfx(sfxRotate);
        KbToast(SalID ? "Perlambat aktif!" : "Slow active!");
    }

    public bool SlowActive { get { return kbSlowTimer > 0f; } }
    public float SlowSecondsLeft { get { return kbSlowTimer; } }

    public void DrawSlowVignette()
    {
        if (kbSlowTimer <= 0f) return;
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3.0f);
        float a = Mathf.Lerp(0.05f, 0.16f, pulse);
        Color c = new Color(0.22f, 0.52f, 1f, a);
        float edge = Mathf.Max(18f, VW * 0.035f);
        FillRect(new Rect(0f, 0f, VW, edge), c);
        FillRect(new Rect(0f, VH - edge, VW, edge), c);
        FillRect(new Rect(0f, 0f, edge, VH), c);
        FillRect(new Rect(VW - edge, 0f, edge, VH), c);
    }

    public void DrawSlowTimer()
    {
        if (kbSlowTimer <= 0f) return;

        Rect hsRect, gemRect, coinRect, pauseRect;
        GetHudRow(out hsRect, out gemRect, out coinRect, out pauseRect);

        float pvSize = Mathf.Min(VW * 0.22f, 132f);
        float pvX = VW - pvSize - 14f;
        float pvY = pauseRect.yMax + 14f;
        float boxH = pvSize + 40f;
        float boxBottom = (pvY - 6f) + boxH;

        float w = pvSize + 22f;
        float h = 50f;
        float x = pvX - 11f;
        float y = boxBottom + 10f;
        Rect r = new Rect(x, y, w, h);

        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f);
        RoundRect(new Rect(r.x - 3f, r.y - 3f, r.width + 6f, r.height + 6f), new Color(0.2f, 0.48f, 1f, 0.22f + 0.28f * pulse), h * 0.5f);
        RoundRect(r, new Color(0.06f, 0.08f, 0.14f, 0.92f), h * 0.5f);
        float ic = h * 0.72f;
        Rect ir = new Rect(r.x + (h - ic) * 0.5f + 4f, r.y + (h - ic) * 0.5f, ic, ic);
        DrawSlowIcon(ir);
        string txt = Mathf.CeilToInt(kbSlowTimer) + "s";
        Rect tr = new Rect(ir.xMax + 4f, r.y, r.xMax - (ir.xMax + 4f) - 8f, r.height);
        GuiText(tr, txt, 24, new Color(0.85f, 0.93f, 1f), TextAnchor.MiddleCenter);
    }

    string BubbleItemName(int t)
    {
        switch (t)
        {
            case IT_BOMB: return SalID ? "Bom" : "Bomb";
            case IT_LINE: return SalID ? "Palu" : "Hammer";
            case IT_SLOW: return SalID ? "Perlambat" : "Slow";
            case IT_GEM:  return SalID ? "Bonus Permata" : "Gem Bonus";
            case IT_COIN: return SalID ? "Koin" : "Coin";
        }
        return "";
    }
    string BubbleItemDesc(int t)
    {
        switch (t)
        {
            case IT_BOMB: return SalID ? "Hancurkan separuh kotak secara acak." : "Destroy half of the blocks at random.";
            case IT_LINE: return SalID ? "Hancurkan 2 baris terbawah." : "Smash the bottom 2 rows.";
            case IT_SLOW: return SalID ? "Balok jatuh lebih lambat." : "Blocks fall slower.";
            case IT_GEM:  return "+" + GEM_BONUS + (SalID ? " Permata." : " Gems.");
            case IT_COIN: return SalID ? "Koin masuk ke poin SALDOKU." : "Coins go to your SALDOKU points.";
        }
        return "";
    }
}

[DefaultExecutionOrder(-25000)]
public class KubikaBubbleHUD : MonoBehaviour
{
    Tetris3D game;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        var go = new GameObject("KubikaBubbleHUD");
        DontDestroyOnLoad(go);
        go.AddComponent<KubikaBubbleHUD>();
    }

    void FindGame()
    {
        if (game == null) game = Object.FindFirstObjectByType<Tetris3D>();
    }

    void Update()
    {
        FindGame();
        if (game != null) game.BubbleTick();
    }

    void OnGUI()
    {
        if (Tetris3D.AdFullscreenShowing) return; // iklan fullscreen -> HUD off (iklan di depan)
        FindGame();
        if (game == null) return;
        game.ApplyUiScale();
        GUI.depth = -800;
        if (game.SlowActive) game.DrawSlowVignette();
        if (game.BubblesVisible) game.DrawBubbles();
        if (game.BubbleClaimOpen) game.DrawBubbleClaim();
        if (game.SlowActive && !game.BubbleClaimOpen) game.DrawSlowTimer();
    }
}

public class KubikaExtraAds : MonoBehaviour
{
    public const int MODE_BUFF = 0;
    public const int MODE_DROP = 1;

    const string AD_UNIT_BUFF = "ca-app-pub-3186700509396792/3839606372";
    const string AD_UNIT_DROP = "ca-app-pub-3186700509396792/6949035774";
    const string AD_UNIT_TEST = "ca-app-pub-3940256099942544/5224354917";
    const bool   USE_TEST_ADS = false;

    static KubikaExtraAds _inst;
    public static KubikaExtraAds Instance
    {
        get
        {
            if (_inst == null)
            {
                var go = new GameObject("KubikaExtraAds");
                DontDestroyOnLoad(go);
                _inst = go.AddComponent<KubikaExtraAds>();
            }
            return _inst;
        }
    }

#if KUBIKA_ADMOB
    RewardedAd _ad;
    bool _init, _wantShow, _rewarded;
    Tetris3D _game;
    string _custom, _unit;
    System.Action _cb;

    string Unit(string prod) { return USE_TEST_ADS ? AD_UNIT_TEST : prod; }

    void EnsureInit()
    {
        if (_init) return;
        _init = true;
        MobileAds.Initialize(_ => Load());
    }

    void Load()
    {
        if (_ad != null) { _ad.Destroy(); _ad = null; }
        string u = string.IsNullOrEmpty(_unit) ? Unit(AD_UNIT_BUFF) : _unit;
        RewardedAd.Load(u, new AdRequest(), (ad, err) =>
        {
            if (err != null || ad == null)
            {
                if (_wantShow)
                {
                    _wantShow = false;
                    if (_game != null) { _game.SetBubbleAdBusy(false); _game.OnBubbleAdUnavailable(_game.BubbleAdsOffMsg()); }
                }
                return;
            }
            _ad = ad;
            Hook(_ad);
            if (_wantShow) { _wantShow = false; DoShow(); }
        });
    }

    void Hook(RewardedAd ad)
    {
        ad.OnAdFullScreenContentOpened += () =>
        {
            Tetris3D.BeginAdFullscreen(); // iklan fullscreen tampil -> sembunyikan SEMUA HUD (iklan di depan)
        };
        ad.OnAdFullScreenContentClosed += () =>
        {
            Tetris3D.EndAdFullscreen(); // iklan tertutup -> HUD tampil lagi
            if (_game != null) _game.SetBubbleAdBusy(false);
            Load();
        };
        ad.OnAdFullScreenContentFailed += (AdError e) =>
        {
            Tetris3D.EndAdFullscreen(); // iklan gagal tampil -> pastikan HUD tampil lagi
            if (_game != null) { _game.SetBubbleAdBusy(false); _game.OnBubbleAdUnavailable(_game.BubbleAdsOffMsg()); }
            Load();
        };
    }

    void DoShow()
    {
        if (_ad == null || !_ad.CanShowAd()) { _wantShow = true; Load(); return; }
        if (!string.IsNullOrEmpty(_custom))
        {
            var ssv = new ServerSideVerificationOptions { CustomData = _custom };
            _ad.SetServerSideVerificationOptions(ssv);
        }
        _rewarded = false;
        _ad.Show(reward => { _rewarded = true; if (_cb != null) _cb(); });
    }

    public void Show(Tetris3D game, int mode, string customData, System.Action onReward)
    {
        _game = game; _custom = customData; _cb = onReward;
        _unit = (mode == MODE_DROP) ? Unit(AD_UNIT_DROP) : Unit(AD_UNIT_BUFF);
        game.SetBubbleAdBusy(true);
        EnsureInit();
        if (_ad != null && _ad.CanShowAd()) DoShow();
        else { _wantShow = true; Load(); }
    }
#else
    public void Show(Tetris3D game, int mode, string customData, System.Action onReward)
    {
#if UNITY_EDITOR
        if (onReward != null) onReward();
#else
        game.OnBubbleAdUnavailable(game.BubbleAdsOffMsg());
#endif
    }
#endif
}
