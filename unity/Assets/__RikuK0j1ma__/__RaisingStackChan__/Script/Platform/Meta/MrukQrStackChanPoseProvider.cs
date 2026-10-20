using System;
using System.Collections.Generic;
using Meta.XR.MRUtilityKit;
using RikuK0j1ma.RaisingStackChan.Entity;
using RikuK0j1ma.RaisingStackChan.UseCase;
using UnityEngine;
using VContainer.Unity;

namespace RikuK0j1ma.RaisingStackChan.Platform.Meta
{
    /// <summary>
    /// Detects the QR code shown by StackChan (MR LINK) through MRUK trackables and exposes it as the
    /// face pose. QR center = face center; the mouth is EatConfig.MouthLocalOffset in the face space.
    /// The QR payload (ws://ip:port/ws?token=...) is handed to IStackChanLink.TrySetEndpointFromQr;
    /// QR codes that are not a StackChan endpoint are ignored.
    /// Needs an MRUK component and OVRCameraRig in the scene and the Scene permission.
    /// The pose is kept after the QR disappears (StackChan hides it once connected).
    /// </summary>
    public sealed class MrukQrStackChanPoseProvider : IStackChanPoseProvider, IInitializable, ITickable, IDisposable
    {
        private readonly IStackChanLink link;
        private readonly EatConfig eatConfig;

        private MRUK mruk;
        private MRUKTrackable current;
        private bool confirmed;
        private bool hasPose;
        private Pose facePose;

        public MrukQrStackChanPoseProvider(IStackChanLink link, EatConfig eatConfig)
        {
            this.link = link;
            this.eatConfig = eatConfig;
        }

        /// <summary>
        /// Raised on the main thread when a StackChan QR is first seen as tracked (again), with the face
        /// pose. Subscribe from the anchor feature (#10) to persist it.
        /// </summary>
        public event Action<Pose> FacePoseConfirmed;

        /// <summary>True when MRUK exists in the scene and QR tracking was requested.</summary>
        public bool IsActive => mruk != null;

        public void Initialize()
        {
            mruk = MRUK.Instance;
            if (mruk == null)
            {
                Debug.LogWarning("MRUK is not in the scene; QR detection is disabled.");
                return;
            }

            var settings = mruk.SceneSettings;
            var config = settings.TrackerConfiguration;
            config.QRCodeTrackingEnabled = true;
            settings.TrackerConfiguration = config;
            settings.TrackableAdded.AddListener(OnTrackableAdded);
            settings.TrackableRemoved.AddListener(OnTrackableRemoved);

            // Trackables found before this ran (MRUK may start first).
            var existing = new List<MRUKTrackable>();
            mruk.GetTrackables(existing);
            foreach (var t in existing) OnTrackableAdded(t);
        }

        public void Dispose()
        {
            if (mruk == null) return;
            var settings = mruk.SceneSettings;
            settings.TrackableAdded.RemoveListener(OnTrackableAdded);
            settings.TrackableRemoved.RemoveListener(OnTrackableRemoved);
            mruk = null;
        }

        public void Tick()
        {
            if (current == null || !current.IsTracked) return;

            var t = current.transform;
            facePose = new Pose(t.position, t.rotation);
            hasPose = true;
            if (confirmed) return;
            confirmed = true;
            FacePoseConfirmed?.Invoke(facePose);
        }

        /// <summary>Uses a pose restored from a saved anchor, without raising <see cref="FacePoseConfirmed"/>.</summary>
        public void ApplyRestoredPose(Pose pose)
        {
            facePose = pose;
            hasPose = true;
            confirmed = true;
        }

        /// <summary>Recalibration: the next tracked QR raises <see cref="FacePoseConfirmed"/> again.</summary>
        public void ResetConfirmation() => confirmed = false;

        public bool TryGetFacePose(out Pose pose)
        {
            pose = facePose;
            return hasPose;
        }

        public bool TryGetMouthPosition(out Vector3 position)
        {
            position = hasPose ? facePose.position + facePose.rotation * eatConfig.MouthLocalOffset : Vector3.zero;
            return hasPose;
        }

        private void OnTrackableAdded(MRUKTrackable trackable)
        {
            if (trackable == null || trackable.TrackableType != OVRAnchor.TrackableType.QRCode) return;
            if (!link.TrySetEndpointFromQr(trackable.MarkerPayloadString)) return;

            current = trackable;
            confirmed = false;
        }

        private void OnTrackableRemoved(MRUKTrackable trackable)
        {
            if (trackable == current) current = null;
        }
    }
}
