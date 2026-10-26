using UnityEngine;

namespace RikuK0j1ma.RaisingStackChan.Entity
{
    /// <summary>
    /// Where StackChan's neck is, relative to the QR face pose, and how the servo angles map to it.
    /// Everything StackChan looks at is computed in this frame (see StackChanFrame); the firmware only moves the servos.
    /// The defaults reproduce the old behaviour (direction seen from the face center, no corrections) until they are
    /// measured on the device (docs/design.md, "StackChan の座標系").
    /// </summary>
    [CreateAssetMenu(fileName = "StackChanRigConfig", menuName = "RaisingStackChan/StackChanRigConfig")]
    public sealed class StackChanRigConfig : ScriptableObject
    {
        [Tooltip("Neck pitch-axis point in the face local space (m), relative to the face center (= QR center). " +
                 "The vertical yaw axis passes through it. Zero: directions are taken from the face center.")]
        [SerializeField] private Vector3 pivotLocalOffset = Vector3.zero;

        [Tooltip("Direction of servo yaw 0 relative to the face local forward (+Z), degrees, + to the right.")]
        [SerializeField] private float yawZeroOffsetDeg;

        [Tooltip("Direction of servo pitch 0 relative to the face local forward (+Z), degrees, + up.")]
        [SerializeField] private float pitchZeroOffsetDeg;

        [Tooltip("Flip the sign of the yaw that is sent (left/right turned the wrong way).")]
        [SerializeField] private bool invertYaw;

        [Tooltip("Flip the sign of the pitch that is sent (up/down turned the wrong way).")]
        [SerializeField] private bool invertPitch;

        public Vector3 PivotLocalOffset => pivotLocalOffset;
        public float YawZeroOffsetDeg => yawZeroOffsetDeg;
        public float PitchZeroOffsetDeg => pitchZeroOffsetDeg;
        public bool InvertYaw => invertYaw;
        public bool InvertPitch => invertPitch;
    }
}
