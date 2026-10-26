using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using RikuK0j1ma.RaisingStackChan.UseCase;
using UnityEngine;
using VContainer;
using VContainer.Unity;
#if UNITY_EDITOR || UNITY_ANDROID
using RikuK0j1ma.RaisingStackChan.Platform.Meta;
#endif

namespace RikuK0j1ma.RaisingStackChan.Context
{
    /// <summary>
    /// Entry point of the MR scene. Startup: restore the face anchor, else wait for the QR.
    /// QR pose confirmed: save the anchor and connect. Recalibrate: drop the anchor and wait for the QR again.
    /// </summary>
    public sealed class MrSceneEntryPoint : IAsyncStartable, IDisposable
    {
        private readonly IStackChanLink link;
        private readonly FaceCalibrationUseCase calibration;
        private readonly RecalibrationService recalibration;
        private readonly CancellationTokenSource cts = new CancellationTokenSource();
#if UNITY_EDITOR || UNITY_ANDROID
        private readonly MrukQrStackChanPoseProvider qr;
#endif

        public MrSceneEntryPoint(IStackChanLink link, FaceCalibrationUseCase calibration,
            RecalibrationService recalibration, IObjectResolver resolver)
        {
            this.link = link;
            this.calibration = calibration;
            this.recalibration = recalibration;
#if UNITY_EDITOR || UNITY_ANDROID
            // Registered only when MRUK is in the scene (see MetaPlatformInstaller).
            resolver.TryResolve(out qr);
#endif
        }

        public async UniTask StartAsync(CancellationToken ct)
        {
            ct = CancellationTokenSource.CreateLinkedTokenSource(ct, cts.Token).Token;
#if UNITY_EDITOR || UNITY_ANDROID
            if (qr != null)
            {
                qr.FacePoseConfirmed += OnQrPose;
                calibration.FacePoseConfirmed += qr.ApplyRestoredPose;
                recalibration.RecalibrationRequested += OnRecalibrationRequested;
                recalibration.QrScanStarted += OnQrScanStarted;
                recalibration.QrScanCancelled += OnQrScanCancelled;
            }
#endif
            var result = await calibration.StartAsync(ct);

            // No QR source (editor / DebugTools): connect right away as before.
            if (result == CalibrationStartResult.NeedsQr && !HasQrSource) await link.ConnectAsync(ct);

            Debug.Log($"[MR] DI ready. link={link.GetType().Name} start={result} connected={link.IsConnected}");
        }

        private bool HasQrSource
        {
            get
            {
#if UNITY_EDITOR || UNITY_ANDROID
                return qr != null && qr.IsActive;
#else
                return false;
#endif
            }
        }

#if UNITY_EDITOR || UNITY_ANDROID
        private void OnQrPose(Pose pose) => SavePoseAndConnectAsync(pose).Forget();

        private async UniTaskVoid SavePoseAndConnectAsync(Pose pose)
        {
            try
            {
                await calibration.OnFacePoseDetectedAsync(pose, cts.Token);
                await link.ConnectAsync(cts.Token);
                // Y-button recalibration: the new pose is fixed, so StackChan goes back to the face.
                await recalibration.OnFacePoseConfirmedAsync(cts.Token);
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { Debug.LogException(e); }
        }

        // Y button: keep the anchor, but accept the next QR as a new pose.
        private void OnQrScanStarted() => qr.ResetConfirmation();

        // Y button cancel: keep the current pose, and do not let a lingering QR trackable re-confirm and overwrite the anchor.
        private void OnQrScanCancelled()
        {
            if (qr.TryGetFacePose(out var pose)) qr.ApplyRestoredPose(pose);
        }

        private void OnRecalibrationRequested()
        {
            qr.ResetConfirmation();
            calibration.RecalibrateAsync(cts.Token).Forget();
        }
#endif

        public void Dispose()
        {
#if UNITY_EDITOR || UNITY_ANDROID
            if (qr != null)
            {
                qr.FacePoseConfirmed -= OnQrPose;
                calibration.FacePoseConfirmed -= qr.ApplyRestoredPose;
                recalibration.RecalibrationRequested -= OnRecalibrationRequested;
                recalibration.QrScanStarted -= OnQrScanStarted;
                recalibration.QrScanCancelled -= OnQrScanCancelled;
            }
#endif
            cts.Cancel();
            cts.Dispose();
        }
    }
}
