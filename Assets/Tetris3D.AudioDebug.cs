using UnityEngine;

// =====================================================================
//  KUBIKA TOWER - LAPORAN AUDIO  (ALAT UKUR, BUKAN FITUR)
// ---------------------------------------------------------------------
//  Gejala yang mendorong file ini: teks pujian GOOD!..LEGENDARY! muncul,
//  tapi suaranya tidak ada.
//
//  Yang penting: DrawKubikaPraiseGui() TIDAK memeriksa tombol suara sama
//  sekali. Jadi teks yang muncul BUKAN bukti soundOn/sfxOn menyala -
//  teks dan suara itu dua jalur terpisah.
//
//  Dua tersangka terkuat tidak bisa dilihat dari luar Unity:
//    1. nilai soundOn / sfxOn / sfxVolume yang TERSIMPAN DI SampleScene.
//       Field itu field LAMA, jadi ter-serialize di GameObject Game, dan
//       nilai scene MENGALAHKAN default di kode.
//    2. ada atau tidaknya mp3 di Assets/Resources/KubikaVoice/.
//  Part3.Sfx() dan KprPlayVoice() sama-sama 'return' diam-diam. Itu benar
//  untuk rilis, tapi tidak bisa didiagnosis. File ini mengubah kegagalan
//  yang SENYAP menjadi laporan yang bisa dibaca - tanpa mengubah satu pun
//  perilaku rilis.
//
//  == HASIL AKHIR DIAGNOSIS (8 September 2026) ==
//  Ternyata BUKAN dua tersangka di atas. mp3-nya terpasang benar dan
//  tombol suaranya menyala; yang salah adalah volume di sisi perangkat
//  (kekecilan), jadi suaranya memang diputar tapi tidak terdengar.
//  Pelajarannya: sebelum menyalahkan aset atau kode, pastikan dulu
//  volume perangkat - laporan ini memang tidak bisa melihat ke sana,
//  karena AudioListener.volume di dalam game bisa 1 sementara volume
//  media HP-nya nol.
//
//  CATATAN: file ini TIDAK memakai 'using System;'. Lihat CS0104 yang
//  baru kena di Tetris3D.Music.cs - System membuat nama pendek 'Object'
//  jadi ambigu. StringBuilder ditulis lengkap System.Text.StringBuilder.
// =====================================================================

public partial class Tetris3D
{
    // DIMATIKAN 8 September 2026 - urusan "pujian bisu" sudah tuntas, jadi
    // laporan ini tidak perlu lagi mengotori Console tiap sesi.
    //
    // SENGAJA TIDAK DIHAPUS: kalau nanti muncul lagi gejala audio senyap,
    // menyalakan satu centang jauh lebih cepat daripada menulis ulang alat
    // ukurnya. Isi laporannya juga masih relevan (tombol suara, listener,
    // AudioSource, klip prosedural, 7 mp3 pujian).
    //
    // JEBAKAN INSPECTOR - baca kalau laporannya MASIH muncul sesudah pull:
    // field ini sempat hidup dengan default 'true', jadi kalau SampleScene
    // pernah disimpan sesudah commit 0e4e8dea, nilai 'true' itu sudah
    // ter-serialize di GameObject Game dan akan MENGALAHKAN default 'false'
    // di bawah. Obatnya: buka SampleScene -> pilih GameObject Game ->
    // komponen Tetris3D -> hilangkan centang "Kubika Audio Report" ->
    // simpan scene. Sesudah itu tidak perlu disentuh lagi.
    public bool kubikaAudioReport = false;

    bool kadDone;
    float kadT0;

    // Dipanggil KubikaAudioDebugDriver.LateUpdate
    public void KadTickReport()
    {
        if (kadDone) return;
        if (!kubikaAudioReport) { kadDone = true; return; }
        if (sfx == null || sfxLong == null) return;        // SetupAudio() belum jalan
        if (kadT0 <= 0f) { kadT0 = Time.unscaledTime; return; }
        if (Time.unscaledTime - kadT0 < 1f) return;        // beri 1 detik supaya semua siap
        kadDone = true;

        bool bad = false;
        var sb = new System.Text.StringBuilder(1600);
        sb.AppendLine("===== LAPORAN AUDIO KUBIKA (sekali jalan) =====");

        // ---------- [1] TOMBOL ----------
        sb.AppendLine("[1] TOMBOL SUARA - nilai NYATA runtime, dari SampleScene (bukan default kode)");
        sb.AppendLine("    soundOn=" + soundOn + "  sfxOn=" + sfxOn + "  musicOn=" + musicOn);
        sb.AppendLine("    sfxVolume=" + sfxVolume.ToString("0.###") +
                      "  musicVolume=" + musicVolume.ToString("0.###"));
        if (!soundOn) { sb.AppendLine("    >>> PENYEBAB: soundOn=false -> Part3.Sfx() langsung return."); bad = true; }
        if (!sfxOn) { sb.AppendLine("    >>> PENYEBAB: sfxOn=false -> Part3.Sfx() langsung return."); bad = true; }
        if (sfxVolume <= 0.001f) { sb.AppendLine("    >>> PENYEBAB: sfxVolume=0 -> semua SFX diputar pada volume nol."); bad = true; }

        // ---------- [2] LISTENER & GLOBAL ----------
        sb.AppendLine("[2] AUDIOLISTENER & GLOBAL");
        AudioListener lis = UnityEngine.Object.FindFirstObjectByType<AudioListener>();
        sb.AppendLine("    AudioListener: " + (lis == null
            ? "TIDAK ADA"
            : lis.gameObject.name + " (enabled=" + lis.enabled + ")"));
        sb.AppendLine("    AudioListener.volume=" + AudioListener.volume.ToString("0.###") +
                      "  AudioListener.pause=" + AudioListener.pause);
        sb.AppendLine("    CATATAN: angka di atas TIDAK bisa melihat volume media perangkat.");
        sb.AppendLine("    Kalau semuanya OK tapi tetap tidak terdengar, cek volume HP dulu.");
        if (lis == null) { sb.AppendLine("    >>> PENYEBAB: tanpa AudioListener, TIDAK ADA suara apa pun yang bisa terdengar."); bad = true; }
        else if (!lis.enabled) { sb.AppendLine("    >>> PENYEBAB: AudioListener mati."); bad = true; }
        if (AudioListener.volume <= 0.001f) { sb.AppendLine("    >>> PENYEBAB: volume global 0 (ini TIDAK terlihat di Inspector Game)."); bad = true; }
        if (AudioListener.pause) { sb.AppendLine("    >>> PENYEBAB: audio global sedang dijeda."); bad = true; }

        // ---------- [3] AUDIOSOURCE ----------
        sb.AppendLine("[3] AUDIOSOURCE");
        sb.AppendLine("    sfx     vol=" + sfx.volume.ToString("0.###") + " mute=" + sfx.mute +
                      " pitch=" + sfx.pitch.ToString("0.##") + " playing=" + sfx.isPlaying);
        sb.AppendLine("    sfxLong vol=" + sfxLong.volume.ToString("0.###") + " mute=" + sfxLong.mute +
                      " pitch=" + sfxLong.pitch.ToString("0.##") + " playing=" + sfxLong.isPlaying);
        sb.AppendLine("    music   " + (music == null ? "NULL" :
                      "vol=" + music.volume.ToString("0.###") + " mute=" + music.mute +
                      " playing=" + music.isPlaying +
                      " clip=" + (music.clip == null ? "NULL" : music.clip.name)));
        sb.AppendLine("    muteUntil=" + muteUntil.ToString("0.##") +
                      " (now=" + Time.unscaledTime.ToString("0.##") + ")" +
                      "  goStingRunning=" + goStingRunning);
        if (sfx.mute || sfxLong.mute) { sb.AppendLine("    >>> PENYEBAB: AudioSource dalam keadaan mute."); bad = true; }
        if (sfx.volume <= 0.02f || sfxLong.volume <= 0.02f)
        {
            sb.AppendLine("    >>> PENYEBAB: volume AudioSource nol - ini sisa bug CoGameOverSting");
            sb.AppendLine("    >>> (koroutin mati kena StopAllCoroutines saat restart). Seharusnya sudah");
            sb.AppendLine("    >>> dijaga KmuTickAudioWatchdog: pastikan kubikaAudioHeal menyala.");
            bad = true;
        }
        if (goStingRunning) { sb.AppendLine("    >>> CATATAN: goStingRunning tersangkut true -> sting game over tidak akan bunyi."); bad = true; }

        // ---------- [4] KLIP PROSEDURAL ----------
        sb.AppendLine("[4] KLIP PROSEDURAL Part3 (null = SetupAudio gagal membangunnya)");
        sb.AppendLine("    rot=" + (sfxRotate != null) + " lock=" + (sfxLock != null) +
                      " drop=" + (sfxDrop != null) + " clear=" + (sfxClear != null));
        sb.AppendLine("    lvl=" + (sfxLevelUp != null) + " go=" + (sfxGameOver != null) +
                      " tick=" + (sfxTick != null) + " deny=" + (sfxDeny != null));

        // ---------- [5] SUARA PUJIAN ----------
        // Memakai KPR_VOICE_DIR & KPR_FILES MILIK Praise.cs, bukan daftar salinan,
        // supaya yang diuji benar-benar path yang dipakai kode sebenarnya.
        sb.AppendLine("[5] SUARA PUJIAN - Resources.Load<AudioClip>(\"" + KPR_VOICE_DIR + "<nama>\")");
        sb.AppendLine("    kubikaPraise=" + kubikaPraise + " kubikaPraiseVoice=" + kubikaPraiseVoice +
                      " kubikaVoiceVolume=" + kubikaVoiceVolume.ToString("0.##"));
        float effVoice = Mathf.Clamp01(sfxVolume * Mathf.Clamp(kubikaVoiceVolume, 0f, 1.5f));
        sb.AppendLine("    volume efektif suara pujian = " + effVoice.ToString("0.###"));
        sb.AppendLine("    kprVoice=" + (kprVoice == null ? "belum dibuat (normal sebelum pujian pertama)"
                      : "ada, mute=" + kprVoice.mute + " vol=" + kprVoice.volume.ToString("0.###")));

        int found = 0;
        for (int i = 0; i < KPR_FILES.Length; i++)
        {
            AudioClip c = Resources.Load<AudioClip>(KPR_VOICE_DIR + KPR_FILES[i]);
            if (c != null)
            {
                found++;
                sb.AppendLine("    OK      " + KPR_FILES[i] +
                              "  (" + c.length.ToString("0.00") + "s, ch=" + c.channels +
                              ", " + c.frequency + "Hz, load=" + c.loadType + ")");
            }
            else
            {
                sb.AppendLine("    HILANG  " + KPR_FILES[i]);
            }
        }
        sb.AppendLine("    terbaca " + found + " dari " + KPR_FILES.Length);

        if (!kubikaPraiseVoice) { sb.AppendLine("    >>> PENYEBAB: kubikaPraiseVoice=false di Inspector."); bad = true; }
        if (effVoice <= 0.001f) { sb.AppendLine("    >>> PENYEBAB: volume efektif 0 (sfxVolume atau kubikaVoiceVolume nol)."); bad = true; }
        if (found == 0)
        {
            sb.AppendLine("    >>> PENYEBAB: TIDAK SATU PUN mp3 terbaca. Yang dicari Unity:");
            sb.AppendLine("    >>>   Assets/Resources/" + KPR_VOICE_DIR + "good.mp3  (dan 6 lainnya)");
            sb.AppendLine("    >>> Folder harus PERSIS bernama 'Resources', mp3 LANGSUNG di dalam");
            sb.AppendLine("    >>> KubikaVoice/ tanpa subfolder, dan nama file huruf kecil semua.");
            bad = true;
        }
        else if (found < KPR_FILES.Length)
        {
            sb.AppendLine("    >>> Sebagian hilang: yang bertanda HILANG di atas akan diam saja,");
            sb.AppendLine("    >>> sementara teksnya tetap muncul.");
            bad = true;
        }

        sb.AppendLine("===== SELESAI - matikan lewat kubikaAudioReport =====");

        if (bad) Debug.LogWarning(sb.ToString());
        else Debug.Log(sb.ToString());
    }
}

// =====================================================================
//  DRIVER LAPORAN - order 25300
//  Paling belakang dari semua driver Kubika (Bg 25000, Fx 25100,
//  Balance 25150, Praise 25200, Music 25250) supaya yang terbaca adalah
//  keadaan SESUDAH semua pihak selesai menyentuh audio di frame itu.
//
//  Driver ini tetap hidup walau kubikaAudioReport=false: biayanya nol
//  karena KadTickReport() langsung menyetel kadDone dan berhenti pada
//  pemanggilan pertama.
// =====================================================================
[DefaultExecutionOrder(25300)]
public class KubikaAudioDebugDriver : MonoBehaviour
{
    Tetris3D game;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (UnityEngine.Object.FindFirstObjectByType<KubikaAudioDebugDriver>() != null) return;
        var go = new GameObject("KubikaAudioDebugDriver");
        DontDestroyOnLoad(go);
        go.AddComponent<KubikaAudioDebugDriver>();
    }

    void LateUpdate()
    {
        if (game == null) game = UnityEngine.Object.FindFirstObjectByType<Tetris3D>();
        if (game == null) return;
        game.KadTickReport();
    }
}
