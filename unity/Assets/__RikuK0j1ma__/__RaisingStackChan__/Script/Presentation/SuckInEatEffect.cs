using System.Threading;
using Cysharp.Threading.Tasks;
using LitMotion;
using RikuK0j1ma.RaisingStackChan.Entity;
using RikuK0j1ma.RaisingStackChan.UseCase;
using UnityEngine;

namespace RikuK0j1ma.RaisingStackChan.Presentation
{
    /// <summary>Draws a temporary sphere that flies to the mouth while shrinking, then disappears.</summary>
    public sealed class SuckInEatEffect : IEatEffect
    {
        private const float Duration = 0.3f;

        private readonly SphereConfig config;

        public SuckInEatEffect(SphereConfig config)
        {
            this.config = config;
        }

        public async UniTask PlayAsync(Vector3 from, Vector3 mouth, CancellationToken ct)
        {
            var startScale = config.Radius * 2f; // same diameter as the spawned sphere
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
            var tr = go.transform;
            tr.position = from;
            tr.localScale = Vector3.one * startScale;

            var handle = LMotion.Create(0f, 1f, Duration)
                .WithEase(Ease.InQuad)
                .Bind(t =>
                {
                    if (tr == null) return;
                    tr.position = Vector3.LerpUnclamped(from, mouth, t);
                    tr.localScale = Vector3.one * (startScale * (1f - t));
                });
            try
            {
                await UniTask.Delay(Mathf.CeilToInt(Duration * 1000f), cancellationToken: ct);
            }
            finally
            {
                if (handle.IsActive()) handle.Cancel();
                if (go != null) Object.Destroy(go);
            }
        }
    }
}
