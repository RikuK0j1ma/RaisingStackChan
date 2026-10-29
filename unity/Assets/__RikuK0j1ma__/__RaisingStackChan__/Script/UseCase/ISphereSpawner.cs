using System;
using System.Collections.Generic;
using UnityEngine;

namespace RikuK0j1ma.RaisingStackChan.UseCase
{
    /// <summary>
    /// Creates and removes the food (the manga meat; the interface keeps its old name). Ids are opaque to callers.
    /// A food is eaten bite by bite: <see cref="Bite"/> takes a chunk and tells when nothing is left.
    /// </summary>
    public interface ISphereSpawner
    {
        /// <summary>Raised after a food is created, with its id. Not raised when Spawn fails (-1).</summary>
        event Action<int> Spawned;

        int Spawn(Vector3 position);

        /// <summary>Position of the edible part (the meat, not the bone). False once the food is gone or finished.</summary>
        bool TryGetPosition(int id, out Vector3 position);

        /// <summary>Distance (m) from <paramref name="point"/> to the edible part; 0 when the point is inside it.</summary>
        bool TryGetEatDistance(int id, Vector3 point, out float distance);

        /// <summary>Clears <paramref name="results"/> and fills it with the ids of all foods currently out (held ones included).</summary>
        void GetActiveIds(List<int> results);

        /// <summary>Removes the food at once.</summary>
        void Despawn(int id);

        /// <summary>
        /// Takes one bite at the part of the food nearest to <paramref name="mouth"/>.
        /// Returns true when that was the last bite: the meat is finished; the bone stays.
        /// </summary>
        bool Bite(int id, Vector3 mouth);

        /// <summary>True while the food is being held (grabbed by the bone) by a hand.</summary>
        bool IsHeld(int id);
    }
}
