using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using RikuK0j1ma.RaisingStackChan.Entity;
using RikuK0j1ma.RaisingStackChan.UseCase;
using UnityEngine;

namespace RikuK0j1ma.RaisingStackChan.Tests.EditMode
{
    public sealed class FaceCalibrationUseCaseTests
    {
        private sealed class FakeStore : IFaceAnchorStore
        {
            public bool HasSaved;
            public Pose Saved;
            public readonly List<string> Calls = new List<string>();

            public UniTask<bool> SaveAsync(Pose facePose, CancellationToken ct)
            {
                Calls.Add("save");
                HasSaved = true;
                Saved = facePose;
                return UniTask.FromResult(true);
            }

            public UniTask<FaceAnchorRestoreResult> RestoreAsync(CancellationToken ct)
            {
                Calls.Add("restore");
                return UniTask.FromResult(HasSaved ? new FaceAnchorRestoreResult(true, Saved) : FaceAnchorRestoreResult.Failed);
            }

            public UniTask DiscardAsync(CancellationToken ct)
            {
                Calls.Add("discard");
                HasSaved = false;
                return UniTask.CompletedTask;
            }

            public bool TryGetPose(out Pose pose) { pose = Saved; return HasSaved; }
        }

        private sealed class FakeLink : IStackChanLink
        {
            public int ConnectCount;
            public StackChanConnectionState State => StackChanConnectionState.Connected;
            public bool IsConnected => true;
            public event Action<StackChanConnectionState> StateChanged { add { } remove { } }
            public string LastError => null;
            public bool TrySetEndpointFromQr(string payload) => false;
            public UniTask ConnectAsync(CancellationToken ct) { ConnectCount++; return UniTask.CompletedTask; }
            public UniTask SendEmotionAsync(StackChanEmotion emotion, int durationMs, CancellationToken ct) => UniTask.CompletedTask;
            public UniTask SendShowQrAsync(CancellationToken ct) => UniTask.CompletedTask;
        }

        private sealed class FakeGuide : ICalibrationGuide
        {
            public bool Visible;
            public void ShowQrGuide() => Visible = true;
            public void HideQrGuide() => Visible = false;
        }

        private FakeStore store;
        private FakeLink link;
        private FakeGuide guide;
        private FaceCalibrationUseCase useCase;

        [SetUp]
        public void SetUp()
        {
            store = new FakeStore();
            link = new FakeLink();
            guide = new FakeGuide();
            useCase = new FaceCalibrationUseCase(store, link, guide);
        }

        [Test]
        public void Start_WithoutAnchor_ShowsQrGuideAndDoesNotConnect()
        {
            var result = useCase.StartAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(CalibrationStartResult.NeedsQr, result);
            Assert.IsTrue(guide.Visible);
            Assert.AreEqual(0, link.ConnectCount);
        }

        [Test]
        public void Start_WithAnchor_ConfirmsPoseAndConnectsOnly()
        {
            store.HasSaved = true;
            store.Saved = new Pose(new Vector3(1, 2, 3), Quaternion.identity);
            guide.Visible = true;
            Pose? confirmed = null;
            useCase.FacePoseConfirmed += p => confirmed = p;

            var result = useCase.StartAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(CalibrationStartResult.Restored, result);
            Assert.IsFalse(guide.Visible);
            Assert.AreEqual(1, link.ConnectCount);
            Assert.AreEqual(new Vector3(1, 2, 3), confirmed.Value.position);
        }

        [Test]
        public void PoseDetected_ReplacesAnchorAndHidesGuide()
        {
            guide.Visible = true;
            var pose = new Pose(Vector3.one, Quaternion.identity);

            var saved = useCase.OnFacePoseDetectedAsync(pose, CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsTrue(saved);
            Assert.IsFalse(guide.Visible);
            CollectionAssert.AreEqual(new[] { "discard", "save" }, store.Calls);
            Assert.AreEqual(Vector3.one, store.Saved.position);
        }

        [Test]
        public void Recalibrate_DiscardsAnchorAndShowsGuide()
        {
            store.HasSaved = true;

            useCase.RecalibrateAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsFalse(store.HasSaved);
            Assert.IsTrue(guide.Visible);
        }
    }
}
