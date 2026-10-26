using System;
using System.Collections.Generic;
using UnityEngine;

namespace RikuK0j1ma.RaisingStackChan.UseCase
{
    /// <summary>Creates and removes food spheres. Ids are opaque to callers.</summary>
    public interface ISphereSpawner
    {
        /// <summary>Raised after a sphere is created, with its id. Not raised when Spawn fails (-1).</summary>
        event Action<int> Spawned;

        int Spawn(Vector3 position);
        bool TryGetPosition(int id, out Vector3 position);

        /// <summary>Clears <paramref name="results"/> and fills it with the ids of all spheres currently out (held ones included).</summary>
        void GetActiveIds(List<int> results);
        void Despawn(int id);

        /// <summary>True while the sphere is being held (grabbed) by a hand.</summary>
        bool IsHeld(int id);
    }
}
