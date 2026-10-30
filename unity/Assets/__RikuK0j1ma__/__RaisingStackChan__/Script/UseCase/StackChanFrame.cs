using RikuK0j1ma.RaisingStackChan.Entity;
using UnityEngine;

namespace RikuK0j1ma.RaisingStackChan.UseCase
{
    /// <summary>
    /// StackChan's own coordinate frame in the world, derived from the QR face pose: the origin is the neck pitch axis
    /// (the vertical yaw axis passes through it) and the forward direction is where the servos point at yaw 0 / pitch 0.
    /// Pure math, no Unity scene access. The angles it returns are the servo angles that are sent as "look".
    /// </summary>
    public readonly struct StackChanFrame
    {
        private readonly bool invertYaw;
        private readonly bool invertPitch;

        /// <summary>Neck pitch-axis point in the world.</summary>
        public Vector3 Pivot { get; }

        /// <summary>Axes at servo (0, 0): +Z forward, +Y up, +X right.</summary>
        public Quaternion Rotation { get; }

        public StackChanFrame(Vector3 pivot, Quaternion rotation, bool invertYaw, bool invertPitch)
        {
            Pivot = pivot;
            Rotation = rotation;
            this.invertYaw = invertYaw;
            this.invertPitch = invertPitch;
        }

        /// <summary>Frame from the face pose with no corrections: directions are taken from the face center.</summary>
        public static StackChanFrame FromFace(Pose face) => new StackChanFrame(face.position, face.rotation, false, false);

        public static StackChanFrame From(Pose face, StackChanRigConfig config)
        {
            if (config == null) return FromFace(face);
            return From(face, config.PivotLocalOffset, config.YawZeroOffsetDeg, config.PitchZeroOffsetDeg,
                config.InvertYaw, config.InvertPitch);
        }

        public static StackChanFrame From(Pose face, Vector3 pivotLocalOffset, float yawZeroDeg, float pitchZeroDeg,
            bool invertYaw, bool invertPitch)
        {
            Vector3 pivot = face.position + face.rotation * pivotLocalOffset;
            // Unity rotates + about +Y toward the right, and + about +X tilts the front down, hence the minus.
            Quaternion zero = Quaternion.AngleAxis(yawZeroDeg, Vector3.up) * Quaternion.AngleAxis(-pitchZeroDeg, Vector3.right);
            return new StackChanFrame(pivot, face.rotation * zero, invertYaw, invertPitch);
        }

        /// <summary>
        /// Servo angles (degrees) that point the neck at <paramref name="target"/>. Yaw is + to the right, pitch is + up,
        /// (0, 0) means straight ahead of the frame. Both are sign-flipped when the config says so.
        /// </summary>
        public void Compute(Vector3 target, out float yawDeg, out float pitchDeg)
        {
            Vector3 local = Quaternion.Inverse(Rotation) * (target - Pivot);
            float horizontal = Mathf.Sqrt(local.x * local.x + local.z * local.z);
            yawDeg = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            pitchDeg = Mathf.Atan2(local.y, horizontal) * Mathf.Rad2Deg;
            if (invertYaw) yawDeg = -yawDeg;
            if (invertPitch) pitchDeg = -pitchDeg;
        }
    }
}
