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

        public float EatRadius => eatRadius;
        public float HoldSeconds => holdSeconds;
        public int HappyDurationMs => happyDurationMs;
    }
}