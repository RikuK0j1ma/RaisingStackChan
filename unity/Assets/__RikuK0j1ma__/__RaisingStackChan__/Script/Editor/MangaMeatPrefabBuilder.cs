using RikuK0j1ma.RaisingStackChan.Entity;
using UnityEditor;
using UnityEngine;

namespace RikuK0j1ma.RaisingStackChan.Editor
{
    /// <summary>
    /// Builds Prefab/MangaMeat.prefab from the FBX (Meat and Bone with the MangaMeat materials) and assigns it to
    /// SphereConfig. Runs by itself when the prefab or the assignment is missing (after the FBX is imported);
    /// the menu item rebuilds it by hand, e.g. after changing the FBX or the materials.
    /// </summary>
    public static class MangaMeatPrefabBuilder
    {
        private const string Root = "Assets/__RikuK0j1ma__/__RaisingStackChan__/";
        private const string FbxPath = Root + "Art/MangaMeat/MangaMeat.fbx";
        private const string MeatMaterialPath = Root + "Material/MangaMeat_Meat.mat";
        private const string BoneMaterialPath = Root + "Material/MangaMeat_Bone.mat";
        private const string PrefabPath = Root + "Prefab/MangaMeat.prefab";

        [InitializeOnLoadMethod]
        private static void AutoBuild()
        {
            // Wait until the editor has finished loading and importing.
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillEnterPlaymode) return;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                if (prefab == null) Build(false);
                else AssignToConfig(prefab);
            };
        }

        [MenuItem("RaisingStackChan/Build MangaMeat Prefab")]
        private static void BuildFromMenu() => Build(true);

        private static void Build(bool verbose)
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            if (fbx == null)
            {
                if (verbose) Debug.LogError($"[MangaMeat] FBX not found or not imported yet: {FbxPath}");
                return;
            }

            var meatMaterial = AssetDatabase.LoadAssetAtPath<Material>(MeatMaterialPath);
            var boneMaterial = AssetDatabase.LoadAssetAtPath<Material>(BoneMaterialPath);

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            try
            {
                var found = 0;
                foreach (var renderer in instance.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var isMeat = renderer.name.IndexOf("meat", System.StringComparison.OrdinalIgnoreCase) >= 0;
                    var isBone = renderer.name.IndexOf("bone", System.StringComparison.OrdinalIgnoreCase) >= 0;
                    var material = isMeat ? meatMaterial : isBone ? boneMaterial : null;
                    if (material == null) continue;

                    var materials = new Material[Mathf.Max(1, renderer.sharedMaterials.Length)];
                    for (var i = 0; i < materials.Length; i++) materials[i] = material;
                    renderer.sharedMaterials = materials;
                    found++;
                }

                if (found < 2)
                {
                    Debug.LogError($"[MangaMeat] The FBX needs two mesh children named Meat and Bone (found {found}).");
                    return;
                }

                instance.name = "MangaMeat";
                var prefab = PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
                AssetDatabase.SaveAssets();
                AssignToConfig(prefab);
                Debug.Log($"[MangaMeat] Built {PrefabPath}");
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        private static void AssignToConfig(GameObject prefab)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:SphereConfig"))
            {
                var config = AssetDatabase.LoadAssetAtPath<SphereConfig>(AssetDatabase.GUIDToAssetPath(guid));
                if (config == null) continue;

                var so = new SerializedObject(config);
                var property = so.FindProperty("prefab");
                if (property == null || property.objectReferenceValue == prefab) continue;
                property.objectReferenceValue = prefab;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssets();
            }
        }
    }
}
