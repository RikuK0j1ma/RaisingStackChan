using RikuK0j1ma.RaisingStackChan.UseCase;
using UnityEngine;

namespace RikuK0j1ma.RaisingStackChan.Platform.Meta
{
    /// <summary>Headset position from the OVRCameraRig centerEyeAnchor (the rig must be in the scene).</summary>
    public sealed class OvrHeadPoseProvider : IHeadPoseProvider
    {
        private OVRCameraRig rig;

        public bool TryGetHeadPosition(out Vector3 position)
        {
            if (rig == null) rig = Object.FindFirstObjectByType<OVRCameraRig>();
            if (rig == null || rig.centerEyeAnchor == null)
            {
                position = Vector3.zero;
                return false;
            }

            position = rig.centerEyeAnchor.position;
            return true;
        }
    }
}
