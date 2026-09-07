using UnityEngine;

// =====================================================================
//  KUBIKA TOWER - BATCH D: MUSIK 96 BPM  (+ SELF-HEAL AUDIO)
// ---------------------------------------------------------------------
//  Sumber: KubikaBlast/Assets/Scripts/KubikaSfx.cs -> BuildMusic()
//
//  File TERPISAH (partial) - ADDITIF. Tidak ada satu baris pun di
//  Tetris3D.cs / Part2 / Part3 / Part4 yang diubah.
//
//  CATATAN 'using System;' - JANGAN DITAMBAHKAN LAGI DI FILE INI.
//  Versi pertama file ini mengimpor System untuk Func<>, dan itu membuat
//  nama pendek 'Object' jadi ambigu antara UnityEngine.Object dan
//  System.Object -> error CS0104 di KubikaMusicDriver. Sekarang Func
//  ditulis lengkap sebagai System.Func<> dan pemanggilan statisnya
//  ditulis UnityEngine.Object.FindFirstObjectByType, jadi aman dari dua
//  arah sekaligus.
//
//  == KENAPA MUSIK LAMA TERASA KOSONG ==
//  Part3.MakeMusic() memutar 16 nada dari tangga 6-nada dengan satu sinus
//  plus satu bass oktaf bawah. Tidak ada harmoni, tidak ada ketukan, dan
//  tidak ada BPM - panjangnya cuma 16 x 0.22 s = 3.5 detik, jadi telinga
//  langsung menangkap perulangannya dan terdengar seperti satu nada yang
//  mondar-mandir.
//
//  KubikaSfx.BuildMusic() menyusunnya sebagai MUSIK, bukan deret nada:
//    - 96 BPM, 4 bar, 4 ketukan (loop 10 detik)
//    - progresi akor C - G - Am - F (root MIDI 60, 55, 57, 53; bar ke-3 minor)
//    - TIGA lapis peran: bass di ketukan 1 & 3, pad triad yang menahan
//      seluruh bar, dan pluck seperdelapan satu oktaf di atas root
//    - dinormalisasi ke puncak 0.9 supaya tidak pernah clipping
//  Yang membuatnya terasa "ada lagunya" adalah pad-nya: itu yang mengikat
//  empat bar menjadi satu kalimat harmoni.
//
//  == KENAPA Part3.cs TETAP TIDAK PERLU DIUBAH ==
//  Part3.SetupAudio() menugaskan music.clip = musicClip SEKALI saja, dan
//  Part3.Update() sesudah itu hanya menyentuh music.volume / Play / Pause -
//  tidak pernah menugaskan ulang music.clip. Jadi menukar klipnya sekali di
//  frame pertama sudah cukup dan stabil. Konsekuensi kecil yang diterima:
//  klip lama tetap dibangun sekali di Start (~155 ribu sampel, beberapa
//  milidetik) lalu dibuang. Menghapusnya berarti mengedit Part3.cs, dan itu
//  harga yang lebih mahal daripada beberapa milidetik.
// =====================================================================

public partial class Tetris3D
{
    // ---------------- TOMBOL PENGATUR (field public BARU) ----------------
    public bool kubikaMusic = true;                      // false = pakai MakeMusic() lama
    [Range(60f, 140f)] public float kubikaMusicBpm = 96f; // KubikaSfx: 96
    public bool kubikaAudioHeal = true;                   // penjaga anti-bisu (lihat bawah)

    // Volume musik TIDAK ditambah field baru: musicVolume yang sudah ada tetap
    // dipakai Part3.Update() sebagai target fade. Dua sumber volume untuk satu
    // hal cuma bikin bingung nanti.

    const int KMU_RATE = 44100;
    const int KMU_BARS = 4;
    const int KMU_BEATS_PER_BAR = 4;
    // C mayor - G mayor - A minor - F mayor (nomor nada MIDI)
    static readonly int[] KMU_BAR_ROOT = { 60, 55, 57, 53 };
    static readonly bool[] KMU_BAR_MINOR = { false, false, true, false };
    // Tambahan di luar port: musik ini loop terus-menerus, dan sambungan buffer
    // yang nilainya tidak nol menghasilkan KLIK tiap 10 detik. 4 ms cukup untuk
    // menghilangkannya dan terlalu pendek untuk terdengar sebagai turun-volume.
    const float KMU_SEAM_FADE = 0.004f;

    bool kmuSwapped;
    AudioClip kmuClip;

    // ================== LOOP (dipanggil KubikaMusicDriver.LateUpdate) ==================
    public void TickKubikaMusic()
    {
        if (!kmuSwapped && kubikaMusic && music != null)
        {
            kmuSwapped = true;   // ditandai LEBIH DULU: kalau gagal, jangan dicoba tiap frame
            KmuSwapMusic();
        }

        if (kubikaAudioHeal) KmuTickAudioWatchdog();
    }

    void KmuSwapMusic()
    {
        kmuClip = KmuBuildMusic();
        if (kmuClip == null || music == null) return;

        bool wasPlaying = music.isPlaying;
        float vol = music.volume;
        music.Stop();
        music.clip = kmuClip;
        music.loop = true;
        music.volume = vol;
        // Kalau tadi sedang berbunyi, lanjutkan. Kalau tidak, Part3.Update() yang
        // akan memutarnya sendiri begitu soundOn && musicOn && !gameOver.
        if (wasPlaying) music.Play();
    }

    // ================== PENJAGA ANTI-BISU ==================
    // AKAR MASALAH "SFX tiba-tiba tidak berbunyi lagi":
    // Part3.CoGameOverSting() meredam sfx.volume dan sfxLong.volume ke 0, lalu
    // memulihkannya ke nilai awal di akhir koroutin. Tapi Part2.ClearBoard()
    // memanggil StopAllCoroutines(). Kalau pemain menekan MAIN LAGI atau KE MENU
    // di dalam jendela ~0,3 detik itu, koroutinnya MATI DI TENGAH FADE dan
    // meninggalkan dua warisan:
    //   1. sfx.volume = 0 dan sfxLong.volume = 0. SetupAudio() hanya jalan sekali
    //      di Start(), jadi TIDAK ADA apa pun yang memulihkannya - seluruh SFX
    //      bisu sampai aplikasi ditutup, walaupun tombol SUARA tetap menyala.
    //   2. goStingRunning = true selamanya -> Sfx(sfxGameOver) langsung return,
    //      jadi sting game over tidak pernah berbunyi lagi.
    // Ini pola yang sama dengan hit-stop di Batch F: keadaan sementara yang
    // dipegang sebuah koroutin SELALU butuh penjaga di luar koroutin itu.
    //
    // Penjaganya sengaja pelit supaya tidak merebut milik pihak lain:
    //   - hanya kalau volume benar-benar <= 0.02 (satu-satunya sisa yang mungkin
    //     ditinggalkan fade tadi; peredaman wajar tidak pernah sedalam itu)
    //   - hanya di dalam ronde yang hidup (started && !gameOver)
    //   - hanya lewat muteUntil + 1 detik, supaya sting yang SAH tidak diganggu
    //   - tidak pernah saat iklan tampil: SDK iklan lazim meredam audio game
    //     sendiri, dan itu memang haknya.
    void KmuTickAudioWatchdog()
    {
        // Suara pujian Batch H hanya memeriksa sfxOn, sedangkan Part3.Sfx()
        // memeriksa soundOn && sfxOn. Tanpa baris ini, mematikan soundOn
        // membungkam semua SFX tapi pujian tetap bicara. Dipaksa dari sini
        // supaya Tetris3D.Praise.cs tidak perlu ikut diubah.
        if (kprVoice != null)
        {
            bool wantVoice = soundOn && sfxOn;
            if (kprVoice.mute == wantVoice) kprVoice.mute = !wantVoice;
            if (!wantVoice && kprVoice.isPlaying) kprVoice.Stop();
        }

        if (AdFullscreenShowing || AdLoadingActive) return;
        if (!started || gameOver) return;
        if (Time.unscaledTime < muteUntil + 1f) return;

        if (sfx != null && sfx.volume <= 0.02f) { sfx.volume = 1f; sfx.pitch = 1f; }
        if (sfxLong != null && sfxLong.volume <= 0.02f) { sfxLong.volume = 1f; sfxLong.pitch = 1f; }
        if (goStingRunning) goStingRunning = false;
    }

    // ================== MUSIK (port KubikaSfx.BuildMusic) ==================
    AudioClip KmuBuildMusic()
    {
        float bpm = Mathf.Clamp(kubikaMusicBpm, 60f, 140f);
        float beat = 60f / bpm;
        int totalBeats = KMU_BARS * KMU_BEATS_PER_BAR;
        int count = Mathf.CeilToInt(totalBeats * beat * KMU_RATE);
        if (count < 2) return null;
        float[] buf = new float[count];

        for (int bar = 0; bar < KMU_BARS; bar++)
        {
            int root = KMU_BAR_ROOT[bar];
            int[] triad = KMU_BAR_MINOR[bar] ? new[] { 0, 3, 7 } : new[] { 0, 4, 7 };

            for (int b = 0; b < KMU_BEATS_PER_BAR; b++)
            {
                int beatIndex = bar * KMU_BEATS_PER_BAR + b;
                float tStart = beatIndex * beat;

                // Bass hanya di ketukan 1 & 3. Inilah yang membuat 96 BPM-nya
                // TERASA sebagai ketukan, bukan cuma nada yang berganti.
                if (b == 0 || b == 2)
                    KmuAddNote(buf, tStart, beat * 0.95f, KmuMidi(root - 12), 0.18f, KmuVoiceBass);

                // Pad menahan seluruh bar - pengikat harmoninya.
                for (int i = 0; i < triad.Length; i++)
                    KmuAddNote(buf, tStart, beat, KmuMidi(root + triad[i]), 0.045f, KmuVoicePad);

                // Pluck seperdelapan, satu oktaf di atas root, memutari triad.
                for (int e = 0; e < 2; e++)
                {
                    int step = beatIndex * 2 + e;
                    int deg = triad[step % 3];
                    KmuAddNote(buf, tStart + e * beat * 0.5f, beat * 0.5f * 0.95f,
                               KmuMidi(root + 12 + deg), 0.11f, KmuVoicePluck);
                }
            }
        }

        // Normalisasi puncak (KubikaSfx: 0.9). Tiga lapis suara yang menumpuk bisa
        // melewati 1.0 dan hasilnya clipping kasar, bukan sekadar terlalu keras.
        float peak = 0f;
        for (int i = 0; i < count; i++) peak = Mathf.Max(peak, Mathf.Abs(buf[i]));
        if (peak > 0.9f)
        {
            float k = 0.9f / peak;
            for (int i = 0; i < count; i++) buf[i] *= k;
        }

        // Fade sambungan loop (lihat KMU_SEAM_FADE).
        int fade = Mathf.Clamp(Mathf.RoundToInt(KMU_SEAM_FADE * KMU_RATE), 1, count / 4);
        for (int i = 0; i < fade; i++)
        {
            float k = i / (float)fade;
            buf[i] *= k;
            buf[count - 1 - i] *= k;
        }

        AudioClip clip = AudioClip.Create("bgm_kubika_96", count, 1, KMU_RATE, false);
        clip.SetData(buf, 0);
        return clip;
    }

    // System.Func ditulis lengkap: lihat catatan di kepala file soal CS0104.
    static void KmuAddNote(float[] buf, float startSec, float durSec, float freq, float amp,
                           System.Func<float, float, float, float> voice)
    {
        int start = Mathf.RoundToInt(startSec * KMU_RATE);
        int len = Mathf.RoundToInt(durSec * KMU_RATE);
        for (int i = 0; i < len; i++)
        {
            int idx = start + i;
            if (idx < 0 || idx >= buf.Length) continue;
            buf[idx] += voice(freq, i / (float)KMU_RATE, durSec) * amp;
        }
    }

    // Petikan: serangan cepat, lepas cepat.
    static float KmuVoicePluck(float f, float lt, float dur)
    {
        float env = Mathf.Exp(-lt * 7f) * (1f - Mathf.Exp(-lt * 400f));
        return (KmuSine(f, lt) * 0.7f + KmuSine(2f * f, lt) * 0.18f + KmuTri(f, lt) * 0.12f) * env;
    }

    // Bass: lebih panjang, harmonik ganjil dari gelombang segitiga biar berisi.
    static float KmuVoiceBass(float f, float lt, float dur)
    {
        float env = Mathf.Exp(-lt * 3.2f) * (1f - Mathf.Exp(-lt * 250f));
        return (KmuSine(f, lt) * 0.8f + KmuTri(f, lt) * 0.2f) * env;
    }

    // Pad: naik 0.12 s, turun 0.18 s - menahan bar tanpa menutupi pluck.
    static float KmuVoicePad(float f, float lt, float dur)
    {
        float atk = Mathf.Clamp01(lt / 0.12f);
        float rel = Mathf.Clamp01((dur - lt) / 0.18f);
        return (KmuSine(f, lt) * 0.6f + KmuSine(2f * f, lt) * 0.15f) * atk * rel;
    }

    // Nama diberi awalan Kmu supaya tidak pernah bentrok dengan pembantu bernama
    // sama di file partial lain yang belum diaudit.
    static float KmuMidi(int m) { return 440f * Mathf.Pow(2f, (m - 69) / 12f); }
    static float KmuSine(float f, float t) { return Mathf.Sin(2f * Mathf.PI * f * t); }
    static float KmuTri(float f, float t) { return 2f * Mathf.Abs(2f * (t * f - Mathf.Floor(t * f + 0.5f))) - 1f; }
}

// =====================================================================
//  DRIVER MUSIK - order 25250
//  Paling akhir dari seluruh driver Kubika (Bg 25000, Fx 25100,
//  Balance 25150, Praise 25200): penjaga audio harus melihat keadaan
//  SESUDAH semua pihak lain selesai menyentuh AudioSource di frame itu.
//  Tidak punya OnGUI - batch ini tidak menggambar apa pun.
// =====================================================================
[DefaultExecutionOrder(25250)]
public class KubikaMusicDriver : MonoBehaviour
{
    Tetris3D game;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (UnityEngine.Object.FindFirstObjectByType<KubikaMusicDriver>() != null) return;
        var go = new GameObject("KubikaMusicDriver");
        DontDestroyOnLoad(go);
        go.AddComponent<KubikaMusicDriver>();
    }

    void LateUpdate()
    {
        if (game == null) game = UnityEngine.Object.FindFirstObjectByType<Tetris3D>();
        if (game == null) return;
        game.TickKubikaMusic();
    }
}
