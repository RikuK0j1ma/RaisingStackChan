using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace RikuK0j1ma.RaisingStackChan.UseCase
{
    public enum CalibrationStartResult
    {
        /// <summary>Anchor restored and the link is connected. No QR needed.</summary>
        Restored,

        /// <summary>No usable anchor. The QR guide is shown; call <see cref="FaceCalibrationUseCase.OnFacePoseDetectedAsync"/> when the QR gives a pose.</summary>
        NeedsQr,
    }

    /// <summary>
    /// Calibration flow around the persisted face anchor.
    /// Startup: restore anchor -> success: connect only / failure: QR guide.
    /// Recalibrate: discard anchor -> QR guide. QR pose detected: (re)create anchor.
    /// </summary>
    public sealed class FaceCalibrationUseCase
    {
        private readonly IFaceAnchorStore store;
        private readonly IStackChanLink link;
        private readonly ICalibrationGuide guide;

        public FaceCalibrationUseCase(IFaceAnchorStore store, IStackChanLink link, ICalibrationGuide guide)
        {
            this.store = store;
            this.link = link;
            this.guide = guide;
        }

        /// <summary>Raised with the face pose when an anchor was restored or newly created.</summary>
        public event Action<Pose> FacePoseConfirmed;

        public async UniTask<CalibrationStartResult> StartAsync(CancellationToken ct)
        {
            var restored = await store.RestoreAsync(ct);
            if (!restored.Success)
            {
                guide.ShowQrGuide();
                return CalibrationStartResult.NeedsQr;
            }

            guide.HideQrGuide();
            FacePoseConfirmed?.Invoke(restored.Pose);
            await link.ConnectAsync(ct);
            return CalibrationStartResult.Restored;
        }

        /// <summary>Called when QR detection determined the face pose. Persists it as the new anchor.</summary>
        /// <returns>False if the anchor could not be saved (the pose is still confirmed for this session).</returns>
        public async UniTask<bool> OnFacePoseDetectedAsync(Pose facePose, CancellationToken ct)
        {
            await store.DiscardAsync(ct);
            var saved = await store.SaveAsync(facePose, ct);
            guide.HideQrGuide();
            FacePoseConfirmed?.Invoke(facePose);
            return saved;
        }

        /// <summary>Discards the anchor and goes back to the QR guide.</summary>
        public async UniTask RecalibrateAsync(CancellationToken ct)
        {
            await store.DiscardAsync(ct);
            guide.ShowQrGuide();
        }
    }
}
