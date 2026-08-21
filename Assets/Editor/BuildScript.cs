#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// KUBIKA TOWER 3D — Android build entry points for CI (Codemagic).
// Menghasilkan: universal APK (ARMv7 + ARM64, IL2CPP) DAN Android App Bundle (.aab).
// Dipanggil via: Unity -batchmode -quit -executeMethod BuildScript.BuildAndroid
public static class BuildScript
{
    const string OutputDir = "build/android";
    const string ApkName   = "kubika-tower-universal.apk";
    const string AabName   = "kubika-tower.aab";

    // Entry point utama: build APK universal + AAB dalam satu sesi Unity.
    public static void BuildAndroid()
    {
        try
        {
            PrepareAndroidSettings();
            var scenes = GetEnabledScenes();
            Directory.CreateDirectory(OutputDir);

            // 1) Universal APK (satu APK berisi ARMv7 + ARM64)
            EditorUserBuildSettings.buildAppBundle = false;
            BuildOne(scenes, Path.Combine(OutputDir, ApkName), "Universal APK");

            // 2) Android App Bundle (untuk Google Play)
            EditorUserBuildSettings.buildAppBundle = true;
            BuildOne(scenes, Path.Combine(OutputDir, AabName), "AAB");
        }
        catch (Exception e)
        {
            Console.WriteLine("[BuildScript] Build GAGAL: " + e);
            EditorApplication.Exit(1);
        }
    }

    // Entry point opsional kalau mau salah satu saja.
    public static void BuildApk()
    {
        PrepareAndroidSettings();
        Directory.CreateDirectory(OutputDir);
        EditorUserBuildSettings.buildAppBundle = false;
        BuildOne(GetEnabledScenes(), Path.Combine(OutputDir, ApkName), "Universal APK");
    }

    public static void BuildAab()
    {
        PrepareAndroidSettings();
        Directory.CreateDirectory(OutputDir);
        EditorUserBuildSettings.buildAppBundle = true;
        BuildOne(GetEnabledScenes(), Path.Combine(OutputDir, AabName), "AAB");
    }

    static void BuildOne(string[] scenes, string outputPath, string label)
    {
        if (scenes == null || scenes.Length == 0)
            throw new Exception("Tidak ada scene untuk di-build. Tambahkan scene di Build Settings.");

        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = outputPath,
            target = BuildTarget.Android,
            targetGroup = BuildTargetGroup.Android,
            options = BuildOptions.None,
        };

        Console.WriteLine($"[BuildScript] Membangun {label} -> {outputPath}");
        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;
        if (summary.result != BuildResult.Succeeded)
            throw new Exception($"{label} gagal: result={summary.result}, errors={summary.totalErrors}");

        Console.WriteLine($"[BuildScript] {label} OK: {summary.totalSize} bytes -> {outputPath}");
    }

    static void PrepareAndroidSettings()
    {
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);

        // Application id (opsional dari env). Default tetap pakai setting project.
        var pkg = Environment.GetEnvironmentVariable("PACKAGE_NAME");
        if (!string.IsNullOrEmpty(pkg))
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, pkg);

        // IL2CPP + ARMv7 + ARM64 => APK universal & AAB kompatibel Play Store.
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARMv7 | AndroidArchitecture.ARM64;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
        PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;

        // Version code / name dari env CI (opsional).
        var buildNumber = Environment.GetEnvironmentVariable("CM_BUILD_NUMBER")
                          ?? Environment.GetEnvironmentVariable("BUILD_NUMBER");
        if (int.TryParse(buildNumber, out var vc) && vc > 0)
            PlayerSettings.Android.bundleVersionCode = vc;

        var versionName = Environment.GetEnvironmentVariable("VERSION_NAME");
        if (!string.IsNullOrEmpty(versionName))
            PlayerSettings.bundleVersion = versionName;

        // Keystore signing dari env CI.
        var keystorePath  = Environment.GetEnvironmentVariable("CM_KEYSTORE_PATH");
        var keystorePass  = Environment.GetEnvironmentVariable("CM_KEYSTORE_PASSWORD");
        var keyAlias      = Environment.GetEnvironmentVariable("CM_KEY_ALIAS");
        var keyAliasPass  = Environment.GetEnvironmentVariable("CM_KEY_PASSWORD");

        if (!string.IsNullOrEmpty(keystorePath) && File.Exists(keystorePath)
            && !string.IsNullOrEmpty(keystorePass)
            && !string.IsNullOrEmpty(keyAlias)
            && !string.IsNullOrEmpty(keyAliasPass))
        {
            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = keystorePath;
            PlayerSettings.Android.keystorePass = keystorePass;
            PlayerSettings.Android.keyaliasName = keyAlias;
            PlayerSettings.Android.keyaliasPass = keyAliasPass;
            Console.WriteLine("[BuildScript] Memakai custom keystore untuk signing rilis.");
        }
        else
        {
            Console.WriteLine("[BuildScript] PERINGATAN: env keystore belum lengkap; hasil pakai debug signing. AAB untuk Play Store WAJIB ditandatangani rilis.");
        }
    }

    static string[] GetEnabledScenes()
    {
        var scenes = EditorBuildSettings.scenes
            .Where(s => s.enabled && !string.IsNullOrEmpty(s.path))
            .Select(s => s.path)
            .ToArray();

        if (scenes.Length > 0) return scenes;

        Console.WriteLine("[BuildScript] Build Settings kosong; fallback ke semua .unity di Assets/.");
        return Directory.GetFiles("Assets", "*.unity", SearchOption.AllDirectories)
            .Select(p => p.Replace('\\', '/'))
            .OrderBy(p => p)
            .ToArray();
    }
}
#endif
