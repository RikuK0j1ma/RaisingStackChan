using UnityEngine;

namespace RikuK0j1ma.RaisingStackChan.UseCase
{
    /// <summary>Hold / release operations on spheres, used by input (Controller layer).</summary>
    public interface ISphereInteraction
    {
        /// <summary>Finds the nearest free (not held) sphere within <paramref name="radius"/> of a point.</summary>
        bool TryFindGrabbable(Vector3 point, float radius, out int id);

        /// <summary>Marks the sphere as held. Returns false if the id is invalid or already held.</summary>
        bool BeginHold(int id);

        /// <summary>Moves a held sphere. Returns false if the sphere no longer exists or is not held.</summary>
        bool MoveHeld(int id, Vector3 position);

        /// <summary>Releases a held sphere with the given world velocity (m/s).</summary>
        void EndHold(int id, Vector3 velocity);
    }
}
