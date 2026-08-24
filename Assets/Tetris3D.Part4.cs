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
                Color rowCol = e.you ? new