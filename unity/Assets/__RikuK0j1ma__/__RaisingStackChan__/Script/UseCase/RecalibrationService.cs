using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace RikuK0j1ma.RaisingStackChan.UseCase
{
    /// <summary>
    /// "Recalibrate" from the menu: restarts the guide, raises <see cref="RecalibrationRequested"/> so the
    /// anchor side (#10) can discard the old anchor and rebuild it once the new pose is fixed, and asks
    /// StackChan to show the QR again (showQr).
    /// </summary>
    public sealed class RecalibrationService
    {
        private readonly IStackChanLink link;
        private readonly SetupGuideUseCase guide;

        public RecalibrationService(IStackChanLink link, SetupGuideUseCase guide)
        {
            this.link = link;
            this.guide = guide;
        }

        /// <summary>Raised before showQr is sent. #10 subscribes to drop the saved anchor.</summary>
        public event Action RecalibrationRequested;

        public async UniTask RequestAsync(CancellationToken ct)
        {
            RecalibrationRequested?.Invoke();
            guide.Flow.Restart();

            // Not connected: StackChan shows the QR by itself after a restart / when MOGUMOGU opens.
            if (!link.IsConnected) return;

            try { await link.SendShowQrAsync(ct); }
            catch (OperationCanceledException) { throw; }
            catch (Exception e) { Debug.LogWarning($"[Setup] showQr failed: {e.Message}"); }
        }
    }
}
