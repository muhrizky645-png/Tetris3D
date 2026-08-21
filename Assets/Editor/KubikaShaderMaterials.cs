#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Membuat material URP sebagai ASET nyata di folder Resources.
// Tujuan: apa pun yang ada di Resources dijamin ikut ke build, jadi Unity
// PASTI mengkompilasi varian shader-nya untuk platform target (Android /
// Vulkan / GLES) dan TIDAK ikut ke-strip walau "Strip Unused Variants" ON.
//
// Ini memperbaiki balok & silinder yang jadi magenta/hilang, karena di
// Tetris3D.cs material dibuat saat runtime lewat Shader.Find(...) sehingga
// build optimizer tidak bisa mendeteksinya. Dengan material kembar sebagai
// aset, varian yang sama tetap dibangun ke APK.
//
// Material dibuat OTOMATIS saat Unity selesai compile, dan bisa juga
// dijalankan manual lewat menu: Tools > Kubika > Buat Material Shader.
public static class KubikaShaderMaterials
{
    const string ResFolder = "Assets/Resources";
    const string MatFolder = "Assets/Resources/KubikaMats";

    [InitializeOnLoadMethod]
    static void AutoEnsure()
    {
        // Ditunda agar AssetDatabase siap setelah domain reload.
        EditorApplication.delayCall += EnsureMaterials;
    }

    [MenuItem("Tools/Kubika/Buat Material Shader (fix magenta)")]
    public static void EnsureMaterials()
    {
        if (!AssetDatabase.IsValidFolder(ResFolder))
            AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder(MatFolder))
            AssetDatabase.CreateFolder(ResFolder, "KubikaMats");

        bool changed = false;
        changed |= CreateBlockLit();
        changed |= CreateBgUnlit();
        changed |= CreateRingUnlit();
        changed |= CreateParticleUnlit();

        if (changed)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Kubika] Material shader Resources dibuat/diperbarui di " + MatFolder);
        }
    }

    static Material Get(string name) =>
        AssetDatabase.LoadAssetAtPath<Material>(MatFolder + "/" + name + ".mat");

    // URP/Lit dengan emission ON -> dipakai balok & silinder (MakeMat di game).
    static bool CreateBlockLit()
    {
        if (Get("BlockLit") != null) return false;
        Shader sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh == null) return false;
        var m = new Material(sh);
        m.EnableKeyword("_EMISSION");
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", Color.white);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0.35f);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.9f);
        AssetDatabase.CreateAsset(m, MatFolder + "/BlockLit.mat");
        return true;
    }

    // URP/Unlit opaque -> background gradient (bgMat di game).
    static bool CreateBgUnlit()
    {
        if (Get("BgUnlit") != null) return false;
        Shader sh = Shader.Find("Universal Render Pipeline/Unlit");
        if (sh == null) return false;
        AssetDatabase.CreateAsset(new Material(sh), MatFolder + "/BgUnlit.mat");
        return true;
    }

    // URP/Unlit transparent -> kill ring (MakeUnlitTransparent di game).
    static bool CreateRingUnlit()
    {
        if (Get("RingUnlit") != null) return false;
        Shader sh = Shader.Find("Universal Render Pipeline/Unlit");
        if (sh == null) return false;
        var m = new Material(sh);
        if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
        if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 0f);
        if (m.HasProperty("_SrcBlend")) m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        if (m.HasProperty("_DstBlend")) m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        if (m.HasProperty("_ZWrite")) m.SetInt("_ZWrite", 0);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)RenderQueue.Transparent;
        AssetDatabase.CreateAsset(m, MatFolder + "/RingUnlit.mat");
        return true;
    }

    // URP/Particles/Unlit -> partikel efek (particleMat di game).
    static bool CreateParticleUnlit()
    {
        if (Get("ParticleUnlit") != null) return false;
        Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (sh == null) return false;
        AssetDatabase.CreateAsset(new Material(sh), MatFolder + "/ParticleUnlit.mat");
        return true;
    }
}
#endif
