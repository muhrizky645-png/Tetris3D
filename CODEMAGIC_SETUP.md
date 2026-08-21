# KUBIKA TOWER 3D - Setup Build di Codemagic (APK universal + AAB)
# Tanpa perlu install Unity di komputer

Project Unity **6000.5.8f1**, lisensi **Personal (gratis)**. Dua file di repo `Tetris3D`:
- `codemagic.yaml` (root repo) - berisi 2 workflow: `unity-activation` & `kubika-android`
- `Assets/Editor/BuildScript.cs` (entry point build; folder `Editor` = editor-only)

> Catatan penting:
> 1. Unity 6000.5.8f1 TIDAK pre-installed di Codemagic, jadi yaml meng-INSTALL-nya otomatis saat build
>    (baca versi dari ProjectVersion.txt). `UNITY_HOME` terisi sendiri - kamu TIDAK perlu mengetiknya.
> 2. Unity Personal WAJIB aktivasi manual sekali lewat web (aturan Unity). Semua bisa dari HP/browser.

## LANGKAH 1 - Hubungkan repo ke Codemagic
1. Login https://codemagic.io -> Add application -> pilih **Unity** -> repo `muhrizky645-png/Tetris3D`.
2. Tab codemagic.yaml -> "Check for configuration files" -> muncul 2 workflow.

## LANGKAH 2 - Buat file aktivasi .alf (workflow `unity-activation`)
TIDAK perlu variabel apa pun untuk langkah ini.
1. Start build -> pilih workflow **`(1x) Unity Personal - Buat file aktivasi .alf`**.
2. Build akan meng-install Unity dulu (agak lama, sekali ini saja), lalu menghasilkan file `.alf`.
3. Di halaman build, download artefak berakhiran **`.alf`** (bisa dari HP).

## LANGKAH 3 - Aktivasi manual di web -> dapat file .ulf
1. Buka https://license.unity3d.com/manual (browser HP juga bisa).
2. Upload file `.alf` tadi.
3. Pilih **Unity Personal** (Personal Edition) -> Next -> download file **`.ulf`**.

## LANGKAH 4 - Buat grup variabel `unity` + isi UNITY_ULF
Tab **Environment variables** -> group name: **unity**
| Variabel | Isi | Secret? |
|----------|-----|---------|
| `UNITY_ULF` | Tempel SELURUH isi file `.ulf` (teks XML) | ya |

File `.ulf` itu teks XML biasa - buka pakai aplikasi teks/browser, copy semua, tempel ke value.
> Alternatif: pakai `UNITY_ULF_B64` (isi .ulf yang di-base64). yaml mendukung keduanya.

## LANGKAH 5 - Grup variabel `keystore` (untuk menandatangani AAB)
group name: **keystore**
| Variabel | Isi | Secret? |
|----------|-----|---------|
| `CM_KEYSTORE` | file `.keystore/.jks` yang sudah di-base64 | ya |
| `CM_KEYSTORE_PASSWORD` | password keystore | ya |
| `CM_KEY_ALIAS` | nama alias key | - |
| `CM_KEY_PASSWORD` | password alias | ya |

Belum punya keystore? Buat sekali (SIMPAN baik-baik - kalau hilang tidak bisa update app di Play):
```
keytool -genkey -v -keystore kubika.keystore -alias kubika -keyalg RSA -keysize 2048 -validity 10000
```
> Tanpa keystore, workflow tetap jalan tapi hasilnya debug-signed (cukup untuk tes install ke HP;
> TIDAK bisa diupload ke Play Store).

## LANGKAH 6 - Build!
1. Start build -> workflow **`KUBIKA Tower 3D - Android (APK + AAB)`**.
2. Artefak hasil:
   - `build/android/kubika-tower-universal.apk` (tes install langsung ke HP)
   - `build/android/kubika-tower.aab` (upload ke Google Play Console)
3. Link download muncul di halaman build + dikirim ke email `rizky.saja059@gmail.com`.

## Ringkas
- Sekali setup: LANGKAH 1-5. Selanjutnya tiap mau rilis: cukup LANGKAH 6 (Start build).
- File `.ulf` Personal bisa dipakai berkali-kali; tidak perlu ulang aktivasi.

## Catatan teknis
- Unity + modul Android (SDK/NDK/OpenJDK) di-install otomatis dari ProjectVersion.txt (versi 6000.5.8f1, changeset 5cb7df797b7d).
- **Universal APK** = IL2CPP + `ARMv7 | ARM64` dalam satu APK. **min SDK 26**, target SDK Auto.
- `bundleVersionCode` otomatis dari nomor build Codemagic; `VERSION_NAME` di `codemagic.yaml`.
- Output tunggal: ganti `-executeMethod BuildScript.BuildAndroid` jadi `BuildScript.BuildApk` / `BuildScript.BuildAab`.
- Build pertama (install Unity + IL2CPP) bisa 20-40 menit. Build berikutnya lebih cepat.
