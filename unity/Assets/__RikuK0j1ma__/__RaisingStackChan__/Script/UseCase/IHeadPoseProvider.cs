using UnityEngine;

namespace RikuK0j1ma.RaisingStackChan.UseCase
{
    /// <summary>Provides the player's head (headset) position in world space. Implemented by the platform layer.</summary>
    public interface IHeadPoseProvider
    {
        /// <summary>False while the headset is not tracked or no headset exists.</summary>
        bool TryGetHeadPosition(out Vector3 position);
    }
}
