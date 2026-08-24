using UnityEngine;
#if KUBIKA_ADMOB
using GoogleMobileAds.Api;
#endif

// =====================================================================
//  KUBIKA TOWER - REVIVE (rewarded) + MREC (banner 300x250)
// ---------------------------------------------------------------------
//  File TERPISAH (partial + manajer) - additive, tidak mengubah file inti.
//
//   * KubikaReviveAds : rewarded ad untuk REVIVE saat game over
//     (unit: ca-app-pub-3186700509396792/5117029021, TANPA SSV).
//   * KubikaMrec      : banner MEDIUM_RECTANGLE 300x250 di TENGAH-ATAS
//     (unit: ca-app-pub-3186700509396792/6580380437). Muncul di layar
//     Jeda, tawaran Revive, dan Game Over. Tidak menutupi tombol karena
//     semua tombol di ketiga layar itu ada di area tengah/bawah.
//   * KubikaMrecDriver: pantau status game tiap frame -> show/hide MREC.
//
//  Semua panggilan SDK diselubungi #if KUBIKA_ADMOB agar aman tanpa SDK.
// =====================================================================

public partial class Tetris3D
{
    // Layar yang boleh menampilkan MREC (Jeda / Revive / Game Over).
    // Dipakai KubikaMrecDriver. Akses field privat karena partial yang sama.
    public bool MrecShouldShow
    {
        get
        {
            if (!started) return false;
            if (showRanks || showProfile) return false;
            return paused || gameOver;
        }
    }

    // ---------------------------------------------------------------
    //  IKLAN FULLSCREEN "BENAR-BENAR DI DEPAN"
    //  Set true selama iklan fullscreen (rewarded/interstitial) tampil.
    //  Selama true, SEMUA HUD IMGUI (skor, chip permata/koin, tombol,
    //  gelembung, toko, overlay) BERHENTI digambar supaya tidak menimpa
    //  iklan. Dipanggil dari hook OnAdFullScreenContentOpened/Closed/
    //  Failed di semua manajer rewarded (KubikaReviveAds, KubikaAds,
    //  KubikaExtraAds).
    // ---------------------------------------------------------------
    public static bool AdFullscreenShowing = false;
    public static void BeginAdFullscreen() { AdFullscreenShowing = true; }
    public static void EndAdFullscreen()   { AdFullscreenShowing = false; }
}

// =====================================================================
//  REVIVE rewarded ad (tanpa SSV)
// =====================================================================
public class KubikaReviveAds : MonoBehaviour
{
    const string AD_UNIT_REVIVE = "ca-app-pub-3186700509396792/5117029021";
    const string AD_UNIT_TEST   = "ca-app-pub-3940256099942544/5224354917"; // test rewarded resmi Google
    const bool   USE_TEST_ADS   = false;

    static KubikaReviveAds _inst;
    public static KubikaReviveAds Instance
    {
        get
        {
            if (_inst == null)
            {
                var go = new GameObject("KubikaReviveAds");
                DontDestroyOnLoad(go);
                _inst = go.AddComponent<KubikaReviveAds>();
            }
            return _inst;
        }
    }

#if KUBIKA_ADMOB
    RewardedAd _ad;
    bool _init, _wantShow;
    Tetris3D _game;
    System.Action _cb;

    string Unit() { return USE_TEST_ADS ? AD_UNIT_TEST : AD_UNIT_REVIVE; }

    void EnsureInit()
    {
        if (_init) return;
        _init = true;
        MobileAds.Initialize(_ => Load());
    }

    void Load()
    {
        if (_ad != null) { _ad.Destroy(); _ad = null; }
        RewardedAd.Load(Unit(), new AdRequest(), (ad, err) =>
        {
            if (err != null || ad == null)
            {
                if (_wantShow)
                {
                    _wantShow = false;
                    if (_game != null) _game.OnReviveAdUnavailable();
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
        ad.OnAdFullScreenContentOpened += () => { Tetris3D.BeginAdFullscreen(); };
        ad.OnAdFullScreenContentClosed += () => { Tetris3D.EndAdFullscreen(); Load(); };
        ad.OnAdFullScreenContentFailed += (AdError e) =>
        {
            Tetris3D.EndAdFullscreen();
            if (_game != null) _game.OnReviveAdUnavailable();
            Load();
        };
    }

    void DoShow()
    {
        if (_ad == null || !_ad.CanShowAd()) { _wantShow = true; Load(); return; }
        _ad.Show(reward => { if (_cb != null) _cb(); });
    }

    public void Show(Tetris3D game, System.Action onReward)
    {
        _game = game; _cb = onReward;
        EnsureInit();
        if (_ad != null && _ad.CanShowAd()) DoShow();
        else { _wantShow = true; Load(); }
    }
#else
    public void Show(Tetris3D game, System.Action onReward)
    {
#if UNITY_EDITOR
        if (onReward != null) onReward();
#else
        if (game != null) game.OnReviveAdUnavailable();
#endif
    }
#endif
}

// =====================================================================
//  MREC banner 300x250 (tengah-atas)
// =====================================================================
public class KubikaMrec : MonoBehaviour
{
    const string AD_UNIT_MREC = "ca-app-pub-3186700509396792/6580380437";
    const string AD_UNIT_TEST = "ca-app-pub-3940256099942544/6300978111"; // test banner resmi Google
    const bool   USE_TEST_ADS = false;

    static KubikaMrec _inst;
    public static KubikaMrec Instance
    {
        get
        {
            if (_inst == null)
            {
                var go = new GameObject("KubikaMrec");
                DontDestroyOnLoad(go);
                _inst = go.AddComponent<KubikaMrec>();
            }
            return _inst;
        }
    }

#if KUBIKA_ADMOB
    BannerView _view;
    bool _init;
    bool _visible;

    string Unit() { return USE_TEST_ADS ? AD_UNIT_TEST : AD_UNIT_MREC; }

    void EnsureInit()
    {
        if (_init) return;
        _init = true;
        MobileAds.Initialize(_ => CreateView());
    }

    void CreateView()
    {
        if (_view != null) return;
        _view = new BannerView(Unit(), AdSize.MediumRectangle, AdPosition.Top);
        _view.OnBannerAdLoaded += () => { if (!_visible) _view.Hide(); };
        _view.LoadAd(new AdRequest());
        if (!_visible) _view.Hide();
    }

    public void ShowMrec()
    {
        _visible = true;
        EnsureInit();
        if (_view != null) _view.Show();
    }

    public void HideMrec()
    {
        _visible = false;
        if (_view != null) _view.Hide();
    }
#else
    public void ShowMrec() { }
    public void HideMrec() { }
#endif
}

// =====================================================================
//  Driver: pantau status game -> show/hide MREC otomatis
// =====================================================================
[DefaultExecutionOrder(-24000)]
public class KubikaMrecDriver : MonoBehaviour
{
    Tetris3D game;
    bool shown;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        var go = new GameObject("KubikaMrecDriver");
        DontDestroyOnLoad(go);
        go.AddComponent<KubikaMrecDriver>();
    }

    void Update()
    {
        if (game == null) game = Object.FindFirstObjectByType<Tetris3D>();
        if (game == null) return;
        // Sembunyikan MREC juga saat ada iklan fullscreen supaya tidak dobel/menimpa iklan.
        bool want = game.MrecShouldShow && !Tetris3D.AdFullscreenShowing;
        if (want == shown) return;
        shown = want;
        if (want) KubikaMrec.Instance.ShowMrec();
        else KubikaMrec.Instance.HideMrec();
    }
}
