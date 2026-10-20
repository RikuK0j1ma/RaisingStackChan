using System.Threading;
using Cysharp.Threading.Tasks;
using LitMotion;
using RikuK0j1ma.RaisingStackChan.UseCase;
using UnityEngine;

namespace RikuK0j1ma.RaisingStackChan.Presentation
{
    /// <summary>Draws a temporary sphere that flies to the mouth while shrinking, then disappears.</summary>
    public sealed class SuckInEatEffect : IEatEffect
    {
        private const float Duration = 0.3f;
        private const float StartScale = 0.06f; // TODO: take from the sphere visual config (#11)

        public async UniTask PlayAsync(Vector3 from, Vector3 mouth, CancellationToken ct)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
            var tr = go.transform;
            tr.position = from;
            tr.localScale = Vector3.one * StartScale;

            var handle = LMotion.Create(0f, 1f, Duration)
                .WithEase(Ease.InQuad)
                .Bind(t =>
                {
                    if (tr == null) return;
                    tr.position = Vector3.LerpUnclamped(from, mouth, t);
                    tr.localScale = Vector3.one * (StartScale * (1f - t));
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
