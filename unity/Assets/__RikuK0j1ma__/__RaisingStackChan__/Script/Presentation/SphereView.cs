using UnityEngine;

namespace RikuK0j1ma.RaisingStackChan.Presentation
{
    /// <summary>Visual and physics body of one food sphere. Created at runtime by <see cref="SphereSpawner"/>.</summary>
    public sealed class SphereView : MonoBehaviour
    {
        private Rigidbody body;

        public int Id { get; private set; }
        public bool IsHeld { get; private set; }

        /// <summary>Creation order; smaller is older.</summary>
        public int Order { get; private set; }

        public void Initialize(int id, int order, float damping)
        {
            Id = id;
            Order = order;
            body = GetComponent<Rigidbody>();
            body.useGravity = false;
            body.linearDamping = damping;
            body.angularDamping = damping;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }

        public void BeginHold()
        {
            IsHeld = true;
            body.linearVelocity = Vector3.zero;
            body.isKinematic = true;
        }

        public void MoveTo(Vector3 position)
        {
            transform.position = position;
        }

        public void EndHold(Vector3 velocity)
        {
            IsHeld = false;
            body.isKinematic = false;
            body.linearVelocity = velocity;
        }
    }
}
