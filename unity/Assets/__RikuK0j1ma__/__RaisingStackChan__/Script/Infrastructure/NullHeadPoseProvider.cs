using RikuK0j1ma.RaisingStackChan.UseCase;
using UnityEngine;

namespace RikuK0j1ma.RaisingStackChan.Infrastructure
{
    /// <summary>Reports no head position (no headset).</summary>
    public sealed class NullHeadPoseProvider : IHeadPoseProvider
    {
        public bool TryGetHeadPosition(out Vector3 position)
        {
            position = Vector3.zero;
            return false;
        }
    }
}
