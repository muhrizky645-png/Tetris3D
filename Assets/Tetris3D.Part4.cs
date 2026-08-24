using UnityEngine;

public partial class Tetris3D
{
    // Ikon tangan penunjuk utk hint swipe (di-load dari Resources, opsional).
    Texture2D handTex;
    bool handTexTried;

    // ---------- UI (skor + tombol Android) ----------
    void FillRect(Rect r, Color col)
    {
        Color old = GUI.color;
        GUI.color = col;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = old;
    }

    // Kotak sudut melengkung (rounded) - pakai border-radius bawaan GUI.DrawTexture
    void RoundRect(Rect r, Color col, float radius)
    {
        radius = Mathf.Min(radius, Mathf.Min(r.width, r.height) * 0.5f);
        GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f,
            col, Vector4.zero, new Vector4(radius, radius, radius, radius));
    }

    // Teks tema: pakai font game (Thaleah) + outline hitam 8 arah biar cerah & kebaca
    void GuiText(Rect r, string t, int size, Color col, TextAnchor anchor)
    {
        GUIStyle st = new GUIStyle { fontSize = size, fontStyle = FontStyle.Bold, alignment = anchor };
        if (uiFont != null) st.font = uiFont;
        float o = Mathf.Max(1.5f, size * 0.07f);
        Color outline = new Color(0f, 0f, 0f, 0.92f * col.a);
        st.normal.textColor = outline;
        GUI.Label(new Rect(r.x - o, r.y, r.width, r.height), t, st);
        GUI.Label(new Rect(r.x + o, r.y, r.width, r.height), t, st);
        GUI.Label(new Rect(r.x, r.y - o, r.width, r.height), t, st);
        GUI.Label(new Rect(r.x, r.y + o, r.width, r.height), t, st);
        GUI.Label(new Rect(r.x - o, r.y - o, r.width, r.height), t, st);
        GUI.Label(new Rect(r.x + o, r.y - o, r.width, r.height), t, st);
        GUI.Label(new Rect(r.x - o, r.y + o, r.width, r.height), t, st);
        GUI.Label(new Rect(r.x + o, r.y + o, r.width, r.height), t, st);
        st.normal.textColor = col;
        GUI.Label(r, t, st);
    }

    // Teks satu lapis (buat lapisan glow) - juga pakai font game
    void GuiLabel(Rect r, string t, int size, Color col, TextAnchor anchor)
    {
        GUIStyle st = new GUIStyle { fontSize = size, fontStyle = FontStyle.Bold, alignment = anchor };
        if (uiFont != null) st.font = uiFont;
        st.normal.textColor = col;
        GUI.Label(r, t, st);
    }

    // Tombol melengkung - ada efek mendem pas ditekan
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
        // label (ukuran auto: gede ngikut tinggi tombol, ngecil otomatis biar muat lebar)
        int lblSize = Mathf.RoundToInt(Mathf.Clamp(fr.height * 0.60f, 24f, 70f));
        if (uiFont != null)
        {
            GUIStyle ms = new GUIStyle { fontStyle = FontStyle.Bold, font = uiFont };
            float maxW = fr.width * 0.90f;
            while (lblSize > 14) { ms.fontSize = lblSize; if (ms.CalcSize(new GUIContent(label)).x <= maxW) break; lblSize -= 2; }
        }
        GuiText(new Rect(fr.x, fr.y, fr.width, fr.height), label, lblSize, Color.white, TextAnchor.MiddleCenter);
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
        float cx = VW / 2f;
        float t = Time.time;

        // Latar (lebih cerah & tembus pandang biar tema menyenangkan)
        FillRect(new Rect(0f, 0f, VW, VH), new Color(0.06f, 0.03f, 0.16f, 0.42f));

        // Blok-blok hias melayang
        DrawMenuDeco(t);

        // Judul KUBIKA TOWER warna-warni pelangi + gerak naik-turun halus, '3D' besar di bawahnya
        float pulse = 0.75f + 0.25f * Mathf.Sin(t * 2.2f);
        float bob = Mathf.Sin(t * 1.6f) * 6f;
        RainbowTitle(new Rect(0f, VH * 0.17f + bob, VW, 110f), "KUBIKA TOWER", 80, pulse, t);
        RainbowTitle(new Rect(0f, VH * 0.17f + 104f + bob, VW, 150f), "3D", 130, pulse, t);

        // Garis pemisah bercahaya
        float lw = Mathf.Min(VW * 0.5f, 300f);
        RoundRect(new Rect(cx - lw / 2f, VH * 0.17f + 250f + bob, lw, 4f), new Color(0.4f, 0.9f, 1f, 0.55f), 2f);

        // Kartu skor tertinggi + mahkota (selebar panel peringkat biar gagah)
        float hw = Mathf.Min(VW * 0.94f, 520f);
        Rect hiCard = new Rect(cx - hw / 2f, VH * 0.40f, hw, 74f);
        RoundRect(new Rect(hiCard.x - 3f, hiCard.y - 3f, hiCard.width + 6f, hiCard.height + 6f), new Color(1f, 0.8f, 0.2f, 0.22f), 20f); // halo
        RoundRect(hiCard, new Color(0.10f, 0.08f, 0.03f, 0.85f), 18f);
        GuiText(new Rect(hiCard.x + 26f, hiCard.y, 220f, 74f), T("record"), 24, new Color(1f, 0.9f, 0.6f, 0.85f), TextAnchor.MiddleLeft);
        if (crownTex != null)
            GUI.DrawTexture(new Rect(hiCard.x + hw - 176f, hiCard.y + 19f, 44f, 38f), crownTex, ScaleMode.StretchToFill, true, 0f,
                new Color(1f, 0.85f, 0.28f), Vector4.zero, Vector4.zero);
        GuiText(new Rect(hiCard.x + hw - 128f, hiCard.y, 116f, 74f), "" + highScore, 42, new Color(1f, 0.9f, 0.45f), TextAnchor.MiddleLeft);

        // Tombol MAIN berdenyut + halo cahaya
        float bw = Mathf.Min(VW * 0.64f, 360f);
        float grow = 6f * (0.5f + 0.5f * Mathf.Sin(t * 3f));
        Rect btn = new Rect(cx - bw / 2f - grow, VH * 0.53f - grow, bw + grow * 2f, 122f + grow * 2f);
        RoundRect(new Rect(btn.x - 6f, btn.y - 6f, btn.width + 12f, btn.height + 12f), new Color(0.2f, 1f, 0.55f, 0.22f), 30f); // halo
        if (Btn3D(btn, T("play"), new Color(0.20f, 0.82f, 0.46f), false))
            StartGame();

        // ---- Panel leaderboard global (Top 5) - di-tap buka full Top 50 ----
        if (ugsReady && !homeRanksRequested) { homeRanksRequested = true; LoadRanks(); }
        float lbw = Mathf.Min(VW * 0.94f, 520f);
        float lbx = cx - lbw / 2f;
        float lby = VH * 0.63f;
        float hdrH = 60f;
        int showN = Mathf.Min(5, ranks.Count);
        float lbRowH = 68f;
        float bodyH = (!ugsReady || ranksLoading || ranks.Count == 0) ? 78f : showN * (lbRowH + 4f);
        float panelH = hdrH + 8f + bodyH + 10f;

        RoundRect(new Rect(lbx - 3f, lby - 3f, lbw + 6f, panelH + 6f), new Color(1f, 0.82f, 0.25f, 0.18f), 20f);
        RoundRect(new Rect(lbx, lby, lbw, panelH), new Color(0.06f, 0.07f, 0.12f, 0.92f), 18f);
        RoundRect(new Rect(lbx + 10f, lby + 8f, lbw - 20f, hdrH - 6f), new Color(0.95f, 0.75f, 0.15f, 0.16f), 12f);
        if (crownTex != null)
            GUI.DrawTexture(new Rect(lbx + 22f, lby + 16f, 42f, 38f), crownTex, ScaleMode.StretchToFill, true, 0f, new Color(1f, 0.85f, 0.28f), Vector4.zero, Vector4.zero);
        GuiText(new Rect(lbx + 78f, lby + 8f, lbw - 220f, hdrH - 6f), T("rankings"), 34, new Color(1f, 0.9f, 0.55f), TextAnchor.MiddleLeft);
        GuiText(new Rect(lbx + lbw - 140f, lby + 8f, 128f, hdrH - 6f), T("viewAll"), 20, new Color(0.6f, 0.85f, 1f), TextAnchor.MiddleRight);

        float lY = lby + hdrH + 6f;
        if (!ugsReady || ranksLoading) GuiText(new Rect(lbx, lY, lbw, 66f), T("connecting"), 30, new Color(1f, 1f, 1f, 0.8f), TextAnchor.MiddleCenter);
        else if (ranks.Count == 0) GuiText(new Rect(lbx, lY, lbw, 66f), T("noScores"), 30, new Color(1f, 1f, 1f, 0.7f), TextAnchor.MiddleCenter);
        else
        {
            for (int i = 0; i < showN; i++)
            {
                var e = ranks[i];
                Rect rr = new Rect(lbx + 10f, lY + i * (lbRowH + 4f), lbw - 20f, lbRowH);
                Color rowCol = e.you ? new Color(0.20f, 0.55f, 0.42f, 0.95f) : (i < 3 ? new Color(0.18f, 0.16f, 0.28f, 0.95f) : new Color(0.10f, 0.12f, 0.18f, 0.9f));
                RoundRect(rr, rowCol, 10f);
                Color rankCol = i == 0 ? new Color(1f, 0.85f, 0.3f) : i == 1 ? new Color(0.82f, 0.86f, 0.92f) : i == 2 ? new Color(0.88f, 0.58f, 0.32f) : new Color(0.7f, 0.75f, 0.85f);
                GuiText(new Rect(rr.x + 14f, rr.y, 66f, lbRowH), "#" + e.rank, 34, rankCol, TextAnchor.MiddleLeft);
                string nm = string.IsNullOrEmpty(e.name) ? "-" : e.name;
                if (e.you) nm += "  (" + T("you") + ")";
                GuiText(new Rect(rr.x + 82f, rr.y + 7f, rr.width - 220f, 32f), nm, 28, Color.white, TextAnchor.LowerLeft);
                if (!string.IsNullOrEmpty(e.country))
                    GuiText(new Rect(rr.x + 82f, rr.y + 40f, rr.width - 220f, 22f), CountryName(e.country), 18, new Color(0.7f, 0.8f, 1f), TextAnchor.UpperLeft);
                GuiText(new Rect(rr.xMax - 165f, rr.y, 152f, lbRowH), "" + e.score, 34, new Color(0.6f, 1f, 0.75f), TextAnchor.MiddleRight);
            }
        }
        if (GUI.Button(new Rect(lbx, lby, lbw, panelH), GUIContent.none, GUIStyle.none)) { showRanks = true; LoadRanks(); }

        // Hint berkedip (di bawah panel leaderboard)
        float ha = 0.55f + 0.45f * Mathf.Sin(t * 3f);
        GuiText(new Rect(0f, lby + panelH + 14f, VW, 30f), T("pressPlay"), 20, new Color(1f, 1f, 1f, ha), TextAnchor.MiddleCenter);

        // Chip profil (pojok kiri atas) - tap buat edit nama & negara kapan aja
        string pf = string.IsNullOrEmpty(playerName) ? T("setProfile") : (playerName + "  \u00b7  " + playerCountry);
        float pcw = Mathf.Min(VW * 0.52f, 240f);
        if (Btn3D(new Rect(16f, 16f, pcw, 46f), pf, new Color(0.30f, 0.40f, 0.62f), false))
        { editingProfile = true; showProfile = true; countryPicking = false; lbStatus = ""; }

        // Pemilih bahasa (pojok kanan atas)
        DrawLangPicker(VW - 96f - 16f, 16f);
    }

    // Teks dengan efek glow (lapisan glow pakai GuiLabel, teks inti pakai GuiText beroutline)
    void GlowText(Rect r, string s, int size, Color col, float glow)
    {
        Color g = new Color(col.r, col.g, col.b, 0.16f * glow);
        for (int i = 0; i < 4; i++)
        {
            float o = 2f + i * 1.6f;
            GuiLabel(new Rect(r.x - o, r.y, r.width, r.height), s, size, g, TextAnchor.MiddleCenter);
            GuiLabel(new Rect(r.x + o, r.y, r.width, r.height), s, size, g, TextAnchor.MiddleCenter);
            GuiLabel(new Rect(r.x, r.y - o, r.width, r.height), s, size, g, TextAnchor.MiddleCenter);
            GuiLabel(new Rect(r.x, r.y + o, r.width, r.height), s, size, g, TextAnchor.MiddleCenter);
        }
        GuiText(r, s, size, col, TextAnchor.MiddleCenter);
    }

    // Judul warna-warni: tiap huruf beda warna pelangi, animasi hue jalan
    void RainbowTitle(Rect r, string s, int size, float glow, float t)
    {
        GUIStyle st = new GUIStyle { fontSize = size, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
        if (uiFont != null) st.font = uiFont;
        float total = 0f;
        float[] widths = new float[s.Length];
        for (int i = 0; i < s.Length; i++)
        {
            widths[i] = st.CalcSize(new GUIContent(s[i].ToString())).x;
            total += widths[i];
        }
        float x = r.x + (r.width - total) / 2f;
        for (int i = 0; i < s.Length; i++)
        {
            Rect cr = new Rect(x, r.y, widths[i] + 6f, r.height);
            float hue = ((float)i / Mathf.Max(1, s.Length) + t * 0.12f) % 1f;
            Color col = Color.HSVToRGB(hue, 0.7f, 1f);
            Color g = new Color(col.r, col.g, col.b, 0.16f * glow);
            string ch = s[i].ToString();
            for (int k = 0; k < 3; k++)
            {
                float o = 2f + k * 1.6f;
                GuiLabel(new Rect(cr.x - o, cr.y, cr.width, cr.height), ch, size, g, TextAnchor.MiddleLeft);
                GuiLabel(new Rect(cr.x + o, cr.y, cr.width, cr.height), ch, size, g, TextAnchor.MiddleLeft);
                GuiLabel(new Rect(cr.x, cr.y - o, cr.width, cr.height), ch, size, g, TextAnchor.MiddleLeft);
                GuiLabel(new Rect(cr.x, cr.y + o, cr.width, cr.height), ch, size, g, TextAnchor.MiddleLeft);
            }
            GuiText(cr, ch, size, col, TextAnchor.MiddleLeft);
            x += widths[i];
        }
    }

    // Blok warna-warni melayang naik di latar menu
    void DrawMenuDeco(float t)
    {
        int n = 7;
        float sz = Mathf.Max(26f, VW * 0.045f);
        for (int i = 0; i < n; i++)
        {
            float fx = (i + 0.5f) / n;
            float px = fx * VW + Mathf.Sin(t * 0.6f + i * 1.3f) * 22f;
            float speed = 38f + (i % 3) * 20f;
            float py = VH - Mathf.Repeat(t * speed + i * 150f, VH + 140f);
            Color c = BlockColor(i % 6);
            c.a = 0.35f;
            RoundRect(new Rect(px - sz / 2f, py, sz, sz), c, sz * 0.28f);
            RoundRect(new Rect(px - sz / 2f + sz * 0.15f, py + sz * 0.12f, sz * 0.7f, sz * 0.28f), new Color(1f, 1f, 1f, 0.15f), sz * 0.14f);
        }
    }

    // ---- Menu jeda (pause) ----
    void DrawPauseMenu()
    {
        float cx = VW / 2f;
        FillRect(new Rect(0f, 0f, VW, VH), new Color(0.02f, 0.01f, 0.06f, 0.72f));
        GlowText(new Rect(0f, VH * 0.30f, VW, 90f), T("pause"), 72, new Color(0.5f, 0.85f, 1f), 1f);

        float bw = Mathf.Min(VW * 0.64f, 360f);
        float bx = cx - bw / 2f;
        float by = VH * 0.40f;
        float bh = 84f, gap = 15f;

        if (Btn3D(new Rect(bx, by, bw, bh), T("resume"), new Color(0.20f, 0.82f, 0.46f), false)) paused = false;
        by += bh + gap;
        if (Btn3D(new Rect(bx, by, bw, bh), T("restart"), new Color(0.95f, 0.70f, 0.20f), false)) RestartGameFull();
        by += bh + gap;
        if (Btn3D(new Rect(bx, by, bw, bh), T("mainmenu"), new Color(0.88f, 0.35f, 0.42f), false)) GoHomeFull();
        by += bh + gap + 12f;

        // Toggle on/off suara
        sfxOn = DrawToggle(new Rect(bx, by, bw, 58f), T("sfx"), sfxOn);
        by += 58f + gap;
        musicOn = DrawToggle(new Rect(bx, by, bw, 58f), T("music"), musicOn);
        by += 58f + gap;

        // Getar (haptic) on/off
        bool newHaptic = DrawToggle(new Rect(bx, by, bw, 58f), T("haptic"), hapticOn);
        if (newHaptic != hapticOn) { hapticOn = newHaptic; SaveHaptic(); }
        by += 58f + gap;

        // Slider sensitivitas geser (kiri: Santai, kanan: Sensitif)
        float sVal = Mathf.InverseLerp(0.14f, 0.05f, dragStep);
        float sNew = DrawSlider(new Rect(bx, by, bw, 74f), T("sens"), sVal, T("sensLow"), T("sensHigh"));
        if (Mathf.Abs(sNew - sVal) > 0.0001f) { dragStep = Mathf.Lerp(0.14f, 0.05f, sNew); SaveDragStep(); }

        // Pemilih bahasa (pojok kanan atas)
        DrawLangPicker(VW - 96f - 16f, 16f);
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
        float cx = VW / 2f;
        FillRect(new Rect(0f, 0f, VW, VH), new Color(0.02f, 0.01f, 0.06f, 0.93f));
        GlowText(new Rect(0f, VH * 0.09f, VW, 80f), T("profileTitle"), 50, new Color(0.5f, 0.9f, 1f), 1f);

        if (editingProfile && Btn3D(new Rect(16f, 16f, 120f, 48f), T("close"), new Color(0.55f, 0.42f, 0.55f), false))
        { editingProfile = false; showProfile = false; countryPicking = false; lbStatus = ""; }

        float pw = Mathf.Min(VW * 0.82f, 470f);
        float px = cx - pw / 2f;
        float py = VH * 0.20f;

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
            float listH = VH * 0.44f;
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
        float cx = VW / 2f;
        FillRect(new Rect(0f, 0f, VW, VH), new Color(0.02f, 0.01f, 0.06f, 0.95f));
        GlowText(new Rect(0f, VH * 0.05f, VW, 80f), T("rankings"), 54, new Color(1f, 0.82f, 0.3f), 1f);

        float pw = Mathf.Min(VW * 0.9f, 540f);
        float px = cx - pw / 2f;
        float py = VH * 0.16f;

        if (ranksLoading) { GuiText(new Rect(0f, VH * 0.45f, VW, 40f), T("loading"), 30, Color.white, TextAnchor.MiddleCenter); }
        else if (!string.IsNullOrEmpty(lbStatus)) { GuiText(new Rect(0f, VH * 0.45f, VW, 40f), lbStatus, 24, new Color(1f, 0.8f, 0.4f), TextAnchor.MiddleCenter); }
        else if (ranks.Count == 0) { GuiText(new Rect(0f, VH * 0.45f, VW, 40f), T("noScores"), 26, new Color(1f, 1f, 1f, 0.8f), TextAnchor.MiddleCenter); }
        else
        {
            float rowH = 64f;
            float listTop = VH * 0.15f;
            float listH = VH * 0.66f;
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
                GuiText(new Rect(rr.x + 14f, rr.y, 68f, rowH), "#" + e.rank, 30, rankCol, TextAnchor.MiddleLeft);
                string nm = string.IsNullOrEmpty(e.name) ? "-" : e.name;
                if (e.you) nm += "  (" + T("you") + ")";
                GuiText(new Rect(rr.x + 88f, rr.y + 8f, content.width - 240f, 32f), nm, 26, Color.white, TextAnchor.LowerLeft);
                if (!string.IsNullOrEmpty(e.country))
                    GuiText(new Rect(rr.x + 88f, rr.y + rowH - 24f, content.width - 240f, 22f), CountryName(e.country), 17, new Color(0.7f, 0.8f, 1f), TextAnchor.UpperLeft);
                GuiText(new Rect(rr.width - 160f, rr.y, 146f, rowH), "" + e.score, 30, new Color(0.6f, 1f, 0.75f), TextAnchor.MiddleRight);
            }
            GUI.EndScrollView();
            string mine = myRank > 0 ? T("yourRank") + "  #" + myRank : T("unranked");
            GuiText(new Rect(px, VH * 0.82f, pw, 34f), mine, 28, new Color(1f, 0.9f, 0.5f), TextAnchor.MiddleCenter);
        }

        float bw = Mathf.Min(VW * 0.6f, 300f);
        if (Btn3D(new Rect(cx - bw / 2f, VH * 0.88f, bw, 66f), T("close"), new Color(0.88f, 0.35f, 0.42f), false))
        { showRanks = false; countryPicking = false; }
    }

    // Tata letak baris HUD atas (ala Block Blast): skor tertinggi | permata | koin | jeda
    // rowY diturunkan otomatis di bawah kamera depan / notch (safe area).
    void GetHudRow(out Rect hsRect, out Rect gemRect, out Rect coinRect, out Rect pauseRect)
    {
        float pad = 14f, rowY = 16f + SafeTopLogical(), rowH = 60f, gap = 12f;
        float pauseW = 92f;
        pauseRect = new Rect(VW - pad - pauseW, rowY, pauseW, rowH);
        float hsW = 172f;
        hsRect = new Rect(pad, rowY, hsW, rowH);
        float midStart = hsRect.xMax + gap;
        float midEnd = pauseRect.x - gap;
        float chipW = (midEnd - midStart - gap) / 2f;
        gemRect = new Rect(midStart, rowY, chipW, rowH);
        coinRect = new Rect(gemRect.xMax + gap, rowY, chipW, rowH);
    }

    void OnGUI()
    {
        // Skala UI responsif: semua UI digambar di ruang logis 720px lalu
        // diskalakan ke lebar layar asli. Pakai VW/VH (bukan Screen.width/height).
        ApplyUiScale();

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

        // Overlay PERINGKAT (leaderboard global) - bisa dibuka dari menu depan / game over
        if (showRanks) { DrawRanksScreen(); return; }

        // Overlay Buat Profil - muncul saat game over pertama sebelum skor dikirim
        if (showProfile) { DrawProfileScreen(); return; }

        // Menu depan (tampilan pembuka) - gambar & stop di sini sampai tekan MAIN
        if (!started) { DrawStartMenu(); return; }

        // Menu jeda - gambar & stop di sini selagi paused
        if (paused) { DrawPauseMenu(); return; }

        // ---- HUD atas ala Block Blast: [skor tertinggi] [permata] [koin] [jeda] ----
        // Permata & koin digambar komponen terpisah (KubikaCurrencyHUD) di gemRect/coinRect.
        Rect hsRect, gemRect, coinRect, pauseRect;
        GetHudRow(out hsRect, out gemRect, out coinRect, out pauseRect);

        // Chip skor tertinggi (kiri) - mahkota + angka
        RoundRect(new Rect(hsRect.x - 3f, hsRect.y - 3f, hsRect.width + 6f, hsRect.height + 6f), new Color(1f, 0.8f, 0.2f, 0.22f), 20f);
        RoundRect(hsRect, new Color(0.06f, 0.08f, 0.12f, 0.92f), 18f);
        if (crownTex != null)
            GUI.DrawTexture(new Rect(hsRect.x + 14f, hsRect.y + (hsRect.height - 32f) / 2f, 38f, 32f), crownTex, ScaleMode.StretchToFill, true, 0f,
                new Color(1f, 0.85f, 0.28f), Vector4.zero, Vector4.zero);
        GuiText(new Rect(hsRect.x + 60f, hsRect.y, hsRect.width - 70f, hsRect.height), "" + highScore, 32, new Color(1f, 0.9f, 0.45f), TextAnchor.MiddleLeft);

        // Skor besar di tengah (fokus utama, mirip Block Blast)
        float bigScoreY = hsRect.yMax + 12f;
        GuiText(new Rect(0f, bigScoreY, VW, 100f), "" + score, 88, Color.white, TextAnchor.MiddleCenter);
        GuiText(new Rect(0f, bigScoreY + 96f, VW, 26f), T("lines") + " " + lines + "   " + T("lvl") + " " + level + "   " + T("cols") + " " + columns, 20, new Color(0.80f, 0.92f, 1f), TextAnchor.MiddleCenter);

        // Teks LEVEL UP! muncul sebentar tiap naik level
        if (levelUpTime > 0f && !gameOver)
        {
            float la = Mathf.Clamp01(levelUpTime / 1.4f);
            GlowText(new Rect(0f, VH * 0.24f, VW, 84f), T("level") + " " + level + "!", 58, new Color(1f, 0.86f, 0.32f, la), la);
        }

        // Teks COMBO! putih besar (seperti judul) + halo warna di belakang biar pop tapi tetap kebaca saat main
        if (comboTime > 0f && !gameOver)
        {
            float ca = Mathf.Clamp01(comboTime / 1.3f);
            Color glowCol = Color.HSVToRGB((Time.time * 0.7f + comboShow * 0.13f) % 1f, 0.85f, 1f);
            glowCol.a = ca;
            int csize = 100 + Mathf.Min(comboShow, 8) * 4;
            Rect comboRect = new Rect(0f, VH * 0.31f, VW, 150f);
            GlowText(comboRect, "COMBO x" + comboShow, csize, glowCol, ca);
            GuiText(comboRect, "COMBO x" + comboShow, csize, new Color(1f, 1f, 1f, ca), TextAnchor.MiddleCenter);
        }

        if (gameOver)
        {
            if (score > highScore) { highScore = score; PlayerPrefs.SetInt("tetris3d_hi", highScore); PlayerPrefs.Save(); }
            // Tawaran REVIVE dulu (hitung mundur + tonton iklan) sebelum layar game over biasa.
            if (reviveOffer) { DrawReviveOffer(); return; }
            FillRect(new Rect(0f, VH * 0.28f, VW, VH * 0.26f), new Color(0f, 0f, 0f, 0.6f));
            GuiText(new Rect(0f, VH * 0.30f, VW, 90f), "GAME OVER", 70, new Color(1f, 0.35f, 0.35f), TextAnchor.MiddleCenter);
            if (Btn3D(new Rect(VW / 2f - 150f, VH * 0.5f, 300f, 88f), T("playAgain"), new Color(0.20f, 0.80f, 0.45f), false)) RestartGameFull();
            if (Btn3D(new Rect(VW / 2f - 150f, VH * 0.5f + 100f, 300f, 72f), T("rankings"), new Color(0.30f, 0.55f, 0.95f), false)) { showRanks = true; LoadRanks(); }
            return;
        }

        // Tombol JEDA / pengaturan (pojok kanan baris atas)
        if (Btn3D(pauseRect, T("pause"), new Color(0.30f, 0.55f, 0.95f), false)) paused = true;

        float bw = Mathf.Min(VW * 0.20f, 168f);
        float bh = bw;
        float pad = 16f;
        float y = VH - bh - pad;

        if (Btn3D(new Rect(pad, y, bw, bh), T("rotate"), new Color(0.16f, 0.78f, 0.40f), false)) Rotate();
        if (Btn3D(new Rect(VW / 2f - bw / 2f, y, bw, bh), T("drop"), new Color(0.10f, 0.62f, 0.32f), false)) HardDrop();
        if (Btn3D(new Rect(VW - bw - pad, y, bw, bh), T("down"), new Color(0.22f, 0.85f, 0.48f), true)) btnSoftDrop = true; // tahan buat turun cepat

        // ---- Kotak preview: bentuk balok BERIKUTNYA (di bawah tombol Jeda) ----
        {
            float pvSize = Mathf.Min(VW * 0.22f, 132f);
            float pvX = VW - pvSize - 14f;
            float pvY = pauseRect.yMax + 14f;
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

        // ---- Tutorial PUTAR TABUNG (besar, di tengah) - hilang setelah sentuhan pertama ----
        if (!hintDone)
        {
            float cxc = VW / 2f;
            float cyc = VH * 0.5f;
            FillRect(new Rect(0f, 0f, VW, VH), new Color(0f, 0f, 0f, 0.35f)); // redupin layar

            float tubeW = Mathf.Min(VW * 0.34f, 360f);
            float tubeH = tubeW * 0.32f;
            Rect tube = new Rect(cxc - tubeW / 2f, cyc - tubeH / 2f, tubeW, tubeH);

            // Tangan penunjuk: geser kiri-kanan, sedikit miring, sampai pemain
            // menyentuh layar (hintDone). Kalau tekstur tangan belum di-import,
            // pakai panah lama sebagai cadangan biar tetap ada petunjuk.
            if (handTex == null && !handTexTried) { handTex = Resources.Load<Texture2D>("KubikaIcons/Hand_A"); handTexTried = true; }
            if (handTex != null)
            {
                float swing = Mathf.Sin(Time.time * 2.4f);   // -1..1 : geser kiri-kanan
                float travel = Mathf.Min(VW * 0.34f, 250f);  // jarak geser kanan-kiri (lebih lebar)
                float hsz = tubeH * 2.0f;
                float hx = cxc + swing * travel;             // hanya X yang berubah -> murni kanan-kiri
                float hy = cyc - hsz * 0.6f;                 // ketinggian tetap (tidak serong)
                Rect handR = new Rect(hx - hsz / 2f, hy, hsz, hsz);
                float tilt = 0f;                             // tegak lurus -> gerak murni kanan-kiri (tidak serong)
                Matrix4x4 mtx = GUI.matrix;
                GUIUtility.RotateAroundPivot(tilt, handR.center);
                GUI.DrawTexture(handR, handTex, ScaleMode.ScaleToFit, true);
                GUI.matrix = mtx;
            }
            else if (triTex != null)
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

            GuiText(new Rect(0f, cyc + tubeH, VW, 60f), T("swipeBig"), 34, Color.white, TextAnchor.MiddleCenter);
            GuiText(new Rect(0f, cyc + tubeH + 56f, VW, 34f), T("touchStart"), 20, new Color(1f, 1f, 1f, 0.8f), TextAnchor.MiddleCenter);
        }
    }
}
