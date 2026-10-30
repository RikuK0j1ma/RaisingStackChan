using System;
using System.Collections.Generic;
using RikuK0j1ma.RaisingStackChan.Entity;
using RikuK0j1ma.RaisingStackChan.UseCase;
using UnityEngine;

namespace RikuK0j1ma.RaisingStackChan.DebugTools
{
    /// <summary>Spawns draggable primitive spheres (same size as SphereConfig) for editor testing.</summary>
    public sealed class DebugSphereSpawner : ISphereSpawner
    {
        private readonly SphereConfig config;
        private readonly Dictionary<int, GameObject> spheres = new Dictionary<int, GameObject>();
        private int nextId;

        public DebugSphereSpawner(SphereConfig config)
        {
            this.config = config;
        }

        public event Action<int> Spawned;

        public IReadOnlyDictionary<int, GameObject> Spheres => spheres;

        public int Spawn(Vector3 position)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = $"DebugSphere_{nextId}";
            go.transform.position = position;
            go.transform.localScale = Vector3.one * (config.Radius * 2f);
            go.AddComponent<DebugDraggable>();
            int id = nextId++;
            spheres[id] = go;
            Spawned?.Invoke(id);
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

        public bool TryGetEatDistance(int id, Vector3 point, out float distance)
        {
            if (TryGetPosition(id, out Vector3 position))
            {
                distance = Vector3.Distance(position, point);
                return true;
            }
            distance = float.MaxValue;
            return false;
        }

        /// <summary>A debug sphere is eaten whole by the first bite.</summary>
        public bool Bite(int id, Vector3 mouth)
        {
            Despawn(id);
            return true;
        }

        public void GetActiveIds(List<int> results)
        {
            results.Clear();
            foreach (var pair in spheres)
            {
                if (pair.Value != null) results.Add(pair.Key);
            }
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

        // Mouse dragging is not a "hold": a dragged sphere dwelling at the mouth is eaten as usual.
        public bool IsHeld(int id) => false;
    }
}
