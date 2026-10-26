using Oculus.Interaction.HandGrab;
using Oculus.Interaction.OVR.Editor.QuickActions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RikuK0j1ma.RaisingStackChan.Editor
{
    /// <summary>
    /// Adds Meta's OVR Comprehensive Interaction Rig to the MR scene (same as the Interaction SDK building block).
    /// It brings the hand and controller models and the interactors that grab the spheres. The bare
    /// OVRCameraRig draws neither, and nothing grabs without the interactors.
    /// Menu: Tools/RaisingStackChan/Add Interaction Rig
    /// </summary>
    public static class InteractionRigMenu
    {
        private const string MrScenePath = "Assets/__RikuK0j1ma__/__RaisingStackChan__/Scene/MR.unity";

        [MenuItem("Tools/RaisingStackChan/Add Interaction Rig")]
        public static void AddToMrScene()
        {
            if (Object.FindFirstObjectByType<OVRCameraRig>() == null)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                EditorSceneManager.OpenScene(MrScenePath);
            }

            var scene = EditorSceneManager.GetActiveScene();
            if (!AddTo()) Debug.Log("[InteractionRig] The scene already has an interaction rig.");
            EditorSceneManager.SaveScene(scene);
        }

        /// <summary>Adds the rig to the open scene. Returns false when one is already there.</summary>
        internal static bool AddTo()
        {
            if (Object.FindFirstObjectByType<HandGrabInteractor>() != null) return false;

            // Uses the existing OVRCameraRig, disables duplicate hand visuals, creates the interactors.
            OVRQuickActionsAPI.AddOVRInteractionRig(generateAsEditableCopy: false);
            Debug.Log("[InteractionRig] Added the OVR interaction rig (hands, controllers, grab interactors).");
            return true;
        }
    }
}
