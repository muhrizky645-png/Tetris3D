#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.PackageManager;
using System;
using UnityEngine;

[InitializeOnLoad]
public class AnimationDependencyInstaller
{
    private static string ProjectSpecificKey
    {
        get { return "2DAnim_Package_Dependency_Checked_" + Application.dataPath.GetHashCode(); }
    }
	
    static AnimationDependencyInstaller()
    {
        if (EditorPrefs.GetBool(ProjectSpecificKey, false))
            return;

        EditorApplication.delayCall += CheckAndInstall;
    }

    private static void CheckAndInstall()
    {
        Type spriteSkinType = Type.GetType("UnityEngine.U2D.Animation.SpriteSkin, Unity.2D.Animation.Runtime");

        if (spriteSkinType == null)
        {
            bool userWantsToInstall = EditorUtility.DisplayDialog(
                "Package Dependencies",
                "Hi, our package requires Unity's official '2D Animation' package to play the bone animations correctly.\n\nWould you like to install it automatically now?",
                "Yes, Install Now",
                "No, I'll do it myself later"
            );

            if (userWantsToInstall)
            {
                Debug.Log("Installing '2D Animation' package via Package Manager...");
                Client.Add("com.unity.2d.animation");
            }
            else
            {
                Debug.LogWarning("2D Animation installation was skipped. Animations may not work until you install it manually.");
            }
        }

        EditorPrefs.SetBool(ProjectSpecificKey, true);
    }
}
#endif