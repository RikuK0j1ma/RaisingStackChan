using RikuK0j1ma.RaisingStackChan.UseCase;
using UnityEngine;

namespace RikuK0j1ma.RaisingStackChan.Infrastructure
{
    /// <summary>Spawner that creates nothing; ids are never valid.</summary>
    public sealed class NullSphereSpawner : ISphereSpawner
    {
        public event System.Action<int> Spawned { add { } remove { } }

        public int Spawn(Vector3 position) => -1;

        public bool TryGetPosition(int id, out Vector3 position)
        {
            position = Vector3.zero;
            return false;
        }

        public bool TryGetEatDistance(int id, Vector3 point, out float distance)
        {
            distance = float.MaxValue;
            return false;
        }

        public void GetActiveIds(System.Collections.Generic.List<int> results) => results.Clear();

        public void Despawn(int id) { }

        public bool Bite(int id, Vector3 mouth) => false;

        public bool IsHeld(int id) => false;
    }
}
