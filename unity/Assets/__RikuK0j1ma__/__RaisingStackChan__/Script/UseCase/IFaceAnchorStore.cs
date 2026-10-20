using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace RikuK0j1ma.RaisingStackChan.UseCase
{
    /// <summary>Result of restoring the persisted face anchor.</summary>
    public readonly struct FaceAnchorRestoreResult
    {
        public readonly bool Success;
        public readonly Pose Pose;

        public FaceAnchorRestoreResult(bool success, Pose pose)
        {
            Success = success;
            Pose = pose;
        }

        public static FaceAnchorRestoreResult Failed => new FaceAnchorRestoreResult(false, Pose.identity);
    }

    /// <summary>Persists the calibrated StackChan face pose as a spatial anchor.</summary>
    public interface IFaceAnchorStore
    {
        /// <summary>Creates an anchor at the pose and persists it, replacing any previous one. Returns false on failure.</summary>
        UniTask<bool> SaveAsync(Pose facePose, CancellationToken ct);

        /// <summary>Restores the persisted anchor. Fails when none is saved, it was erased, or the space changed.</summary>
        UniTask<FaceAnchorRestoreResult> RestoreAsync(CancellationToken ct);

        /// <summary>Erases the anchor and forgets its id. Safe to call when nothing is saved.</summary>
        UniTask DiscardAsync(CancellationToken ct);

        /// <summary>Current pose of the live anchor (it can be refined by the runtime). False when no anchor is live.</summary>
        bool TryGetPose(out Pose pose);
    }

    /// <summary>Stores the id (UUID string) of the persisted anchor across app launches.</summary>
    public interface IFaceAnchorIdStore
    {
        bool TryLoad(out string uuid);
        void Save(string uuid);
        void Clear();
    }

    /// <summary>Shows or hides the "scan the QR code on StackChan" guidance.</summary>
    public interface ICalibrationGuide
    {
        void ShowQrGuide();
        void HideQrGuide();
    }
}
