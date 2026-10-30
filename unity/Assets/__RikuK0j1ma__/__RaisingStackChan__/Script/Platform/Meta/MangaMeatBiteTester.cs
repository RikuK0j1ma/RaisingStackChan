using System.Collections.Generic;
using UnityEngine;

namespace RikuK0j1ma.RaisingStackChan.Platform.Meta
{
    /// <summary>
    /// Test helper for the bite look. Added to the "Foods" object at runtime (editor and development builds only).
    /// Pick a menu item from the component's context menu (the three dots in the Inspector header, in Play mode).
    /// </summary>
    public sealed class MangaMeatBiteTester : MonoBehaviour
    {
        private const float SpawnDistance = 0.5f;

        private readonly List<int> ids = new List<int>();
        private MetaSphereSpawner spawner;

        public void Initialize(MetaSphereSpawner spawner)
        {
            this.spawner = spawner;
        }

        /// <summary>Takes one bite out of the newest meat, on the side facing the camera. Repeat to see more bites.</summary>
        [ContextMenu("Bite Once")]
        private void BiteOnce()
        {
            if (!TryGetCameraPosition(out var cameraPosition)) return;

            spawner.GetActiveIds(ids);
            if (ids.Count == 0)
            {
                Debug.LogWarning("[BiteTester] No meat. Run \"Spawn Meat In Front Of Camera\" or press A first.");
                return;
            }

            var id = ids[ids.Count - 1];
            var finished = spawner.Bite(id, cameraPosition);
            Debug.Log($"[BiteTester] Bite meat {id} finished={finished}");
        }

        [ContextMenu("Spawn Meat In Front Of Camera")]
        private void SpawnInFrontOfCamera()
        {
            if (!TryGetCameraPosition(out var cameraPosition)) return;

            var forward = Camera.main.transform.forward;
            var id = spawner.Spawn(cameraPosition + forward * SpawnDistance);
            Debug.Log($"[BiteTester] Spawned meat {id}");
        }

        private bool TryGetCameraPosition(out Vector3 position)
        {
            var camera = Camera.main;
            if (spawner == null || camera == null)
            {
                Debug.LogWarning("[BiteTester] No spawner or main camera.");
                position = Vector3.zero;
                return false;
            }

            position = camera.transform.position;
            return true;
        }
    }
}
