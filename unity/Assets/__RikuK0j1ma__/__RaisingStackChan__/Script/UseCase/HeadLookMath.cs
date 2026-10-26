using UnityEngine;

namespace RikuK0j1ma.RaisingStackChan.UseCase
{
    /// <summary>Pure math of the "look" message: direction from the StackChan face to the headset.</summary>
    public static class HeadLookMath
    {
        /// <summary>
        /// Direction of <paramref name="headPosition"/> seen from the face, in degrees. The face's local +Z is taken as
        /// its front and +Y as up (unverified on device, see docs/design.md). Yaw is + to the face's right,
        /// pitch is + up; (0, 0) means the head is straight in front.
        /// </summary>
        public static void Compute(Pose facePose, Vector3 headPosition, out float yawDeg, out float pitchDeg)
        {
            Vector3 local = Quaternion.Inverse(facePose.rotation) * (headPosition - facePose.position);
            float horizontal = Mathf.Sqrt(local.x * local.x + local.z * local.z);
            yawDeg = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            pitchDeg = Mathf.Atan2(local.y, horizontal) * Mathf.Rad2Deg;
        }

        /// <summary>Wraps an angle into [-180, 180].</summary>
        public static float WrapDegrees(float deg) => Mathf.DeltaAngle(0f, deg);

        /// <summary>
        /// One low-pass step toward <paramref name="target"/> (exponential, time constant <paramref name="tau"/> seconds).
        /// Frame-rate independent. Yaw takes the short way round the circle.
        /// </summary>
        public static float SmoothAngle(float current, float target, float deltaTime, float tau)
        {
            if (tau <= 0f) return target;
            float alpha = 1f - Mathf.Exp(-Mathf.Max(deltaTime, 0f) / tau);
            return WrapDegrees(current + Mathf.DeltaAngle(current, target) * alpha);
        }
    }
}
