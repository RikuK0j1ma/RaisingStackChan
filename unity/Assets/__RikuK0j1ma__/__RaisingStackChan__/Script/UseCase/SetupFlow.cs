using System;
using RikuK0j1ma.RaisingStackChan.Entity;

namespace RikuK0j1ma.RaisingStackChan.UseCase
{
    public enum SetupPhase
    {
        /// <summary>Explains camera / spatial data / anchor permissions before the system dialog.</summary>
        PermissionExplanation = 0,
        LookAtFace = 1,
        /// <summary>A QR candidate is visible but the pose is not fixed yet.</summary>
        Detecting = 2,
        /// <summary>Pose fixed (ring on the face). Waiting for the link.</summary>
        Detected = 3,
        Connecting = 4,
        Connected = 5,
        Failed = 6,
    }

    public enum SetupFailure
    {
        None = 0,
        QrTooSmall = 1,
        QrTooDark = 2,
        QrGlare = 3,
        /// <summary>StackChan unreachable: different Wi-Fi or device-to-device traffic blocked.</summary>
        WifiMismatch = 4,
        /// <summary>Connected, then dropped right away: token is wrong (StackChan was restarted).</summary>
        TokenRejected = 5,
    }

    /// <summary>
    /// Pure state machine of the first-time setup / recalibration guide. No UI, no Unity types.
    /// Inputs: user actions, QR detection reports (#9 wires these), link state (via SetupGuideUseCase), Tick.
    /// </summary>
    public sealed class SetupFlow
    {
        /// <summary>Seconds without a connection while Connecting before blaming the Wi-Fi.</summary>
        public const float WifiTimeoutSeconds = 8f;

        /// <summary>A drop within this many seconds after Connected is treated as a token rejection.</summary>
        public const float AuthGraceSeconds = 3f;

        private float phaseTime;

        public SetupPhase Phase { get; private set; } = SetupPhase.PermissionExplanation;
        public SetupFailure Failure { get; private set; }
        public StackChanConnectionState LinkState { get; private set; } = StackChanConnectionState.Disconnected;

        /// <summary>False while the link is down; feeding judgement should be paused.</summary>
        public bool IsLinkConnected => LinkState == StackChanConnectionState.Connected;

        public event Action<SetupPhase, SetupFailure> PhaseChanged;
        public event Action<StackChanConnectionState> LinkStateChanged;

        /// <param name="permissionsAlreadyGranted">Skip the explanation (2nd launch).</param>
        public SetupFlow(bool permissionsAlreadyGranted = false)
        {
            if (permissionsAlreadyGranted) Phase = SetupPhase.LookAtFace;
        }

        /// <summary>User pressed "OK" on the permission explanation.</summary>
        public void AcceptPermissionExplanation()
        {
            if (Phase == SetupPhase.PermissionExplanation) Enter(SetupPhase.LookAtFace);
        }

        /// <summary>A QR candidate is visible (pose not fixed yet).</summary>
        public void ReportQrCandidate()
        {
            if (Phase == SetupPhase.LookAtFace || (Phase == SetupPhase.Failed && IsQrFailure(Failure)))
                Enter(SetupPhase.Detecting);
        }

        /// <summary>Pose fixed from the QR.</summary>
        public void ReportQrDetected()
        {
            if (Phase == SetupPhase.LookAtFace || Phase == SetupPhase.Detecting || Phase == SetupPhase.Failed)
                Enter(SetupPhase.Detected);
        }

        public void ReportQrProblem(SetupFailure problem)
        {
            if (!IsQrFailure(problem)) return;
            if (Phase == SetupPhase.LookAtFace || Phase == SetupPhase.Detecting)
                Enter(SetupPhase.Failed, problem);
        }

        /// <summary>Recalibration requested (menu) or retry after a failure. Back to looking at the face.</summary>
        public void Restart() => Enter(SetupPhase.LookAtFace);

        /// <summary>
        /// The QR scan was cancelled (Y button) while the link is up: the previous pose stays valid, so go back to Connected.
        /// Does nothing in other phases.
        /// </summary>
        public void CancelRecalibration()
        {
            if (!IsLinkConnected) return;
            if (Phase == SetupPhase.LookAtFace || Phase == SetupPhase.Detecting
                || (Phase == SetupPhase.Failed && IsQrFailure(Failure)))
                Enter(SetupPhase.Connected);
        }

        public void OnLinkState(StackChanConnectionState state)
        {
            // Re-applying the same state is allowed: it re-evaluates the phase (e.g. link was already up when the pose got fixed).
            var previous = LinkState;
            LinkState = state;
            if (state != previous) LinkStateChanged?.Invoke(state);

            switch (state)
            {
                case StackChanConnectionState.Connected:
                    if (Phase == SetupPhase.Detected || Phase == SetupPhase.Connecting
                        || (Phase == SetupPhase.Failed && !IsQrFailure(Failure)))
                        Enter(SetupPhase.Connected);
                    break;

                case StackChanConnectionState.Connecting:
                    if (Phase == SetupPhase.Detected) Enter(SetupPhase.Connecting);
                    break;

                case StackChanConnectionState.Reconnecting:
                case StackChanConnectionState.Disconnected:
                    if (Phase == SetupPhase.Detected) Enter(SetupPhase.Connecting);
                    else if (Phase == SetupPhase.Connected && previous == StackChanConnectionState.Connected
                             && phaseTime < AuthGraceSeconds)
                        Enter(SetupPhase.Failed, SetupFailure.TokenRejected);
                    // A later drop is shown by the indicator only; the guide stays done.
                    break;
            }
        }

        public void Tick(float deltaTime)
        {
            phaseTime += deltaTime;
            if (Phase == SetupPhase.Connecting && !IsLinkConnected && phaseTime >= WifiTimeoutSeconds)
                Enter(SetupPhase.Failed, SetupFailure.WifiMismatch);
        }

        private static bool IsQrFailure(SetupFailure f) =>
            f == SetupFailure.QrTooSmall || f == SetupFailure.QrTooDark || f == SetupFailure.QrGlare;

        private void Enter(SetupPhase phase, SetupFailure failure = SetupFailure.None)
        {
            if (phase == Phase && failure == Failure) return;
            Phase = phase;
            Failure = failure;
            phaseTime = 0f;
            PhaseChanged?.Invoke(phase, failure);
        }
    }
}
