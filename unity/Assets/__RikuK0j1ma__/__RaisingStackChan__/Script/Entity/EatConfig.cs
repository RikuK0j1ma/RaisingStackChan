using UnityEngine;

namespace RikuK0j1ma.RaisingStackChan.Entity
{
    /// <summary>Tuning values for feeding the sphere to StackChan.</summary>
    [CreateAssetMenu(fileName = "EatConfig", menuName = "RaisingStackChan/EatConfig")]
    public sealed class EatConfig : ScriptableObject
    {
        [Tooltip("Distance (m) from the mouth within which the sphere counts as eaten.")]
        [SerializeField, Min(0.01f)] private float eatRadius = 0.08f;

        [Tooltip("Seconds the sphere must stay inside the radius.")]
        [SerializeField, Min(0f)] private float holdSeconds = 0.5f;

        [Tooltip("How long StackChan keeps the happy face (ms).")]
        [SerializeField, Min(0)] private int happyDurationMs = 3000;

        [Tooltip("Mouth position in the face local space (m), relative to the face center (= QR center). Placeholder until measured on the device.")]
        [SerializeField] private Vector3 mouthLocalOffset = new Vector3(0f, -0.02f, 0f);

        public float EatRadius => eatRadius;
        public float HoldSeconds => holdSeconds;
        public int HappyDurationMs => happyDurationMs;
        public Vector3 MouthLocalOffset => mouthLocalOffset;
    }
}