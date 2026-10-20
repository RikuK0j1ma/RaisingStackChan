using System.IO;
using System.Linq;
using RikuK0j1ma.RaisingStackChan.Context;
using RikuK0j1ma.RaisingStackChan.Entity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer.Unity;

namespace RikuK0j1ma.RaisingStackChan.Editor
{
    /// <summary>
    /// One-click generation of RootLTS prefab, EatConfig asset, Boot / MR scenes,
    /// the Build Settings entries and the VContainerSettings link.
    /// Menu: RaisingStackChan/Setup DI Scenes
    /// </summary>
    public static class SceneSetupMenu
    {
        private const string Root = "Assets/__RikuK0j1ma__/__RaisingStackChan__";
        private const string PrefabPath = Root + "/Prefab/RootLTS.prefab";
        private const string ConfigPath = Root + "/Data/EatConfig.asset";
        private const string BootScenePath = Root + "/Scene/Boot.unity";
        private const string MrScenePath = Root + "/Scene/MR.unity";

        [MenuItem("RaisingStackChan/Setup DI Scenes")]
        public static void Setup()
        {
            var config = CreateConfig();
            var rootPrefab = CreateRootPrefab();
            LinkVContainerSettings(rootPrefab);
            CreateBootScene();
            CreateMrScene(config);
            RegisterBuildScenes();
            AssetDatabase.SaveAssets();
            Debug.Log("[SceneSetupMenu] Done. Open Boot scene and press Play.");
        }

        private static EatConfig CreateConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<EatConfig>(ConfigPath);
            if (config != null) return config;
            config = ScriptableObject.CreateInstance<EatConfig>();
            AssetDatabase.CreateAsset(config, ConfigPath);
            return config;
        }

        private static GameObject CreateRootPrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing != null) return existing;
            var go = new GameObject("RootLTS");
            go.AddComponent<RootLifetimeScope>();
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static void LinkVContainerSettings(GameObject rootPrefab)
        {
            // VContainerSettings is created by Assets > Create > VContainer > VContainerSettings.
            var guid = AssetDatabase.FindAssets("t:VContainerSettings").FirstOrDefault();
            if (guid == null)
            {
                Debug.LogWarning("[SceneSetupMenu] VContainerSettings asset not found. " +
                    "Create it (Assets > Create > VContainer > VContainerSettings) and set Root Lifetime Scope to RootLTS.");
                return;
            }
            var settings = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(guid));
            var so = new SerializedObject(settings);
            var prop = so.FindProperty("RootLifetimeScope");
            if (prop == null)
            {
                Debug.LogWarning("[SceneSetupMenu] RootLifetimeScope field not found. Set it manually.");
                return;
            }
            prop.objectReferenceValue = rootPrefab.GetComponent<RootLifetimeScope>();
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(settings);
        }

        private static void CreateBootScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("BootEntryPoint").AddComponent<BootEntryPoint>();
            EditorSceneManager.SaveScene(scene, BootScenePath);
        }

        private static void CreateMrScene(EatConfig config)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var go = new GameObject("MrSceneLTS");
            var scope = go.AddComponent<MrSceneLifetimeScope>();
            var so = new SerializedObject(scope);
            so.FindProperty("eatConfig").objectReferenceValue = config;
            so.ApplyModifiedProperties();
            EditorSceneManager.SaveScene(scene, MrScenePath);
        }

        private static void RegisterBuildScenes()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(BootScenePath, true),
                new EditorBuildSettingsScene(MrScenePath, true),
            };
        }
    }
}