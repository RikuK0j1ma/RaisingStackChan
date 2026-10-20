using RikuK0j1ma.RaisingStackChan.UseCase;
using UnityEngine;

namespace RikuK0j1ma.RaisingStackChan.Infrastructure
{
    /// <summary>Reports no pose (not calibrated).</summary>
    public sealed class NullStackChanPoseProvider : IStackChanPoseProvider
    {
        public bool TryGetFacePose(out Pose pose)
        {
            pose = Pose.identity;
            return false;
        }

        public bool TryGetMouthPosition(out Vector3 position)
        {
            position = Vector3.zero;
            return false;
        }
    }
}