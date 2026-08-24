using System.Collections;
using UnityEngine;
#if KUBIKA_ADMOB
using GoogleMobileAds.Api;
#endif

// =====================================================================
//  KUBIKA TOWER x SALDOKU - PETI KOIN (rewarded ad)
// ---------------------------------------------------------------------
//  File TERPISAH (partial) - additive, tidak mengubah gameplay lama
//  maupun Tetris3D.Currency.cs / Tetris3D.Saldoku.cs.
//
//  Skema Peti (server = sumber kebenaran):
//    * Tonton 5 iklan  -> 1 Peti  -> +1000 Koin (poin SALDOKU)
//    * Batas 20 iklan/hari  -> maksimal 4000 Koin/hari
//
//  KOIN tetap READ-ONLY di game. Reward TIDAK ditambahkan oleh client.
//  Alur:
//    1. Client tampilkan rewarded ad (AdMob) dengan ServerSideVerification
//       custom_data = game_token.
//    2. Google kirim SSV callback -> ssv_kubika_callback.php (bertanda
//       tangan). Server verifikasi, resolve token -> user (getGameUser),
//       catat 1 iklan; tiap kelipatan 5 -> +1000 poin (ref_id idempotent),
//       hormati batas harian.
//    3. Client refresh poin_game_status_apk.php -> Koin & progress peti
//       diperbarui dari server.
//
//  CATATAN BUILD - cara mengaktifkan iklan:
//    a. Import "Google Mobile Ads Unity Plugin" (v9+).
//    b. Project Settings > Player > Scripting Define Symbols (Android):
//       tambahkan  KUBIKA_ADMOB
//    c. Isi AdMob App ID di GoogleMobileAds settings:
//       ca-app-pub-3186700509396792~4847592405
//    Tanpa define KUBIKA_ADMOB, tombol Peti Koin menampilkan pesan
//    "fitur iklan belum aktif" (project tetap bisa di-build).
// =====================================================================

public partial class Tetris3D
{
    bool   petiBusy;
    string petiStatus = "";

    // Label tombol, mis. "Tonton Iklan   2/5"
    public string PetiKoinBtn()
    {
        string t = SalID ? "Tonton Iklan" : "Watch Ad";
        return t + "   " + peti_progress + "/" + iklanPerPeti;
    }

    public void WatchPetiAd()
    {
        EnsureCurrency();
        EnsureSaldoku();
        if (petiBusy) return;
        if (!cur_linked || string.IsNullOrEmpty(sal_token)) { petiStatus = SalNotLinked(); return; }
        if (batasHarian > 0 && sisaIklan <= 0)             { petiStatus = PetiDailyDone(); return; }
        petiStatus = "";
        KubikaAds.Instance.ShowPetiAd(this, sal_token);
    }

    // ---- callback dari KubikaAds (dijalankan di main thread) ----
    public void SetPetiBusy(bool b) { petiBusy = b; }

    public void OnPetiAdReward()
    {
        // Reward dikreditkan SERVER via SSV. Client hanya menyegarkan.
        petiStatus = PetiCrediting();
        StartCoroutine(CoAfterPetiAd());
    }

    IEnumerator CoAfterPetiAd()
    {
        // beri jeda agar SSV callback server sempat diproses sebelum refresh
        yield return new WaitForSeconds(2.5f);
        yield return StartCoroutine(CoRefreshKoin(false));
        petiStatus = PetiDone();
    }

    public void OnPetiAdUnavailable(string msg)
    {
        petiBusy   = false;
        petiStatus = msg;
    }

    // ---- teks lokal (ID + fallback EN) ----
    string PetiDailyDone()       { return SalID ? "Batas iklan harian tercapai." : "Daily ad limit reached."; }
    string PetiCrediting()       { return SalID ? "Iklan selesai. Menambahkan Koin..." : "Ad finished. Crediting Koin..."; }
    string PetiDone()            { return SalID ? "Koin & peti diperbarui!" : "Koin & chest updated!"; }
    public string PetiNoAdMsg()  { return SalID ? "Iklan belum siap. Coba lagi." : "Ad not ready. Please try again."; }
    public string PetiAdsOffMsg(){ return SalID ? "Fitur iklan belum aktif di build ini." : "Ads are not enabled in this build."; }
}

// =====================================================================
//  Manajer Rewarded Ad (AdMob). Singleton, dibuat saat dibutuhkan.
//  Semua panggilan SDK diselubungi #if KUBIKA_ADMOB agar aman tanpa SDK.
// =====================================================================
public class KubikaAds : MonoBehaviour
{
    const string AD_UNIT_PROD = "ca-app-pub-3186700509396792/6949035774";
    const string AD_UNIT_TEST = "ca-app-pub-3940256099942544/5224354917"; // test rewarded resmi Google
    const bool   USE_TEST_ADS = false;

    static KubikaAds _inst;
    public static KubikaAds Instance
    {
        get
        {
            if (_inst == null)
            {
                var go = new GameObject("KubikaAds");
                DontDestroyOnLoad(go);
                _inst = go.AddComponent<KubikaAds>();
            }
            return _inst;
        }
    }

#if KUBIKA_ADMOB
    RewardedAd _ad;
    bool       _init;
    bool       _wantShow;
    Tetris3D   _game;
    string     _customData;

    void EnsureInit()
    {
        if (_init) return;
        _init = true;
        MobileAds.Initialize(_ => Load());
    }

    string Unit() { return USE_TEST_ADS ? AD_UNIT_TEST : AD_UNIT_PROD; }

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
                    if (_game != null) _game.OnPetiAdUnavailable(_game.PetiNoAdMsg());
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
        ad.OnAdFullScreenContentClosed += () =>
        {
            Tetris3D.EndAdFullscreen();
            if (_game != null) _game.SetPetiBusy(false);
            Load(); // preload iklan berikutnya
        };
        ad.OnAdFullScreenContentFailed += (AdError e) =>
        {
            Tetris3D.EndAdFullscreen();
            if (_game != null)
            {
                _game.SetPetiBusy(false);
                _game.OnPetiAdUnavailable(_game.PetiNoAdMsg());
            }
            Load();
        };
    }

    void DoShow()
    {
        if (_ad == null || !_ad.CanShowAd()) { _wantShow = true; Load(); return; }
        if (!string.IsNullOrEmpty(_customData))
        {
            var ssv = new ServerSideVerificationOptions { CustomData = _customData };
            _ad.SetServerSideVerificationOptions(ssv);
        }
        Tetris3D.BeginAdFullscreen(); // set SEBELUM Show: placeholder editor & sebagian device tidak memicu OnAdFullScreenContentOpened
        _ad.Show(reward =>
        {
            if (_game != null) _game.OnPetiAdReward();
        });
    }

    public void ShowPetiAd(Tetris3D game, string customData)
    {
        _game       = game;
        _customData = customData;
        game.SetPetiBusy(true);
        EnsureInit();
        if (_ad != null && _ad.CanShowAd()) DoShow();
        else { _wantShow = true; Load(); }
    }
#else
    public void ShowPetiAd(Tetris3D game, string customData)
    {
        game.OnPetiAdUnavailable(game.PetiAdsOffMsg());
    }
#endif
}
