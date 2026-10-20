using System;
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
        void Despawn(int id);

        /// <summary>True while the sphere is being held (grabbed) by a hand.</summary>
        bool IsHeld(int id);
    }
}
