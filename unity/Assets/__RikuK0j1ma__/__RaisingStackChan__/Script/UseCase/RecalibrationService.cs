using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using RikuK0j1ma.RaisingStackChan.Entity;
using UnityEngine;

namespace RikuK0j1ma.RaisingStackChan.UseCase
{
    /// <summary>
    /// Recalibration. Two entry points:
    /// - menu "Recalibrate" (<see cref="RequestAsync"/>): drops the anchor, restarts the guide, asks StackChan for the QR (showQr).
    /// - Y button (<see cref="ToggleQrAsync"/>): face -> QR keeps the anchor until a new pose is fixed;
    ///   QR -> face sends hideQr and keeps the pose as it is. The connection is never dropped.
    /// Tracks whether StackChan currently shows the QR (<see cref="IsQrShown"/>).
    /// </summary>
    public sealed class RecalibrationService : IDisposable
    {
        private readonly IStackChanLink link;
        private readonly SetupGuideUseCase guide;

        public RecalibrationService(IStackChanLink link, SetupGuideUseCase guide)
        {
            this.link = link;
            this.guide = guide;
            link.StateChanged += OnLinkState;
        }

        /// <summary>True while StackChan is showing the QR for recalibration (we asked for it and it was not hidden yet).</summary>
        public bool IsQrShown { get; private set; }

        /// <summary>Raised before showQr is sent. #10 subscribes to drop the saved anchor.</summary>
        public event Action RecalibrationRequested;

        /// <summary>Raised when the Y button starts a QR scan. The anchor is kept; the pose side must accept the next QR as new.</summary>
        public event Action QrScanStarted;

        /// <summary>Raised when the Y button cancelled the QR scan. The previous pose must be kept as it is.</summary>
        public event Action QrScanCancelled;

        public async UniTask RequestAsync(CancellationToken ct)
        {
            RecalibrationRequested?.Invoke();
            guide.Flow.Restart();

            // Not connected: StackChan shows the QR by itself after a restart / when MOGUMOGU opens.
            if (!link.IsConnected) return;

            try
            {
                await link.SendShowQrAsync(ct);
                IsQrShown = true;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception e) { Debug.LogWarning($"[Setup] showQr failed: {e.Message}"); }
        }

        /// <summary>Y button. Face -> QR, or QR -> face. Ignored while the link is down.</summary>
        public async UniTask ToggleQrAsync(CancellationToken ct)
        {
            if (!link.IsConnected) return;
            if (IsQrShown) await HideQrAsync(ct);
            else await ShowQrKeepingAnchorAsync(ct);
        }

        /// <summary>
        /// A new face pose was fixed and saved. If StackChan still shows the QR from a recalibration, hide it
        /// (the firmware only hides it on a new connection). Also ends the wait for a fresh pose.
        /// </summary>
        public async UniTask OnFacePoseConfirmedAsync(CancellationToken ct)
        {
            guide.EndFreshPoseWait();
            if (!IsQrShown || !link.IsConnected) return;
            try
            {
                await link.SendHideQrAsync(ct);
                IsQrShown = false;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception e) { Debug.LogWarning($"[Setup] hideQr failed: {e.Message}"); }
        }

        public void Dispose() => link.StateChanged -= OnLinkState;

        private async UniTask ShowQrKeepingAnchorAsync(CancellationToken ct)
        {
            QrScanStarted?.Invoke();
            guide.BeginFreshPoseWait();
            guide.Flow.Restart();
            try
            {
                await link.SendShowQrAsync(ct);
                IsQrShown = true;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception e)
            {
                Debug.LogWarning($"[Setup] showQr failed: {e.Message}");
                guide.EndFreshPoseWait();
                guide.Flow.CancelRecalibration();
                QrScanCancelled?.Invoke();
            }
        }

        private async UniTask HideQrAsync(CancellationToken ct)
        {
            try { await link.SendHideQrAsync(ct); }
            catch (OperationCanceledException) { throw; }
            catch (Exception e)
            {
                // Keep the state: StackChan still shows the QR, so the next Y press must try to hide it again.
                Debug.LogWarning($"[Setup] hideQr failed: {e.Message}");
                return;
            }

            IsQrShown = false;
            guide.EndFreshPoseWait();
            guide.Flow.CancelRecalibration();
            QrScanCancelled?.Invoke();
        }

        // StackChan hides the QR itself when a client connects, so a fresh connection means the face is back.
        private void OnLinkState(StackChanConnectionState state)
        {
            if (state != StackChanConnectionState.Connected) return;
            IsQrShown = false;
            guide.EndFreshPoseWait();
        }
    }
}
