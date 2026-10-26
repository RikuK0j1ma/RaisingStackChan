using System.Collections.Generic;
using NUnit.Framework;
using RikuK0j1ma.RaisingStackChan.UseCase;
using UnityEngine;

namespace RikuK0j1ma.RaisingStackChan.Tests.EditMode
{
    public sealed class LookTargetSelectorTests
    {
        private static LookCandidate C(int id, float x) => new LookCandidate(id, new Vector3(x, 0f, 0f));

        [Test]
        public void NoSpheres_LooksAtHeadset()
        {
            Assert.AreEqual(LookTargetSelector.HeadsetId,
                LookTargetSelector.Select(Vector3.zero, new List<LookCandidate>(), 3));
        }

        [Test]
        public void SingleSphere_IsChosen()
        {
            Assert.AreEqual(5, LookTargetSelector.Select(Vector3.zero, new List<LookCandidate> { C(5, 1f) },
                LookTargetSelector.HeadsetId));
        }

        [Test]
        public void Nearest_IsChosenWhenNoCurrent()
        {
            var list = new List<LookCandidate> { C(1, 2f), C(2, 0.5f), C(3, 1f) };
            Assert.AreEqual(2, LookTargetSelector.Select(Vector3.zero, list, LookTargetSelector.HeadsetId));
        }

        [Test]
        public void NearlyEqualDistance_KeepsCurrent()
        {
            // 0.50 vs 0.49: only 1 cm / 2% closer.
            var list = new List<LookCandidate> { C(1, 0.5f), C(2, 0.49f) };
            Assert.AreEqual(1, LookTargetSelector.Select(Vector3.zero, list, 1));
        }

        [Test]
        public void MuchCloser_Switches()
        {
            // 1.0 vs 0.5: 50% and 50 cm closer.
            var list = new List<LookCandidate> { C(1, 1f), C(2, 0.5f) };
            Assert.AreEqual(2, LookTargetSelector.Select(Vector3.zero, list, 1));
        }

        [Test]
        public void CloseRange_NeedsAbsoluteMargin()
        {
            // 0.10 vs 0.06 (4 cm closer) switches; 0.10 vs 0.08 (2 cm, under the 3 cm margin) does not.
            Assert.AreEqual(2, LookTargetSelector.Select(Vector3.zero,
                new List<LookCandidate> { C(1, 0.10f), C(2, 0.06f) }, 1));
            Assert.AreEqual(1, LookTargetSelector.Select(Vector3.zero,
                new List<LookCandidate> { C(1, 0.10f), C(2, 0.08f) }, 1));
        }

        [Test]
        public void CurrentGone_SwitchesToNearestImmediately()
        {
            var list = new List<LookCandidate> { C(2, 0.50f), C(3, 0.49f) };
            Assert.AreEqual(3, LookTargetSelector.Select(Vector3.zero, list, 1));
        }

        [Test]
        public void LastSphereEaten_ReturnsToHeadset()
        {
            Assert.AreEqual(LookTargetSelector.HeadsetId,
                LookTargetSelector.Select(Vector3.zero, new List<LookCandidate>(), 1));
        }

        [Test]
        public void DistanceIsMeasuredFromOrigin()
        {
            var list = new List<LookCandidate> { C(1, 0f), C(2, 10f) };
            Assert.AreEqual(2, LookTargetSelector.Select(new Vector3(10f, 0f, 0f), list, LookTargetSelector.HeadsetId));
        }
    }
}
