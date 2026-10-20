using System.Threading;
using Cysharp.Threading.Tasks;
using RikuK0j1ma.RaisingStackChan.UseCase;
using UnityEngine;

namespace RikuK0j1ma.RaisingStackChan.Infrastructure
{
    /// <summary>No spatial anchor support (no headset): nothing is ever restored, so the QR flow is used.</summary>
    public sealed class NullFaceAnchorStore : IFaceAnchorStore
    {
        public UniTask<bool> SaveAsync(Pose facePose, CancellationToken ct) => UniTask.FromResult(false);

        public UniTask<FaceAnchorRestoreResult> RestoreAsync(CancellationToken ct) =>
            UniTask.FromResult(FaceAnchorRestoreResult.Failed);

        public UniTask DiscardAsync(CancellationToken ct) => UniTask.CompletedTask;

        public bool TryGetPose(out Pose pose)
        {
            pose = Pose.identity;
            return false;
        }
    }
}
