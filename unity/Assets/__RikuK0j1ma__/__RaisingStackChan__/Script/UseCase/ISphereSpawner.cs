using UnityEngine;

namespace RikuK0j1ma.RaisingStackChan.UseCase
{
    /// <summary>Creates and removes food spheres. Ids are opaque to callers.</summary>
    public interface ISphereSpawner
    {
        int Spawn(Vector3 position);
        bool TryGetPosition(int id, out Vector3 position);
        void Despawn(int id);
    }
}