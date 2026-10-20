using System.Collections.Generic;
using RikuK0j1ma.RaisingStackChan.UseCase;
using UnityEngine;

namespace RikuK0j1ma.RaisingStackChan.DebugTools
{
    /// <summary>Spawns draggable primitive spheres (diameter 0.06 m) for editor testing.</summary>
    public sealed class DebugSphereSpawner : ISphereSpawner
    {
        private const float Diameter = 0.06f;
        private readonly Dictionary<int, GameObject> spheres = new Dictionary<int, GameObject>();
        private int nextId;

        public IReadOnlyDictionary<int, GameObject> Spheres => spheres;

        public int Spawn(Vector3 position)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = $"DebugSphere_{nextId}";
            go.transform.position = position;
            go.transform.localScale = Vector3.one * Diameter;
            go.AddComponent<DebugDraggable>();
            int id = nextId++;
            spheres[id] = go;
            return id;
        }

        public bool TryGetPosition(int id, out Vector3 position)
        {
            if (spheres.TryGetValue(id, out GameObject go) && go != null)
            {
                position = go.transform.position;
                return true;
            }
            position = Vector3.zero;
            return false;
        }

        public void Despawn(int id)
        {
            if (spheres.TryGetValue(id, out GameObject go))
            {
                if (go != null)
                {
                    Object.Destroy(go);
                }
                spheres.Remove(id);
            }
        }
    }
}
