using UnityEngine;

namespace RikuK0j1ma.RaisingStackChan.Entity
{
    /// <summary>
    /// Tuning values for spawning and handling the food (the manga meat). The class keeps its old name
    /// from when the food was a sphere.
    /// </summary>
    [CreateAssetMenu(fileName = "SphereConfig", menuName = "RaisingStackChan/SphereConfig")]
    public sealed class SphereConfig : ScriptableObject
    {
        /// <summary>Most bites the shader can hold.</summary>
        public const int MaxBiteCount = 8;

        [Tooltip("Model of the food (MangaMeat.fbx). It needs two mesh children named Meat and Bone.")]
        [SerializeField] private GameObject prefab;

        [Tooltip("Material of the meat (MangaMeat shader). Copied per food, because each one has its own bites.")]
        [SerializeField] private Material meatMaterial;

        [Tooltip("Cut face, step 2 (MangaMeatVolume shader): counts the meat volume in the stencil. Copied per food.")]
        [SerializeField] private Material volumeMaterial;

        [Tooltip("Cut face, step 1 (MangaMeatBiteDepth shader): depth of the invisible bite spheres. Copied per food.")]
        [SerializeField] private Material biteDepthMaterial;

        [Tooltip("Cut face, step 3 (MangaMeatBiteCap shader): the cut face pattern. Copied per food.")]
        [SerializeField] private Material biteCapMaterial;

        [Tooltip("Material of the bone (MangaMeat shader, no bites).")]
        [SerializeField] private Material boneMaterial;

        [Tooltip("Length of the bone (m) after scaling. The whole model is scaled to this.")]
        [SerializeField, Min(0.05f)] private float boneLength = 0.14f;

        [Tooltip("Size of the crumb that flies into the mouth, and the default size of a bite (m).")]
        [SerializeField, Min(0.005f)] private float radius = 0.04f;

        [Tooltip("Bites to finish the meat. The last bite takes what is left.")]
        [SerializeField, Range(1, MaxBiteCount)] private int biteCount = 4;

        [Tooltip("Radius of one bite (m).")]
        [SerializeField, Min(0.005f)] private float biteRadius = 0.03f;

        [Tooltip("Hand distance (m) to the bone within which it can be grabbed.")]
        [SerializeField, Min(0.01f)] private float grabReach = 0.06f;

        [Tooltip("Maximum number of foods alive at once. The oldest free one is removed when exceeded.")]
        [SerializeField, Min(1)] private int maxCount = 5;

        [Tooltip("Foods farther than this (m) from the headset are recalled (removed).")]
        [SerializeField, Min(0.5f)] private float recallDistance = 6f;

        public GameObject Prefab => prefab;
        public Material MeatMaterial => meatMaterial;
        public Material BoneMaterial => boneMaterial;
        public Material VolumeMaterial => volumeMaterial;
        public Material BiteDepthMaterial => biteDepthMaterial;
        public Material BiteCapMaterial => biteCapMaterial;
        public float BoneLength => boneLength;
        public float Radius => radius;
        public int BiteCount => Mathf.Clamp(biteCount, 1, MaxBiteCount);
        public float BiteRadius => biteRadius;
        public float GrabReach => grabReach;
        public int MaxCount => maxCount;
        public float RecallDistance => recallDistance;
    }
}
