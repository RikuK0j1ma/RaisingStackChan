using System.Collections.Generic;
using RikuK0j1ma.RaisingStackChan.Entity;
using RikuK0j1ma.RaisingStackChan.UseCase;
using UnityEngine;

namespace RikuK0j1ma.RaisingStackChan.Presentation
{
    /// <summary>
    /// Runtime implementation of <see cref="ISphereSpawner"/> / <see cref="ISphereInteraction"/>.
    /// Spheres are generated from a Unity primitive (no prefab asset), kept at or below
    /// <see cref="SphereConfig.MaxCount"/>, and recalled when they drift too far from the headset.
    /// </summary>
    public sealed class SphereSpawner : ISphereSpawner, ISphereInteraction
    {
        private readonly SphereConfig config;
        private readonly Dictionary<int, SphereView> views = new Dictionary<int, SphereView>();
        private readonly List<int> scratch = new List<int>();
        private Transform root;
        private int nextId;

        public SphereSpawner(SphereConfig config)
        {
            this.config = config;
        }

        public event System.Action<int> Spawned;

        public int Spawn(Vector3 position)
        {
            EnsureRoot();
            while (views.Count >= config.MaxCount && RemoveOldestFree()) { }
            if (views.Count >= config.MaxCount) return -1;

            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "FoodSphere";
            go.transform.SetParent(root, false);
            go.transform.position = position;
            go.transform.localScale = Vector3.one * (config.Radius * 2f);
            go.GetComponent<Renderer>().material.color = new Color(1f, 0.55f, 0.2f);
            go.AddComponent<Rigidbody>();

            var view = go.AddComponent<SphereView>();
            var id = nextId++;
            view.Initialize(id, id, config.FreeDamping);
            views[id] = view;
            Spawned?.Invoke(id);
            return id;
        }

        public bool TryGetPosition(int id, out Vector3 position)
        {
            if (TryGetView(id, out var view))
            {
                position = view.transform.position;
                return true;
            }
            position = Vector3.zero;
            return false;
        }

        public void Despawn(int id)
        {
            if (!views.TryGetValue(id, out var view)) return;
            views.Remove(id);
            if (view != null) Object.Destroy(view.gameObject);
        }

        public bool IsHeld(int id) => TryGetView(id, out var view) && view.IsHeld;

        public bool TryFindGrabbable(Vector3 point, float radius, out int id)
        {
            id = -1;
            var best = float.MaxValue;
            foreach (var pair in views)
            {
                var view = pair.Value;
                if (view == null || view.IsHeld) continue;
                var d = Vector3.Distance(point, view.transform.position);
                if (d > radius + config.Radius || d >= best) continue;
                best = d;
                id = pair.Key;
            }
            return id >= 0;
        }

        public bool BeginHold(int id)
        {
            if (!TryGetView(id, out var view) || view.IsHeld) return false;
            view.BeginHold();
            return true;
        }

        public bool MoveHeld(int id, Vector3 position)
        {
            if (!TryGetView(id, out var view) || !view.IsHeld) return false;
            view.MoveTo(position);
            return true;
        }

        public void EndHold(int id, Vector3 velocity)
        {
            if (TryGetView(id, out var view) && view.IsHeld) view.EndHold(velocity);
        }

        /// <summary>Removes destroyed entries and recalls free spheres that are too far away. Called every frame.</summary>
        internal void Sweep()
        {
            var cam = Camera.main;
            var origin = cam != null ? cam.transform.position : Vector3.zero;
            var limit = config.RecallDistance;
            scratch.Clear();
            foreach (var pair in views)
            {
                var view = pair.Value;
                if (view == null) { scratch.Add(pair.Key); continue; }
                if (view.IsHeld) continue;
                if ((view.transform.position - origin).sqrMagnitude > limit * limit) scratch.Add(pair.Key);
            }
            foreach (var id in scratch) Despawn(id);
        }

        private bool TryGetView(int id, out SphereView view)
        {
            if (views.TryGetValue(id, out view) && view != null) return true;
            view = null;
            return false;
        }

        /// <summary>Frees one slot: purges destroyed entries first, else removes the oldest free sphere.</summary>
        private bool RemoveOldestFree()
        {
            scratch.Clear();
            foreach (var pair in views)
            {
                if (pair.Value == null) scratch.Add(pair.Key);
            }
            if (scratch.Count > 0)
            {
                foreach (var id in scratch) views.Remove(id);
                return true;
            }

            SphereView oldest = null;
            foreach (var view in views.Values)
            {
                if (view.IsHeld) continue;
                if (oldest == null || view.Order < oldest.Order) oldest = view;
            }
            if (oldest == null) return false;
            Despawn(oldest.Id);
            return true;
        }

        private void EnsureRoot()
        {
            if (root != null) return;
            var go = new GameObject("FoodSpheres");
            go.AddComponent<SphereSweeper>().Bind(this);
            root = go.transform;
        }

        private sealed class SphereSweeper : MonoBehaviour
        {
            private SphereSpawner owner;

            public void Bind(SphereSpawner spawner) => owner = spawner;

            private void Update() => owner?.Sweep();
        }
    }
}
