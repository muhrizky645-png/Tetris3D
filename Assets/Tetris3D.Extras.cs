using System;
using UnityEngine;

// =====================================================================
//  KUBIKA TOWER - TAMBAHAN (additive, partial class)
// ---------------------------------------------------------------------
//  Fitur baru yang dipisah biar file inti tidak berisiko rusak:
//   - Safe-area: turunkan HUD atas di bawah kamera depan / notch HP
//   - Haptic (getar) saat line clear & game over (bisa ON/OFF)
//   - Auto-pause saat aplikasi ke background
//   - Slider sensitivitas geser (dragStep) di menu Jeda
//   - REVIVE saat game over: 5 detik hitung mundur + SFX detikan,
//     lanjut dengan menonton iklan berhadiah (AdMob). Maks 1x.
//   - Skor akhir: animasi angka naik cepat + SFX tick + fanfare rekor baru.
// =====================================================================
public partial class Tetris3D
{
    // ---------- STATE TAMBAHAN ----------
    bool hapticOn = true;       // getar saat clear & game over
    bool extrasLoaded;          // penanda prefs sudah dimuat

    // Revive
    bool reviveOffer;           // sedang menawarkan revive (hitung mundur)
    bool reviveUsed;            // sudah revive di game ini (maks 1x)
    bool reviveDeclined;        // pemain lewati / waktu habis
    bool reviveAdPending;       // iklan revive sedang diminta/ditampilkan
    float reviveTimer;          // sisa detik tawaran revive
    float reviveTickAcc;        // akumulator SFX detikan tiap 1 detik
    const float REVIVE_SECONDS = 5f;

    // Deteksi tepi buat haptic
    int prevLines;
    bool prevGameOver;

    // SFX detikan revive
    AudioClip sfxTick;

    // Skor akhir (game over): animasi angka naik + suara + deteksi rekor baru
    int runBaselineHi;      // rekor sebelum run ini (buat deteksi rekor baru)
    bool goAnimInit;        // animasi skor game over sudah diinit
    float goAnimStart;      // waktu mulai animasi (Time.time)
    float goAnimShown;      // angka skor yang sedang ditampilkan
    int goTickIndex;        // jumlah tick SFX yang sudah dibunyikan
    bool goWasNewHigh;      // run ini memecahkan rekor
    bool goHighPlayed;      // suara penghargaan sudah dimainkan
    AudioClip sfxCount;     // tick cepat saat angka naik
    AudioClip sfxNewHigh;   // fanfare penghargaan rekor baru

    // Toast kecil (mis. info iklan belum siap)
    string extrasToast = "";
    float extrasToastTime;

    // ---------- LOAD / SIMPAN PENGATURAN ----------
    void LoadExtrasPrefs()
    {
        // Rekor sebelum run ini (buat deteksi rekor baru di layar game over).
        // Ditangkap saat skor masih 0 di awal tiap run, sebelum rekor ter-update.
        if (started && !gameOver && score == 0) runBaselineHi = highScore;

        if (extrasLoaded) return;
        extrasLoaded = true;
        dragStep = PlayerPrefs.GetFloat("kubika_dragstep", dragStep);
        hapticOn = PlayerPrefs.GetInt("kubika_haptic", 1) == 1;
    }

    void SaveDragStep()
    {
        PlayerPrefs.SetFloat("kubika_dragstep", dragStep);
        PlayerPrefs.Save();
    }

    void SaveHaptic()
    {
        PlayerPrefs.SetInt("kubika_haptic", hapticOn ? 1 : 0);
        PlayerPrefs.Save();
    }

    // ---------- SAFE AREA ----------
    // Tinggi area atas yang ketutup (poni/kamera) dalam satuan LOGIS (ruang 720).
    // Selalu turun minimal MIN_TOP_LOGICAL biar HUD tetap agak turun walau:
    //   - di PC/Editor (tidak ada notch), dan
    //   - di HP yang tidak melaporkan lubang kamera sebagai safe-area inset.
    const float MIN_TOP_LOGICAL = 30f;
    float SafeTopLogical()
    {
        float topInsetPx = 0f;
        Rect sa = Screen.safeArea;
        float h = Screen.height;
        if (h > 1f)
        {
            topInsetPx = h - (sa.y + sa.height);
            if (topInsetPx < 0f) topInsetPx = 0f;
        }
        float scale = UiScale <= 0f ? 1f : UiScale;
        float logical = topInsetPx / scale;
        return Mathf.Max(MIN_TOP_LOGICAL, logical);
    }

    // ---------- GETAR (HAPTIC) ----------
    // Catatan penting: getar butuh izin android.permission.VIBRATE di manifest.
    // Dengan MEMANGGIL Handheld.Vibrate() (di fallback), Unity OTOMATIS menambahkan
    // izin VIBRATE ke manifest saat build -> jalur Vibrator (amplitudo) di bawah pun
    // ikut bekerja. Sebelumnya getar diam-diam gagal karena izin ini belum ada.
    void Haptic(long ms)
    {
        if (!hapticOn) return;
#if UNITY_ANDROID && !UNITY_EDITOR
        bool done = false;
        try
        {
            using (var up = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var act = up.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var vib = act.Call<AndroidJavaObject>("getSystemService", "vibrator"))
            {
                if (vib != null && vib.Call<bool>("hasVibrator"))
                {
                    int sdk = 0;
                    using (var ver = new AndroidJavaClass("android.os.Build$VERSION")) sdk = ver.GetStatic<int>("SDK_INT");
                    if (sdk >= 26)
                    {
                        using (var eff = new AndroidJavaClass("android.os.VibrationEffect"))
                        {
                            int amp = eff.GetStatic<int>("DEFAULT_AMPLITUDE");
                            using (var ve = eff.CallStatic<AndroidJavaObject>("createOneShot", ms, amp))
                                vib.Call("vibrate", ve);
                        }
                    }
                    else
                    {
                        vib.Call("vibrate", ms);
                    }
                    done = true;
                }
            }
        }
        catch { done = false; }

        // Fallback + pemicu izin VIBRATE otomatis dari Unity.
        if (!done)
        {
            try { Handheld.Vibrate(); } catch { }
        }
#endif
    }

    // ---------- AUTO-PAUSE saat app ke background ----------
    void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus && started && !gameOver && !reviveOffer) paused = true;
    }

    // ---------- REVIVE ----------
    void ResetRevive()
    {
        reviveOffer = false;
        reviveUsed = false;
        reviveDeclined = false;
        reviveAdPending = false;
        reviveTimer = 0f;
        reviveTickAcc = 0f;
    }

    // Akhiri tawaran revive. Dipanggil dari DUA jalur yang HARUS sama persis:
    //   1) pemain menekan tombol LEWATI, dan
    //   2) hitung mundur habis sendiri (Part3.Update).
    // Selain set flag, reset goAnimInit=false biar animasi angka skor di layar
    // Game Over SELALU mulai fresh dari 0 (bukan langsung lompat ke angka akhir),
    // jadi count-up tetap terlihat walau cooldown dibiarkan habis sendiri.
    void DeclineRevive()
    {
        reviveOffer = false;
        reviveDeclined = true;
        goAnimInit = false;
    }

    // Retry versi lengkap: reset status revive dulu, lalu jalankan logika asli.
    void RestartGameFull()
    {
        ResetRevive();
        prevGameOver = false;
        prevLines = 0;
        goAnimInit = false;
        RetryGame();
    }

    void GoHomeFull()
    {
        ResetRevive();
        prevGameOver = false;
        prevLines = 0;
        goAnimInit = false;
        GoHome();
    }

    // Dipanggil dari tombol "Tonton Iklan" di layar game over.
    void RequestReviveByAd()
    {
        if (reviveAdPending) return;
        reviveAdPending = true;
        // Bekukan hitung mundur selama iklan diminta/ditampilkan (Part3 hanya
        // tamat saat reviveTimer <= 0, dan tick SFX butuh reviveTickAcc >= 1).
        reviveTimer = 9999f;
        reviveTickAcc = -100000f;
        ShowRewardedAd(() =>
        {
            reviveAdPending = false;
            reviveUsed = true;
            reviveOffer = false;
            reviveDeclined = false;
            ClearTopRowsForRevive();
            gameOver = false;
            gameOverHandled = false;
            clearing = false;
            fallTimer = 0f;
            prevGameOver = false;
            Haptic(45);
            SpawnPiece();
        });
    }

    // Dipanggil KubikaReviveAds saat iklan revive gagal / tidak tersedia.
    public void OnReviveAdUnavailable()
    {
        reviveAdPending = false;
        // Lanjutkan lagi hitung mundur supaya pemain bisa coba lagi / lewati.
        reviveTimer = REVIVE_SECONDS;
        reviveTickAcc = 0f;
        Toast(T("adNotReady"));
    }

    // Bersihkan SEPARUH papan bagian atas biar ada ruang lagi.
    void ClearTopRowsForRevive()
    {
        if (grid == null || cells == null) return;
        int low = height / 2;
        for (int r = low; r < height; r++)
            for (int c = 0; c < columns; c++)
            {
                if (cells[c, r] != null) { Destroy(cells[c, r]); cells[c, r] = null; }
                grid[c, r] = -1;
            }
    }

    // ---------- IKLAN BERHADIAH (REVIVE) ----------
    // Revive pakai rewarded ad khusus (TANPA SSV) lewat KubikaReviveAds.
    // Di Editor tanpa SDK: langsung beri hadiah (buat tes). Di perangkat tanpa
    // define KUBIKA_ADMOB: tampilkan info iklan belum tersedia.
    void ShowRewardedAd(Action onReward)
    {
        KubikaReviveAds.Instance.Show(this, onReward);
    }

    void Toast(string msg)
    {
        extrasToast = msg;
        extrasToastTime = 2.4f;
    }

    // ---------- UI: layar tawaran REVIVE ----------
    void DrawReviveOffer()
    {
        float cx = VW / 2f;
        float off = MrecUiShift(); // geser ke bawah kalau banner MREC tampil di atas
        FillRect(new Rect(0f, 0f, VW, VH), new Color(0f, 0f, 0f, 0.80f));

        GuiText(new Rect(0f, VH * 0.30f + off, VW, 60f), "GAME OVER", 46, new Color(1f, 0.4f, 0.42f), TextAnchor.MiddleCenter);
        GuiText(new Rect(0f, VH * 0.36f + off, VW, 40f), T("reviveAsk"), 26, Color.white, TextAnchor.MiddleCenter);

        // Lingkaran hitung mundur
        int secs = Mathf.Max(0, Mathf.CeilToInt(reviveTimer));
        float frac = reviveTimer - Mathf.Floor(reviveTimer); // 0..1 dalam 1 detik
        float pulse = 1f - frac;
        float ring = Mathf.Min(VW * 0.40f, 300f);
        Rect ringRect = new Rect(cx - ring / 2f, VH * 0.43f + off, ring, ring);
        Color glow = Color.Lerp(new Color(1f, 0.85f, 0.3f), new Color(1f, 0.3f, 0.3f), 1f - reviveTimer / REVIVE_SECONDS);
        RoundRect(new Rect(ringRect.x - 8f, ringRect.y - 8f, ringRect.width + 16f, ringRect.height + 16f), new Color(glow.r, glow.g, glow.b, 0.22f + 0.28f * pulse), ring / 2f + 8f);
        RoundRect(ringRect, new Color(0.10f, 0.12f, 0.20f, 0.95f), ring / 2f);
        if (reviveAdPending)
            GuiText(ringRect, "...", 90, Color.white, TextAnchor.MiddleCenter);
        else
            GuiText(ringRect, "" + secs, 120, Color.white, TextAnchor.MiddleCenter);

        // Tombol tonton iklan -> revive
        float bw = Mathf.Min(VW * 0.74f, 440f);
        Rect adBtn = new Rect(cx - bw / 2f, ringRect.yMax + 28f, bw, 96f);
        if (Btn3D(adBtn, T("watchAd"), new Color(0.20f, 0.82f, 0.46f), false)) RequestReviveByAd();

        // Tombol lewati -> SAMA PERSIS dengan waktu habis sendiri (DeclineRevive).
        float sw = Mathf.Min(VW * 0.5f, 300f);
        Rect skip = new Rect(cx - sw / 2f, adBtn.yMax + 16f, sw, 64f);
        if (Btn3D(skip, T("skipRevive"), new Color(0.55f, 0.35f, 0.42f), false)) DeclineRevive();

        if (!string.IsNullOrEmpty(extrasToast) && extrasToastTime > 0f)
            GuiText(new Rect(0f, skip.yMax + 12f, VW, 30f), extrasToast, 20, new Color(1f, 0.85f, 0.4f), TextAnchor.MiddleCenter);
    }

    // ---------- UI: skor akhir + animasi angka naik + suara ----------
    // Dipanggil dari layar Game Over biasa (Part4). Angka naik cepat dari 0 ke skor
    // dengan SFX tick; kalau memecahkan rekor, tambah fanfare + getar.
    void EnsureScoreSfx()
    {
        if (sfxCount == null) sfxCount = MakeTone("count", 1200f, 0.03f, 0.32f, 0, 1500f);
        if (sfxNewHigh == null) sfxNewHigh = MakeArp("newhi", new float[] { 523.25f, 659.25f, 783.99f, 1046.50f, 1318.51f, 1567.98f, 2093.00f }, 0.12f, 0.6f);
    }

    string TNewHigh()
    {
        switch (lang)
        {
            case Lang.ID: return "REKOR BARU!";
            case Lang.ES: return "\u00a1NUEVO R\u00c9CORD!";
            case Lang.PT: return "NOVO RECORDE!";
            case Lang.FR: return "NOUVEAU RECORD!";
            default: return "NEW BEST!";
        }
    }

    void DrawGameOverScore()
    {
        EnsureScoreSfx();
        float off = MrecUiShift(); // geser ke bawah kalau banner MREC tampil di atas

        if (!goAnimInit)
        {
            goAnimInit = true;
            goAnimStart = Time.time;
            goTickIndex = 0;
            goHighPlayed = false;
            goWasNewHigh = score > runBaselineHi && score > 0;
            goAnimShown = 0f;
        }

        // Jeda kecil di awal (setelah layar revive) biar mata pemain sempat fokus
        // ke layar Game Over dulu sebelum angka mulai naik.
        const float START_DELAY = 0.5f;
        // Durasi animasi: cepat (~0.35-1.1 dtk) biar greget tapi tetap kebaca.
        float dur = Mathf.Clamp(score / 18000f, 0.35f, 1.1f);
        float elapsed = Mathf.Max(0f, Time.time - goAnimStart - START_DELAY);
        float p = dur <= 0f ? 1f : Mathf.Clamp01(elapsed / dur);
        float pe = 1f - (1f - p) * (1f - p); // sedikit melambat di akhir
        goAnimShown = pe * score;
        int shown = Mathf.RoundToInt(goAnimShown);

        // SFX tick cepat selama menghitung (maju sekali per interval; aman dari OnGUI ganda
        // karena Time.time sama di beberapa pass dalam satu frame).
        if (p < 1f)
        {
            int ticks = Mathf.FloorToInt(elapsed / 0.045f);
            if (ticks > goTickIndex) { goTickIndex = ticks; Sfx(sfxCount); }
        }
        else if (!goHighPlayed)
        {
            goHighPlayed = true;
            if (goWasNewHigh) { Sfx(sfxNewHigh); Haptic(60); }
            else Sfx(sfxCount);
        }

        // Label kecil "SKOR"
        GuiText(new Rect(0f, VH * 0.375f + off, VW, 26f), T("score"), 22, new Color(1f, 1f, 1f, 0.85f), TextAnchor.MiddleCenter);
        // Angka skor besar (emas kalau rekor baru, putih kalau biasa)
        Color numCol = goWasNewHigh ? new Color(1f, 0.86f, 0.32f) : Color.white;
        GuiText(new Rect(0f, VH * 0.40f + off, VW, 92f), "" + shown, 80, numCol, TextAnchor.MiddleCenter);

        // Baris rekor / banner REKOR BARU (muncul berkedip setelah animasi selesai)
        if (goWasNewHigh && p >= 1f)
        {
            float a = 0.65f + 0.35f * Mathf.Sin(Time.time * 6f);
            GlowText(new Rect(0f, VH * 0.475f + off, VW, 40f), TNewHigh(), 30, new Color(1f, 0.86f, 0.32f), a);
        }
        else
        {
            GuiText(new Rect(0f, VH * 0.478f + off, VW, 30f), T("record") + " " + highScore, 22, new Color(1f, 0.9f, 0.55f), TextAnchor.MiddleCenter);
        }
    }

    // ---------- UI: slider bertema (menu Jeda) ----------
    // Kembalikan nilai 0..1 baru berdasar sentuhan pada track.
    float DrawSlider(Rect r, string label, float value01, string leftLabel, string rightLabel)
    {
        value01 = Mathf.Clamp01(value01);
        RoundRect(r, new Color(0.10f, 0.12f, 0.18f, 0.92f), 16f);
        GuiText(new Rect(r.x + 18f, r.y + 6f, r.width - 36f, 24f), label, 20, Color.white, TextAnchor.MiddleLeft);

        float trackX = r.x + 22f;
        float trackW = r.width - 44f;
        float trackH = 12f;
        float trackY = r.y + r.height - 22f;
        Rect track = new Rect(trackX, trackY, trackW, trackH);

        GuiText(new Rect(trackX, r.y + 32f, trackW * 0.5f, 20f), leftLabel, 14, new Color(0.7f, 0.8f, 1f), TextAnchor.MiddleLeft);
        GuiText(new Rect(trackX + trackW * 0.5f, r.y + 32f, trackW * 0.5f, 20f), rightLabel, 14, new Color(0.7f, 0.8f, 1f), TextAnchor.MiddleRight);

        RoundRect(track, new Color(0.30f, 0.32f, 0.40f), trackH / 2f);
        RoundRect(new Rect(trackX, trackY, Mathf.Max(trackH, trackW * value01), trackH), new Color(0.20f, 0.82f, 0.46f), trackH / 2f);
        float knob = 26f;
        float kx = trackX + trackW * value01 - knob / 2f;
        RoundRect(new Rect(kx, trackY + trackH / 2f - knob / 2f, knob, knob), Color.white, knob / 2f);

        Event e = Event.current;
        Rect hit = new Rect(trackX - 12f, trackY - 20f, trackW + 24f, 48f);
        if (e != null && (e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && hit.Contains(e.mousePosition))
        {
            value01 = Mathf.Clamp01((e.mousePosition.x - trackX) / Mathf.Max(1f, trackW));
            e.Use();
        }
        return value01;
    }
}
