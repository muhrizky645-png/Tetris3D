using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

// =====================================================================
//  KUBIKA TOWER x SALDOKU - HUBUNGKAN AKUN & SINKRON KOIN
// ---------------------------------------------------------------------
//  File TERPISAH (partial) supaya TIDAK mengubah file gameplay lama
//  maupun Tetris3D.Currency.cs. Semuanya additive.
//
//  Alur (sesuai server):
//   1. User login di APP SALDOKU -> game_link_start.php -> dapat KODE.
//   2. User masukkan KODE di game ini.
//   3. Game POST game_link_verify.php {kode, device} -> game_token.
//   4. Game GET poin_game_status_apk.php (Bearer game_token) -> koin+peti.
//
//  KOIN tetap READ-ONLY di game: hanya server yang menambah (via AdMob SSV).
// =====================================================================

public partial class Tetris3D
{
    // ---- endpoint & penyimpanan ----
    const string SALDOKU_BASE = "https://saldoku.site";
    const string PP_TOKEN = "kubika_game_token";
    const string PP_NAMA  = "kubika_saldoku_nama";
    const string PP_JULUKAN = "kubika_saldoku_julukan";

    // ---- state akun ----
    string sal_token;
    string sal_nama;
    string sal_julukan;   // julukan lokal (nama tampilan) -> ganti nama asli SALDOKU demi privasi
    bool   sal_ready;

    // ---- state UI overlay ----
    bool   linkOpen;
    string linkCode   = "";
    string linkStatus = "";
    bool   linkBusy;

    // ---- info peti/harian (dari poin_game_status_apk.php) ----
    int peti_progress;
    int peti_sisa;
    int iklanHariIni;
    int sisaIklan;
    int iklanPerPeti = 5;
    int poinPerPeti  = 1000;
    int batasHarian  = 20;

    bool SalID { get { return lang == Lang.ID; } }

    public bool SaldokuOverlayOpen { get { return linkOpen; } }

    // Chip mata uang boleh tampil di MENU AWAL?
    public bool CurrencyMenuVisible
    {
        get { return !started && !showProfile && !showRanks; }
    }

    // Kotak chip Koin (chip ke-2) relatif ke titik awal DrawCurrencyHUD(x,y).
    public Rect KoinChipRect(float x, float y)
    {
        float w = 300f, h = 76f, gap = 10f;
        return new Rect(x, y + h + gap, w, h);
    }

    void EnsureSaldoku()
    {
        if (sal_ready) return;
        sal_token = PlayerPrefs.GetString(PP_TOKEN, "");
        sal_nama  = PlayerPrefs.GetString(PP_NAMA, "");
        sal_julukan = PlayerPrefs.GetString(PP_JULUKAN, "");
        sal_ready = true;
    }

    // Nama yang DITAMPILKAN untuk akun SALDOKU: julukan kalau ada, kalau tidak nama asli,
    // kalau dua-duanya kosong pakai "SALDOKU". Nama asli tidak pernah tampil kalau julukan diisi.
    string SalDisplayName()
    {
        if (!string.IsNullOrEmpty(sal_julukan)) return sal_julukan;
        if (!string.IsNullOrEmpty(sal_nama)) return sal_nama;
        return "SALDOKU";
    }

    void SaveJulukan()
    {
        sal_julukan = (sal_julukan ?? "").Trim();
        PlayerPrefs.SetString(PP_JULUKAN, sal_julukan);
        PlayerPrefs.Save();
        linkStatus = SalNickSaved();
    }

    // ---- entry points (dipanggil komponen UI) ----
    public void OpenSaldokuLink()
    {
        EnsureCurrency();
        EnsureSaldoku();
        linkOpen   = true;
        linkStatus = "";
        if (!cur_linked) linkCode = "";
    }

    public void CloseSaldokuLink() { linkOpen = false; }

    public void SubmitSaldokuCode()
    {
        if (linkBusy) return;
        EnsureSaldoku();
        string k = (linkCode ?? "").Trim().ToUpperInvariant();
        if (k.Length < 4) { linkStatus = SalNeedCode(); return; }
        StartCoroutine(CoLinkAccount(k));
    }

    public void RefreshKoinNow()
    {
        EnsureSaldoku();
        if (string.IsNullOrEmpty(sal_token)) { linkStatus = SalNotLinked(); return; }
        StartCoroutine(CoRefreshKoin(false));
    }

    public void UnlinkSaldoku()
    {
        DoUnlinkSilent();
        linkStatus = "";
        linkCode   = "";
    }

    public void AutoRefreshKoinOnStart()
    {
        EnsureCurrency();
        EnsureSaldoku();
        if (!string.IsNullOrEmpty(sal_token)) StartCoroutine(CoRefreshKoin(true));
    }

    void DoUnlinkSilent()
    {
        sal_token = "";
        sal_nama  = "";
        PlayerPrefs.DeleteKey(PP_TOKEN);
        PlayerPrefs.DeleteKey(PP_NAMA);
        SetKoinFromServer(0, false, false);
    }

    // ---- networking ----
    IEnumerator CoLinkAccount(string kode)
    {
        linkBusy   = true;
        linkStatus = SalConnecting();

        string url  = SALDOKU_BASE + "/game_link_verify.php";
        string json = JsonUtility.ToJson(new SalVerifyReq { kode = kode, device = SystemInfo.deviceUniqueIdentifier });
        byte[] body = System.Text.Encoding.UTF8.GetBytes(json);

        using (var req = new UnityWebRequest(url, "POST"))
        {
            req.uploadHandler   = new UploadHandlerRaw(body);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = 20;
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                linkStatus = SalNetErr();
                linkBusy   = false;
                yield break;
            }

            SalVerifyResp resp = null;
            try { resp = JsonUtility.FromJson<SalVerifyResp>(req.downloadHandler.text); }
            catch { resp = null; }

            if (resp == null || !resp.status || resp.data == null || string.IsNullOrEmpty(resp.data.game_token))
            {
                linkStatus = (resp != null && !string.IsNullOrEmpty(resp.message)) ? resp.message : SalBadCode();
                linkBusy   = false;
                yield break;
            }

            sal_token = resp.data.game_token;
            sal_nama  = resp.data.nama ?? "";
            PlayerPrefs.SetString(PP_TOKEN, sal_token);
            PlayerPrefs.SetString(PP_NAMA, sal_nama);
            PlayerPrefs.Save();
            linkStatus = SalOkLoading();
        }

        yield return StartCoroutine(CoRefreshKoin(false));
        linkBusy = false;
        if (cur_linked) { linkStatus = SalOk(); linkOpen = false; }
    }

    IEnumerator CoRefreshKoin(bool silent)
    {
        EnsureSaldoku();
        if (string.IsNullOrEmpty(sal_token)) yield break;
        if (!silent) linkStatus = SalLoading();

        string url = SALDOKU_BASE + "/poin_game_status_apk.php";
        using (var req = UnityWebRequest.Get(url))
        {
            req.SetRequestHeader("Authorization", "Bearer " + sal_token);
            req.timeout = 20;
            yield return req.SendWebRequest();

            long httpCode = req.responseCode;
            string text = (req.downloadHandler != null) ? req.downloadHandler.text : "";

            if (req.result != UnityWebRequest.Result.Success)
            {
                if (httpCode == 401) { DoUnlinkSilent(); if (!silent) linkStatus = SalTokenExpired(); }
                else { SetKoinFromServer(cur_koin, false, true); if (!silent) linkStatus = SalOfflineKeep(); }
                yield break;
            }

            SalStatusResp resp = null;
            try { resp = JsonUtility.FromJson<SalStatusResp>(text); }
            catch { resp = null; }

            if (resp == null || !resp.status || resp.data == null)
            {
                if (httpCode == 401) { DoUnlinkSilent(); if (!silent) linkStatus = SalTokenExpired(); }
                yield break;
            }

            SalStatusData d = resp.data;
            peti_progress = d.peti_progress;
            peti_sisa     = d.sisa_ke_peti;
            iklanHariIni  = d.iklan_hari_ini;
            sisaIklan     = d.sisa_iklan;
            if (d.iklan_per_peti > 0) iklanPerPeti = d.iklan_per_peti;
            if (d.poin_per_peti  > 0) poinPerPeti  = d.poin_per_peti;
            if (d.batas_harian   > 0) batasHarian  = d.batas_harian;
            if (!string.IsNullOrEmpty(d.nama)) { sal_nama = d.nama; PlayerPrefs.SetString(PP_NAMA, sal_nama); }

            SetKoinFromServer(d.koin, true, true);
            if (!silent) linkStatus = "";
        }
    }

    // ---- overlay UI ----
    public void DrawSaldokuOverlay()
    {
        EnsureCurrency();
        EnsureSaldoku();

        float sw = VW, sh = VH;

        // Backdrop gelap (VISUAL saja). Penelan klik LUAR digambar PALING AKHIR
        // supaya tidak mencuri MouseDown/fokus dari input & tombol di dalam panel.
        RoundRect(new Rect(0f, 0f, sw, sh), new Color(0f, 0f, 0f, 0.72f), 0f);

        float pw = Mathf.Min(sw * 0.88f, 760f);
        float ph = Mathf.Min(sh * 0.90f, 720f);
        float px = (sw - pw) * 0.5f;
        float py = (sh - ph) * 0.5f;

        RoundRect(new Rect(px - 4f, py - 4f, pw + 8f, ph + 8f), new Color(0.62f, 0.35f, 1f, 0.5f), 26f);
        RoundRect(new Rect(px, py, pw, ph), new Color(0.06f, 0.08f, 0.12f, 0.98f), 24f);

        float cx = px + 34f;
        float cw = pw - 68f;
        float yy = py + 30f;

        GuiText(new Rect(cx, yy, cw, 48f), SalTitle(), 38, Color.white, TextAnchor.UpperLeft);
        yy += 64f;

        if (!cur_linked)
        {
            GuiText(new Rect(cx, yy, cw, 200f), SalHowto(), 24,
                new Color(0.80f, 0.82f, 0.90f), TextAnchor.UpperLeft);
            yy += 190f;

            GuiText(new Rect(cx, yy, cw, 30f), SalCodeLabel(), 22,
                new Color(0.62f, 0.70f, 1f), TextAnchor.UpperLeft);
            yy += 38f;

            GUIStyle tf = new GUIStyle(GUI.skin.textField);
            tf.fontSize    = 40;
            tf.alignment   = TextAnchor.MiddleCenter;
            tf.fixedHeight = 72f;
            GUI.SetNextControlName("SalCodeField");
            string typed = GUI.TextField(new Rect(cx, yy, cw, 72f), linkCode ?? "", 8, tf);
            linkCode = typed.ToUpperInvariant();
            yy += 88f;

            if (!string.IsNullOrEmpty(linkStatus))
                GuiText(new Rect(cx, yy, cw, 30f), linkStatus, 22,
                    new Color(1f, 0.85f, 0.5f), TextAnchor.UpperLeft);
            yy += 44f;

            float bw = (cw - 16f) * 0.5f;
            if (SalButton(new Rect(cx, yy, bw, 72f), linkBusy ? "..." : CurConnect(),
                    new Color(0.62f, 0.35f, 1f)) && !linkBusy)
                SubmitSaldokuCode();
            if (SalButton(new Rect(cx + bw + 16f, yy, bw, 72f), SalClose(),
                    new Color(0.30f, 0.34f, 0.42f)))
                CloseSaldokuLink();
        }
        else
        {
            GuiText(new Rect(cx, yy, cw, 34f),
                SalLinkedAs() + " " + SalDisplayName(),
                26, new Color(0.6f, 1f, 0.7f), TextAnchor.UpperLeft);
            yy += 44f;

            // --- Julukan (nama tampilan) lokal: ganti nama asli SALDOKU demi privasi ---
            GuiText(new Rect(cx, yy, cw, 26f), SalNickLabel(), 20,
                new Color(0.62f, 0.70f, 1f), TextAnchor.UpperLeft);
            yy += 30f;
            {
                GUIStyle jf = new GUIStyle(GUI.skin.textField);
                jf.fontSize    = 26;
                jf.alignment   = TextAnchor.MiddleLeft;
                jf.fixedHeight = 60f;
                float jbw = 150f;
                float jtw = cw - jbw - 12f;
                GUI.SetNextControlName("SalNickField");
                sal_julukan = GUI.TextField(new Rect(cx, yy, jtw, 60f), sal_julukan ?? "", 16, jf);
                if (SalButton(new Rect(cx + jtw + 12f, yy, jbw, 60f), SalSave(), new Color(0.20f, 0.60f, 1f)))
                    SaveJulukan();
            }
            yy += 72f;

            GuiText(new Rect(cx, yy, cw, 44f),
                "Koin: " + CurShort(cur_koin) + (cur_online ? "" : " (offline)"),
                36, Color.white, TextAnchor.UpperLeft);
            yy += 56f;

            GuiText(new Rect(cx, yy, cw, 32f),
                SalPeti() + " " + peti_progress + "/" + iklanPerPeti +
                "   " + SalToday() + " " + iklanHariIni + "/" + batasHarian,
                22, new Color(0.80f, 0.82f, 0.90f), TextAnchor.UpperLeft);
            yy += 50f;

            // --- Peti Koin: tonton iklan berhadiah (reward via server SSV) ---
            if (SalButton(new Rect(cx, yy, cw, 74f),
                    petiBusy ? (SalID ? "Memuat iklan..." : "Loading ad...") : PetiKoinBtn(),
                    new Color(1f, 0.62f, 0.12f)) && !petiBusy)
                WatchPetiAd();
            yy += 86f;

            if (!string.IsNullOrEmpty(petiStatus))
                GuiText(new Rect(cx, yy, cw, 30f), petiStatus, 22,
                    new Color(0.75f, 1f, 0.8f), TextAnchor.UpperLeft);
            yy += 40f;

            if (!string.IsNullOrEmpty(linkStatus))
                GuiText(new Rect(cx, yy, cw, 30f), linkStatus, 22,
                    new Color(1f, 0.85f, 0.5f), TextAnchor.UpperLeft);
            yy += 42f;

            float bw = (cw - 16f) * 0.5f;
            if (SalButton(new Rect(cx, yy, bw, 68f), linkBusy ? "..." : SalRefresh(),
                    new Color(0.20f, 0.60f, 1f)) && !linkBusy)
                RefreshKoinNow();
            if (SalButton(new Rect(cx + bw + 16f, yy, bw, 68f), SalUnlink(),
                    new Color(0.70f, 0.25f, 0.30f)))
                UnlinkSaldoku();

            if (SalButton(new Rect(cx, yy + 80f, cw, 60f), SalClose(),
                    new Color(0.30f, 0.34f, 0.42f)))
                CloseSaldokuLink();
        }

        // --- Penelan klik LUAR panel: digambar PALING AKHIR supaya tidak
        //     menutupi input & tombol (mereka digambar lebih dulu -> tangkap
        //     MouseDown