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
//     lanjut dengan menonton iklan (stub; AdMob menyusul). Maks 1x.
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
    float reviveTimer;          // sisa detik tawaran revive
    float reviveTickAcc;        // akumulator SFX detikan tiap 1 detik
    const float REVIVE_SECONDS = 5f;
    const int REVIVE_CLEAR_ROWS = 5;   // baris teratas (dekat plafon) yang dibersihkan

    // Deteksi tepi buat haptic
    int prevLines;
    bool prevGameOver;

    // SFX detikan revive
    AudioClip sfxTick;

    // Toast kecil (mis. info iklan belum siap)
    string extrasToast = "";
    float extrasToastTime;

    // ---------- LOAD / SIMPAN PENGATURAN ----------
    void LoadExtrasPrefs()
    {
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
    float SafeTopLogical()
    {
        Rect sa = Screen.safeArea;
        float h = Screen.height;
        if (h <= 1f) return 0f;
        float topInsetPx = h - (sa.y + sa.height);
        if (topInsetPx < 0f) topInsetPx = 0f;
        float scale = UiScale <= 0f ? 1f : UiScale;
        return topInsetPx / scale;
    }

    // ---------- GETAR (HAPTIC) ----------
    void Haptic(long ms)
    {
        if (!hapticOn) return;
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (var up = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var act = up.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var vib = act.Call<AndroidJavaObject>("getSystemService", "vibrator"))
            {
                if (vib == null) return;
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
            }
        }
        catch { }
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
        reviveTimer = 0f;
        reviveTickAcc = 0f;
    }

    // Retry versi lengkap: reset status revive dulu, lalu jalankan logika asli.
    void RestartGameFull()
    {
        ResetRevive();
        prevGameOver = false;
        prevLines = 0;
        RetryGame();
    }

    void GoHomeFull()
    {
        ResetRevive();
        prevGameOver = false;
        prevLines = 0;
        GoHome();
    }

    // Dipanggil dari tombol "Tonton Iklan" di layar game over.
    void RequestReviveByAd()
    {
        ShowRewardedAd(() =>
        {
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

    // Bersihkan beberapa baris teratas (dekat plafon) biar ada ruang lagi.
    void ClearTopRowsForRevive()
    {
        if (grid == null || cells == null) return;
        int low = Mathf.Max(1, killLine - REVIVE_CLEAR_ROWS);
        for (int r = low; r < height; r++)
            for (int c = 0; c < columns; c++)
            {
                if (cells[c, r] != null) { Destroy(cells[c, r]); cells[c, r] = null; }
                grid[c, r] = -1;
            }
    }

    // ---------- IKLAN BERHADIAH (STUB) ----------
    // AdMob belum terpasang. Sementara: di Editor langsung beri hadiah (buat tes),
    // di perangkat tampilkan info kalau iklan belum tersedia. Nanti saat AdMob
    // dipasang, cukup ganti isi fungsi ini -> revive & Peti Koin pakai jalur sama.
    void ShowRewardedAd(Action onReward)
    {
#if UNITY_EDITOR
        if (onReward != null) onReward();
#else
        Toast(T("adNotReady"));
#endif
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
        FillRect(new Rect(0f, 0f, VW, VH), new Color(0f, 0f, 0f, 0.80f));

        GuiText(new Rect(0f, VH * 0.15f, VW, 60f), "GAME OVER", 46, new Color(1f, 0.4f, 0.42f), TextAnchor.MiddleCenter);
        GuiText(new Rect(0f, VH * 0.22f, VW, 40f), T("reviveAsk"), 26, Color.white, TextAnchor.MiddleCenter);

        // Lingkaran hitung mundur
        int secs = Mathf.Max(0, Mathf.CeilToInt(reviveTimer));
        float frac = reviveTimer - Mathf.Floor(reviveTimer); // 0..1 dalam 1 detik
        float pulse = 1f - frac;
        float ring = Mathf.Min(VW * 0.40f, 300f);
        Rect ringRect = new Rect(cx - ring / 2f, VH * 0.29f, ring, ring);
        Color glow = Color.Lerp(new Color(1f, 0.85f, 0.3f), new Color(1f, 0.3f, 0.3f), 1f - reviveTimer / REVIVE_SECONDS);
        RoundRect(new Rect(ringRect.x - 8f, ringRect.y - 8f, ringRect.width + 16f, ringRect.height + 16f), new Color(glow.r, glow.g, glow.b, 0.22f + 0.28f * pulse), ring / 2f + 8f);
        RoundRect(ringRect, new Color(0.10f, 0.12f, 0.20f, 0.95f), ring / 2f);
        GuiText(ringRect, "" + secs, 120, Color.white, TextAnchor.MiddleCenter);

        // Tombol tonton iklan -> revive
        float bw = Mathf.Min(VW * 0.74f, 440f);
        Rect adBtn = new Rect(cx - bw / 2f, ringRect.yMax + 28f, bw, 96f);
        if (Btn3D(adBtn, T("watchAd"), new Color(0.20f, 0.82f, 0.46f), false)) RequestReviveByAd();

        // Tombol lewati
        float sw = Mathf.Min(VW * 0.5f, 300f);
        Rect skip = new Rect(cx - sw / 2f, adBtn.yMax + 16f, sw, 64f);
        if (Btn3D(skip, T("skipRevive"), new Color(0.55f, 0.35f, 0.42f), false)) { reviveOffer = false; reviveDeclined = true; }

        if (!string.IsNullOrEmpty(extrasToast) && extrasToastTime > 0f)
            GuiText(new Rect(0f, skip.yMax + 12f, VW, 30f), extrasToast, 20, new Color(1f, 0.85f, 0.4f), TextAnchor.MiddleCenter);
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
