using UnityEngine;

namespace RikuK0j1ma.RaisingStackChan.Entity
{
    /// <summary>Tuning values for spawning and handling food spheres.</summary>
    [CreateAssetMenu(fileName = "SphereConfig", menuName = "RaisingStackChan/SphereConfig")]
    public sealed class SphereConfig : ScriptableObject
    {
        [Tooltip("Prefab of one food sphere. Spawned as is; its scale is set from Radius.")]
        [SerializeField] private GameObject prefab;

        [Tooltip("Sphere radius (m).")]
        [SerializeField, Min(0.005f)] private float radius = 0.04f;

        [Tooltip("Maximum number of spheres alive at once. The oldest free sphere is removed when exceeded.")]
        [SerializeField, Min(1)] private int maxCount = 5;

        [Tooltip("Spheres farther than this (m) from the headset are recalled (removed).")]
        [SerializeField, Min(0.5f)] private float recallDistance = 6f;

        public GameObject Prefab => prefab;
        public float Radius => radius;
        public int MaxCount => maxCount;
        public float RecallDistance => recallDistance;
    }
}
