# KUBIKA TOWER 3D - Setup Build di Codemagic (APK universal + AAB)
# Tanpa perlu install Unity di komputer

Project Unity **6000.5.8f1**, lisensi **Personal (gratis)**. Dua file di repo `Tetris3D`:
- `codemagic.yaml` (root repo) - berisi 2 workflow: `unity-activation` & `kubika-android`
- `Assets/Editor/BuildScript.cs` (entry point build; folder `Editor` = editor-only)

> Catatan penting: Unity Personal WAJIB aktivasi manual sekali lewat web (aturan Unity, tidak bisa
> full-otomatis untuk lisensi gratis). Tapi semua bisa dilakukan dari HP/browser - tidak perlu
> install Unity di komputer.

## LANGKAH 1 - Hubungkan repo ke Codemagic
1. Login https://codemagic.io -> Add application -> GitHub -> repo `muhrizky645-png/Tetris3D`.
2. Pilih "I have a codemagic.yaml". Akan muncul 2 workflow.

## LANGKAH 2 - Grup variabel `unity` (buat dulu, minimal UNITY_HOME)
Application settings -> Environment variables -> group name: **unity**
| Variabel | Isi |
|----------|-----|
| `UNITY_HOME` | Path Unity di mesin Codemagic, mis. `/Applications/Unity/Hub/Editor/6000.5.8f1/Unity.app` |

> Kalau versi 6000.5.8f1 belum tersedia di Codemagic, pakai versi Unity 6 terdekat & sesuaikan path.

## LANGKAH 3 - Buat file aktivasi .alf (jalankan workflow `unity-activation`)
1. Start build -> pilih workflow **`(1x) Unity Personal - Buat file aktivasi .alf`**.
2. Setelah selesai, di halaman build ada artefak berakhiran **`.alf`** -> download (bisa dari HP).

## LANGKAH 4 - Aktivasi manual di web -> dapat file .ulf
1. Buka https://license.unity3d.com/manual (browser HP juga bisa).
2. Upload file `.alf` tadi.
3. Pilih **Unity Personal** (Personal Edition) -> Next -> download file **`.ulf`**.

## LANGKAH 5 - Masukkan isi .ulf ke variabel `UNITY_ULF`
File `.ulf` itu teks XML biasa. Buka dengan aplikasi teks / browser, **copy semua isinya**.
Di grup `unity` tambahkan:
| Variabel | Isi |
|----------|-----|
| `UNITY_ULF` | Tempel SELURUH isi file `.ulf` (XML) *(tandai secure)* |

> Alternatif: kalau lebih suka base64, pakai `UNITY_ULF_B64` (isi file .ulf yang di-base64). yaml mendukung keduanya.

## LANGKAH 6 - Grup variabel `keystore` (untuk menandatangani AAB)
group name: **keystore**
| Variabel | Isi |
|----------|-----|
| `CM_KEYSTORE` | file `.keystore/.jks` yang sudah di-base64 *(secure)* |
| `CM_KEYSTORE_PASSWORD` | password keystore *(secure)* |
| `CM_KEY_ALIAS` | nama alias key |
| `CM_KEY_PASSWORD` | password alias *(secure)* |

Belum punya keystore? Buat sekali (SIMPAN baik-baik - kalau hilang tidak bisa update app di Play):
```
keytool -genkey -v -keystore kubika.keystore -alias kubika -keyalg RSA -keysize 2048 -validity 10000
```
> keytool ada di paket Java/Android Studio. Kalau tidak mau ribet keystore dulu, workflow tetap jalan
> tapi hasilnya debug-signed (cukup untuk tes install ke HP; TIDAK bisa diupload ke Play Store).

## LANGKAH 7 - Build!
1. Start build -> workflow **`KUBIKA Tower 3D - Android (APK + AAB)`**.
2. Artefak hasil:
   - `build/android/kubika-tower-universal.apk` (tes install langsung ke HP)
   - `build/android/kubika-tower.aab` (upload ke Google Play Console)
3. Link download muncul di halaman build + dikirim ke email `rizky.saja059@gmail.com`.

## Ringkas
- Sekali setup: LANGKAH 1-6. Selanjutnya tiap mau rilis: cukup LANGKAH 7 (Start build).
- File `.ulf` Personal bisa dipakai berkali-kali; tidak perlu ulang aktivasi.

## Catatan teknis
- **Universal APK** = IL2CPP + `ARMv7 | ARM64` dalam satu APK.
- **min SDK 26**, target SDK Auto.
- `bundleVersionCode` otomatis dari nomor build Codemagic; `VERSION_NAME` di `codemagic.yaml`.
- Output tunggal: ganti `-executeMethod BuildScript.BuildAndroid` jadi `BuildScript.BuildApk` / `BuildScript.BuildAab`.
- Build pertama Unity 6 + IL2CPP bisa 10-25 menit (kompilasi native).
