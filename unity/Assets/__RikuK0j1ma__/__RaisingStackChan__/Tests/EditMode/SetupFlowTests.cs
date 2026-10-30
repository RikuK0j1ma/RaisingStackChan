using NUnit.Framework;
using RikuK0j1ma.RaisingStackChan.Entity;
using RikuK0j1ma.RaisingStackChan.UseCase;

namespace RikuK0j1ma.RaisingStackChan.Tests.EditMode
{
    public sealed class SetupFlowTests
    {
        private static SetupFlow Ready()
        {
            var f = new SetupFlow();
            f.AcceptPermissionExplanation();
            return f;
        }

        [Test]
        public void HappyPath_ReachesConnected()
        {
            var f = Ready();
            Assert.AreEqual(SetupPhase.LookAtFace, f.Phase);
            f.ReportQrCandidate();
            Assert.AreEqual(SetupPhase.Detecting, f.Phase);
            f.ReportQrDetected();
            Assert.AreEqual(SetupPhase.Detected, f.Phase);
            f.OnLinkState(StackChanConnectionState.Connecting);
            Assert.AreEqual(SetupPhase.Connecting, f.Phase);
            f.OnLinkState(StackChanConnectionState.Connected);
            Assert.AreEqual(SetupPhase.Connected, f.Phase);
            Assert.IsTrue(f.IsLinkConnected);
        }

        [Test]
        public void LinkAlreadyConnected_WhenPoseFixed_GoesToConnected()
        {
            var f = Ready();
            f.OnLinkState(StackChanConnectionState.Connected);
            f.ReportQrDetected();
            f.OnLinkState(StackChanConnectionState.Connected);
            Assert.AreEqual(SetupPhase.Connected, f.Phase);
        }

        [Test]
        public void QrProblem_FailsThenRecoversOnCandidate()
        {
            var f = Ready();
            f.ReportQrProblem(SetupFailure.QrTooDark);
            Assert.AreEqual(SetupPhase.Failed, f.Phase);
            Assert.AreEqual(SetupFailure.QrTooDark, f.Failure);
            f.ReportQrCandidate();
            Assert.AreEqual(SetupPhase.Detecting, f.Phase);
            Assert.AreEqual(SetupFailure.None, f.Failure);
        }

        [Test]
        public void NonQrFailure_IsIgnoredAsQrProblem()
        {
            var f = Ready();
            f.ReportQrProblem(SetupFailure.WifiMismatch);
            Assert.AreEqual(SetupPhase.LookAtFace, f.Phase);
        }

        [Test]
        public void ConnectingTimeout_BecomesWifiMismatch()
        {
            var f = Ready();
            f.ReportQrDetected();
            f.OnLinkState(StackChanConnectionState.Connecting);
            f.Tick(SetupFlow.WifiTimeoutSeconds - 1f);
            Assert.AreEqual(SetupPhase.Connecting, f.Phase);
            f.Tick(2f);
            Assert.AreEqual(SetupPhase.Failed, f.Phase);
            Assert.AreEqual(SetupFailure.WifiMismatch, f.Failure);
        }

        [Test]
        public void DropRightAfterConnect_IsTokenRejected()
        {
            var f = Ready();
            f.ReportQrDetected();
            f.OnLinkState(StackChanConnectionState.Connected);
            f.Tick(0.5f);
            f.OnLinkState(StackChanConnectionState.Reconnecting);
            Assert.AreEqual(SetupPhase.Failed, f.Phase);
            Assert.AreEqual(SetupFailure.TokenRejected, f.Failure);
        }

        [Test]
        public void LateDrop_KeepsGuideDone_ButLinkIsDown()
        {
            var f = Ready();
            f.ReportQrDetected();
            f.OnLinkState(StackChanConnectionState.Connected);
            f.Tick(SetupFlow.AuthGraceSeconds + 1f);
            f.OnLinkState(StackChanConnectionState.Reconnecting);
            Assert.AreEqual(SetupPhase.Connected, f.Phase);
            Assert.IsFalse(f.IsLinkConnected);
        }

        [Test]
        public void Restart_ReturnsToLookAtFace()
        {
            var f = Ready();
            f.ReportQrDetected();
            f.OnLinkState(StackChanConnectionState.Connected);
            f.Restart();
            Assert.AreEqual(SetupPhase.LookAtFace, f.Phase);
        }

        [Test]
        public void PermissionsAlreadyGranted_SkipsExplanation()
        {
            Assert.AreEqual(SetupPhase.LookAtFace, new SetupFlow(true).Phase);
        }
    }
}
