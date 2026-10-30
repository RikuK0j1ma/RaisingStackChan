using System.Collections.Generic;
using UnityEngine;

namespace RikuK0j1ma.RaisingStackChan.UseCase
{
    /// <summary>A food sphere that StackChan may look at.</summary>
    public readonly struct LookCandidate
    {
        public readonly int Id;
        public readonly Vector3 Position;

        public LookCandidate(int id, Vector3 position)
        {
            Id = id;
            Position = position;
        }
    }

    /// <summary>
    /// Pure choice of what StackChan looks at: the food sphere nearest to the face (mouth), or the headset when there
    /// is none. Held spheres are candidates too. To avoid flip-flopping between two similar distances, the current
    /// target is kept unless a new one is closer by both <see cref="SwitchRatio"/> of the current distance and
    /// <see cref="SwitchMarginMeters"/>.
    /// </summary>
    public static class LookTargetSelector
    {
        /// <summary>Returned when no sphere is chosen: look at the headset.</summary>
        public const int HeadsetId = -1;

        /// <summary>The new target must be at least this fraction closer than the current one (10%).</summary>
        public const float SwitchRatio = 0.1f;

        /// <summary>... and at least this many meters closer (3 cm).</summary>
        public const float SwitchMarginMeters = 0.03f;

        /// <summary>Returns the id of the sphere to look at, or <see cref="HeadsetId"/>.</summary>
        /// <param name="currentId">Previous result (<see cref="HeadsetId"/> if none).</param>
        public static int Select(Vector3 origin, IReadOnlyList<LookCandidate> candidates, int currentId)
        {
            if (candidates == null || candidates.Count == 0) return HeadsetId;

            int bestId = HeadsetId;
            float bestDist = float.MaxValue;
            bool currentFound = false;
            float currentDist = 0f;
            for (int i = 0; i < candidates.Count; i++)
            {
                LookCandidate c = candidates[i];
                float d = Vector3.Distance(origin, c.Position);
                if (d < bestDist)
                {
                    bestDist = d;
                    bestId = c.Id;
                }

                if (c.Id == currentId)
                {
                    currentFound = true;
                    currentDist = d;
                }
            }

            if (!currentFound || bestId == currentId) return bestId;
            float required = Mathf.Max(currentDist * SwitchRatio, SwitchMarginMeters);
            return currentDist - bestDist >= required ? bestId : currentId;
        }
    }
}
