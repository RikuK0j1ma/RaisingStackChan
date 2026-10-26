using System;
using RikuK0j1ma.RaisingStackChan.Entity;

namespace RikuK0j1ma.RaisingStackChan.UseCase
{
    /// <summary>
    /// Feeds link state and pose availability into <see cref="SetupFlow"/>.
    /// Uses only existing interfaces (IStackChanLink, IStackChanPoseProvider).
    /// QR quality reports (#9) call Flow.ReportQrCandidate / ReportQrProblem directly.
    /// </summary>
    public sealed class SetupGuideUseCase : IDisposable
    {
        private readonly IStackChanLink link;
        private readonly IStackChanPoseProvider pose;

        private bool awaitingFreshPose;

        public SetupFlow Flow { get; }

        public SetupGuideUseCase(IStackChanLink link, IStackChanPoseProvider pose)
        {
            this.link = link;
            this.pose = pose;
            // Permission explanation is shown on every launch for now; persisting "already granted" is up to the caller.
            Flow = new SetupFlow(false);
            link.StateChanged += Flow.OnLinkState;
            Flow.OnLinkState(link.State);
        }

        /// <summary>Last link error for display; null when none.</summary>
        public string LastError => link.LastError;

        /// <summary>
        /// While true, the pose still held from before is not taken as a detection (recalibration via Y button).
        /// Without this the guide would jump to Connected on the next Tick while StackChan still shows the QR.
        /// </summary>
        public void BeginFreshPoseWait() => awaitingFreshPose = true;

        public void EndFreshPoseWait() => awaitingFreshPose = false;

        /// <summary>Call every frame.</summary>
        public void Tick(float deltaTime)
        {
            if (!awaitingFreshPose
                && (Flow.Phase == SetupPhase.LookAtFace || Flow.Phase == SetupPhase.Detecting)
                && pose.TryGetFacePose(out _))
            {
                Flow.ReportQrDetected();
                // The link may already be connecting/connected by the time the pose is fixed.
                Flow.OnLinkState(link.State);
            }
            Flow.Tick(deltaTime);
        }

        public void Dispose() => link.StateChanged -= Flow.OnLinkState;
    }
}
