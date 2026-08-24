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
using Random = UnityEngine.Random;
using Object = UnityEngine.Object;

public partial class Tetris3D
{
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
        sfxClear = MakeArp("clr", new float[] { 523.25f, 659.25f, 783.99f, 1046.50f, 1318.51f, 1567.98f }, 0.07f, 0.55f);
        sfxGameOver = MakeArp("go", new float[] { 587.33f, 493.88f, 392.00f, 293.66f, 261.63f }, 0.16f, 0.55f);
        sfxLevelUp = MakeArp("lvl", new float[] { 523.25f, 659.25f, 783.99f, 1046.50f, 1318.51f }, 0.10f, 0.55f);
        sfxTick = MakeTone("tick", 950f, 0.05f, 0.5f, 0, 950f);

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
            if (wave == 1) s = Mathf.Sign(Mathf.Sin(phase)) * 0.6f + Mathf.Sin(phase2) * 0.2f;
            else if (wave == 2) s = (Mathf.PingPong(phase / Mathf.PI, 1f) * 2f - 1f) * 0.7f + Mathf.Sin(phase3) * 0.2f;
            else s = Mathf.Sin(phase) * 0.8f + Mathf.Sin(phase2) * 0.25f + Mathf.Sin(phase3) * 0.12f;
            float atk = Mathf.Min(1f, t / 0.008f);
            float dec = Mathf.Exp(-3.2f * t);
            data[i] = Mathf.Clamp(s * atk * dec * vol, -1f, 1f);
        }
        AudioClip clip = AudioClip.Create(name, n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    // Arpeggio meriah: nada beruntun + ekor bergema (overlap antar nada) + sparkle oktaf biar terasa perayaan
    AudioClip MakeArp(string name, float[] freqs, float noteDur, float vol)
    {
        int rate = 44100;
        int nPer = Mathf.Max(1, (int)(rate * noteDur));
        int tail = (int)(rate * 0.34f);               // ekor gema biar tiap nada nyambung & rame
        int total = nPer * freqs.Length + tail;
        float[] data = new float[total];
        for (int k = 0; k < freqs.Length; k++)
        {
            float f = freqs[k];
            int start = k * nPer;
            int len = nPer + tail;                    // tiap nada dibiarkan bergema (overlap)
            for (int i = 0; i < len && start + i < total; i++)
            {
                float t = i / (float)len;
                float tt = i / (float)rate;
                float s = Mathf.Sin(2f * Mathf.PI * f * tt) * 0.6f
                        + Mathf.Sin(2f * Mathf.PI * f * 2.01f * tt) * 0.30f
                        + Mathf.Sin(2f * Mathf.PI * f * 3f * tt) * 0.18f
                        + Mathf.Sin(2f * Mathf.PI * f * 4f * tt) * 0.10f    // sparkle oktaf atas biar cerah
                        + Mathf.Sin(2f * Mathf.PI * f * 0.5f * tt) * 0.22f; // bass biar berisi
                float env = Mathf.Min(1f, t / 0.006f) * Mathf.Exp(-3.0f * t);
                data[start + i] = Mathf.Clamp(data[start + i] + s * env * vol * 0.7f, -1f, 1f);
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
        // Pusatkan tabung pada TITIK TENGAH KOLOM YANG BENAR-BENAR TERISI
        // (bukan tengah kotak pembungkus), biar balok yang bentuknya tidak
        // simetris (garis tegak, L, dsb.) tetap fokus pas di depan/tengah.
        int minx = int.MaxValue, maxx = int.MinValue;
        if (curBox != null)
        {
            for (int i = 0; i < curBox.Length; i++)
            {
                if (curBox[i].x < minx) minx = curBox[i].x;
                if (curBox[i].x > maxx) maxx = curBox[i].x;
            }
        }
        float mid = (minx <= maxx) ? (minx + maxx) * 0.5f : (curN - 1) * 0.5f;
        float centerCol = curCol + mid;
        targetSpin = 180f - 360f * centerCol / columns;
    }

    // ---------- LOOP ----------
    void Update()
    {
        LoadExtrasPrefs();
        if (extrasToastTime > 0f) extrasToastTime -= Time.deltaTime;

        // Getar (haptic) berbasis perubahan: line clear (baris nambah) & game over
        if (lines > prevLines) Haptic(30);
        prevLines = lines;
        if (gameOver && !prevGameOver) Haptic(120);
        prevGameOver = gameOver;

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
            // Tawaran REVIVE (maks 1x): 5 detik hitung mundur + SFX detikan sebelum benar-benar tamat.
            if (!reviveUsed && !reviveDeclined && !reviveOffer && !gameOverHandled)
            {
                reviveOffer = true;
                reviveTimer = REVIVE_SECONDS;
                reviveTickAcc = 0f;
            }
            if (reviveOffer)
            {
                reviveTimer -= Time.deltaTime;
                reviveTickAcc += Time.deltaTime;
                if (reviveTickAcc >= 1f) { reviveTickAcc -= 1f; Sfx(sfxTick); Haptic(25); }
                // Waktu habis sendiri = sama persis dengan menekan LEWATI (DeclineRevive):
                // reset animasi skor biar count-up TETAP jalan, bukan langsung lompat ke angka akhir.
                if (reviveTimer <= 0f) DeclineRevive();
                return;
            }

            if (!gameOverHandled)
            {
                gameOverHandled = true;
                if (!profileDone) showProfile = true;
                else SubmitScore();
            }
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame) RestartGameFull();
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

        // Tabung selalu ikut memusatkan balok aktif (termasuk SETELAH ROTATE), biar tetap fokus di tengah.
        if (active != null && !clearing && !gameOver) UpdateTargetSpin();

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

    // Kirim/ubah nama pemain di UGS (dipakai pas edit profil dari menu).
    // PENTING: leaderboard menyimpan nama SAAT skor dikirim (snapshot). Jadi kalau cuma ganti
    // nama tanpa main lagi, baris skor lama tetap pakai nama lama. Solusi: langsung KIRIM ULANG
    // skor terbaik supaya baris di papan ikut ke-update dengan nama baru seketika, lalu refresh.
    async void PushName()
    {
        if (!ugsReady || string.IsNullOrEmpty(playerName)) return;
        try
        {
            lbStatus = T("sending");
            await AuthenticationService.Instance.UpdatePlayerNameAsync(playerName);
            if (highScore > 0)
            {
                var opt = new AddPlayerScoreOptions { Metadata = new Dictionary<string, object> { { "country", playerCountry } } };
                await LeaderboardsService.Instance.AddPlayerScoreAsync(LB_ID, highScore, opt);
            }
            lbStatus = "";
        }
        catch (Exception e) { lbStatus = "Err: " + e.Message; }
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
        int q1 = meta.IndexOf('\"', k + 9);
        if (q1 < 0) return "";
        int q2 = meta.IndexOf('\"', q1 + 1);
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
            { "record",     new[]{ "BEST", "REKOR", "R\u00c9CORD", "RECORDE", "RECORD" } },
            { "play",       new[]{ "PLAY", "MAIN", "JUGAR", "JOGAR", "JOUER" } },
            { "pressPlay",  new[]{ "Press PLAY to start", "Tekan MAIN untuk mulai", "Pulsa JUGAR para empezar", "Toque JOGAR para come\u00e7ar", "Appuyez sur JOUER" } },
            { "swipeHint",  new[]{ "Swipe = rotate tube", "Geser layar = putar tabung", "Desliza = girar el tubo", "Arraste = girar o tubo", "Glissez = tourner le tube" } },
            { "ctrlHint",   new[]{ "ROTATE  \u2022  DOWN  \u2022  DROP", "ROTASI  \u2022  TURUN  \u2022  JATUH", "GIRAR  \u2022  BAJAR  \u2022  CAER", "GIRAR  \u2022  DESCER  \u2022  SOLTAR", "TOURNER  \u2022  BAS  \u2022  L\u00c2CHER" } },
            { "pause",      new[]{ "PAUSE", "JEDA", "PAUSA", "PAUSA", "PAUSE" } },
            { "resume",     new[]{ "RESUME", "LANJUT", "CONTINUAR", "CONTINUAR", "REPRENDRE" } },
            { "restart",    new[]{ "RESTART", "ULANG", "REINICIAR", "REINICIAR", "RECOMMENCER" } },
            { "mainmenu",   new[]{ "MAIN MENU", "KE MENU", "MEN\u00da", "MENU", "MENU" } },
            { "sfx",        new[]{ "Sound FX", "Efek Suara (SFX)", "Efectos (SFX)", "Efeitos (SFX)", "Effets (SFX)" } },
            { "music",      new[]{ "Music", "Musik", "M\u00fasica", "M\u00fasica", "Musique" } },
            { "score",      new[]{ "SCORE", "SKOR", "PUNTOS", "PONTOS", "SCORE" } },
            { "lines",      new[]{ "Lines", "Baris", "L\u00edneas", "Linhas", "Lignes" } },
            { "lvl",        new[]{ "Lv", "Lv", "Nv", "Nv", "Niv" } },
            { "cols",       new[]{ "Cols", "Kolom", "Cols", "Cols", "Cols" } },
            { "level",      new[]{ "LEVEL", "LEVEL", "NIVEL", "N\u00cdVEL", "NIVEAU" } },
            { "playAgain",  new[]{ "PLAY AGAIN", "MAIN LAGI", "JUGAR OTRA VEZ", "JOGAR DE NOVO", "REJOUER" } },
            { "rotate",     new[]{ "ROTATE", "ROTASI", "GIRAR", "GIRAR", "TOURNER" } },
            { "drop",       new[]{ "DROP", "JATUH", "CAER", "SOLTAR", "L\u00c2CHER" } },
            { "down",       new[]{ "DOWN", "TURUN", "BAJAR", "DESCER", "BAS" } },
            { "next",       new[]{ "NEXT", "BERIKUTNYA", "SIGUIENTE", "PR\u00d3XIMO", "SUIVANT" } },
            { "swipeBig",   new[]{ "SWIPE TO ROTATE THE TUBE", "GESER UNTUK MEMUTAR TABUNG", "DESLIZA PARA GIRAR EL TUBO", "ARRASTE PARA GIRAR O TUBO", "GLISSEZ POUR TOURNER LE TUBE" } },
            { "touchStart", new[]{ "touch screen to start", "sentuh layar buat mulai", "toca para empezar", "toque para come\u00e7ar", "touchez pour commencer" } },
            { "rankings",   new[]{ "RANKINGS", "PERINGKAT", "RANKING", "RANKING", "CLASSEMENT" } },
            { "viewAll",    new[]{ "Tap for all", "Ketuk lihat semua", "Ver todo", "Ver tudo", "Tout voir" } },
            { "setProfile", new[]{ "Set profile", "Buat profil", "Crear perfil", "Criar perfil", "Cr\u00e9er profil" } },
            { "saveProfile", new[]{ "SAVE", "SIMPAN", "GUARDAR", "SALVAR", "ENREGISTRER" } },
            { "profileTitle", new[]{ "YOUR PROFILE", "PROFIL KAMU", "TU PERFIL", "SEU PERFIL", "TON PROFIL" } },
            { "nameLabel",  new[]{ "Name", "Nama", "Nombre", "Nome", "Nom" } },
            { "countryLabel", new[]{ "Country", "Negara", "Pa\u00eds", "Pa\u00eds", "Pays" } },
            { "submit",     new[]{ "SAVE & SUBMIT", "SIMPAN & KIRIM", "GUARDAR Y ENVIAR", "SALVAR E ENVIAR", "ENREGISTRER" } },
            { "you",        new[]{ "YOU", "KAMU", "T\u00da", "VOC\u00ca", "TOI" } },
            { "loading",    new[]{ "Loading...", "Memuat...", "Cargando...", "Carregando...", "Chargement..." } },
            { "close",      new[]{ "CLOSE", "TUTUP", "CERRAR", "FECHAR", "FERMER" } },
            { "yourRank",   new[]{ "Your rank", "Peringkatmu", "Tu rango", "Sua posi\u00e7\u00e3o", "Ton rang" } },
            { "unranked",   new[]{ "Not ranked yet", "Belum masuk peringkat", "Sin clasificar", "Sem classifica\u00e7\u00e3o", "Non class\u00e9" } },
            { "enterName",  new[]{ "Enter your name first", "Isi nama dulu", "Escribe tu nombre", "Digite seu nome", "Entre ton nom" } },
            { "connecting", new[]{ "Connecting...", "Menyambung...", "Conectando...", "Conectando...", "Connexion..." } },
            { "noScores",   new[]{ "No scores yet", "Belum ada skor", "Sin puntajes a\u00fan", "Sem pontua\u00e7\u00f5es ainda", "Aucun score" } },
            { "sending",    new[]{ "Sending...", "Mengirim...", "Enviando...", "Enviando...", "Envoi..." } },
            { "reviveAsk",  new[]{ "Continue playing?", "Lanjut main?", "\u00bfSeguir jugando?", "Continuar jogando?", "Continuer ?" } },
            { "watchAd",    new[]{ "WATCH AD & REVIVE", "TONTON IKLAN & LANJUT", "VER ANUNCIO Y SEGUIR", "VER AN\u00daNCIO E VOLTAR", "PUB & CONTINUER" } },
            { "skipRevive", new[]{ "SKIP", "LEWATI", "OMITIR", "PULAR", "PASSER" } },
            { "adNotReady", new[]{ "Ad not ready yet", "Iklan belum tersedia", "Anuncio no disponible", "An\u00fancio indispon\u00edvel", "Pub indisponible" } },
            { "sens",       new[]{ "Swipe sensitivity", "Sensitivitas geser", "Sensibilidad", "Sensibilidade", "Sensibilit\u00e9" } },
            { "sensLow",    new[]{ "Calm", "Santai", "Suave", "Calmo", "Doux" } },
            { "sensHigh",   new[]{ "Sensitive", "Sensitif", "Sensible", "Sens\u00edvel", "Sensible" } },
            { "haptic",     new[]{ "Vibration", "Getaran", "Vibraci\u00f3n", "Vibra\u00e7\u00e3o", "Vibration" } },
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
}
