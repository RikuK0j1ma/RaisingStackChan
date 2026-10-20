using UnityEngine;

namespace RikuK0j1ma.RaisingStackChan.Entity
{
    /// <summary>Tuning values for spawning and handling food spheres.</summary>
    [CreateAssetMenu(fileName = "SphereConfig", menuName = "RaisingStackChan/SphereConfig")]
    public sealed class SphereConfig : ScriptableObject
    {
        [Tooltip("Sphere radius (m).")]
        [SerializeField, Min(0.005f)] private float radius = 0.04f;

        [Tooltip("Maximum number of spheres alive at once. The oldest free sphere is removed when exceeded.")]
        [SerializeField, Min(1)] private int maxCount = 5;

        [Tooltip("Spheres farther than this (m) from the headset are recalled (removed).")]
        [SerializeField, Min(0.5f)] private float recallDistance = 6f;

        [Tooltip("Hand-to-sphere distance (m) within which a grab picks the sphere up.")]
        [SerializeField, Min(0.01f)] private float grabRadius = 0.12f;

        [Tooltip("Linear damping of a free sphere, so thrown spheres drift to a stop (no gravity).")]
        [SerializeField, Min(0f)] private float freeDamping = 1.5f;

        [Tooltip("Multiplier on the hand velocity applied when a sphere is released.")]
        [SerializeField, Min(0f)] private float throwScale = 1f;

        public float Radius => radius;
        public int MaxCount => maxCount;
        public float RecallDistance => recallDistance;
        public float GrabRadius => grabRadius;
        public float FreeDamping => freeDamping;
        public float ThrowScale => throwScale;
    }
}
