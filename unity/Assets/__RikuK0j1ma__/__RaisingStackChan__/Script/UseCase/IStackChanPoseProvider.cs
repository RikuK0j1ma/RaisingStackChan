using UnityEngine;

namespace RikuK0j1ma.RaisingStackChan.UseCase
{
    /// <summary>Provides the calibrated StackChan face pose in world space.</summary>
    public interface IStackChanPoseProvider
    {
        bool TryGetFacePose(out Pose pose);
        bool TryGetMouthPosition(out Vector3 position);
    }
}