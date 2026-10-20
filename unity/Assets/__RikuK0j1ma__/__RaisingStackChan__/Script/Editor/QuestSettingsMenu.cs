using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;

namespace RikuK0j1ma.RaisingStackChan.Editor
{
    /// <summary>
    /// Applies the Android / Quest player settings the project needs.
    /// Meta-specific settings (OpenXR features, permissions) are not touched here;
    /// use Meta XR Project Setup Tool for those.
    /// Menu: Tools/RaisingStackChan/Apply Quest Settings
    /// </summary>
    public static class QuestSettingsMenu
    {
        private const string PackageName = "com.rikuk0j1ma.raisingstackchan";

        [MenuItem("Tools/RaisingStackChan/Apply Quest Settings")]
        public static void Apply()
        {
            var target = NamedBuildTarget.Android;
            PlayerSettings.SetScriptingBackend(target, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.SetApplicationIdentifier(target, PackageName);
            PlayerSettings.SetManagedStrippingLevel(target, ManagedStrippingLevel.Low);
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });
            AssetDatabase.SaveAssets();
            Debug.Log("[QuestSettings] Applied IL2CPP / ARM64 / minSdk 29 / Vulkan / Linear. Run Meta XR Project Setup Tool for the rest.");
        }

        [MenuItem("Tools/RaisingStackChan/Switch Build Target to Android")]
        public static void SwitchToAndroid()
        {
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
        }
    }
}
