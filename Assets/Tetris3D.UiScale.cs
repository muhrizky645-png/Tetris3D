using UnityEngine;

// =====================================================================
//  KUBIKA TOWER - SKALA UI RESPONSIF (reference-resolution scaling)
// ---------------------------------------------------------------------
//  Semua UI IMGUI (OnGUI) digambar di "ruang logis" selebar 720 px,
//  lalu diskalakan ke lebar layar ASLI lewat GUI.matrix. Jadi ukuran
//  tombol/teks tampil dengan PROPORSI yang SAMA di semua HP - persis
//  seperti tampilan pada layar 720 lebar (potret 1280x720).
//
//  Contoh faktor skala:
//    layar 720  -> 1.0x  (acuan)
//    layar 1080 -> 1.5x
//    layar 1440 -> 2.0x
//
//  Cara pakai di tiap OnGUI:
//    1. Panggil ApplyUiScale() di baris PALING ATAS.
//    2. Untuk layout, pakai VW & VH (bukan Screen.width / Screen.height).
//
//  Input (klik/tekan) otomatis ikut ter-transform oleh GUI.matrix,
//  jadi hit-test tombol tetap akurat tanpa perubahan lain.
//
//  File TERPISAH (partial) - additive, tidak mengubah logika gameplay.
// =====================================================================

public partial class Tetris3D
{
    // Lebar acuan desain. 720 = lebar potret "1280x720" yang proporsinya pas.
    public const float UI_REF_WIDTH = 720f;

    // Faktor skala = lebar layar asli / lebar acuan.
    public float UiScale
    {
        get { float w = Screen.width; return w <= 1f ? 1f : w / UI_REF_WIDTH; }
    }

    // Lebar logis (selalu 720) & tinggi logis (ikut rasio layar).
    public float VW { get { return UI_REF_WIDTH; } }
    public float VH { get { return Screen.height / UiScale; } }

    // Panggil di awal SETIAP OnGUI sebelum menggambar apa pun.
    public void ApplyUiScale()
    {
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity,
            new Vector3(UiScale, UiScale, 1f));
    }
}
