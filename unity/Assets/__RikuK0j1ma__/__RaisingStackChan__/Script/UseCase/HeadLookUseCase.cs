using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using RikuK0j1ma.RaisingStackChan.Entity;
using UnityEngine;

namespace RikuK0j1ma.RaisingStackChan.UseCase
{
    /// <summary>
    /// While connected, sends the direction from the StackChan face to the headset ("look") so StackChan turns its
    /// neck toward the player. Sends nothing while disconnected, while the QR is being shown (setup guide not in the
    /// Connected phase: first connect, recalibration), or while the face pose or headset is unknown.
    /// While food spheres are out it looks at the one nearest to the mouth instead (see LookTargetSelector).
    /// Angles are low-pass filtered and sent at a limited rate. StackChan stops in place if "look" stops arriving,
    /// so it keeps sending at that rate even when the head is still.
    /// </summary>
    public sealed class HeadLookUseCase : IDisposable
    {
        /// <summary>Low-pass time constant in seconds.</summary>
        public const float SmoothingSeconds = 0.15f;

        /// <summary>Seconds between messages (about 15 Hz).</summary>
        public const float SendIntervalSeconds = 1f / 15f;

        private readonly IStackChanLink link;
        private readonly IStackChanPoseProvider pose;
        private readonly IHeadPoseProvider head;
        private readonly SetupGuideUseCase guide;
        private readonly ISphereSpawner spheres;
        private readonly StackChanRigConfig rig;
        private readonly List<int> ids = new List<int>();
        private readonly List<LookCandidate> candidates = new List<LookCandidate>();
        private int currentSphereId = LookTargetSelector.HeadsetId;
        private readonly CancellationTokenSource cts = new CancellationTokenSource();

        private bool hasSmoothed;
        private float yaw;
        private float pitch;
        private float sinceSend;
        private bool sending;

        public HeadLookUseCase(IStackChanLink link, IStackChanPoseProvider pose, IHeadPoseProvider head,
            SetupGuideUseCase guide, ISphereSpawner spheres, StackChanRigConfig rig)
        {
            this.rig = rig;
            this.spheres = spheres;
            this.link = link;
            this.pose = pose;
            this.head = head;
            this.guide = guide;
        }

        /// <summary>True while "look" messages are being sent.</summary>
        public bool IsActive { get; private set; }

        /// <summary>Last smoothed angles (degrees), for diagnostics.</summary>
        public float LastYawDeg => yaw;

        public float LastPitchDeg => pitch;

        /// <summary>True while the target is a food sphere, false while it is the headset (valid while <see cref="IsActive"/>).</summary>
        public bool IsLookingAtFood { get; private set; }

        /// <summary>What StackChan is looking at (world position) while <see cref="IsActive"/>, for the scene view.</summary>
        public Vector3 LastLookAt { get; private set; }

        /// <summary>
        /// StackChan's frame (neck pivot and zero direction) from the face pose, shown by the scene view.
        /// False while the face pose is unknown.
        /// </summary>
        public bool TryGetFrame(out StackChanFrame frame)
        {
            if (pose.TryGetFacePose(out Pose face))
            {
                frame = StackChanFrame.From(face, rig);
                return true;
            }

            frame = default;
            return false;
        }

        /// <summary>Call every frame.</summary>
        public void Tick(float deltaTime)
        {
            if (!TryComputeTarget(out float targetYaw, out float targetPitch))
            {
                IsActive = false;
                hasSmoothed = false;
                sinceSend = SendIntervalSeconds; // send right away once active again
                return;
            }

            IsActive = true;
            if (!hasSmoothed)
            {
                yaw = targetYaw;
                pitch = targetPitch;
                hasSmoothed = true;
            }
            else
            {
                yaw = HeadLookMath.SmoothAngle(yaw, targetYaw, deltaTime, SmoothingSeconds);
                pitch = HeadLookMath.SmoothAngle(pitch, targetPitch, deltaTime, SmoothingSeconds);
            }

            sinceSend += deltaTime;
            if (sinceSend < SendIntervalSeconds || sending) return;
            sinceSend = 0f;
            SendAsync(yaw, pitch).Forget();
        }

        public void Dispose()
        {
            cts.Cancel();
            cts.Dispose();
        }

        private bool TryComputeTarget(out float targetYaw, out float targetPitch)
        {
            targetYaw = 0f;
            targetPitch = 0f;
            if (!link.IsConnected) return false;
            if (guide.Flow.Phase != SetupPhase.Connected) return false;
            if (!pose.TryGetFacePose(out Pose face)) return false;
            if (!head.TryGetHeadPosition(out Vector3 headPosition)) return false;

            Vector3 lookAt = headPosition;
            IsLookingAtFood = TrySelectSphere(face, out Vector3 spherePosition);
            if (IsLookingAtFood) lookAt = spherePosition;

            LastLookAt = lookAt;
            StackChanFrame.From(face, rig).Compute(lookAt, out targetYaw, out targetPitch);
            return true;
        }

        /// <summary>Picks the food sphere to look at (nearest to the mouth, with hysteresis). False: look at the headset.</summary>
        private bool TrySelectSphere(Pose face, out Vector3 position)
        {
            position = Vector3.zero;
            Vector3 origin = pose.TryGetMouthPosition(out Vector3 mouth) ? mouth : face.position;

            spheres.GetActiveIds(ids);
            candidates.Clear();
            foreach (int id in ids)
            {
                if (spheres.TryGetPosition(id, out Vector3 p)) candidates.Add(new LookCandidate(id, p));
            }

            currentSphereId = LookTargetSelector.Select(origin, candidates, currentSphereId);
            if (currentSphereId == LookTargetSelector.HeadsetId) return false;
            return spheres.TryGetPosition(currentSphereId, out position);
        }

        private async UniTaskVoid SendAsync(float yawDeg, float pitchDeg)
        {
            sending = true;
            try
            {
                await link.SendLookAsync(yawDeg, pitchDeg, cts.Token);
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { Debug.LogWarning($"[HeadLook] send failed: {e.Message}"); }
            finally { sending = false; }
        }
    }
}
