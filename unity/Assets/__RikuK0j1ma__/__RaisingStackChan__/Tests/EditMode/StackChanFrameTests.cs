using NUnit.Framework;
using RikuK0j1ma.RaisingStackChan.Entity;
using RikuK0j1ma.RaisingStackChan.UseCase;
using UnityEngine;

namespace RikuK0j1ma.RaisingStackChan.Tests.EditMode
{
    public sealed class StackChanFrameTests
    {
        private const float Tolerance = 0.01f;
        private static readonly Pose Identity = new Pose(Vector3.zero, Quaternion.identity);

        [Test]
        public void FromFace_StraightAhead_IsZero()
        {
            StackChanFrame.FromFace(Identity).Compute(new Vector3(0f, 0f, 1f), out float yaw, out float pitch);
            Assert.AreEqual(0f, yaw, Tolerance);
            Assert.AreEqual(0f, pitch, Tolerance);
        }

        [Test]
        public void FromFace_RightAndUp_ArePositive()
        {
            StackChanFrame.FromFace(Identity).Compute(new Vector3(1f, 0f, 1f), out float yaw, out _);
            Assert.AreEqual(45f, yaw, Tolerance);

            StackChanFrame.FromFace(Identity).Compute(new Vector3(0f, 1f, 1f), out _, out float pitch);
            Assert.AreEqual(45f, pitch, Tolerance);
        }

        [Test]
        public void FromFace_FollowsFacePoseRotationAndPosition()
        {
            var face = new Pose(new Vector3(1f, 2f, 3f), Quaternion.Euler(0f, 90f, 0f));
            Vector3 target = face.position + face.rotation * new Vector3(0.5f, 0.2f, 1f);

            StackChanFrame.FromFace(face).Compute(target, out float yaw, out float pitch);

            Assert.AreEqual(Mathf.Atan2(0.5f, 1f) * Mathf.Rad2Deg, yaw, Tolerance);
            Assert.AreEqual(Mathf.Atan2(0.2f, Mathf.Sqrt(0.5f * 0.5f + 1f)) * Mathf.Rad2Deg, pitch, Tolerance);
        }

        [Test]
        public void HeadLookMath_Compute_MatchesFromFace()
        {
            var face = new Pose(new Vector3(0.3f, 1f, -0.2f), Quaternion.Euler(10f, 35f, 0f));
            var target = new Vector3(-0.4f, 1.3f, 0.9f);

            HeadLookMath.Compute(face, target, out float yaw, out float pitch);
            StackChanFrame.FromFace(face).Compute(target, out float expectedYaw, out float expectedPitch);

            Assert.AreEqual(expectedYaw, yaw, 1e-4f);
            Assert.AreEqual(expectedPitch, pitch, 1e-4f);
        }

        [Test]
        public void DefaultConfig_ReproducesTheOldBehaviour()
        {
            var config = ScriptableObject.CreateInstance<StackChanRigConfig>();
            try
            {
                var face = new Pose(new Vector3(0.1f, 1.2f, 0.4f), Quaternion.Euler(0f, 20f, 0f));
                var target = new Vector3(0.5f, 1.0f, 1.2f);

                StackChanFrame.From(face, config).Compute(target, out float yaw, out float pitch);
                StackChanFrame.FromFace(face).Compute(target, out float expectedYaw, out float expectedPitch);

                Assert.AreEqual(expectedYaw, yaw, 1e-4f);
                Assert.AreEqual(expectedPitch, pitch, 1e-4f);
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void NullConfig_FallsBackToTheFaceCenter()
        {
            var target = new Vector3(0.2f, 0f, 0.4f);

            StackChanFrame.From(Identity, null).Compute(target, out float yaw, out _);

            Assert.AreEqual(Mathf.Atan2(0.2f, 0.4f) * Mathf.Rad2Deg, yaw, Tolerance);
        }

        [Test]
        public void PivotBehindTheFace_ChangesTheAngleForANearTarget()
        {
            // The neck axis is 10 cm behind the face center, so a near target is seen at a smaller angle from it.
            var frame = StackChanFrame.From(Identity, new Vector3(0f, 0f, -0.1f), 0f, 0f, false, false);

            frame.Compute(new Vector3(0.2f, 0f, 0.4f), out float yaw, out _);

            Assert.AreEqual(Mathf.Atan2(0.2f, 0.5f) * Mathf.Rad2Deg, yaw, Tolerance);
            Assert.Less(yaw, Mathf.Atan2(0.2f, 0.4f) * Mathf.Rad2Deg);
        }

        [Test]
        public void PivotOffset_IsInTheFaceLocalSpace()
        {
            var face = new Pose(new Vector3(1f, 0f, 0f), Quaternion.Euler(0f, 90f, 0f));
            // Face local -Z (behind the face) is world -X after the 90 degree turn: the pivot is at x = 0.9.
            var frame = StackChanFrame.From(face, new Vector3(0f, 0f, -0.1f), 0f, 0f, false, false);

            Assert.AreEqual(0.9f, frame.Pivot.x, 1e-4f);
            Assert.AreEqual(0f, frame.Pivot.y, 1e-4f);
            Assert.AreEqual(0f, frame.Pivot.z, 1e-4f);
        }

        [Test]
        public void YawZeroOffset_TurnsTheZeroDirectionToTheRight()
        {
            var frame = StackChanFrame.From(Identity, Vector3.zero, 10f, 0f, false, false);

            // Servo zero now points 10 degrees right, so something straight ahead is 10 degrees to its left.
            frame.Compute(new Vector3(0f, 0f, 1f), out float yaw, out float pitch);
            Assert.AreEqual(-10f, yaw, Tolerance);
            Assert.AreEqual(0f, pitch, Tolerance);

            // And something 10 degrees to the right is exactly at the zero direction.
            frame.Compute(new Vector3(Mathf.Tan(10f * Mathf.Deg2Rad), 0f, 1f), out yaw, out _);
            Assert.AreEqual(0f, yaw, Tolerance);
        }

        [Test]
        public void PitchZeroOffset_TiltsTheZeroDirectionUp()
        {
            var frame = StackChanFrame.From(Identity, Vector3.zero, 0f, 10f, false, false);

            frame.Compute(new Vector3(0f, 0f, 1f), out float yaw, out float pitch);

            Assert.AreEqual(0f, yaw, Tolerance);
            Assert.AreEqual(-10f, pitch, Tolerance);
        }

        [Test]
        public void Invert_FlipsOnlyTheChosenAxis()
        {
            var target = new Vector3(0.5f, 0.3f, 1f);
            StackChanFrame.From(Identity, Vector3.zero, 0f, 0f, false, false).Compute(target, out float yaw, out float pitch);

            StackChanFrame.From(Identity, Vector3.zero, 0f, 0f, true, false).Compute(target, out float yawFlipped, out float pitchSame);
            Assert.AreEqual(-yaw, yawFlipped, 1e-4f);
            Assert.AreEqual(pitch, pitchSame, 1e-4f);

            StackChanFrame.From(Identity, Vector3.zero, 0f, 0f, false, true).Compute(target, out float yawSame, out float pitchFlipped);
            Assert.AreEqual(yaw, yawSame, 1e-4f);
            Assert.AreEqual(-pitch, pitchFlipped, 1e-4f);
        }
    }
}
