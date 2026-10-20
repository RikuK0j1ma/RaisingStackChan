using RikuK0j1ma.RaisingStackChan.UseCase;
using UnityEngine;

namespace RikuK0j1ma.RaisingStackChan.DebugTools
{
    /// <summary>
    /// Treats a scene object as the StackChan face. Move/rotate it in the editor
    /// (or drag it in the Game view). The mouth is a local offset from it.
    /// </summary>
    public sealed class DebugStackChanPoseProvider : IStackChanPoseProvider
    {
        private readonly Transform face;
        private readonly Vector3 mouthLocalOffset;

        public DebugStackChanPoseProvider(Transform face, Vector3 mouthLocalOffset)
        {
            this.face = face;
            this.mouthLocalOffset = mouthLocalOffset;
        }

        public bool TryGetFacePose(out Pose pose)
        {
            if (face == null)
            {
                pose = Pose.identity;
                return false;
            }
            pose = new Pose(face.position, face.rotation);
            return true;
        }

        public bool TryGetMouthPosition(out Vector3 position)
        {
            if (face == null)
            {
                position = Vector3.zero;
                return false;
            }
            position = face.TransformPoint(mouthLocalOffset);
            return true;
        }
    }
}
